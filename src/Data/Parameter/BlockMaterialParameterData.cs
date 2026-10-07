using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 블록 재질 파라미터(공용, 재질 목록)를 담는 컨테이너 클래스.
    /// </summary>
    public class BlockMaterialParameterData
    {
        public BlockMaterialGeneralData blockMaterialGeneralData = new BlockMaterialGeneralData();
        public List<BlockMaterialData> blockMaterialData = new List<BlockMaterialData>();
    }
}
