using System;
using System.Collections.Generic;
using System.IO;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터가 아는 이벤트 종류의 목록. 원본 이벤트(10 에서 19)는 여기서 정하고, 스크립트 이벤트는 등록 파일(설치형 godotdata/event/*.json 과 미션 전용 묶음)에서 읽는다.
    /// 등록 파일만 읽고 스크립트는 로드하지 않는다. 에디터는 이것으로 이벤트의 이름과 입력 칸을 그린다.
    /// </summary>
    public sealed class EventCatalog
    {
        /// <summary>
        /// 메뉴에 함께 묶여 나오는 이벤트들.
        /// </summary>
        public sealed class Group
        {
            public string Name;
            public readonly List<EventDefinitionData> Events = new List<EventDefinitionData>();
        }

        private const string k_installedFolder = "godotdata/event";
        private const string k_basePack = "base";
        private const string k_slotP2 = "p2";
        private const string k_slotP3 = "p3";

        // 기본 제공 묶음은 번호대로 나눠 보여 준다 (20 대기, 40 동작, 60 흐름, 70 화면 글자).
        private static readonly (int First, string Name)[] s_baseGroups = { (20, "Wait"), (40, "Action"), (60, "Flow"), (70, "Screen text") };

        private readonly Dictionary<int, EventDefinitionData> m_definitions = new Dictionary<int, EventDefinitionData>();
        private readonly List<Group> m_groups = new List<Group>();

        public IReadOnlyList<Group> Groups => m_groups;

        /// <summary>
        /// 목록을 다시 읽는다.
        /// </summary>
        /// <param name="missionPackPath">미션 전용 묶음의 등록 파일 (exe 폴더 기준). 없으면 빈 문자열이나 null.</param>
        public void Reload(string missionPackPath)
        {
            m_definitions.Clear();
            m_groups.Clear();

            var original = new Group { Name = "Original" };
            original.Events.Add(Builtin(10, "Mission Complete", null, false));
            original.Events.Add(Builtin(11, "Mission Failed", null, false));
            original.Events.Add(Builtin(12, "Wait Death", ("human", "human"), true));
            original.Events.Add(Builtin(13, "Wait Arrival", ("human", "human"), true));
            original.Events.Add(Builtin(14, "Change To Walk", ("path", "path"), true));
            original.Events.Add(Builtin(15, "Wait Break Object", ("object", "object"), true));
            original.Events.Add(Builtin(16, "Wait Case", ("human", "human"), true));
            original.Events.Add(Builtin(17, "Wait Time", ("seconds", "int"), true));
            original.Events.Add(Builtin(18, "Message", ("message", "message"), true));
            original.Events.Add(Builtin(19, "Change Team", ("human", "human"), true));
            AddGroup(original);

            string folder = GamePath.Resolve(k_installedFolder);
            if (folder != null && Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    AddPack(file, Path.GetFileNameWithoutExtension(file));
                }
            }

            string missionPack = string.IsNullOrEmpty(missionPackPath) ? null : GamePath.Resolve(missionPackPath);
            if (missionPack != null && File.Exists(missionPack)) AddPack(missionPack, "Mission");
        }

        /// <summary>
        /// 종류 번호의 등록 정보를 찾는다.
        /// </summary>
        /// <param name="type">포인트 종류.</param>
        /// <returns>등록 정보. 모르는 종류면 null.</returns>
        public EventDefinitionData Get(int type)
        {
            return m_definitions.TryGetValue(type, out EventDefinitionData definition) ? definition : null;
        }

        /// <summary>
        /// 이벤트의 출구들. 등록 정보가 없거나 출구를 적지 않은 스크립트 이벤트는 P3 하나다 (게임과 같다). 끝내는 원본 이벤트(10, 11)는 출구가 없다.
        /// </summary>
        /// <param name="type">포인트 종류.</param>
        /// <returns>출구 목록.</returns>
        public IReadOnlyList<EventSlotData> ExitsOf(int type)
        {
            EventDefinitionData definition = Get(type);
            if (definition != null && (definition.exits.Count > 0 || type < EventManager.ScriptEventFirst)) return definition.exits;
            return s_defaultExits;
        }

        private static readonly EventSlotData[] s_defaultExits = { new EventSlotData { name = "next", slot = k_slotP3, kind = "event" } };

        /// <summary>
        /// 원본 이벤트 하나의 등록 정보를 만든다.
        /// </summary>
        /// <param name="type">종류 번호.</param>
        /// <param name="name">이름.</param>
        /// <param name="parameter">P2 의 이름과 형식. 없으면 null.</param>
        /// <param name="hasNext">P3 이 다음 이벤트인지.</param>
        /// <returns>등록 정보.</returns>
        private static EventDefinitionData Builtin(int type, string name, (string Name, string Kind)? parameter, bool hasNext)
        {
            var definition = new EventDefinitionData { type = type, name = name };
            if (parameter != null) definition.parameters.Add(new EventSlotData { name = parameter.Value.Name, slot = k_slotP2, kind = parameter.Value.Kind });
            if (hasNext) definition.exits.Add(new EventSlotData { name = "next", slot = k_slotP3, kind = "event" });
            return definition;
        }

        /// <summary>
        /// 등록 파일 하나를 읽어 묶음으로 더한다. 이미 있는 종류 번호는 먼저 읽은 것이 남는다.
        /// </summary>
        /// <param name="fullPath">등록 파일 전체 경로.</param>
        /// <param name="name">묶음의 이름.</param>
        private void AddPack(string fullPath, string name)
        {
            var pack = new EventPackData();
            JsonData.Overwrite(EncodingHelper.ReadAllText(fullPath), pack, name);

            bool split = string.Equals(name, k_basePack, StringComparison.OrdinalIgnoreCase);
            var groups = new List<Group>();
            foreach (EventDefinitionData definition in pack.events)
            {
                if (definition.type < EventManager.ScriptEventFirst || m_definitions.ContainsKey(definition.type)) continue;

                string groupName = name;
                if (split)
                {
                    foreach ((int first, string label) in s_baseGroups)
                    {
                        if (definition.type >= first) groupName = label;
                    }
                }
                Group group = groups.Find(g => g.Name == groupName);
                if (group == null)
                {
                    group = new Group { Name = groupName };
                    groups.Add(group);
                }
                group.Events.Add(definition);
                m_definitions[definition.type] = definition;
            }
            m_groups.AddRange(groups);
        }

        /// <summary>
        /// 묶음 하나를 더한다.
        /// </summary>
        /// <param name="group">묶음.</param>
        private void AddGroup(Group group)
        {
            m_groups.Add(group);
            foreach (EventDefinitionData definition in group.Events)
            {
                m_definitions[definition.type] = definition;
            }
        }
    }
}
