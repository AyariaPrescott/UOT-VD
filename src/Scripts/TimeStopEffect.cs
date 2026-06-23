using System;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 时间停止视觉特效 — 绿色时钟/波纹效果，覆盖全屏
    /// 持续期间显示一个脉动的绿色光环 + 屏幕边缘暗角
    /// </summary>
    public class TimeStopEffect : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private FSprite _ringSprite;      // 外圈波纹环
        private FSprite _glowSprite;      // 中心光晕
        private FSprite _vignetteSprite;  // 屏幕暗角
        private FSprite _clockSprite;     // 时钟指针
        private FSprite[] _particles;     // 悬浮粒子
        private float _timer;
        private float _phase;
        private bool _destroyed;
        private float _remainingRatio;    // 剩余时间比例

        // 粒子状态
        private struct ParticleState
        {
            public float Angle;
            public float Radius;
            public float Speed;
            public float Life;
        }
        private ParticleState[] _particleStates;
        private const int PARTICLE_COUNT = 16;

        public TimeStopEffect(Player player)
        {
            _playerRef = new WeakReference(player);
            _pos = player.firstChunk.pos;
            _lastPos = _pos;
            _timer = 0f;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            _destroyed = false;
            _remainingRatio = 1f;

            // 初始化粒子状态
            _particleStates = new ParticleState[PARTICLE_COUNT];
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _particleStates[i] = new ParticleState
                {
                    Angle = UnityEngine.Random.value * Mathf.PI * 2f,
                    Radius = 20f + UnityEngine.Random.value * 60f,
                    Speed = 0.2f + UnityEngine.Random.value * 0.6f,
                    Life = UnityEngine.Random.value
                };
            }
        }

        public void SetRemaining(float ratio)
        {
            _remainingRatio = Mathf.Clamp01(ratio);
        }

        public override void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;

            if (_ringSprite != null) { _ringSprite.RemoveFromContainer(); _ringSprite = null; }
            if (_glowSprite != null) { _glowSprite.RemoveFromContainer(); _glowSprite = null; }
            if (_vignetteSprite != null) { _vignetteSprite.RemoveFromContainer(); _vignetteSprite = null; }
            if (_clockSprite != null) { _clockSprite.RemoveFromContainer(); _clockSprite = null; }
            if (_particles != null)
            {
                foreach (var p in _particles)
                {
                    if (p != null) p.RemoveFromContainer();
                }
                _particles = null;
            }

            base.Destroy();
        }

        public override void Update(bool eu)
        {
            base.Update(eu);

            if (_destroyed) return;

            if (!(_playerRef.Target is Player player) || player.room != room)
            {
                Destroy();
                return;
            }

            _lastPos = _pos;
            _pos = player.firstChunk.pos;

            _timer += 0.02f;
            _phase += 0.025f;

            // 更新粒子 — 顺时针缓慢旋转（时间冻结感）
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _particleStates[i].Angle += _particleStates[i].Speed * 0.015f; // 顺时针
                _particleStates[i].Radius += Mathf.Sin(_timer * 1.5f + _particleStates[i].Life * Mathf.PI * 2f) * 0.15f;
                if (_particleStates[i].Radius > 80f)
                {
                    _particleStates[i].Radius = 20f + UnityEngine.Random.value * 30f;
                    _particleStates[i].Angle = UnityEngine.Random.value * Mathf.PI * 2f;
                }
                if (_particleStates[i].Radius < 10f)
                {
                    _particleStates[i].Radius = 40f + UnityEngine.Random.value * 40f;
                }
            }

            // 如果时间停止结束，销毁特效
            if (!NeuronModule.GlobalTimeStopActive)
            {
                Destroy();
            }
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[4 + PARTICLE_COUNT];

            // 屏幕暗角 — 覆盖全屏
            _vignetteSprite = new FSprite("Futile_White", true);
            _vignetteSprite.shader = rCam.game.rainWorld.Shaders["FlatLight"];
            _vignetteSprite.scaleX = 90f;
            _vignetteSprite.scaleY = 55f;
            _vignetteSprite.alpha = 0.12f;
            _vignetteSprite.color = new Color(0.05f, 0.2f, 0.1f);
            sLeaser.sprites[0] = _vignetteSprite;

            // 外圈波纹环 — 随时间扩散
            _ringSprite = new FSprite("Futile_White", true);
            _ringSprite.shader = rCam.game.rainWorld.Shaders["GravityDisruptor"];
            _ringSprite.scale = 40f / 16f;
            _ringSprite.alpha = 0.5f;
            _ringSprite.color = new Color(0.1f, 0.85f, 0.3f);
            sLeaser.sprites[1] = _ringSprite;

            // 中心光晕
            _glowSprite = new FSprite("Futile_White", true);
            _glowSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _glowSprite.scale = 100f / 16f;
            _glowSprite.alpha = 0.3f;
            _glowSprite.color = new Color(0.15f, 0.9f, 0.25f);
            sLeaser.sprites[2] = _glowSprite;

            // 时钟指针 — 旋转指示剩余时间
            _clockSprite = new FSprite("pixel", true);
            _clockSprite.scaleX = 2f;
            _clockSprite.scaleY = 40f;
            _clockSprite.alpha = 0.7f;
            _clockSprite.color = new Color(0.2f, 1f, 0.3f);
            sLeaser.sprites[3] = _clockSprite;

            // 悬浮粒子
            _particles = new FSprite[PARTICLE_COUNT];
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                var p = new FSprite("pixel", true);
                p.scaleX = 2.5f;
                p.scaleY = 2.5f;
                p.alpha = 0.5f;
                p.color = new Color(0.2f, 0.9f, 0.3f);
                _particles[i] = p;
                sLeaser.sprites[4 + i] = p;
            }

            AddToContainer(sLeaser, rCam, null);
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer)
        {
            var container = newContainer ?? rCam.ReturnFContainer("HUD");

            // 暗角放在 Background 层（最底层）
            sLeaser.sprites[0].RemoveFromContainer();
            rCam.ReturnFContainer("Background").AddChild(sLeaser.sprites[0]);

            for (int i = 1; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                container.AddChild(sLeaser.sprites[i]);
            }
        }

        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (_destroyed || sLeaser.deleteMeNextFrame) return;

            Vector2 drawPos = Vector2.Lerp(_lastPos, _pos, timeStacker) - camPos;
            Vector2 screenCenter = rCam.pos - camPos;

            // 暗角跟随屏幕中心
            _vignetteSprite.SetPosition(screenCenter);
            _vignetteSprite.alpha = 0.08f + 0.04f * Mathf.Sin(_timer * 1.5f);
            _vignetteSprite.color = new Color(0.03f, 0.15f, 0.08f, 1f);

            // 外圈波纹 — 扩散+收缩
            float ringPulse = 1f + 0.3f * Mathf.Sin(_phase * 2f);
            _ringSprite.SetPosition(drawPos);
            _ringSprite.scale = (45f + Mathf.Sin(_phase * 0.7f) * 15f) / 16f * ringPulse;
            _ringSprite.alpha = 0.35f + 0.15f * Mathf.Sin(_phase * 1.3f);
            _ringSprite.color = Color.Lerp(new Color(0.1f, 0.85f, 0.3f), new Color(0.05f, 0.5f, 0.2f), 0.3f + 0.3f * Mathf.Sin(_phase * 0.5f));

            // 中心光晕
            _glowSprite.SetPosition(drawPos);
            _glowSprite.alpha = 0.2f + 0.1f * Mathf.Sin(_timer * 3f);
            _glowSprite.scale = (90f + Mathf.Sin(_timer * 2.1f) * 20f) / 16f;
            _glowSprite.color = new Color(0.15f + 0.05f * Mathf.Sin(_timer * 1.5f), 0.9f, 0.25f + 0.1f * Mathf.Sin(_timer * 2f));

            // 时钟指针 — 旋转速度随剩余时间变化
            _clockSprite.SetPosition(drawPos);
            float clockAngle = _timer * 120f * (0.5f + _remainingRatio * 0.5f);
            _clockSprite.rotation = clockAngle;
            _clockSprite.alpha = 0.5f + 0.2f * Mathf.Sin(_timer * 2f);
            _clockSprite.color = _remainingRatio < 0.3f
                ? Color.Lerp(new Color(1f, 0.3f, 0.1f), new Color(0.2f, 1f, 0.3f), Mathf.Sin(_timer * 8f) * 0.5f + 0.5f)
                : new Color(0.2f, 1f, 0.3f);

            // 悬浮粒子 — 缓慢顺时针旋转，时间冻结感
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                var state = _particleStates[i];
                float px = drawPos.x + Mathf.Cos(state.Angle) * state.Radius;
                float py = drawPos.y + Mathf.Sin(state.Angle) * state.Radius;
                _particles[i].SetPosition(px, py);
                _particles[i].rotation = state.Angle * Mathf.Rad2Deg;
                _particles[i].alpha = 0.25f + 0.25f * (1f - state.Radius / 80f);
                _particles[i].scaleX = 1.5f + 2f * (1f - state.Radius / 80f);
                _particles[i].scaleY = 1.5f + 2f * (1f - state.Radius / 80f);
                _particles[i].color = new Color(0.15f, 0.85f, 0.25f, 0.4f + 0.3f * (1f - state.Radius / 80f));
            }

            // 确保 sprites 在正确容器中
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if (sLeaser.sprites[i] != null && sLeaser.sprites[i].container == null)
                {
                    if (i == 0)
                        rCam.ReturnFContainer("Background").AddChild(sLeaser.sprites[i]);
                    else
                        rCam.ReturnFContainer("HUD").AddChild(sLeaser.sprites[i]);
                }
            }
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }
    }
}
