using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 모든 인간 캐릭터 파라미터를 섹션별로 담는 컨테이너 클래스.
    /// </summary>
    public class HumanParameterData
    {
        // 각 필드는 기본값으로 초기화한다. 섹션 파일 로드가 실패(부재/빈/깨짐)해도 해당 필드는
        // 빈 기본 인스턴스로 남아 다운스트림 NullReference를 막는다(OverwriteFromJson 참조).
        public HumanGeneralData humanGeneralData = new HumanGeneralData();
        public HumanControllerData humanControllerData = new HumanControllerData();
        public List<ControllerSizeData> controllerSizeData = new List<ControllerSizeData>();
        public List<HumanHitboxSizeData> humanHitboxSizeData = new List<HumanHitboxSizeData>();
        public HumanInteractionData humanInteractionData = new HumanInteractionData();
        public HumanAnimationData humanAnimationData = new HumanAnimationData();
        public List<HumanData> humanData = new List<HumanData>();
        public List<HumanModelData> humanModelData = new List<HumanModelData>();
        public List<HumanArmModelData> humanArmModelData = new List<HumanArmModelData>();
        public List<HumanLegModelData> humanLegModelData = new List<HumanLegModelData>();
        public List<HumanTypeData> humanTypeData = new List<HumanTypeData>();
        public HumanAIParameterData humanAIParameterData = new HumanAIParameterData();
    }
}
