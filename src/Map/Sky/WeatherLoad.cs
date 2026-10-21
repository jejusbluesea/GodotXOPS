namespace GodotXOPS
{
    // MapLoader 의 날씨 담당 partial: 지금 날씨의 번호를 들고 날씨를 바꾼다. 날씨는 맵에 붙고 맵을 내리면(UnloadSkyData) 지워진다.
    // 날씨는 연출만 하고 게임 결과에 영향을 주지 않는다. 바로 바뀐다.
    public partial class MapLoader
    {
        private WeatherView m_weatherView;
        // 지금 날씨의 번호. -1 이면 날씨가 없다 (맑음과 같다).
        private int m_weatherIndex = -1;
        private WeatherData m_weather;

        // 지금 날씨의 번호. -1 이면 날씨가 없다.
        public static int WeatherIndex => Loaded ? Instance.m_weatherIndex : -1;
        // 지금 날씨. 없으면 null.
        public static WeatherData CurrentWeather => Loaded ? Instance.m_weather : null;
        // 날씨를 그리는 노드 (점검이 읽는다).
        public static WeatherView WeatherView => Instance.EnsureWeatherView();

        /// <summary>
        /// 날씨를 바꾼다. 바로 바뀐다. 날씨가 하늘 번호를 적었으면 하늘도 바꾸고, 안개는 날씨의 것으로 다시 맞춘다.
        /// 10000 미만은 weather_data.json, 10000 에서 19999 는 맵팩, 20000 이상은 미션의 목록이다.
        /// </summary>
        /// <param name="index">날씨 번호.</param>
        /// <returns>바꿨으면 true. 없는 번호면 경고하고 false (날씨는 그대로).</returns>
        public static bool SetWeather(int index)
        {
            var list = DataManager.Instance.WeatherParameterData.weatherData;
            if (!list.Has(index))
            {
                Debugger.LogWarning($"Weather {index} is not in the weather list.", nameof(MapLoader));
                return false;
            }

            MapLoader loader = Instance;
            loader.m_weatherIndex = index;
            loader.m_weather = list[index];
            loader.EnsureWeatherView().Apply(loader.m_weather);

            if (loader.m_weather.skyIndex >= 0) LoadSkyData(loader.m_weather.skyIndex);
            else RefreshFog();
            return true;
        }

        /// <summary>
        /// 날씨를 지운다 (맵을 내릴 때).
        /// </summary>
        public static void ClearWeather()
        {
            if (!Loaded) return;

            MapLoader loader = Instance;
            loader.m_weatherIndex = -1;
            loader.m_weather = null;
            loader.m_weatherView?.Clear();
        }

        /// <summary>
        /// 날씨를 그리는 노드가 없으면 만든다.
        /// </summary>
        /// <returns>노드.</returns>
        private WeatherView EnsureWeatherView()
        {
            if (m_weatherView == null)
            {
                m_weatherView = new WeatherView { Name = "Weather" };
                AddChild(m_weatherView);
            }
            return m_weatherView;
        }
    }
}
