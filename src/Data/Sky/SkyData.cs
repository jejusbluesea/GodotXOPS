using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 스카이박스 메시 경로와 텍스처 경로 목록을 담는 데이터 클래스.
    /// </summary>
    public class SkyData
    {
        // 리스트는 파일 로드가 실패(부재/빈/깨짐)해도 소비자(SkyLoad)가 null 역참조하지 않도록 기본값으로 초기화한다.
        public string skyMeshPath;
        public List<string> skyTexturePath = new List<string>();

        // sky 번호별 fog 색 (skyTexturePath 와 동일 인덱스). 원본 SetFog skycolor switch (d3dgraphics-directx.cpp:1287-1292).
        public List<Color32> skyColor = new List<Color32>();

        // 안개 적용 여부 (false 면 fog 미적용). fogStart~fogEnd = Linear 안개 거리 구간.
        public bool fog;
        public float fogStart;
        public float fogEnd;

        // sky 번호별 겹쳐 그리는 것 (skyTexturePath 와 동일 인덱스). 목록에 없는 번호는 하늘만 그린다 (원본 그대로).
        public List<SkyLayerData> skyLayerData = new List<SkyLayerData>();
    }

    /// <summary>
    /// 하늘 하나에 겹쳐 그리는 풍경과 해·달. 원본에 없다.
    /// </summary>
    public class SkyLayerData
    {
        // 하늘 위에 겹치는 풍경(도시, 산). 하늘과 같은 모델에 입히고 텍스처의 알파로 섞는다. 비우면 없다.
        public string billboardTexturePath = string.Empty;
        // 해·달. 늘 카메라를 보는 사각형이고 풍경보다 뒤에 그린다. 비우면 없다.
        public string sunTexturePath = string.Empty;
        // 해가 있는 방향 (UnityXOPS 공간: +x 오른쪽, +y 위, +z 앞). 길이는 쓰지 않는다.
        public Vector3 sunDirection = new Vector3(0f, 1f, 1f);
        // 해의 겉보기 크기 (도. 사각형의 한 변이 이 각도다).
        public float sunSize = 10f;
    }
}
