using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 모든 이펙트가 공유하는 텍스처 경로 목록을 담는 컨테이너 클래스.
    /// 원본 OpenXOPS Resource->LoadEffectTexture 가 하드코딩한 4 개 dds 순서를 그대로 따름:
    /// [0]=blood, [1]=mflash, [2]=smoke, [3]=yakkyou.
    /// </summary>
    public class EffectGeneralData
    {
        public List<string> texturePaths = new List<string>();
        // CollideMap 혈흔 입자가 Block 에 닿을 때 생성할 벽 데칼 이펙트 인덱스 (원본 AddMapEffect 의 하드코딩 대응). 보통 WallBlood.
        public int wallBloodEffectIndex;
    }
}
