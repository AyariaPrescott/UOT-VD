using System;

namespace UniverseVoyager
{
    internal struct NeuronPose
    {
        internal float X, Y, Angle, LazyAngle, Spin;
    }

    // A visual-only spring, shared by the cameras drawing one player.
    // All coordinates are world coordinates; no game physics is changed.
    internal sealed class NeuronMotion
    {
        private object room;
        private bool initialized;
        private int lastTick;
        private float x, y, lastX, lastY, velocityX, velocityY;
        private float headX, headY, angle, lastAngle, lazyAngle, lastLazyAngle;
        private double spin, lastSpin;

        internal void Advance(int tick, object roomKey, float newHeadX, float newHeadY, int seed)
        {
            float distanceX = newHeadX - x;
            float distanceY = newHeadY - y;
            long elapsed = (long)tick - lastTick;
            if (!initialized || !ReferenceEquals(room, roomKey) || elapsed < 0 || elapsed > 12 ||
                distanceX * distanceX + distanceY * distanceY > 160f * 160f)
            {
                room = roomKey;
                initialized = true;
                lastTick = tick;
                headX = newHeadX;
                headY = newHeadY;
                Target(tick, seed, headX, headY, out x, out y);
                lastX = x;
                lastY = y;
                velocityX = velocityY = 0f;
                angle = lastAngle = lazyAngle = lastLazyAngle = 0f;
                spin = lastSpin = tick * 0.07;
                return;
            }
            // Repeated draws, including another camera, cannot integrate twice.
            if (elapsed == 0) return;
            for (int step = 1; step <= elapsed; step++)
            {
                int frame = lastTick + step;
                float fraction = step / (float)elapsed;
                float anchorX = headX + (newHeadX - headX) * fraction;
                float anchorY = headY + (newHeadY - headY) * fraction;
                lastX = x;
                lastY = y;
                lastAngle = angle;
                lastLazyAngle = lazyAngle;
                lastSpin = spin;
                float targetX, targetY;
                Target(frame, seed, anchorX, anchorY, out targetX, out targetY);
                velocityX = (velocityX + (targetX - x) * 0.045f) * 0.86f;
                velocityY = (velocityY + (targetY - y) * 0.045f) * 0.86f;
                float speed = (float)Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
                if (speed > 11f)
                {
                    velocityX *= 11f / speed;
                    velocityY *= 11f / speed;
                }
                x += velocityX;
                y += velocityY;
                // Keep the wandering neuron above this cat, including jumps.
                x = Clamp(x, anchorX - 28f, anchorX + 28f);
                y = Clamp(y, anchorY + 15f, anchorY + 46f);
                float time = frame / 40f;
                float targetAngle = (float)Math.Sin(time * 0.9f + seed) * 32f + velocityX * 3f;
                angle += (targetAngle - angle) * 0.12f;
                lazyAngle += (angle - lazyAngle) * 0.08f;
                spin += 0.07 + 0.016 * Math.Sin(time * 1.9);
            }
            headX = newHeadX;
            headY = newHeadY;
            lastTick = tick;
        }

        internal NeuronPose Sample(float interpolation)
        {
            float t = Clamp(interpolation, 0f, 1f);
            return new NeuronPose
            {
                X = lastX + (x - lastX) * t,
                Y = lastY + (y - lastY) * t,
                Angle = lastAngle + (angle - lastAngle) * t,
                LazyAngle = lastLazyAngle + (lazyAngle - lastLazyAngle) * t,
                Spin = (float)(lastSpin + (spin - lastSpin) * t)
            };
        }

        private static void Target(int tick, int seed, float anchorX, float anchorY,
            out float targetX, out float targetY)
        {
            double time = tick / 40.0;
            targetX = anchorX + (float)(Math.Sin(time * 1.1 + seed) * 5.0 +
                Math.Sin(time * 0.37 + seed * 0.3) * 3.0) +
                (VisualMath.SmoothNoise(tick, seed, 91, 81) - 0.5f) * 8f;
            targetY = anchorY + 28f + (float)(Math.Sin(time * 1.4 + seed * 0.1) * 4.0 +
                Math.Sin(time * 0.7) * 2.0) +
                (VisualMath.SmoothNoise(tick, seed, 92, 97) - 0.5f) * 4f;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
