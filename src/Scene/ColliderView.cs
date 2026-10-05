using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 판정 범위를 선으로 그려 보여 주는 디버그 표시 (디버그 콘솔의 collider).
    /// 사람의 총알 판정 원기둥은 초록, 떨어진 무기의 줍기 범위는 빨강, 소물의 판정 형상은 파랑이다.
    /// 판정에 쓰는 것과 같은 논리 위치와 데이터로 매 프레임 다시 그린다. 깊이 테스트를 꺼서 벽과 모델에 가려지지 않는다.
    /// </summary>
    public partial class ColliderView : MeshInstance3D
    {
        // 원 하나를 나누는 선분 수, 원기둥·캡슐의 세로선을 긋는 간격(선분 수 단위).
        private const int k_segments = 16;
        private const int k_sideLineStep = 4;

        private static readonly Color s_humanColor = new Color(0f, 1f, 0f);
        private static readonly Color s_weaponColor = new Color(1f, 0f, 0f);
        private static readonly Color s_objectColor = new Color(0f, 0.4f, 1f);

        private ImmediateMesh m_lines;
        private bool m_surfaceOpen;

        // 사람의 총알 판정 원기둥(머리·상반신·다리)을 그릴지.
        public bool ShowHuman { get; set; }
        // 떨어진 무기의 줍기 범위를 그릴지.
        public bool ShowWeapon { get; set; }
        // 소물의 판정 형상을 그릴지.
        public bool ShowObject { get; set; }

        public override void _Ready()
        {
            // 논리 위치(월드 좌표)로 그리므로 부모의 transform 을 따르지 않게 한다.
            TopLevel = true;
            m_lines = new ImmediateMesh();
            Mesh = m_lines;
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
            };
        }

        public override void _Process(double delta)
        {
            m_lines.ClearSurfaces();
            m_surfaceOpen = false;
            if (!MapLoader.Loaded || (!ShowHuman && !ShowWeapon && !ShowObject)) return;

            if (ShowHuman) DrawHumans();
            if (ShowWeapon) DrawWeaponPickupRanges();
            if (ShowObject) DrawObjects();

            if (m_surfaceOpen) m_lines.SurfaceEnd();
        }

        /// <summary>
        /// 살아 있는 사람의 총알 판정 원기둥을 그린다. 부위의 위치와 회전은 HumanHitbox.Contains 와 같은 식으로 구한다.
        /// </summary>
        private void DrawHumans()
        {
            // 1인칭에서는 자기 원기둥이 화면을 가리므로 그리지 않는다.
            PlayerController controller = PlayerController.Current;
            Human hidden = controller != null && controller.ViewMode == ViewMode.FirstPerson ? MapLoader.Player : null;

            foreach (Human human in MapLoader.Humans)
            {
                if (!human.Alive || human.HitboxSize == null || human == hidden) continue;

                Vector3 position = human.Controller.Position;
                Basis body = Basis.FromEuler(Coord.FromUnityEuler(new Vector3(0f, human.Controller.Yaw, 0f)));
                DrawHitboxPart(human.HitboxSize.head, position, body);
                DrawHitboxPart(human.HitboxSize.body, position, body);
                DrawHitboxPart(human.HitboxSize.leg, position, body);
            }
        }

        /// <summary>
        /// 사람의 판정 부위 하나를 그린다.
        /// </summary>
        /// <param name="part">부위 크기 데이터. null 이면 그리지 않는다.</param>
        /// <param name="humanPosition">사람의 논리 위치 (발 기준).</param>
        /// <param name="body">사람의 몸 방향.</param>
        private void DrawHitboxPart(HitboxPartSizeData part, Vector3 humanPosition, Basis body)
        {
            if (part == null) return;

            Basis rotation = body * Basis.FromEuler(Coord.FromUnityEuler(part.rotationEuler));
            Vector3 center = humanPosition + body * Coord.FromUnity(part.position);
            DrawCylinder(center, rotation, part.radius, -part.height * 0.5f, part.height * 0.5f, s_humanColor);
        }

        /// <summary>
        /// 떨어진 무기마다 줍기 범위를 그린다. 사람의 발 위치가 이 원기둥 안에 들어오면 줍는다 (WeaponManager.TickPickup).
        /// 판정은 "무기 높이 − 사람 높이"가 범위 안인지 보므로, 사람 쪽에서 보면 무기 높이에서 그 범위를 뺀 구간이다.
        /// </summary>
        private void DrawWeaponPickupRanges()
        {
            HumanInteractionData interaction = DataManager.Instance.HumanParameterData.humanInteractionData;
            float radius = interaction.weaponPickupRadius;
            float bottom = -interaction.weaponPickupVerticalRange.max;
            float top = -interaction.weaponPickupVerticalRange.min;

            WeaponManager weapons = WeaponManager.Instance;
            for (int i = 0; i < WeaponManager.PoolSize; i++)
            {
                if (!weapons.TryGetDropped(i, out _, out Vector3 position, out _)) continue;

                DrawCylinder(position, Basis.Identity, radius, bottom, top, s_weaponColor);
            }
        }

        /// <summary>
        /// 부서지지 않은 소물의 판정 형상(구, 박스, 캡슐)을 전부 그린다. 형상의 자리는 SmallObject.Contains 와 같은 식으로 구한다.
        /// </summary>
        private void DrawObjects()
        {
            foreach (SmallObject smallObject in MapLoader.SmallObjects)
            {
                ObjectColliderData collider = smallObject.ColliderData;
                if (smallObject.IsDestroyed || collider == null) continue;

                Basis basis = smallObject.ColliderBasis;
                for (int i = 0; i < collider.shapes.Count; i++)
                {
                    ColliderShape shape = collider.shapes[i];
                    Vector3 center = smallObject.LogicPosition + basis * Coord.FromUnity(shape.center);
                    switch (shape.type)
                    {
                        case ColliderShapeType.Sphere:
                            DrawSphere(center, basis, shape.size.X, s_objectColor);
                            break;

                        case ColliderShapeType.Box:
                            DrawBox(center, basis, shape.size * 0.5f, s_objectColor);
                            break;

                        case ColliderShapeType.Capsule:
                            DrawCapsule(center, basis, shape, s_objectColor);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 원기둥을 그린다. 축은 basis 의 Y 다.
        /// </summary>
        /// <param name="center">기준점.</param>
        /// <param name="basis">방향.</param>
        /// <param name="radius">반지름.</param>
        /// <param name="bottom">기준점에서 밑면까지의 축 방향 거리.</param>
        /// <param name="top">기준점에서 윗면까지의 축 방향 거리.</param>
        /// <param name="color">선 색.</param>
        private void DrawCylinder(Vector3 center, Basis basis, float radius, float bottom, float top, Color color)
        {
            for (int i = 0; i < k_segments; i++)
            {
                Vector3 r0 = RingPoint(i, radius, 0, 2);
                Vector3 r1 = RingPoint(i + 1, radius, 0, 2);
                Vector3 low = Vector3.Up * bottom;
                Vector3 high = Vector3.Up * top;

                AddLine(center + basis * (r0 + low), center + basis * (r1 + low), color);
                AddLine(center + basis * (r0 + high), center + basis * (r1 + high), color);
                if (i % k_sideLineStep == 0) AddLine(center + basis * (r0 + low), center + basis * (r0 + high), color);
            }
        }

        /// <summary>
        /// 구를 서로 수직인 원 세 개로 그린다.
        /// </summary>
        /// <param name="center">중심.</param>
        /// <param name="basis">방향.</param>
        /// <param name="radius">반지름.</param>
        /// <param name="color">선 색.</param>
        private void DrawSphere(Vector3 center, Basis basis, float radius, Color color)
        {
            for (int i = 0; i < k_segments; i++)
            {
                AddLine(center + basis * RingPoint(i, radius, 0, 2), center + basis * RingPoint(i + 1, radius, 0, 2), color);
                AddLine(center + basis * RingPoint(i, radius, 0, 1), center + basis * RingPoint(i + 1, radius, 0, 1), color);
                AddLine(center + basis * RingPoint(i, radius, 1, 2), center + basis * RingPoint(i + 1, radius, 1, 2), color);
            }
        }

        /// <summary>
        /// 박스의 모서리 12개를 그린다.
        /// </summary>
        /// <param name="center">중심.</param>
        /// <param name="basis">방향.</param>
        /// <param name="half">각 축의 절반 크기.</param>
        /// <param name="color">선 색.</param>
        private void DrawBox(Vector3 center, Basis basis, Vector3 half, Color color)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 from = Vector3.Zero;
                    from[u] = (corner & 1) == 0 ? -half[u] : half[u];
                    from[v] = (corner & 2) == 0 ? -half[v] : half[v];
                    Vector3 to = from;
                    from[axis] = -half[axis];
                    to[axis] = half[axis];
                    AddLine(center + basis * from, center + basis * to, color);
                }
            }
        }

        /// <summary>
        /// 캡슐을 그린다: 양 끝의 구와 그 사이를 잇는 세로선. 크기 해석은 SmallObject 의 판정과 같다 (x 반지름, y 전체 높이, z 축 번호).
        /// </summary>
        /// <param name="center">중심.</param>
        /// <param name="basis">방향.</param>
        /// <param name="shape">형상 데이터.</param>
        /// <param name="color">선 색.</param>
        private void DrawCapsule(Vector3 center, Basis basis, ColliderShape shape, Color color)
        {
            float radius = shape.size.X;
            float halfSegment = Mathf.Max(0f, shape.size.Y * 0.5f - radius);
            int axis = Mathf.Clamp((int)shape.size.Z, 0, 2);
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3 offset = Vector3.Zero;
            offset[axis] = halfSegment;
            DrawSphere(center + basis * offset, basis, radius, color);
            DrawSphere(center - basis * offset, basis, radius, color);

            for (int i = 0; i < k_segments; i += k_sideLineStep)
            {
                Vector3 ring = RingPoint(i, radius, u, v);
                AddLine(center + basis * (ring + offset), center + basis * (ring - offset), color);
            }
        }

        /// <summary>
        /// 원 둘레의 점 하나를 구한다.
        /// </summary>
        /// <param name="index">선분 번호 (k_segments 가 한 바퀴).</param>
        /// <param name="radius">반지름.</param>
        /// <param name="u">원이 놓인 평면의 첫 번째 축 번호.</param>
        /// <param name="v">원이 놓인 평면의 두 번째 축 번호.</param>
        /// <returns>원의 중심을 원점으로 한 점.</returns>
        private static Vector3 RingPoint(int index, float radius, int u, int v)
        {
            float angle = Mathf.Tau * index / k_segments;
            Vector3 point = Vector3.Zero;
            point[u] = Mathf.Cos(angle) * radius;
            point[v] = Mathf.Sin(angle) * radius;
            return point;
        }

        private void AddLine(Vector3 from, Vector3 to, Color color)
        {
            if (!m_surfaceOpen)
            {
                m_lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
                m_surfaceOpen = true;
            }

            m_lines.SurfaceSetColor(color);
            m_lines.SurfaceAddVertex(from);
            m_lines.SurfaceSetColor(color);
            m_lines.SurfaceAddVertex(to);
        }
    }
}
