using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. godotdata/ 의 각 JSON 파일을 DataManager 가 로드한 값과 키 단위로 대조해
    /// 데이터 클래스에 없는 키(누락된 필드)와 값 불일치를 찾아 출력한 뒤 종료한다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/data_check.tscn
    /// </summary>
    public partial class DataCheck : Node
    {
        private int m_checkedValues;
        private readonly List<string> m_problems = new List<string>();
        private readonly SortedSet<string> m_defaults = new SortedSet<string>();

        public override void _Ready()
        {
            DataManager data = DataManager.Instance;
            string root = GamePath.Resolve(GamePath.GodotDataFolder);
            int fileCount = 0;

            foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                object target = PickTarget(data, relative);
                if (target == null)
                {
                    continue;
                }

                fileCount++;
                JsonNode source = JsonNode.Parse(EncodingHelper.ReadAllText(path), null, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                JsonNode loaded = JsonSerializer.SerializeToNode(target, target.GetType(), JsonData.Options);
                Compare(source, loaded, relative, true);
            }

            GD.Print($"파일 {fileCount}개, 값 {m_checkedValues}개 대조 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            if (m_defaults.Count > 0)
            {
                GD.Print($"JSON 에 없어 기본값으로 남은 필드: {string.Join(", ", m_defaults)}");
            }

            GD.Print($"human {data.HumanParameterData.humanData.Count}, humanModel {data.HumanParameterData.humanModelData.Count}, " +
                $"weapon {data.WeaponParameterData.weaponData.Count}, weaponModel {data.WeaponParameterData.weaponModelData.Count}, " +
                $"bullet {data.WeaponParameterData.bulletData.Count}, scope {data.WeaponParameterData.scopeData.Count}, " +
                $"object {data.ObjectParameterData.objectData.Count}, effect {data.EffectParameterData.effectData.Count}, " +
                $"sky {data.SkyData.skyTexturePath.Count}, officialMission {data.MissionData.officialMissions.Count}, demo {data.MissionData.demoData.Count}");
            for (int page = 0; page < data.MissionData.addonMissions.Count; page++)
            {
                List<AddonMissionData> missions = data.MissionData.addonMissions[page];
                GD.Print($"addon 페이지 {page} \"{data.MissionData.addonPageNames[page]}\": {missions.Count}개 — {string.Join(", ", missions.ConvertAll(m => m.name))}");
            }
            GD.Print($"global: {data.GlobalData.productName} {data.GlobalData.Version}");

            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// godotdata 기준 상대 경로로 그 파일이 채우는 컨테이너를 고른다.
        /// </summary>
        /// <param name="data">DataManager 인스턴스.</param>
        /// <param name="relative">godotdata 기준 상대 경로.</param>
        /// <returns>대응하는 컨테이너. DataManager 가 다루지 않는 파일이면 null.</returns>
        private static object PickTarget(DataManager data, string relative)
        {
            if (relative.StartsWith("human/")) return data.HumanParameterData;
            if (relative.StartsWith("weapon/")) return data.WeaponParameterData;
            if (relative.StartsWith("object/")) return data.ObjectParameterData;

            switch (relative)
            {
                case "effect_data.json": return data.EffectParameterData;
                case "sky_data.json": return data.SkyData;
                case "mission_data.json": return data.MissionData;
                case "global.json": return data.GlobalData;
                default: return null;
            }
        }

        /// <summary>
        /// 원본 JSON 노드와 로드된 값을 재귀적으로 대조한다. 원본에 있는 모든 키가 같은 값으로 로드됐는지 확인한다.
        /// </summary>
        /// <param name="source">파일에서 읽은 노드.</param>
        /// <param name="loaded">로드된 객체를 다시 직렬화한 노드.</param>
        /// <param name="path">문제 보고용 위치 문자열.</param>
        /// <param name="isFileRoot">섹션 파일의 루트면 true. 루트에서는 다른 파일이 채우는 필드를 기본값 목록에 넣지 않는다.</param>
        private void Compare(JsonNode source, JsonNode loaded, string path, bool isFileRoot)
        {
            if (source is JsonObject sourceObject)
            {
                if (loaded is not JsonObject loadedObject)
                {
                    m_problems.Add($"{path}: 오브젝트가 아닌 값으로 로드됨");
                    return;
                }

                foreach (KeyValuePair<string, JsonNode> pair in sourceObject)
                {
                    if (!loadedObject.ContainsKey(pair.Key))
                    {
                        m_problems.Add($"{path}.{pair.Key}: 데이터 클래스에 없는 키");
                        continue;
                    }
                    Compare(pair.Value, loadedObject[pair.Key], $"{path}.{pair.Key}", false);
                }

                if (!isFileRoot)
                {
                    foreach (KeyValuePair<string, JsonNode> pair in loadedObject)
                    {
                        if (!sourceObject.ContainsKey(pair.Key))
                        {
                            m_defaults.Add(StripIndices($"{path}.{pair.Key}"));
                        }
                    }
                }
                return;
            }

            if (source is JsonArray sourceArray)
            {
                if (loaded is not JsonArray loadedArray || loadedArray.Count != sourceArray.Count)
                {
                    m_problems.Add($"{path}: 배열 길이 불일치 (파일 {sourceArray.Count})");
                    return;
                }

                for (int i = 0; i < sourceArray.Count; i++)
                {
                    Compare(sourceArray[i], loadedArray[i], $"{path}[{i}]", false);
                }
                return;
            }

            m_checkedValues++;
            if (!ValuesEqual(source, loaded))
            {
                m_problems.Add($"{path}: 파일 {source?.ToJsonString() ?? "null"} ≠ 로드 {loaded?.ToJsonString() ?? "null"}");
            }
        }

        /// <summary>
        /// 단일 값 두 개가 같은지 비교한다. 숫자는 float 정밀도 오차를 허용한다.
        /// </summary>
        /// <param name="source">파일의 값.</param>
        /// <param name="loaded">로드된 값.</param>
        /// <returns>같으면 true.</returns>
        private static bool ValuesEqual(JsonNode source, JsonNode loaded)
        {
            if (source == null)
            {
                // 파일의 null 은 기본값 유지로 처리되므로 무엇으로 로드됐든 정상이다.
                return true;
            }
            if (loaded == null)
            {
                return false;
            }

            JsonElement a = source.GetValue<JsonElement>();
            JsonElement b = JsonSerializer.SerializeToElement(loaded);
            if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number)
            {
                double x = a.GetDouble();
                double y = b.GetDouble();
                return Math.Abs(x - y) <= 1e-5 * Math.Max(1.0, Math.Abs(x));
            }
            if (a.ValueKind != b.ValueKind)
            {
                return false;
            }
            return a.ValueKind != JsonValueKind.String || a.GetString() == b.GetString();
        }

        /// <summary>
        /// 위치 문자열에서 배열 인덱스를 지워 같은 필드가 한 번만 보고되게 한다.
        /// </summary>
        /// <param name="path">위치 문자열.</param>
        /// <returns>인덱스가 [] 로 바뀐 문자열.</returns>
        private static string StripIndices(string path)
        {
            return System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", "[]");
        }
    }
}
