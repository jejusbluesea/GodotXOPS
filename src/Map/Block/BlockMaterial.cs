using System.Collections.Generic;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        // 목록에 없는 번호가 가리키는 재질. 소리도 이펙트도 없다.
        private static readonly BlockMaterialData s_emptyMaterial = new BlockMaterialData();

        // 재질이 없는 맵(BD1)의 모든 면이 쓰는 재질 번호. 원본의 벽 착탄 연기와 착탄음이 이 재질에 들어 있다.
        private const int k_legacyMaterialIndex = 0;

        // 면 재질 번호 -1 이 가리키는 재질 번호.
        private int m_defaultBlockMaterial;

        // 면 재질 번호 -1 이 가리키는 재질 번호. 확장 미션 데이터가 정한다 (기본 0).
        public static int DefaultBlockMaterial
        {
            get => Instance.m_defaultBlockMaterial;
            set => Instance.m_defaultBlockMaterial = value;
        }

        /// <summary>
        /// 블록 면의 재질을 얻는다.
        /// </summary>
        /// <param name="block">블록.</param>
        /// <param name="face">면 번호 (0 에서 5).</param>
        /// <returns>재질. null 이 아니다. 재질 번호가 없는 맵(BD1)의 블록은 0번 재질이고, 10000 이상은 미션의 에드온 재질이며, 번호가 목록에 없으면 소리도 이펙트도 없는 빈 재질이다.</returns>
        public static BlockMaterialData GetFaceMaterial(Block block, int face)
        {
            int index = k_legacyMaterialIndex;
            if (block?.faceMaterials != null && face >= 0 && face < block.faceMaterials.Length)
            {
                index = block.faceMaterials[face];
                if (index == -1) index = Instance.m_defaultBlockMaterial;
            }

            DataList<BlockMaterialData> materials = DataManager.Instance.BlockMaterialParameterData.blockMaterialData;
            return materials.Has(index) ? materials[index] : s_emptyMaterial;
        }
    }
}
