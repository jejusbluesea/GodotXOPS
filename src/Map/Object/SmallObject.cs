using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 맵에 놓인 파괴 가능한 소물 (캔, PC 등). 원본 OpenXOPS smallobject 에 해당한다.
    /// 모델은 자식 노드로 조립하고, 총알·폭발 판정은 ObjectColliderData 의 형상(구·박스·캡슐)에 대해 "점이 안에 있는가"를 직접 계산한다.
    /// 부서지면 판정에서 바로 빠지고, 튀어 올랐다 사라지는 움직임은 화면 연출로만 진행한다.
    /// 이벤트가 걸면 정해진 시간에 걸쳐 한 자리로 움직인다 (StartTween). 판정이 따라가야 하므로 틱에서 논리 위치를 옮기고 노드는 틱 사이를 보간한다. 원본에는 없는 동작이다.
    /// </summary>
    public partial class SmallObject : Node3D, ISimTickable
    {
        // 부서질 때의 움직임 — 원본 smallobject::Destruction / ProcessObject (object.cpp:2684-2751) 의 프레임당 값을 초 단위로 바꾼 것.
        // 수평 속도 = jump × 0.04243, 처음 상승 속도 = jump × 0.1, 프레임마다 0.1 씩 감소 (모두 원본 길이 단위 / 프레임).
        private const float k_destroyHorizontalPerJump = 0.04243f * Coord.Scale * SimClock.FrameRate; // m/s
        private const float k_destroyVerticalPerJump = 0.1f * Coord.Scale * SimClock.FrameRate; // m/s
        private const float k_destroyGravity = 0.1f * Coord.Scale * SimClock.FrameRate * SimClock.FrameRate; // m/s²
        // 사라지기까지의 시간 (초). 원본은 34 프레임 뒤에 지운다 (cnt > GAMEFPS).
        private const float k_destroyLifetime = 34f / SimClock.FrameRate;
        // 바닥에 붙일 때의 레이 시작 높이와 최대 거리 (m). 원본 COLLISION_ADDSIZE, 1000.0.
        private const float k_snapEpsilon = 0.001f;
        private const float k_snapMaxDistance = 100f;
        // 구형 소물이 바닥에 묻히는 깊이 = 반지름 × 3/13. 원본은 판정 크기(decide)의 1/10 만큼만 띄우고, 구 반지름은 decide × 0.13 이다.
        private const float k_sphereSinkRatio = 3f / 13f;

        private Node3D m_visualRoot;
        private ObjectData m_objectData;
        private ObjectColliderData m_colliderData;
        private int m_objectIndex;
        private int m_identifier;
        private float m_hp;
        private bool m_destroyed;
        // 논리 위치와 방향. 노드의 transform 은 부서진 뒤 연출로 움직이므로 판정에는 이 값을 쓴다.
        private Vector3 m_position;
        private Basis m_colliderBasis;

        // 움직이는 중(트윈)의 상태: 출발과 도착 자리, 전체 틱 수와 지난 틱 수, 한 틱 전의 논리 위치(보간용).
        private bool m_tweening;
        private bool m_tweenEase;
        private Vector3 m_tweenFrom;
        private Vector3 m_tweenTo;
        private Vector3 m_previousPosition;
        private int m_tweenTicks;
        private int m_tweenElapsed;

        private Vector3 m_destroyVelocity;
        private Vector2 m_destroyAngularVelocity;
        private float m_destroyTimer;

        public int ObjectIndex => m_objectIndex;
        public ObjectData ObjectData => m_objectData;
        public int Identifier => m_identifier;
        public float HP => m_hp;
        public bool IsDestroyed => m_destroyed;
        public Vector3 LogicPosition => m_position;
        // 판정 형상과 그 방향. 디버그 표시(ColliderView)가 판정과 같은 자리에 그릴 때 쓴다. 형상이 없으면 null.
        public ObjectColliderData ColliderData => m_colliderData;
        public Basis ColliderBasis => m_colliderBasis;
        public bool IsTweening => m_tweening;
        // 사람(10)과 떨어진 무기(30) 뒤, 총알(40) 앞. 총알은 이번 틱에 옮겨진 자리로 판정한다.
        public int SimOrder => 35;

        /// <summary>
        /// 소물을 만든다. 트리에 추가한 뒤 호출한다.
        /// </summary>
        /// <param name="objectIndex">ObjectParameterData.objectData 인덱스.</param>
        /// <param name="identifier">식별번호 (PD1 의 네 번째 파라미터). 이벤트가 소물을 찾을 때 쓴다.</param>
        /// <param name="position">위치.</param>
        /// <param name="yawDeg">방향 yaw (도).</param>
        public void CreateObject(int objectIndex, int identifier, Vector3 position, float yawDeg)
        {
            ObjectParameterData parameter = DataManager.Instance.ObjectParameterData;
            m_objectIndex = objectIndex;
            m_objectData = parameter.objectData[objectIndex];
            m_identifier = identifier;
            m_hp = m_objectData.hp;
            m_destroyed = false;

            int colliderIndex = m_objectData.colliderIndex;
            m_colliderData = parameter.objectColliderData.Has(colliderIndex) ? parameter.objectColliderData[colliderIndex] : null;

            Position = position;
            Rotation = Coord.FromUnityEuler(new Vector3(0f, yawDeg, 0f));
            m_position = position;
            // 형상 중심은 Y 180° 돌린 공간 기준이다 (모델과 같은 보정).
            m_colliderBasis = Basis * Basis.FromEuler(new Vector3(0f, Mathf.Pi, 0f));

            // 원본 모델은 정면이 반대라 Y 180° 돌려 놓는다.
            m_visualRoot = new Node3D
            {
                Name = "Visual",
                Rotation = new Vector3(0f, Mathf.Pi, 0f),
                Scale = Vector3.One * parameter.objectGeneralData.modelScale,
            };
            AddChild(m_visualRoot);

            int modelIndex = m_objectData.modelIndex;
            if (parameter.objectModelData.Has(modelIndex))
            {
                ObjectModelData model = parameter.objectModelData[modelIndex];
                WeaponVisual.BuildModelParts(m_visualRoot, model.textures, model.modelData);
            }
        }

        public override void _ExitTree()
        {
            SimClock.Unregister(this);
        }

        /// <summary>
        /// 정해진 시간에 걸쳐 한 자리로 움직이기 시작한다. 방향은 바로 바뀌고, 자리는 지금 자리에서 직선으로 옮겨 간다. 블록과 사람을 무시한다.
        /// 움직이는 중에 다시 부르면 그 순간의 자리에서 새로 시작한다. 부서진 소물은 움직이지 않는다.
        /// </summary>
        /// <param name="target">도착할 자리.</param>
        /// <param name="yawDeg">소물의 방향 yaw (도. 소물 포인트의 방향과 같은 값).</param>
        /// <param name="ticks">걸리는 틱 수. 0 이하면 바로 옮긴다.</param>
        /// <param name="ease">true 면 천천히 출발해 천천히 멈춘다. false 면 같은 속도로 간다.</param>
        /// <returns>시작했으면 true. 부서졌거나 자리가 올바른 수가 아니면 false.</returns>
        public bool StartTween(Vector3 target, float yawDeg, int ticks, bool ease)
        {
            if (m_destroyed || !target.IsFinite() || !float.IsFinite(yawDeg)) return false;

            Rotation = Coord.FromUnityEuler(new Vector3(0f, yawDeg, 0f));
            m_colliderBasis = Basis * Basis.FromEuler(new Vector3(0f, Mathf.Pi, 0f));
            if (ticks <= 0)
            {
                StopTween();
                m_position = target;
                Position = target;
                return true;
            }

            m_tweening = true;
            m_tweenEase = ease;
            m_tweenFrom = m_position;
            m_tweenTo = target;
            m_previousPosition = m_position;
            m_tweenTicks = ticks;
            m_tweenElapsed = 0;
            SimClock.Register(this);
            return true;
        }

        public void SimTick()
        {
            if (!m_tweening) return;

            // 도착한 다음 틱에 끝낸다. 그 사이에 노드가 마지막 한 틱을 보간해 따라온다.
            if (m_tweenElapsed >= m_tweenTicks)
            {
                StopTween();
                return;
            }

            m_previousPosition = m_position;
            m_tweenElapsed++;
            float progress = (float)m_tweenElapsed / m_tweenTicks;
            if (m_tweenEase) progress = progress * progress * (3f - 2f * progress);
            m_position = m_tweenFrom.Lerp(m_tweenTo, progress);
        }

        /// <summary>
        /// 움직임을 멈추고 노드를 논리 위치에 맞춘다.
        /// </summary>
        private void StopTween()
        {
            if (!m_tweening) return;

            m_tweening = false;
            SimClock.Unregister(this);
            Position = m_position;
        }

        public override void _Process(double delta)
        {
            if (m_tweening && !m_destroyed) Position = m_previousPosition.Lerp(m_position, SimClock.InterpolationAlpha);
            if (!m_destroyed || !Visible) return;

            float dt = (float)delta;
            m_destroyVelocity.Y -= k_destroyGravity * dt;
            Position += m_destroyVelocity * dt;
            RotateObjectLocal(Vector3.Right, -Mathf.DegToRad(m_destroyAngularVelocity.X) * dt);
            RotateObjectLocal(Vector3.Up, -Mathf.DegToRad(m_destroyAngularVelocity.Y) * dt);

            m_destroyTimer -= dt;
            if (m_destroyTimer <= 0f) Visible = false;
        }

        /// <summary>
        /// 점이 이 소물의 판정 형상 안에 있는지 본다. 부서진 소물은 맞지 않는다.
        /// </summary>
        /// <param name="point">검사할 점.</param>
        /// <returns>형상 중 하나라도 점을 포함하면 true.</returns>
        public bool Contains(Vector3 point)
        {
            if (m_destroyed || m_colliderData == null) return false;

            Vector3 local = m_colliderBasis.Inverse() * (point - m_position);
            for (int i = 0; i < m_colliderData.shapes.Count; i++)
            {
                ColliderShape shape = m_colliderData.shapes[i];
                if (ShapeContains(shape, local - Coord.FromUnity(shape.center))) return true;
            }
            return false;
        }

        /// <summary>
        /// 바로 아래 블록 위로 내려 붙인다. 원본 smallobject::CollisionMap (object.cpp:2633-2658).
        /// 가장 낮은 형상의 바닥이 지면에 닿게 하고, 그 형상이 구이면 원본처럼 조금 묻힌다.
        /// </summary>
        /// <returns>아래에 블록이 있어 위치를 옮겼으면 true.</returns>
        public bool SnapToGround()
        {
            Vector3 origin = m_position + Vector3.Up * k_snapEpsilon;
            if (!MapLoader.RaycastBlock(BlockLayer.Human, origin, Vector3.Down, k_snapMaxDistance + k_snapEpsilon, out float hitDist)) return false;

            float groundY = origin.Y - hitDist;
            float lowestBottom = 0f;
            ColliderShape lowest = null;
            if (m_colliderData != null)
            {
                for (int i = 0; i < m_colliderData.shapes.Count; i++)
                {
                    ColliderShape shape = m_colliderData.shapes[i];
                    float bottom = shape.center.Y - HalfExtentY(shape);
                    if (lowest == null || bottom < lowestBottom)
                    {
                        lowestBottom = bottom;
                        lowest = shape;
                    }
                }
            }

            float sink = lowest != null && lowest.type == ColliderShapeType.Sphere ? lowest.size.X * k_sphereSinkRatio : 0f;
            m_position.Y = groundY - lowestBottom - sink;
            Position = m_position;
            return true;
        }

        /// <summary>
        /// 총알에 맞은 처리. 원본 smallobject::HitBullet (object.cpp:2663-2669).
        /// </summary>
        /// <param name="attacks">데미지. 호출하는 쪽이 위력에 소물 데미지 배율을 곱해 넘긴다.</param>
        public void HitBullet(int attacks)
        {
            TakeDamage(attacks);
        }

        /// <summary>
        /// 폭발에 맞은 처리. 원본 smallobject::HitGrenadeExplosion (object.cpp:2674-2680).
        /// </summary>
        /// <param name="damage">데미지. 호출하는 쪽이 거리 감쇠를 적용해 넘긴다.</param>
        public void HitGrenadeExplosion(int damage)
        {
            TakeDamage(damage);
        }

        /// <summary>
        /// 내구력과 무관하게 바로 부순다 (스크립트 이벤트). 피격음은 내지 않는다.
        /// </summary>
        public void Break()
        {
            if (m_destroyed) return;

            m_hp = 0f;
            StartDestruction();
        }

        /// <summary>
        /// 피격음을 내고 내구력을 깎는다. 0 이하가 되면 부서진다.
        /// </summary>
        /// <param name="amount">데미지.</param>
        private void TakeDamage(int amount)
        {
            if (m_destroyed) return;

            // 원본은 맞을 때마다 한 번 소리를 낸다 (부서졌는지, 데미지가 얼마인지와 무관).
            if (SoundManager.Loaded) SoundManager.Instance.PlayAt(m_objectData.soundPath, m_position, m_objectData.soundVolume);

            m_hp -= amount;
            if (m_hp <= 0f)
            {
                m_hp = 0f;
                StartDestruction();
            }
        }

        /// <summary>
        /// 부서지기 시작한다. 원본 smallobject::Destruction (object.cpp:2684-2710): 10° 단위의 무작위 방향으로 튀어 오르며 돈다.
        /// 판정에서 빠진 뒤의 연출이라 연출용 난수를 쓴다.
        /// </summary>
        private void StartDestruction()
        {
            // 움직이던 소물은 그 자리에서 부서진다.
            StopTween();
            m_destroyed = true;

            int jump = m_objectData.jump;
            float direction = Mathf.DegToRad(GameRandom.Visual.Range(0, 36) * 10f);
            float horizontal = jump * k_destroyHorizontalPerJump;
            m_destroyVelocity = Coord.FromUnity(new Vector3(Mathf.Cos(direction) * horizontal, jump * k_destroyVerticalPerJump, Mathf.Sin(direction) * horizontal));
            // 원본 add_rx / add_ry = 프레임당 0~19°.
            m_destroyAngularVelocity = new Vector2(GameRandom.Visual.Range(0, 20), GameRandom.Visual.Range(0, 20)) * SimClock.FrameRate;
            m_destroyTimer = k_destroyLifetime;
        }

        /// <summary>
        /// 형상 중심 기준 좌표의 점이 형상 안에 있는지 본다.
        /// size 해석: 구 = x 반지름 / 박스 = 전체 크기 / 캡슐 = x 반지름, y 높이, z 축 방향(0 X, 1 Y, 2 Z).
        /// </summary>
        /// <param name="shape">형상.</param>
        /// <param name="local">형상 중심 기준 좌표.</param>
        /// <returns>안에 있으면 true.</returns>
        private static bool ShapeContains(ColliderShape shape, Vector3 local)
        {
            switch (shape.type)
            {
                case ColliderShapeType.Sphere:
                    return local.LengthSquared() <= shape.size.X * shape.size.X;

                case ColliderShapeType.Box:
                    return Mathf.Abs(local.X) <= shape.size.X * 0.5f
                        && Mathf.Abs(local.Y) <= shape.size.Y * 0.5f
                        && Mathf.Abs(local.Z) <= shape.size.Z * 0.5f;

                case ColliderShapeType.Capsule:
                {
                    float radius = shape.size.X;
                    float halfSegment = Mathf.Max(0f, shape.size.Y * 0.5f - radius);
                    int axis = Mathf.Clamp((int)shape.size.Z, 0, 2);
                    // 축 위에서 점에 가장 가까운 곳까지의 거리가 반지름 이하이면 안이다.
                    float along = Mathf.Clamp(local[axis], -halfSegment, halfSegment);
                    Vector3 nearest = Vector3.Zero;
                    nearest[axis] = along;
                    return (local - nearest).LengthSquared() <= radius * radius;
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// 형상 중심에서 바닥까지의 높이를 구한다.
        /// </summary>
        /// <param name="shape">형상.</param>
        /// <returns>중심에서 바닥까지의 거리.</returns>
        private static float HalfExtentY(ColliderShape shape)
        {
            switch (shape.type)
            {
                case ColliderShapeType.Sphere:
                    return shape.size.X;
                case ColliderShapeType.Box:
                    return shape.size.Y * 0.5f;
                case ColliderShapeType.Capsule:
                    return (int)shape.size.Z == 1 ? Mathf.Max(shape.size.Y, 2f * shape.size.X) * 0.5f : shape.size.X;
                default:
                    return 0f;
            }
        }
    }
}
