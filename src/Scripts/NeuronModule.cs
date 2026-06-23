using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using SlugBase.SaveData;

namespace SlugTemplate
{
    /// <summary>
    /// 管理每个玩家神经元实例和护盾状态的模块（使用ConditionalWeakTable绑定到Player）
    /// </summary>
    public class NeuronModule
    {
        // 使用ConditionalWeakTable，玩家销毁时自动清理
        private static readonly ConditionalWeakTable<Player, NeuronModule> _data = new();

        /// <summary>
        /// 尝试获取玩家的NeuronModule。如果玩家启用了rgb_neuron但module尚未创建，会自动创建。
        /// 仿照pearlcat的TryGetPearlcatModule模式。
        /// </summary>
        public static bool TryGet(Player player, out NeuronModule module)
        {
            bool hasFeature = Plugin.RGB_Neuron.TryGet(player, out bool hasNeuron) && hasNeuron;

            // Debug模式下始终允许（不需要JSON配置feature）
            if (DEBUG_MODE)
                hasFeature = true;

            if (!hasFeature)
            {
                module = null!;
                return false;
            }

            if (!_data.TryGetValue(player, out module))
            {
                module = new NeuronModule();
                _data.Add(player, module);
                module.PlayerRef = player;
                module.LoadFromSave(player);
            }

            return true;
        }

        private NeuronModule()
        {
        }

        // ---- 神经元实例 ----
        public NeuronHalo Halo { get; set; }
        public List<NeuronZero> Zeros { get; } = new();
        public List<NeuronOne> Ones { get; } = new();
        public List<NeuronTwo> Twos { get; } = new();
        public List<NeuronThreeOne> ThreeOnes { get; } = new();
        public List<NeuronThreeTwo> ThreeTwos { get; } = new();

        // ---- 持久化计数器 ----
        public int PersistedZeroCount { get; set; }
        public int PersistedOneCount { get; set; }
        public int PersistedTwoCount { get; set; }
        public int PersistedThreeOneCount { get; set; }
        public int PersistedThreeTwoCount { get; set; }

        // ---- Shield护盾（ShieldCount与NeuronOne数量同步，消耗护盾时同时销毁NeuronOne） ----
        /// <summary>ShieldActive = 护盾计时器运行中 或 拥有可用的护盾次数</summary>
        public bool ShieldActive => (ShieldTimer > 0 || ShieldCount > 0) && !ShieldDisabled && PlayerRef is not null && !PlayerRef.dead;
        public int ShieldTimer { get; set; }
        public int ShieldCount { get; set; }  // 可用护盾次数 = NeuronOne数量
        public bool ShieldDisabled { get; set; }
        public const int SHIELD_DURATION = 400;  // 10秒 (40fps × 10)
        public const int SHIELD_RECHARGE_TIME = 120;  // 护盾冷却时间（帧）= 3秒
        public int ShieldRechargeTimer { get; set; }
        public const float SHIELD_RADIUS = 90f;
        public ShieldEffect ActiveShieldEffect { get; set; }
        private Player _playerRef;
        public Player PlayerRef
        {
            get => _playerRef != null && !_playerRef.slatedForDeletetion ? _playerRef : null;
            set => _playerRef = value;
        }

        // ---- 移动速度加成 ----
        public const float ZERO_SPEED_BOOST = 0.01f;

        // ---- Debug ----
        public bool DebugNeuronsSpawned { get; set; }
        public const bool DEBUG_MODE = true;
        public const int DEBUG_ZERO_COUNT = 3;

        // ---- Ctrl长按秒杀（需NeuronTwo） ----
        public const int CTRL_HOLD_THRESHOLD = 60;  // 长按阈值（帧）= 1.5秒 (40fps)
        public int CtrlKeyHoldTimer { get; set; }
        public const int CTRL_KILL_COOLDOWN = 120;  // 秒杀冷却时间（帧）= 3秒
        public int CtrlKillCooldownTimer { get; set; }

        // ---- 输入缓存（参考pearlcat的checkInput模式） ----
        public Player.InputPackage UnblockedInput { get; set; }
        /// <summary>当前帧C键是否按下（Input.GetKey持续检测）</summary>
        public bool CPressed { get; set; }
        /// <summary>上一帧C键是否按下，用于检测上升沿（CPressed && !CWasPressed）</summary>
        public bool CWasPressed { get; set; }

        // ---- 时间停止（NeuronThreeOne 能力） ----
        /// <summary>时间停止剩余帧数（40fps × 10秒 = 400帧）</summary>
        public int TimeStopTimer { get; set; }
        /// <summary>时间停止冷却帧数（防止连续使用）</summary>
        public int TimeStopCooldownTimer { get; set; }
        public const int TIME_STOP_DURATION = 400;       // 10秒 (40fps)
        public const int TIME_STOP_COOLDOWN = 200;       // 冷却5秒
        /// <summary>时间停止是否激活中</summary>
        public bool TimeStopActive => TimeStopTimer > 0 && PlayerRef is not null && !PlayerRef.dead;
        /// <summary>当前帧跳跃+拾取是否同时按下（上升沿）</summary>
        public bool JumpPickPressed { get; set; }
        /// <summary>上一帧跳跃+拾取是否同时按下</summary>
        public bool WasJumpPickPressed { get; set; }

        // 静态字段：Room_Update 直接读取，避免跨对象查找失败
        /// <summary>当前时间停止的玩家引用（静态，供Room_Update读取）</summary>
        public static Player GlobalTimeStopPlayer { get; set; }
        /// <summary>全局时间停止是否激活（静态，供Room_Update读取）</summary>
        public static bool GlobalTimeStopActive { get; set; }
        /// <summary>时间停止视觉特效</summary>
        public TimeStopEffect ActiveTimeStopEffect { get; set; }

        // ---- 时间倒流（NeuronThreeTwo 能力，长按A键） ----
        /// <summary>A键是否被长按（供Player_Update读取）</summary>
        public bool AKeyHeld { get; set; }
        /// <summary>上一帧A键是否被长按（用于检测松开事件）</summary>
        public bool AKeyWasHeld { get; set; }
        /// <summary>全局时间倒流是否激活（静态，供Room_Update读取）</summary>
        public static bool GlobalTimeReverseActive { get; set; }
        /// <summary>当前时间倒流的玩家引用（静态，供Room_Update读取）</summary>
        public static Player GlobalTimeReversePlayer { get; set; }
        /// <summary>时间倒流视觉特效</summary>
        public TimeReverseEffect ActiveTimeReverseEffect { get; set; }
        /// <summary>时间倒流冷却帧数</summary>
        public int TimeReverseCooldownTimer { get; set; }
        public const int TIME_REVERSE_COOLDOWN = 120;  // 冷却3秒
        /// <summary>位置历史最大记录帧数（约30秒 @ 40fps）</summary>
        public const int MAX_POSITION_HISTORY = 1200;
        /// <summary>时间倒流每帧回退的帧数（1=实时倒流，越大越快）</summary>
        public const int REVERSE_SPEED = 1;

        // ---- 二段跳（NeuronThreeTwo 被动能力，每个蓝色神经元提供一次额外跳跃） ----
        /// <summary>剩余可用的二段跳次数（落地或抓杆后重置为AliveThreeTwoCount）</summary>
        public int DoubleJumpRemaining { get; set; }
        /// <summary>二段跳刚被触发（用于上升沿检测，防止连续触发）</summary>
        public bool DoubleJumpTriggered { get; set; }
        /// <summary>S键是否被按下（时间停止触发器）</summary>
        public bool SPressed { get; set; }
        /// <summary>上一帧S键是否被按下</summary>
        public bool SWasPressed { get; set; }

        // ---- 持久化 ----
        public void LoadFromSave(Player player)
        {
            if (player.room?.game == null) return;
            var session = player.room.game.GetStorySession;
            if (session?.saveState?.miscWorldSaveData == null) return;

            string key = $"UOT_NeuronData_{player.playerState.playerNumber}";
            var slugBaseData = session.saveState.miscWorldSaveData.GetSlugBaseData();
            if (slugBaseData.TryGet<NeuronSaveData>(key, out var data))
            {
                PersistedZeroCount = data.ZeroCount;
                PersistedOneCount = data.OneCount;
                PersistedTwoCount = data.TwoCount;
                PersistedThreeOneCount = data.ThreeOneCount;
                PersistedThreeTwoCount = data.ThreeTwoCount;
                ShieldCount = data.OneCount;  // 初始化ShieldCount
            }
            else
            {
                PersistedZeroCount = 0;
                PersistedOneCount = 0;
                PersistedTwoCount = 0;
                PersistedThreeOneCount = 0;
                PersistedThreeTwoCount = 0;
                ShieldCount = 0;
            }
        }

        public void SaveToSave(Player player)
        {
            if (player.room?.game == null) return;
            var session = player.room.game.GetStorySession;
            if (session?.saveState?.miscWorldSaveData == null) return;

            string key = $"UOT_NeuronData_{player.playerState.playerNumber}";
            var slugBaseData = session.saveState.miscWorldSaveData.GetSlugBaseData();
            slugBaseData.Set(key, new NeuronSaveData { ZeroCount = PersistedZeroCount, OneCount = PersistedOneCount, TwoCount = PersistedTwoCount, ThreeOneCount = PersistedThreeOneCount, ThreeTwoCount = PersistedThreeTwoCount });
        }

        public void ClearSave(Player player)
        {
            if (player.room?.game == null) return;
            var session = player.room.game.GetStorySession;
            if (session?.saveState?.miscWorldSaveData != null)
            {
                string key = $"UOT_NeuronData_{player.playerState.playerNumber}";
                session.saveState.miscWorldSaveData.GetSlugBaseData().Remove(key);
            }
        }

        // ---- 便捷方法 ----
        public bool HasNeuronOne => AliveOneCount > 0;
        public bool HasNeuronTwo => AliveTwoCount > 0;
        public bool HasNeuronThreeOne => AliveThreeOneCount > 0;
        public bool HasNeuronThreeTwo => AliveThreeTwoCount > 0;

        public int AliveZeroCount
        {
            get
            {
                Zeros.RemoveAll(z => z == null || z.slatedForDeletetion);
                return Zeros.Count;
            }
        }

        public int AliveOneCount
        {
            get
            {
                Ones.RemoveAll(o => o == null || o.slatedForDeletetion);
                return Ones.Count;
            }
        }

        public int AliveTwoCount
        {
            get
            {
                Twos.RemoveAll(t => t == null || t.slatedForDeletetion);
                return Twos.Count;
            }
        }

        public int AliveThreeOneCount
        {
            get
            {
                ThreeOnes.RemoveAll(t => t == null || t.slatedForDeletetion);
                return ThreeOnes.Count;
            }
        }

        public int AliveThreeTwoCount
        {
            get
            {
                ThreeTwos.RemoveAll(t => t == null || t.slatedForDeletetion);
                return ThreeTwos.Count;
            }
        }

        // ---- Shield护盾方法（ShieldCount与NeuronOne同步，消耗护盾时同时销毁NeuronOne） ----
        /// <summary>
        /// 激活护盾。ShieldCount-- 同时销毁一颗NeuronOne本体。
        /// </summary>
        public bool ActivateVisualShield(Player player)
        {
            if (ShieldTimer > 0)
            {
                // 护盾已在运行，刷新计时器
                ShieldTimer = SHIELD_DURATION;
                return true;
            }

            if (ShieldCount <= 0)
            {
                Plugin.Logger.LogInfo($"[UOT-VD] Shield activation FAILED - ShieldCount=0, HasNeuronOne={HasNeuronOne}");
                return false;
            }

            // 消耗ShieldCount
            ShieldCount--;

            // 同时销毁一颗NeuronOne本体
            if (HasNeuronOne)
            {
                var one = Ones[Ones.Count - 1];
                Ones.RemoveAt(Ones.Count - 1);
                one.Destroy();
                PersistedOneCount = AliveOneCount;
            }

            ShieldTimer = SHIELD_DURATION;

            player.room?.PlaySound(SoundID.Bomb_Explode, player.firstChunk.pos, 0.6f, 0.5f);
            player.room?.AddObject(new Explosion.ExplosionLight(player.firstChunk.pos, 100f, 0.6f, 3, new Color(1f, 0.9f, 0.2f)));

            // 销毁旧特效
            if (ActiveShieldEffect != null && !ActiveShieldEffect.slatedForDeletetion)
            {
                ActiveShieldEffect.Destroy();
            }

            // 创建护盾视觉特效
            if (player.room != null)
            {
                var shieldEffect = new ShieldEffect(player, this);
                player.room.AddObject(shieldEffect);
                ActiveShieldEffect = shieldEffect;
            }

            player.AllGraspsLetGoOfThisObject(false);
            SaveToSave(player);

            Plugin.Logger.LogInfo($"[UOT-VD] Shield activated! ShieldCount={ShieldCount}, NeuronOne consumed. ShieldTimer={ShieldTimer}, RemainingOnes={AliveOneCount}");
            return true;
        }

        /// <summary>
        /// 刷新ShieldCount：确保ShieldCount与AliveOneCount同步。
        /// 当NeuronOne数量变化时（新增/销毁），ShieldCount自动跟随。
        /// </summary>
        public void RefreshShieldCount(Player player)
        {
            int aliveCount = AliveOneCount;
            if (ShieldCount != aliveCount)
            {
                ShieldCount = aliveCount;
            }
        }
    }
}
