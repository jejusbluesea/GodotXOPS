using System;
using System.Collections.Generic;
using System.IO;

namespace GodotXOPS
{
    public partial class EventManager
    {
        // 스크립트 이벤트의 첫 종류 번호. 1 에서 19 는 원본의 포인트 종류다.
        public const int ScriptEventFirst = 20;
        // 미션 전용 이벤트의 첫 종류 번호 (다른 에드온 데이터와 같은 규칙).
        public const int AddonEventFirst = 10000;

        // 설치형 이벤트 묶음의 등록 파일이 있는 폴더 (exe 폴더 기준). 그 안의 .json 하나가 묶음 하나다.
        private const string k_packFolder = "godotdata/event";
        // 미션 변수의 최대 개수. 스크립트가 변수를 끝없이 만드는 것을 막는다.
        private const int k_maxVariables = 4096;

        /// <summary>
        /// 등록 파일에서 읽은 이벤트 하나.
        /// </summary>
        private sealed class Registered
        {
            public EventDefinitionData definition;
            public string scriptPath;
            public string source;
            // 같은 번호를 둘 이상이 등록했다. 이 번호를 쓰는 미션은 로드하지 않는다.
            public bool conflicted;
        }

        private readonly List<ScriptEventPack> m_packs = new List<ScriptEventPack>();
        private readonly List<int> m_scriptTypes = new List<int>();
        private readonly Dictionary<int, string> m_scriptNames = new Dictionary<int, string>();
        private readonly Dictionary<int, int> m_variables = new Dictionary<int, int>();
        private bool m_autoJudge = true;
        private int m_missionTicks;

        // 미션을 시작한 뒤 지난 틱 수.
        public int MissionTicks => m_missionTicks;

        // 자동 판정(적 전멸이면 클리어, 플레이어가 죽으면 실패)을 할지. 미션을 시작하면 켜진다. 끄면 이벤트만이 미션을 끝낸다.
        public bool AutoJudge
        {
            get => m_autoJudge;
            set => m_autoJudge = value;
        }

        /// <summary>
        /// 미션이 쓰는 스크립트 이벤트를 준비한다. 등록 파일을 읽고, 쓰이는 종류가 들어 있는 묶음의 스크립트만 샌드박스에 올린다.
        /// 포인트 데이터를 로드할 때 불린다. 하나라도 준비하지 못하면 미션을 로드하지 않는다 (없는 번호, 겹친 번호, 컴파일 오류, 없는 함수, 격리 실패).
        /// </summary>
        /// <param name="usedTypes">포인트 데이터에 있는 20 이상의 종류 번호.</param>
        /// <param name="addonPackPath">미션 전용 묶음의 등록 파일 (exe 폴더 기준). 없으면 빈 문자열.</param>
        /// <returns>전부 준비했으면 true.</returns>
        public bool LoadScripts(IReadOnlyCollection<int> usedTypes, string addonPackPath)
        {
            UnloadScripts();
            if (usedTypes == null || usedTypes.Count == 0) return true;

            if (!ConfigManager.Instance.GetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowEventScript, true))
            {
                Debugger.LogError("This mission uses script events, but AllowEventScript is off in config.json", nameof(EventManager));
                return false;
            }

            var registry = new Dictionary<int, Registered>();
            string folder = GamePath.Resolve(k_packFolder);
            if (folder != null && Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    ReadPack(file, false, registry);
                }
            }
            if (!string.IsNullOrEmpty(addonPackPath))
            {
                string full = GamePath.Resolve(addonPackPath);
                if (full == null || !File.Exists(full))
                {
                    Debugger.LogError($"Add-on event data open failed: {addonPackPath}", nameof(EventManager));
                    return false;
                }
                ReadPack(full, true, registry);
            }

            var packs = new Dictionary<string, ScriptEventPack>(StringComparer.OrdinalIgnoreCase);
            foreach (int type in usedTypes)
            {
                if (!registry.TryGetValue(type, out Registered entry))
                {
                    return FailScripts($"A point uses event type {type}, but no event pack registers it");
                }
                if (entry.conflicted)
                {
                    return FailScripts($"Event type {type} is registered more than once ({entry.source})");
                }

                if (!packs.TryGetValue(entry.scriptPath, out ScriptEventPack pack))
                {
                    pack = new ScriptEventPack(this);
                    m_packs.Add(pack);
                    if (!pack.Load(entry.scriptPath, out string loadError)) return FailScripts(loadError);
                    packs[entry.scriptPath] = pack;
                }
                if (!ScriptEventHandler.Create(pack, entry.definition, out ScriptEventHandler handler, out string error))
                {
                    return FailScripts($"{error} ({entry.source})");
                }
                m_handlers[type] = handler;
                m_scriptTypes.Add(type);
                m_scriptNames[type] = string.IsNullOrEmpty(entry.definition.name) ? entry.definition.function : entry.definition.name;
            }
            return true;
        }

        /// <summary>
        /// 올려 둔 스크립트를 전부 내린다. 맵을 내릴 때 불린다.
        /// </summary>
        public void UnloadScripts()
        {
            foreach (int type in m_scriptTypes)
            {
                m_handlers.Remove(type);
            }
            m_scriptTypes.Clear();
            m_scriptNames.Clear();
            foreach (ScriptEventPack pack in m_packs)
            {
                pack.Free();
            }
            m_packs.Clear();
        }

        /// <summary>
        /// 미션 변수를 읽는다. 변수는 정수이고 미션을 시작할 때 전부 0 이다. 이벤트 줄끼리 조건을 주고받는 데 쓴다.
        /// </summary>
        /// <param name="index">변수 번호.</param>
        /// <returns>값. 쓴 적이 없으면 0.</returns>
        public int GetVariable(int index)
        {
            return m_variables.TryGetValue(index, out int value) ? value : 0;
        }

        /// <summary>
        /// 미션 변수를 쓴다.
        /// </summary>
        /// <param name="index">변수 번호.</param>
        /// <param name="value">값.</param>
        public void SetVariable(int index, int value)
        {
            if (!m_variables.ContainsKey(index) && m_variables.Count >= k_maxVariables) return;
            m_variables[index] = value;
        }

        /// <summary>
        /// 이벤트 줄을 지정한 포인트에서 (다시) 진행시킨다. 멈춰 있던 줄도 풀린다.
        /// </summary>
        /// <param name="line">줄 번호 (0 부터).</param>
        /// <param name="pointId">시작할 포인트의 식별번호.</param>
        public void StartLine(int line, int pointId)
        {
            if (line < 0 || line >= m_lines.Length) return;

            m_lines[line].MoveTo(pointId);
            m_lines[line].WaitCount = 0;
            m_lines[line].Stopped = false;
        }

        /// <summary>
        /// 이벤트 줄을 멈춘다. StartLine 으로 다시 진행시킬 수 있다.
        /// </summary>
        /// <param name="line">줄 번호 (0 부터).</param>
        public void StopLine(int line)
        {
            if (line >= 0 && line < m_lines.Length) m_lines[line].Stopped = true;
        }

        /// <summary>
        /// 이벤트 종류의 이름 (디버그 콘솔의 event). 원본 이벤트는 EventType 의 이름, 스크립트 이벤트는 등록 파일의 name 이다.
        /// </summary>
        /// <param name="type">포인트 종류 번호.</param>
        /// <returns>이름. 모르는 번호면 번호 그대로.</returns>
        public string EventName(int type)
        {
            if (m_scriptNames.TryGetValue(type, out string name)) return name;
            return System.Enum.IsDefined(typeof(EventType), type) ? ((EventType)type).ToString() : type.ToString();
        }

        /// <summary>
        /// 줄이 멈춰 있는지 (디버그 콘솔의 event).
        /// </summary>
        /// <param name="line">줄 번호 (0 부터).</param>
        /// <returns>멈춰 있으면 true. 줄 번호가 범위 밖이면 false.</returns>
        public bool LineStopped(int line)
        {
            return line >= 0 && line < m_lines.Length && m_lines[line].Stopped;
        }

        /// <summary>
        /// 스크립트 준비에 실패했을 때: 이유를 남기고 올리던 것을 내린다.
        /// </summary>
        /// <param name="message">이유 (영어).</param>
        /// <returns>늘 false.</returns>
        private bool FailScripts(string message)
        {
            Debugger.LogError(message, nameof(EventManager));
            UnloadScripts();
            return false;
        }

        /// <summary>
        /// 등록 파일 하나를 읽어 목록에 더한다. 형식이 틀린 항목은 경고를 남기고 건너뛴다.
        /// 번호가 구간을 벗어난 항목도 건너뛴다: 설치형은 20 에서 9999 사이, 미션 전용은 10000 이상이어야 한다.
        /// </summary>
        /// <param name="fullPath">등록 파일 전체 경로.</param>
        /// <param name="addon">미션 전용 묶음이면 true.</param>
        /// <param name="registry">종류 번호 → 등록된 이벤트.</param>
        private static void ReadPack(string fullPath, bool addon, Dictionary<int, Registered> registry)
        {
            string label = Path.GetRelativePath(GamePath.Root, fullPath).Replace('\\', '/');
            var data = new EventPackData();
            try
            {
                if (!JsonData.Overwrite(EncodingHelper.ReadAllText(fullPath), data, label)) return;
            }
            catch (IOException e)
            {
                Debugger.LogError($"Event data read failed: {label} ({e.Message})", nameof(EventManager));
                return;
            }

            foreach (EventDefinitionData definition in data.events)
            {
                if (definition == null) continue;

                bool inRange = addon ? definition.type >= AddonEventFirst : definition.type >= ScriptEventFirst && definition.type < AddonEventFirst;
                if (!inRange)
                {
                    Debugger.LogWarning($"Event type {definition.type} is out of range for {(addon ? "a mission pack (10000 or more)" : "an installed pack (20 to 9999)")}: {label}", nameof(EventManager));
                    continue;
                }
                definition.parameters ??= new List<EventSlotData>();
                definition.exits ??= new List<EventSlotData>();

                if (registry.TryGetValue(definition.type, out Registered existing))
                {
                    existing.conflicted = true;
                    existing.source = $"{existing.source}, {label}";
                    continue;
                }
                registry[definition.type] = new Registered { definition = definition, scriptPath = data.scriptPath ?? string.Empty, source = label };
            }
        }
    }
}
