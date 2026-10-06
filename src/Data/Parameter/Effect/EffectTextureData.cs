namespace GodotXOPS
{
    /// <summary>
    /// 이펙트가 쓰는 텍스처 하나를 담는 데이터 클래스. emitter 의 textureIndex 가 이 항목의 목록 번호를 가리킨다.
    /// 기본 데이터의 순서는 원본 OpenXOPS Resource->LoadEffectTexture 가 하드코딩한 4 개 dds 순서를 그대로 따른다:
    /// [0]=blood, [1]=mflash, [2]=smoke, [3]=yakkyou.
    /// </summary>
    public class EffectTextureData
    {
        // 데이터 루트 기준 텍스처 경로.
        public string texturePath = string.Empty;
    }
}
