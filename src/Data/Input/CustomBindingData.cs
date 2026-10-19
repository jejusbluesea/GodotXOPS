using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 커스텀 바인딩 등록 파일(JSON)의 내용. 설치형은 godotdata/input/*.json, 맵팩은 Manifest 의 inputDataPath, 미션은 MIF2 의 addonInputDataPath 다.
    /// 등록한 버튼은 OPTION 의 Custom 탭에서 키를 바꿀 수 있고, 스크립트 이벤트와 화면 스크립트가 이름으로 읽는다.
    /// </summary>
    public class CustomBindingListData
    {
        public List<CustomBindingData> bindings = new List<CustomBindingData>();
    }

    /// <summary>
    /// 커스텀 바인딩 하나. 버튼 하나짜리만 받는다.
    /// </summary>
    public class CustomBindingData
    {
        // 액션 이름. 영문자·숫자·밑줄만 쓴다. 기본 바인딩(jump, fire 등)과 같은 이름은 받지 않는다. 같은 이름은 맵팩이 달라도 같은 키 설정을 함께 쓴다.
        public string name = string.Empty;
        // OPTION 에 보이는 이름 (스프라이트 글꼴이라 영문, 8자 안). 비우면 name 이다.
        public string label = string.Empty;
        // 처음 키. 바인딩 경로 형식이다 (예: "<Keyboard>/v", "<Mouse>/rightButton"). 비우면 묶인 키가 없다.
        public string baseValue = string.Empty;
    }

    /// <summary>
    /// config.json 에 남기는 커스텀 바인딩의 키 하나. 유저가 처음 키와 다르게 바꾼 것만 남는다.
    /// </summary>
    public class CustomBindingValue
    {
        public string name = string.Empty;
        public string binding = string.Empty;
    }
}
