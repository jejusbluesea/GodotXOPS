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
        public DataList<ControllerSizeData> controllerSizeData = new DataList<ControllerSizeData>();
        public DataList<HumanHitboxSizeData> humanHitboxSizeData = new DataList<HumanHitboxSizeData>();
        public HumanInteractionData humanInteractionData = new HumanInteractionData();
        public HumanAnimationData humanAnimationData = new HumanAnimationData();
        public DataList<HumanData> humanData = new DataList<HumanData>();
        public DataList<HumanModelData> humanModelData = new DataList<HumanModelData>();
        public DataList<HumanArmModelData> humanArmModelData = new DataList<HumanArmModelData>();
        public DataList<HumanLegModelData> humanLegModelData = new DataList<HumanLegModelData>();
        public DataList<HumanTypeData> humanTypeData = new DataList<HumanTypeData>();
        public HumanAIParameterData humanAIParameterData = new HumanAIParameterData();
    }
}
