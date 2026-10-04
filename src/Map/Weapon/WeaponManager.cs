using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 맵에 떨어져 있는 무기(맵 배치, 버린 무기, 사망 시 떨어뜨린 무기)를 고정 크기 풀로 관리하는 싱글톤.
    /// 사람이 든 무기는 여기 속하지 않는다. 풀 크기는 원본 MAX_WEAPON(200) 이다.
    /// 낙하와 줍기는 SimClock 틱에서 계산하고(무기가 멈춘 위치가 줍기 판정에 쓰인다), 모델은 틱 사이를 보간해 놓는다.
    /// 낙하 수치는 WeaponDropPhysicsData 에서 온다. 틱 단위로 바꾸면 원본 weapon::ProcessObject (object.cpp:2448-2516) 와 같은 값이다.
    /// </summary>
    public partial class WeaponManager : Singleton<WeaponManager>, ISimTickable
    {
        public const int PoolSize = 200;

        /// <summary>
        /// 풀 한 자리의 상태.
        /// </summary>
        private class Slot
        {
            public readonly Weapon weapon = new Weapon();
            public bool active;
            public bool falling;
            public Vector3 position;
            public Vector3 prevPosition;
            public Vector3 velocity;
            public Node3D node;
            public WeaponVisual visual;
        }

        private readonly Slot[] m_pool = new Slot[PoolSize];

        // 원본 무기 처리 — 사람(10) 뒤, 총알(40) 앞.
        public int SimOrder => 30;

        public override void _Ready()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var slot = new Slot
                {
                    node = new Node3D { Name = $"DroppedWeapon_{i}", Visible = false },
                    visual = new WeaponVisual { Name = "Weapon" },
                };
                AddChild(slot.node);
                slot.node.AddChild(slot.visual);
                m_pool[i] = slot;
            }

            SimClock.Register(this);
        }

        public override void _ExitTree()
        {
            SimClock.Unregister(this);
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float alpha = SimClock.InterpolationAlpha;
            for (int i = 0; i < PoolSize; i++)
            {
                Slot slot = m_pool[i];
                if (slot.active) slot.node.Position = slot.prevPosition.Lerp(slot.position, alpha);
            }
        }

        public void SimTick()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                Slot slot = m_pool[i];
                if (!slot.active) continue;

                slot.prevPosition = slot.position;
                if (slot.falling) TickFall(slot);
            }

            TickPickup();
        }

        /// <summary>
        /// 풀에서 빈 자리를 찾아 떨어진 무기를 놓는다. 빈 자리는 앞에서부터 찾는다 (원본 AddWeaponIndex).
        /// </summary>
        /// <param name="weaponIndex">무기 인덱스.</param>
        /// <param name="magazine">장전된 탄.</param>
        /// <param name="reserve">예비 탄.</param>
        /// <param name="position">놓을 위치.</param>
        /// <param name="yawDeg">모델 방향 yaw (도).</param>
        /// <param name="horizontalVelocity">초기 수평 속도 (m/s).</param>
        /// <returns>놓았으면 true. 풀이 가득 찼으면 false.</returns>
        public bool Spawn(int weaponIndex, int magazine, int reserve, Vector3 position, float yawDeg, Vector3 horizontalVelocity)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                Slot slot = m_pool[i];
                if (slot.active) continue;

                slot.weapon.Configure(weaponIndex, magazine, reserve, false);
                slot.active = true;
                slot.falling = true;
                slot.position = position;
                slot.prevPosition = position;
                slot.velocity = new Vector3(horizontalVelocity.X, 0f, horizontalVelocity.Z);

                slot.visual.Build(slot.weapon.Data, slot.weapon.ModelData, true);
                // 떨어진 무기는 옆으로 눕혀 놓는다 (원본 weapon::Render 의 모델 축 보정).
                slot.node.Basis = Basis.FromEuler(Coord.FromUnityEuler(new Vector3(0f, yawDeg, 0f)))
                    * Basis.FromEuler(Coord.FromUnityEuler(new Vector3(0f, 0f, 90f)));
                slot.node.Position = position;
                slot.node.Visible = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 떨어진 무기를 모두 치운다. 맵을 내릴 때 호출한다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                m_pool[i].active = false;
                m_pool[i].node.Visible = false;
            }
        }

        /// <summary>
        /// 떨어져 있는 무기 수를 센다.
        /// </summary>
        /// <returns>활성 무기 수.</returns>
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
        /// 풀 인덱스로 떨어진 무기의 상태를 조회한다. 점검 도구용.
        /// </summary>
        /// <param name="index">풀 인덱스.</param>
        /// <param name="weapon">무기.</param>
        /// <param name="position">논리 위치.</param>
        /// <param name="falling">낙하 중이면 true.</param>
        /// <returns>그 자리에 무기가 있으면 true.</returns>
        public bool TryGetDropped(int index, out Weapon weapon, out Vector3 position, out bool falling)
        {
            weapon = null;
            position = Vector3.Zero;
            falling = false;
            if (index < 0 || index >= PoolSize || !m_pool[index].active) return false;

            weapon = m_pool[index].weapon;
            position = m_pool[index].position;
            falling = m_pool[index].falling;
            return true;
        }

        /// <summary>
        /// 떨어지는 무기 한 틱. 수평 속도를 감쇠하며 블록 앞에서 멈추고, 아래로 떨어지다 바닥 바로 위에서 멈춘다.
        /// </summary>
        /// <param name="slot">풀 자리.</param>
        private static void TickFall(Slot slot)
        {
            WeaponDropPhysicsData physics = DataManager.Instance.WeaponParameterData.weaponDropPhysicsData;
            float dt = SimClock.FrameTime;

            // 수평 감쇠 — 원본 move_x/z *= 0.96 (프레임당). 데이터는 초당 배율이다.
            float damping = Mathf.Pow(physics.horizontalDampingPerSec, dt);
            slot.velocity.X *= damping;
            slot.velocity.Z *= damping;

            float horizontalSpeed = Mathf.Sqrt(slot.velocity.X * slot.velocity.X + slot.velocity.Z * slot.velocity.Z);
            if (horizontalSpeed < physics.horizontalStopThreshold)
            {
                slot.velocity.X = 0f;
                slot.velocity.Z = 0f;
                horizontalSpeed = 0f;
            }

            if (horizontalSpeed > 0f)
            {
                var direction = new Vector3(slot.velocity.X / horizontalSpeed, 0f, slot.velocity.Z / horizontalSpeed);
                float distance = horizontalSpeed * dt;
                if (MapLoader.RaycastBlock(slot.position, direction, distance + physics.groundCollisionMargin, out float hitDist))
                {
                    slot.position += direction * Mathf.Max(0f, hitDist - physics.groundCollisionMargin);
                    slot.velocity.X = 0f;
                    slot.velocity.Z = 0f;
                }
                else
                {
                    slot.position += direction * distance;
                }
            }

            slot.velocity.Y -= physics.gravity * dt;
            if (slot.velocity.Y < physics.terminalVelocityY) slot.velocity.Y = physics.terminalVelocityY;

            float moveY = slot.velocity.Y * dt;
            if (moveY < 0f && MapLoader.RaycastBlock(slot.position, Vector3.Down, -moveY + physics.groundCollisionMargin, out float groundDist))
            {
                slot.position += Vector3.Down * (groundDist - physics.groundCollisionMargin);
                slot.velocity = Vector3.Zero;
                slot.falling = false;
                return;
            }

            slot.position.Y += moveY;

            // 맵 밖으로 끝없이 떨어지지 않게 한계 높이에서 멈춘다.
            if (slot.position.Y < physics.deadlineY)
            {
                slot.position.Y = physics.deadlineY;
                slot.velocity = Vector3.Zero;
                slot.falling = false;
            }
        }

        /// <summary>
        /// 줍기 판정. 현재 슬롯이 맨손인 살아 있는 사람이 범위 안의 떨어진 무기를 줍는다.
        /// 원본 ObjectManager::PickupWeapon (objectmanager.cpp:1332-1371): 높이 범위 안이고 수평 거리가 반경 미만이면 줍는다.
        /// </summary>
        private void TickPickup()
        {
            HumanInteractionData interaction = DataManager.Instance.HumanParameterData.humanInteractionData;
            float radiusSq = interaction.weaponPickupRadius * interaction.weaponPickupRadius;
            float yMin = interaction.weaponPickupVerticalRange.min;
            float yMax = interaction.weaponPickupVerticalRange.max;

            IReadOnlyList<Human> humans = MapLoader.Humans;
            for (int h = 0; h < humans.Count; h++)
            {
                Human human = humans[h];
                if (!human.CanPickupWeapon) continue;

                Vector3 humanPosition = human.Controller.Position;
                for (int i = 0; i < PoolSize; i++)
                {
                    Slot slot = m_pool[i];
                    if (!slot.active) continue;

                    float dy = slot.position.Y - humanPosition.Y;
                    if (dy < yMin || dy > yMax) continue;

                    float dx = slot.position.X - humanPosition.X;
                    float dz = slot.position.Z - humanPosition.Z;
                    if (dx * dx + dz * dz >= radiusSq) continue;

                    human.PickupWeapon(slot.weapon.WeaponIndex, slot.weapon.Magazine, slot.weapon.Reserve);
                    slot.active = false;
                    slot.node.Visible = false;
                    break;
                }
            }
        }
    }
}
