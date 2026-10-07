using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 확장 미션 파일(MIF2, JSON)의 내용을 담는 데이터 클래스. 원본 MIF 를 대체하는 GodotXOPS 전용 형식이다.
    /// 경로는 모두 exe 폴더 기준이고, 블록과 포인트는 확장 형식(BD2, PD2)만 받는다.
    /// </summary>
    public class ExtendedMissionData
    {
        // 미션 목록에 나오는 이름과 브리핑 제목.
        public string name = string.Empty;
        public string fullname = string.Empty;
        // 블록 데이터(BD2)와 포인트 데이터(PD2).
        public string blockPath = string.Empty;
        public string pointPath = string.Empty;
        public int skyIndex;
        // 추가 충돌 검사와 어두운 화면 (MIF 의 화면 플래그 두 비트).
        public bool adjustCollision;
        public bool darkScreen;
        // 브리핑 이미지 두 장. 비워 두면 없다.
        public string image0 = string.Empty;
        public string image1 = string.Empty;
        // 브리핑 본문. 항목 하나가 한 줄이다.
        public List<string> briefing = new List<string>();
        // BD2 의 면 재질 번호 -1 이 가리키는 재질 번호.
        public int defaultBlockMaterial;
        // 이 미션이 들고 오는 추가 데이터 파일. 기본 파라미터 파일들의 목록 섹션을 한 파일에 모은 JSON 이고, 번호 10000 부터가 이 파일의 항목이다. 비워 두면 없다.
        public string addonHumanDataPath = string.Empty;
        public string addonWeaponDataPath = string.Empty;
        public string addonObjectDataPath = string.Empty;
        public string addonEffectDataPath = string.Empty;
        public string addonBlockMaterialDataPath = string.Empty;
        // 이 미션 전용 스크립트 이벤트 묶음의 등록 파일. 종류 번호 10000 이상이 이 묶음의 이벤트다. 비워 두면 없다.
        public string addonEventDataPath = string.Empty;
    }
}
