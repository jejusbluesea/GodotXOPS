using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 내리는 것의 종류. 비는 세로로 긴 줄, 눈은 둥근 점으로 그린다 (텍스처를 주면 그 그림).
    /// </summary>
    public enum WeatherPrecipitation
    {
        None = 0,
        Rain = 1,
        Snow = 2,
    }

    /// <summary>
    /// 날씨 하나. 내리는 것(비, 눈), 안개 덮어쓰기, 하늘 바꾸기, 반복 소리, 번개를 정한다. 전부 연출이고 게임 결과에 영향을 주지 않는다.
    /// 날씨는 바로 바뀐다 (서서히 바뀌는 것은 없다. 원하면 이벤트의 암전으로 가린다).
    /// </summary>
    public class WeatherData
    {
        // 에디터와 문서에서 알아보는 이름.
        public string name = string.Empty;
        // 이 날씨가 되면 바꿀 하늘 번호. -1 이면 하늘을 그대로 둔다.
        public int skyIndex = -1;

        public WeatherPrecipitation precipitation = WeatherPrecipitation.None;
        // 내리는 것의 그림 (알파로 섞는다). 비우면 흰 사각형이다.
        public string particleTexturePath = string.Empty;
        // 내리는 것의 색과 진하기 (0 에서 255 사이).
        public Color32 particleColor = new Color32 { r = 255, g = 255, b = 255, a = 128 };
        // 카메라 둘레에 동시에 떠 있는 수.
        public int particleAmount = 1000;
        // 하나의 너비와 길이 (m). 비는 길이가 너비보다 길다.
        public float particleWidth = 0.02f;
        public float particleLength = 0.5f;
        // 떨어지는 속도 (m/s)와 바람 (m/s, UnityXOPS 공간).
        public float fallSpeed = 10f;
        public Vector3 wind = Vector3.Zero;
        // 카메라 둘레에서 내리는 범위: 가로 반지름과 높이 (m).
        public float areaRadius = 15f;
        public float areaHeight = 12f;

        // true 면 하늘의 안개 대신 이 안개를 쓴다. 설정에서 안개를 끄면 이것도 그리지 않는다.
        public bool fogOverride;
        public Color32 fogColor;
        public float fogStart = 10f;
        public float fogEnd = 79f;

        // 되풀이해 트는 소리 (소리 목록의 번호, -1 이면 없음)와 크기. 거리와 무관하게 들린다.
        public int soundIndex = -1;
        public float soundVolume = 1f;

        // 번개가 치는 간격 (초, 그 사이의 난수). 최대가 0 이면 번개가 없다.
        public float lightningIntervalMin;
        public float lightningIntervalMax;
        // 번쩍이는 색 (a 가 가장 밝을 때의 진하기)과 사라지는 데 걸리는 시간 (초).
        public Color32 lightningColor = new Color32 { r = 255, g = 255, b = 255, a = 160 };
        public float lightningDuration = 0.3f;
        // 번개 소리 (소리 목록의 번호, -1 이면 없음)와 번쩍인 뒤 소리가 나기까지의 시간 (초).
        public int lightningSoundIndex = -1;
        public float lightningSoundDelay = 0.5f;
    }
}
