namespace GodotXOPS
{
    /// <summary>
    /// 개별 오브젝트의 모델, 콜라이더, 내구력, 피격 음향, 파괴 튐 강도 파라미터를 담는 컨테이너 클래스.
    /// </summary>
    public class ObjectData
    {
        public string name;
        public int modelIndex;
        public int colliderIndex;
        public float hp;
        public string soundPath;
        public float soundVolume;
        public int jump;
    }
}
