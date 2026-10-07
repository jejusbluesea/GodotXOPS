using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 인간 AI의 난이도 프리셋(aiData), 무기 스코프별 파라미터(aiScopeData), 그리고 탐색·조준·청취·경로 등 AI 전역 상수를 담는 컨테이너 클래스.
    /// </summary>
    public class HumanAIParameterData
    {
        public DataList<HumanAIData> aiData = new DataList<HumanAIData>();
        public DataList<HumanAIScopeData> aiScopeData = new DataList<HumanAIScopeData>();

        // === AI 전역 상수 (전투 코어) — 원본 OpenXOPS ai.h #define + SearchEnemy/Action 하드코딩의 데이터화. ===
        // 시야각 (deg, 전체 각도 → 코드에서 ½씩 좌우 적용). near=근거리(A) / long=원거리(B) / Caution=경계 상태 확장각.
        // 원본 ai.cpp:1326-1343 SearchEnemy.
        public float aiSearchFovHNear; // 110
        public float aiSearchFovVNear; // 60
        public float aiSearchFovHLong; // 60
        public float aiSearchFovVLong; // 40
        public float aiSearchFovHNearCaution; // 130
        public float aiSearchFovVNearCaution; // 80
        public float aiSearchFovHLongCaution; // 80
        public float aiSearchFovVLongCaution; // 50

        // 탐색 거리 = base + coeff×(search-2) + 스코프 가산 (m 단위, 원본 ×0.1). 루프 횟수 = search × loopScale.
        public float aiSearchDistBaseNormal; // 35 (원본 350)
        public float aiSearchDistCoeffNormal; // 1.2 (원본 12)
        public float aiSearchDistBaseCaution; // 42 (원본 420)
        public float aiSearchDistCoeffCaution; // 1.5 (원본 15)
        public int aiSearchLoopScale; // 4 (원본 AI_TOTALHUMAN_SCALE = MAX_HUMAN/24)
        public int aiSearchPoolSize; // 96 (원본 MAX_HUMAN 샘플 풀 — 클수록 발견 느림=반응 지연↑. 인원<풀이면 빈칸 miss)

        // 조준 회전 적분 (원본 AIObjectDriver::ControlObject ai.cpp:2316-2371). per-frame(33.333fps) 가속/감쇠.
        public float aiTurnRateDeg; // 0.8 (AI_ADDTURNRAD, 프레임당 각속도 가산)
        public float aiTurnDamping; // 0.8 (프레임당 각속도 감쇠 배율)
        public float aiTurnDeadzoneDeg; // 0.2 (각속도 데드존)
        public float aiTurnMaxPitchDeg; // 70 (ry clamp)

        // 거리 임계 (m 단위, 원본 ×0.1).
        public float aiShortAttackDist; // 20 (근/원거리 경계, 원본 AI_CHECKSHORTATTACK_DIST 200)
        public float aiActionCancelDist; // 62 (전투 종료 거리, 원본 620)
        public int aiCautionFrames; // 160 (경계 지속 프레임)
        // 좀비 할퀴기 팔 각도 (원본 space, 음수=아래; armAngleInitial 과 동일 규약). 원본 AI_ZOMBIEATTACK_ARMRY -15°.
        // 값을 키울수록(0=수평, 양수=위) 좀비가 적 식별 시 팔을 더 위로 든다.
        public float aiZombieArmAngle; // -15 (원본 충실), 더 들고 싶으면 0~+30 등으로 조정

        // AI 청취 거리 — 음원이 이 안이면 경계(CAUTION) 전환. 원본 SoundManager::GetWorldSound maxdist ×0.1 (soundmanager.cpp:323-372).
        public float aiHearGunfireDist; // 12 (적 일반 발포, 원본 120)
        public float aiHearGunfireSilencerDist; // 3 (소음(suppressor) 무기 적 발포, 원본 30)
        public float aiHearGunfireAllyDist; // 2 (아군 발포 — 가까이서만, 원본 20)
        public float aiHearBulletDist; // 2 (총알 통과 closest-approach, 팀 무관, 원본 20)
        public float aiHearBulletWallHitDist; // 3 (총알 벽 탄착 HIT_MAP, 팀 무관 = 적/아군 탄 모두, 원본 30)
        public float aiHearExplosionDist; // 8 (폭발, 팀 무관, 원본 80)

        // 피탄음 — 총알이 사람/소품에 맞을 때, 팀 무관(원본 HIT_HUMAN_*/HIT_SMALLOBJECT, ×0.1).
        public float aiHearHitHumanHead; // 6 (머리 피탄, 원본 60)
        public float aiHearHitHumanBody; // 5 (몸통 피탄, 원본 50)
        public float aiHearHitHumanLeg; // 4 (다리 피탄, 원본 40)
        public float aiHearHitHumanZombie; // 4 (좀비 근접 타격, 원본 HIT_HUMAN_ZOMBIE 40)
        public float aiHearHitSmallObject; // 2 (소품 피탄, 원본 20)

        // 적(다른 팀) 달리기 발소리 청취 거리 — 방향별 (원본 soundmanager.cpp:348-365, ×0.1). 걷기/점프/정지는 인식 안 됨. 같은 팀 발소리는 무시.
        public float aiHearFootstepForward; // 4.0 (전진 달리기, 원본 40)
        public float aiHearFootstepSide; // 3.5 (좌우 달리기, 원본 35)
        public float aiHearFootstepBack; // 3.0 (후진 달리기, 원본 30)

        // AI 경로 이동 (기본 순찰) — 원본 ai.h #define ×0.1.
        public float aiArrivalDistPath; // 0.5 (웨이포인트 도착 판정, 원본 AI_ARRIVALDIST_PATH 5.0)
        public int aiStop5SecFrames; // 167 (STOP_5SEC 대기 프레임, 원본 GAMEFPS×5)
        public float aiJumpCheckDist; // 0.2 (진행 방향 앞 점프 장애물 판정 거리, 원본 AI_CHECKJUMP_DIST 2.0)
        public float aiArrivalDistTracking; // 1.8 (추적 도착 거리, 원본 AI_ARRIVALDIST_TRACKING 18.0)
        public float aiArrivalDistWalkTracking; // 2.4 (추적 걷기 전환 거리, 원본 AI_ARRIVALDIST_WALKTRACKING 24.0)
        public float aiCombatRetreatDist; // 2.0 (전투 중 적 근접 후퇴 거리, 원본 MoveRandom 20.0)
    }
}
