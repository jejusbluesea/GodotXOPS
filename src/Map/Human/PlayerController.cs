using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 시점 종류.
    /// </summary>
    public enum ViewMode
    {
        FirstPerson = 0,
        ThirdPerson = 1,
    }

    /// <summary>
    /// 플레이어 조작과 카메라. MapLoader.Player 가 가리키는 Human 에 입력을 넣고, 1인칭/3인칭/사망 카메라를 배치한다.
    /// 메인게임 씬에 하나 둔다. 시점 각도는 UnityXOPS 규약(도, yaw 오른쪽 +, pitch 아래 +)으로 들고 Coord 로 변환한다.
    /// </summary>
    public partial class PlayerController : Node3D
    {
        // Human 의 시각 보간(Human.VisualProcessPriority) 뒤에 돌아 카메라가 보간된 위치를 읽게 한다.
        private const int k_processPriority = 100;

        private const float k_pitchLimit = 70f;
        private const float k_referenceFps = SimClock.FrameRate;

        // OpenXOPS gamemain.cpp:2651-2678 외부 3인칭 공식 상수 (원본 × 0.1)
        private const float k_thirdPersonPivotBack = 0.30f;
        private const float k_thirdPersonMaxDist = 1.40f; // VIEW_F1MODE_DIST
        private const float k_thirdPersonHeightBias = 0.25f;
        private const float k_thirdPersonWallMargin = 0.10f; // 원본 dist − 1.0
        private const float k_thirdPersonPivotHeight = 1.95f; // HUMAN_HEIGHT − 0.5
        private const float k_thirdPersonInitialPitch = 22.5f; // VIEW_F1MODE_ANGLE
        private const float k_thirdPersonNumpadDegPerFrame = 2f; // INPUT_F1NUMKEYS_ANGLE
        private const float k_thirdPersonSmoothBase = 0.8f; // 카메라각 8:2 관성

        // OpenXOPS gamemain.cpp:2633-2647 사망 카메라 상수
        private const float k_deathCamYawRate = 1f * k_referenceFps; // 1°/frame
        private const float k_deathCamPitchTarget = 89f;
        private const float k_deathCamPitchBlendBase = 0.95f;
        private const float k_deathCamRadius = 0.312f;
        private const float k_deathCamHeight = 3.33f;

        private Camera3D m_camera;
        private Human m_player;
        private HumanController m_controller;
        private ViewMode m_viewMode = ViewMode.FirstPerson;

        private float m_yaw;
        private float m_pitch;
        // 3인칭 카메라 시선 오프셋 (넘버패드 조정). 원본 view_rx/view_ry.
        private float m_viewYawOffset;
        private float m_viewPitchOffset;
        // 8:2 관성이 적용된 실제 카메라 궤도각. 원본 camera_rx/camera_ry.
        private float m_camYaw;
        private float m_camPitch;
        private float m_deathCamYaw;
        private float m_deathCamPitch;
        private bool m_deathCamInitialized;

        public Camera3D Camera => m_camera;
        public ViewMode ViewMode => m_viewMode;
        public float Yaw => m_yaw;
        public float Pitch => m_pitch;

        public override void _Ready()
        {
            ProcessPriority = k_processPriority;

            // 카메라는 매 프레임 월드 좌표로 직접 배치하므로 부모 transform 을 따르지 않게 한다.
            m_camera = new Camera3D { Name = "PlayerCamera", TopLevel = true };
            AddChild(m_camera);
            MapLoader.ApplyCameraSettings(m_camera);
            m_camera.MakeCurrent();
        }

        public override void _Process(double delta)
        {
            if (!TryAcquirePlayer()) return;

            float dt = (float)delta;
            InputManager input = InputManager.Instance;

            // 치트 F5 — F5+Enter 를 누르고 있는 동안 강제 상승 (원본 gamemain.cpp:2326-2332).
            m_controller.SetCheatRise(m_player.Alive && input.IsKeyPressed(Key.F5) && input.IsKeyPressed(Key.Enter));

            // 치트 F8 — F8 을 누른 채 ←/→ 로 조작 대상을 이전/다음 Human 으로 교체 (원본 gamemain.cpp:2367-2408). ← 가 인덱스 증가.
            if (input.IsKeyPressed(Key.F8))
            {
                if (input.WasKeyPressed(Key.Left) && CyclePlayer(1)) return;
                if (input.WasKeyPressed(Key.Right) && CyclePlayer(-1)) return;
            }

            if (m_player.Alive)
            {
                // F1 — 1인칭 ↔ 3인칭 (원본 gamemain.cpp:2293-2304).
                if (input.WasKeyPressed(Key.F1)) ToggleViewMode();

                ReadInput(input, dt);
            }

            UpdateCamera(dt);
        }

        /// <summary>
        /// 1인칭 ↔ 3인칭 시점을 토글한다. 3인칭 진입 시 시선 오프셋을 초기화하고(살짝 내려다봄) 관성 카메라각을 현재 시선에서 출발시킨다.
        /// </summary>
        public void ToggleViewMode()
        {
            m_viewMode = m_viewMode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson;
            m_viewYawOffset = 0f;
            m_viewPitchOffset = m_viewMode == ViewMode.ThirdPerson ? k_thirdPersonInitialPitch : 0f;
            m_camYaw = m_yaw;
            m_camPitch = m_pitch;
            ApplyViewpoint();
        }

        /// <summary>
        /// 이동·조준 입력을 읽어 컨트롤러에 넘긴다.
        /// </summary>
        /// <param name="input">입력 매니저.</param>
        /// <param name="dt">프레임 시간.</param>
        private void ReadInput(InputManager input, float dt)
        {
            ConfigManager config = ConfigManager.Instance;
            float sensitivity = config.MouseSensitivity;
            float invertY = config.InvertY ? -1f : 1f;

            // 시뮬레이션이 지난 프레임 동안 조준각에 가한 변화(반동)를 먼저 받아들이고, 그 위에 이번 프레임 입력을 얹는다.
            m_yaw = m_controller.Yaw;
            m_pitch = Mathf.Clamp(m_controller.Pitch, -k_pitchLimit, k_pitchLimit);

            Vector2 look = input.ReadVector(InputManager.Look);
            m_yaw += look.X * sensitivity;
            m_pitch = Mathf.Clamp(m_pitch - look.Y * sensitivity * invertY, -k_pitchLimit, k_pitchLimit);

            // 3인칭 카메라 시선 오프셋 — 넘버패드로 조정 (원본 gamemain.cpp:2307-2320, 프레임당 2°).
            if (m_viewMode == ViewMode.ThirdPerson)
            {
                float step = k_thirdPersonNumpadDegPerFrame * dt * k_referenceFps;
                if (input.IsKeyPressed(Key.Kp8)) m_viewPitchOffset += step;
                if (input.IsKeyPressed(Key.Kp5)) m_viewPitchOffset -= step;
                if (input.IsKeyPressed(Key.Kp4)) m_viewYawOffset -= step;
                if (input.IsKeyPressed(Key.Kp6)) m_viewYawOffset += step;
            }

            Vector2 move = input.ReadVector(InputManager.Move);
            HumanMoveFlag moveFlag = HumanMoveFlag.None;
            if (move.Y > 0f) moveFlag |= HumanMoveFlag.Forward;
            if (move.Y < 0f) moveFlag |= HumanMoveFlag.Back;
            if (move.X < 0f) moveFlag |= HumanMoveFlag.Left;
            if (move.X > 0f) moveFlag |= HumanMoveFlag.Right;
            if (input.IsPressed(InputManager.Walk)) moveFlag |= HumanMoveFlag.Walk;
            if (input.WasPressed(InputManager.Jump)) moveFlag |= HumanMoveFlag.Jump;

            var frameInput = new HumanInput { moveFlag = moveFlag, yaw = m_yaw, pitch = m_pitch };
            m_controller.SetInput(in frameInput);
        }

        /// <summary>
        /// 현재 상태(생존/시점)에 맞는 카메라를 배치한다.
        /// </summary>
        /// <param name="dt">프레임 시간.</param>
        private void UpdateCamera(float dt)
        {
            if (!m_player.Alive)
            {
                ApplyDeathCamera(dt);
                return;
            }

            if (m_viewMode == ViewMode.FirstPerson)
            {
                m_camera.Position = m_player.GlobalPosition + Vector3.Up * m_player.CameraHeight;
                m_camera.Rotation = Coord.FromUnityEuler(new Vector3(m_pitch, m_yaw, 0f));
                return;
            }

            ApplyThirdPersonCamera(dt);
        }

        /// <summary>
        /// OpenXOPS gamemain.cpp:2633-2647 사망 카메라. 플레이어 위쪽 일정 높이에서 작은 원을 그리며 돌고, 아래를 내려다보는 각으로 서서히 넘어간다.
        /// </summary>
        /// <param name="dt">프레임 시간.</param>
        private void ApplyDeathCamera(float dt)
        {
            if (!m_deathCamInitialized)
            {
                m_deathCamYaw = m_yaw;
                m_deathCamPitch = m_pitch;
                m_deathCamInitialized = true;
                // 사망 카메라는 바깥에서 보는 시점이므로 1인칭이었어도 몸통/다리를 보이게 한다.
                m_player.HumanVisual.SetBodyVisible(true);
            }

            m_deathCamYaw += k_deathCamYawRate * dt;
            float blend = 1f - Mathf.Pow(k_deathCamPitchBlendBase, dt * k_referenceFps);
            m_deathCamPitch = Mathf.Lerp(m_deathCamPitch, k_deathCamPitchTarget, blend);

            m_camera.Position = m_player.GlobalPosition
                + Coord.YawForward(m_deathCamYaw) * k_deathCamRadius
                + Vector3.Up * k_deathCamHeight;
            m_camera.Rotation = Coord.FromUnityEuler(new Vector3(m_deathCamPitch, m_deathCamYaw, 0f));
        }

        /// <summary>
        /// OpenXOPS gamemain.cpp:2651-2678 외부 3인칭 공식 (정중앙 뒤, 어깨 오프셋 없음).
        /// 시선 + 넘버패드 오프셋을 목표각으로 8:2 관성 접근한 뒤, 주시점을 잡고 그 뒤로 시선 반대 방향에 카메라를 둔다.
        /// 주시점과 카메라 사이에 블록이 있으면 그 앞까지만 물러난다.
        /// </summary>
        /// <param name="dt">프레임 시간.</param>
        private void ApplyThirdPersonCamera(float dt)
        {
            float smooth = 1f - Mathf.Pow(k_thirdPersonSmoothBase, dt * k_referenceFps);
            m_camYaw = Coord.LerpAngle(m_camYaw, m_yaw + m_viewYawOffset, smooth);
            m_camPitch = Coord.LerpAngle(m_camPitch, m_pitch + m_viewPitchOffset, smooth);

            Vector3 look = Coord.AimDirection(m_camYaw, m_camPitch);
            float pitchRad = Mathf.DegToRad(m_camPitch);

            // 원본 주시점 — 수평은 시선 방향으로 sin(pitch)×0.3, 수직은 1.95 + cos(pitch)×0.25.
            Vector3 focus = m_player.GlobalPosition
                + Coord.YawForward(m_camYaw) * (Mathf.Sin(pitchRad) * k_thirdPersonPivotBack)
                + Vector3.Up * (k_thirdPersonPivotHeight + Mathf.Cos(pitchRad) * k_thirdPersonHeightBias);

            float dist = k_thirdPersonMaxDist;
            if (MapLoader.RaycastBlock(focus, -look, k_thirdPersonMaxDist, out float hitDist))
            {
                dist = Mathf.Max(0f, hitDist - k_thirdPersonWallMargin);
            }

            m_camera.Position = focus - look * dist;
            m_camera.Rotation = Coord.FromUnityEuler(new Vector3(m_camPitch, m_camYaw, 0f));
        }

        /// <summary>
        /// 시점에 맞춰 플레이어 몸통/다리 표시를 정한다. 1인칭에서는 숨긴다.
        /// </summary>
        private void ApplyViewpoint()
        {
            m_player?.HumanVisual.SetBodyVisible(m_viewMode != ViewMode.FirstPerson);
        }

        /// <summary>
        /// 치트(F8) — 조작 대상을 Humans 목록에서 이전/다음으로 순환 교체한다.
        /// </summary>
        /// <param name="direction">-1 = 이전, +1 = 다음.</param>
        /// <returns>실제로 교체됐으면 true.</returns>
        private bool CyclePlayer(int direction)
        {
            int count = MapLoader.HumanCount;
            int index = MapLoader.PlayerIndex;
            if (count == 0 || index < 0) return false;

            int next = ((index + direction) % count + count) % count;
            if (next == index) return false;

            // 옛 플레이어는 바깥에서 보이는 대상이 되므로 1인칭 때 숨겼던 몸통/다리를 다시 보이게 하고 상승 치트를 푼다.
            m_player.HumanVisual.SetBodyVisible(true);
            m_controller.SetCheatRise(false);

            MapLoader.SetPlayer(MapLoader.GetHuman(next));
            return true;
        }

        /// <summary>
        /// MapLoader.Player 가 바뀌었으면 새 Human 의 조작권을 얻고 시선을 그 Human 의 방향으로 맞춘다.
        /// </summary>
        /// <returns>조작할 플레이어가 있으면 true.</returns>
        private bool TryAcquirePlayer()
        {
            Human player = MapLoader.Player;
            if (player == null || !IsInstanceValid(player))
            {
                m_player = null;
                m_controller = null;
                return false;
            }

            if (player != m_player)
            {
                m_player = player;
                m_controller = player.Controller;
                m_yaw = m_controller.Yaw;
                // 초기 시선 pitch — 정면(0)이 아니라 팔이 쉬는 각(살짝 아래)에서 시작한다.
                m_pitch = -DataManager.Instance.HumanParameterData.humanGeneralData.armAngleInitial;
                m_camYaw = m_yaw;
                m_camPitch = m_pitch;
                m_viewYawOffset = 0f;
                m_viewPitchOffset = m_viewMode == ViewMode.ThirdPerson ? k_thirdPersonInitialPitch : 0f;
                m_deathCamInitialized = false;

                var initial = new HumanInput { moveFlag = HumanMoveFlag.None, yaw = m_yaw, pitch = m_pitch };
                m_controller.SetInput(in initial);
                ApplyViewpoint();
            }

            return true;
        }
    }
}
