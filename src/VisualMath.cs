using System;

namespace UniverseVoyager
{
    // Pure, stateless visual math. In particular this never advances Unity's
    // random generator, which is also used by actual gameplay.
    internal static class VisualMath
    {
        internal static float Noise(int frame, int seed, int channel)
        {
            unchecked
            {
                uint value = (uint)frame * 747796405u ^
                    (uint)seed * 2891336453u ^ (uint)channel * 277803737u;
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777216f;
            }
        }

        internal static bool IsGlitching(int tick, int seed)
        {
            long shifted = (long)Math.Max(0, tick) + ((long)seed & 0x7fffffffL) % 211L;
            int cycle = (int)(shifted / 220L);
            int phase = (int)(shifted % 220L);
            int start = 24 + (int)(Noise(cycle, seed, 101) * 150f);
            int duration = 14 + (int)(Noise(cycle, seed, 102) * 10f);
            return phase >= start && phase < start + duration;
        }

        internal static float SmoothNoise(int tick, int seed, int channel, int period)
        {
            int frame = Math.Max(0, tick) / period;
            float t = (Math.Max(0, tick) % period) / (float)period;
            t = t * t * (3f - 2f * t);
            return Noise(frame, seed, channel) * (1f - t) + Noise(frame + 1, seed, channel) * t;
        }

        internal static void PartOffset(int tick, int seed, int group, out float x, out float y)
        {
            x = y = 0f;
            if (!IsGlitching(tick, seed)) return;
            int frame = tick / 2;
            float direction = Noise(frame, seed, 150 + group) < 0.5f ? -1f : 1f;
            x = direction * (2f + Noise(frame, seed, 160 + group) * 5f);
            y = (Noise(frame, seed, 170 + group) - 0.5f) * 3f;
        }

        internal static float HueAt(int tick, float interpolation, float spatialOffset)
        {
            double hue = ((double)tick + interpolation) / 160.0 + spatialOffset;
            float result = (float)(hue - Math.Floor(hue));
            return result >= 1f ? 0f : result;
        }
    }
}
