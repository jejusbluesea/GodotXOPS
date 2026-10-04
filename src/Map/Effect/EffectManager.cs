using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// 이펙트(총구 화염, 연기, 탄피, 혈흔, 폭발) 빌보드 쿼드의 풀을 관리하는 싱글톤. 풀 크기는 원본 MAX_EFFECT(256) 이다.
    /// 이펙트 프리셋(EffectData) 하나는 여러 emitter 의 묶음이고, emitter 하나가 원본의 AddEffect 호출 하나에 해당한다.
    /// 게임 결과에 영향을 주지 않는 연출이라 틱이 아니라 렌더 프레임에서 진행하고, 난수도 연출용 스트림을 쓴다.
    /// 원본 effect::ProcessObject (object.cpp:3168-3207) 처럼 위치·크기·투명도·회전을 시간에 따라 바꾸고, 투명도나 수명이 다하면 풀로 돌려보낸다.
    /// </summary>
    public partial class EffectManager : Singleton<EffectManager>
    {
        public const int PoolSize = 256;

        // 벽 데칼을 면에서 살짝 띄워 겹쳐 떨리는 것을 막는다 (m).
        private const float k_decalSurfaceOffset = 0.05f;

        private static readonly StringName s_effectAlpha = "effect_alpha";

        /// <summary>
        /// 풀 한 자리의 상태.
        /// </summary>
        private class Slot
        {
            public MeshInstance3D node;
            public bool active;
            public Vector3 position;
            public Vector3 velocity;
            public float gravityY;
            public float rotation;
            public float rotationRate;
            public float size;
            public float sizeRate;
            public float alpha;
            public float alphaRate;
            public float lifetime;
            public bool billboard;
            public bool collideMap;
            public Basis fixedBasis;
        }

        private readonly Slot[] m_pool = new Slot[PoolSize];
        private readonly Dictionary<int, ShaderMaterial> m_materials = new Dictionary<int, ShaderMaterial>();

        // 점검 도구용 누계.
        public static int SpawnCount { get; private set; }

        public override void _Ready()
        {
            // 1 × 1 쿼드. 앞면이 +Z 를 향한다. 크기는 노드 스케일로 조절한다.
            var quad = new QuadMesh { Size = Vector2.One };
            for (int i = 0; i < PoolSize; i++)
            {
                var node = new MeshInstance3D
                {
                    Name = $"Effect_{i}",
                    Mesh = quad,
                    Visible = false,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(node);
                m_pool[i] = new Slot { node = node };
            }
        }

        public override void _Process(double delta)
        {
            Camera3D camera = GetViewport().GetCamera3D();
            Vector3 cameraPosition = camera != null ? camera.GlobalPosition : Vector3.Zero;
            float dt = (float)delta;

            for (int i = 0; i < PoolSize; i++)
            {
                if (m_pool[i].active) Tick(m_pool[i], dt, cameraPosition);
            }
        }

        /// <summary>
        /// 이펙트 프리셋을 방향 없이 재생한다. 착탄, 폭발처럼 방향이 없는 이펙트용.
        /// </summary>
        /// <param name="effectIndex">EffectParameterData.effectData 인덱스. 범위 밖이면 무시.</param>
        /// <param name="position">재생 위치.</param>
        /// <param name="triggerValue">개수를 정하는 값(피격 데미지 등). countPerTrigger 를 쓰는 emitter 에만 영향을 준다.</param>
        public void Play(int effectIndex, Vector3 position, float triggerValue = 0f)
        {
            Play(effectIndex, position, Basis.Identity, 1f, Vector3.Zero, triggerValue);
        }

        /// <summary>
        /// 이펙트 프리셋을 재생한다. emitter 마다 지정된 개수만큼 풀에서 꺼내 무작위 범위를 적용한다. 풀이 가득 차면 남은 것은 버린다.
        /// </summary>
        /// <param name="effectIndex">EffectParameterData.effectData 인덱스. 범위 밖이면 무시.</param>
        /// <param name="position">재생 위치.</param>
        /// <param name="orientation">emitter 의 위치 오프셋과 속도를 돌리는 기준 (무기 부착 루트의 방향 등). 빌보드가 아닌 이펙트는 이 방향으로 고정된다.</param>
        /// <param name="sizeScale">emitter 크기에 곱하는 배율 (무기별 총구 화염·탄피 크기).</param>
        /// <param name="extraVelocity">방향과 무관하게 더하는 속도 (탄피가 튀어나가는 속도).</param>
        /// <param name="triggerValue">개수를 정하는 값. countPerTrigger 가 0 보다 큰 emitter 는 개수 = floor(이 값 × countPerTrigger).</param>
        public void Play(int effectIndex, Vector3 position, Basis orientation, float sizeScale, Vector3 extraVelocity, float triggerValue = 0f)
        {
            List<EffectData> all = DataManager.Instance.EffectParameterData.effectData;
            if (effectIndex < 0 || effectIndex >= all.Count) return;

            List<EffectEmitter> emitters = all[effectIndex].emitters;
            for (int e = 0; e < emitters.Count; e++)
            {
                EffectEmitter emitter = emitters[e];
                ShaderMaterial material = GetMaterial(emitter.textureIndex);
                if (material == null) continue;

                // 혈흔이 튀는 수는 데미지에 비례한다 (원본 damage / 10).
                int count = emitter.countPerTrigger > 0f ? Mathf.FloorToInt(triggerValue * emitter.countPerTrigger) : emitter.spawnCount;

                for (int s = 0; s < count; s++)
                {
                    Slot slot = FindIdle();
                    if (slot == null) return;

                    slot.active = true;
                    slot.position = position + orientation * Coord.FromUnity(emitter.positionOffset + RandomVector(emitter.positionRandomRange));
                    slot.velocity = orientation * Coord.FromUnity(emitter.velocity + RandomVector(emitter.velocityRandomRange)) + extraVelocity;
                    slot.gravityY = emitter.gravityY;
                    slot.rotation = emitter.rotationDeg + RandomRange(emitter.rotationRandomRange);
                    slot.rotationRate = emitter.rotationRateDeg + RandomRange(emitter.rotationRateRandomRange);
                    slot.size = (emitter.size + RandomRange(emitter.sizeRandomRange)) * sizeScale;
                    slot.sizeRate = emitter.sizeRate;
                    slot.alpha = emitter.alpha;
                    slot.alphaRate = emitter.alphaRate;
                    slot.lifetime = emitter.lifetime;
                    slot.billboard = (emitter.flags & EffectFlags.NoBillboard) == 0;
                    slot.collideMap = (emitter.flags & EffectFlags.CollideMap) != 0;
                    slot.fixedBasis = orientation;

                    slot.node.MaterialOverride = material;
                    Camera3D camera = GetViewport().GetCamera3D();
                    ApplyTransform(slot, camera != null ? camera.GlobalPosition : slot.position);
                    slot.node.SetInstanceShaderParameter(s_effectAlpha, slot.alpha);
                    slot.node.Visible = true;
                    SpawnCount++;
                }
            }
        }

        /// <summary>
        /// 혈흔 입자가 블록에 닿은 자리에 벽 데칼을 만든다. 원본 ObjectManager::AddMapEffect 에 해당한다.
        /// </summary>
        /// <param name="point">닿은 지점.</param>
        /// <param name="normal">닿은 면의 법선.</param>
        public void SpawnWallBlood(Vector3 point, Vector3 normal)
        {
            // 쿼드의 앞면(+Z)이 법선을 향하게 한다. 바닥·천장이면 위쪽 기준을 바꾼다.
            Vector3 up = Mathf.Abs(normal.Y) > 0.99f ? Vector3.Forward : Vector3.Up;
            Basis basis = Basis.LookingAt(-normal, up);

            int index = DataManager.Instance.EffectParameterData.effectGeneralData.wallBloodEffectIndex;
            Play(index, point + normal * k_decalSurfaceOffset, basis, 1f, Vector3.Zero);
        }

        /// <summary>
        /// 재생 중인 이펙트를 모두 치운다. 맵을 내릴 때 호출한다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < PoolSize; i++) Recycle(m_pool[i]);
        }

        /// <summary>
        /// 재생 중인 이펙트 수를 센다.
        /// </summary>
        /// <returns>활성 이펙트 수.</returns>
        public int CountActive()
        {
            int count = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (m_pool[i].active) count++;
            }
            return count;
        }

        /// <summary>
        /// 이펙트 하나를 한 프레임 진행한다.
        /// </summary>
        /// <param name="slot">풀 자리.</param>
        /// <param name="dt">프레임 시간.</param>
        /// <param name="cameraPosition">카메라 위치.</param>
        private void Tick(Slot slot, float dt, Vector3 cameraPosition)
        {
            slot.lifetime -= dt;
            if (slot.lifetime <= 0f)
            {
                Recycle(slot);
                return;
            }

            Vector3 next = slot.position + slot.velocity * dt;

            // 움직이는 혈흔 입자가 블록에 닿으면 그 자리에 벽 데칼을 남기고 사라진다 (원본 CollideBlood, objectmanager.cpp:1281-1320).
            if (slot.collideMap)
            {
                Vector3 move = next - slot.position;
                float distance = move.Length();
                if (distance > 1e-6f && MapLoader.RaycastBlock(slot.position, move / distance, distance, out float hitDist, out Vector3 normal))
                {
                    SpawnWallBlood(slot.position + move / distance * hitDist, normal);
                    Recycle(slot);
                    return;
                }
            }

            slot.position = next;
            slot.velocity.Y += slot.gravityY * dt;

            slot.size += slot.sizeRate * dt;
            slot.alpha += slot.alphaRate * dt;
            // 원본은 투명도가 0 이하가 되면 수명이 남아도 바로 지운다 (object.cpp:3196).
            if (slot.size <= 0f || slot.alpha <= 0f)
            {
                Recycle(slot);
                return;
            }

            slot.rotation += slot.rotationRate * dt;

            ApplyTransform(slot, cameraPosition);
            slot.node.SetInstanceShaderParameter(s_effectAlpha, slot.alpha);
        }

        /// <summary>
        /// 이펙트 노드의 위치·방향·크기를 적용한다. 빌보드는 카메라를 향하고, 아니면 고정 방향이다. 둘 다 그 위에 텍스처 회전을 얹는다.
        /// </summary>
        /// <param name="slot">풀 자리.</param>
        /// <param name="cameraPosition">카메라 위치.</param>
        private static void ApplyTransform(Slot slot, Vector3 cameraPosition)
        {
            Basis basis;
            if (slot.billboard)
            {
                Vector3 toCamera = cameraPosition - slot.position;
                if (toCamera.LengthSquared() > 1e-6f)
                {
                    Vector3 direction = toCamera.Normalized();
                    Vector3 up = Mathf.Abs(direction.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
                    basis = Basis.LookingAt(-direction, up);
                }
                else
                {
                    basis = Basis.Identity;
                }
            }
            else
            {
                basis = slot.fixedBasis;
            }

            basis *= new Basis(Vector3.Back, Mathf.DegToRad(slot.rotation));
            slot.node.Transform = new Transform3D(basis.Scaled(Vector3.One * slot.size), slot.position);
        }

        private static void Recycle(Slot slot)
        {
            slot.active = false;
            slot.node.Visible = false;
        }

        private Slot FindIdle()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (!m_pool[i].active) return m_pool[i];
            }
            return null;
        }

        /// <summary>
        /// 텍스처 번호에 해당하는 이펙트 머티리얼을 얻는다. 한 번 만들면 계속 쓴다.
        /// </summary>
        /// <param name="textureIndex">EffectGeneralData.texturePaths 인덱스.</param>
        /// <returns>머티리얼. 텍스처가 없으면 null.</returns>
        private ShaderMaterial GetMaterial(int textureIndex)
        {
            if (m_materials.TryGetValue(textureIndex, out ShaderMaterial cached)) return cached;

            List<string> paths = DataManager.Instance.EffectParameterData.effectGeneralData.texturePaths;
            if (textureIndex < 0 || textureIndex >= paths.Count) return null;

            string fullPath = GamePath.Resolve(paths[textureIndex]);
            ImageTexture texture = fullPath != null ? ImageLoader.LoadTexture(fullPath) : null;
            if (texture == null) return null;

            ShaderMaterial material = MaterialManager.Instance.CreateEffectMaterial(texture);
            m_materials[textureIndex] = material;
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
