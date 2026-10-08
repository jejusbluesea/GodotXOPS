namespace GodotXOPS
{
    /// <summary>
    /// 이벤트가 번호로 재생하는 소리의 목록을 담는 컨테이너 클래스. 스크립트 이벤트는 파일 경로를 받지 않으므로 소리도 다른 데이터처럼 번호로 가리킨다.
    /// 10000 미만은 기본 목록(godotdata/sound_data.json), 10000 이상은 미션의 에드온 목록(MIF2 의 addonSoundDataPath)이다.
    /// </summary>
    public class SoundParameterData
    {
        public DataList<SoundData> soundData = new DataList<SoundData>();
    }
}
