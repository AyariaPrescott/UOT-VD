using System;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 护盾视觉特效 — 金色球体护罩（模仿pearlcat的GravityDisruptor shader效果）
    /// 绑定到玩家位置，护盾激活时显示，持续时间由ShieldTimer控制。
    /// </summary>
    public class ShieldEffect : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private readonly NeuronModule _module;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private FSprite _shieldSprite;
        private FSprite _glowSprite;
        private float _flickerTimer;
        private float _flickerPhase;
        private bool _destroyed;

        public ShieldEffect(Player player, NeuronModule module)
        {
            _playerRef = new WeakReference(player);
            _module = module;
            _pos = player.firstChunk.pos;
            _lastPos = _pos;
            _flickerTimer = 0f;
            _flickerPhase = UnityEngine.Random.value * Mathf.PI * 2f;
            _destroyed = false;
        }

        public override void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;

            // 清理sprites防止残留在场景中
            if (_shieldSprite != null)
            {
                _shieldSprite.RemoveFromContainer();
                _shieldSprite = null;
            }
            if (_glowSprite != null)
            {
                _glowSprite.RemoveFromContainer();
                _glowSprite = null;
            }

            // 清除模块引用
            if (_module != null && _module.ActiveShieldEffect == this)
            {
                _module.ActiveShieldEffect = null;
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

            // 护盾过期后销毁
            if (!_module.ShieldActive)
            {
                Destroy();
                return;
            }

            _flickerTimer += 0.02f;
            _flickerPhase += 0.03f;
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[2];

            // 外层光晕 — 大尺寸半透明金色（扩大一倍）
            _glowSprite = new FSprite("Futile_White", true);
            _glowSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _glowSprite.scale = 120f / 16f;
            _glowSprite.alpha = 0.25f;
            _glowSprite.color = new Color(1f, 0.85f, 0.2f);
            sLeaser.sprites[0] = _glowSprite;

            // 内层护罩 — 使用GravityDisruptor shader模拟力场球体（扩大一倍）
            _shieldSprite = new FSprite("Futile_White", true);
            _shieldSprite.shader = rCam.game.rainWorld.Shaders["GravityDisruptor"];
            _shieldSprite.scale = 80f / 16f;
            _shieldSprite.alpha = 0.7f;
            _shieldSprite.color = new Color(1f, 0.9f, 0.3f);
            sLeaser.sprites[1] = _shieldSprite;

            AddToContainer(sLeaser, rCam, null);
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            // 放在HUD层，确保在玩家上方渲染
            var container = newContatiner ?? rCam.ReturnFContainer("HUD");

            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                container.AddChild(sLeaser.sprites[i]);
            }
        }

        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (_destroyed || sLeaser.deleteMeNextFrame) return;

            Vector2 drawPos = Vector2.Lerp(_lastPos, _pos, timeStacker) - camPos;

            // 护盾剩余时间比例
            float lifeRatio = _module.ShieldTimer / (float)NeuronModule.SHIELD_DURATION;

            // 临近消失时闪烁
            float flickerAlpha = 1f;
            if (lifeRatio < 0.3f)
            {
                flickerAlpha = 0.5f + 0.5f * Mathf.Sin(_flickerPhase * 6f);
            }

            // 外层光晕（扩大一倍）
            _glowSprite.SetPosition(drawPos);
            _glowSprite.alpha = 0.2f + 0.05f * Mathf.Sin(_flickerTimer * 3f);
            _glowSprite.scale = (116f + Mathf.Sin(_flickerTimer * 2.3f) * 12f) / 16f;
            _glowSprite.color = new Color(1f, 0.85f + 0.1f * Mathf.Sin(_flickerTimer * 2f), 0.2f + 0.1f * Mathf.Sin(_flickerTimer * 1.7f));

            // 内层护罩（扩大一倍）
            _shieldSprite.SetPosition(drawPos);
            _shieldSprite.alpha = 0.6f * flickerAlpha + 0.05f * Mathf.Sin(_flickerTimer * 4f);
            _shieldSprite.scale = (76f + Mathf.Sin(_flickerTimer * 2.7f) * 10f) / 16f;
            _shieldSprite.color = new Color(1f, 0.9f + 0.08f * Mathf.Sin(_flickerTimer * 2f), 0.3f + 0.15f * Mathf.Sin(_flickerTimer * 1.5f));

            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if (sLeaser.sprites[i].container == null)
                    rCam.ReturnFContainer("HUD").AddChild(sLeaser.sprites[i]);
            }
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }
    }
}
