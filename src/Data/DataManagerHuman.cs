namespace GodotXOPS
{
    public partial class DataManager
    {
        /// <summary>
        /// 인간 파라미터 데이터를 human/ 폴더의 섹션별 JSON에서 각각 읽어 하나의 컨테이너로 병합한다.
        /// 각 파일은 컨테이너 필드명 하나로 감싼 오브젝트이며, OverwriteFromJson으로 그 필드만 채운다.
        /// </summary>
        private void LoadHumanParameterData()
        {
            HumanParameterData = new HumanParameterData();
            OverwriteFromJson(k_humanGeneralDataPath, HumanParameterData);
            OverwriteFromJson(k_humanControllerDataPath, HumanParameterData);
            OverwriteFromJson(k_humanHitboxSizeDataPath, HumanParameterData);
            OverwriteFromJson(k_humanInteractionDataPath, HumanParameterData);
            OverwriteFromJson(k_humanAnimationDataPath, HumanParameterData);
            OverwriteFromJson(k_humanListDataPath, HumanParameterData);
            OverwriteFromJson(k_humanModelDataPath, HumanParameterData);
            OverwriteFromJson(k_humanArmModelDataPath, HumanParameterData);
            OverwriteFromJson(k_humanLegModelDataPath, HumanParameterData);
            OverwriteFromJson(k_humanTypeDataPath, HumanParameterData);
            OverwriteFromJson(k_humanAIParameterDataPath, HumanParameterData);
        }
    }
}
