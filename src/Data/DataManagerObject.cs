using System.Diagnostics;

namespace GodotXOPS
{
    public partial class DataManager
    {
        /// <summary>
        /// 오브젝트 파라미터 데이터를 object/ 폴더의 섹션별 JSON에서 각각 읽어 하나의 컨테이너로 병합한다.
        /// 각 파일은 컨테이너 필드명 하나로 감싼 오브젝트이며, OverwriteFromJson으로 그 필드만 채운다.
        /// </summary>
        private void LoadObjectParameterData()
        {
            ObjectParameterData = new ObjectParameterData();
            OverwriteFromJson(k_objectGeneralDataPath, ObjectParameterData);
            OverwriteFromJson(k_objectListDataPath, ObjectParameterData);
            OverwriteFromJson(k_objectModelDataPath, ObjectParameterData);
            OverwriteFromJson(k_objectColliderDataPath, ObjectParameterData);

            ValidateAddonObjectIndex();
        }

        /// <summary>
        /// 어드온 오브젝트 예약 슬롯(addonObjectIndex)이 실제 목록 범위 안을 가리키는지 에디터에서 확인한다.
        /// 모더가 list.json 항목을 지우거나 순서를 바꾸면 어드온 오브젝트가 조용히 표시되지 않으므로 원인을 미리 알린다.
        /// 익스포트 빌드에서는 호출 자체가 제거된다.
        /// </summary>
        [Conditional("TOOLS")]
        private void ValidateAddonObjectIndex()
        {
            ObjectParameterData data = ObjectParameterData;
            int index = data.objectGeneralData.addonObjectIndex;
            if (index < 0 || index >= data.objectData.Count)
            {
                Debugger.LogError($"objectGeneralData.addonObjectIndex({index}) 가 objectData 범위(0~{data.objectData.Count - 1}) 밖입니다. 맵 추가 오브젝트가 표시되지 않습니다.", nameof(DataManager));
                return;
            }

            ObjectData slot = data.objectData[index];
            if (slot.modelIndex < 0 || slot.modelIndex >= data.objectModelData.Count
                || slot.colliderIndex < 0 || slot.colliderIndex >= data.objectColliderData.Count)
            {
                Debugger.LogError($"어드온 예약 슬롯 [{index}] {slot.name} 의 modelIndex/colliderIndex 가 범위 밖입니다. 맵 추가 오브젝트가 표시되지 않습니다.", nameof(DataManager));
            }
        }
    }
}
