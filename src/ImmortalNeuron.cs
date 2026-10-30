using UnityEngine;

namespace UniverseVoyager
{
    internal static class ImmortalNeuron
    {
        internal const int SpriteCount = 4;
        private const int Grid = 4;

        internal static void Create(FSprite[] sprites, int first)
        {
            // These are the exact four visible atlas elements used by the
            // game's OracleSwarmer. Its separate light halo is not added.
            sprites[first] = TriangleMesh.MakeGridMesh("JetFishEyeA", Grid);
            sprites[first + 1] = TriangleMesh.MakeGridMesh("deerEyeA2", Grid);
            sprites[first + 2] = TriangleMesh.MakeGridMesh("deerEyeA2", Grid);
            sprites[first + 3] = TriangleMesh.MakeGridMesh("JetFishEyeB", Grid);
        }

        internal static void Draw(FSprite[] sprites, int first, NeuronPose pose,
            Vector2 cameraPosition, int tick, float interpolation, float visibility)
        {
            Vector2 center = new Vector2(pose.X, pose.Y) - cameraPosition;
            float angle = pose.Angle;
            float wave = Mathf.Sin(pose.Spin);
            float turn = Mathf.Cos(pose.Spin);
            float spread = 10f + Mathf.Clamp01(Mathf.Abs(angle - pose.LazyAngle) / 40f) * 35f;
            Vector2 up = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad),
                Mathf.Cos(angle * Mathf.Deg2Rad));
            Vector2 right = new Vector2(up.y, -up.x);

            DrawPart((TriangleMesh)sprites[first], center, center, angle,
                0.75f, 1.2f, 0.5f, tick, interpolation, visibility, 1f);
            DrawPart((TriangleMesh)sprites[first + 1], center - up * 4f,
                center, pose.LazyAngle + 90f - spread * wave, 1f, -wave, 0f,
                tick, interpolation, visibility, 1f);
            DrawPart((TriangleMesh)sprites[first + 2], center - up * 4f,
                center, pose.LazyAngle + 90f + spread * wave, 1f, wave, 0f,
                tick, interpolation, visibility, 1f);
            DrawPart((TriangleMesh)sprites[first + 3],
                center + right * (2f * turn * Mathf.Sign(wave)), center,
                angle, 1f - Mathf.Abs(turn), 1f, 0.5f,
                tick, interpolation, visibility, 0.6f);
        }

        private static void DrawPart(TriangleMesh mesh, Vector2 origin,
            Vector2 neuronCenter, float angle, float scaleX, float scaleY,
            float anchorX, int tick, float interpolation, float visibility, float brightness)
        {
            FAtlasElement element = mesh.element;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 right = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
            Vector2 up = new Vector2(-right.y, right.x);
            mesh.isVisible = true;
            for (int row = 0; row <= Grid; row++)
            {
                for (int column = 0; column <= Grid; column++)
                {
                    float u = column / (float)Grid;
                    float v = row / (float)Grid;
                    float x = (element.sourceRect.x + element.sourceRect.width * u -
                        element.sourceSize.x * anchorX) * scaleX;
                    // Futile stores trimming offsets from the top of the
                    // source image, while mesh vertices use bottom-up Y.
                    float y = (element.sourceSize.y * 0.5f - element.sourceRect.y -
                        element.sourceRect.height + element.sourceRect.height * v) * scaleY;
                    Vector2 position = origin + right * x + up * y;
                    int index = row * (Grid + 1) + column;
                    mesh.MoveVertice(index, position);
                    Vector2 offset = position - neuronCenter;
                    Color color = Color.HSVToRGB(
                        VisualMath.HueAt(tick, interpolation, (offset.x + offset.y) * 0.085f),
                        0.9f, brightness);
                    color.a = visibility;
                    mesh.verticeColors[index] = color;
                }
            }
            mesh.Refresh();
        }
    }
}
