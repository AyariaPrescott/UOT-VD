using System;
using BepInEx;
using BepInEx.Logging;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "UOT-VD", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "AyariaPrescott.UOT";

        public static readonly PlayerFeature<float> SuperJump = PlayerFloat("AyariaPrescott.UOT/super_jump");
        public static readonly PlayerFeature<bool> ExplodeOnDeath = PlayerBool("AyariaPrescott.UOT/explode_on_death");
        public static readonly GameFeature<float> MeanLizards = GameFloat("AyariaPrescott.UOT/mean_lizards");
        public static readonly PlayerFeature<bool> RGB_Neuron = PlayerBool("AyariaPrescott.UOT/rgb_neuron");

        public new static ManualLogSource Logger { get; private set; } = null!;

        public void OnEnable()
        {
            Logger = base.Logger;
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            try
            {
                Hooks.ApplyHooks();
                Logger.LogInfo("[UOT-VD] Hooks applied successfully");
            }
            catch (Exception e)
            {
                Logger.LogError($"[UOT-VD] Failed to apply hooks: {e}");
            }
            finally
            {
                orig(self);
            }
        }
    }
}
