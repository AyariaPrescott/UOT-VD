using System;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 蓝色神经元小球，绕彩色NeuronHalo旋转。样式与NeuronThreeOne相似。
    /// 通过彩色珍珠（非白色珍珠）转换获得。
    /// </summary>
    public class NeuronThreeTwo : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private float _orbitAngle;
        private float _orbitSpeed;
        private float _orbitRadius;
        private FSprite _sprite;

        public NeuronThreeTwo(Player player)
        {
            _playerRef = new WeakReference(player);
            _pos = player.mainBodyChunk.pos;
            _lastPos = _pos;
            _orbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _orbitSpeed = 0.04f + UnityEngine.Random.Range(0f, 0.03f);
            _orbitRadius = 30f + UnityEngine.Random.Range(0f, 6f);
        }

        public void ForceUpdate()
        {
            if (_playerRef.Target is Player player)
            {
                _lastPos = _pos;
                _pos = player.mainBodyChunk.pos;
            }
        }

        public override void Update(bool eu)
        {
            try
            {
                base.Update(eu);

                if (!(_playerRef.Target is Player player))
                    return;

                _lastPos = _pos;
                _pos = player.mainBodyChunk.pos;

                if (player.dead)
                {
                    Destroy();
                    return;
                }

                if (room != null && player.room != null && player.room != room)
                {
                    room.RemoveObject(this);
                    player.room.AddObject(this);
                }

                _orbitAngle += _orbitSpeed;
                if (_orbitAngle > Mathf.PI * 2f) _orbitAngle -= Mathf.PI * 2f;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"[UOT-VD] NeuronThreeTwo.Update exception: {ex}");
            }
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];

            _sprite = new FSprite("Futile_White", true);
            _sprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _sprite.scale = 14f / 16f;
            _sprite.alpha = 1f;
            _sprite.color = new Color(0.15f, 0.35f, 0.9f);  // 蓝色
            sLeaser.sprites[0] = _sprite;

            AddToContainer(sLeaser, rCam, null);
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (newContatiner == null)
                newContatiner = rCam.ReturnFContainer("HUD2");

            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                newContatiner.AddChild(sLeaser.sprites[i]);
                sLeaser.sprites[i].MoveToFront();
            }
        }

        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            Vector2 drawPos = Vector2.Lerp(_lastPos, _pos, timeStacker);
            float angle = _orbitAngle + _orbitSpeed * timeStacker;

            Vector2 haloCenter = new Vector2(drawPos.x, drawPos.y + 45f);

            float px = Mathf.Cos(angle) * _orbitRadius;
            float py = Mathf.Sin(angle) * _orbitRadius * 0.75f;

            Vector2 orbitPos = haloCenter + new Vector2(px, py) - camPos;

            _sprite.SetPosition(orbitPos);
            _sprite.color = new Color(0.15f, 0.35f, 0.9f);  // 蓝色
            _sprite.scale = (13f + Mathf.Sin(angle * 2.3f) * 3f) / 16f;
            _sprite.alpha = 0.92f + Mathf.Sin(angle * 1.7f) * 0.08f;

            if (sLeaser.deleteMeNextFrame) return;

            if (_sprite.container == null)
                rCam.ReturnFContainer("HUD2").AddChild(_sprite);
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }
    }
}
