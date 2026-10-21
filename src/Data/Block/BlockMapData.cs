using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// BD2 옆에 두는 맵 데이터 파일(JSON)의 내용을 담는 컨테이너 클래스: 텍스처 목록과 구역 태그.
    /// BD2 파일이 이 파일의 경로(mapDataPath)를 들고 있고, 여러 맵이 한 파일을 같이 쓸 수 있다.
    /// </summary>
    public class BlockMapData
    {
        public List<BlockTextureData> textureData = new List<BlockTextureData>();
        // 구역 태그. BD2 의 구역 블록이 이 목록의 번호를 가리키고, 같은 태그의 구역 블록들이 구역 하나다.
        public List<AreaTagData> areaTagData = new List<AreaTagData>();
    }

    /// <summary>
    /// 구역 안에서 팀끼리 적대하는 방식. AI 의 판단만 바꾸고 데미지는 그대로 들어간다.
    /// </summary>
    public enum AreaHostility
    {
        // 늘 적대한다 (원본과 같다).
        Always = 0,
        // 적대하지 않는다.
        Never = 1,
        // 무기를 든 상대를 보거나 그 팀에게 공격받으면 적대한다. 구역을 나가면 잊는다.
        Provoked = 2,
    }

    /// <summary>
    /// 구역 하나(같은 태그의 구역 블록들)의 처음 성질. 미션 중에는 이벤트가 바꿀 수 있고, 맵을 다시 로드하면 이 값으로 돌아간다.
    /// 판정은 사람의 발 위치로 한다. 처음 값은 전부 원본과 같은 동작이다.
    /// </summary>
    public class AreaTagData
    {
        // 에디터와 이벤트에서 알아보는 이름.
        public string name = string.Empty;
        // 구역 안의 사람이 받는 데미지 (초당 HP). 무적은 막고 방어구는 거치지 않는다.
        public float damagePerSecond;
        // 구역 안의 사람에게 거는 중력의 배율. 음수면 위로 떨어진다.
        public float gravityScale = 1f;
        // 구역 안의 AI 가 볼 수 있는 거리의 배율.
        public float aiSightScale = 1f;
        // 듣는 자리(카메라)가 구역 안이면 거는 소리의 울림 (0 에서 1 사이, 실내 효과).
        public float soundReverb;
        // 카메라가 구역 안이면 날씨(비, 눈)를 그리지 않고 날씨 소리를 줄인다 (실내).
        public bool blockWeather;
        // 구역 안의 AI 가 팀끼리 적대하는 규칙. 규칙이 없는 팀끼리는 늘 적대한다.
        public List<AreaHostilityRule> hostility = new List<AreaHostilityRule>();
    }

    /// <summary>
    /// 구역 안의 적대 규칙 하나: team 의 AI 가 targetTeam 의 사람을 어떻게 대하는가.
    /// </summary>
    public class AreaHostilityRule
    {
        public int team;
        public int targetTeam;
        public AreaHostility mode = AreaHostility.Always;
    }
}
