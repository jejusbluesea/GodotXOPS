namespace GodotXOPS
{
    /// <summary>
    /// 한 체형의 총알 충돌 히트박스(머리/몸통/다리) 부위별 위치·회전·크기. per-type 목록의 한 항목이며
    /// HumanTypeData.hitboxSizeIndex 로 지정한다.
    /// </summary>
    public class HumanHitboxSizeData
    {
        public HitboxPartSizeData head;
        public HitboxPartSizeData body;
        public HitboxPartSizeData leg;
    }
}
