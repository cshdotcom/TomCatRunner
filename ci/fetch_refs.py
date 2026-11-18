#!/usr/bin/env python3
"""TomCatRunner CI - Reference fetcher.

Downloads and prepares every reference needed to compile the project's C#
code with the Roslyn compiler, WITHOUT a Unity license:

  refs/
    unity/Editor/Data/...          - Unity 2022.3.62 editor managed DLLs
    packages/<name>/...            - package sources / prebuilt DLLs
      (com.unity.* from packages.unity.com, third-party from GitHub releases)

The same script runs locally and on GitHub Actions. All steps are
idempotent (skip when already done), and the refs/ folder is meant to be
cached by CI (see workflow). Delete refs/ to force a refetch.

Usage:  python3 ci/fetch_refs.py [--tarball-url URL]
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REFS = os.path.join(ROOT, "refs")

# Bump to invalidate CI caches.
REFS_VERSION = "2022.3.62f3-v3"

DEFAULT_TARBALL_URL = (
    "https://download.unitychina.cn/download_unity/1623fc0bbb97/"
    "LinuxEditorInstaller/Unity-2022.3.62f3c1.tar.xz"
)
# International mirror (redirects); both serve identical content.
ALT_TARBALL_URL = (
    "https://download.unity3d.com/download_unity/1623fc0bbb97/"
    "LinuxEditorInstaller/Unity-2022.3.62f3c1.tar.xz"
)

UNITY_REG = "https://packages.unity.com"

# name -> (source, pin, location)
#   source: "unity"   -> npm registry tarball at packages.unity.com
#           "builtin" -> shipped inside the editor tarball BuiltInPackages
#           "github"  -> codeload tarball of a release tag
PACKAGES = {
    "com.unity.mathematics":            ("unity", "1.2.6", None),
    "com.unity.burst":                  ("unity", "1.8.19", None),
    "com.unity.textmeshpro":            ("unity", "3.0.9", None),
    "com.unity.addressables":           ("unity", "1.22.3", None),
    "com.unity.scriptablebuildpipeline": ("unity", "1.22.2", None),
    "com.unity.test-framework":         ("unity", "1.1.33", None),
    "com.unity.ext.nunit":              ("unity", "1.0.6", None),
    "com.unity.nuget.newtonsoft-json":  ("unity", "3.2.1", None),
    "com.unity.ugui":                   ("builtin", "1.0.0", None),
    "com.neuecc.unirx":                 ("github", "7.1.0", "neuecc/UniRx"),
    "com.cysharp.unitask":              ("github", "2.5.11", "Cysharp/UniTask"),
    "com.hadashikick.vcontainer":       ("github", "1.19.0", "hadashiA/VContainer"),
}


def log(msg):
    print(f"[fetch-refs] {msg}", flush=True)


def http_json(url):
    req = urllib.request.Request(
        url, headers={"Accept": "application/json", "User-Agent": "tcr-ci/1.0"}
    )
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())


def curl(url, out, retries=3):
    subprocess.run(
        ["curl", "-sL", "--retry", str(retries), url, "-o", out], check=True
    )


# --------------------------------------------------------------------------
# Unity editor tarball (parallel chunked download + selective extraction)
# --------------------------------------------------------------------------

def http_head(url):
    out = subprocess.run(
        ["curl", "-sIL", url], capture_output=True, text=True, timeout=90
    ).stdout
    size = None
    for line in out.splitlines():
        if line.lower().startswith("content-length:"):
            size = int(line.split(":", 1)[1].strip())
    return size


def download_chunk(url, start, end, path, max_seconds):
    expected = end - start + 1
    if os.path.exists(path) and os.path.getsize(path) == expected:
        return True
    deadline = time.time() + max_seconds
    while time.time() < deadline:
        have = os.path.getsize(path) if os.path.exists(path) else 0
        if have >= expected:
            break
        subprocess.run(
            [
                "curl", "-s",
                "--speed-limit", "10000", "--speed-time", "30",
                "-r", f"{start + have}-{end}", "-o", path + ".part", url,
            ],
            timeout=max(30, int(deadline - time.time())),
        )
        part = path + ".part"
        if os.path.exists(part):
            with open(path, "ab") as f, open(part, "rb") as p:
                shutil.copyfileobj(p, f, 1 << 20)
            os.remove(part)
    return os.path.exists(path) and os.path.getsize(path) == expected


def fetch_unity(url, max_seconds=520, parallel=12):
    marker = os.path.join(REFS, "unity", ".done")
    if os.path.exists(marker):
        log("unity refs already present, skipping")
        return
    workdir = os.path.join(REFS, "tarball")
    dest = os.path.join(REFS, "unity")
    os.makedirs(workdir, exist_ok=True)
    os.makedirs(dest, exist_ok=True)

    size_file = os.path.join(workdir, "total_size")
    if os.path.exists(size_file):
        total = int(open(size_file).read().strip())
    else:
        total = http_head(url)
        if not total:
            raise RuntimeError(f"could not determine tarball size for {url}")
        open(size_file, "w").write(str(total))
    log(f"unity editor tarball: {total} bytes")

    chunk = 280 * 1024 * 1024
    chunks = []
    start, idx = 0, 0
    while start < total:
        end = min(start + chunk - 1, total - 1)
        chunks.append((idx, start, end))
        start = end + 1
        idx += 1

    t0 = time.time()
    ok_all = True
    with ThreadPoolExecutor(max_workers=parallel) as ex:
        futs = {
            ex.submit(download_chunk, url, s, e,
                      os.path.join(workdir, f"part_{i:03d}"), max_seconds): i
            for i, s, e in chunks
        }
        for n, fut in enumerate(as_completed(futs), 1):
            ok = fut.result()
            ok_all &= ok
            log(f"chunk {n}/{len(chunks)} {'ok' if ok else 'INCOMPLETE'} "
                f"({time.time()-t0:.0f}s)")
    if not ok_all:
        log("some chunks incomplete - rerun this script to resume")
        sys.exit(2)

    log("extracting managed DLLs from tarball ...")
    subprocess.run(
        "cat %s/part_* | tar -xJf - -C %s --wildcards "
        "'Editor/Data/Managed/*' "
        "'Editor/Data/NetStandard/*' "
        "'Editor/Data/UnityReferenceAssemblies/*' "
        "'Editor/Data/Resources/PackageManager/BuiltInPackages/*'" % (workdir, dest),
        shell=True, check=True, executable="/bin/bash",
    )
    open(marker, "w").write(REFS_VERSION)

    # Prune the heavy built-in packages we do not compile, to keep CI
    # caches small (only ugui and its module deps are needed).
    bip = os.path.join(dest, "Editor/Data/Resources/PackageManager/BuiltInPackages")
    keep = {"com.unity.ugui"}
    if os.path.isdir(bip):
        for entry in os.listdir(bip):
            path = os.path.join(bip, entry)
            if entry not in keep:
                if os.path.isdir(path):
                    shutil.rmtree(path, ignore_errors=True)
                elif os.path.isfile(path):
                    os.remove(path)

    shutil.rmtree(workdir, ignore_errors=True)
    log("unity refs ready")


# --------------------------------------------------------------------------
# Packages
# --------------------------------------------------------------------------

def semver_key(v):
    return [int(p) if p.isdigit() else p for p in re.findall(r"\d+|\D+", v)]


def unity_compat(vinfo, target=(2022, 3)):
    u = vinfo.get("unity", "")
    if not u:
        return True
    try:
        parts = u.split(".")
        return (int(parts[0]), int(parts[1])) <= target
    except Exception:
        return True


def resolve_unity_version(name, pin):
    data = http_json(f"{UNITY_REG}/{name}")
    versions = data.get("versions", {})
    if pin in versions and unity_compat(versions[pin]):
        return pin, versions[pin]["dist"]["tarball"]
    best = None
    for v, info in versions.items():
        if "-" in v:
            continue
        if not unity_compat(info):
            continue
        if best is None or semver_key(v) > semver_key(best):
            best = v
    if best is None:
        raise RuntimeError(f"no compatible version found for {name}")
    return best, versions[best]["dist"]["tarball"]


def safe_extract(tgz, dest):
    os.makedirs(dest, exist_ok=True)
    with tarfile.open(tgz, "r:gz") as t:
        members = []
        for m in t.getmembers():
            name = m.name
            if name.startswith("/") or ".." in name.split("/"):
                continue
            members.append(m)
        t.extractall(dest, members=members, filter="data")


def fetch_unity_pkg(name, pin):
    dest = os.path.join(REFS, "packages", name)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if os.path.exists(os.path.join(dest, ".done")):
        return
    ver, tarball = resolve_unity_version(name, pin)
    log(f"{name} -> {ver}")
    tgz = os.path.join(REFS, "packages", f"{name}-{ver}.tgz")
    curl(tarball, tgz)
    safe_extract(tgz, dest)
    open(os.path.join(dest, ".done"), "w").write(ver)
    os.remove(tgz)


def fetch_builtin_pkg(name):
    dest = os.path.join(REFS, "packages", name)
    if os.path.exists(os.path.join(dest, ".done")):
        return
    src = os.path.join(
        REFS, "unity", "Editor/Data/Resources/PackageManager/BuiltInPackages", name
    )
    if not os.path.isdir(src):
        raise RuntimeError(f"built-in package {name} missing from editor refs")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    shutil.copytree(src, dest)
    open(os.path.join(dest, ".done"), "w").write("builtin")


def fetch_github_pkg(name, tag, repo):
    dest = os.path.join(REFS, "packages", name)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if os.path.exists(os.path.join(dest, ".done")):
        return
    log(f"{name} -> github {repo}@{tag}")
    tgz = os.path.join(REFS, "packages", f"{name}.tar.gz")
    curl(f"https://codeload.github.com/{repo}/tar.gz/refs/tags/{tag}", tgz)
    safe_extract(tgz, dest)
    if name == "com.neuecc.unirx":
        # UniRx 7.1.0's asmdef predates the uGUI package split (Unity 2019.3)
        # and does not reference the UnityEngine.UI assembly, so its
        # EventSystems triggers fail to compile on Unity 2019.3+. Patch the
        # asmdef so the project (Unity 2022.3 + ugui) compiles in the real
        # editor as well.
        import glob as _glob
        for asm in _glob.glob(os.path.join(
                dest, "*", "Assets", "Plugins", "UniRx", "Scripts", "UniRx.asmdef")):
            with open(asm, encoding="utf-8") as f:
                data = json.load(f)
            if "UnityEngine.UI" not in data.get("references", []):
                data["references"] = data.get("references", []) + ["UnityEngine.UI"]
            with open(asm, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=4)
            log("patched UniRx.asmdef (+ UnityEngine.UI reference)")
    open(os.path.join(dest, ".done"), "w").write(tag)
    os.remove(tgz)


# --------------------------------------------------------------------------
# Pruning of huge package contents we never compile
# --------------------------------------------------------------------------

def prune_packages():
    p = lambda *xs: os.path.join(REFS, "packages", *xs)

    # burst: drop the native toolchain (700+ MB) - only Runtime source and
    # the precompiled Unity.Burst.Unsafe.dll are needed.
    for junk in [p("com.unity.burst", "package", ".Runtime")]:
        shutil.rmtree(junk, ignore_errors=True)

    # vcontainer: drop bundled test project, website and source generators
    vc = p("com.hadashikick.vcontainer")
    if os.path.isdir(vc):
        for entry in os.listdir(vc):
            if not entry.startswith("VContainer-"):
                continue
            base = os.path.join(vc, entry)
            for sub in ("tests", "website", "VContainer.Standalone",
                        "VContainer.SourceGenerator",
                        "VContainer.SourceGenerator.Roslyn3"):
                shutil.rmtree(os.path.join(base, sub), ignore_errors=True)
            shutil.rmtree(os.path.join(base, "VContainer", "Library"),
                          ignore_errors=True)

    # unirx: drop examples / test projects
    ur = p("com.neuecc.unirx")
    if os.path.isdir(ur):
        for entry in os.listdir(ur):
            if entry.startswith("UniRx-"):
                base = os.path.join(ur, entry)
                for sub in ("Assets/UniRx/Examples", "ProjectSettings"):
                    shutil.rmtree(os.path.join(base, sub), ignore_errors=True)

    # unitask: drop docs
    ut = p("com.cysharp.unitask")
    if os.path.isdir(ut):
        for entry in os.listdir(ut):
            if entry.startswith("UniTask-"):
                shutil.rmtree(os.path.join(ut, entry, "docs"), ignore_errors=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tarball-url", default=DEFAULT_TARBALL_URL)
    ap.add_argument("--skip-unity", action="store_true")
    args = ap.parse_args()

    os.makedirs(REFS, exist_ok=True)
    log(f"refs version: {REFS_VERSION}")

    if not args.skip_unity:
        try:
            fetch_unity(args.tarball_url)
        except Exception as e:
            log(f"primary tarball url failed ({e}); trying alternate mirror")
            fetch_unity(ALT_TARBALL_URL)

    for name, (src, pin, repo) in PACKAGES.items():
        if src == "unity":
            fetch_unity_pkg(name, pin)
        elif src == "builtin":
            fetch_builtin_pkg(name)
        else:
            fetch_github_pkg(name, pin, repo)
        log(f"{name} ok")

    prune_packages()
    log("ALL REFERENCES READY")


if __name__ == "__main__":
    main()
