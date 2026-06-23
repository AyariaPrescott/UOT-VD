using System;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 灰色神经元小球，绕玩家身体旋转。
    /// </summary>
    public class NeuronZero : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private float _orbitAngle;
        private float _orbitSpeed;
        private float _orbitRadius;
        private FSprite _zeroSprite;

        public NeuronZero(Player player)
        {
            _playerRef = new WeakReference(player);
            _pos = player.mainBodyChunk.pos;
            _lastPos = _pos;
            _orbitAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _orbitSpeed = 0.03f + UnityEngine.Random.Range(0f, 0.02f);
            _orbitRadius = 25f + UnityEngine.Random.Range(0f, 8f);
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
            base.Update(eu);

            if (_playerRef.Target is Player player)
            {
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
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];

            _zeroSprite = new FSprite("Futile_White", true);
            _zeroSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _zeroSprite.scale = 16f / 16f;
            _zeroSprite.alpha = 1f;
            _zeroSprite.color = new Color(0.75f, 0.75f, 0.75f);
            sLeaser.sprites[0] = _zeroSprite;

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

            float px = Mathf.Cos(angle) * _orbitRadius;
            float py = Mathf.Sin(angle) * _orbitRadius * 0.6f;

            Vector2 orbitPos = new Vector2(drawPos.x + px, drawPos.y + 10f + py) - camPos;

            _zeroSprite.SetPosition(orbitPos);
            _zeroSprite.color = new Color(0.75f, 0.75f, 0.75f);
            _zeroSprite.scale = (15f + Mathf.Sin(angle * 2f) * 3f) / 16f;
            _zeroSprite.alpha = 0.92f + Mathf.Sin(angle * 1.5f) * 0.08f;

            if (sLeaser.deleteMeNextFrame) return;

            if (sLeaser.sprites[0].container == null)
                rCam.ReturnFContainer("HUD2").AddChild(sLeaser.sprites[0]);
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }
    }
}
