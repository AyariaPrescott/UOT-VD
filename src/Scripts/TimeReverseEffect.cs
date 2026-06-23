using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 时间倒流视觉特效 — 蓝色时计逆时针旋转 + 粒子回溯效果
    /// 倒流期间显示蓝色光环、逆时针指针、回溯粒子
    /// </summary>
    public class TimeReverseEffect : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private FSprite _ringSprite;        // 蓝色外环（逆时针旋转）
        private FSprite _glowSprite;        // 中心蓝光
        private FSprite _clockSprite;       // 逆时针指针
        private FSprite _vignetteSprite;    // 屏幕蓝调暗角
        private FSprite[] _particles;       // 回溯粒子
        private float _timer;
        private float _phase;
        private bool _destroyed;
        private float _reverseProgress;     // 倒流进度（0~1, 0=刚开始, 1=即将结束）

        // 粒子状态
        private struct ParticleState
        {
            public float Angle;
            public float Radius;
            public float Speed;
            public float Life;
        }
        private ParticleState[] _particleStates;
        private const int PARTICLE_COUNT = 8;

        public TimeReverseEffect(Player player)
        {
            _playerRef = new WeakReference(player);
            _pos = player.firstChunk.pos;
            _lastPos = _pos;
            _timer = 0f;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            _destroyed = false;
            _reverseProgress = 0f;

            // 初始化粒子状态
            _particleStates = new ParticleState[PARTICLE_COUNT];
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _particleStates[i] = new ParticleState
                {
                    Angle = UnityEngine.Random.value * Mathf.PI * 2f,
                    Radius = 30f + UnityEngine.Random.value * 50f,
                    Speed = 0.3f + UnityEngine.Random.value * 0.7f,
                    Life = UnityEngine.Random.value
                };
            }
        }

        public void SetReverseProgress(float progress)
        {
            _reverseProgress = Mathf.Clamp01(progress);
        }

        public override void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;

            if (_ringSprite != null) { _ringSprite.RemoveFromContainer(); _ringSprite = null; }
            if (_glowSprite != null) { _glowSprite.RemoveFromContainer(); _glowSprite = null; }
            if (_clockSprite != null) { _clockSprite.RemoveFromContainer(); _clockSprite = null; }
            if (_vignetteSprite != null) { _vignetteSprite.RemoveFromContainer(); _vignetteSprite = null; }
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
            _phase += 0.03f;

            // 更新粒子 — 向内螺旋
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _particleStates[i].Angle -= _particleStates[i].Speed * 0.04f; // 逆时针
                _particleStates[i].Radius -= 0.3f;
                if (_particleStates[i].Radius < 5f)
                {
                    _particleStates[i].Radius = 50f + UnityEngine.Random.value * 40f;
                    _particleStates[i].Angle = UnityEngine.Random.value * Mathf.PI * 2f;
                }
            }

            // 如果时间倒流结束，销毁特效
            if (!NeuronModule.GlobalTimeReverseActive)
            {
                Destroy();
            }
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[4 + PARTICLE_COUNT];

            // 屏幕暗角 — 蓝色调
            _vignetteSprite = new FSprite("Futile_White", true);
            _vignetteSprite.shader = rCam.game.rainWorld.Shaders["FlatLight"];
            _vignetteSprite.scaleX = 90f;
            _vignetteSprite.scaleY = 55f;
            _vignetteSprite.alpha = 0.1f;
            _vignetteSprite.color = new Color(0.05f, 0.1f, 0.3f);
            sLeaser.sprites[0] = _vignetteSprite;

            // 外环 — 蓝色力场，逆时针旋转
            _ringSprite = new FSprite("Futile_White", true);
            _ringSprite.shader = rCam.game.rainWorld.Shaders["GravityDisruptor"];
            _ringSprite.scale = 50f / 16f;
            _ringSprite.alpha = 0.55f;
            _ringSprite.color = new Color(0.2f, 0.4f, 1f);
            sLeaser.sprites[1] = _ringSprite;

            // 中心光晕
            _glowSprite = new FSprite("Futile_White", true);
            _glowSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _glowSprite.scale = 110f / 16f;
            _glowSprite.alpha = 0.25f;
            _glowSprite.color = new Color(0.15f, 0.35f, 0.95f);
            sLeaser.sprites[2] = _glowSprite;

            // 时钟指针 — 逆时针旋转
            _clockSprite = new FSprite("pixel", true);
            _clockSprite.scaleX = 2f;
            _clockSprite.scaleY = 35f;
            _clockSprite.alpha = 0.75f;
            _clockSprite.color = new Color(0.3f, 0.55f, 1f);
            sLeaser.sprites[3] = _clockSprite;

            // 回溯粒子
            _particles = new FSprite[PARTICLE_COUNT];
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                var p = new FSprite("pixel", true);
                p.scaleX = 3f;
                p.scaleY = 6f;
                p.alpha = 0.6f;
                p.color = new Color(0.3f, 0.5f, 1f);
                _particles[i] = p;
                sLeaser.sprites[4 + i] = p;
            }

            AddToContainer(sLeaser, rCam, null);
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer)
        {
            var container = newContainer ?? rCam.ReturnFContainer("HUD");

            // 暗角放在 Background 层
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

            // 暗角
            _vignetteSprite.SetPosition(screenCenter);
            _vignetteSprite.alpha = 0.06f + 0.04f * Mathf.Sin(_timer * 1.8f);
            _vignetteSprite.color = new Color(0.04f, 0.08f, 0.25f, 1f);

            // 外环 — 逆时针旋转（负角速度），带呼吸效果
            float ringRotation = -_phase * 60f; // 逆时针
            _ringSprite.SetPosition(drawPos);
            _ringSprite.rotation = ringRotation;
            _ringSprite.scale = (50f + Mathf.Sin(_phase * 0.6f) * 12f) / 16f;
            _ringSprite.alpha = 0.4f + 0.15f * Mathf.Sin(_phase * 1.5f);
            _ringSprite.color = Color.Lerp(
                new Color(0.2f, 0.4f, 1f),
                new Color(0.15f, 0.3f, 0.7f),
                0.3f + 0.3f * Mathf.Sin(_phase * 0.4f));

            // 中心光晕 — 蓝色脉冲
            _glowSprite.SetPosition(drawPos);
            _glowSprite.alpha = 0.18f + 0.07f * Mathf.Sin(_timer * 3.5f);
            _glowSprite.scale = (100f + Mathf.Sin(_timer * 2.5f) * 25f) / 16f;
            _glowSprite.color = new Color(
                0.15f + 0.05f * Mathf.Sin(_timer * 2f),
                0.35f + 0.1f * Mathf.Sin(_timer * 1.7f),
                0.95f + 0.05f * Mathf.Sin(_timer * 2.3f));

            // 时钟指针 — 逆时针快速旋转
            float clockAngle = -_timer * 150f; // 逆时针
            _clockSprite.SetPosition(drawPos);
            _clockSprite.rotation = clockAngle;
            _clockSprite.alpha = 0.55f + 0.2f * Mathf.Sin(_timer * 2.5f);
            _clockSprite.color = _reverseProgress > 0.8f
                ? Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(0.3f, 0.55f, 1f), Mathf.Sin(_timer * 10f) * 0.5f + 0.5f)
                : new Color(0.3f, 0.55f, 1f);

            // 回溯粒子 — 向内螺旋，模拟时间回溯
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                var state = _particleStates[i];
                float px = drawPos.x + Mathf.Cos(state.Angle) * state.Radius;
                float py = drawPos.y + Mathf.Sin(state.Angle) * state.Radius;
                _particles[i].SetPosition(px, py);
                _particles[i].rotation = state.Angle * Mathf.Rad2Deg + 90f;
                _particles[i].alpha = 0.3f + 0.3f * (state.Radius / 90f);
                _particles[i].scaleY = 3f + 4f * (state.Radius / 90f);
                _particles[i].color = new Color(0.2f, 0.45f, 1f, state.Radius / 90f);
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
