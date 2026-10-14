using UnityEngine;

namespace Dinner
{
    /// The project ships no sound, so every cue is synthesised at startup.
    /// Placeholder-grade on purpose: enough for the ear to tell pan from table.
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static System.Random _rng = new System.Random(2290);

        static float White() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// Crossfade the tail into the head so a looped clip doesn't click.
        static void Loopify(float[] d, int fade = 3000)
        {
            for (int i = 0; i < fade && i < d.Length / 2; i++)
            {
                float t = i / (float)fade;
                d[i] = Mathf.Lerp(d[d.Length - fade + i], d[i], t);
            }
        }

        /// Fat hitting a hot pan: brown noise bed with random crackles. Loop it.
        public static AudioClip Sizzle(float seconds = 5f)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float brown = 0f, crackle = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = White();
                brown = (brown + 0.03f * w) / 1.03f;
                if (_rng.NextDouble() < 0.0012) crackle = 0.5f + 0.5f * (float)_rng.NextDouble();
                crackle *= 0.9965f;
                lp += 0.25f * (w - lp);
                float swell = 0.75f + 0.25f * Mathf.Sin(i * 2f * Mathf.PI / Rate * 0.37f);
                d[i] = Mathf.Clamp(brown * 3.2f * swell + lp * 0.10f + w * crackle * 0.45f, -1f, 1f);
            }
            Loopify(d);
            return Make("Sizzle", d);
        }

        /// A grease pop — the sound that turns your head.
        public static AudioClip Pop()
        {
            int n = (int)(0.22f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 55f);
                float thump = Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 30f);
                d[i] = Mathf.Clamp(White() * env * 0.9f + thump * 0.6f, -1f, 1f);
            }
            return Make("Pop", d);
        }

        /// Voices through the back of your head: low-passed noise with syllable-rate wobble. Loop it.
        public static AudioClip Murmur(float seconds = 6f)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp1 = 0f, lp2 = 0f, amp = 0.4f, target = 0.4f;
            int nextSyllable = 0;
            for (int i = 0; i < n; i++)
            {
                if (i >= nextSyllable)
                {
                    target = (float)_rng.NextDouble() < 0.25 ? 0.05f : 0.3f + 0.7f * (float)_rng.NextDouble();
                    nextSyllable = i + (int)(Rate * (0.08f + 0.17f * (float)_rng.NextDouble()));
                }
                amp += (target - amp) * 0.0008f;
                float w = White();
                lp1 += 0.045f * (w - lp1);
                lp2 += 0.045f * (lp1 - lp2);
                d[i] = Mathf.Clamp(lp2 * 6f * amp, -1f, 1f);
            }
            Loopify(d);
            return Make("Murmur", d);
        }

        /// Three people laughing, approximated: a run of pitched, breathy pulses.
        public static AudioClip Laughter()
        {
            float seconds = 1.6f;
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int v = 0; v < 3; v++)   // one voice per friend
            {
                float basePitch = 180f + v * 70f;
                float period = 0.16f + v * 0.02f;
                float start = v * 0.07f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate - start;
                    if (t < 0f) continue;
                    int pulse = (int)(t / period);
                    if (pulse > 6) break;
                    float pt = t - pulse * period;
                    float env = Mathf.Exp(-pt * 22f) * (1f - pulse / 8f);
                    float pitch = basePitch * (1f - pulse * 0.04f);
                    float tone = Mathf.Sin(2f * Mathf.PI * pitch * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * pitch * 2f * t);
                    float w = White();
                    lp += 0.12f * (w - lp);
                    d[i] += (tone * 0.18f + lp * 0.5f) * env * 0.35f;
                }
            }
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            return Make("Laughter", d);
        }

        /// Soft two-note chime for prompts.
        public static AudioClip Chime()
        {
            int n = (int)(0.6f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 6f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1318f * t) * 0.3f) * env * 0.4f;
            }
            return Make("Chime", d);
        }

        /// Smoke alarm: three sharp chirps.
        public static AudioClip Alarm()
        {
            int n = (int)(1.2f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float phase = t % 0.4f;
                float gate = phase < 0.12f ? 1f : 0f;
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 3100f * t));
                d[i] = sq * gate * 0.25f;
            }
            return Make("Alarm", d);
        }
    }
}
