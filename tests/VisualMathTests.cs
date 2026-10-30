using System;
using UniverseVoyager;

internal static class VisualMathTests
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static int Main()
    {
        try
        {
            for (int seed = 2719; seed < 2723; seed++)
            {
                int active = 0;
                int longestBurst = 0;
                int burst = 0;
                int longestQuiet = 0;
                int quiet = 0;
                for (int tick = 0; tick < 40000; tick++)
                {
                    bool glitch = VisualMath.IsGlitching(tick, seed);
                    if (glitch)
                    {
                        active++;
                        burst++;
                        quiet = 0;
                        longestBurst = Math.Max(longestBurst, burst);
                    }
                    else
                    {
                        quiet++;
                        burst = 0;
                        longestQuiet = Math.Max(longestQuiet, quiet);
                    }
                    // Additional cameras and reordered calls cannot advance
                    // the animation or consume shared random state.
                    VisualMath.Noise(tick, seed + 1, 7);
                    Require(glitch == VisualMath.IsGlitching(tick, seed), "Camera-order dependence");
                    float sample = VisualMath.Noise(tick, seed, 2);
                    Require(sample >= 0f && sample < 1f, "Noise outside range");
                }
                Require(active > 1600 && active < 4800, "Glitch is absent or too persistent");
                Require(longestBurst >= 14 && longestBurst <= 23, "Glitch duration outside bounds");
                Require(longestQuiet < 480, "Excessively long interval without a glitch");
            }

            foreach (int tick in new int[] { 0, 1, 959, 960, 1000000, int.MaxValue })
            {
                float hue = VisualMath.HueAt(tick, 0.5f, -1.25f);
                Require(hue >= 0f && hue < 1f, "Hue outside range");
                VisualMath.IsGlitching(tick, int.MinValue);
            }
            Require(Math.Abs(VisualMath.HueAt(123, 0.5f, 0.2f) -
                VisualMath.HueAt(283, 0.5f, 0.2f)) < 0.00001f, "Gradient loop discontinuity");
            Require(Math.Abs(VisualMath.HueAt(0, 0f, 0f) - VisualMath.HueAt(40, 0f, 0f)) > 0.2f,
                "Gradient animation is not visibly progressing within one second");
            CheckMotion();
            CheckDisplacement();
            Console.WriteLine("PASS: independent neuron drift/inertia, camera deduplication, room reset, dynamic gradient, intermittent bounded visual tearing.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void CheckMotion()
    {
        object room = new object();
        NeuronMotion motion = new NeuronMotion();
        float minimumX = float.MaxValue, maximumX = float.MinValue;
        float minimumAngle = float.MaxValue, maximumAngle = float.MinValue;
        for (int tick = 0; tick < 1200; tick++)
        {
            motion.Advance(tick, room, 100f, 100f, 2719);
            NeuronPose pose = motion.Sample(1f);
            minimumX = Math.Min(minimumX, pose.X);
            maximumX = Math.Max(maximumX, pose.X);
            minimumAngle = Math.Min(minimumAngle, pose.Angle);
            maximumAngle = Math.Max(maximumAngle, pose.Angle);
            Require(pose.Y >= 115f && pose.Y <= 146f, "Neuron leaves its hovering range");
            for (int camera = 0; camera < 3; camera++)
                motion.Advance(tick, room, 100f, 100f, 2719);
            NeuronPose second = motion.Sample(1f);
            Require(pose.X == second.X && pose.Y == second.Y && pose.Spin == second.Spin,
                "Additional camera advances motion");
        }
        Require(maximumX - minimumX > 8f, "Neuron appears pinned above a stationary head");
        Require(maximumAngle - minimumAngle > 30f, "Neuron direction appears fixed");
        NeuronPose before = motion.Sample(1f);
        motion.Advance(1200, room, 112f, 100f, 2719);
        NeuronPose after = motion.Sample(1f);
        Require(Math.Abs(after.X - before.X) < 8f, "Neuron is rigidly attached to head motion");
        Require(motion.Sample(0f).X == before.X, "Frame interpolation lost previous position");
        motion.Advance(1201, new object(), 5000f, 3000f, 2719);
        NeuronPose warped = motion.Sample(1f);
        Require(Math.Abs(warped.X - 5000f) < 20f && warped.Y > 3014f && warped.Y < 3047f,
            "Room transition leaves a streak across the map");
    }

    private static void CheckDisplacement()
    {
        int different = 0;
        for (int tick = 0; tick < 20000; tick++)
        {
            float x, y, otherX, otherY;
            VisualMath.PartOffset(tick, 2719, 0, out x, out y);
            VisualMath.PartOffset(tick, 2719, 2, out otherX, out otherY);
            Require(Math.Abs(x) <= 7f && Math.Abs(y) <= 1.5f, "Body displacement is excessive");
            if (!VisualMath.IsGlitching(tick, 2719))
                Require(x == 0f && y == 0f, "Body remains displaced outside a glitch");
            else if (Math.Abs(x - otherX) > 2f) different++;
        }
        Require(different > 200, "Body sections never tear independently");
    }
}
