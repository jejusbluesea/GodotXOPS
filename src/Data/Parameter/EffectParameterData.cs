using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 모든 이펙트 파라미터(공용, 데이터)를 담는 컨테이너 클래스.
    /// </summary>
    public class EffectParameterData
    {
        // 파일 로드가 실패(부재/빈/깨짐)해도 소비자(EffectManager)가 null 역참조하지 않도록 기본값으로 초기화한다.
        public EffectGeneralData effectGeneralData = new EffectGeneralData();
        public List<EffectTextureData> effectTextureData = new List<EffectTextureData>();
        public List<EffectData> effectData = new List<EffectData>();
    }
}
