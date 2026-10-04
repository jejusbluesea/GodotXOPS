using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// HUD 에 띄우는 3D 무기 표시. 원본 OpenXOPS gamemain.cpp:3259-3279 — 든 무기는 크게 놓고 돌리고, 멘 무기는 작게 고정한다.
    /// 자기만의 3D 공간을 가진 뷰포트에 무기 모델 두 개와 카메라를 두고, 그 결과 텍스처를 HUD 가 화면에 놓는다. 배경은 투명이다.
    /// 무기 자리의 위치·크기·회전과 카메라 구도는 HUD 쪽이 정하고, 이 노드는 플레이어의 무기가 바뀌면 모델을 다시 만든다.
    /// 좌표는 UnityXOPS 공간으로 받는다.
    /// </summary>
    public partial class HudWeaponView : SubViewport
    {
        // 무기 모델은 맵과 같은 안개 셰이더를 쓴다. 카메라와의 거리가 멀면 안개가 끼므로 공간 전체를 이만큼 줄여 카메라 바로 앞에 둔다.
        private const float k_worldScale = 0.01f;
        private const float k_cameraNear = 0.001f;
        private const float k_cameraFar = 10f;

        private Node3D m_root;
        private Node3D m_mainSlot;
        private Node3D m_subSlot;
        private Node3D m_mainModel;
        private Node3D m_subModel;
        private Camera3D m_camera;

        private Weapon m_builtMain;
        private Weapon m_builtSub;
        // 같은 슬롯의 무기가 종류만 바뀌는 경우(치트, 종류 전환, 줍기)를 알아채기 위해 무기 번호도 기억한다.
        private int m_builtMainIndex = -1;
        private int m_builtSubIndex = -1;

        public override void _Ready()
        {
            OwnWorld3D = true;
            TransparentBg = true;
            RenderTargetUpdateMode = UpdateMode.Always;

            m_root = new Node3D { Name = "Root", Scale = Vector3.One * k_worldScale };
            AddChild(m_root);

            m_mainSlot = new Node3D { Name = "Main" };
            m_subSlot = new Node3D { Name = "Sub" };
            m_root.AddChild(m_mainSlot);
            m_root.AddChild(m_subSlot);

            m_camera = new Camera3D { Name = "Camera", Near = k_cameraNear, Far = k_cameraFar, Current = true };
            AddChild(m_camera);
        }

        public override void _Process(double delta)
        {
            // 조작 대상은 매 프레임 새로 읽는다 (치트 F8 로 바뀔 수 있다).
            Human player = MapLoader.Player;
            if (player == null || !IsInstanceValid(player)) return;

            int selected = player.SelectWeapon;
            UpdateSlot(ref m_mainModel, ref m_builtMain, ref m_builtMainIndex, m_mainSlot, player.GetWeapon(selected));
            UpdateSlot(ref m_subModel, ref m_builtSub, ref m_builtSubIndex, m_subSlot, player.GetWeapon(1 - selected));
        }

        /// <summary>
        /// 든 무기 자리를 놓는다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="scale">크기 배율.</param>
        /// <param name="yawDeg">Y 축 회전 (도).</param>
        public void SetMainSlot(Vector3 position, float scale, float yawDeg)
        {
            ApplySlot(m_mainSlot, position, scale, yawDeg);
        }

        /// <summary>
        /// 멘 무기 자리를 놓는다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="scale">크기 배율.</param>
        /// <param name="yawDeg">Y 축 회전 (도).</param>
        public void SetSubSlot(Vector3 position, float scale, float yawDeg)
        {
            ApplySlot(m_subSlot, position, scale, yawDeg);
        }

        /// <summary>
        /// 무기를 비추는 카메라의 구도를 정한다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="euler">회전 (UnityXOPS 오일러 각, 도).</param>
        /// <param name="fov">세로 시야각 (도).</param>
        public void SetViewCamera(Vector3 position, Vector3 euler, float fov)
        {
            m_camera.Position = Coord.FromUnity(position) * k_worldScale;
            m_camera.Rotation = Coord.FromUnityEuler(euler);
            m_camera.Fov = fov;
        }

        private static void ApplySlot(Node3D slot, Vector3 position, float scale, float yawDeg)
        {
            slot.Position = Coord.FromUnity(position);
            slot.Scale = Vector3.One * scale;
            slot.Rotation = Coord.FromUnityEuler(new Vector3(0f, yawDeg, 0f));
        }

        /// <summary>
        /// 자리의 모델을 지금 무기에 맞춘다. 무기 인스턴스나 종류가 바뀌었을 때만 다시 만든다.
        /// </summary>
        /// <param name="model">자리에 놓인 모델.</param>
        /// <param name="built">마지막으로 만든 무기.</param>
        /// <param name="builtIndex">마지막으로 만든 무기의 번호.</param>
        /// <param name="slot">모델을 놓을 자리.</param>
        /// <param name="weapon">지금 무기.</param>
        private static void UpdateSlot(ref Node3D model, ref Weapon built, ref int builtIndex, Node3D slot, Weapon weapon)
        {
            int index = weapon != null ? weapon.WeaponIndex : -1;
            if (ReferenceEquals(weapon, built) && index == builtIndex) return;

            built = weapon;
            builtIndex = index;

            if (model != null)
            {
                slot.RemoveChild(model);
                model.Free();
                model = null;
            }
            if (weapon == null || weapon.ModelData == null) return;

            model = new Node3D { Name = "Model", Scale = Vector3.One * weapon.Data.size };
            slot.AddChild(model);
            WeaponVisual.BuildModelParts(model, weapon.ModelData);
        }
    }
}
