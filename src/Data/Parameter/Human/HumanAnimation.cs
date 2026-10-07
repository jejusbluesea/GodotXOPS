using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 인간 애니메이션 프레임 인덱스와 이동 속도 정보를 담는 컨테이너 클래스.
    /// </summary>
    public class HumanAnimation
    {
        public string name;
        // 섹션 파일이 깨지거나 항목에 키가 없어도 소비자(HumanVisual)가 null 역참조하지 않도록 미리 초기화한다.
        public List<int> index = new List<int>();
        public float forwardSpeed;
        public float strafeSpeed;
        public float backwardSpeed;
        // 한 사이클 안에서 발이 땅에 닿는 순간들 (사이클 비율, 0 이상 1 미만). 발소리가 이때 난다. 비어 있으면 발소리가 없다.
        public List<float> footstepPhase = new List<float>();
    }
}
