using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GodotXOPS
{
    /// <summary>
    /// 데모, 공식 미션, 어드온 미션 목록을 담는 최상위 미션 데이터 클래스.
    /// </summary>
    public class MissionData
    {
        // 파일 로드가 실패(부재/빈/깨짐)해도 소비자가 null 역참조하지 않도록 기본값으로 초기화한다.
        public DemoData openingData = new DemoData();
        public List<DemoData> demoData = new List<DemoData>();
        public List<OfficialMissionData> officialMissions = new List<OfficialMissionData>();

        // 애드온은 페이지(List) 단위로 나뉜다. 페이지 0 = addon 폴더 자동 로드분. addon.json 으로 페이지를 늘릴 수 있다.
        // addonPageNames는 addonMissions와 같은 인덱스로 정렬된 각 페이지 이름(빈 문자열 가능).
        // 둘 다 JSON 이 아니라 런타임 폴더 스캔으로 채운다.
        [JsonIgnore]
        public List<List<AddonMissionData>> addonMissions = new List<List<AddonMissionData>>();
        [JsonIgnore]
        public List<string> addonPageNames = new List<string>();
    }
}
