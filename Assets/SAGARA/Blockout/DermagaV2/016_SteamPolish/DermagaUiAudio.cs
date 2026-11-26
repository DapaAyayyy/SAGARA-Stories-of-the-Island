using UnityEngine;

namespace Sagara.DermagaV2
{
    /// <summary>Synthesizes soft, cozy woody/kalimba UI feedback tones so buttons and journal pages feel alive without external audio dependencies.</summary>
    internal static class DermagaUiAudio
    {
        private static AudioSource source;
        private static AudioClip hoverClip;
        private static AudioClip clickClip;
        private static AudioClip pageClip;
        private static AudioClip openClip;
        private static AudioClip lockedClip;
        private static AudioClip fishHitClip;
        private static AudioClip fishMissClip;
        private static AudioClip fishBiteClip;
        private static AudioClip fishThresholdClip;
        private static AudioClip fishCatchClip;
        private static AudioClip clingSparkleClip;
        private static float lastHoverTime = -1f;

        internal static void PlayHover()
        {
            if (Time.unscaledTime - lastHoverTime < 0.06f)
            {
                return;
            }
            lastHoverTime = Time.unscaledTime;
            EnsureReady();
            if (source && hoverClip)
            {
                source.pitch = Random.Range(0.97f, 1.04f);
                source.PlayOneShot(hoverClip, 0.14f);
            }
        }

        internal static void PlayClick()
        {
            EnsureReady();
            if (source && clickClip)
            {
                source.pitch = Random.Range(0.96f, 1.04f);
                source.PlayOneShot(clickClip, 0.26f);
            }
        }

        internal static void PlayPageTurn()
        {
            EnsureReady();
            if (source && pageClip)
            {
                source.pitch = Random.Range(0.96f, 1.05f);
                source.PlayOneShot(pageClip, 0.24f);
            }
        }

        internal static void PlayOpenBook()
        {
            EnsureReady();
            if (source && openClip)
            {
                source.pitch = 1f;
                source.PlayOneShot(openClip, 0.25f);
            }
        }

        internal static void PlayLocked()
        {
            EnsureReady();
            if (source && lockedClip)
            {
                source.pitch = Random.Range(0.96f, 1.03f);
                source.PlayOneShot(lockedClip, 0.24f);
            }
        }

        internal static void PlayFishHit(int streak)
        {
            EnsureReady();
            if (source && fishHitClip)
            {
                float semitones = Mathf.Clamp(streak, 0, 8) * 0.75f;
                source.pitch = Mathf.Pow(2f, semitones / 12f);
                source.PlayOneShot(fishHitClip, 0.24f);
            }
        }

        internal static void PlayFishMiss()
        {
            EnsureReady();
            if (source && fishMissClip)
            {
                source.pitch = Random.Range(0.94f, 1.02f);
                source.PlayOneShot(fishMissClip, 0.22f);
            }
        }

        internal static void PlayFishBite()
        {
            EnsureReady();
            if (source && fishBiteClip)
            {
                source.pitch = 1f;
                source.PlayOneShot(fishBiteClip, 0.28f);
            }
        }

        internal static void PlayFishThreshold()
        {
            EnsureReady();
            if (source && fishThresholdClip)
            {
                source.pitch = 1f;
                source.PlayOneShot(fishThresholdClip, 0.26f);
            }
        }

        internal static void PlayFishCatch(bool legendary)
        {
            EnsureReady();
            if (source && fishCatchClip)
            {
                source.pitch = legendary ? 1.12f : 1.0f;
                source.PlayOneShot(fishCatchClip, legendary ? 0.32f : 0.26f);
            }
        }

        internal static void PlayClingSparkle(float pitch = 1.0f)
        {
            EnsureReady();
            if (source && clingSparkleClip)
            {
                source.pitch = pitch;
                source.PlayOneShot(clingSparkleClip, 0.34f);
            }
        }

        private static void EnsureReady()
        {
            if (!source)
            {
                var go = new GameObject("DermagaUiAudio") { hideFlags = HideFlags.DontSave };
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            if (!hoverClip)         hoverClip         = BuildTone("UI_Hover",         0.035f, 580f, 700f, 0.0f);
            if (!clickClip)         clickClip         = BuildTone("UI_Click",         0.065f, 640f, 380f, 0.06f);
            if (!pageClip)          pageClip          = BuildTone("UI_Page",          0.115f, 420f, 520f, 0.24f);
            if (!openClip)          openClip          = BuildChord("UI_Open",         0.22f,  523.25f, 659.25f, 783.99f);
            if (!lockedClip)        lockedClip        = BuildTone("UI_Locked",        0.090f, 220f, 130f, 0.05f);
            if (!fishHitClip)       fishHitClip       = BuildTone("UI_FishHit",       0.095f, 587.33f, 783.99f, 0.02f);
            if (!fishMissClip)      fishMissClip      = BuildTone("UI_FishMiss",      0.110f, 210f, 125f, 0.08f);
            if (!fishBiteClip)      fishBiteClip      = BuildChord("UI_FishBite",     0.20f,  659.25f, 880.00f, 1046.50f);
            if (!fishThresholdClip) fishThresholdClip = BuildChord("UI_FishThresh",   0.26f,  587.33f, 880.00f, 1174.66f);
            if (!fishCatchClip)     fishCatchClip     = BuildChord("UI_FishCatch",    0.34f,  523.25f, 659.25f, 1046.50f);
            if (!clingSparkleClip)  clingSparkleClip  = BuildChord("UI_ClingSparkle", 0.42f, 1046.50f, 1318.51f, 2093.00f);
        }

        private static AudioClip BuildTone(string name, float duration, float fStart, float fEnd, float noiseMix)
        {
            const int rate = 44100;
            int samples = Mathf.Max(128, Mathf.RoundToInt(rate * duration));
            var data = new float[samples];
            float phase = 0f;
            uint seed = 2463534242u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Sin(Mathf.Min(1f, t * 8f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - t, 2.2f);
                float freq = Mathf.Lerp(fStart, fEnd, t);
                phase += (2f * Mathf.PI * freq) / rate;
                float tone = Mathf.Sin(phase) * 0.78f + Mathf.Sin(phase * 2f) * 0.22f;
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                float n = ((seed & 0xFFFF) / 32768f - 1f) * noiseMix;
                data[i] = Mathf.Clamp((tone * (1f - noiseMix) + n) * env * 0.7f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildChord(string name, float duration, float f1, float f2, float f3)
        {
            const int rate = 44100;
            int samples = Mathf.RoundToInt(rate * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float sec = i / (float)rate;
                float t = i / (float)samples;
                float env1 = Mathf.Exp(-sec * 14f) * Mathf.Clamp01(sec * 120f);
                float env2 = sec > 0.045f ? Mathf.Exp(-(sec - 0.045f) * 12f) * Mathf.Clamp01((sec - 0.045f) * 120f) : 0f;
                float env3 = sec > 0.09f ? Mathf.Exp(-(sec - 0.09f) * 10f) * Mathf.Clamp01((sec - 0.09f) * 120f) : 0f;
                float s = Mathf.Sin(2f * Mathf.PI * f1 * sec) * env1 * 0.45f
                    + Mathf.Sin(2f * Mathf.PI * f2 * sec) * env2 * 0.40f
                    + Mathf.Sin(2f * Mathf.PI * f3 * sec) * env3 * 0.35f;
                data[i] = Mathf.Clamp(s * (1f - t * 0.2f), -1f, 1f);
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
