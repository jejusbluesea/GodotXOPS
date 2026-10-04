using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// 탄환 풀과 그 모델 노드를 관리하는 싱글톤. 탄도·판정은 SimClock 틱(SimTick)에서 돌리고,
    /// 모델은 매 렌더 프레임 직전 틱과 현재 틱 사이를 보간해 놓는다.
    /// 풀 크기는 원본 MAX_BULLET(128) + MAX_GRENADE(32) 이다. 한 틱 안에서는 원본처럼 직선 탄을 먼저, 수류탄을 나중에 처리한다.
    /// </summary>
    public partial class BulletManager : Singleton<BulletManager>, ISimTickable
    {
        public const int PoolSize = 160;

        private readonly Bullet[] m_pool = new Bullet[PoolSize];
        private readonly Node3D[] m_nodes = new Node3D[PoolSize];
        private readonly MeshInstance3D[] m_meshes = new MeshInstance3D[PoolSize];
        // 총구에서 충분히 멀어져 모델을 보이기 시작했는지.
        private readonly bool[] m_shown = new bool[PoolSize];

        // 점검 도구용 누계.
        public static int SpawnCount { get; private set; }
        public static int ExplosionCount { get; private set; }
        public static Vector3 LastExplosionPosition { get; private set; }

        // 원본 총알 처리 — 사람 이동(10) 뒤, 인간간 충돌(100) 앞.
        public int SimOrder => 40;

        public override void _Ready()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                m_pool[i] = new Bullet();

                m_nodes[i] = new Node3D { Name = $"Bullet_{i}", Visible = false };
                AddChild(m_nodes[i]);
                m_meshes[i] = new MeshInstance3D { Name = "Visual" };
                m_nodes[i].AddChild(m_meshes[i]);
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
                Bullet bullet = m_pool[i];
                if (!bullet.IsActive)
                {
                    if (m_nodes[i].Visible) m_nodes[i].Visible = false;
                    continue;
                }

                Vector3 position = bullet.VisualPosition(alpha);
                m_nodes[i].Position = position;

                // 탄환은 눈높이에서 나가므로 그대로 그리면 사수의 머리와 총을 뚫고 나오는 것처럼 보인다.
                // 그래서 보간된 위치가 총구에서 탄환 모델 길이(bulletBoundAdjust)만큼 멀어진 뒤부터 보인다. 판정에는 영향이 없다.
                if (!m_shown[i])
                {
                    float bound = bullet.Data.bulletBoundAdjust;
                    if ((position - bullet.VisualOrigin).LengthSquared() >= bound * bound) m_shown[i] = true;
                }
                m_nodes[i].Visible = m_shown[i];
            }
        }

        public void SimTick()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (m_pool[i].IsActive && !m_pool[i].UseGravity) m_pool[i].Tick();
            }
            for (int i = 0; i < PoolSize; i++)
            {
                if (m_pool[i].IsActive && m_pool[i].UseGravity) m_pool[i].Tick();
            }
        }

        /// <summary>
        /// 풀에서 빈 자리를 찾아 탄환을 발사 상태로 만든다. 빈 자리는 앞에서부터 찾는다 (원본 GetNewBulletObject).
        /// </summary>
        /// <param name="data">탄환 데이터.</param>
        /// <param name="owner">쏜 사람.</param>
        /// <param name="team">쏜 사람의 팀.</param>
        /// <param name="attacks">위력.</param>
        /// <param name="penetration">관통력.</param>
        /// <param name="position">발사 위치.</param>
        /// <param name="yawDeg">발사 yaw (도).</param>
        /// <param name="pitchDeg">발사 pitch (도, 아래 +).</param>
        /// <param name="speedPerTick">틱당 이동 거리 (m).</param>
        /// <param name="visualOrigin">총구 위치.</param>
        /// <returns>발사된 탄환. 풀이 가득 찼으면 null.</returns>
        public Bullet Spawn(BulletData data, Human owner, int team, int attacks, int penetration,
            Vector3 position, float yawDeg, float pitchDeg, float speedPerTick, Vector3 visualOrigin)
        {
            if (data == null) return null;

            for (int i = 0; i < PoolSize; i++)
            {
                Bullet bullet = m_pool[i];
                if (bullet.IsActive) continue;

                bullet.Spawn(data, owner, team, attacks, penetration, position, yawDeg, pitchDeg, speedPerTick, visualOrigin);
                ApplyVisual(i, data, bullet);
                SpawnCount++;
                return bullet;
            }
            return null;
        }

        /// <summary>
        /// 날아가는 탄환을 모두 없앤다. 맵을 내릴 때 호출해 이전 미션의 탄환이 남지 않게 한다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                m_pool[i].Deactivate();
                m_nodes[i].Visible = false;
            }
        }

        /// <summary>
        /// 풀 인덱스로 탄환을 조회한다. 점검 도구가 탄환 상태를 읽을 때 쓴다.
        /// </summary>
        /// <param name="index">풀 인덱스.</param>
        /// <returns>그 자리의 탄환. 범위 밖이면 null.</returns>
        public Bullet GetBullet(int index)
        {
            return index >= 0 && index < PoolSize ? m_pool[index] : null;
        }

        /// <summary>
        /// 날아가는 탄환 수를 센다.
        /// </summary>
        /// <returns>활성 탄환 수.</returns>
        public int CountActive()
        {
            int count = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (m_pool[i].IsActive) count++;
            }
            return count;
        }

        /// <summary>
        /// 폭발이 일어났음을 기록한다. Bullet 이 호출한다.
        /// </summary>
        /// <param name="position">폭발 위치.</param>
        public static void NotifyExplosion(Vector3 position)
        {
            ExplosionCount++;
            LastExplosionPosition = position;
        }

        /// <summary>
        /// 풀 자리의 모델 노드를 탄환 데이터에 맞게 설정한다. 방향은 발사 방향으로 고정한다.
        /// </summary>
        /// <param name="index">풀 인덱스.</param>
        /// <param name="data">탄환 데이터.</param>
        /// <param name="bullet">발사된 탄환.</param>
        private void ApplyVisual(int index, BulletData data, Bullet bullet)
        {
            Node3D node = m_nodes[index];
            MeshInstance3D mesh = m_meshes[index];

            string meshPath = string.IsNullOrEmpty(data.modelPath) ? null : GamePath.Resolve(data.modelPath);
            mesh.Mesh = meshPath != null ? ModelLoader.LoadMesh(meshPath) : null;
            mesh.MaterialOverride = MapLoader.GetEntityMaterial(data.texturePath);
            mesh.Position = Coord.FromUnity(data.modelPosition);
            mesh.Rotation = Coord.FromUnityEuler(data.modelRotation);
            mesh.Scale = data.modelScale;

            Vector3 direction = bullet.Direction;
            // 거의 수직이면 위쪽 기준을 바꿔 준다.
            Vector3 up = Mathf.Abs(direction.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
            node.Basis = Basis.LookingAt(direction, up);
            node.Position = bullet.Position;

            m_shown[index] = data.bulletBoundAdjust <= 0f;
            node.Visible = m_shown[index];
        }
    }
}
