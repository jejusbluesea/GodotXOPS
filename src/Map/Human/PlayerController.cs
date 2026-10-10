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
    /// 플레이어 조작과 카메라. MapLoader.Player 가 가리키는 Human 에 이동·조준·무기 입력을 넣고, 1인칭/3인칭/사망 카메라를 배치한다.
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
        // 조작권을 얻은 뒤 발사 버튼을 한 번 떼야 발사를 받는다. 이전 화면을 닫은 클릭이 첫 발사로 새는 것을 막는다.
        private bool m_fireReady;
        // 스코프를 쓰지 않을 때의 시야각 (설정값).
        private float m_baseFov;
        // 지난 프레임에 카메라가 플레이어에게서 떨어져 있었는지 (이벤트 Detach Camera).
        private bool m_cameraDetached;

        // 지금 씬에서 조작을 맡은 컨트롤러. 없으면 null.
        public static PlayerController Current { get; private set; }

        public Camera3D Camera => m_camera;
        public ViewMode ViewMode => m_viewMode;
        // 카메라가 플레이어의 눈에 있는지. 3인칭이거나 이벤트가 카메라를 뗐으면 false 다.
        public bool FirstPersonView => m_viewMode == ViewMode.FirstPerson && !m_cameraDetached;
        public float Yaw => m_yaw;
        public float Pitch => m_pitch;

        public override void _Ready()
        {
            ProcessPriority = k_processPriority;

            // 카메라는 매 프레임 월드 좌표로 직접 배치하므로 부모 transform 을 따르지 않게 한다.
            m_camera = new Camera3D { Name = "PlayerCamera", TopLevel = true };
            AddChild(m_camera);
            MapLoader.ApplyCameraSettings(m_camera);
            m_baseFov = m_camera.Fov;
            m_camera.MakeCurrent();
        }

        public override void _EnterTree()
        {
            Current = this;
        }

        public override void _ExitTree()
        {
            if (Current == this) Current = null;
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

            // 치트 F9 — F9 를 누른 채 ↑ 로 따라오는 복제, ↓ 로 제자리를 지키는 복제를 만든다 (원본 gamemain.cpp:2411-2455).
            if (input.IsKeyPressed(Key.F9))
            {
                if (input.WasKeyPressed(Key.Up)) SpawnClone(true);
                else if (input.WasKeyPressed(Key.Down)) SpawnClone(false);
            }

            // 이벤트의 조작 잠금. 플레이어가 이벤트의 지시(AI Look At / AI Fire At)를 받는 동안에는 전부 잠긴 것으로 친다.
            int locks = EventManager.Loaded ? EventManager.Instance.PlayerLock : 0;
            if (m_player.Brain != null && m_player.Brain.Directed) locks = EventManager.LockAll;

            if (m_player.Alive)
            {
                // F1 — 1인칭 ↔ 3인칭 (원본 gamemain.cpp:2293-2304).
                if (input.WasKeyPressed(Key.F1) && (locks & EventManager.LockView) == 0) ToggleViewMode();

                // 치트 F6 — F6 을 누른 채 Enter 로 현재 무기의 예비 탄을 장탄수만큼 추가 (원본 gamemain.cpp:2336-2341).
                if (input.IsKeyPressed(Key.F6) && input.WasKeyPressed(Key.Enter)) m_player.CheatAddMagazine();

                // 치트 F7 — F7 을 누른 채 ←/→ 로 현재 무기를 다음/이전 종류로 교체 (원본 gamemain.cpp:2344-2363). ← 가 번호 증가.
                if (input.IsKeyPressed(Key.F7))
                {
                    if (input.WasKeyPressed(Key.Left)) m_player.CheatCycleWeapon(1);
                    else if (input.WasKeyPressed(Key.Right)) m_player.CheatCycleWeapon(-1);
                }

                ReadInput(input, dt, locks);
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
        /// <param name="locks">이벤트가 잠근 조작 (EventManager.Lock* 비트의 합).</param>
        private void ReadInput(InputManager input, float dt, int locks)
        {
            ConfigManager config = ConfigManager.Instance;
            float sensitivity = config.MouseSensitivity;
            float invertY = config.InvertY ? -1f : 1f;

            // 시뮬레이션이 지난 프레임 동안 조준각에 가한 변화(반동)를 먼저 받아들이고, 그 위에 이번 프레임 입력을 얹는다.
            m_yaw = m_controller.Yaw;
            m_pitch = Mathf.Clamp(m_controller.Pitch, -k_pitchLimit, k_pitchLimit);

            // Interact 는 사람이 아니라 미션 이벤트가 받는다. 다음 틱의 이벤트가 소비한다. 조작 잠금으로 막지 않는다.
            if (input.WasPressed(InputManager.Interact) && EventManager.Loaded) EventManager.Instance.QueueInteract();

            // 전부 잠겼으면 사람에게 아무 입력도 넣지 않는다. 게임 정지 중에는 틱이 입력을 소비하지 않고, 지시를 받는 중이면 AI 가 넣은 입력을 덮어쓰면 안 된다.
            if (locks == EventManager.LockAll)
            {
                m_fireReady = false;
                return;
            }

            bool lookLocked = (locks & EventManager.LockLook) != 0;
            if (!lookLocked)
            {
                Vector2 look = input.ReadVector(InputManager.Look);
                m_yaw += look.X * sensitivity;
                m_pitch = Mathf.Clamp(m_pitch - look.Y * sensitivity * invertY, -k_pitchLimit, k_pitchLimit);
            }

            // 3인칭 카메라 시선 오프셋 — 넘버패드로 조정 (원본 gamemain.cpp:2307-2320, 프레임당 2°).
            if (m_viewMode == ViewMode.ThirdPerson && !lookLocked)
            {
                float step = k_thirdPersonNumpadDegPerFrame * dt * k_referenceFps;
                if (input.IsKeyPressed(Key.Kp8)) m_viewPitchOffset += step;
                if (input.IsKeyPressed(Key.Kp5)) m_viewPitchOffset -= step;
                if (input.IsKeyPressed(Key.Kp4)) m_viewYawOffset -= step;
                if (input.IsKeyPressed(Key.Kp6)) m_viewYawOffset += step;
            }

            HumanMoveFlag moveFlag = HumanMoveFlag.None;
            if ((locks & EventManager.LockMove) == 0)
            {
                Vector2 move = input.ReadVector(InputManager.Move);
                if (move.Y > 0f) moveFlag |= HumanMoveFlag.Forward;
                if (move.Y < 0f) moveFlag |= HumanMoveFlag.Back;
                if (move.X < 0f) moveFlag |= HumanMoveFlag.Left;
                if (move.X > 0f) moveFlag |= HumanMoveFlag.Right;
                if (input.IsPressed(InputManager.Walk)) moveFlag |= HumanMoveFlag.Walk;
            }
            if (input.WasPressed(InputManager.Jump) && (locks & EventManager.LockJump) == 0) moveFlag |= HumanMoveFlag.Jump;

            HumanWeaponAction weapon = ReadWeaponInput(input, locks);

            var frameInput = new HumanInput { moveFlag = moveFlag, yaw = m_yaw, pitch = m_pitch, weapon = weapon };
            m_controller.SetInput(in frameInput);
            m_player.QueueWeaponInput(weapon);

            // 사람 노드의 회전과 팔 각도는 이 노드보다 먼저(Human 의 시각 갱신에서) 정해져서, 그대로 두면 이번 프레임의 마우스 입력이 다음 프레임에야 팔에 반영된다.
            // 카메라는 바로 돌기 때문에 1인칭에서 팔이 시선을 한 박자 늦게 따라오는 것처럼 보인다. 새 조준각으로 한 번 더 맞춘다.
            m_controller.ApplyVisual();
        }

        /// <summary>
        /// 무기 입력을 읽는다. 실행은 다음 틱에 Human 이 한다 (원본 gamemain.cpp:2224-2288).
        /// 발사는 단발 무기면 누른 순간만, 그 밖에는 누르고 있는 동안 계속 받는다.
        /// </summary>
        /// <param name="input">입력 매니저.</param>
        /// <param name="locks">이벤트가 잠근 조작. LockFire 는 발사를, LockWeapon 은 그 밖의 무기 조작을 막는다.</param>
        /// <returns>이번 프레임의 무기 입력 플래그.</returns>
        private HumanWeaponAction ReadWeaponInput(InputManager input, int locks)
        {
            HumanWeaponAction weapon = HumanWeaponAction.None;

            // 발사가 잠긴 동안 누르고 있던 버튼은 풀린 뒤에 한 번 떼야 발사로 받는다.
            if (!input.IsPressed(InputManager.Fire)) m_fireReady = true;
            else if ((locks & EventManager.LockFire) != 0) m_fireReady = false;
            if (m_fireReady && (locks & EventManager.LockFire) == 0)
            {
                bool semiAuto = m_player.CurrentWeapon.Data.burstMode == WeaponBurstMode.SemiAuto;
                if (semiAuto ? input.WasPressed(InputManager.Fire) : input.IsPressed(InputManager.Fire)) weapon |= HumanWeaponAction.Fire;
            }

            if ((locks & EventManager.LockWeapon) != 0) return weapon;

            if (input.WasPressed(InputManager.Reload)) weapon |= HumanWeaponAction.Reload;
            if (input.WasPressed(InputManager.First)) weapon |= HumanWeaponAction.SelectFirst;
            if (input.WasPressed(InputManager.Second)) weapon |= HumanWeaponAction.SelectSecond;
            if (input.WasPressed(InputManager.Previous)) weapon |= HumanWeaponAction.SwitchPrevious;
            if (input.WasPressed(InputManager.Next)) weapon |= HumanWeaponAction.SwitchNext;
            if (input.WasPressed(InputManager.Drop)) weapon |= HumanWeaponAction.Drop;
            if (input.WasPressed(InputManager.Zoom)) weapon |= HumanWeaponAction.Scope;

            return weapon;
        }

        /// <summary>
        /// 현재 상태(생존/시점)에 맞는 카메라를 배치한다.
        /// </summary>
        /// <param name="dt">프레임 시간.</param>
        private void UpdateCamera(float dt)
        {
            // 이벤트가 카메라를 뗐으면 그 자리에 놓는다. 바깥에서 보는 시점이라 플레이어의 몸통과 다리를 보이게 한다.
            if (EventManager.Loaded && EventManager.Instance.CameraDetached)
            {
                if (!m_cameraDetached)
                {
                    m_cameraDetached = true;
                    m_player.HumanVisual.SetBodyVisible(true);
                }
                EventManager.Instance.GetStageCamera(out Vector3 position, out Vector3 angles, out float fov);
                m_camera.Fov = fov > 0f ? fov : m_baseFov;
                m_camera.Position = position;
                m_camera.Rotation = Coord.FromUnityEuler(angles);
                return;
            }
            if (m_cameraDetached)
            {
                // 다시 붙었다. 3인칭 카메라는 관성 없이 지금 시선에서 출발하고, 죽어 있었으면 사망 카메라가 처음부터 다시 돈다.
                m_cameraDetached = false;
                m_camYaw = m_yaw + m_viewYawOffset;
                m_camPitch = m_pitch + m_viewPitchOffset;
                m_deathCamInitialized = false;
                ApplyViewpoint();
            }

            if (!m_player.Alive)
            {
                ApplyDeathCamera(dt);
                return;
            }

            // 스코프 시야각은 1인칭에서만 적용한다 (원본 gamemain.cpp:2935).
            ScopeData scope = m_viewMode == ViewMode.FirstPerson ? m_player.ActiveScope : null;
            m_camera.Fov = scope != null && scope.fovDegrees > 0f ? scope.fovDegrees : m_baseFov;

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
                m_camera.Fov = m_baseFov;
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
            if (MapLoader.RaycastBlock(BlockLayer.Human, focus, -look, k_thirdPersonMaxDist, out float hitDist))
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
            m_player?.HumanVisual.SetBodyVisible(m_viewMode != ViewMode.FirstPerson || m_cameraDetached);
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

            return SwitchPlayer(((index + direction) % count + count) % count);
        }

        /// <summary>
        /// 조작 대상을 Humans 목록의 지정한 사람으로 바꾼다 (치트 F8, 디버그 콘솔의 player).
        /// </summary>
        /// <param name="next">새 조작 대상의 목록 인덱스.</param>
        /// <returns>실제로 교체됐으면 true. 범위 밖이거나 지금 대상과 같으면 false.</returns>
        public bool SwitchPlayer(int next)
        {
            if (m_player == null || next < 0 || next >= MapLoader.HumanCount || next == MapLoader.PlayerIndex) return false;

            // 옛 플레이어는 바깥에서 보이는 대상이 되므로 1인칭 때 숨겼던 몸통/다리를 다시 보이게 하고 상승 치트와 비행 모드를 푼다.
            m_player.HumanVisual.SetBodyVisible(true);
            m_controller.SetCheatRise(false);
            m_controller.SetFlight(false);
            m_player.ClearPendingWeaponInput();

            MapLoader.SetPlayer(MapLoader.GetHuman(next));
            return true;
        }

        /// <summary>
        /// 치트(F9) — 플레이어를 복제해 앞에 세운다. 종류·팀·무기 종류가 같고, 플레이어를 따라오거나 그 자리를 지킨다.
        /// </summary>
        /// <param name="follow">true 면 플레이어를 따라오고, false 면 만들어진 자리에서 대기한다.</param>
        private void SpawnClone(bool follow)
        {
            Human clone = MapLoader.SpawnHumanClone(m_player);
            if (clone == null) return;

            if (follow) clone.Brain.SetHoldTracking(m_player);
            else clone.Brain.SetHoldWait(clone.Controller.Position, clone.Controller.Yaw);
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
                m_fireReady = false;
                m_cameraDetached = false;

                var initial = new HumanInput { moveFlag = HumanMoveFlag.None, yaw = m_yaw, pitch = m_pitch };
                m_controller.SetInput(in initial);
                ApplyViewpoint();
            }

            return true;
        }
    }
}
