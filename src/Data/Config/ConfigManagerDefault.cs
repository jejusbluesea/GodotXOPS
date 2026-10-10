using System.Collections.Generic;

namespace GodotXOPS
{
    public partial class ConfigManager
    {
        private const string k_configPath = "godotdata/config.json";

        // 섹션 이름(코어 4종). 이 외의 섹션도 추가할 수 있다.
        public const string SectionGeneral = "General";
        public const string SectionInput = "Input";
        public const string SectionGraphic = "Graphic";
        public const string SectionSound = "Sound";

        public const string TypeInt = "int";
        public const string TypeFloat = "float";
        public const string TypeBool = "bool";
        public const string TypeString = "string";

        // 코어 설정 이름(그래픽 적용/감도 조회에 쓰는 키).
        public const string KeyFullscreen = "fullscreen";
        public const string KeyResolution = "resolution";
        public const string KeyVsync = "vsync";
        public const string KeyLimitFrame = "limitFrame";
        public const string KeyFrameLimit = "frameLimit";
        public const string KeySensitivity = "sensitivity";
        public const string KeyInvertY = "invertY";
        public const string KeyBrightness = "brightness";
        public const string KeyGamma = "gamma";
        public const string KeyMasterVolume = "MasterVolume";
        public const string KeyUIScale = "UIScale";
        // 디버그 콘솔(메인게임의 F11)을 허용할지. 옵션 화면에 없는 설정이라 config.json 을 직접 고쳐서 켠다.
        public const string KeyAllowConsole = "AllowConsole";
        public const string KeyAllowEventScript = "AllowEventScript";
        public const string KeyAllowUiScript = "AllowUiScript";

        /// <summary>
        /// config.json이 없을 때 기록할 기본 설정을 코드로 구성한다. 코어 섹션 4종과 기본 입력 바인딩을 담는다.
        /// </summary>
        /// <returns>기본값으로 채운 GameConfig.</returns>
        private static GameConfig BuildDefaultConfig()
        {
            return new GameConfig
            {
                sections = BuildDefaultSections(),
                bindings = BuildDefaultBindings(),
            };
        }

        /// <summary>
        /// 코어 섹션(General/Input/Graphic/Sound)의 기본 스칼라 설정 목록을 만든다.
        /// </summary>
        /// <returns>기본 섹션 목록.</returns>
        private static List<ConfigSection> BuildDefaultSections()
        {
            return new List<ConfigSection>
            {
                new ConfigSection
                {
                    name = SectionGeneral,
                    settings = new List<ConfigSetting>
                    {
                        new ConfigSetting { name = "ShowFPS", type = TypeBool, value = "false", min = 0f, max = 0f },
                        // UIScale 상한은 해상도별로 다르다(k_resolutions의 maxUIScale). 부팅/해상도 변경 시 ApplyUIScaleLimit가 min/max를 갱신한다.
                        // 여기 값은 기본 해상도(640x480)의 상한 = 1.0 고정.
                        new ConfigSetting { name = KeyUIScale, type = TypeFloat, value = "1", min = 1f, max = 1f },
                        new ConfigSetting { name = "aimLength", type = TypeInt, value = "10", min = 1f, max = 20f },
                        new ConfigSetting { name = "aimThick", type = TypeInt, value = "1", min = 1f, max = 5f },
                        new ConfigSetting { name = "aimGap", type = TypeInt, value = "3", min = 0f, max = 5f },
                        // 크로스헤어 정적/동적. true=고정, false=반동(ErrorRange)으로 벌어짐.
                        new ConfigSetting { name = "StaticAim", type = TypeBool, value = "false", min = 0f, max = 0f },
                        new ConfigSetting { name = "aimColorR", type = TypeFloat, value = "1", min = 0f, max = 1f },
                        new ConfigSetting { name = "aimColorG", type = TypeFloat, value = "0", min = 0f, max = 1f },
                        new ConfigSetting { name = "aimColorB", type = TypeFloat, value = "0", min = 0f, max = 1f },
                        new ConfigSetting { name = "aimColorA", type = TypeFloat, value = "1", min = 0f, max = 1f },
                        new ConfigSetting { name = "playerName", type = TypeString, value = "xopsPlayer", min = 0f, max = 0f },
                        new ConfigSetting { name = KeyAllowConsole, type = TypeBool, value = "false", min = 0f, max = 0f },
                        // 스크립트 이벤트(20 이상의 포인트 종류)를 돌릴지. 끄면 그것을 쓰는 미션은 로드하지 않는다. 옵션 화면에 없는 설정이다.
                        new ConfigSetting { name = KeyAllowEventScript, type = TypeBool, value = "true", min = 0f, max = 0f },
                        new ConfigSetting { name = KeyAllowUiScript, type = TypeBool, value = "true", min = 0f, max = 0f },
                    },
                },
                new ConfigSection
                {
                    name = SectionInput,
                    settings = new List<ConfigSetting>
                    {
                        new ConfigSetting { name = KeyInvertY, type = TypeBool, value = "false", min = 0f, max = 0f },
                        new ConfigSetting { name = KeySensitivity, type = TypeFloat, value = "0.1", min = 0.01f, max = 1f },
                    },
                },
                new ConfigSection
                {
                    name = SectionGraphic,
                    settings = new List<ConfigSetting>
                    {
                        // 밝기 곱(노출). 화면 후처리 셰이더가 색에 곱한다. 1.0=원본, 0.5~1.5.
                        new ConfigSetting { name = KeyBrightness, type = TypeFloat, value = "1", min = 0.5f, max = 1.5f },
                        // 감마(모니터 관례). pow(c, gamma). 1.0=중립, 값이 클수록 어둡고 작을수록 밝다.
                        new ConfigSetting { name = KeyGamma, type = TypeFloat, value = "1", min = 0.5f, max = 2f },
                        new ConfigSetting { name = KeyFullscreen, type = TypeBool, value = "true", min = 0f, max = 0f },
                        // 해상도는 인덱스(4:3=0~5, 16:9=10~15). 기본 640x480=0. min/max는 인덱스 범위.
                        new ConfigSetting { name = KeyResolution, type = TypeInt, value = "0", min = 0f, max = 15f },
                        new ConfigSetting { name = KeyVsync, type = TypeBool, value = "false", min = 0f, max = 0f },
                        new ConfigSetting { name = KeyLimitFrame, type = TypeBool, value = "true", min = 0f, max = 0f },
                        new ConfigSetting { name = KeyFrameLimit, type = TypeInt, value = "60", min = 60f, max = 480f },
                        new ConfigSetting { name = "nearClippingPlane", type = TypeFloat, value = "0.1", min = 0.03f, max = 0.1f },
                        new ConfigSetting { name = "farClippingPlane", type = TypeFloat, value = "80", min = 80f, max = 1000f },
                        new ConfigSetting { name = "fov", type = TypeInt, value = "65", min = 60f, max = 90f },
                    },
                },
                new ConfigSection
                {
                    name = SectionSound,
                    settings = new List<ConfigSetting>
                    {
                        new ConfigSetting { name = KeyMasterVolume, type = TypeFloat, value = "1", min = 0f, max = 1f },
                    },
                },
            };
        }

        /// <summary>
        /// 기본 입력 바인딩 정의를 만든다(원본 XOPS 조작 기준).
        /// </summary>
        /// <returns>기본 입력 액션 정의 배열.</returns>
        private static InputActionDefinition[] BuildDefaultBindings()
        {
            return new[]
            {
                new InputActionDefinition
                {
                    name = InputManager.Look,
                    type = "PassThrough",
                    bindings = new[] { InputPath.MouseDelta },
                    composites = new[]
                    {
                        new InputCompositeDefinition
                        {
                            type = "2DVector",
                            up = "<Keyboard>/upArrow", down = "<Keyboard>/downArrow",
                            left = "<Keyboard>/leftArrow", right = "<Keyboard>/rightArrow",
                        },
                    },
                },
                new InputActionDefinition
                {
                    name = InputManager.Move,
                    type = "Value",
                    composites = new[]
                    {
                        new InputCompositeDefinition
                        {
                            type = "2DVector",
                            up = "<Keyboard>/w", down = "<Keyboard>/s",
                            left = "<Keyboard>/a", right = "<Keyboard>/d",
                        },
                    },
                },
                new InputActionDefinition { name = InputManager.Jump, type = "Button", bindings = new[] { "<Keyboard>/space" } },
                new InputActionDefinition { name = InputManager.Walk, type = "Button", bindings = new[] { "<Keyboard>/tab" } },
                new InputActionDefinition { name = InputManager.Drop, type = "Button", bindings = new[] { "<Keyboard>/g" } },
                new InputActionDefinition { name = InputManager.Fire, type = "Button", bindings = new[] { "<Mouse>/leftButton" } },
                new InputActionDefinition { name = InputManager.Zoom, type = "Button", bindings = new[] { "<Keyboard>/leftShift" } },
                new InputActionDefinition { name = InputManager.Previous, type = "Button", bindings = new[] { "<Keyboard>/z" } },
                new InputActionDefinition { name = InputManager.Next, type = "Button", bindings = new[] { "<Keyboard>/x" } },
                new InputActionDefinition { name = InputManager.Reload, type = "Button", bindings = new[] { "<Keyboard>/r" } },
                new InputActionDefinition { name = InputManager.First, type = "Button", bindings = new[] { "<Keyboard>/1" } },
                new InputActionDefinition { name = InputManager.Second, type = "Button", bindings = new[] { "<Keyboard>/2" } },
                new InputActionDefinition { name = InputManager.Interact, type = "Button", bindings = new[] { "<Keyboard>/f" } },
                new InputActionDefinition { name = InputManager.Escape, type = "Button", bindings = new[] { "<Keyboard>/escape" } },
            };
        }
    }
}
