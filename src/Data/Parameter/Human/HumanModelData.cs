using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 인간 모델의 신체, 팔, 다리 메시와 텍스처 인덱스를 담는 컨테이너 클래스.
    /// </summary>
    public class HumanModelData
    {
        public string name;
        // 섹션 파일이 깨지거나 항목에 키가 없어도 소비자(HumanVisual)가 null 역참조하지 않도록 미리 초기화한다.
        public List<string> textures = new List<string>();
        public List<ModelData> modelData = new List<ModelData>();
        public int armIndex;
        public int armTextureIndex;
        public int legIndex;
        public int legTextureIndex;
    }
}
