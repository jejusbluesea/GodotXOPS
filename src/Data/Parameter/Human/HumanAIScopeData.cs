namespace GodotXOPS
{
    /// <summary>
    /// 무기 스코프(scopemode)별 AI 사격/탐색 파라미터. HumanAIParameterData.aiScopeData 가 무기의 scopemode 로 인덱싱.
    /// 원본 OpenXOPS parameter.h:164-167 ScopeParameter 의 AI 필드 / parameter.cpp:1830-1863 대응.
    /// 각도는 deg (스케일 무관), 탐색 거리 가산은 m 단위 (원본 ×0.1).
    /// </summary>
    public class HumanAIScopeData
    {
        public float aiShotAngle; // 발포 판정 각도 (근거리, deg).
        public float aiShotAngleLong; // 발포 판정 각도 (원거리, deg).
        public float aiAddSearchDistNormal; // 탐색 거리 가산 (평상시, m 단위).
        public float aiAddSearchDistCaution; // 탐색 거리 가산 (경계시, m 단위).
    }
}
