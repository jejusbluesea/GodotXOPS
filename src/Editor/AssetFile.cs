using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터가 연 데이터 파일(JSON) 하나. 게임이 그 파일을 읽는 데이터 클래스에 담아 들고, 파일에 있던 최상위 키만 다시 쓴다
    /// (기본 데이터는 한 클래스를 여러 파일에 나눠 적고, 에드온 데이터 파일은 목록 섹션만 갖기 때문이다).
    /// </summary>
    public sealed class AssetFile
    {
        /// <summary>
        /// 데이터 파일의 종류: 화면에 보일 이름과 그 파일을 담는 데이터 클래스.
        /// </summary>
        public readonly struct Kind
        {
            public readonly string Name;
            public readonly Type Type;

            public Kind(string name, Type type)
            {
                Name = name;
                Type = type;
            }
        }

        // 에디터가 아는 종류들. 파일의 종류는 최상위 키로 알아낸다 (키를 가장 많이 가진 것. 같으면 앞의 것).
        public static readonly Kind[] Kinds =
        {
            new Kind("Human data", typeof(HumanParameterData)),
            new Kind("Weapon data", typeof(WeaponParameterData)),
            new Kind("Object data", typeof(ObjectParameterData)),
            new Kind("Effect data", typeof(EffectParameterData)),
            new Kind("Block material data", typeof(BlockMaterialParameterData)),
            new Kind("Sound data", typeof(SoundParameterData)),
            new Kind("Event pack", typeof(EventPackData)),
            new Kind("Sky data", typeof(SkyData)),
            new Kind("Mission list", typeof(MissionData)),
            new Kind("Global data", typeof(GlobalData)),
            new Kind("Block texture list", typeof(BlockTextureListData)),
        };

        // 파일 경로 (exe 폴더 기준).
        public string Path { get; private set; }
        public Kind FileKind { get; private set; }
        // 파일의 내용을 담은 데이터 객체. 파일에 없던 키의 필드는 기본값이고 쓰지 않는다.
        public object Container { get; private set; }
        // 파일에 있는 최상위 키들 (파일에 적힌 순서).
        public List<FieldInfo> Fields { get; } = new List<FieldInfo>();
        // 파일에 있었지만 이 종류의 데이터 클래스에 없는 키의 수. 저장하면 사라진다.
        public int UnknownKeys { get; private set; }
        public bool Dirty;

        /// <summary>
        /// 데이터 클래스의 필드 가운데 JSON 에 쓰이는 것들.
        /// </summary>
        /// <param name="type">데이터 클래스.</param>
        /// <returns>필드들 (선언 순서).</returns>
        public static List<FieldInfo> DataFields(Type type)
        {
            var result = new List<FieldInfo>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!field.IsDefined(typeof(JsonIgnoreAttribute))) result.Add(field);
            }
            return result;
        }

        /// <summary>
        /// 데이터 파일을 읽는다.
        /// </summary>
        /// <param name="relativePath">파일 경로 (exe 폴더 기준).</param>
        /// <param name="file">읽은 파일. 실패하면 null.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>읽었으면 true.</returns>
        public static bool Load(string relativePath, out AssetFile file, out string error)
        {
            file = null;
            error = null;
            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                error = $"file open failed: {relativePath}";
                return false;
            }

            string text;
            var keys = new List<string>();
            try
            {
                text = EncodingHelper.ReadAllText(fullPath);
                using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = "the file is not a JSON object";
                    return false;
                }
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    keys.Add(property.Name);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                error = e.Message;
                return false;
            }

            Kind best = default;
            int bestCount = 0;
            foreach (Kind kind in Kinds)
            {
                int count = 0;
                foreach (string key in keys)
                {
                    FieldInfo field = kind.Type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && !field.IsDefined(typeof(JsonIgnoreAttribute))) count++;
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    best = kind;
                }
            }
            if (best.Type == null)
            {
                error = "none of its keys belong to a data file this editor knows";
                return false;
            }

            file = new AssetFile { Path = relativePath, FileKind = best, Container = Activator.CreateInstance(best.Type), UnknownKeys = keys.Count - bestCount };
            JsonData.Overwrite(text, file.Container, relativePath);
            foreach (string key in keys)
            {
                FieldInfo field = best.Type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && !field.IsDefined(typeof(JsonIgnoreAttribute))) file.Fields.Add(field);
            }
            return true;
        }

        /// <summary>
        /// 새 데이터 파일을 만든다 (아직 쓰지 않는다). 목록 섹션만 갖는다: 미션의 에드온 데이터 파일이 그런 모양이다. 목록이 아닌 필드만 있는 종류는 필드를 전부 갖는다.
        /// </summary>
        /// <param name="relativePath">파일 경로 (exe 폴더 기준).</param>
        /// <param name="kind">종류.</param>
        /// <returns>새 파일. 저장할 내용이 있는 것으로 표시돼 있다.</returns>
        public static AssetFile Create(string relativePath, Kind kind)
        {
            var file = new AssetFile { Path = relativePath, FileKind = kind, Container = Activator.CreateInstance(kind.Type), Dirty = true };
            List<FieldInfo> fields = DataFields(kind.Type);
            List<FieldInfo> lists = fields.FindAll(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(DataList<>));
            file.Fields.AddRange(lists.Count > 0 ? lists : fields);
            return file;
        }

        /// <summary>
        /// 파일에 쓸 내용을 JSON 으로 만든다 (되돌리기의 전후로도 쓴다).
        /// </summary>
        /// <returns>JSON.</returns>
        public string Serialize()
        {
            var root = new JsonObject();
            foreach (FieldInfo field in Fields)
            {
                root[field.Name] = JsonSerializer.SerializeToNode(field.GetValue(Container), field.FieldType, JsonData.Options);
            }
            return root.ToJsonString(JsonData.Options);
        }

        /// <summary>
        /// 내용을 떠 둔 JSON 으로 되돌린다. 데이터 객체가 새것으로 바뀐다.
        /// </summary>
        /// <param name="json">Serialize 로 뜬 JSON.</param>
        public void Restore(string json)
        {
            Container = Activator.CreateInstance(FileKind.Type);
            JsonData.Overwrite(json, Container, Path);
        }

        /// <summary>
        /// 파일을 쓴다. 덮어쓰는 파일은 먼저 .bak 으로 남긴다.
        /// </summary>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>썼으면 true.</returns>
        public bool Save(out string error)
        {
            error = null;
            string fullPath = GamePath.Resolve(Path);
            if (fullPath == null)
            {
                error = "the file must be inside the game folder";
                return false;
            }
            if (!string.Equals(System.IO.Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                error = "the file must be a .json file";
                return false;
            }
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
                if (File.Exists(fullPath)) File.Copy(fullPath, fullPath + ".bak", true);
                File.WriteAllText(fullPath, Serialize());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
            Dirty = false;
            UnknownKeys = 0;
            return true;
        }
    }
}
