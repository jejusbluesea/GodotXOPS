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

        // 애드온은 페이지(List) 단위로 나뉜다. 페이지 0 = addon 폴더 자동 로드분. 페이지 1 부터는 addon.json 에 적은 맵팩 Manifest 다.
        // addonPageNames 와 addonPagePacks 는 addonMissions 와 같은 인덱스로 정렬된 페이지 이름(빈 문자열 가능)과 그 페이지의 맵팩(페이지 0 은 null)이다.
        // 셋 다 JSON 이 아니라 런타임에 채운다.
        [JsonIgnore]
        public List<List<AddonMissionData>> addonMissions = new List<List<AddonMissionData>>();
        [JsonIgnore]
        public List<string> addonPageNames = new List<string>();
        [JsonIgnore]
        public List<MapPackManifestData> addonPagePacks = new List<MapPackManifestData>();
    }
}
