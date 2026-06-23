using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RWCustom;

namespace SlugTemplate
{
    /// <summary>
    /// Player.Update 钩子：管理NeuronHalo/Zero/One的创建、维护、速度加成、Shield护盾、C键长按
    /// </summary>
    public static class Player_Hooks
    {
        public static void ApplyHooks()
        {
            On.Player.Update += Player_Update;
            On.Player.checkInput += Player_checkInput;
            On.Player.Die += Player_Die;
            On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdated;
            On.Room.Update += Room_Update;
        }

        // checkInput hook：缓存玩家输入（参考pearlcat模式）
        private static void Player_checkInput(On.Player.orig_checkInput orig, Player self)
        {
            orig(self);

            if (!NeuronModule.TryGet(self, out var mod)) return;

            var input = self.input[0];
            mod.UnblockedInput = input;

            // 用 Input.GetKey 持续检测 C 键（参考pearlcat IsSentryKeybindPressed）
            mod.CPressed = GetKey(KeyCode.C);

            // 检测A键（时间倒流触发器，NeuronThreeTwo能力）
            mod.AKeyHeld = GetKey(KeyCode.A);

            // 检测跳跃+拾取同时按下（二段跳触发器，上升沿检测）
            bool jumpPickNow = input.jmp && input.pckp;
            mod.JumpPickPressed = jumpPickNow && !mod.WasJumpPickPressed;  // 上升沿
            mod.WasJumpPickPressed = jumpPickNow;  // 保存当前帧状态供下帧比较

            // 检测S键（时间停止触发器）
            mod.SPressed = GetKey(KeyCode.S);
            // 注意：上升沿在Player_Update中用 SPressed && !SWasPressed 检测
        }

        // 反射调用 Input.GetKey（避免直接引用 UnityEngine.InputLegacyModule）
        private static MethodInfo _inputGetKeyMethod;
        private static bool GetKey(KeyCode key)
        {
            if (_inputGetKeyMethod == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var inputType = asm.GetType("UnityEngine.Input");
                    if (inputType != null)
                    {
                        _inputGetKeyMethod = inputType.GetMethod("GetKey", new[] { typeof(KeyCode) });
                        if (_inputGetKeyMethod != null) break;
                    }
                }
            }
            if (_inputGetKeyMethod != null)
                return (bool)_inputGetKeyMethod.Invoke(null, new object[] { key });
            return false;
        }

        private static Type _singularityBombType;
        private static Type _iProvideEdibleType;
        private static bool _typesResolved;
        private static void ResolveTypes()
        {
            if (_typesResolved) return;
            _typesResolved = true;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_singularityBombType == null)
                {
                    _singularityBombType = asm.GetType("SingularityBomb");
                    if (_singularityBombType == null)
                        _singularityBombType = asm.GetType("MoreSlugcats.SingularityBomb");
                }
                if (_singularityBombType != null) break;
            }
            if (_singularityBombType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name.Contains("Singularity") && t.Name.Contains("Bomb"))
                            {
                                _singularityBombType = t;
                                break;
                            }
                        }
                    }
                    catch { }
                    if (_singularityBombType != null) break;
                }
            }
        }

        private static bool IsFoodObject(PhysicalObject obj)
        {
            if (obj == null) return false;
            if (_iProvideEdibleType == null)
            {
                foreach (var iface in obj.GetType().GetInterfaces())
                {
                    if (iface.Name == "IProvideEdible")
                    {
                        _iProvideEdibleType = iface;
                        break;
                    }
                }
            }
            if (_iProvideEdibleType != null)
                return _iProvideEdibleType.IsAssignableFrom(obj.GetType());
            return false;
        }

        // ---- Player.Update ----
        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self.room == null) return;
            if (!NeuronModule.TryGet(self, out var mod)) return;

            // Debug模式：首次自动生成神经元（每种1个）
            if (NeuronModule.DEBUG_MODE && !mod.DebugNeuronsSpawned)
            {
                mod.DebugNeuronsSpawned = true;
                Plugin.Logger.LogInfo($"[UOT-VD] Debug mode ON: spawning 1x each neuron type");

                // NeuronZero
                if (mod.AliveZeroCount == 0)
                {
                    var zero = new NeuronZero(self);
                    self.room.AddObject(zero);
                    mod.Zeros.Add(zero);
                }
                mod.PersistedZeroCount = mod.AliveZeroCount;

                // NeuronOne
                if (mod.AliveOneCount == 0)
                {
                    var one = new NeuronOne(self);
                    self.room.AddObject(one);
                    mod.Ones.Add(one);
                }
                mod.PersistedOneCount = mod.AliveOneCount;
                mod.ShieldCount = mod.AliveOneCount;

                // NeuronTwo
                if (mod.AliveTwoCount == 0)
                {
                    var two = new NeuronTwo(self);
                    self.room.AddObject(two);
                    mod.Twos.Add(two);
                }
                mod.PersistedTwoCount = mod.AliveTwoCount;

                // NeuronThreeOne
                if (mod.AliveThreeOneCount == 0)
                {
                    var threeOne = new NeuronThreeOne(self);
                    self.room.AddObject(threeOne);
                    mod.ThreeOnes.Add(threeOne);
                }
                mod.PersistedThreeOneCount = mod.AliveThreeOneCount;

                // NeuronThreeTwo
                if (mod.AliveThreeTwoCount == 0)
                {
                    var threeTwo = new NeuronThreeTwo(self);
                    self.room.AddObject(threeTwo);
                    mod.ThreeTwos.Add(threeTwo);
                }
                mod.PersistedThreeTwoCount = mod.AliveThreeTwoCount;

                mod.SaveToSave(self);
                Plugin.Logger.LogInfo($"[UOT-VD] Debug spawn complete: Zeros={mod.AliveZeroCount}, Ones={mod.AliveOneCount}, Twos={mod.AliveTwoCount}, ThreeOnes={mod.AliveThreeOneCount}, ThreeTwos={mod.AliveThreeTwoCount}");
            }

            // 维护NeuronHalo
            if (mod.Halo == null || mod.Halo.slatedForDeletetion)
            {
                mod.Halo = new NeuronHalo(self);
                self.room.AddObject(mod.Halo);
            }

            // 清理已销毁的神经元并同步持久计数器
            int beforeZero = mod.Zeros.Count;
            mod.Zeros.RemoveAll(z => z == null || z.slatedForDeletetion);
            if (mod.Zeros.Count < beforeZero)
            {
                mod.PersistedZeroCount = mod.Zeros.Count;
                mod.SaveToSave(self);
            }
            int beforeOne = mod.Ones.Count;
            mod.Ones.RemoveAll(o => o == null || o.slatedForDeletetion);
            if (mod.Ones.Count < beforeOne)
            {
                mod.PersistedOneCount = mod.Ones.Count;
                mod.SaveToSave(self);
            }
            int beforeTwo = mod.Twos.Count;
            mod.Twos.RemoveAll(t => t == null || t.slatedForDeletetion);
            if (mod.Twos.Count < beforeTwo)
            {
                mod.PersistedTwoCount = mod.Twos.Count;
                mod.SaveToSave(self);
            }
            int beforeThreeOne = mod.ThreeOnes.Count;
            mod.ThreeOnes.RemoveAll(t => t == null || t.slatedForDeletetion);
            if (mod.ThreeOnes.Count < beforeThreeOne)
            {
                mod.PersistedThreeOneCount = mod.ThreeOnes.Count;
                mod.SaveToSave(self);
            }
            int beforeThreeTwo = mod.ThreeTwos.Count;
            mod.ThreeTwos.RemoveAll(t => t == null || t.slatedForDeletetion);
            if (mod.ThreeTwos.Count < beforeThreeTwo)
            {
                mod.PersistedThreeTwoCount = mod.ThreeTwos.Count;
                mod.SaveToSave(self);
            }

            // 刷新ShieldCount（与NeuronOne数量同步）
            mod.RefreshShieldCount(self);

            // 护盾冷却倒计时（冷却结束恢复ShieldCount）
            if (mod.ShieldRechargeTimer > 0)
            {
                mod.ShieldRechargeTimer--;
                if (mod.ShieldRechargeTimer == 0)
                {
                    // 冷却完毕，ShieldCount恢复（但受NeuronOne数量上限约束）
                    mod.RefreshShieldCount(self);
                    Plugin.Logger.LogInfo($"[UOT-VD] Shield cooldown finished! ShieldCount={mod.ShieldCount}, NeuronOneCount={mod.AliveOneCount}");
                }
            }

            // 每60帧输出一次状态
            if (Time.frameCount % 60 == 0)
            {
                Plugin.Logger.LogInfo($"[UOT-VD] Status: AliveOneCount={mod.AliveOneCount}, ShieldActive={mod.ShieldActive}, ShieldTimer={mod.ShieldTimer}, ShieldCount={mod.ShieldCount}");
            }

            // 雨眠后恢复：Debug模式下将所有神经元数量补到1个
            if (NeuronModule.DEBUG_MODE)
            {
                // Debug：每种神经元至少1个
                if (mod.AliveZeroCount < 1)
                {
                    var newZero = new NeuronZero(self);
                    self.room.AddObject(newZero);
                    mod.Zeros.Add(newZero);
                    mod.PersistedZeroCount = mod.AliveZeroCount;
                }
                if (mod.AliveOneCount < 1)
                {
                    var newOne = new NeuronOne(self);
                    self.room.AddObject(newOne);
                    mod.Ones.Add(newOne);
                    mod.PersistedOneCount = mod.AliveOneCount;
                    mod.ShieldCount = mod.AliveOneCount;
                }
                if (mod.AliveTwoCount < 1)
                {
                    var newTwo = new NeuronTwo(self);
                    self.room.AddObject(newTwo);
                    mod.Twos.Add(newTwo);
                    mod.PersistedTwoCount = mod.AliveTwoCount;
                }
                if (mod.AliveThreeOneCount < 1)
                {
                    var newThreeOne = new NeuronThreeOne(self);
                    self.room.AddObject(newThreeOne);
                    mod.ThreeOnes.Add(newThreeOne);
                    mod.PersistedThreeOneCount = mod.AliveThreeOneCount;
                }
                if (mod.AliveThreeTwoCount < 1)
                {
                    var newThreeTwo = new NeuronThreeTwo(self);
                    self.room.AddObject(newThreeTwo);
                    mod.ThreeTwos.Add(newThreeTwo);
                    mod.PersistedThreeTwoCount = mod.AliveThreeTwoCount;
                }
                mod.SaveToSave(self);
            }
            else
            {
                // 正常模式：恢复至持久化数量
                if (mod.PersistedZeroCount > 0 && mod.AliveZeroCount < mod.PersistedZeroCount)
                {
                    for (int i = mod.AliveZeroCount; i < mod.PersistedZeroCount; i++)
                    {
                        var newZero = new NeuronZero(self);
                        self.room.AddObject(newZero);
                        mod.Zeros.Add(newZero);
                    }
                }
                if (mod.PersistedOneCount > 0 && mod.AliveOneCount < mod.PersistedOneCount)
                {
                    for (int i = mod.AliveOneCount; i < mod.PersistedOneCount; i++)
                    {
                        var newOne = new NeuronOne(self);
                        self.room.AddObject(newOne);
                        mod.Ones.Add(newOne);
                    }
                    mod.ShieldCount = mod.AliveOneCount;
                }
                if (mod.PersistedTwoCount > 0 && mod.AliveTwoCount < mod.PersistedTwoCount)
                {
                    for (int i = mod.AliveTwoCount; i < mod.PersistedTwoCount; i++)
                    {
                        var newTwo = new NeuronTwo(self);
                        self.room.AddObject(newTwo);
                        mod.Twos.Add(newTwo);
                    }
                }
                if (mod.PersistedThreeOneCount > 0 && mod.AliveThreeOneCount < mod.PersistedThreeOneCount)
                {
                    for (int i = mod.AliveThreeOneCount; i < mod.PersistedThreeOneCount; i++)
                    {
                        var newThreeOne = new NeuronThreeOne(self);
                        self.room.AddObject(newThreeOne);
                        mod.ThreeOnes.Add(newThreeOne);
                    }
                }
                if (mod.PersistedThreeTwoCount > 0 && mod.AliveThreeTwoCount < mod.PersistedThreeTwoCount)
                {
                    for (int i = mod.AliveThreeTwoCount; i < mod.PersistedThreeTwoCount; i++)
                    {
                        var newThreeTwo = new NeuronThreeTwo(self);
                        self.room.AddObject(newThreeTwo);
                        mod.ThreeTwos.Add(newThreeTwo);
                    }
                }
            }

            // 速度加成
            int zeroCount = mod.AliveZeroCount;
            float speedMult = 1f + zeroCount * NeuronModule.ZERO_SPEED_BOOST;
            for (int i = 0; i < self.bodyChunks.Length; i++)
            {
                if (self.bodyChunks[i].vel.magnitude > 0.1f)
                    self.bodyChunks[i].vel *= speedMult;
            }

            // Shield护盾持续期间（模仿pearlcat PlayerAbilities_Helpers_Shield.Update）
            if (mod.ShieldTimer > 0)
            {
                // 持续释放所有抓住玩家的物体（模仿pearlcat的 self.AllGraspsLetGoOfThisObject(false)）
                self.AllGraspsLetGoOfThisObject(false);

                mod.ShieldTimer--;

                // 护盾期间保持呼吸（模仿pearlcat的 self.airInLungs = 1.0f）
                self.airInLungs = 1.0f;

                // 护盾到期：启动冷却计时器
                if (mod.ShieldTimer == 0)
                {
                    mod.ShieldRechargeTimer = NeuronModule.SHIELD_RECHARGE_TIME;
                    self.room?.PlaySound(SoundID.Bomb_Explode, self.firstChunk.pos, 0.3f, 0.3f);
                }

                // 弹开附近的投掷物（模仿pearlcat的 roomObjects 遍历）
                var roomObjects = self.room?.updateList;
                if (roomObjects != null)
                {
                    for (int i = roomObjects.Count - 1; i >= 0; i--)
                    {
                        var obj = roomObjects[i];
                        if (obj is Weapon weapon && weapon.mode == Weapon.Mode.Thrown && weapon.thrownBy != self)
                        {
                            // 不弹开背上玩家扔的矛（模仿pearlcat）
                            if (weapon.thrownBy is Player thrownByPlayer && thrownByPlayer.onBack == self)
                                continue;

                            if (Custom.DistLess(weapon.firstChunk.pos, self.firstChunk.pos, NeuronModule.SHIELD_RADIUS))
                            {
                                weapon.ChangeMode(Weapon.Mode.Free);
                                weapon.SetRandomSpin();
                                weapon.firstChunk.vel *= -0.2f;
                                self.room?.AddObject(new Explosion.ExplosionLight(weapon.firstChunk.pos, 40f, 0.4f, 2, new Color(1f, 0.9f, 0.2f)));
                            }
                        }
                        else if (obj is LizardSpit spit)
                        {
                            if (Custom.DistLess(spit.pos, self.firstChunk.pos, NeuronModule.SHIELD_RADIUS))
                            {
                                spit.vel = Vector2.zero;
                                self.room?.AddObject(new Explosion.ExplosionLight(spit.pos, 30f, 0.3f, 2, new Color(1f, 0.9f, 0.2f)));
                            }
                        }
                    }
                }
            }

            // NeuronTwo效果：肺活量翻倍 + 溪流身体素质
            if (mod.HasNeuronTwo)
            {
                var stats = self.slugcatStats;
                // 肺活量翻倍
                stats.lungsFac = 2.0f;
                // 溪流身体素质：高速度、低体重
                stats.runspeedFac = 1.35f;
                stats.corridorClimbSpeedFac = 1.2f;
                stats.poleClimbSpeedFac = 1.25f;
                stats.bodyWeightFac = 0.85f;
            }

            // ---- 多段跳（NeuronThreeTwo 被动能力，每个蓝色神经元提供一次额外跳跃） ----
            if (mod.HasNeuronThreeTwo)
            {
                // 重置条件（落地、站立、抓取等，参考pearlcat animWhichResetsCooldown）
                bool shouldReset = self.canJump > 0  // 可以正常跳跃 = 在地面上
                    || self.standing
                    || self.bodyMode == Player.BodyModeIndex.Crawl
                    || self.bodyMode == Player.BodyModeIndex.CorridorClimb
                    || self.bodyMode == Player.BodyModeIndex.ClimbIntoShortCut
                    || self.bodyMode == Player.BodyModeIndex.WallClimb
                    || self.bodyMode == Player.BodyModeIndex.ClimbingOnBeam
                    || self.bodyMode == Player.BodyModeIndex.Swimming
                    || self.animation == Player.AnimationIndex.HangFromBeam
                    || self.animation == Player.AnimationIndex.ClimbOnBeam
                    || self.animation == Player.AnimationIndex.AntlerClimb
                    || self.animation == Player.AnimationIndex.VineGrab
                    || self.animation == Player.AnimationIndex.ZeroGPoleGrab;

                if (shouldReset)
                {
                    // 重置为当前蓝色神经元数量（每个NeuronThreeTwo提供一次额外跳跃）
                    mod.DoubleJumpRemaining = mod.AliveThreeTwoCount;
                    mod.DoubleJumpTriggered = false;
                }

                // 检测二段跳输入：使用 JumpPickPressed 上升沿（在checkInput中已计算好）
                // 多段跳可用条件
                bool canDoubleJump = mod.DoubleJumpRemaining > 0
                    && self.Consious
                    && self.bodyMode != Player.BodyModeIndex.Crawl
                    && self.bodyMode != Player.BodyModeIndex.CorridorClimb
                    && self.bodyMode != Player.BodyModeIndex.ClimbIntoShortCut
                    && self.bodyMode != Player.BodyModeIndex.WallClimb
                    && self.bodyMode != Player.BodyModeIndex.Swimming
                    && self.animation != Player.AnimationIndex.HangFromBeam
                    && self.animation != Player.AnimationIndex.ClimbOnBeam
                    && self.animation != Player.AnimationIndex.AntlerClimb
                    && self.animation != Player.AnimationIndex.VineGrab
                    && self.animation != Player.AnimationIndex.ZeroGPoleGrab
                    && self.onBack is null
                    && !self.standing;  // 不在地面上

                if (mod.JumpPickPressed && canDoubleJump)
                {
                    mod.DoubleJumpRemaining--;
                    mod.DoubleJumpTriggered = true;

                    // 短暂阻止抓取（参考pearlcat self.noGrabCounter = 5）
                    self.noGrabCounter = 5;

                    // 速度逻辑（参考pearlcat Agility速度计算）
                    if (self.bodyMode == Player.BodyModeIndex.ZeroG || (self.room?.gravity ?? 1f) == 0f || self.gravity == 0f)
                    {
                        // 零重力环境
                        float inputX = self.input[0].x;
                        float inputY = self.input[0].y;
                        self.bodyChunks[0].vel.x = 9f * inputX;
                        self.bodyChunks[0].vel.y = 9f * inputY;
                        self.bodyChunks[1].vel.x = 8f * inputX;
                        self.bodyChunks[1].vel.y = 8f * inputY;
                    }
                    else
                    {
                        // 正常重力环境
                        if (self.input[0].x != 0)
                        {
                            self.bodyChunks[0].vel.y = Mathf.Min(self.bodyChunks[0].vel.y, 0f) + 8f;
                            self.bodyChunks[1].vel.y = Mathf.Min(self.bodyChunks[1].vel.y, 0f) + 7f;
                            self.jumpBoost = 6f;
                        }

                        if (self.input[0].x == 0 || self.input[0].y == 1)
                        {
                            self.bodyChunks[0].vel.y = 16f;
                            self.bodyChunks[1].vel.y = 15f;
                            self.jumpBoost = 8f;
                        }

                        if (self.input[0].y == 1)
                        {
                            self.bodyChunks[0].vel.x = 10f * self.input[0].x;
                            self.bodyChunks[1].vel.x = 8f * self.input[0].x;
                        }
                        else
                        {
                            self.bodyChunks[0].vel.x = 15f * self.input[0].x;
                            self.bodyChunks[1].vel.x = 13f * self.input[0].x;
                        }

                        self.animation = Player.AnimationIndex.Flip;
                        self.bodyMode = Player.BodyModeIndex.Default;
                    }

                    // 视觉效果（参考pearlcat 爆炸光 + 粒子）
                    self.room?.PlaySound(SoundID.Fire_Spear_Explode, self.firstChunk.pos, 0.2f, 0.8f);
                    self.room?.AddObject(new Explosion.ExplosionLight(self.firstChunk.pos, 160f, 1f, 3, new Color(0.2f, 0.4f, 1f)));

                    // 蓝色粒子爆发
                    for (int j = 0; j < 10; j++)
                    {
                        var randVec = Custom.RNV();
                        self.room?.AddObject(new Spark(
                            self.firstChunk.pos + randVec * UnityEngine.Random.value * 40f,
                            randVec * Mathf.Lerp(4f, 30f, UnityEngine.Random.value),
                            new Color(0.2f, 0.4f, 1f), null, 4, 18));
                    }

                    Plugin.Logger.LogInfo($"[UOT-VD] Double jump triggered! Remaining={mod.DoubleJumpRemaining}/{mod.AliveThreeTwoCount}");
                }
            }

            // 时间停止倒计时
            if (mod.TimeStopTimer > 0)
            {
                mod.TimeStopTimer--;
                // 更新时间停止特效的剩余时间
                if (mod.ActiveTimeStopEffect != null && !mod.ActiveTimeStopEffect.slatedForDeletetion)
                {
                    mod.ActiveTimeStopEffect.SetRemaining((float)mod.TimeStopTimer / NeuronModule.TIME_STOP_DURATION);
                }
                if (mod.TimeStopTimer == 0)
                {
                    Plugin.Logger.LogInfo($"[UOT-VD] Time stop ended!");
                    NeuronModule.GlobalTimeStopActive = false;
                    NeuronModule.GlobalTimeStopPlayer = null;
                    // 特效会在自身Update中检测到GlobalTimeStopActive==false后自毁
                }
            }
            if (mod.TimeStopCooldownTimer > 0)
            {
                mod.TimeStopCooldownTimer--;
            }

            // S键按下 → 时间停止（需NeuronThreeOne）
            if (mod.SPressed && !mod.SWasPressed && mod.HasNeuronThreeOne && mod.TimeStopCooldownTimer <= 0 && mod.TimeStopTimer <= 0)
            {
                // 消耗一颗NeuronThreeOne
                var threeOne = mod.ThreeOnes[mod.ThreeOnes.Count - 1];
                mod.ThreeOnes.RemoveAt(mod.ThreeOnes.Count - 1);
                threeOne.Destroy();
                mod.PersistedThreeOneCount = mod.AliveThreeOneCount;

                // 激活时间停止
                mod.TimeStopTimer = NeuronModule.TIME_STOP_DURATION;
                mod.TimeStopCooldownTimer = NeuronModule.TIME_STOP_COOLDOWN;
                NeuronModule.GlobalTimeStopActive = true;
                NeuronModule.GlobalTimeStopPlayer = self;
                mod.SaveToSave(self);

                // 创建时间停止特效
                if (self.room != null)
                {
                    // 销毁旧特效
                    if (mod.ActiveTimeStopEffect != null && !mod.ActiveTimeStopEffect.slatedForDeletetion)
                        mod.ActiveTimeStopEffect.Destroy();

                    var timeStopEffect = new TimeStopEffect(self);
                    self.room.AddObject(timeStopEffect);
                    mod.ActiveTimeStopEffect = timeStopEffect;
                }

                self.room?.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, self.firstChunk.pos, 0.8f, 0.3f);
                self.room?.AddObject(new Explosion.ExplosionLight(self.firstChunk.pos, 200f, 0.5f, 4, new Color(0.1f, 0.9f, 0.2f)));

                Plugin.Logger.LogInfo($"[UOT-VD] Time stop activated! Consumed NeuronThreeOne. Remaining ThreeOnes={mod.AliveThreeOneCount}, TimeStopTimer={mod.TimeStopTimer}");
            }

            // ---- 时间倒流（NeuronThreeTwo 能力，长按A键） ----
            if (mod.HasNeuronThreeTwo)
            {
                // 冷却倒计时
                if (mod.TimeReverseCooldownTimer > 0)
                {
                    mod.TimeReverseCooldownTimer--;
                }

                if (mod.AKeyHeld && mod.TimeReverseCooldownTimer <= 0 && !NeuronModule.GlobalTimeStopActive)
                {
                    // 长按A键：激活时间倒流
                    if (!NeuronModule.GlobalTimeReverseActive)
                    {
                        // 首次按下A键，激活时间倒流
                        NeuronModule.GlobalTimeReverseActive = true;
                        NeuronModule.GlobalTimeReversePlayer = self;
                        StartTimeReverse();

                        // 创建时间倒流特效
                        if (self.room != null)
                        {
                            if (mod.ActiveTimeReverseEffect != null && !mod.ActiveTimeReverseEffect.slatedForDeletetion)
                                mod.ActiveTimeReverseEffect.Destroy();

                            var reverseEffect = new TimeReverseEffect(self);
                            self.room.AddObject(reverseEffect);
                            mod.ActiveTimeReverseEffect = reverseEffect;
                        }

                        Plugin.Logger.LogInfo($"[UOT-VD] Time reverse activated! (A key held)");
                    }
                }
                else if (mod.AKeyWasHeld && !mod.AKeyHeld && NeuronModule.GlobalTimeReverseActive)
                {
                    // A键松开：停止时间倒流，消耗一颗NeuronThreeTwo
                    NeuronModule.GlobalTimeReverseActive = false;
                    NeuronModule.GlobalTimeReversePlayer = null;
                    StopTimeReverse();

                    // 销毁倒流特效
                    if (mod.ActiveTimeReverseEffect != null && !mod.ActiveTimeReverseEffect.slatedForDeletetion)
                    {
                        mod.ActiveTimeReverseEffect.Destroy();
                        mod.ActiveTimeReverseEffect = null;
                    }

                    // 消耗一颗蓝色神经元
                    var threeTwo = mod.ThreeTwos[mod.ThreeTwos.Count - 1];
                    mod.ThreeTwos.RemoveAt(mod.ThreeTwos.Count - 1);
                    threeTwo.Destroy();
                    mod.PersistedThreeTwoCount = mod.AliveThreeTwoCount;

                    mod.TimeReverseCooldownTimer = NeuronModule.TIME_REVERSE_COOLDOWN;
                    mod.SaveToSave(self);

                    self.room?.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, self.firstChunk.pos, 0.8f, 0.5f);
                    self.room?.AddObject(new Explosion.ExplosionLight(self.firstChunk.pos, 180f, 0.5f, 4, new Color(0.15f, 0.35f, 0.9f)));

                    Plugin.Logger.LogInfo($"[UOT-VD] Time reverse ended! Consumed NeuronThreeTwo. Remaining ThreeTwos={mod.AliveThreeTwoCount}");
                }

                // 更新上帧A键状态
                mod.AKeyWasHeld = mod.AKeyHeld;
            }
            else
            {
                // 没有NeuronThreeTwo时，确保时间倒流关闭
                if (NeuronModule.GlobalTimeReverseActive)
                {
                    NeuronModule.GlobalTimeReverseActive = false;
                    NeuronModule.GlobalTimeReversePlayer = null;
                    StopTimeReverse();
                    // 销毁倒流特效
                    if (mod.ActiveTimeReverseEffect != null && !mod.ActiveTimeReverseEffect.slatedForDeletetion)
                    {
                        mod.ActiveTimeReverseEffect.Destroy();
                        mod.ActiveTimeReverseEffect = null;
                    }
                }
                mod.AKeyWasHeld = false;
            }

            // C键单击逻辑（参考pearlcat: abilityInput && !wasAbilityInput）
            if (mod.CPressed && !mod.CWasPressed)
            {
                HandleCKeyAction(self, mod);
            }

            // Ctrl长按秒杀逻辑（需要NeuronTwo）
            if (mod.HasNeuronTwo)
            {
                bool ctrlHeld = GetKey(KeyCode.LeftControl) || GetKey(KeyCode.RightControl);
                if (ctrlHeld)
                {
                    mod.CtrlKeyHoldTimer++;
                    if (mod.CtrlKeyHoldTimer >= NeuronModule.CTRL_HOLD_THRESHOLD && mod.CtrlKillCooldownTimer <= 0)
                    {
                        HandleCtrlHoldAction(self, mod);
                        mod.CtrlKeyHoldTimer = 0;
                        mod.CtrlKillCooldownTimer = NeuronModule.CTRL_KILL_COOLDOWN;
                    }
                }
                else
                {
                    mod.CtrlKeyHoldTimer = 0;
                }

                // 冷却倒计时
                if (mod.CtrlKillCooldownTimer > 0)
                    mod.CtrlKillCooldownTimer--;
            }
            else
            {
                mod.CtrlKeyHoldTimer = 0;
            }

            // 更新上帧C键和S键状态（参考pearlcat WasSentryInput = sentryInput）
            mod.CWasPressed = mod.CPressed;
            mod.SWasPressed = mod.SPressed;
        }

        private static Type _dataPearlType;
        private static Type _abstractDataPearlType;
        private static Type _extEnumBaseType;
        private static bool IsWhitePearl(PhysicalObject obj)
        {
            if (obj == null) return false;

            // 解析类型
            if (_dataPearlType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    _dataPearlType = asm.GetType("DataPearl");
                    if (_dataPearlType == null) _dataPearlType = asm.GetType("MoreSlugcats.DataPearl");
                    _abstractDataPearlType = asm.GetType("DataPearl+AbstractDataPearl");
                    if (_abstractDataPearlType == null) _abstractDataPearlType = asm.GetType("MoreSlugcats.DataPearl+AbstractDataPearl");
                    _extEnumBaseType = asm.GetType("ExtEnum`1");
                    if (_extEnumBaseType == null) _extEnumBaseType = asm.GetType("ExtEnumBase");
                    if (_abstractDataPearlType != null && _dataPearlType != null) break;
                }
            }
            if (_dataPearlType == null || _abstractDataPearlType == null)
            {
                Plugin.Logger.LogWarning("[UOT-VD] IsWhitePearl: Could not find DataPearl type!");
                return false;
            }

            // 检查 obj 是否为 DataPearl
            if (!_dataPearlType.IsAssignableFrom(obj.GetType())) return false;

            // 获取 AbstractPearl 属性
            var abstractPearlProp = _dataPearlType.GetProperty("AbstractPearl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (abstractPearlProp == null)
            {
                Plugin.Logger.LogWarning("[UOT-VD] IsWhitePearl: Could not find AbstractPearl property!");
                return false;
            }
            var abstractPearl = abstractPearlProp.GetValue(obj);
            if (abstractPearl == null) return false;

            // 获取 dataPearlType 属性/字段
            var dataPearlTypeProp = _abstractDataPearlType.GetProperty("dataPearlType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var dataPearlTypeField = _abstractDataPearlType.GetField("dataPearlType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object dataPearlType = null;
            if (dataPearlTypeProp != null)
                dataPearlType = dataPearlTypeProp.GetValue(abstractPearl);
            else if (dataPearlTypeField != null)
                dataPearlType = dataPearlTypeField.GetValue(abstractPearl);
            
            if (dataPearlType == null)
            {
                Plugin.Logger.LogWarning("[UOT-VD] IsWhitePearl: Could not read dataPearlType!");
                return false;
            }

            // 获取 ExtEnum 的 value（字段或属性）
            var dtType = dataPearlType.GetType();
            string value = null;

            // 尝试 .value 字段
            var valueField = dtType.GetField("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (valueField != null)
                value = valueField.GetValue(dataPearlType) as string;

            // 尝试 .value 属性
            if (value == null)
            {
                var valueProp = dtType.GetProperty("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (valueProp != null)
                    value = valueProp.GetValue(dataPearlType) as string;
            }

            // 兜底：ToString()
            if (value == null)
                value = dataPearlType.ToString();

            // 检查 Index 属性（某些白色珍珠 Index == -1）
            var indexProp = dtType.GetProperty("Index", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var indexField = dtType.GetField("Index", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int index = int.MaxValue;
            if (indexProp != null)
                index = (int)indexProp.GetValue(dataPearlType);
            else if (indexField != null)
                index = (int)indexField.GetValue(dataPearlType);

            bool isWhite = value == "Misc" || value == "Misc2" || index == -1;

            Plugin.Logger.LogInfo($"[UOT-VD] IsWhitePearl: objType={obj.GetType().Name}, value={value}, index={index}, isWhite={isWhite}");
            return isWhite;
        }

        // 检测是否为彩色珍珠（DataPearl 且非白色）
        private static bool IsColorPearl(PhysicalObject obj)
        {
            if (obj == null) return false;

            // 先确认是 DataPearl
            if (_dataPearlType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    _dataPearlType = asm.GetType("DataPearl");
                    if (_dataPearlType == null) _dataPearlType = asm.GetType("MoreSlugcats.DataPearl");
                    if (_dataPearlType != null) break;
                }
            }
            if (_dataPearlType == null) return false;
            if (!_dataPearlType.IsAssignableFrom(obj.GetType())) return false;

            // 排除白色珍珠，其余都是彩色
            return !IsWhitePearl(obj);
        }

        private static void HandleCKeyAction(Player self, NeuronModule mod)
        {
            KarmaFlower heldFlower = null;
            bool heldRock = false;
            PhysicalObject heldWhitePearl = null;
            PhysicalObject heldColorPearl = null;
            PhysicalObject heldOther = null;
            int heldItemGraspIndex = -1;

            for (int i = 0; i < self.grasps.Length; i++)
            {
                if (self.grasps[i] == null) continue;
                if (self.grasps[i].grabbed is KarmaFlower flower)
                {
                    heldFlower = flower;
                    heldItemGraspIndex = i;
                    break;
                }
                if (self.grasps[i].grabbed is Rock)
                {
                    heldRock = true;
                    heldItemGraspIndex = i;
                    break;
                }
                // 检查是否为白色珍珠
                if (IsWhitePearl(self.grasps[i].grabbed))
                {
                    heldWhitePearl = self.grasps[i].grabbed;
                    heldItemGraspIndex = i;
                    break;
                }
                // 检查是否为彩色珍珠
                if (IsColorPearl(self.grasps[i].grabbed))
                {
                    heldColorPearl = self.grasps[i].grabbed;
                    heldItemGraspIndex = i;
                    break;
                }
                // 手持任意其他物品
                heldOther = self.grasps[i].grabbed;
                heldItemGraspIndex = i;
                break;
            }

            int zeroCount = mod.AliveZeroCount;

            // 条件1：手持业力花 + 存在NeuronZero → 转化为NeuronOne
            if (heldFlower != null && zeroCount > 0)
            {
                var zero = mod.Zeros[mod.Zeros.Count - 1];
                mod.Zeros.RemoveAt(mod.Zeros.Count - 1);
                zero.Destroy();
                mod.PersistedZeroCount = mod.AliveZeroCount;

                self.ReleaseGrasp(heldItemGraspIndex);
                heldFlower.Destroy();
                var newOne = new NeuronOne(self);
                self.room.AddObject(newOne);
                mod.Ones.Add(newOne);
                mod.PersistedOneCount = mod.AliveOneCount;
                mod.ShieldCount = mod.AliveOneCount;
                mod.SaveToSave(self);
            }
            // 条件2：手持白色珍珠 + 存在NeuronZero → 转化为NeuronThreeOne
            else if (heldWhitePearl != null && zeroCount > 0)
            {
                var zero = mod.Zeros[mod.Zeros.Count - 1];
                mod.Zeros.RemoveAt(mod.Zeros.Count - 1);
                zero.Destroy();
                mod.PersistedZeroCount = mod.AliveZeroCount;

                // 先释放抓取，再销毁珍珠
                self.ReleaseGrasp(heldItemGraspIndex);
                heldWhitePearl.Destroy();

                var newThreeOne = new NeuronThreeOne(self);
                self.room.AddObject(newThreeOne);
                mod.ThreeOnes.Add(newThreeOne);
                mod.PersistedThreeOneCount = mod.AliveThreeOneCount;
                mod.SaveToSave(self);

                Plugin.Logger.LogInfo($"[UOT-VD] Converted white pearl to NeuronThreeOne! ThreeOneCount={mod.AliveThreeOneCount}");
            }
            // 条件2.5：手持彩色珍珠 + 存在NeuronZero → 转化为NeuronThreeTwo
            else if (heldColorPearl != null && zeroCount > 0)
            {
                var zero = mod.Zeros[mod.Zeros.Count - 1];
                mod.Zeros.RemoveAt(mod.Zeros.Count - 1);
                zero.Destroy();
                mod.PersistedZeroCount = mod.AliveZeroCount;

                // 先释放抓取，再销毁珍珠
                self.ReleaseGrasp(heldItemGraspIndex);
                heldColorPearl.Destroy();

                var newThreeTwo = new NeuronThreeTwo(self);
                self.room.AddObject(newThreeTwo);
                mod.ThreeTwos.Add(newThreeTwo);
                mod.PersistedThreeTwoCount = mod.AliveThreeTwoCount;
                mod.SaveToSave(self);

                Plugin.Logger.LogInfo($"[UOT-VD] Converted color pearl to NeuronThreeTwo! ThreeTwoCount={mod.AliveThreeTwoCount}");
            }
            // 条件3：手持石头 + 存在NeuronZero → 将石头变成奇点炸弹
            else if (heldRock && zeroCount > 0)
            {
                var zero = mod.Zeros[mod.Zeros.Count - 1];
                mod.Zeros.RemoveAt(mod.Zeros.Count - 1);
                zero.Destroy();
                mod.PersistedZeroCount = mod.AliveZeroCount;
                mod.SaveToSave(self);

                var rock = self.grasps[heldItemGraspIndex].grabbed as Rock;
                var rockPos = rock.firstChunk.pos;
                self.ReleaseGrasp(heldItemGraspIndex);
                rock.Destroy();

                ResolveTypes();
                if (_singularityBombType != null)
                {
                    var bombAbsObj = new AbstractPhysicalObject(self.room.world, AbstractPhysicalObject.AbstractObjectType.ScavengerBomb, null, self.room.GetWorldCoordinate(rockPos), self.room.game.GetNewID());
                    object bomb = null;
                    foreach (var ctor in _singularityBombType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        var parms = ctor.GetParameters();
                        try
                        {
                            object[] args = new object[parms.Length];
                            for (int pi = 0; pi < parms.Length; pi++)
                            {
                                var pt = parms[pi].ParameterType;
                                if (typeof(AbstractPhysicalObject).IsAssignableFrom(pt))
                                    args[pi] = bombAbsObj;
                                else if (pt == typeof(World))
                                    args[pi] = self.room.world;
                                else
                                    args[pi] = null;
                            }
                            bomb = ctor.Invoke(args);
                            break;
                        }
                        catch (System.Exception) { }
                    }
                    if (bomb != null)
                    {
                        self.room.AddObject(bomb as PhysicalObject);
                        self.SlugcatGrab(bomb as PhysicalObject, heldItemGraspIndex);
                    }
                }
            }
            // 条件4：手持任意其他物品 + 存在NeuronZero → 转化为NeuronTwo
            else if (heldOther != null && zeroCount > 0)
            {
                var zero = mod.Zeros[mod.Zeros.Count - 1];
                mod.Zeros.RemoveAt(mod.Zeros.Count - 1);
                zero.Destroy();
                mod.PersistedZeroCount = mod.AliveZeroCount;

                self.ReleaseGrasp(heldItemGraspIndex);
                heldOther.Destroy();
                var newTwo = new NeuronTwo(self);
                self.room.AddObject(newTwo);
                mod.Twos.Add(newTwo);
                mod.PersistedTwoCount = mod.AliveTwoCount;
                mod.SaveToSave(self);
            }
            // 条件5：饱食度>=1 → 生成NeuronZero
            else if (heldFlower == null && heldWhitePearl == null && !heldRock && heldOther == null && self.FoodInStomach >= 1)
            {
                self.SubtractFood(1);
                var newZero = new NeuronZero(self);
                self.room.AddObject(newZero);
                mod.Zeros.Add(newZero);
                mod.PersistedZeroCount = mod.AliveZeroCount;
                mod.SaveToSave(self);
            }
        }

        // ---- Ctrl长按秒杀（需要NeuronTwo） ----
        private static void HandleCtrlHoldAction(Player self, NeuronModule mod)
        {
            if (self.room == null) return;
            if (!mod.HasNeuronTwo) return;

            Plugin.Logger.LogInfo($"[UOT-VD] Ctrl hold triggered! Killing all creatures in room. NeuronTwo count before={mod.AliveTwoCount}");

            // 消耗一颗NeuronTwo
            var two = mod.Twos[mod.Twos.Count - 1];
            mod.Twos.RemoveAt(mod.Twos.Count - 1);
            two.Destroy();
            mod.PersistedTwoCount = mod.AliveTwoCount;
            mod.SaveToSave(self);

            // 遍历房间内所有生物并杀死
            var roomObjects = self.room.updateList;
            if (roomObjects != null)
            {
                int killCount = 0;
                for (int i = roomObjects.Count - 1; i >= 0; i--)
                {
                    var obj = roomObjects[i];
                    // 跳过玩家自身、神经元、已销毁物体
                    if (obj == self) continue;
                    if (obj == null || obj.slatedForDeletetion) continue;
                    if (obj is NeuronHalo || obj is NeuronZero || obj is NeuronOne || obj is NeuronTwo || obj is NeuronThreeOne || obj is NeuronThreeTwo) continue;

                    // 只杀生物（Creature及其子类）
                    if (obj is Creature creature)
                    {
                        // 检查距离（全房间范围，但排除太远的？不，用户要求"整个房间"）
                        creature.Die();
                        killCount++;
                    }
                    // 也杀PhysicalObject中有生命的东西（如VultureGrub等非Creature但可死的）
                    else if (obj is PhysicalObject po && po is not Weapon && po is not Spear)
                    {
                        // 大部分生物都是Creature子类，这里处理边缘情况
                        // 用反射检查是否有Die方法
                        var dieMethod = obj.GetType().GetMethod("Die", System.Type.EmptyTypes);
                        if (dieMethod != null)
                        {
                            try { dieMethod.Invoke(obj, null); killCount++; }
                            catch { }
                        }
                    }
                }

                // 视觉效果
                self.room.AddObject(new Explosion.ExplosionLight(self.firstChunk.pos, 300f, 0.8f, 5, new Color(1f, 0.2f, 0.2f)));
                self.room.AddObject(new ShockWave(self.firstChunk.pos, 500f, 0.06f, 3, false));
                self.room.ScreenMovement(self.firstChunk.pos, default, 1.5f);
                self.room.PlaySound(SoundID.Bomb_Explode, self.firstChunk.pos, 1.2f, 0.2f);
                self.room.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, self.firstChunk.pos, 1.0f, 0.5f);

                Plugin.Logger.LogInfo($"[UOT-VD] Ctrl hold kill complete! Killed {killCount} creatures. NeuronTwo count after={mod.AliveTwoCount}");
            }
        }

        // ---- Player.Die（模仿pearlcat Player_Hooks.Player_Die） ----
        private static void Player_Die(On.Player.orig_Die orig, Player self)
        {
            // ShieldActive 阻止死亡（护盾运行中或有可用护盾次数）
            if (NeuronModule.TryGet(self, out var mod) && mod.ShieldActive)
            {
                Plugin.Logger.LogInfo($"[UOT-VD] Player.Die blocked by ShieldActive! ShieldTimer={mod.ShieldTimer}, ShieldCount={mod.ShieldCount}");

                // 如果护盾计时器未运行，触发护盾视觉效果
                if (mod.ShieldTimer <= 0)
                {
                    if (!mod.ActivateVisualShield(self))
                    {
                        // 激活失败（无资源），允许死亡
                        orig(self);
                        return;
                    }
                }

                self.Stun(10);
                return;
            }

            bool wasDead = self.dead;
            orig(self);

            if (!wasDead && self.dead)
            {
                if (NeuronModule.TryGet(self, out var deathMod))
                {
                    deathMod.PersistedZeroCount = 0;
                    deathMod.PersistedOneCount = 0;
                    deathMod.PersistedTwoCount = 0;
                    deathMod.PersistedThreeOneCount = 0;
                    deathMod.PersistedThreeTwoCount = 0;
                    deathMod.ShieldTimer = 0;
                    deathMod.ShieldCount = 0;
                    deathMod.ShieldRechargeTimer = 0;
                    deathMod.TimeStopTimer = 0;
                    deathMod.TimeStopCooldownTimer = 0;

                    // 销毁特效
                    if (deathMod.ActiveTimeStopEffect != null && !deathMod.ActiveTimeStopEffect.slatedForDeletetion)
                    {
                        deathMod.ActiveTimeStopEffect.Destroy();
                        deathMod.ActiveTimeStopEffect = null;
                    }
                    if (deathMod.ActiveTimeReverseEffect != null && !deathMod.ActiveTimeReverseEffect.slatedForDeletetion)
                    {
                        deathMod.ActiveTimeReverseEffect.Destroy();
                        deathMod.ActiveTimeReverseEffect = null;
                    }

                    deathMod.ClearSave(self);
                }
                NeuronModule.GlobalTimeStopActive = false;
                NeuronModule.GlobalTimeStopPlayer = null;
                NeuronModule.GlobalTimeReverseActive = false;
                NeuronModule.GlobalTimeReversePlayer = null;
                StopTimeReverse();
            }

            if (!wasDead && self.dead
                && Plugin.ExplodeOnDeath.TryGet(self, out bool explode) && explode)
            {
                var room = self.room;
                var pos = self.mainBodyChunk.pos;
                var color = self.ShortCutColor();
                room.AddObject(new Explosion(room, self, pos, 7, 250f, 6.2f, 2f, 280f, 0.25f, self, 0.7f, 160f, 1f));
                room.AddObject(new Explosion.ExplosionLight(pos, 280f, 1f, 7, color));
                room.AddObject(new Explosion.ExplosionLight(pos, 230f, 1f, 3, new Color(1f, 1f, 1f)));
                room.AddObject(new ExplosionSpikes(room, pos, 14, 30f, 9f, 7f, 170f, color));
                room.AddObject(new ShockWave(pos, 330f, 0.045f, 5, false));
                room.ScreenMovement(pos, default, 1.3f);
                room.PlaySound(SoundID.Bomb_Explode, pos);
                room.InGameNoise(new Noise.InGameNoise(pos, 9000f, self, 1f));
            }
        }

        // ---- Player.GraphicsModuleUpdated ----
        private static void Player_GraphicsModuleUpdated(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
        {
            orig(self, actuallyViewed, eu);

            if (!NeuronModule.TryGet(self, out var mod)) return;

            if (mod == null) return;

            if (mod.Halo != null && !mod.Halo.slatedForDeletetion)
                mod.Halo.ForceUpdate();

            foreach (var zero in mod.Zeros)
            {
                if (zero != null && !zero.slatedForDeletetion)
                    zero.ForceUpdate();
            }

            foreach (var one in mod.Ones)
            {
                if (one != null && !one.slatedForDeletetion)
                    one.ForceUpdate();
            }

            foreach (var two in mod.Twos)
            {
                if (two != null && !two.slatedForDeletetion)
                    two.ForceUpdate();
            }

            foreach (var threeOne in mod.ThreeOnes)
            {
                if (threeOne != null && !threeOne.slatedForDeletetion)
                    threeOne.ForceUpdate();
            }

            foreach (var threeTwo in mod.ThreeTwos)
            {
                if (threeTwo != null && !threeTwo.slatedForDeletetion)
                    threeTwo.ForceUpdate();
            }
        }

        // ---- 位置历史记录（用于时间倒流） ----
        // 历史帧快照：位置数组 + 生物存活状态
        private struct HistoryFrame
        {
            public Vector2[] Positions;
            public bool WasAlive;       // 该帧此物体是否存活（对Creature：!dead；对物品：!slatedForDeletetion）
        }

        // key: PhysicalObject, value: 最近N帧的历史
        private static readonly System.Collections.Generic.Dictionary<PhysicalObject, System.Collections.Generic.List<HistoryFrame>> _positionHistory = new();
        /// <summary>当前倒流回放游标（指向历史中的当前帧索引，-1表示未在倒流）</summary>
        private static int _reverseHistoryCursor = -1;
        private const int MAX_HISTORY = NeuronModule.MAX_POSITION_HISTORY;
        private const int REVERSE_SPEED = NeuronModule.REVERSE_SPEED;

        /// <summary>
        /// 记录房间中所有PhysicalObject的当前位置和存活状态到历史缓冲区（仅在非倒流时调用）
        /// </summary>
        private static void RecordPositions(Room room)
        {
            if (room?.physicalObjects == null) return;

            // 追踪当前帧有哪些物体存活（用于标记不在列表中的物体为已死）
            var aliveThisFrame = new System.Collections.Generic.HashSet<PhysicalObject>();

            int recordedCount = 0;
            foreach (var list in room.physicalObjects)
            {
                if (list == null) continue;
                foreach (var obj in list)
                {
                    if (obj == null || obj.slatedForDeletetion) continue;

                    aliveThisFrame.Add(obj);

                    if (!_positionHistory.TryGetValue(obj, out var history))
                    {
                        history = new System.Collections.Generic.List<HistoryFrame>();
                        _positionHistory[obj] = history;
                    }

                    var positions = new Vector2[obj.bodyChunks.Length];
                    for (int i = 0; i < obj.bodyChunks.Length; i++)
                    {
                        positions[i] = obj.bodyChunks[i].pos;
                    }

                    bool wasAlive = !obj.slatedForDeletetion;
                    if (obj is Creature creature)
                        wasAlive = !creature.dead;

                    history.Add(new HistoryFrame { Positions = positions, WasAlive = wasAlive });

                    // 限制历史长度
                    while (history.Count > MAX_HISTORY)
                        history.RemoveAt(0);

                    recordedCount++;
                }
            }

            // 对历史中存在但当前不在房间中的物体，记录一帧"已死亡"状态
            foreach (var kvp in _positionHistory)
            {
                if (!aliveThisFrame.Contains(kvp.Key) && kvp.Value.Count > 0)
                {
                    // 物体已不在房间中（被销毁），记录死亡帧
                    var lastFrame = kvp.Value[kvp.Value.Count - 1];
                    kvp.Value.Add(new HistoryFrame { Positions = lastFrame.Positions, WasAlive = false });
                    while (kvp.Value.Count > MAX_HISTORY)
                        kvp.Value.RemoveAt(0);
                }
            }

            // 每300帧输出一次诊断
            if (Time.frameCount % 300 == 0 && recordedCount > 0)
            {
                Plugin.Logger.LogInfo($"[UOT-VD] RecordPositions: recorded {recordedCount} objects, history keys={_positionHistory.Count}");
            }

            // 清理长期死亡且历史全部为死的物体（减少内存占用）
            var toRemove = new System.Collections.Generic.List<PhysicalObject>();
            foreach (var kvp in _positionHistory)
            {
                // 如果物体已销毁且所有历史帧都是死亡状态，可以安全清理
                if ((kvp.Key == null || kvp.Key.slatedForDeletetion) && kvp.Value.Count > 0)
                {
                    bool allDead = true;
                    for (int i = kvp.Value.Count - 1; i >= 0 && i >= kvp.Value.Count - 60; i--)
                    {
                        if (kvp.Value[i].WasAlive)
                        {
                            allDead = false;
                            break;
                        }
                    }
                    if (allDead && kvp.Value.Count >= MAX_HISTORY)
                        toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
                _positionHistory.Remove(key);
        }

        /// <summary>
        /// 从位置历史中回放：恢复物体位置、复活生物
        /// </summary>
        private static void ApplyTimeReverse(Room room)
        {
            if (room?.physicalObjects == null) return;

            int appliedCount = 0;
            int revivedCount = 0;

            // 遍历当前房间中所有物体，应用历史位置
            foreach (var list in room.physicalObjects)
            {
                if (list == null) continue;
                foreach (var obj in list)
                {
                    if (obj == null || obj.slatedForDeletetion) continue;

                    if (_positionHistory.TryGetValue(obj, out var history) && history.Count > 0)
                    {
                        // 从历史中取出_reverseHistoryCursor帧
                        int idx = _reverseHistoryCursor;
                        if (idx < 0) idx = 0;
                        if (idx >= history.Count) idx = history.Count - 1;

                        var frame = history[idx];

                        // 恢复位置
                        for (int i = 0; i < frame.Positions.Length && i < obj.bodyChunks.Length; i++)
                        {
                            obj.bodyChunks[i].pos = frame.Positions[i];
                            obj.bodyChunks[i].vel = Vector2.zero;
                        }

                        // 如果历史中该帧生物是活的，但现在是死的 → 复活
                        if (frame.WasAlive && obj is Creature creature && creature.dead)
                        {
                            // 通过反射调用 Creature.Revive() 或直接重置 dead 状态
                            creature.dead = false;
                            // 重置生命值相关状态
                            if (creature.State != null)
                                creature.State.alive = true;
                            revivedCount++;
                            Plugin.Logger.LogInfo($"[UOT-VD] Time reverse revived: {obj.GetType().Name}");
                        }

                        appliedCount++;
                    }
                }
            }

            // 每60帧输出诊断
            if (Time.frameCount % 60 == 0)
            {
                Plugin.Logger.LogInfo($"[UOT-VD] ApplyTimeReverse: cursor={_reverseHistoryCursor}, applied={appliedCount}, revived={revivedCount}, historyKeys={_positionHistory.Count}");
            }
        }

        /// <summary>
        /// 初始化倒流游标（从历史末尾开始回放）
        /// </summary>
        private static void StartTimeReverse()
        {
            // 找到所有物体历史的最小长度
            int minHistoryLen = MAX_HISTORY;
            int totalKeys = 0;
            foreach (var history in _positionHistory.Values)
            {
                totalKeys++;
                if (history.Count > 0 && history.Count < minHistoryLen)
                    minHistoryLen = history.Count;
            }

            Plugin.Logger.LogInfo($"[UOT-VD] StartTimeReverse: totalKeys={totalKeys}, minHistoryLen={minHistoryLen}, MAX_HISTORY={MAX_HISTORY}");

            if (totalKeys == 0 || minHistoryLen <= 0)
            {
                // 没有历史数据，从0开始
                _reverseHistoryCursor = 0;
                Plugin.Logger.LogWarning("[UOT-VD] StartTimeReverse: No position history available!");
                return;
            }

            // 从历史约80%位置开始（即回退约20%的时间），这样按A键瞬间就有明显变化
            _reverseHistoryCursor = (int)(minHistoryLen * 0.8f);
            if (_reverseHistoryCursor < 0) _reverseHistoryCursor = 0;
            if (_reverseHistoryCursor >= minHistoryLen) _reverseHistoryCursor = minHistoryLen - 1;

            Plugin.Logger.LogInfo($"[UOT-VD] StartTimeReverse: cursor initialized to {_reverseHistoryCursor}/{minHistoryLen}");
        }

        /// <summary>
        /// 停止时间倒流，重置游标（不清空历史，以便下次使用）
        /// </summary>
        private static void StopTimeReverse()
        {
            Plugin.Logger.LogInfo($"[UOT-VD] StopTimeReverse: resetting cursor (was {_reverseHistoryCursor}), history kept ({_positionHistory.Count} keys)");
            _reverseHistoryCursor = -1;
            // 不清空历史！历史缓冲区持续维护，下次倒流时可立即使用
        }

        // ---- Room.Update hook：时间停止/时间倒流期间冻结/回放所有物体 ----
        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            // 时间倒流优先（长按A键）
            if (NeuronModule.GlobalTimeReverseActive && NeuronModule.GlobalTimeReversePlayer != null)
            {
                // 时间倒流模式：先调用orig让非物理逻辑运行，然后用历史覆盖所有物体位置
                orig(self);

                // 用历史位置覆盖所有物体（含玩家自己）
                ApplyTimeReverse(self);

                // 游标回退
                _reverseHistoryCursor -= REVERSE_SPEED;

                // 更新倒流特效的进度
                var reversePlayer = NeuronModule.GlobalTimeReversePlayer;
                if (reversePlayer != null && NeuronModule.TryGet(reversePlayer, out var rmod))
                {
                    if (rmod.ActiveTimeReverseEffect != null && !rmod.ActiveTimeReverseEffect.slatedForDeletetion)
                    {
                        // 找到所有物体历史的最小长度来计算进度
                        int maxLen = 0;
                        foreach (var h in _positionHistory.Values)
                        {
                            if (h.Count > maxLen) maxLen = h.Count;
                        }
                        float progress = maxLen > 0 ? 1f - (float)_reverseHistoryCursor / maxLen : 0f;
                        rmod.ActiveTimeReverseEffect.SetReverseProgress(progress);
                    }
                }

                // 游标到0时自动停止倒流（历史回放完毕）
                if (_reverseHistoryCursor <= 0)
                {
                    _reverseHistoryCursor = 0;
                    Plugin.Logger.LogInfo("[UOT-VD] Time reverse: reached beginning of history, auto-stopping.");

                    // 销毁倒流特效
                    if (reversePlayer != null && NeuronModule.TryGet(reversePlayer, out var rmod2))
                    {
                        if (rmod2.ActiveTimeReverseEffect != null && !rmod2.ActiveTimeReverseEffect.slatedForDeletetion)
                        {
                            rmod2.ActiveTimeReverseEffect.Destroy();
                            rmod2.ActiveTimeReverseEffect = null;
                        }
                    }
                    NeuronModule.GlobalTimeReverseActive = false;
                    NeuronModule.GlobalTimeReversePlayer = null;
                    StopTimeReverse();

                    if (reversePlayer != null && NeuronModule.TryGet(reversePlayer, out var rmod3) && rmod3.HasNeuronThreeTwo)
                    {
                        var threeTwo = rmod3.ThreeTwos[rmod3.ThreeTwos.Count - 1];
                        rmod3.ThreeTwos.RemoveAt(rmod3.ThreeTwos.Count - 1);
                        threeTwo.Destroy();
                        rmod3.PersistedThreeTwoCount = rmod3.AliveThreeTwoCount;
                        rmod3.TimeReverseCooldownTimer = NeuronModule.TIME_REVERSE_COOLDOWN;
                        rmod3.SaveToSave(reversePlayer);

                        reversePlayer.room?.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, reversePlayer.firstChunk.pos, 0.8f, 0.5f);
                        reversePlayer.room?.AddObject(new Explosion.ExplosionLight(reversePlayer.firstChunk.pos, 180f, 0.5f, 4, new Color(0.15f, 0.35f, 0.9f)));

                        Plugin.Logger.LogInfo($"[UOT-VD] Time reverse auto-ended! Consumed NeuronThreeTwo. Remaining ThreeTwos={rmod3.AliveThreeTwoCount}");
                    }
                }
            }
            else if (NeuronModule.GlobalTimeStopActive && NeuronModule.GlobalTimeStopPlayer != null)
            {
                var timeStopPlayer = NeuronModule.GlobalTimeStopPlayer;

                // 保存所有非玩家PhysicalObject的位置，并清零速度
                // 使用临时列表避免遍历时集合被修改
                var savedPositions = new System.Collections.Generic.List<(PhysicalObject obj, Vector2[] positions)>();

                if (self.physicalObjects != null)
                {
                    foreach (var list in self.physicalObjects)
                    {
                        if (list == null) continue;
                        foreach (var obj in list)
                        {
                            if (obj == timeStopPlayer) continue;

                            var posCopy = new Vector2[obj.bodyChunks.Length];
                            for (int i = 0; i < obj.bodyChunks.Length; i++)
                            {
                                posCopy[i] = obj.bodyChunks[i].pos;
                                obj.bodyChunks[i].vel = Vector2.zero;
                            }
                            savedPositions.Add((obj, posCopy));
                        }
                    }
                }

                // 调用原始Room.Update（物理引擎可能移动物体）
                orig(self);

                // 恢复所有非玩家物体的位置并清零速度
                foreach (var (obj, positions) in savedPositions)
                {
                    if (obj == null || obj.slatedForDeletetion) continue;
                    for (int i = 0; i < positions.Length && i < obj.bodyChunks.Length; i++)
                    {
                        obj.bodyChunks[i].pos = positions[i];
                        obj.bodyChunks[i].vel = Vector2.zero;
                    }
                }

            }
            else
            {
                // 正常模式：在orig之前记录位置历史（用于后续可能的时间倒流）
                if (_reverseHistoryCursor < 0)
                    RecordPositions(self);
                orig(self);
            }
        }
    }
}
