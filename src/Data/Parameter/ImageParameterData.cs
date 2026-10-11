namespace GodotXOPS
{
    /// <summary>
    /// 이벤트와 화면 스크립트가 번호로 그리는 이미지의 목록을 담는 컨테이너 클래스. 스크립트는 파일 경로를 받지 않으므로 이미지도 다른 데이터처럼 번호로 가리킨다.
    /// 10000 미만은 기본 목록(godotdata/image_data.json), 10000 이상은 미션의 에드온 목록(MIF2 의 addonImageDataPath)이다.
    /// 미션의 목록은 다른 에드온 데이터와 달리 미션 정보와 함께 붙고 떨어진다: 맵이 내려간 결과 화면에서도 써야 한다.
    /// </summary>
    public class ImageParameterData
    {
        public DataList<ImageData> imageData = new DataList<ImageData>();
    }
}
