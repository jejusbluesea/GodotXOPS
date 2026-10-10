using Godot;

namespace GodotXOPS
{
    // EventManager 의 연출 담당 partial: 플레이어 조작 잠금, HUD 감추기, 화면 암전, 레터박스, 플레이어에게서 뗀 카메라. 원본에 없는 동작이다.
    // 값은 틱에서 진행하고 화면과 카메라가 틱 사이를 보간해 읽는다 (게임 정지 중에도 이벤트 틱은 돌므로 계속 움직인다).
    // 미션을 시작하거나 멈추면 전부 처음 상태로 돌아간다. 미션이 끝난 뒤에는 그대로 남는다 (끝나는 장면을 연출한 채로 결과 화면에 넘어가게).
    public partial class EventManager
    {
        // 플레이어 조작 잠금의 비트 (이벤트 Lock Player). PlayerController 가 읽는다.
        public const int LockMove = 1;
        public const int LockJump = 2;
        public const int LockLook = 4;
        public const int LockFire = 8;
        public const int LockWeapon = 16;
        public const int LockView = 32;
        public const int LockAll = LockMove | LockJump | LockLook | LockFire | LockWeapon | LockView;

        // 암전, 레터박스, 카메라 이동에 걸리는 시간의 상한 (초).
        private const float k_maxStageSeconds = 3600f;
        // 레터박스 띠 하나의 높이 상한 (화면 높이 480 기준. 둘이 만나면 화면이 전부 가려진다).
        private const float k_maxLetterboxHeight = 240f;
        // 뗀 카메라의 pitch 한계와 시야각 범위 (도).
        private const float k_cameraMaxPitch = 89f;
        private const float k_cameraMinFov = 1f;
        private const float k_cameraMaxFov = 170f;

        /// <summary>
        /// 정해진 틱 수에 걸쳐 한 값에서 다른 값으로 가는 수 하나. 한 틱 전의 값을 함께 들어 틱 사이를 보간한다.
        /// </summary>
        private sealed class StageValue
        {
            private float m_from;
            private float m_to;
            private float m_value;
            private float m_previous;
            private int m_ticks;
            private int m_elapsed;
            private bool m_ease;

            // 지금 값 (틱 기준).
            public float Value => m_value;
            // 도착할 값.
            public float Target => m_to;
            public bool Moving => m_elapsed < m_ticks;

            /// <summary>
            /// 값을 바로 정한다.
            /// </summary>
            /// <param name="value">새 값.</param>
            public void Set(float value)
            {
                m_from = m_to = m_value = m_previous = value;
                m_ticks = m_elapsed = 0;
            }

            /// <summary>
            /// 지금 값에서 새 값으로 가기 시작한다.
            /// </summary>
            /// <param name="target">도착할 값.</param>
            /// <param name="ticks">걸리는 틱 수. 0 이하면 바로 바뀐다.</param>
            /// <param name="ease">true 면 천천히 출발해 천천히 멈춘다.</param>
            public void Start(float target, int ticks, bool ease)
            {
                if (ticks <= 0)
                {
                    Set(target);
                    return;
                }
                m_from = m_value;
                m_to = target;
                m_previous = m_value;
                m_ticks = ticks;
                m_elapsed = 0;
                m_ease = ease;
            }

            /// <summary>
            /// 한 틱 진행한다.
            /// </summary>
            public void Tick()
            {
                m_previous = m_value;
                if (m_elapsed >= m_ticks) return;

                m_elapsed++;
                float progress = (float)m_elapsed / m_ticks;
                if (m_ease) progress = progress * progress * (3f - 2f * progress);
                m_value = Mathf.Lerp(m_from, m_to, progress);
            }

            /// <summary>
            /// 틱 사이를 보간한 값.
            /// </summary>
            /// <param name="alpha">다음 틱까지의 진행 비율.</param>
            /// <returns>화면에 쓸 값.</returns>
            public float Visual(float alpha)
            {
                return Mathf.Lerp(m_previous, m_value, alpha);
            }
        }

        private int m_playerLock;
        private bool m_hudHidden;
        // 이벤트가 AI 를 멈췄는지. 되돌릴 때 이벤트가 멈춘 것만 푼다 (점검 도구와 디버그 콘솔이 멈춘 것은 건드리지 않는다).
        private bool m_aiPaused;

        private Color m_fadeColor = Colors.Black;
        private readonly StageValue m_fadeAlpha = new StageValue();
        private readonly StageValue m_letterbox = new StageValue();

        private bool m_cameraDetached;
        // 뗀 카메라의 자세: 위치(Godot 좌표)와 UnityXOPS 오일러 각(x pitch, y yaw, z roll, 도), 시야각(0 이면 설정값).
        private Vector3 m_cameraFromPosition;
        private Vector3 m_cameraToPosition;
        private Vector3 m_cameraFromAngles;
        private Vector3 m_cameraToAngles;
        private float m_cameraFromFov;
        private float m_cameraToFov;
        // 카메라 이동의 진행 0~1.
        private readonly StageValue m_cameraProgress = new StageValue();

        // 플레이어 조작 잠금 (Lock* 비트의 합). 게임 정지 중에는 전부 잠긴 것으로 친다.
        public int PlayerLock => SimClock.WorldPaused ? LockAll : m_playerLock;
        // HUD 를 그릴지 (이벤트 Show HUD). 미션이 끝난 뒤에는 늘 true 다 (종료 문구와 암전이 보여야 한다).
        public bool HudVisible => !m_hudHidden || m_result != MissionResult.InProgress;
        // 이벤트의 화면 암전 색. 알파가 지금의 진하기다 (틱 사이를 보간한 값). HUD 아래에 그린다.
        public Color StageFadeColor => new Color(m_fadeColor.R, m_fadeColor.G, m_fadeColor.B, Mathf.Clamp(m_fadeAlpha.Visual(SimClock.EventAlpha), 0f, 1f));
        // 레터박스 띠 하나의 높이 (화면 높이 480 기준, 틱 사이를 보간한 값). 0 이면 없다.
        public float LetterboxHeight => Mathf.Max(0f, m_letterbox.Visual(SimClock.EventAlpha));
        // 카메라가 플레이어에게서 떨어져 있는지 (이벤트 Detach Camera).
        public bool CameraDetached => m_cameraDetached;
        // 뗀 카메라가 움직이는 중인지.
        public bool CameraMoving => m_cameraDetached && m_cameraProgress.Moving;

        /// <summary>
        /// 모든 AI 를 멈추거나 다시 돌린다 (이벤트 Pause AI). 이벤트의 지시(AI Look At / AI Fire At)를 받는 사람은 멈춰 있어도 그 지시대로 움직인다.
        /// </summary>
        /// <param name="paused">멈출지.</param>
        public void SetAiPaused(bool paused)
        {
            m_aiPaused = paused;
            AIController.Enabled = !paused;
        }

        /// <summary>
        /// 플레이어 조작 잠금을 정한다.
        /// </summary>
        /// <param name="flags">Lock* 비트의 합. 0 이면 전부 푼다.</param>
        public void SetPlayerLock(int flags)
        {
            m_playerLock = flags & LockAll;
        }

        /// <summary>
        /// HUD 를 켜거나 끈다. 이벤트 메시지와 이벤트가 놓은 글자는 꺼도 남는다.
        /// </summary>
        /// <param name="visible">그릴지.</param>
        public void SetHudVisible(bool visible)
        {
            m_hudHidden = !visible;
        }

        /// <summary>
        /// 화면을 한 색으로 덮거나 걷는다. 지금의 진하기에서 새 진하기로 간다. 색은 바로 바뀐다.
        /// </summary>
        /// <param name="rgb">색 (0xRRGGBB).</param>
        /// <param name="alpha">도착할 진하기 (0 은 걷힘, 1 은 완전히 덮임).</param>
        /// <param name="seconds">걸리는 시간 (초). 0 이하면 바로 바뀐다.</param>
        public void FadeScreen(int rgb, float alpha, float seconds)
        {
            if (!float.IsFinite(alpha) || !float.IsFinite(seconds)) return;

            m_fadeColor = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
            m_fadeAlpha.Start(Mathf.Clamp(alpha, 0f, 1f), StageTicks(seconds), false);
        }

        /// <summary>
        /// 화면 위아래의 검은 띠를 넣거나 뺀다. 지금 높이에서 새 높이로 간다.
        /// </summary>
        /// <param name="height">띠 하나의 높이 (화면 높이 480 기준). 0 이면 없앤다.</param>
        /// <param name="seconds">걸리는 시간 (초). 0 이하면 바로 바뀐다.</param>
        public void SetLetterbox(float height, float seconds)
        {
            if (!float.IsFinite(height) || !float.IsFinite(seconds)) return;

            m_letterbox.Start(Mathf.Clamp(height, 0f, k_maxLetterboxHeight), StageTicks(seconds), true);
        }

        /// <summary>
        /// 카메라를 플레이어에게서 떼어 한 자리에 놓는다. 이미 떼어져 있으면 그 자리로 바로 옮긴다.
        /// </summary>
        /// <param name="position">자리 (Godot 좌표).</param>
        /// <param name="yaw">방향 (도, 사람 기준 yaw).</param>
        /// <param name="pitch">아래를 보는 각 (도, 아래 +).</param>
        /// <param name="roll">기울기 (도).</param>
        /// <param name="fov">시야각 (도). 0 이하면 설정값.</param>
        /// <returns>놓았으면 true. 올바른 수가 아니면 false.</returns>
        public bool DetachCamera(Vector3 position, float yaw, float pitch, float roll, float fov)
        {
            if (!position.IsFinite() || !float.IsFinite(yaw) || !float.IsFinite(pitch) || !float.IsFinite(roll) || !float.IsFinite(fov)) return false;

            m_cameraDetached = true;
            m_cameraFromPosition = m_cameraToPosition = position;
            m_cameraFromAngles = m_cameraToAngles = CameraAngles(yaw, pitch, roll);
            m_cameraFromFov = m_cameraToFov = CameraFov(fov);
            m_cameraProgress.Set(1f);
            return true;
        }

        /// <summary>
        /// 카메라를 플레이어에게 다시 붙인다.
        /// </summary>
        public void AttachCamera()
        {
            m_cameraDetached = false;
            m_cameraProgress.Set(1f);
        }

        /// <summary>
        /// 뗀 카메라를 정해진 시간에 걸쳐 새 자세로 옮긴다. 블록과 사람을 무시한다. yaw 는 가까운 쪽으로 돈다.
        /// </summary>
        /// <param name="position">도착할 자리 (Godot 좌표).</param>
        /// <param name="yaw">도착할 방향 (도, 사람 기준 yaw).</param>
        /// <param name="pitch">도착할 pitch (도, 아래 +).</param>
        /// <param name="roll">도착할 기울기 (도).</param>
        /// <param name="fov">도착할 시야각 (도). 0 이하면 설정값.</param>
        /// <param name="seconds">걸리는 시간 (초). 0 이하면 바로 옮긴다.</param>
        /// <param name="ease">true 면 천천히 출발해 천천히 멈춘다.</param>
        /// <returns>시작했으면 true. 카메라가 붙어 있거나 올바른 수가 아니면 false.</returns>
        public bool TweenCamera(Vector3 position, float yaw, float pitch, float roll, float fov, float seconds, bool ease)
        {
            if (!m_cameraDetached) return false;
            if (!position.IsFinite() || !float.IsFinite(yaw) || !float.IsFinite(pitch) || !float.IsFinite(roll) || !float.IsFinite(fov) || !float.IsFinite(seconds)) return false;

            float progress = m_cameraProgress.Value;
            m_cameraFromPosition = m_cameraFromPosition.Lerp(m_cameraToPosition, progress);
            m_cameraFromAngles = m_cameraFromAngles.Lerp(m_cameraToAngles, progress);
            m_cameraFromFov = Mathf.Lerp(m_cameraFromFov, m_cameraToFov, progress);

            m_cameraToPosition = position;
            Vector3 target = CameraAngles(yaw, pitch, roll);
            // yaw 만 가까운 쪽으로 돌게 맞춘다. 출발 각을 한 바퀴 안으로 접어 두면 되풀이해도 값이 커지지 않는다.
            m_cameraFromAngles.Y = Mathf.PosMod(m_cameraFromAngles.Y, 360f);
            target.Y = m_cameraFromAngles.Y + Coord.DeltaAngle(m_cameraFromAngles.Y, target.Y);
            m_cameraToAngles = target;
            m_cameraToFov = CameraFov(fov);

            m_cameraProgress.Set(0f);
            m_cameraProgress.Start(1f, StageTicks(seconds), ease);
            return true;
        }

        /// <summary>
        /// 뗀 카메라의 지금 자세 (틱 사이를 보간한 값). PlayerController 가 카메라를 놓는 데 쓴다.
        /// </summary>
        /// <param name="position">자리 (Godot 좌표).</param>
        /// <param name="angles">UnityXOPS 오일러 각 (x pitch, y yaw, z roll, 도).</param>
        /// <param name="fov">시야각 (도). 0 이면 설정값.</param>
        public void GetStageCamera(out Vector3 position, out Vector3 angles, out float fov)
        {
            float progress = m_cameraProgress.Visual(SimClock.EventAlpha);
            position = m_cameraFromPosition.Lerp(m_cameraToPosition, progress);
            angles = m_cameraFromAngles.Lerp(m_cameraToAngles, progress);
            // 설정값(0)과 정해진 시야각 사이는 보간할 수 없어서 도착한 쪽의 값을 쓴다.
            fov = m_cameraFromFov > 0f && m_cameraToFov > 0f ? Mathf.Lerp(m_cameraFromFov, m_cameraToFov, progress) : m_cameraToFov;
        }

        /// <summary>
        /// 연출 상태를 값만 담은 사전으로 만든다 (스크립트 이벤트의 stage).
        /// </summary>
        /// <returns>paused, ai_paused, lock, hud, fade(도착할 진하기), fading, letterbox(도착할 높이), camera_detached, camera_moving.</returns>
        public Godot.Collections.Dictionary StageInfo()
        {
            return new Godot.Collections.Dictionary
            {
                ["paused"] = SimClock.WorldPaused,
                ["ai_paused"] = m_aiPaused,
                ["lock"] = m_playerLock,
                ["hud"] = !m_hudHidden,
                ["fade"] = m_fadeAlpha.Target,
                ["fading"] = m_fadeAlpha.Moving,
                ["letterbox"] = m_letterbox.Target,
                ["camera_detached"] = m_cameraDetached,
                ["camera_moving"] = CameraMoving,
            };
        }

        /// <summary>
        /// 틱의 끝에: 암전, 레터박스, 카메라 이동을 한 틱 진행한다.
        /// </summary>
        private void TickStage()
        {
            m_fadeAlpha.Tick();
            m_letterbox.Tick();
            m_cameraProgress.Tick();
        }

        /// <summary>
        /// 연출 상태를 전부 처음으로 되돌린다 (미션 시작, 맵 내리기): 게임 정지와 AI 정지를 풀고, 조작 잠금·HUD·암전·레터박스·카메라를 원래대로 한다.
        /// 사람에게 건 것(무적, 무한 탄약, AI 지시)은 사람과 함께 사라지므로 여기서 다루지 않는다.
        /// </summary>
        private void ResetStage()
        {
            SimClock.WorldPaused = false;
            if (m_aiPaused) AIController.Enabled = true;
            m_aiPaused = false;
            m_playerLock = 0;
            m_hudHidden = false;
            m_fadeColor = Colors.Black;
            m_fadeAlpha.Set(0f);
            m_letterbox.Set(0f);
            m_cameraDetached = false;
            m_cameraProgress.Set(1f);
        }

        /// <summary>
        /// 초를 틱 수로 바꾼다.
        /// </summary>
        /// <param name="seconds">시간 (초).</param>
        /// <returns>틱 수. 0 이하의 시간은 0.</returns>
        private static int StageTicks(float seconds)
        {
            return Mathf.RoundToInt(Mathf.Clamp(seconds, 0f, k_maxStageSeconds) * SimClock.FrameRate);
        }

        /// <summary>
        /// 카메라의 각도를 UnityXOPS 오일러 각으로 묶는다. pitch 는 한계 안으로 자른다.
        /// </summary>
        /// <param name="yaw">yaw (도).</param>
        /// <param name="pitch">pitch (도, 아래 +).</param>
        /// <param name="roll">roll (도).</param>
        /// <returns>(pitch, yaw, roll).</returns>
        private static Vector3 CameraAngles(float yaw, float pitch, float roll)
        {
            return new Vector3(Mathf.Clamp(pitch, -k_cameraMaxPitch, k_cameraMaxPitch), yaw, roll);
        }

        /// <summary>
        /// 시야각을 범위 안으로 자른다.
        /// </summary>
        /// <param name="fov">시야각 (도).</param>
        /// <returns>시야각. 0 이하였으면 0 (설정값을 쓴다는 뜻).</returns>
        private static float CameraFov(float fov)
        {
            return fov > 0f ? Mathf.Clamp(fov, k_cameraMinFov, k_cameraMaxFov) : 0f;
        }
    }
}
