using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 인간 팔 모델의 좌우 메시 경로를 담는 컨테이너 클래스.
    /// </summary>
    public class HumanArmModelData
    {
        public string name;
        // 섹션 파일이 깨지거나 항목에 키가 없어도 소비자(HumanVisual)가 null 역참조하지 않도록 미리 초기화한다.
        public List<string> leftArms = new List<string>();
        public List<string> rightArms = new List<string>();
    }
}
