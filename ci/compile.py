#!/usr/bin/env python3
"""TomCatRunner CI - C# compiler driver.

Compiles every assembly listed in ci/assemblies.json with the Roslyn csc
from the .NET SDK, using the reference DLLs prepared by ci/fetch_refs.py.
This mirrors how Unity itself compiles asmdef assemblies inside the editor
(same reference assemblies, same UNITY_* defines) - but requires NO Unity
license, so it runs on any CI machine.

Exit code 0 + "ZERO ERRORS" banner = all assemblies compiled.

Usage: python3 ci/compile.py [--only NAME] [--list] [--dotnet PATH]
"""
import argparse
import glob
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REFS = os.path.join(ROOT, "refs")
BUILD = os.path.join(REFS, "build")
MANIFEST = os.path.join(HERE, "assemblies.json")

ENGINE_DIR = os.path.join(REFS, "unity", "Editor", "Data", "Managed", "UnityEngine")
NETSTANDARD = os.path.join(REFS, "unity", "Editor", "Data", "NetStandard", "ref", "2.1.0", "netstandard.dll")

BASE_DEFINES = [
    "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_LINUX",
    "UNITY_STANDALONE", "UNITY_STANDALONE_LINUX",
    "UNITY_64", "UNITY_2022", "UNITY_2022_3", "UNITY_2022_3_62",
    "UNITY_ASSERTIONS", "ENABLE_MONO", "NET_STANDARD_2_0",
    # Unity auto-defines the *_OR_NEWER set for the editor version
    "UNITY_2017_1_OR_NEWER", "UNITY_2017_2_OR_NEWER", "UNITY_2017_3_OR_NEWER",
    "UNITY_2017_4_OR_NEWER", "UNITY_2018_1_OR_NEWER", "UNITY_2018_2_OR_NEWER",
    "UNITY_2018_3_OR_NEWER", "UNITY_2018_4_OR_NEWER",
    "UNITY_2019_1_OR_NEWER", "UNITY_2019_2_OR_NEWER", "UNITY_2019_3_OR_NEWER",
    "UNITY_2019_4_OR_NEWER", "UNITY_2020_1_OR_NEWER", "UNITY_2020_2_OR_NEWER",
    "UNITY_2020_3_OR_NEWER", "UNITY_2021_1_OR_NEWER", "UNITY_2021_2_OR_NEWER",
    "UNITY_2021_3_OR_NEWER", "UNITY_2022_1_OR_NEWER", "UNITY_2022_2_OR_NEWER",
    "UNITY_2022_3_OR_NEWER",
]


def find_csc(dotnet_hint=None):
    candidates = []
    if dotnet_hint:
        candidates.append(dotnet_hint)
    for env in ("DOTNET_ROOT",):
        if os.environ.get(env):
            candidates.append(os.path.join(os.environ[env], "dotnet"))
    candidates += [
        "/home/z/my-project/tools/dotnet/dotnet",
        os.path.expanduser("~/.dotnet/dotnet"),
        "/usr/share/dotnet/dotnet",
        "/usr/lib/dotnet/dotnet",
        "dotnet",
    ]
    for dotnet in candidates:
        try:
            out = subprocess.run([dotnet, "--list-sdks"], capture_output=True,
                                 text=True, timeout=60)
        except Exception:
            continue
        if out.returncode != 0:
            continue
        sdk_root = os.path.dirname(os.path.abspath(
            subprocess.run([dotnet, "--info"], capture_output=True, text=True)
            .stdout.split() and dotnet or dotnet))
        # base dir of dotnet executable
        base = os.path.dirname(os.path.abspath(
            subprocess.run(["which", dotnet], capture_output=True, text=True).stdout.strip()
            or dotnet))
        sdks = sorted(glob.glob(os.path.join(base, "sdk", "*")), reverse=True)
        for sdk in sdks:
            csc = os.path.join(sdk, "Roslyn", "bincore", "csc.dll")
            if os.path.exists(csc):
                return dotnet, csc
    raise RuntimeError("could not locate the .NET SDK / Roslyn csc - install "
                       "dotnet-sdk-8.0 or pass --dotnet")


def engine_refs():
    refs = [NETSTANDARD]
    for dll in sorted(glob.glob(os.path.join(ENGINE_DIR, "*.dll"))):
        refs.append(dll)
    # .NET facade / compat shims - needed so precompiled DLLs built against
    # .NET Framework (nunit, Burst.Unsafe, DOTween) unify with netstandard.
    shims = os.path.join(REFS, "unity", "Editor", "Data", "NetStandard",
                         "compat", "2.1.0", "shims")
    for sub in ("netstandard", "netfx"):
        for dll in sorted(glob.glob(os.path.join(shims, sub, "*.dll"))):
            refs.append(dll)
    return refs


def editor_refs():
    return engine_refs()  # engine dir already contains UnityEditor*.dll


def collect_sources(entry):
    files = []
    for pattern in entry["sources"]:
        files.extend(glob.glob(os.path.join(ROOT, pattern), recursive=True))
    excluded = []
    for pattern in entry.get("exclude", []):
        excluded.extend(glob.glob(os.path.join(ROOT, pattern), recursive=True))
    ex = set(excluded)
    # exclusion by path prefix (folder excludes)
    out = []
    for f in sorted(set(files)):
        if f in ex:
            continue
        out.append(f)
    return out


class Compiler:
    def __init__(self):
        self.dotnet, self.csc = find_csc()
        self.built = {}
        self.errors = []
        self.warnings = []

    def resolve_refs(self, entry):
        refs = []
        for r in entry["references"]:
            if r == "engine":
                refs.extend(engine_refs())
            elif r == "engine-editor":
                refs.extend(editor_refs())
            elif r in self.built:
                refs.append(self.built[r])
            else:
                # 仓库相对路径（refs/... 或 Assets/... 等）
                p = r if os.path.isabs(r) else os.path.join(ROOT, r)
                if not os.path.exists(p):
                    raise RuntimeError(f"missing reference {r} for {entry['name']}")
                refs.append(p)
        return refs

    def compile(self, entry):
        name = entry["name"]
        out = os.path.join(BUILD, name + ".dll")
        os.makedirs(BUILD, exist_ok=True)
        sources = collect_sources(entry)
        if not sources:
            raise RuntimeError(f"no source files found for {name}")
        refs = self.resolve_refs(entry)
        defines = list(BASE_DEFINES) + list(entry.get("defines", []))
        unsafe = bool(entry.get("unsafe", False))

        rsp = os.path.join(BUILD, name + ".rsp")
        with open(rsp, "w", encoding="utf-8") as f:
            f.write("-noconfig\n-nostdlib+\n-nologo\n")
            f.write("-target:library\n")
            f.write("-langversion:9.0\n")
            f.write(f"-unsafe{'+' if unsafe else '-'}\n")
            f.write("-warn:3\n-warnaserror-\n")
            f.write(f"-define:{';'.join(defines)}\n")
            f.write(f'-out:"{out}"\n')
            for r in refs:
                f.write(f'-r:"{r}"\n')
            for s in sources:
                f.write(f'"{s}"\n')

        cmd = [self.dotnet, "exec", self.csc, "@" + rsp]
        proc = subprocess.run(cmd, capture_output=True, text=True, cwd=ROOT)
        err_lines = [l for l in proc.stderr.splitlines() + proc.stdout.splitlines()
                     if l.strip()]
        n_err = sum(1 for l in err_lines if ": error " in l)
        n_warn = sum(1 for l in err_lines if ": warning " in l)

        status = "OK " if proc.returncode == 0 else "FAIL"
        print(f"[compile] {status} {name}  ({len(sources)} files, "
              f"{n_err} errors, {n_warn} warnings)", flush=True)
        for line in err_lines:
            if ": error " in line or n_err > 0 or os.environ.get("CI_VERBOSE"):
                print("    " + line[:400], flush=True)

        if proc.returncode == 0:
            self.built[name] = out
            return True
        self.errors.append((name, err_lines))
        return False


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", help="compile a single assembly by name")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--dotnet", help="path to the dotnet executable")
    ap.add_argument("--stop-on-error", action="store_true")
    args = ap.parse_args()

    manifest = json.load(open(MANIFEST, encoding="utf-8"))
    entries = manifest["assemblies"]
    if args.list:
        for e in entries:
            print(e["name"])
        return
    if args.only:
        names = [e["name"] for e in entries]
        if args.only not in names:
            print(f"no assembly named {args.only}")
            sys.exit(2)
        # compile every dependency before the target itself
        cut = names.index(args.only) + 1
        entries = entries[:cut]

    os.makedirs(BUILD, exist_ok=True)
    c = Compiler()
    print(f"[compile] compiler: {c.dotnet} ({c.csc})", flush=True)
    ok = 0
    for entry in entries:
        try:
            if c.compile(entry):
                ok += 1
            elif args.stop_on_error:
                break
        except Exception as e:
            print(f"[compile] FAIL {entry['name']}: {e}", flush=True)
            c.errors.append((entry["name"], [str(e)]))
            if args.stop_on_error:
                break

    print()
    if c.errors:
        print("=" * 70)
        print(f"COMPILATION FAILED: {len(c.errors)} assembly(ies) with errors")
        for name, lines in c.errors:
            print(f"\n--- {name} ---")
            for line in lines:
                if ": error " in line or "error CS" in line or len(c.errors) == 1:
                    print("  " + line[:400])
        sys.exit(1)
    print("=" * 70)
    print(f"COMPILATION SUCCEEDED: {ok}/{len(entries)} assemblies, ZERO ERRORS")
    print(f"Output: {BUILD}")
    sys.exit(0)


if __name__ == "__main__":
    main()
