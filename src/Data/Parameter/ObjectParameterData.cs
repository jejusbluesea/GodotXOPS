using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 모든 오브젝트 파라미터(공용, 데이터, 모델, 콜라이더)를 담는 컨테이너 클래스.
    /// </summary>
    public class ObjectParameterData
    {
        // 각 필드는 기본값으로 초기화한다. 섹션 파일 로드가 실패(부재/빈/깨짐)해도 해당 필드는
        // 빈 기본 인스턴스로 남아 다운스트림 NullReference를 막는다(OverwriteFromJson 참조).
        public ObjectGeneralData objectGeneralData = new ObjectGeneralData();
        public List<ObjectData> objectData = new List<ObjectData>();
        public List<ObjectModelData> objectModelData = new List<ObjectModelData>();
        public List<ObjectColliderData> objectColliderData = new List<ObjectColliderData>();
    }
}
