using Godot;

namespace GodotXOPS
{
    // AIBrain 의 연출 지시 담당 partial. 이벤트(AI Look At / AI Fire At)가 한 점을 바라보거나 쏘게 한 사람은 풀릴 때까지 평소의 판단을 하지 않는다. 원본에 없는 동작이다.
    public partial class AIBrain
    {
        // 지시받은 점을 겨눴다고 보는 오차 (도). 이 안에 들어와야 쏜다.
        private const float k_directAimTolerance = 0.5f;

        private bool m_directed;
        private Vector3 m_directTarget;
        // 더 쏴야 하는 발 수.
        private int m_directShots;
        // 지난 틱의 발사 요청이 받아들여지지 않았는지. 그러면 한 틱 쉬어서 단발 무기의 연속 발사 제한이 풀리게 한다 (요청이 없는 틱에 풀린다).
        private bool m_directShotRefused;

        // 이벤트의 지시를 받고 있는지. 켜져 있으면 AI 전체가 멈춰 있어도, 플레이어가 조작하는 사람이어도 이 지시대로 움직인다.
        public bool Directed => m_directed;
        // 지시받은 사격에서 남은 발 수.
        public int DirectShotsLeft => m_directShots;

        /// <summary>
        /// 한 점을 계속 바라보게 한다. 제자리에서 평소의 회전 속도로 돌아 yaw 와 pitch 를 맞춘다. 걸려 있는 동안 움직이지 않고 적을 찾지 않는다.
        /// </summary>
        /// <param name="target">바라볼 점 (Godot 좌표).</param>
        public void SetDirectLook(Vector3 target)
        {
            if (!m_directed) BeginDirect();
            m_directTarget = target;
            m_directShots = 0;
        }

        /// <summary>
        /// 한 점을 바라보고, 겨눠지면 정해진 발 수만큼 쏘게 한다. 다 쏜 뒤에는 계속 바라본다. 탄창이 비면 재장전하고, 쏠 탄이 없으면 그만둔다.
        /// </summary>
        /// <param name="target">쏠 점 (Godot 좌표).</param>
        /// <param name="shots">쏠 발 수.</param>
        public void SetDirectFire(Vector3 target, int shots)
        {
            if (!m_directed) BeginDirect();
            m_directTarget = target;
            m_directShots = Mathf.Max(0, shots);
        }

        /// <summary>
        /// 지시를 풀고 평소의 판단으로 돌아간다.
        /// </summary>
        public void ClearDirect()
        {
            if (!m_directed) return;

            m_directed = false;
            m_directShots = 0;
            m_addYaw = 0f;
            m_addPitch = 0f;
            if (m_mode != AIBattleMode.Dead) m_nav.Refresh();
        }

        /// <summary>
        /// 지시를 받기 시작할 때: 하던 전투와 유지되던 이동 의사를 버린다.
        /// </summary>
        private void BeginDirect()
        {
            m_directed = true;
            if (m_mode != AIBattleMode.Dead) m_mode = AIBattleMode.Normal;
            m_enemy = null;
            m_cautionCnt = 0;
            m_actionCnt = 0;
            m_faceCaution = false;
            m_scanLeft = false;
            m_scanRight = false;
            m_combatMove = HumanMoveFlag.None;
        }

        /// <summary>
        /// 지시를 받는 동안의 한 틱. 평소의 회전(ApplyControl)과 같은 가속·감쇠로 돌되, 목표를 지나치게 되는 틱에는 목표 각에 정확히 멈춘다.
        /// </summary>
        private void DirectMain()
        {
            HumanAIParameterData ai = AIData;

            Vector3 eye = m_controller.Position + Vector3.Up * m_controller.CameraHeight;
            Vector3 toTarget = m_directTarget - eye;
            bool hasTarget = toTarget.LengthSquared() > Mathf.Epsilon;

            float targetYaw = hasTarget ? YawTo(toTarget) : m_controller.Yaw;
            float targetPitch = hasTarget ? Mathf.Clamp(PitchTo(toTarget), -ai.aiTurnMaxPitchDeg, ai.aiTurnMaxPitchDeg) : m_controller.Pitch;

            bool aimed = Mathf.Abs(Coord.DeltaAngle(m_controller.Yaw, targetYaw)) + Mathf.Abs(targetPitch - m_controller.Pitch) < k_directAimTolerance;
            if (aimed && m_directShots > 0) DirectShoot();

            // 이번 틱에 쏜 반동이 더해진 뒤의 조준각에서 출발한다.
            float yaw = DirectTurn(m_controller.Yaw, Coord.DeltaAngle(m_controller.Yaw, targetYaw), ref m_addYaw, ai);
            float pitch = DirectTurn(m_controller.Pitch, targetPitch - m_controller.Pitch, ref m_addPitch, ai);

            var input = new HumanInput { moveFlag = HumanMoveFlag.None, yaw = yaw, pitch = pitch };
            m_controller.SetInput(in input);
        }

        /// <summary>
        /// 한 축을 목표 쪽으로 한 틱 돌린다.
        /// </summary>
        /// <param name="angle">지금 각 (도).</param>
        /// <param name="delta">목표까지 남은 각 (도, 부호 있음).</param>
        /// <param name="speed">이 축의 각속도 (도/틱). 갱신된다.</param>
        /// <param name="ai">AI 데이터.</param>
        /// <returns>새 각 (도).</returns>
        private static float DirectTurn(float angle, float delta, ref float speed, HumanAIParameterData ai)
        {
            if (delta > 0f) speed += ai.aiTurnRateDeg;
            if (delta < 0f) speed -= ai.aiTurnRateDeg;

            // 이번 틱의 회전이 남은 각을 넘거나 반대쪽을 향하면 목표에 멈춘다.
            if (Mathf.Abs(speed) >= Mathf.Abs(delta) || speed * delta < 0f)
            {
                speed = 0f;
                return angle + delta;
            }

            float result = angle + speed;
            speed *= ai.aiTurnDamping;
            if (Mathf.Abs(speed) < ai.aiTurnDeadzoneDeg) speed = 0f;
            return result;
        }

        /// <summary>
        /// 지시받은 사격 한 틱. 평소의 발사 함수를 거치므로 발사 간격, 조준 오차, 반동이 그대로 들어간다.
        /// </summary>
        private void DirectShoot()
        {
            Weapon weapon = m_self.CurrentWeapon;
            if (weapon.IsNone || TotalBullets(weapon) == 0)
            {
                m_directShots = 0;
                return;
            }

            if (weapon.Magazine == 0)
            {
                m_self.ReloadWeapon();
                return;
            }

            if (m_directShotRefused)
            {
                m_directShotRefused = false;
                return;
            }

            if (m_self.ShotWeapon()) m_directShots--;
            else m_directShotRefused = true;
        }
    }
}
