using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 커스텀 바인딩을 등록한 곳. 맵팩의 것은 미션이 붙인 맵팩(Pack)이 있으면 그것이고, 없으면 메뉴가 보고 있는 맵팩(MenuPack)이다.
    /// </summary>
    public enum CustomBindingLayer
    {
        Installed,
        MenuPack,
        Pack,
    }

    // InputManager 의 커스텀 바인딩 담당 partial: 등록 파일이 정한 버튼 액션을 층(설치형, 맵팩)마다 받아 지금 쓰는 것만 액션으로 만든다.
    // 키는 config.json 의 customBindings(유저가 바꾼 것)가 먼저이고 없으면 등록 파일의 baseValue 다.
    public partial class InputManager
    {
        private const string k_customFolder = "godotdata/input";

        // 층마다 받은 등록 (이름과 처음 키를 확인한 뒤의 것).
        private readonly Dictionary<CustomBindingLayer, List<CustomBindingData>> m_customLayers = new Dictionary<CustomBindingLayer, List<CustomBindingData>>();
        // 지금 액션으로 만들어 둔 커스텀 바인딩. 이름 → 등록. 순서는 m_customOrder 다 (설치형 → 맵팩 → 미션, 파일에 적은 순서).
        private readonly Dictionary<string, CustomBindingData> m_customActive = new Dictionary<string, CustomBindingData>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> m_customOrder = new List<string>();
        // 미션이 붙인 맵팩의 등록 파일에 적힌 이름들 (파일의 순서 그대로, 거절된 것도 자리를 지킨다). 이벤트 Wait Input 이 번호로 가리킨다.
        private readonly List<string> m_packBindingNames = new List<string>();

        /// <summary>
        /// 설치형 등록 파일(godotdata/input/*.json)을 파일 이름 순서로 읽어 설치형 층에 넣는다. 부팅 때 한 번 부른다.
        /// </summary>
        private void LoadInstalledCustomBindings()
        {
            var merged = new CustomBindingListData();
            string folder = GamePath.Resolve(k_customFolder);
            if (folder != null && Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    var data = new CustomBindingListData();
                    try
                    {
                        if (JsonData.Overwrite(EncodingHelper.ReadAllText(file), data, Path.GetFileName(file)))
                        {
                            merged.bindings.AddRange(data.bindings);
                        }
                    }
                    catch (IOException e)
                    {
                        Debugger.LogWarning($"Custom binding file read failed: {Path.GetFileName(file)} ({e.Message})", nameof(InputManager));
                    }
                }
            }
            SetCustomLayer(CustomBindingLayer.Installed, merged);
        }

        /// <summary>
        /// 등록 파일 하나를 읽어 그 층에 넣는다. 경로가 비었으면 그 층을 비운다.
        /// </summary>
        /// <param name="layer">층.</param>
        /// <param name="relativePath">등록 파일 경로 (exe 폴더 기준). 비었으면 층을 비운다.</param>
        public void LoadCustomLayer(CustomBindingLayer layer, string relativePath)
        {
            SetCustomLayer(layer, MapLoader.LoadAddonFile<CustomBindingListData>(relativePath));
        }

        /// <summary>
        /// 한 층의 커스텀 바인딩을 바꾸고 지금 쓰는 액션을 다시 맞춘다. 이름이 잘못됐거나 기본 바인딩과 같은 이름이면 경고를 남기고 뺀다.
        /// 처음 키가 잘못됐거나 예약 키(Esc, F1 에서 F12)면 경고를 남기고 묶지 않는다.
        /// </summary>
        /// <param name="layer">층.</param>
        /// <param name="data">그 층의 등록. null 이면 층을 비운다.</param>
        public void SetCustomLayer(CustomBindingLayer layer, CustomBindingListData data)
        {
            if (layer == CustomBindingLayer.Pack)
            {
                m_packBindingNames.Clear();
                if (data != null)
                {
                    foreach (CustomBindingData binding in data.bindings) m_packBindingNames.Add(binding?.name ?? string.Empty);
                }
            }

            if (data == null || data.bindings.Count == 0)
            {
                if (!m_customLayers.Remove(layer)) return;
            }
            else
            {
                var list = new List<CustomBindingData>();
                foreach (CustomBindingData binding in data.bindings)
                {
                    if (binding == null) continue;
                    if (!ValidCustomName(binding.name))
                    {
                        Debugger.LogWarning($"Custom binding name \"{binding.name}\" ignored (use letters, digits and underscores).", nameof(InputManager));
                        continue;
                    }
                    if (m_actions.ContainsKey(binding.name) && !m_customActive.ContainsKey(binding.name))
                    {
                        Debugger.LogWarning($"Custom binding \"{binding.name}\" ignored: a built-in action has that name.", nameof(InputManager));
                        continue;
                    }

                    string baseValue = binding.baseValue ?? string.Empty;
                    if (baseValue.Length > 0 && (InputPath.Parse(baseValue) == null || IsReservedPath(baseValue)))
                    {
                        Debugger.LogWarning($"Custom binding \"{binding.name}\": key \"{baseValue}\" is unknown or reserved, left unbound.", nameof(InputManager));
                        baseValue = string.Empty;
                    }
                    list.Add(new CustomBindingData
                    {
                        name = binding.name,
                        label = string.IsNullOrEmpty(binding.label) ? binding.name : binding.label,
                        baseValue = baseValue,
                    });
                }
                m_customLayers[layer] = list;
            }

            RebuildCustomActions();
        }

        /// <summary>
        /// 층들에서 지금 쓰는 커스텀 바인딩을 모아 액션을 더하고 뺀다. 같은 이름은 앞의 층(설치형 → 맵팩 → 미션)의 것을 쓴다.
        /// </summary>
        private void RebuildCustomActions()
        {
            var wanted = new List<CustomBindingData>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CustomBindingLayer packLayer = m_customLayers.ContainsKey(CustomBindingLayer.Pack) ? CustomBindingLayer.Pack : CustomBindingLayer.MenuPack;
            foreach (CustomBindingLayer layer in new[] { CustomBindingLayer.Installed, packLayer })
            {
                if (!m_customLayers.TryGetValue(layer, out List<CustomBindingData> list)) continue;
                foreach (CustomBindingData binding in list)
                {
                    if (seen.Add(binding.name)) wanted.Add(binding);
                }
            }

            foreach (string name in m_customOrder)
            {
                if (seen.Contains(name) || !m_actions.TryGetValue(name, out ActionEntry entry)) continue;

                if (InputMap.HasAction(entry.button)) InputMap.EraseAction(entry.button);
                m_actions.Remove(name);
            }

            m_customActive.Clear();
            m_customOrder.Clear();
            foreach (CustomBindingData binding in wanted)
            {
                m_customActive[binding.name] = binding;
                m_customOrder.Add(binding.name);
                if (!m_actions.ContainsKey(binding.name))
                {
                    BuildAction(new InputActionDefinition { name = binding.name, type = "Button", bindings = new[] { string.Empty } });
                }
                RefreshCustomBinding(m_actions[binding.name]);
            }
        }

        /// <summary>
        /// 커스텀 액션의 키를 설정(유저가 바꾼 것)이나 처음 키로 다시 맞춘다.
        /// </summary>
        /// <param name="entry">커스텀 액션.</param>
        private void RefreshCustomBinding(ActionEntry entry)
        {
            CustomBindingData binding = m_customActive[entry.definition.name];
            entry.definition.bindings[0] = ConfigManager.Instance.GetCustomBinding(binding.name, out string saved) ? saved : binding.baseValue;
            ApplyButtonBindings(entry);
        }

        /// <summary>
        /// 미션이 붙인 맵팩의 커스텀 바인딩 하나의 이름. 번호는 등록 파일에 적힌 순서다 (0 부터).
        /// </summary>
        /// <param name="index">번호.</param>
        /// <returns>이름. 맵팩이 없거나 없는 번호면 빈 문자열.</returns>
        public string PackBindingName(int index)
        {
            return index >= 0 && index < m_packBindingNames.Count ? m_packBindingNames[index] : string.Empty;
        }

        // 지금 쓰는 커스텀 바인딩의 이름들. 프레임마다 읽는 쪽(이벤트 입력, HUD 값)이 배열을 만들지 않게 그대로 내준다.
        public IReadOnlyList<string> CustomActionNames => m_customOrder;

        /// <summary>
        /// 지금 쓰는 커스텀 바인딩의 이름들 (설치형 → 맵팩 → 미션, 파일에 적은 순서). OPTION 의 Custom 탭과 스크립트가 쓴다.
        /// </summary>
        /// <returns>액션 이름 배열.</returns>
        public string[] GetCustomActionNames()
        {
            return m_customOrder.ToArray();
        }

        /// <summary>
        /// 커스텀 바인딩의 OPTION 에 보이는 이름.
        /// </summary>
        /// <param name="action">액션 이름.</param>
        /// <returns>이름. 지금 쓰는 커스텀 바인딩이 아니면 빈 문자열.</returns>
        public string GetCustomLabel(string action)
        {
            return action != null && m_customActive.TryGetValue(action, out CustomBindingData binding) ? binding.label : string.Empty;
        }

        /// <summary>
        /// 지금 쓰는 커스텀 바인딩인지.
        /// </summary>
        /// <param name="action">액션 이름.</param>
        /// <returns>커스텀 바인딩이면 true.</returns>
        public bool IsCustomAction(string action)
        {
            return action != null && m_customActive.ContainsKey(action);
        }

        private static bool ValidCustomName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (char c in name)
            {
                if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')) return false;
            }
            return true;
        }

        private static bool IsReservedPath(string path)
        {
            return InputPath.Parse(path) is InputEventKey key && s_reservedBindKeys.Contains(key.PhysicalKeycode);
        }
    }
}
