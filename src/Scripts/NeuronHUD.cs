using System.Collections.Generic;
using HUD;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 在饱食度UI上方显示神经元数量指示器
    /// 灰色圆点 = NeuronZero，深色圆点 = NeuronOne，白色圆点 = NeuronTwo，绿色圆点 = NeuronThreeOne
    /// 模仿pearlcat的InventoryHUD模式
    /// </summary>
    public class NeuronHUD : HudPart
    {
        private readonly List<FSprite> _zeroDots = new();
        private readonly List<FSprite> _oneDots = new();
        private readonly List<FSprite> _twoDots = new();
        private readonly List<FSprite> _threeOneDots = new();
        private readonly List<FSprite> _threeTwoDots = new();
        private int _lastZeroCount = -1;
        private int _lastOneCount = -1;
        private int _lastTwoCount = -1;
        private int _lastThreeOneCount = -1;
        private int _lastThreeTwoCount = -1;

        // 布局常量
        private const float DOT_SCALE = 5.4f;   // 1.8 × 3 = 5.4
        private const float DOT_GAP = 16f;
        private const float HUD_OFFSET_X = 0f;   // 起始X偏移（从饱食度左边缘开始）
        private const float HUD_OFFSET_Y = 22f;

        public NeuronHUD(HUD.HUD hud) : base(hud)
        {
        }

        public override void Draw(float timeStacker)
        {
            if (hud.rainWorld.processManager.currentMainLoop is not RainWorldGame game)
                return;

            var player = game.Players.Count > 0 ? game.Players[0]?.realizedCreature as Player : null;
            if (player == null || player.room == null)
                return;

            if (!NeuronModule.TryGet(player, out var mod))
                return;

            int zeroCount = mod.AliveZeroCount;
            int oneCount = mod.AliveOneCount;
            int twoCount = mod.AliveTwoCount;
            int threeOneCount = mod.AliveThreeOneCount;
            int threeTwoCount = mod.AliveThreeTwoCount;

            // 数量没变化就不重建sprites
            if (zeroCount == _lastZeroCount && oneCount == _lastOneCount && twoCount == _lastTwoCount && threeOneCount == _lastThreeOneCount && threeTwoCount == _lastThreeTwoCount)
            {
                UpdateDotPositions(player, timeStacker);
                return;
            }

            _lastZeroCount = zeroCount;
            _lastOneCount = oneCount;
            _lastTwoCount = twoCount;
            _lastThreeOneCount = threeOneCount;
            _lastThreeTwoCount = threeTwoCount;

            RebuildDots(zeroCount, oneCount, twoCount, threeOneCount, threeTwoCount);
            UpdateDotPositions(player, timeStacker);
        }

        private void RebuildDots(int zeroCount, int oneCount, int twoCount, int threeOneCount, int threeTwoCount)
        {
            // 清理旧圆点
            foreach (var dot in _zeroDots)
                dot.RemoveFromContainer();
            _zeroDots.Clear();

            foreach (var dot in _oneDots)
                dot.RemoveFromContainer();
            _oneDots.Clear();

            foreach (var dot in _twoDots)
                dot.RemoveFromContainer();
            _twoDots.Clear();

            foreach (var dot in _threeOneDots)
                dot.RemoveFromContainer();
            _threeOneDots.Clear();

            foreach (var dot in _threeTwoDots)
                dot.RemoveFromContainer();
            _threeTwoDots.Clear();

            var container = hud.fContainers[1]; // 和饱食度同层

            // 创建灰色圆点（NeuronZero）
            for (int i = 0; i < zeroCount; i++)
            {
                var dot = new FSprite("pixel", true)
                {
                    scale = DOT_SCALE,
                    color = new Color(0.4f, 0.4f, 0.4f),  // 灰色
                    alpha = 0.85f
                };
                container.AddChild(dot);
                _zeroDots.Add(dot);
            }

            // 创建深色圆点（NeuronOne）
            for (int i = 0; i < oneCount; i++)
            {
                var dot = new FSprite("pixel", true)
                {
                    scale = DOT_SCALE,
                    color = new Color(0.1f, 0.1f, 0.1f),  // 深色/黑色
                    alpha = 0.9f
                };
                container.AddChild(dot);
                _oneDots.Add(dot);
            }

            // 创建白色圆点（NeuronTwo）
            for (int i = 0; i < twoCount; i++)
            {
                var dot = new FSprite("pixel", true)
                {
                    scale = DOT_SCALE,
                    color = new Color(0.85f, 0.85f, 0.85f),  // 白色
                    alpha = 0.9f
                };
                container.AddChild(dot);
                _twoDots.Add(dot);
            }

            // 创建绿色圆点（NeuronThreeOne）
            for (int i = 0; i < threeOneCount; i++)
            {
                var dot = new FSprite("pixel", true)
                {
                    scale = DOT_SCALE,
                    color = new Color(0.1f, 0.8f, 0.2f),  // 绿色
                    alpha = 0.9f
                };
                container.AddChild(dot);
                _threeOneDots.Add(dot);
            }

            // 创建蓝色圆点（NeuronThreeTwo）
            for (int i = 0; i < threeTwoCount; i++)
            {
                var dot = new FSprite("pixel", true)
                {
                    scale = DOT_SCALE,
                    color = new Color(0.15f, 0.35f, 0.9f),  // 蓝色
                    alpha = 0.9f
                };
                container.AddChild(dot);
                _threeTwoDots.Add(dot);
            }
        }

        private void UpdateDotPositions(Player player, float timeStacker)
        {
            // 获取饱食度UI位置
            var foodMeter = hud.foodMeter;
            Vector2 foodPos = Vector2.Lerp(foodMeter.lastPos, foodMeter.pos, timeStacker);

            // 在饱食度上方，从foodPos位置开始向右排列
            Vector2 basePos = foodPos + new Vector2(HUD_OFFSET_X, HUD_OFFSET_Y);

            int totalDots = _zeroDots.Count + _oneDots.Count + _twoDots.Count + _threeOneDots.Count + _threeTwoDots.Count;
            if (totalDots == 0) return;

            // 从basePos开始向右依次排列
            int idx = 0;
            foreach (var dot in _zeroDots)
            {
                dot.SetPosition(new Vector2(basePos.x + idx * DOT_GAP, basePos.y));
                idx++;
            }
            foreach (var dot in _oneDots)
            {
                dot.SetPosition(new Vector2(basePos.x + idx * DOT_GAP, basePos.y));
                idx++;
            }
            foreach (var dot in _twoDots)
            {
                dot.SetPosition(new Vector2(basePos.x + idx * DOT_GAP, basePos.y));
                idx++;
            }
            foreach (var dot in _threeOneDots)
            {
                dot.SetPosition(new Vector2(basePos.x + idx * DOT_GAP, basePos.y));
                idx++;
            }
            foreach (var dot in _threeTwoDots)
            {
                dot.SetPosition(new Vector2(basePos.x + idx * DOT_GAP, basePos.y));
                idx++;
            }
        }

        public override void Update()
        {
            // 不需要每帧更新，Draw里处理
        }

        public override void ClearSprites()
        {
            foreach (var dot in _zeroDots)
                dot.RemoveFromContainer();
            _zeroDots.Clear();

            foreach (var dot in _oneDots)
                dot.RemoveFromContainer();
            _oneDots.Clear();

            foreach (var dot in _twoDots)
                dot.RemoveFromContainer();
            _twoDots.Clear();

            foreach (var dot in _threeOneDots)
                dot.RemoveFromContainer();
            _threeOneDots.Clear();

            foreach (var dot in _threeTwoDots)
                dot.RemoveFromContainer();
            _threeTwoDots.Clear();

            _lastZeroCount = -1;
            _lastOneCount = -1;
            _lastTwoCount = -1;
            _lastThreeOneCount = -1;
            _lastThreeTwoCount = -1;
        }
    }
}
