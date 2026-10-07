using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GodotXOPS.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        /// <summary>
        /// 에드온 오브젝트 데이터 파일로 쓸 내용. 목록 섹션만 담는다 (에드온 데이터 파일은 전역 설정을 갖지 않는다).
        /// </summary>
        private class AddonObjectSections
        {
            public List<ObjectData> objectData = new List<ObjectData>();
            public List<ObjectModelData> objectModelData = new List<ObjectModelData>();
            public List<ObjectColliderData> objectColliderData = new List<ObjectColliderData>();
        }

        /// <summary>
        /// 지금 MapLoader 에 들어 있는 원본 형식 미션(BD1 + PD1, 공식 미션이나 MIF)을 확장 형식 한 벌(BD2, 텍스처 목록, PD2, .msg, MIF2)로 바꿔 쓴다.
        /// 같은 화면과 같은 판정이 나오는 미션이 된다. MIF 의 추가 사물(addon-object)이 있으면 에드온 오브젝트 데이터 파일로 옮기고,
        /// 그것을 가리키던 소물 포인트의 번호를 10000 으로 바꾼다.
        /// 먼저 LoadMissionData 나 LoadMissionFile 로 미션 정보를 읽어 둔다. 맵은 로드돼 있지 않아도 된다.
        /// </summary>
        /// <param name="outputFolder">파일들을 쓸 폴더 (exe 폴더 기준). 없으면 만든다.</param>
        /// <param name="baseName">파일 이름의 몸통 (확장자 없이).</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>만든 MIF2 의 경로 (exe 폴더 기준). 실패하면 null.</returns>
        public static string ConvertMissionToExtended(string outputFolder, string baseName, out string error)
        {
            error = null;
            MapLoader loader = Instance;

            if (loader.m_extendedMission)
            {
                error = "the mission is already in the extended format";
                return null;
            }

            string folder = GamePath.Resolve(outputFolder);
            if (folder == null)
            {
                error = $"the output folder is outside the game folder: {outputFolder}";
                return null;
            }

            string prefix = $"{outputFolder.Replace('\\', '/').TrimEnd('/')}/{baseName}";
            string blockRelative = prefix + BD2File.Extension;
            string texturesRelative = prefix + "_textures.json";
            string pointRelative = prefix + PD2File.Extension;
            string objectsRelative = prefix + "_objects.json";
            string missionRelative = prefix + MIF2File.Extension;

            if (!ConvertBD1(loader.m_missionBD1Path, texturesRelative, out BD2File blocks, out BlockTextureListData textures))
            {
                error = $"block data open failed: {loader.m_missionBD1Path}";
                return null;
            }
            if (!ConvertPD1(loader.m_missionPD1Path, out PD2File points))
            {
                error = $"point data open failed: {loader.m_missionPD1Path}";
                return null;
            }

            var mission = new ExtendedMissionData
            {
                name = loader.m_missionName,
                fullname = loader.m_missionFullname,
                blockPath = blockRelative,
                pointPath = pointRelative,
                skyIndex = loader.m_skyIndex,
                adjustCollision = loader.m_adjustCollision,
                darkScreen = loader.m_darkScreen,
                image0 = ToGameRelative(loader.m_missionImage0),
                image1 = ToGameRelative(loader.m_missionImage1),
                briefing = new List<string>(loader.m_missionBriefing.Split('\n')),
            };

            // 원본 MIF 의 추가 사물은 소물 목록의 예약 자리(addonObjectIndex)를 미션마다 덮어쓰는 방식이다.
            // 확장 형식에서는 에드온 오브젝트 데이터의 첫 항목(번호 10000)으로 옮긴다.
            AddonObjectSections addonObject = BuildAddonObjectSections();
            if (addonObject != null)
            {
                int legacyIndex = DataManager.Instance.ObjectParameterData.objectGeneralData.addonObjectIndex;
                foreach (PD2Point point in points.points)
                {
                    if (point.type == PointSmallObject && point.param1 == legacyIndex) point.param1 = DataList<ObjectData>.AddonBase;
                }
                mission.addonObjectDataPath = objectsRelative;
            }

            try
            {
                Directory.CreateDirectory(folder);
                if (!blocks.Write(GamePath.Resolve(blockRelative), out error)) return null;
                File.WriteAllText(GamePath.Resolve(texturesRelative), JsonData.ToJson(textures));
                if (!points.Write(GamePath.Resolve(pointRelative), out error)) return null;

                string messages = Path.ChangeExtension(loader.m_missionPD1Path, ".msg");
                string messagesCopy = Path.ChangeExtension(GamePath.Resolve(pointRelative), ".msg");
                if (File.Exists(messages)) File.Copy(messages, messagesCopy, true);
                else File.Delete(messagesCopy);

                if (addonObject != null) File.WriteAllText(GamePath.Resolve(objectsRelative), JsonData.ToJson(addonObject));
                if (!MIF2File.Write(GamePath.Resolve(missionRelative), mission, out error)) return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return null;
            }

            return missionRelative;
        }

        /// <summary>
        /// 지금 미션의 MIF 추가 사물을 에드온 오브젝트 데이터로 만든다.
        /// </summary>
        /// <returns>에드온 오브젝트 데이터. 추가 사물이 없는 미션이면 null.</returns>
        private static AddonObjectSections BuildAddonObjectSections()
        {
            if (!ParseAddonObjectFile(Instance.m_missionAddonObjectPath, out _)) return null;

            // 예약 자리를 이 미션의 추가 사물로 채운 뒤 그 내용을 떠 낸다.
            InitializeAddonObject();

            ObjectParameterData parameter = DataManager.Instance.ObjectParameterData;
            int index = parameter.objectGeneralData.addonObjectIndex;
            if (!parameter.objectData.Has(index)) return null;

            ObjectData source = parameter.objectData[index];
            if (!parameter.objectModelData.Has(source.modelIndex) || !parameter.objectColliderData.Has(source.colliderIndex)) return null;

            ObjectData data = Clone(source);
            data.modelIndex = DataList<ObjectModelData>.AddonBase;
            data.colliderIndex = DataList<ObjectColliderData>.AddonBase;

            var sections = new AddonObjectSections();
            sections.objectData.Add(data);
            sections.objectModelData.Add(Clone(parameter.objectModelData[source.modelIndex]));
            sections.objectColliderData.Add(Clone(parameter.objectColliderData[source.colliderIndex]));
            return sections;
        }

        /// <summary>
        /// 데이터 객체를 JSON 을 거쳐 복사한다.
        /// </summary>
        /// <typeparam name="T">데이터 클래스.</typeparam>
        /// <param name="value">복사할 객체.</param>
        /// <returns>내용이 같은 새 객체.</returns>
        private static T Clone<T>(T value)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonData.Options), JsonData.Options);
        }

        /// <summary>
        /// 전체 경로를 exe 폴더 기준 경로로 바꾼다.
        /// </summary>
        /// <param name="fullPath">전체 경로. 비어 있으면 빈 문자열을 돌려준다.</param>
        /// <returns>exe 폴더 기준 경로 (슬래시 구분).</returns>
        private static string ToGameRelative(string fullPath)
        {
            return string.IsNullOrEmpty(fullPath) ? string.Empty : Path.GetRelativePath(GamePath.Root, fullPath).Replace('\\', '/');
        }
    }
}
