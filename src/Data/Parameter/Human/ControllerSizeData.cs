namespace GodotXOPS
{
    /// <summary>
    /// 인간 컨트롤러(캡슐이 아닌 radius/height 직접 감지)의 체형 크기. per-type 크기 목록의 한 항목이며
    /// HumanTypeData.controllerSizeIndex 로 지정한다. 사람/개/비휴머노이드 로봇 등 서로 다른 체형을 표현하기 위한 것.
    /// </summary>
    public class ControllerSizeData
    {
        public float height; // 세로 높이 — 맵/인간간 충돌의 수직 판정
        public float cameraHeight; // 눈/카메라 부착 높이 — 1인칭 카메라·AI 시야·사운드 리스너·총알 발사 높이
        public float mapRadius; // 컨트롤러 ↔ 맵 블록 충돌 반경
        public float humanRadius; // 컨트롤러 ↔ 컨트롤러(인간간) 충돌 반경
    }
}
