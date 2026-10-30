using System;
using UnityEngine;

namespace UniverseVoyager
{
    internal sealed class RenderState
    {
        internal const float BodyOpacity = 0.16f;
        internal const int GlitchSpriteCount = 10;
        private const int BodySpriteCount = 10;
        private readonly int firstEffect;
        private readonly int firstGlitch;
        private readonly NeuronMotion motion;
        private readonly float[] vanillaAlphas = new float[BodySpriteCount];
        private readonly Color[] vanillaColors = new Color[9];
        private readonly Vector2[] vanillaPositions = new Vector2[BodySpriteCount];
        private Vector2[] vanillaTailVertices;
        private bool appearanceApplied;

        internal RenderState(RoomCamera.SpriteLeaser leaser, NeuronMotion neuronMotion)
        {
            motion = neuronMotion;
            firstEffect = leaser.sprites.Length;
            firstGlitch = firstEffect + ImmortalNeuron.SpriteCount;
            FSprite[] sprites = leaser.sprites;
            Array.Resize(ref sprites, firstGlitch + GlitchSpriteCount);
            leaser.sprites = sprites;
            ImmortalNeuron.Create(sprites, firstEffect);
            for (int i = 0; i < GlitchSpriteCount; i++)
                sprites[firstGlitch + i] = new FSprite("pixel", true);
            HideEffects(leaser);
        }

        internal void Attach(RoomCamera.SpriteLeaser leaser,
            RoomCamera camera, FContainer requestedContainer)
        {
            FContainer container = leaser.sprites[3].container ??
                requestedContainer ?? camera.ReturnFContainer("Midground");
            for (int i = firstEffect; i < firstGlitch + GlitchSpriteCount; i++)
            {
                leaser.sprites[i].RemoveFromContainer();
                container.AddChild(leaser.sprites[i]);
            }
        }

        internal void RestoreAppearance(RoomCamera.SpriteLeaser leaser)
        {
            if (!appearanceApplied) return;
            for (int i = 0; i < BodySpriteCount; i++)
            {
                if (i < 9) leaser.sprites[i].color = vanillaColors[i];
                leaser.sprites[i].alpha = vanillaAlphas[i];
                leaser.sprites[i].SetPosition(vanillaPositions[i]);
            }
            TriangleMesh tail = leaser.sprites[2] as TriangleMesh;
            if (tail != null && vanillaTailVertices != null &&
                tail.vertices.Length == vanillaTailVertices.Length)
            {
                for (int i = 0; i < vanillaTailVertices.Length; i++)
                    tail.MoveVertice(i, vanillaTailVertices[i]);
            }
            appearanceApplied = false;
        }

        internal void Draw(PlayerGraphics graphics, Player player,
            RoomCamera.SpriteLeaser leaser, float timeStacker, Vector2 cameraPosition)
        {
            FSprite[] sprites = leaser.sprites;
            for (int i = 0; i < BodySpriteCount; i++)
            {
                vanillaAlphas[i] = sprites[i].alpha;
                vanillaPositions[i] = sprites[i].GetPosition();
                if (i < 9)
                {
                    vanillaColors[i] = sprites[i].color;
                    sprites[i].color = Color.white;
                }
                // This is absolute relative to the restored vanilla alpha,
                // never a repeated multiplication of last frame's result.
                sprites[i].alpha = vanillaAlphas[i] * BodyOpacity;
            }
            TriangleMesh tail = sprites[2] as TriangleMesh;
            if (tail != null)
            {
                if (vanillaTailVertices == null || vanillaTailVertices.Length != tail.vertices.Length)
                    vanillaTailVertices = new Vector2[tail.vertices.Length];
                Array.Copy(tail.vertices, vanillaTailVertices, tail.vertices.Length);
            }
            appearanceApplied = true;

            if (!sprites[3].isVisible || vanillaAlphas[3] <= 0.001f)
            {
                HideEffects(leaser);
                return;
            }

            int tick = player.room.game.clock;
            int seed = player.playerState.playerNumber + 2719;
            float visibility = Mathf.Clamp01(vanillaAlphas[3]);
            Vector2 headWorld = graphics.head != null ? graphics.head.pos :
                vanillaPositions[3] + cameraPosition;
            motion.Advance(tick, player.room, headWorld.x, headWorld.y, seed);
            ImmortalNeuron.Draw(sprites, firstEffect, motion.Sample(timeStacker),
                cameraPosition, tick, timeStacker, visibility);

            bool glitching = VisualMath.IsGlitching(tick, seed);
            if (glitching) DisplaceBody(leaser, tick, seed);
            Vector2 hips = vanillaPositions[1];
            Vector2 head = vanillaPositions[3];
            Vector2 axis = head - hips;
            Vector2 side = axis.sqrMagnitude > 0.01f ?
                new Vector2(axis.y, -axis.x).normalized : Vector2.right;
            int frame = tick / 2;
            for (int i = 0; i < GlitchSpriteCount; i++)
            {
                FSprite block = sprites[firstGlitch + i];
                block.isVisible = glitching;
                if (!glitching) continue;
                float along = 0.1f + VisualMath.Noise(frame, seed, i * 7) * 0.8f;
                float sideways = (VisualMath.Noise(frame, seed, i * 7 + 1) - 0.5f) * 14f;
                block.SetPosition(Vector2.Lerp(hips, head, along) + side * sideways);
                float size = 8f + 10f * VisualMath.Noise(frame, seed, i * 7 + 2);
                bool square = i < 4;
                block.scaleX = size;
                block.scaleY = square ? size : 2f + 4f * VisualMath.Noise(frame, seed, i * 7 + 3);
                block.rotation = 0f;
                block.color = Color.HSVToRGB(VisualMath.HueAt(tick, timeStacker,
                    VisualMath.Noise(frame, seed, i * 7 + 4)), 0.95f, 1f);
                block.alpha = (square ? 0.48f : 0.72f) * visibility;
            }
        }

        private void DisplaceBody(RoomCamera.SpriteLeaser leaser, int tick, int seed)
        {
            for (int i = 0; i < BodySpriteCount; i++)
            {
                if (i == 2) continue;
                // Keep head and eyes together, and legs with the hips.
                int group = (i == 3 || i == 9) ? 2 : ((i == 1 || i == 4) ? 1 : 0);
                float x, y;
                VisualMath.PartOffset(tick, seed, group, out x, out y);
                leaser.sprites[i].SetPosition(vanillaPositions[i] + new Vector2(x, y));
            }
            TriangleMesh tail = leaser.sprites[2] as TriangleMesh;
            if (tail != null && vanillaTailVertices != null)
            {
                float x, y;
                VisualMath.PartOffset(tick, seed, 3, out x, out y);
                Vector2 offset = new Vector2(x, y);
                for (int i = 0; i < vanillaTailVertices.Length; i++)
                    tail.MoveVertice(i, vanillaTailVertices[i] + offset);
            }
        }

        internal void HideEffects(RoomCamera.SpriteLeaser leaser)
        {
            for (int i = firstEffect; i < firstGlitch + GlitchSpriteCount; i++)
                leaser.sprites[i].isVisible = false;
        }

        internal void RemoveEffects(RoomCamera.SpriteLeaser leaser)
        {
            RestoreAppearance(leaser);
            HideEffects(leaser);
            for (int i = firstEffect; i < firstGlitch + GlitchSpriteCount; i++)
            {
                leaser.sprites[i].alpha = 0f;
                leaser.sprites[i].RemoveFromContainer();
            }
            // Remove our unused slots when no other mod appended after us.
            if (leaser.sprites.Length == firstGlitch + GlitchSpriteCount)
            {
                FSprite[] sprites = leaser.sprites;
                Array.Resize(ref sprites, firstEffect);
                leaser.sprites = sprites;
            }
        }
    }
}
