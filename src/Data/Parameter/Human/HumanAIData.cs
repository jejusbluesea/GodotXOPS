namespace GodotXOPS
{
    /// <summary>
    /// AI 레벨별 행동 파라미터. HumanData.aiIndex 가 이 리스트를 인덱싱한다 (원본 AIlevel → AIParameter 테이블).
    /// 원본 OpenXOPS parameter.h:142-149 AIParameter / parameter.cpp:1793-1816 테이블 대응.
    /// 전부 스케일 무관한 횟수/확률값 (좌표 ×0.1 변환 대상 아님).
    /// </summary>
    public class HumanAIData
    {
        public int aiming; // 조준 빈도 (회전 게이트). 클수록 자주 조준 보정.
        public int attack; // 발사 확률. GetRand(attack)==0 일 때 발사 → 작을수록 자주 쏨 (99=거의 안 쏨).
        public int search; // 탐색 능력. 탐색 루프 횟수 + 시야 거리 가산에 사용.
        public int limitsError; // 발사 판정 각도 보정. 음수면 허용각이 좁아져 정밀 조준 요구.
    }
}
