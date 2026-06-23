using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// Creature.Violence 和 Creature.Grab 钩子（模仿pearlcat的PlayerPearl_Hooks + Player_Hooks模式）
    /// - Creature.Violence: 护盾激活时格挡伤害 + 触发护盾
    /// - Creature.Grab: 护盾激活时弹开抓取生物
    /// </summary>
    public static class Creature_Violence_Hook
    {
        public static void ApplyHooks()
        {
            On.Creature.Violence += Creature_Violence;
            On.Creature.Grab += Creature_Grab;
        }

        // ---- Creature.Violence（模仿pearlcat Player_Hooks.Creature_Violence） ----
        private static void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
        {
            // 只拦截玩家受伤
            if (self is Player player && NeuronModule.TryGet(player, out var mod))
            {
                bool shouldShield = mod.ShieldActive;
                var attacker = source?.owner;

                // 排除非敌对生物（模仿pearlcat）
                if (attacker is JetFish)
                    shouldShield = false;
                if (attacker is Cicada)
                    shouldShield = false;
                if (attacker is Centipede centipede && centipede.Small)
                    shouldShield = false;
                if (damage <= 0.1f)
                    shouldShield = false;

                if (shouldShield)
                {
                    // 如果护盾计时器未运行，触发护盾视觉效果
                    if (mod.ShieldTimer <= 0)
                    {
                        if (!mod.ActivateVisualShield(player))
                        {
                            // 激活失败（无资源），不格挡伤害
                            orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
                            return;
                        }
                    }

                    Plugin.Logger.LogInfo($"[UOT-VD] Violence blocked by ShieldActive! damage={damage}, attacker={attacker?.GetType().Name ?? "null"}, ShieldTimer={mod.ShieldTimer}, ShieldCount={mod.ShieldCount}");

                    // 格挡伤害
                    return;
                }
            }

            orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
        }

        // ---- Creature.Grab（模仿pearlcat PlayerPearl_Hooks.Creature_Grab） ----
        private static bool Creature_Grab(On.Creature.orig_Grab orig, Creature self, PhysicalObject obj, int graspUsed, int chunkGrabbed, Creature.Grasp.Shareability shareability, float dominance, bool overrideEquallyDominant, bool pacifying)
        {
            var result = orig(self, obj, graspUsed, chunkGrabbed, shareability, dominance, overrideEquallyDominant, pacifying);

            // 如果被抓的是玩家且护盾激活
            if (obj is Player player && NeuronModule.TryGet(player, out var mod) && mod.ShieldActive)
            {
                // 使用反射调用 IsHostileToMe（模仿pearlcat的 player.IsHostileToMe(self)）
                bool isHostile = false;
                try
                {
                    var method = typeof(Creature).GetMethod("IsHostileToMe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                        isHostile = (bool)method.Invoke(self, new object[] { player });
                }
                catch { }

                if (isHostile || self is Lizard)
                {
                    // 蜈蚣特殊处理：护盾期间不弹开（模仿pearlcat）
                    if (!(self is Centipede && mod.ShieldTimer > 0))
                    {
                        self.room?.AddObject(new Explosion.ExplosionLight(self.DangerPos, 40f, 0.4f, 2, new Color(1f, 0.9f, 0.2f)));
                    }

                    // 触发护盾视觉效果，只有成功激活才弹开
                    if (mod.ActivateVisualShield(player))
                    {
                        self.Stun(10);
                        self.ReleaseGrasp(graspUsed);
                        Plugin.Logger.LogInfo($"[UOT-VD] Creature.Grab blocked by shield! grabber={self.GetType().Name}");
                        return false;
                    }
                }
            }

            return result;
        }
    }
}
