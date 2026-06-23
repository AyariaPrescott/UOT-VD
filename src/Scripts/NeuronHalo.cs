using System;
using UnityEngine;

namespace SlugTemplate
{
    /// <summary>
    /// 头顶漂浮的发光小球，被彩色粒子环绕。
    /// 白色核心球体 + 9个彩色粒子沿轨道旋转，带有自发光效果。
    /// </summary>
    public class NeuronHalo : UpdatableAndDeletable, IDrawable
    {
        private readonly WeakReference _playerRef;
        private Vector2 _pos;
        private Vector2 _lastPos;
        private float _rgbTimer;
        private float _bobPhase;
        private float _bobSpeed;
        private LightSource _lightSource;

        private FSprite _coreSprite;
        private FSprite _glowSprite;
        private const int PARTICLE_COUNT = 9;
        private readonly FSprite[] _particles = new FSprite[PARTICLE_COUNT];
        private readonly float[] _particlePhases = new float[PARTICLE_COUNT];
        private readonly float[] _particleSpeeds = new float[PARTICLE_COUNT];
        private readonly float[] _particleRadii = new float[PARTICLE_COUNT];

        public NeuronHalo(Player player)
        {
            _playerRef = new WeakReference(player);
            _pos = player.mainBodyChunk.pos;
            _lastPos = _pos;
            _rgbTimer = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _bobSpeed = 0.6f + UnityEngine.Random.Range(0f, 0.3f);

            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _particlePhases[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                _particleSpeeds[i] = 0.5f + UnityEngine.Random.Range(0f, 0.7f);
                _particleRadii[i] = 10f + UnityEngine.Random.Range(0f, 4f);
            }
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

                _rgbTimer += 0.005f;
                if (_rgbTimer > 1f) _rgbTimer -= 1f;

                _bobPhase += _bobSpeed * 0.02f;
                if (_bobPhase > Mathf.PI * 2f) _bobPhase -= Mathf.PI * 2f;

                for (int i = 0; i < PARTICLE_COUNT; i++)
                {
                    _particlePhases[i] += _particleSpeeds[i] * 0.025f;
                    if (_particlePhases[i] > Mathf.PI * 2f) _particlePhases[i] -= Mathf.PI * 2f;
                }
            }
        }

        private Color GetParticleColor(int index)
        {
            float hue = (index / (float)PARTICLE_COUNT + _rgbTimer) % 1f;
            return Color.HSVToRGB(hue, 0.85f, 1f);
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[2 + PARTICLE_COUNT];

            _glowSprite = new FSprite("Futile_White", true);
            _glowSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _glowSprite.scale = 28f / 16f;
            _glowSprite.alpha = 0.55f;
            sLeaser.sprites[0] = _glowSprite;

            _coreSprite = new FSprite("Futile_White", true);
            _coreSprite.shader = rCam.game.rainWorld.Shaders["LightSource"];
            _coreSprite.scale = 38f / 16f;
            _coreSprite.alpha = 1f;
            sLeaser.sprites[1] = _coreSprite;

            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _particles[i] = new FSprite("Futile_White", true);
                _particles[i].shader = rCam.game.rainWorld.Shaders["LightSource"];
                _particles[i].scale = 10f / 16f;
                _particles[i].alpha = 0.95f;
                sLeaser.sprites[2 + i] = _particles[i];
            }

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
            float currentBob = _bobPhase + _bobSpeed * timeStacker * 0.02f;
            float bobY = Mathf.Sin(currentBob) * 5f;
            float bobX = Mathf.Cos(currentBob * 1.15f) * 2f;

            Vector2 center = new Vector2(drawPos.x + bobX, drawPos.y + 45f + bobY) - camPos;

            Color glowColor = Color.HSVToRGB(_rgbTimer, 0.6f, 1f);
            _glowSprite.SetPosition(center);
            _glowSprite.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.50f + Mathf.Sin(currentBob) * 0.08f);
            _glowSprite.scale = (28f + Mathf.Sin(currentBob * 1.3f) * 4f) / 16f;

            Color coreColor = Color.HSVToRGB(_rgbTimer, 0.7f, 1f);
            _coreSprite.SetPosition(center);
            _coreSprite.color = Color.Lerp(coreColor, Color.white, 0.25f);
            _coreSprite.scale = (38f + Mathf.Sin(currentBob * 1.8f) * 5f) / 16f;
            _coreSprite.alpha = 0.95f + Mathf.Sin(currentBob * 1.8f) * 0.05f;

            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                float phase = _particlePhases[i] + _particleSpeeds[i] * timeStacker * 0.025f;
                float radius = _particleRadii[i];
                float angle = phase;
                float px = Mathf.Cos(angle) * radius;
                float py = Mathf.Sin(angle) * radius * 0.75f;

                Vector2 pPos = center + new Vector2(px, py);
                _particles[i].SetPosition(pPos);
                _particles[i].color = GetParticleColor(i);
                _particles[i].scale = (9f + Mathf.Sin(currentBob * 2f + _particlePhases[i]) * 2f) / 16f;
                _particles[i].alpha = 0.92f + Mathf.Sin(currentBob * 1.5f + _particlePhases[i]) * 0.08f;
            }

            Vector2 worldPos = center + camPos;
            Color lightColor = Color.HSVToRGB(_rgbTimer, 0.5f, 0.9f);
            if (_lightSource == null || _lightSource.slatedForDeletetion)
            {
                _lightSource = new LightSource(worldPos, false, lightColor, this);
                _lightSource.requireUpKeep = true;
                room.AddObject(_lightSource);
            }
            _lightSource.setPos = worldPos;
            _lightSource.setRad = 160f;
            _lightSource.setAlpha = 0.35f + Mathf.Sin(currentBob) * 0.08f;
            _lightSource.color = lightColor;

            if (sLeaser.deleteMeNextFrame) return;

            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if (sLeaser.sprites[i].container == null)
                    rCam.ReturnFContainer("HUD2").AddChild(sLeaser.sprites[i]);
            }
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }
    }
}
