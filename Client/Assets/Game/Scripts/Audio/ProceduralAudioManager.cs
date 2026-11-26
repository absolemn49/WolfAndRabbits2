using System;
using UnityEngine;

namespace WolfAndRabbits.Client.Audio
{
    public class ProceduralAudioManager : MonoBehaviour
    {
        private AudioSource _sfxSource;
        private AudioClip _buildClip;
        private AudioClip _upgradeClip;
        private AudioClip _slashClip;
        private AudioClip _cannonClip;
        private AudioClip _hitClip;
        private AudioClip _destroyClip;
        private AudioClip _coinClip;
        private AudioClip _shopClip;
        private AudioClip _victoryClip;
        private AudioClip _defeatClip;
        private AudioClip _clickClip;

        private void Awake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume = 0.55f;

            GenerateClips();
        }

        private void GenerateClips()
        {
            _clickClip = CreateTone("Click", 0.05f, 680f, 820f, 0.2f, false);
            _buildClip = CreateChord("Build", 0.22f, new[] { 330f, 440f, 554f }, 0.35f);
            _upgradeClip = CreateArpeggio("Upgrade", 0.36f, new[] { 440f, 554f, 659f, 880f }, 0.35f);
            _slashClip = CreateNoiseSwipe("Slash", 0.16f, 280f, 110f, 0.42f);
            _cannonClip = CreateNoiseSwipe("Cannon", 0.26f, 150f, 42f, 0.50f);
            _hitClip = CreateNoiseSwipe("Hit", 0.11f, 220f, 90f, 0.35f);
            _destroyClip = CreateNoiseSwipe("Destroy", 0.38f, 160f, 36f, 0.52f);
            _coinClip = CreateArpeggio("Coin", 0.18f, new[] { 988f, 1318f }, 0.26f);
            _shopClip = CreateArpeggio("Shop", 0.32f, new[] { 523f, 659f, 784f, 1046f }, 0.34f);
            _victoryClip = CreateArpeggio("Victory", 0.72f, new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.38f);
            _defeatClip = CreateArpeggio("Defeat", 0.68f, new[] { 440f, 392f, 349f, 261f }, 0.36f);
        }

        public void PlayClick() => Play(_clickClip, 0.45f);
        public void PlayBuild() => Play(_buildClip, 0.75f);
        public void PlayUpgrade() => Play(_upgradeClip, 0.85f);
        public void PlaySlash() => Play(_slashClip, 0.80f);
        public void PlayCannon() => Play(_cannonClip, 0.72f);
        public void PlayHit() => Play(_hitClip, 0.65f);
        public void PlayDestroy() => Play(_destroyClip, 0.88f);
        public void PlayCoin() => Play(_coinClip, 0.60f);
        public void PlayShop() => Play(_shopClip, 0.85f);
        public void PlayVictory() => Play(_victoryClip, 0.9f);
        public void PlayDefeat() => Play(_defeatClip, 0.85f);

        private void Play(AudioClip clip, float vol)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, vol);
        }

        private static AudioClip CreateTone(string name, float duration, float fStart, float fEnd, float amp, bool triangle)
        {
            const int sr = 22050;
            int samples = Mathf.Max(64, (int)(sr * duration));
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Pow(1f - t, 1.8f);
                float freq = Mathf.Lerp(fStart, fEnd, t);
                phase += (2f * Mathf.PI * freq) / sr;
                float wave = triangle ? (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(phase)) : Mathf.Sin(phase);
                data[i] = wave * env * amp;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateChord(string name, float duration, float[] freqs, float amp)
        {
            const int sr = 22050;
            int samples = Mathf.Max(64, (int)(sr * duration));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Sin(Mathf.Min(1f, t * 12f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - t, 1.5f);
                float sum = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[k] * (i / (float)sr));
                data[i] = (sum / freqs.Length) * env * amp;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateArpeggio(string name, float duration, float[] freqs, float amp)
        {
            const int sr = 22050;
            int samples = Mathf.Max(64, (int)(sr * duration));
            float[] data = new float[samples];
            float stepDur = duration / freqs.Length;
            for (int i = 0; i < samples; i++)
            {
                float sec = i / (float)sr;
                int idx = Mathf.Clamp((int)(sec / stepDur), 0, freqs.Length - 1);
                float localT = (sec - idx * stepDur) / stepDur;
                float env = Mathf.Pow(1f - localT, 1.4f);
                float f = freqs[idx];
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * sec) * 0.75f + Mathf.Sin(4f * Mathf.PI * f * sec) * 0.25f) * env * amp;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNoiseSwipe(string name, float duration, float fStart, float fEnd, float amp)
        {
            const int sr = 22050;
            int samples = Mathf.Max(64, (int)(sr * duration));
            float[] data = new float[samples];
            System.Random rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Pow(1f - t, 2.0f);
                float freq = Mathf.Lerp(fStart, fEnd, t);
                phase += (2f * Mathf.PI * freq) / sr;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = (Mathf.Sin(phase) * 0.65f + noise * 0.35f) * env * amp;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
