using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GodotXOPS
{
    /// <summary>
    /// 맵팩의 Manifest(JSON)를 담는 데이터 클래스. addon.json 의 manifests 가 이 파일들을 가리키고, Manifest 가 없으면 맵팩이 아니다.
    /// 맵팩 데이터는 번호 10000 에서 19999 이고, 그 맵팩의 미션을 로드하는 동안 붙어 있다. 경로는 모두 exe 폴더 기준이다.
    /// </summary>
    public class MapPackManifestData
    {
        // 메뉴의 페이지 이름.
        public string name = string.Empty;
        // 맵팩의 미션(MIF2). 적은 순서가 메뉴의 순서다.
        public List<string> missions = new List<string>();
        // 맵팩의 데이터 파일. 기본 파라미터 파일들의 목록 섹션을 한 파일에 모은 JSON 이다. 비워 두면 없다.
        public string humanDataPath = string.Empty;
        public string weaponDataPath = string.Empty;
        public string objectDataPath = string.Empty;
        public string soundDataPath = string.Empty;
        public string imageDataPath = string.Empty;
        // sky_data.json 과 같은 키를 쓰는 JSON. skyTexturePath 와 skyColor 두 목록만 읽는다.
        public string skyDataPath = string.Empty;
        // 스크립트 이벤트 묶음의 등록 파일. 종류 번호 10000 에서 19999 가 이 묶음의 이벤트다.
        public string eventDataPath = string.Empty;
        public string blockMaterialDataPath = string.Empty;
        public string effectDataPath = string.Empty;
        // 커스텀 바인딩 등록 파일. 이 맵팩의 미션을 하는 동안과 메뉴가 이 맵팩의 페이지에 있는 동안 OPTION 의 Custom 탭에 나온다. 비워 두면 없다.
        public string inputDataPath = string.Empty;
        // 날씨 목록 (weather_data.json 과 같은 키). 날씨 번호 10000 에서 19999 가 이 파일의 항목이다. 비워 두면 없다.
        public string weatherDataPath = string.Empty;
        // 맵팩의 미션에서 기본 화면 대신 그리는 화면 스크립트(.sgd)와 스프라이트 글꼴 그림. 미션(MIF2)이 같은 것을 적었으면 그것이 먼저다. 비워 두면 없다.
        public string briefingScriptPath = string.Empty;
        public string hudScriptPath = string.Empty;
        public string resultScriptPath = string.Empty;
        public string fontImageOverridePath = string.Empty;
        // 메뉴에서 이 맵팩의 페이지에 있는 동안 타이틀과 버전 자리를 그리는 화면 스크립트(.sgd). 비워 두면 기본 타이틀이다.
        public string mainMenuScriptPath = string.Empty;

        // 이 Manifest 파일의 경로 (exe 폴더 기준). 파일이 아니라 읽을 때 채운다.
        [JsonIgnore]
        public string manifestPath = string.Empty;
    }
}
