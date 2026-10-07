using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 게임 설정(General/Input/Graphic/Sound + 입력 바인딩)을 JSON에서 로드/저장하고 전역 접근을 제공하는 싱글톤.
    /// 스칼라 설정은 타입 태그(int/float/bool/string)로 다루며, 섹션/설정을 런타임에 추가 등록할 수 있다.
    /// 입력 바인딩은 구조가 달라 제네릭 설정에서 제외하고 그대로 보관하며, InputManager가 이 데이터를 받아 빌드한다.
    /// Autoload 순서상 InputManager 보다 먼저 등록해야 한다.
    /// </summary>
    public partial class ConfigManager : Singleton<ConfigManager>
    {
        public GameConfig Config { get; private set; } = new GameConfig();

        // 섹션명 → (설정명 → 설정). 대소문자 무시 조회.
        private readonly Dictionary<string, Dictionary<string, ConfigSetting>> m_lookup =
            new Dictionary<string, Dictionary<string, ConfigSetting>>(StringComparer.OrdinalIgnoreCase);

        // 핫패스(시점 회전/화면 후처리)에서 매 프레임 문자열 파싱을 피하려 값들을 캐시한다. 로드/값 변경 시에만 갱신.
        private float m_mouseSensitivity = 0.1f;
        private bool m_invertY;
        private float m_brightness = 1f;
        private float m_gamma = 1f;
        private float m_masterVolume = 1f;

        // 마지막 저장 시점의 값 스냅샷(옵션 BACK로 되돌리기). 섹션명 → (설정명 → 값 문자열).
        private readonly Dictionary<string, Dictionary<string, string>> m_savedValues =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        // 초기값(옵션 RESET용). 섹션명 → (설정명 → 값 문자열).
        private readonly Dictionary<string, Dictionary<string, string>> m_defaults =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        // 입력 바인딩(0번 경로) 스냅샷/기본값. 액션명 → 경로. BACK/RESET에서 바인딩까지 되돌리는 데 쓴다.
        private readonly Dictionary<string, string> m_savedBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> m_defaultBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 마우스 감도(0~1). PlayerController가 매 프레임 시점 회전 배율로 읽으므로 캐시 값을 반환한다.
        public float MouseSensitivity => m_mouseSensitivity;
        // 마우스 상하(Y축) 반전 여부.
        public bool InvertY => m_invertY;
        // 화면 밝기 곱(0.5~1.5).
        public float Brightness => m_brightness;
        // 화면 감마(셰이더가 pow(c, gamma) 적용. 1.0=중립, 클수록 어둡게).
        public float Gamma => m_gamma;
        // 마스터 볼륨(0~1).
        public float MasterVolume => m_masterVolume;
        // InputManager가 액션을 빌드할 때 소비하는 입력 바인딩 정의.
        public InputActionDefinition[] Bindings => Config.bindings;

        public override void _Ready()
        {
            LoadConfig();
            BuildLookup();
            MergeDefaults();
            ApplyUIScaleLimit();
            SnapshotSaved();
            RefreshValueCaches();
            ApplyGraphic();
        }

        /// <summary>
        /// 값 캐시(감도/상하반전/밝기/감마/마스터볼륨)를 현재 설정값으로 다시 계산한다. 로드 후와 값 변경 후에 호출한다.
        /// </summary>
        private void RefreshValueCaches()
        {
            m_mouseSensitivity = GetFloat(SectionInput, KeySensitivity, 0.1f);
            m_invertY = GetBool(SectionInput, KeyInvertY, false);
            m_brightness = GetFloat(SectionGraphic, KeyBrightness, 1f);
            m_gamma = GetFloat(SectionGraphic, KeyGamma, 1f);
            m_masterVolume = GetFloat(SectionSound, KeyMasterVolume, 1f);
        }

        /// <summary>
        /// config.json을 로드한다. 파일이 없으면 코드 기본값으로 새 파일을 만든다.
        /// 파일이 깨져 있으면 기본값으로 진행하되, 유저가 고칠 수 있도록 파일을 덮어쓰지 않는다.
        /// </summary>
        private void LoadConfig()
        {
            string fullPath = GamePath.Resolve(k_configPath);
            if (!File.Exists(fullPath))
            {
                Config = BuildDefaultConfig();
                WriteConfigFile(fullPath);
                return;
            }

            var loaded = new GameConfig();
            bool ok = false;
            try
            {
                ok = JsonData.Overwrite(EncodingHelper.ReadAllText(fullPath), loaded, k_configPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debugger.LogWarning($"config.json could not be read, using defaults: {e.Message}", nameof(ConfigManager));
            }

            Config = ok ? loaded : BuildDefaultConfig();
            Config.sections ??= new List<ConfigSection>();
            Config.bindings ??= new InputActionDefinition[0];
        }

        /// <summary>
        /// 코드 기본값을 스키마로 병합한다. 파일에 없는 설정·섹션·액션은 기본값으로 추가하고,
        /// 이미 있는 설정은 값을 유지한 채 타입/범위만 최신 정의로 맞춘다. RESET용 기본값 맵도 여기서 만든다.
        /// </summary>
        private void MergeDefaults()
        {
            m_defaults.Clear();
            foreach (ConfigSection section in BuildDefaultSections())
            {
                foreach (ConfigSetting setting in section.settings)
                {
                    RegisterSetting(section.name, setting.name, setting.type, setting.value, setting.min, setting.max);
                    SetDefault(section.name, setting.name, setting.value);
                }
            }

            m_defaultBindings.Clear();
            var bindings = new List<InputActionDefinition>(Config.bindings);
            foreach (InputActionDefinition def in BuildDefaultBindings())
            {
                if (def.bindings.Length > 0)
                {
                    m_defaultBindings[def.name] = def.bindings[0];
                }
                if (!bindings.Exists(existing => existing != null && string.Equals(existing.name, def.name, StringComparison.OrdinalIgnoreCase)))
                {
                    bindings.Add(def);
                }
            }
            bindings.RemoveAll(def => def == null || string.IsNullOrEmpty(def.name));
            foreach (InputActionDefinition def in bindings)
            {
                def.bindings ??= new string[0];
                def.composites ??= new InputCompositeDefinition[0];
            }
            Config.bindings = bindings.ToArray();
        }

        /// <summary>
        /// 섹션/설정 조회 사전을 Config에서 다시 구성한다.
        /// </summary>
        private void BuildLookup()
        {
            m_lookup.Clear();
            Config.sections.RemoveAll(section => section == null || string.IsNullOrEmpty(section.name));
            foreach (ConfigSection section in Config.sections)
            {
                Dictionary<string, ConfigSetting> map = GetOrCreateSectionMap(section.name);
                section.settings ??= new List<ConfigSetting>();
                section.settings.RemoveAll(setting => setting == null || string.IsNullOrEmpty(setting.name));
                foreach (ConfigSetting setting in section.settings)
                {
                    map[setting.name] = setting;
                }
            }
        }

        private Dictionary<string, ConfigSetting> GetOrCreateSectionMap(string section)
        {
            if (!m_lookup.TryGetValue(section, out Dictionary<string, ConfigSetting> map))
            {
                map = new Dictionary<string, ConfigSetting>(StringComparer.OrdinalIgnoreCase);
                m_lookup[section] = map;
            }
            return map;
        }

        /// <summary>
        /// 섹션+이름으로 설정을 찾는다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <returns>설정 객체, 없으면 null.</returns>
        public ConfigSetting FindSetting(string section, string name)
        {
            if (section != null && name != null
                && m_lookup.TryGetValue(section, out Dictionary<string, ConfigSetting> map)
                && map.TryGetValue(name, out ConfigSetting setting))
            {
                return setting;
            }
            return null;
        }

        /// <summary>
        /// 섹션이 없으면 추가한다. 이미 있으면 기존 섹션을 반환한다.
        /// </summary>
        /// <param name="name">섹션 이름.</param>
        /// <returns>추가되거나 기존인 섹션. 이름이 비면 null.</returns>
        public ConfigSection AddSection(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (ConfigSection existing in Config.sections)
            {
                if (string.Equals(existing.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            var section = new ConfigSection { name = name };
            Config.sections.Add(section);
            GetOrCreateSectionMap(name);
            return section;
        }

        /// <summary>
        /// 설정을 스키마+기본값으로 등록한다. 이미 로드된 값이 있으면 값은 유지하고 타입/범위 메타만 갱신한다(영속 값 우선).
        /// 없으면 기본값으로 새로 추가한다.
        /// </summary>
        /// <param name="section">섹션 이름(없으면 생성).</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="type">타입 태그(int/float/bool/string).</param>
        /// <param name="defaultValue">문자열 기본값.</param>
        /// <param name="min">범위 최소(int/float에서 min &lt; max 일 때만 의미).</param>
        /// <param name="max">범위 최대.</param>
        /// <returns>등록되거나 기존인 설정. 이름이 비면 null.</returns>
        public ConfigSetting RegisterSetting(string section, string name, string type, string defaultValue, float min, float max)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            ConfigSection sectionObj = AddSection(section);
            ConfigSetting setting = FindSetting(section, name);
            if (setting == null)
            {
                setting = new ConfigSetting
                {
                    name = name,
                    type = type,
                    value = defaultValue ?? string.Empty,
                    min = min,
                    max = max,
                };
                sectionObj.settings.Add(setting);
                GetOrCreateSectionMap(section)[name] = setting;
            }
            else
            {
                setting.type = type;
                setting.min = min;
                setting.max = max;
            }
            return setting;
        }

        /// <summary>
        /// 등록된 모든 섹션 이름을 반환한다.
        /// </summary>
        /// <returns>섹션 이름 배열.</returns>
        public string[] GetSectionNames()
        {
            return Config.sections.ConvertAll(section => section.name).ToArray();
        }

        /// <summary>
        /// 지정 섹션의 설정 이름들을 반환한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <returns>설정 이름 배열. 섹션이 없으면 빈 배열.</returns>
        public string[] GetSettingNames(string section)
        {
            foreach (ConfigSection sec in Config.sections)
            {
                if (string.Equals(sec.name, section, StringComparison.OrdinalIgnoreCase))
                {
                    return sec.settings.ConvertAll(setting => setting.name).ToArray();
                }
            }
            return new string[0];
        }

        /// <summary>
        /// 설정을 정수로 읽는다. min이 max보다 작으면 min/max로 클램프한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="fallback">설정이 없거나 파싱 실패 시 반환값.</param>
        /// <returns>정수 값.</returns>
        public int GetInt(string section, string name, int fallback = 0)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting == null || !TryParseInt(setting.value, out int result))
            {
                return fallback;
            }
            if (HasRange(setting))
            {
                result = Mathf.Clamp(result, Mathf.RoundToInt(setting.min), Mathf.RoundToInt(setting.max));
            }
            return result;
        }

        /// <summary>
        /// 설정을 실수로 읽는다. min이 max보다 작으면 min/max로 클램프한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="fallback">설정이 없거나 파싱 실패 시 반환값.</param>
        /// <returns>실수 값.</returns>
        public float GetFloat(string section, string name, float fallback = 0f)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting == null || !TryParseFloat(setting.value, out float result))
            {
                return fallback;
            }
            if (HasRange(setting))
            {
                result = Mathf.Clamp(result, setting.min, setting.max);
            }
            return result;
        }

        /// <summary>
        /// 설정을 불리언으로 읽는다("true"/"1"이면 참).
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="fallback">설정이 없을 때 반환값.</param>
        /// <returns>불리언 값.</returns>
        public bool GetBool(string section, string name, bool fallback = false)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting == null || string.IsNullOrEmpty(setting.value))
            {
                return fallback;
            }
            string v = setting.value.Trim();
            return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        /// <summary>
        /// 설정을 문자열로 읽는다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="fallback">설정이 없을 때 반환값.</param>
        /// <returns>문자열 값.</returns>
        public string GetString(string section, string name, string fallback = "")
        {
            ConfigSetting setting = FindSetting(section, name);
            return setting != null ? setting.value ?? string.Empty : fallback;
        }

        /// <summary>
        /// 설정의 허용 범위 최솟값을 반환한다. 설정 화면이 화살표를 끌 자리를 정할 때 쓴다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <returns>최솟값. 설정이 없으면 0.</returns>
        public float GetMin(string section, string name)
        {
            ConfigSetting setting = FindSetting(section, name);
            return setting != null ? setting.min : 0f;
        }

        /// <summary>
        /// 설정의 허용 범위 최댓값을 반환한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <returns>최댓값. 설정이 없으면 0.</returns>
        public float GetMax(string section, string name)
        {
            ConfigSetting setting = FindSetting(section, name);
            return setting != null ? setting.max : 0f;
        }

        /// <summary>
        /// 정수 값을 설정한다. min이 max보다 작으면 클램프한다. 설정이 없으면 무시된다(Save로 파일에 반영).
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="value">저장할 값.</param>
        public void SetInt(string section, string name, int value)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting == null)
            {
                return;
            }
            if (HasRange(setting))
            {
                value = Mathf.Clamp(value, Mathf.RoundToInt(setting.min), Mathf.RoundToInt(setting.max));
            }
            setting.value = value.ToString(CultureInfo.InvariantCulture);

            // 해상도가 바뀌면 그 해상도의 UIScale 상한으로 범위를 갱신하고 초과분을 내린다.
            if (string.Equals(section, SectionGraphic, StringComparison.OrdinalIgnoreCase)
                && string.Equals(name, KeyResolution, StringComparison.OrdinalIgnoreCase))
            {
                ApplyUIScaleLimit();
            }
        }

        /// <summary>
        /// 실수 값을 설정한다. min이 max보다 작으면 클램프한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="value">저장할 값.</param>
        public void SetFloat(string section, string name, float value)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting == null)
            {
                return;
            }
            if (HasRange(setting))
            {
                value = Mathf.Clamp(value, setting.min, setting.max);
            }
            setting.value = value.ToString(CultureInfo.InvariantCulture);
            RefreshValueCaches();
        }

        /// <summary>
        /// 불리언 값을 설정한다("true"/"false"로 저장).
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="value">저장할 값.</param>
        public void SetBool(string section, string name, bool value)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting != null)
            {
                setting.value = value ? "true" : "false";
            }
            RefreshValueCaches();
        }

        /// <summary>
        /// 문자열 값을 설정한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="value">저장할 값.</param>
        public void SetString(string section, string name, string value)
        {
            ConfigSetting setting = FindSetting(section, name);
            if (setting != null)
            {
                setting.value = value ?? string.Empty;
            }
        }

        /// <summary>
        /// 현재 설정을 config.json에 기록한다. 옵션 UI가 변경을 확정할 때 호출한다.
        /// </summary>
        public void Save()
        {
            WriteConfigFile(GamePath.Resolve(k_configPath));
            SnapshotSaved();
        }

        /// <summary>
        /// 현재 모든 설정 값을 "저장된 상태" 스냅샷으로 기록한다. 로드 직후와 Save 후에 호출한다(BACK 되돌리기 기준).
        /// </summary>
        private void SnapshotSaved()
        {
            m_savedValues.Clear();
            foreach (ConfigSection section in Config.sections)
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ConfigSetting setting in section.settings)
                {
                    map[setting.name] = setting.value;
                }
                m_savedValues[section.name] = map;
            }

            m_savedBindings.Clear();
            foreach (InputActionDefinition def in Config.bindings)
            {
                if (def.bindings.Length > 0)
                {
                    m_savedBindings[def.name] = def.bindings[0];
                }
            }
        }

        /// <summary>
        /// 저장하지 않은 변경을 마지막 저장 스냅샷으로 되돌린다(옵션 화면 BACK). 값만 되돌리고 스키마는 유지한다.
        /// </summary>
        public void RevertToSaved()
        {
            RestoreValues(m_savedValues);
            RestoreBindings(m_savedBindings);
        }

        /// <summary>
        /// 설정의 초기값을 등록한다(RESET용). 코어 설정은 코드 기본값이 자동 등록되고, 추가 설정은 등록한 쪽이 호출한다.
        /// </summary>
        /// <param name="section">섹션 이름.</param>
        /// <param name="name">설정 이름.</param>
        /// <param name="value">초기값 문자열.</param>
        public void SetDefault(string section, string name, string value)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
            {
                return;
            }
            if (!m_defaults.TryGetValue(section, out Dictionary<string, string> map))
            {
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                m_defaults[section] = map;
            }
            map[name] = value ?? string.Empty;
        }

        /// <summary>
        /// 등록된 초기값으로 설정 값을 되돌린다(옵션 화면 RESET). 저장은 별도(Save)로 한다.
        /// </summary>
        public void ResetToDefaults()
        {
            // 옵션 화면에 없는 설정은 RESET 이 건드리지 않는다. 파일을 직접 고쳐 켠 값이 화면의 버튼 때문에 꺼지면 안 된다.
            bool allowConsole = GetBool(SectionGeneral, KeyAllowConsole);

            RestoreValues(m_defaults);
            RestoreBindings(m_defaultBindings);
            SetBool(SectionGeneral, KeyAllowConsole, allowConsole);
        }

        /// <summary>
        /// 주어진 (섹션→(이름→값)) 맵의 값들을 현재 설정에 덮어쓴다. 스키마는 건드리지 않고 값만 되돌린다.
        /// </summary>
        /// <param name="values">복원할 값 맵.</param>
        private void RestoreValues(Dictionary<string, Dictionary<string, string>> values)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> sectionPair in values)
            {
                foreach (KeyValuePair<string, string> valuePair in sectionPair.Value)
                {
                    ConfigSetting setting = FindSetting(sectionPair.Key, valuePair.Key);
                    if (setting != null)
                    {
                        setting.value = valuePair.Value;
                    }
                }
            }

            // 복원된 해상도에 맞춰 UIScale 범위/값을 다시 맞춘다.
            ApplyUIScaleLimit();
            RefreshValueCaches();
        }

        /// <summary>
        /// 주어진 (액션명 → 경로) 맵으로 Config 바인딩(0번)을 덮어쓰고, 라이브 액션에도 재적용한다(BACK/RESET용).
        /// </summary>
        /// <param name="source">복원할 바인딩 맵.</param>
        private void RestoreBindings(Dictionary<string, string> source)
        {
            foreach (InputActionDefinition def in Config.bindings)
            {
                if (def.bindings.Length > 0 && source.TryGetValue(def.name, out string path))
                {
                    def.bindings[0] = path;
                }
            }

            if (InputManager.Loaded)
            {
                InputManager.Instance.ReapplyBindings();
            }
        }

        private void WriteConfigFile(string fullPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, JsonData.ToJson(Config));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debugger.LogWarning($"config.json save failed: {e.Message}", nameof(ConfigManager));
            }
        }

        // 범위 제한은 별도 타입이 아니라 min < max 여부로 결정한다. min >= max 면 클램프하지 않는다(무제한).
        private static bool HasRange(ConfigSetting setting)
        {
            return setting.min < setting.max;
        }

        private static bool TryParseInt(string text, out int result)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            // "1920.0"처럼 실수 문자열로 저장된 경우도 정수로 받아준다.
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float asFloat))
            {
                result = Mathf.RoundToInt(asFloat);
                return true;
            }
            return false;
        }

        private static bool TryParseFloat(string text, out float result)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
