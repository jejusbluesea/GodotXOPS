using System.Collections.Generic;

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

        // 메인 카메라 클리핑 면 (원본 CLIPPINGPLANE_NEAR/FAR ×0.1). CameraClippingApplier 가 카메라에 적용.
        public float nearClippingPlane;
        public float farClippingPlane;

        // 안개 적용 여부 (false 면 fog 미적용). fogStart~fogEnd = Linear 안개 거리 구간.
        public bool fog;
        public float fogStart;
        public float fogEnd;
    }
}
