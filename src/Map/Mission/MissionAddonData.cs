using System.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        /// <summary>
        /// 지금 미션(MIF2)이 들고 온 에드온 데이터 파일들을 읽어 기본 데이터 목록 뒤(번호 10000 부터)에 붙인다. 사람·무기·소물을 스폰하기 전에 부른다.
        /// 에드온 파일은 기본 파라미터 파일들의 목록 섹션을 한 파일에 모은 JSON 이다. 전역 설정(GeneralData 등)은 들어 있어도 쓰지 않는다:
        /// 미션 하나가 맨손 무기 번호나 풀 크기 같은 게임 전체의 값을 바꾸면 안 된다.
        /// 경로가 비어 있는 종류와 MIF2 가 아닌 미션은 에드온이 없는 상태가 된다.
        /// </summary>
        public static void LoadAddonData()
        {
            UnloadAddonData();

            MapLoader loader = Instance;
            DataManager data = DataManager.Instance;

            HumanParameterData human = LoadAddonFile<HumanParameterData>(loader.m_addonHumanDataPath);
            if (human != null)
            {
                HumanParameterData target = data.HumanParameterData;
                target.humanData.SetAddon(human.humanData);
                target.humanModelData.SetAddon(human.humanModelData);
                target.humanArmModelData.SetAddon(human.humanArmModelData);
                target.humanLegModelData.SetAddon(human.humanLegModelData);
                target.humanTypeData.SetAddon(human.humanTypeData);
                target.controllerSizeData.SetAddon(human.controllerSizeData);
                target.humanHitboxSizeData.SetAddon(human.humanHitboxSizeData);
                target.humanAIParameterData.aiData.SetAddon(human.humanAIParameterData.aiData);
                target.humanAIParameterData.aiScopeData.SetAddon(human.humanAIParameterData.aiScopeData);
            }

            WeaponParameterData weapon = LoadAddonFile<WeaponParameterData>(loader.m_addonWeaponDataPath);
            if (weapon != null)
            {
                WeaponParameterData target = data.WeaponParameterData;
                target.weaponData.SetAddon(weapon.weaponData);
                target.bulletData.SetAddon(weapon.bulletData);
                target.scopeData.SetAddon(weapon.scopeData);
                target.weaponModelData.SetAddon(weapon.weaponModelData);
            }

            ObjectParameterData smallObject = LoadAddonFile<ObjectParameterData>(loader.m_addonObjectDataPath);
            if (smallObject != null)
            {
                ObjectParameterData target = data.ObjectParameterData;
                target.objectData.SetAddon(smallObject.objectData);
                target.objectModelData.SetAddon(smallObject.objectModelData);
                target.objectColliderData.SetAddon(smallObject.objectColliderData);
            }

            EffectParameterData effect = LoadAddonFile<EffectParameterData>(loader.m_addonEffectDataPath);
            if (effect != null)
            {
                EffectParameterData target = data.EffectParameterData;
                target.effectData.SetAddon(effect.effectData);
                target.effectTextureData.SetAddon(effect.effectTextureData);
            }

            BlockMaterialParameterData material = LoadAddonFile<BlockMaterialParameterData>(loader.m_addonBlockMaterialDataPath);
            if (material != null)
            {
                data.BlockMaterialParameterData.blockMaterialData.SetAddon(material.blockMaterialData);
            }

            SoundParameterData sound = LoadAddonFile<SoundParameterData>(loader.m_addonSoundDataPath);
            if (sound != null)
            {
                data.SoundParameterData.soundData.SetAddon(sound.soundData);
            }
        }

        /// <summary>
        /// 모든 데이터 목록에서 에드온 데이터를 뗀다. 맵을 내릴 때 부른다. 에드온 번호로 만들어 둔 이펙트 머티리얼도 같이 버린다 (같은 번호가 다음 미션에서는 다른 텍스처다).
        /// </summary>
        public static void UnloadAddonData()
        {
            DataManager data = DataManager.Instance;

            HumanParameterData human = data.HumanParameterData;
            human.humanData.ClearAddon();
            human.humanModelData.ClearAddon();
            human.humanArmModelData.ClearAddon();
            human.humanLegModelData.ClearAddon();
            human.humanTypeData.ClearAddon();
            human.controllerSizeData.ClearAddon();
            human.humanHitboxSizeData.ClearAddon();
            human.humanAIParameterData.aiData.ClearAddon();
            human.humanAIParameterData.aiScopeData.ClearAddon();

            WeaponParameterData weapon = data.WeaponParameterData;
            weapon.weaponData.ClearAddon();
            weapon.bulletData.ClearAddon();
            weapon.scopeData.ClearAddon();
            weapon.weaponModelData.ClearAddon();

            ObjectParameterData smallObject = data.ObjectParameterData;
            smallObject.objectData.ClearAddon();
            smallObject.objectModelData.ClearAddon();
            smallObject.objectColliderData.ClearAddon();

            EffectParameterData effect = data.EffectParameterData;
            effect.effectData.ClearAddon();
            effect.effectTextureData.ClearAddon();

            data.BlockMaterialParameterData.blockMaterialData.ClearAddon();
            data.SoundParameterData.soundData.ClearAddon();

            if (EffectManager.Loaded) EffectManager.Instance.ClearAddonMaterials();
        }

        /// <summary>
        /// 에드온 데이터 파일 하나를 읽는다.
        /// </summary>
        /// <typeparam name="T">그 종류의 파라미터 컨테이너. 기본 데이터와 같은 클래스를 쓴다.</typeparam>
        /// <param name="relativePath">exe 폴더 기준 경로. 비어 있으면 읽지 않는다.</param>
        /// <returns>읽은 내용. 경로가 비었거나 파일이 없거나 JSON 이 아니면 null.</returns>
        private static T LoadAddonFile<T>(string relativePath) where T : class, new()
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                Debugger.LogError($"Add-on data open failed: {relativePath}", nameof(MapLoader));
                return null;
            }

            try
            {
                var result = new T();
                return JsonData.Overwrite(EncodingHelper.ReadAllText(fullPath), result, relativePath) ? result : null;
            }
            catch (IOException e)
            {
                Debugger.LogError($"Add-on data read failed: {relativePath} ({e.Message})", nameof(MapLoader));
                return null;
            }
        }
    }
}
