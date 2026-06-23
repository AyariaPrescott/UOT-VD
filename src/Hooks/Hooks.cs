namespace SlugTemplate
{
    /// <summary>
    /// 统一管理所有钩子注册
    /// </summary>
    public static class Hooks
    {
        public static void ApplyHooks()
        {
            Player_Hooks.ApplyHooks();
            Creature_Violence_Hook.ApplyHooks();
            HUD_Hooks.ApplyHooks();
        }
    }
}
