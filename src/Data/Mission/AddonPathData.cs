using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// addon.json 을 역직렬화하는 데이터 클래스. 맵팩 Manifest 파일의 경로 목록(exe 폴더 기준)을 담는다.
    /// Manifest 하나가 메뉴의 어드온 페이지 하나가 되고, 페이지 이름과 미션 순서는 Manifest 가 정한다.
    /// </summary>
    public class AddonPathData
    {
        // 다른 컨테이너와 달리 기본값으로 초기화하지 않는다 — addon.json은 선택 파일이라
        // "목록 자체가 없음"(null)이 곧 맵팩 없음을 뜻하고, LoadAddonMissions가 그 null을 신호로 읽는다.
        public List<string> manifests;
    }
}
