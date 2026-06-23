using System;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 白色神经元小球，绕彩色NeuronHalo旋转。形状与NeuronOne相同。
    /// 护盾消耗优先级：NeuronOne → NeuronTwo
    /// </summary>
    public class NeuronTwo : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private float _orbitAngle;
        private float _orbitSpeed;
        private float _orbitRadius;
        private FSprite _twoSprite;

        public NeuronTwo(Player player)
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
                Plugin.Logger.LogError($"[UOT-VD] NeuronTwo.Update exception: {ex}");
            }
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];

            _twoSprite = new FSprite("Futile_White", true);
            _twoSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _twoSprite.scale = 14f / 16f;
            _twoSprite.alpha = 1f;
            _twoSprite.color = new Color(0.9f, 0.9f, 0.9f);  // 白色
            sLeaser.sprites[0] = _twoSprite;

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

            _twoSprite.SetPosition(orbitPos);
            _twoSprite.color = new Color(0.9f, 0.9f, 0.9f);
            _twoSprite.scale = (13f + Mathf.Sin(angle * 2.3f) * 3f) / 16f;
            _twoSprite.alpha = 0.92f + Mathf.Sin(angle * 1.7f) * 0.08f;

            if (sLeaser.deleteMeNextFrame) return;

            if (sLeaser.sprites[0].container == null)
                rCam.ReturnFContainer("HUD2").AddChild(sLeaser.sprites[0]);
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }
    }
}
