using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using UnityEngine;

namespace UniverseVoyager
{
    [BepInPlugin(ModId, DisplayName, Version)]
    [BepInDependency("slime-cubed.slugbase")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModId = "ayaria.universevoyager";
        public const string DisplayName = "寰宇观游//虚拟神明";
        public const string Version = "0.2.0";
        public const string CharacterId = "Ayaria_UniverseVoyager";

        private readonly ConditionalWeakTable<RoomCamera.SpriteLeaser, RenderState> states =
            new ConditionalWeakTable<RoomCamera.SpriteLeaser, RenderState>();
        private readonly List<WeakReference> leases = new List<WeakReference>();
        private readonly ConditionalWeakTable<PlayerGraphics, NeuronMotion> motions =
            new ConditionalWeakTable<PlayerGraphics, NeuronMotion>();
        private bool hooked;

        private void OnEnable()
        {
            if (hooked) return;
            On.PlayerGraphics.InitiateSprites += InitiateSprites;
            On.PlayerGraphics.DrawSprites += DrawSprites;
            On.PlayerGraphics.AddToContainer += AddToContainer;
            hooked = true;
        }

        private void OnDisable()
        {
            if (!hooked) return;
            On.PlayerGraphics.InitiateSprites -= InitiateSprites;
            On.PlayerGraphics.DrawSprites -= DrawSprites;
            On.PlayerGraphics.AddToContainer -= AddToContainer;
            foreach (WeakReference reference in leases)
            {
                RoomCamera.SpriteLeaser leaser = reference.Target as RoomCamera.SpriteLeaser;
                RenderState state;
                if (leaser == null || !states.TryGetValue(leaser, out state)) continue;
                state.RemoveEffects(leaser);
                states.Remove(leaser);
            }
            leases.Clear();
            hooked = false;
        }

        private static bool IsCharacter(PlayerGraphics graphics)
        {
            Player player = graphics.owner as Player;
            return player != null && player.SlugCatClass != null &&
                player.SlugCatClass.value == CharacterId;
        }

        private void InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser leaser, RoomCamera camera)
        {
            RenderState previous;
            if (states.TryGetValue(leaser, out previous))
            {
                previous.RemoveEffects(leaser);
                states.Remove(leaser);
            }
            orig(self, leaser, camera);
            if (!IsCharacter(self)) return;

            RenderState state = new RenderState(leaser, motions.GetValue(self, CreateMotion));
            states.Add(leaser, state);
            leases.RemoveAll(delegate(WeakReference reference)
            {
                return !reference.IsAlive || ReferenceEquals(reference.Target, leaser);
            });
            leases.Add(new WeakReference(leaser));
            state.Attach(leaser, camera, null);
        }

        private static NeuronMotion CreateMotion(PlayerGraphics graphics)
        {
            return new NeuronMotion();
        }

        private void AddToContainer(On.PlayerGraphics.orig_AddToContainer orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser leaser,
            RoomCamera camera, FContainer container)
        {
            orig(self, leaser, camera, container);
            RenderState state;
            if (IsCharacter(self) && states.TryGetValue(leaser, out state))
                state.Attach(leaser, camera, container);
        }

        private void DrawSprites(On.PlayerGraphics.orig_DrawSprites orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser leaser,
            RoomCamera camera, float timeStacker, Vector2 cameraPosition)
        {
            RenderState state;
            bool hasState = states.TryGetValue(leaser, out state);
            // Restore vanilla values before its draw pass, so opacity never
            // compounds and the preceding white tint cannot feed back into it.
            if (hasState) state.RestoreAppearance(leaser);
            orig(self, leaser, camera, timeStacker, cameraPosition);
            if (!hasState) return;
            Player player = self.owner as Player;

            if (!IsCharacter(self) || leaser.deleteMeNextFrame || self.culled ||
                player.room == null || player.room != camera.room ||
                player.slatedForDeletetion || player.inShortcut)
            {
                state.HideEffects(leaser);
                return;
            }

            state.Draw(self, player, leaser, timeStacker, cameraPosition);
        }
    }
}
