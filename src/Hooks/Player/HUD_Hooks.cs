namespace SlugTemplate
{
    /// <summary>
    /// HUD相关钩子 — 在饱食度UI上方添加神经元数量指示器
    /// </summary>
    public static class HUD_Hooks
    {
        public static void ApplyHooks()
        {
            On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
        }

        private static void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
        {
            orig(self, cam);

            self.AddPart(new NeuronHUD(self));
        }
    }
}
