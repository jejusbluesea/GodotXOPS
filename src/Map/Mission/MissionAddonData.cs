using System.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        // 지금 미션이 속한 맵팩. 맵팩에 속하지 않은 미션이면 null.
        private MapPackManifestData m_mapPack;

        // 지금 미션이 속한 맵팩. 맵팩 데이터(번호 10000 에서 19999)가 붙어 있는 동안만 있다. 없으면 null.
        public static MapPackManifestData MapPack => Instance.m_mapPack;

        /// <summary>
        /// 지금 미션(MIF2)이 들고 온 에드온 데이터 파일들(사람, 무기, 오브젝트, 소리)을 읽어 기본 데이터 목록 뒤(번호 20000 부터)에 붙인다. 사람·무기·소물을 스폰하기 전에 부른다.
        /// 에드온 파일은 기본 파라미터 파일들의 목록 섹션을 한 파일에 모은 JSON 이다. 전역 설정(GeneralData 등)은 들어 있어도 쓰지 않는다:
        /// 미션 하나가 맨손 무기 번호나 풀 크기 같은 게임 전체의 값을 바꾸면 안 된다.
        /// 경로가 비어 있는 종류와 MIF2 가 아닌 미션은 미션 데이터가 없는 상태가 된다. 이미지는 미션 정보와 함께 붙는다 (LoadMissionImages).
        /// </summary>
        public static void LoadAddonData()
        {
            UnloadAddonData();

            MapLoader loader = Instance;
            DataManager data = DataManager.Instance;

            HumanParameterData human = LoadAddonFile<HumanParameterData>(loader.m_addonHumanDataPath);
            if (human != null) SetHumanData(data.HumanParameterData, human, false);

            WeaponParameterData weapon = LoadAddonFile<WeaponParameterData>(loader.m_addonWeaponDataPath);
            if (weapon != null) SetWeaponData(data.WeaponParameterData, weapon, false);

            ObjectParameterData smallObject = LoadAddonFile<ObjectParameterData>(loader.m_addonObjectDataPath);
            if (smallObject != null) SetObjectData(data.ObjectParameterData, smallObject, false);

            SoundParameterData sound = LoadAddonFile<SoundParameterData>(loader.m_addonSoundDataPath);
            if (sound != null) data.SoundParameterData.soundData.SetMission(sound.soundData);
        }

        /// <summary>
        /// 모든 데이터 목록에서 미션 데이터(20000 부터)를 뗀다. 맵을 내릴 때 부른다. 맵팩 데이터와 미션의 이미지는 남는다 (미션 정보와 함께 붙고 떨어진다).
        /// </summary>
        public static void UnloadAddonData()
        {
            DataManager data = DataManager.Instance;
            SetHumanData(data.HumanParameterData, null, false);
            SetWeaponData(data.WeaponParameterData, null, false);
            SetObjectData(data.ObjectParameterData, null, false);
            data.SoundParameterData.soundData.ClearMission();
        }

        /// <summary>
        /// 미션이 속한 맵팩의 데이터 9종을 읽어 번호 10000 부터에 붙인다. 미션 정보를 읽을 때 부른다:
        /// 맵이 없는 브리핑과 맵을 내린 결과 화면에서도 맵팩의 이미지와 하늘을 써야 한다. 이벤트 묶음은 경로만 들고 포인트를 로드할 때 올린다.
        /// </summary>
        /// <param name="pack">맵팩. null 이면 맵팩 데이터가 없는 상태가 된다.</param>
        private static void LoadPackData(MapPackManifestData pack)
        {
            UnloadPackData();
            if (pack == null) return;

            MapLoader loader = Instance;
            DataManager data = DataManager.Instance;
            loader.m_mapPack = pack;

            HumanParameterData human = LoadAddonFile<HumanParameterData>(pack.humanDataPath);
            if (human != null) SetHumanData(data.HumanParameterData, human, true);

            WeaponParameterData weapon = LoadAddonFile<WeaponParameterData>(pack.weaponDataPath);
            if (weapon != null) SetWeaponData(data.WeaponParameterData, weapon, true);

            ObjectParameterData smallObject = LoadAddonFile<ObjectParameterData>(pack.objectDataPath);
            if (smallObject != null) SetObjectData(data.ObjectParameterData, smallObject, true);

            EffectParameterData effect = LoadAddonFile<EffectParameterData>(pack.effectDataPath);
            if (effect != null)
            {
                data.EffectParameterData.effectData.SetPack(effect.effectData);
                data.EffectParameterData.effectTextureData.SetPack(effect.effectTextureData);
            }

            BlockMaterialParameterData material = LoadAddonFile<BlockMaterialParameterData>(pack.blockMaterialDataPath);
            if (material != null) data.BlockMaterialParameterData.blockMaterialData.SetPack(material.blockMaterialData);

            SoundParameterData sound = LoadAddonFile<SoundParameterData>(pack.soundDataPath);
            if (sound != null) data.SoundParameterData.soundData.SetPack(sound.soundData);

            ImageParameterData image = LoadAddonFile<ImageParameterData>(pack.imageDataPath);
            if (image != null) data.ImageParameterData.imageData.SetPack(image.imageData);

            LoadPackSky(pack.skyDataPath);
        }

        /// <summary>
        /// 맵팩 데이터(10000 에서 19999)를 전부 뗀다. 미션 정보를 내릴 때 부른다. 에드온 번호로 만들어 둔 이펙트 머티리얼도 같이 버린다 (같은 번호가 다른 맵팩에서는 다른 텍스처다).
        /// </summary>
        private static void UnloadPackData()
        {
            MapLoader loader = Instance;
            loader.m_mapPack = null;
            loader.m_packSky = null;
            if (!DataManager.Loaded) return;

            DataManager data = DataManager.Instance;
            SetHumanData(data.HumanParameterData, null, true);
            SetWeaponData(data.WeaponParameterData, null, true);
            SetObjectData(data.ObjectParameterData, null, true);
            data.EffectParameterData.effectData.ClearPack();
            data.EffectParameterData.effectTextureData.ClearPack();
            data.BlockMaterialParameterData.blockMaterialData.ClearPack();
            data.SoundParameterData.soundData.ClearPack();
            data.ImageParameterData.imageData.ClearPack();

            if (EffectManager.Loaded) EffectManager.Instance.ClearAddonMaterials();
        }

        /// <summary>
        /// 사람 데이터의 목록들에 맵팩이나 미션의 항목을 붙이거나 뗀다.
        /// </summary>
        /// <param name="target">게임의 사람 데이터.</param>
        /// <param name="source">붙일 에드온 파일의 내용. null 이면 뗀다.</param>
        /// <param name="pack">true 면 맵팩 층(10000 부터), false 면 미션 층(20000 부터).</param>
        private static void SetHumanData(HumanParameterData target, HumanParameterData source, bool pack)
        {
            SetLayer(target.humanData, source?.humanData, pack);
            SetLayer(target.humanModelData, source?.humanModelData, pack);
            SetLayer(target.humanArmModelData, source?.humanArmModelData, pack);
            SetLayer(target.humanLegModelData, source?.humanLegModelData, pack);
            SetLayer(target.humanTypeData, source?.humanTypeData, pack);
            SetLayer(target.controllerSizeData, source?.controllerSizeData, pack);
            SetLayer(target.humanHitboxSizeData, source?.humanHitboxSizeData, pack);
            SetLayer(target.humanAIParameterData.aiData, source?.humanAIParameterData.aiData, pack);
            SetLayer(target.humanAIParameterData.aiScopeData, source?.humanAIParameterData.aiScopeData, pack);
        }

        /// <summary>
        /// 무기 데이터의 목록들에 맵팩이나 미션의 항목을 붙이거나 뗀다.
        /// </summary>
        /// <param name="target">게임의 무기 데이터.</param>
        /// <param name="source">붙일 에드온 파일의 내용. null 이면 뗀다.</param>
        /// <param name="pack">true 면 맵팩 층(10000 부터), false 면 미션 층(20000 부터).</param>
        private static void SetWeaponData(WeaponParameterData target, WeaponParameterData source, bool pack)
        {
            SetLayer(target.weaponData, source?.weaponData, pack);
            SetLayer(target.bulletData, source?.bulletData, pack);
            SetLayer(target.scopeData, source?.scopeData, pack);
            SetLayer(target.weaponModelData, source?.weaponModelData, pack);
        }

        /// <summary>
        /// 오브젝트 데이터의 목록들에 맵팩이나 미션의 항목을 붙이거나 뗀다.
        /// </summary>
        /// <param name="target">게임의 오브젝트 데이터.</param>
        /// <param name="source">붙일 에드온 파일의 내용. null 이면 뗀다.</param>
        /// <param name="pack">true 면 맵팩 층(10000 부터), false 면 미션 층(20000 부터).</param>
        private static void SetObjectData(ObjectParameterData target, ObjectParameterData source, bool pack)
        {
            SetLayer(target.objectData, source?.objectData, pack);
            SetLayer(target.objectModelData, source?.objectModelData, pack);
            SetLayer(target.objectColliderData, source?.objectColliderData, pack);
        }

        /// <summary>
        /// 목록 하나의 맵팩 층이나 미션 층을 바꾼다.
        /// </summary>
        /// <typeparam name="T">항목의 형식.</typeparam>
        /// <param name="target">게임의 목록.</param>
        /// <param name="items">붙일 항목들. null 이면 뗀다.</param>
        /// <param name="pack">true 면 맵팩 층, false 면 미션 층.</param>
        private static void SetLayer<T>(DataList<T> target, DataList<T> items, bool pack)
        {
            if (pack) target.SetPack(items);
            else target.SetMission(items);
        }

        /// <summary>
        /// 에드온 데이터 파일 하나를 읽는다.
        /// </summary>
        /// <typeparam name="T">그 종류의 파라미터 컨테이너. 기본 데이터와 같은 클래스를 쓴다.</typeparam>
        /// <param name="relativePath">exe 폴더 기준 경로. 비어 있으면 읽지 않는다.</param>
        /// <returns>읽은 내용. 경로가 비었거나 파일이 없거나 JSON 이 아니면 null.</returns>
        public static T LoadAddonFile<T>(string relativePath) where T : class, new()
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
