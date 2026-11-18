using System.Collections.Generic;
using UnityEngine;

namespace TomCatRunner.Gameplay.Core
{
    /// <summary>
    /// 程序化音频服务 - 全部音效与背景音乐均由代码合成，无外部音频文件
    /// 正弦/噪声合成 + 包络，简单而温暖
    /// </summary>
    public class ProceduralAudioService
    {
        private const int SampleRate = 44100;

        private readonly AudioSource[] _sfxSources;
        private readonly AudioSource _musicSource;
        private int _sfxIndex;

        private readonly AudioClip _jumpClip, _coinClip, _hitClip, _powerupClip, _clickClip,
            _countdownClip, _goClip, _landClip, _shieldBreakClip, _boostClip, _denyClip;
        private readonly AudioClip _musicClip;

        public bool SfxEnabled { get; set; } = true;
        public bool MusicEnabled { get; set; } = true;

        public ProceduralAudioService()
        {
            var host = new GameObject("[Audio]");
            Object.DontDestroyOnLoad(host);

            _sfxSources = new AudioSource[8];
            for (int i = 0; i < _sfxSources.Length; i++)
                _sfxSources[i] = host.AddComponent<AudioSource>();

            _musicSource = host.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.volume = 0.35f;
            _musicSource.spatialBlend = 0f;

            _jumpClip = BuildJump();
            _coinClip = BuildCoin();
            _hitClip = BuildHit();
            _powerupClip = BuildPowerup();
            _clickClip = BuildClick();
            _countdownClip = BuildTone(520f, 520f, 0.16f, 0.35f);
            _goClip = BuildTone(780f, 1040f, 0.4f, 0.4f);
            _landClip = BuildTone(170f, 110f, 0.1f, 0.28f);
            _shieldBreakClip = BuildShieldBreak();
            _boostClip = BuildBoost();
            _denyClip = BuildTone(220f, 160f, 0.18f, 0.3f);
            _musicClip = BuildMusicLoop();

            _musicSource.clip = _musicClip;
            PlayMusic();
        }

        public void ApplySettings(bool sfxOn, bool musicOn)
        {
            SfxEnabled = sfxOn;
            MusicEnabled = musicOn;
            if (!musicOn && _musicSource.isPlaying) _musicSource.Stop();
            else if (musicOn && !_musicSource.isPlaying) _musicSource.Play();
        }

        public void ApplyMusic(bool on)
        {
            MusicEnabled = on;
            if (!on && _musicSource.isPlaying) _musicSource.Stop();
            else if (on && !_musicSource.isPlaying) _musicSource.Play();
        }

        public void ApplySfx(bool on) => SfxEnabled = on;

        public void PlayMusic()
        {
            if (MusicEnabled && !_musicSource.isPlaying)
                _musicSource.Play();
        }

        public void StopMusic() => _musicSource.Stop();

        public void PlayJump() => Play(_jumpClip, 1f, Random.Range(0.94f, 1.06f));
        public void PlayCoin() => Play(_coinClip, 0.9f, Random.Range(0.98f, 1.05f));
        public void PlayHit() => Play(_hitClip, 1f, 1f);
        public void PlayPowerup() => Play(_powerupClip, 1f, 1f);
        public void PlayClick() => Play(_clickClip, 0.7f, 1f);
        public void PlayCountdown() => Play(_countdownClip, 0.9f, 1f);
        public void PlayGo() => Play(_goClip, 1f, 1f);
        public void PlayLand() => Play(_landClip, 0.55f, Random.Range(0.95f, 1.05f));
        public void PlayShieldBreak() => Play(_shieldBreakClip, 0.9f, 1f);
        public void PlayBoost() => Play(_boostClip, 0.85f, 1f);
        public void PlayDeny() => Play(_denyClip, 0.8f, 1f);

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (!SfxEnabled || clip == null) return;
            var src = _sfxSources[_sfxIndex];
            _sfxIndex = (_sfxIndex + 1) % _sfxSources.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        // =====================================================================
        // 合成器
        // =====================================================================

        /// <summary>基频扫频音（正弦 + 轻微二次谐波），带短包络</summary>
        private static AudioClip BuildTone(float freqFrom, float freqTo, float duration, float volume)
        {
            int samples = (int)(SampleRate * duration);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(freqFrom, freqTo, t);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Envelope(i, samples, 0.02f);
                data[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * env * volume * 0.5f;
            }
            return CreateClip(data, "tone_" + freqFrom);
        }

        private static AudioClip BuildJump()
        {
            int samples = (int)(SampleRate * 0.2f);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(320f, 680f, t * t);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Envelope(i, samples, 0.03f);
                data[i] = Mathf.Sin(phase) * env * 0.5f;
            }
            return CreateClip(data, "sfx_jump");
        }

        private static AudioClip BuildCoin()
        {
            // 两段音符：B5 → E6，清脆叮当
            int s1 = (int)(SampleRate * 0.06f);
            int s2 = (int)(SampleRate * 0.14f);
            var data = new float[s1 + s2];
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                bool first = i < s1;
                float freq = first ? 987.77f : 1318.5f;
                int localIdx = first ? i : i - s1;
                int localLen = first ? s1 : s2;
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Envelope(localIdx, localLen, 0.015f);
                float v = Mathf.Sin(phase) + 0.15f * Mathf.Sin(phase * 3f);
                data[i] = v * env * 0.42f;
            }
            return CreateClip(data, "sfx_coin");
        }

        private static AudioClip BuildHit()
        {
            // 噪声爆发 + 低频闷响
            int samples = (int)(SampleRate * 0.32f);
            var data = new float[samples];
            var rng = new System.Random(42);
            float lowPhase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Envelope(i, samples, 0.01f) * (1f - t * 0.5f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                lowPhase += 2f * Mathf.PI * Mathf.Lerp(160f, 60f, t) / SampleRate;
                float thud = Mathf.Sin(lowPhase) * 0.55f;
                data[i] = (noise + thud) * env * 0.5f;
            }
            return CreateClip(data, "sfx_hit");
        }

        private static AudioClip BuildPowerup()
        {
            // 上行琶音 C5 E5 G5 C6
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.5f };
            int noteLen = (int)(SampleRate * 0.09f);
            var data = new float[freqs.Length * noteLen];
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                int note = i / noteLen;
                int localIdx = i - note * noteLen;
                phase += 2f * Mathf.PI * freqs[note] / SampleRate;
                float env = Envelope(localIdx, noteLen, 0.012f);
                data[i] = (Mathf.Sin(phase) + 0.2f * Mathf.Sin(phase * 2f)) * env * 0.4f;
            }
            return CreateClip(data, "sfx_powerup");
        }

        private static AudioClip BuildClick()
        {
            int samples = (int)(SampleRate * 0.05f);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                phase += 2f * Mathf.PI * 1250f / SampleRate;
                float env = Envelope(i, samples, 0.008f);
                data[i] = Mathf.Sin(phase) * env * 0.3f;
            }
            return CreateClip(data, "sfx_click");
        }

        private static AudioClip BuildShieldBreak()
        {
            // 玻璃碎裂感：高频噪声快速衰减
            int samples = (int)(SampleRate * 0.25f);
            var data = new float[samples];
            var rng = new System.Random(7);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Exp(-t * 18f);
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                // 简单高频强调：差分
                float hi = noise - (i > 0 ? data[i - 1] * 0.5f : 0f);
                data[i] = hi * env * 0.35f;
            }
            return CreateClip(data, "sfx_shield_break");
        }

        private static AudioClip BuildBoost()
        {
            // 呼啸声：噪声 + 上扫带通（用扫频正弦模拟）
            int samples = (int)(SampleRate * 0.5f);
            var data = new float[samples];
            var rng = new System.Random(99);
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(200f, 900f, t);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Envelope(i, samples, 0.05f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.3f;
                data[i] = (Mathf.Sin(phase) * 0.35f + noise) * env * 0.5f;
            }
            return CreateClip(data, "sfx_boost");
        }

        /// <summary>背景音乐 - 8 秒循环：柔和双和弦垫 + 琶音点缀</summary>
        private static AudioClip BuildMusicLoop()
        {
            const int channels = 2;
            int barLen = SampleRate * 4;              // 4 秒一个和弦
            int total = barLen * 2;
            var data = new float[total * channels];

            // 和弦：C 大三和弦 → A 小三和弦
            float[][] chords =
            {
                new[] { 130.81f, 164.81f, 196.00f, 261.63f }, // C3 E3 G3 C4
                new[] { 110.00f, 130.81f, 164.81f, 220.00f }, // A2 C3 E3 A3
            };
            // 琶音点缀（每 0.5 秒一个音）
            float[] arpC = { 523.25f, 659.25f, 783.99f, 659.25f, 523.25f, 783.99f, 1046.5f, 783.99f };
            float[] arpA = { 440.00f, 523.25f, 659.25f, 523.25f, 440.00f, 659.25f, 880.00f, 659.25f };

            var detunePhases = new float[4];
            float arpPhase = 0f;
            float prevArpFreq = 0f;

            for (int i = 0; i < total; i++)
            {
                int bar = i / barLen;
                int local = i - bar * barLen;
                var chord = chords[bar];

                float pad = 0f;
                for (int v = 0; v < chord.Length; v++)
                {
                    detunePhases[v] += 2f * Mathf.PI * (chord[v] + 0.7f) / SampleRate; // 轻微失谐增加温暖感
                    float voice = Mathf.Sin(detunePhases[v]) * 0.6f + Mathf.Sin(detunePhases[v] * 0.5f) * 0.4f;
                    pad += voice * 0.055f;
                }

                // 琶音（柔和正弦，短包络）
                float arp = 0f;
                int arpIdx = (local / (SampleRate / 2)) % 8;
                float arpFreq = (bar == 0 ? arpC : arpA)[arpIdx];
                if (!Mathf.Approximately(arpFreq, prevArpFreq)) { arpPhase = 0f; prevArpFreq = arpFreq; }
                arpPhase += 2f * Mathf.PI * arpFreq / SampleRate;
                int arpLocal = local % (SampleRate / 2);
                float arpEnv = Envelope(arpLocal, SampleRate / 2, 0.02f) * 0.7f;
                arp = Mathf.Sin(arpPhase) * arpEnv * 0.05f;

                // 轻微立体声摆动
                float sway = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * i / (float)SampleRate * 0.125f);
                float sample = (pad + arp) * 0.9f;
                data[i * channels] = sample * (0.7f + 0.3f * (1f - sway));
                data[i * channels + 1] = sample * (0.7f + 0.3f * sway);
            }

            var clip = AudioClip.Create("music_loop", total, channels, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>短攻击短释放包络</summary>
        private static float Envelope(int i, int total, float attackFrac)
        {
            int attack = Mathf.Max(1, (int)(total * attackFrac));
            int release = Mathf.Max(1, total / 4);
            float a = Mathf.Clamp01(i / (float)attack);
            float r = Mathf.Clamp01((total - i) / (float)release);
            return a * r;
        }

        private static AudioClip CreateClip(float[] data, string name)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
