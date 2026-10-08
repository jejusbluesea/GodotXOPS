using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터의 미리 보기에서 이펙트 프리셋 하나를 되풀이해 재생한다. 입자가 모두 사라지면 잠깐 쉬었다가 다시 낸다.
    /// 게임의 EffectManager 는 게임의 3D 공간과 읽어 둔 데이터에 묶여 있어서, 미리 보기 공간에서 고치는 중인 데이터로 재생하려고 같은 계산을 따로 갖는다.
    /// **EffectManager 의 Spawn / Tick / ApplyTransform 을 고치면 여기도 같이 고친다.** 맵 충돌(혈흔이 벽에 닿는 것)은 미리 보지 않는다.
    /// </summary>
    public partial class EffectPreview : Node3D
    {
        /// <summary>
        /// 재생 중인 입자 하나.
        /// </summary>
        private sealed class Particle
        {
            public MeshInstance3D Node;
            public Vector3 Position;
            public Vector3 Velocity;
            public float GravityY;
            public float Rotation;
            public float RotationRate;
            public float Size;
            public float SizeRate;
            public float Alpha;
            public float AlphaRate;
            public float Lifetime;
            public bool Billboard;
            public bool Additive;
            public float Brightness;
            public float BrightnessRate;
        }

        // 입자가 모두 사라진 뒤 다시 낼 때까지 쉬는 시간 (초).
        private const float k_restSeconds = 0.5f;
        // 한 번에 내는 입자 수의 한계. 값을 잘못 넣어도 화면이 멈추지 않게 한다.
        private const int k_maxParticles = 512;
        // 개수가 "트리거값 × 배율"인 emitter(혈흔)에 넣는 트리거값. 게임에서는 피격 데미지다.
        public const float TriggerValue = 50f;

        private static readonly StringName s_effectAlpha = "effect_alpha";
        private static readonly StringName s_effectBright = "effect_bright";

        private readonly List<Particle> m_particles = new List<Particle>();
        private readonly Dictionary<(string, EffectBlendMode), ShaderMaterial> m_materials = new Dictionary<(string, EffectBlendMode), ShaderMaterial>();
        private QuadMesh m_quad;
        private EffectData m_effect;
        private Func<int, string> m_texturePath;
        private float m_rest;

        // 지금까지 낸 횟수 (점검용).
        public int Bursts { get; private set; }
        public int ParticleCount => m_particles.Count;

        /// <summary>
        /// 재생할 이펙트를 정한다. 바로 한 번 내고, 그 뒤로는 되풀이한다.
        /// </summary>
        /// <param name="effect">이펙트 프리셋. 고치는 중인 데이터 객체를 그대로 줘도 된다 (낼 때마다 지금 값을 읽는다).</param>
        /// <param name="texturePath">이펙트 텍스처 번호를 이미지 경로(exe 폴더 기준)로 바꾸는 함수. 없는 번호면 null.</param>
        public void Play(EffectData effect, Func<int, string> texturePath)
        {
            m_effect = effect;
            m_texturePath = texturePath;
            Restart();
        }

        /// <summary>
        /// 재생 중인 입자를 치우고 처음부터 다시 낸다 (값을 고친 뒤).
        /// </summary>
        public void Restart()
        {
            ClearParticles();
            m_rest = 0f;
            Burst();
        }

        public override void _Process(double delta)
        {
            if (m_effect == null) return;

            Camera3D camera = GetViewport().GetCamera3D();
            Vector3 cameraPosition = camera != null ? camera.GlobalPosition : Vector3.Zero;
            float dt = (float)delta;
            for (int i = m_particles.Count - 1; i >= 0; i--)
            {
                if (Tick(m_particles[i], dt, cameraPosition)) continue;

                RemoveChild(m_particles[i].Node);
                m_particles[i].Node.Free();
                m_particles.RemoveAt(i);
            }

            if (m_particles.Count > 0) return;
            m_rest += dt;
            if (m_rest >= k_restSeconds)
            {
                m_rest = 0f;
                Burst();
            }
        }

        /// <summary>
        /// 이펙트를 한 번 낸다: emitter 마다 정해진 수의 입자를 무작위 범위를 적용해 만든다 (EffectManager.Spawn 과 같다).
        /// </summary>
        private void Burst()
        {
            if (m_effect == null) return;

            m_quad ??= new QuadMesh { Size = Vector2.One };
            Bursts++;
            Camera3D camera = IsInsideTree() ? GetViewport().GetCamera3D() : null;
            foreach (EffectEmitter emitter in m_effect.emitters)
            {
                ShaderMaterial material = GetMaterial(emitter.textureIndex, emitter.blendMode);
                if (material == null) continue;

                int count = emitter.countPerTrigger > 0f ? Mathf.FloorToInt(TriggerValue * emitter.countPerTrigger) : emitter.spawnCount;
                for (int s = 0; s < count && m_particles.Count < k_maxParticles; s++)
                {
                    var particle = new Particle
                    {
                        Position = Coord.FromUnity(emitter.positionOffset + RandomVector(emitter.positionRandomRange)),
                        Velocity = Coord.FromUnity(emitter.velocity + RandomVector(emitter.velocityRandomRange)),
                        GravityY = emitter.gravityY,
                        Rotation = emitter.rotationDeg + RandomRange(emitter.rotationRandomRange),
                        RotationRate = emitter.rotationRateDeg + RandomRange(emitter.rotationRateRandomRange),
                        Size = emitter.size + RandomRange(emitter.sizeRandomRange),
                        SizeRate = emitter.sizeRate,
                        Alpha = emitter.alpha,
                        AlphaRate = emitter.alphaRate,
                        Lifetime = emitter.lifetime,
                        Billboard = (emitter.flags & EffectFlags.NoBillboard) == 0,
                        Additive = emitter.blendMode == EffectBlendMode.Additive,
                        Brightness = emitter.brightness,
                        BrightnessRate = emitter.brightnessRate,
                        Node = new MeshInstance3D { Mesh = m_quad, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off },
                    };
                    AddChild(particle.Node);
                    m_particles.Add(particle);
                    Apply(particle, camera != null ? camera.GlobalPosition : particle.Position);
                }
            }
        }

        /// <summary>
        /// 입자 하나를 한 프레임 진행한다 (EffectManager.Tick 과 같다. 맵 충돌은 뺀다).
        /// </summary>
        /// <param name="particle">입자.</param>
        /// <param name="dt">프레임 시간 (초).</param>
        /// <param name="cameraPosition">카메라 위치.</param>
        /// <returns>아직 살아 있으면 true.</returns>
        private static bool Tick(Particle particle, float dt, Vector3 cameraPosition)
        {
            particle.Lifetime -= dt;
            if (particle.Lifetime <= 0f) return false;

            particle.Position += particle.Velocity * dt;
            particle.Velocity.Y += particle.GravityY * dt;
            particle.Size += particle.SizeRate * dt;
            particle.Alpha += particle.AlphaRate * dt;
            particle.Brightness += particle.BrightnessRate * dt;
            if (particle.Size <= 0f || particle.Alpha <= 0f || (particle.Additive && particle.Brightness <= 0f)) return false;

            particle.Rotation += particle.RotationRate * dt;
            Apply(particle, cameraPosition);
            return true;
        }

        /// <summary>
        /// 입자의 위치·방향·크기·투명도를 노드에 적용한다. 빌보드는 카메라를 향하고, 아니면 눕지 않은 채 고정이다.
        /// </summary>
        /// <param name="particle">입자.</param>
        /// <param name="cameraPosition">카메라 위치.</param>
        private static void Apply(Particle particle, Vector3 cameraPosition)
        {
            Basis basis = Basis.Identity;
            Vector3 toCamera = cameraPosition - particle.Position;
            if (particle.Billboard && toCamera.LengthSquared() > 1e-6f)
            {
                Vector3 direction = toCamera.Normalized();
                Vector3 up = Mathf.Abs(direction.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
                basis = Basis.LookingAt(-direction, up);
            }
            basis *= new Basis(Vector3.Back, Mathf.DegToRad(particle.Rotation));
            particle.Node.Transform = new Transform3D(basis.Scaled(Vector3.One * Mathf.Max(particle.Size, 1e-4f)), particle.Position);
            particle.Node.SetInstanceShaderParameter(s_effectAlpha, particle.Alpha);
            if (particle.Additive) particle.Node.SetInstanceShaderParameter(s_effectBright, particle.Brightness);
        }

        /// <summary>
        /// 재생 중인 입자를 모두 치운다.
        /// </summary>
        private void ClearParticles()
        {
            foreach (Particle particle in m_particles)
            {
                RemoveChild(particle.Node);
                particle.Node.Free();
            }
            m_particles.Clear();
        }

        /// <summary>
        /// 텍스처 번호와 블렌드 방식의 이펙트 머티리얼을 얻는다. 같은 그림과 방식은 함께 쓴다.
        /// </summary>
        /// <param name="textureIndex">이펙트 텍스처 번호.</param>
        /// <param name="blendMode">색을 섞는 방식.</param>
        /// <returns>머티리얼. 텍스처가 없으면 null.</returns>
        private ShaderMaterial GetMaterial(int textureIndex, EffectBlendMode blendMode)
        {
            string path = m_texturePath?.Invoke(textureIndex);
            if (string.IsNullOrEmpty(path)) return null;
            if (m_materials.TryGetValue((path, blendMode), out ShaderMaterial cached)) return cached;

            string fullPath = GamePath.Resolve(path);
            ImageTexture texture = fullPath != null && System.IO.File.Exists(fullPath) ? GodotXOPS.IO.ImageLoader.LoadTexture(fullPath) : null;
            if (texture == null) return null;

            ShaderMaterial material = MaterialManager.Instance.CreateEffectMaterial(texture, blendMode);
            m_materials[(path, blendMode)] = material;
            return material;
        }

        private static Vector3 RandomVector(Vector3 range)
        {
            return new Vector3(RandomRange(range.X), RandomRange(range.Y), RandomRange(range.Z));
        }

        private static float RandomRange(float range)
        {
            return GameRandom.Visual.Range(-range, range);
        }
    }
}
