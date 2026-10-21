namespace GodotXOPS
{
    /// <summary>
    /// 날씨 목록을 담는 컨테이너 클래스 (godotdata/weather_data.json). 원본에 없다.
    /// 10000 미만은 기본 목록, 10000 에서 19999 는 맵팩의 목록(Manifest 의 weatherDataPath), 20000 이상은 미션의 목록(MIF2 의 addonWeatherDataPath)이다.
    /// 기본 목록의 0 번은 맑음(XOPS 그대로)이다. 날씨는 연출만 하고 게임 결과에 영향을 주지 않는다.
    /// </summary>
    public class WeatherParameterData
    {
        public DataList<WeatherData> weatherData = new DataList<WeatherData>();
    }
}
