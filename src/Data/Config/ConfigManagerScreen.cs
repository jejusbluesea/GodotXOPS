using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    public partial class ConfigManager
    {
        // 지원 해상도. Graphic 섹션의 "resolution" 설정에 인덱스로 저장된다.
        // 4:3 = 0~5, 16:9 = 10~15. 매핑되지 않는 값은 640x480으로 폴백한다.
        // maxUIScale = 그 해상도에서 허용하는 UIScale 상한(하한은 항상 1.0). 화면이 클수록 큰 UI 배수를 허용한다.
        private static readonly (int index, int width, int height, float maxUIScale)[] s_resolutions =
        {
            (0, 640, 480, 1.0f), (1, 800, 600, 1.2f), (2, 1024, 768, 1.6f),
            (3, 1280, 960, 2.0f), (4, 1600, 1200, 2.5f), (5, 2048, 1536, 3.2f),
            (10, 960, 540, 1.1f), (11, 1280, 720, 1.5f), (12, 1600, 900, 1.8f),
            (13, 1920, 1080, 2.2f), (14, 2560, 1440, 3.0f), (15, 3840, 2160, 4.5f),
        };

        private const int k_fallbackWidth = 640;
        private const int k_fallbackHeight = 480;
        private const float k_minUIScale = 1f;

        // 디스플레이 네이티브 해상도 이하로 걸러낸 프리셋 목록. 최초 조회 시 1회 계산해 캐시한다.
        private (int index, int width, int height)[] m_availableResolutions;

        private (int index, int width, int height)[] AvailableResolutions => m_availableResolutions ??= BuildAvailableResolutions();

        // 선택 가능한 해상도 옵션 개수(디스플레이 지원분만). 옵션 UI가 순회할 때 쓴다.
        public int ResolutionOptionCount => AvailableResolutions.Length;

        /// <summary>
        /// 프리셋 중 현재 디스플레이가 감당 가능한(네이티브 이하) 것만 남긴 목록을 만든다.
        /// 화면비 차이는 레터박스가 처리하므로 4:3/16:9를 모두 남기고 크기만 걸러낸다.
        /// </summary>
        /// <returns>표시 가능한 해상도 목록. 하나도 없으면 640x480 하나.</returns>
        private static (int index, int width, int height)[] BuildAvailableResolutions()
        {
            Vector2I screen = DisplayServer.ScreenGetSize();
            var list = new List<(int, int, int)>();
            foreach ((int index, int width, int height, float maxUIScale) r in s_resolutions)
            {
                if (r.width <= screen.X && r.height <= screen.Y)
                {
                    list.Add((r.index, r.width, r.height));
                }
            }
            if (list.Count == 0)
            {
                list.Add((0, k_fallbackWidth, k_fallbackHeight));
            }
            return list.ToArray();
        }

        /// <summary>
        /// i번째(0-기반) 해상도 옵션의 "resolution" 설정에 저장할 인덱스 값을 반환한다.
        /// </summary>
        /// <param name="i">옵션 순번(0 ~ ResolutionOptionCount-1).</param>
        /// <returns>해당 옵션의 저장 인덱스. 범위 밖이면 -1.</returns>
        public int ResolutionOptionIndexAt(int i)
        {
            return (i >= 0 && i < AvailableResolutions.Length) ? AvailableResolutions[i].index : -1;
        }

        /// <summary>
        /// i번째(0-기반) 해상도 옵션의 표시 라벨("640x480")을 반환한다.
        /// </summary>
        /// <param name="i">옵션 순번.</param>
        /// <returns>"width x height" 라벨. 범위 밖이면 빈 문자열.</returns>
        public string ResolutionOptionLabelAt(int i)
        {
            if (i < 0 || i >= AvailableResolutions.Length)
            {
                return string.Empty;
            }
            return AvailableResolutions[i].width + "x" + AvailableResolutions[i].height;
        }

        /// <summary>
        /// Graphic 섹션의 fullscreen/resolution/vsync/프레임 제한 설정을 화면에 적용한다. 부팅 시 1회 호출되며 옵션 UI가 재호출할 수 있다.
        /// 지정 해상도는 렌더 해상도로 쓰고(루트 뷰포트 크기), 창과 화면비가 다르면 검은 띠를 둔다.
        /// 전체화면은 보더리스(창은 디스플레이 네이티브), 창모드는 지정 해상도를 창 크기로 쓴다.
        /// </summary>
        public void ApplyGraphic()
        {
            bool fullscreen = GetBool(SectionGraphic, KeyFullscreen);
            (int width, int height) = ResolveResolution(GetInt(SectionGraphic, KeyResolution));
            var size = new Vector2I(width, height);

            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            root.ContentScaleSize = size;

            if (fullscreen)
            {
                root.Mode = Window.ModeEnum.Fullscreen;
            }
            else
            {
                root.Mode = Window.ModeEnum.Windowed;
                root.Size = size;
                root.MoveToCenter();
            }

            // VSync / 프레임 제한(전역). vsync가 켜져 있으면 프레임 제한은 쓰지 않는다.
            bool vsync = GetBool(SectionGraphic, KeyVsync);
            DisplayServer.WindowSetVsyncMode(vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            bool limitFrame = GetBool(SectionGraphic, KeyLimitFrame);
            Engine.MaxFps = (!vsync && limitFrame) ? GetInt(SectionGraphic, KeyFrameLimit) : 0;
        }

        /// <summary>
        /// 해상도 인덱스를 실제 픽셀 크기로 변환한다. 매핑 실패나 디스플레이 네이티브 초과 시 640x480으로 폴백한다.
        /// </summary>
        /// <param name="index">해상도 인덱스(0~5, 10~15).</param>
        /// <returns>적용할 (width, height).</returns>
        private static (int width, int height) ResolveResolution(int index)
        {
            Vector2I screen = DisplayServer.ScreenGetSize();
            foreach ((int index, int width, int height, float maxUIScale) r in s_resolutions)
            {
                if (r.index != index)
                {
                    continue;
                }
                if (r.width > screen.X || r.height > screen.Y)
                {
                    return (k_fallbackWidth, k_fallbackHeight);
                }
                return (r.width, r.height);
            }
            return (k_fallbackWidth, k_fallbackHeight);
        }

        /// <summary>
        /// 현재 선택된 해상도가 허용하는 UIScale 상한을 반환한다.
        /// </summary>
        /// <returns>UIScale 최대값. 매핑되지 않는 해상도면 1.0.</returns>
        public float MaxUIScale()
        {
            int current = GetInt(SectionGraphic, KeyResolution);
            foreach ((int index, int width, int height, float maxUIScale) r in s_resolutions)
            {
                if (r.index == current)
                {
                    return r.maxUIScale;
                }
            }
            return k_minUIScale;
        }

        /// <summary>
        /// UIScale 설정의 허용 범위를 현재 해상도에 맞춰 갱신하고(min 1.0 ~ 해상도별 max), 저장값이 범위를 벗어나면 맞춰 고친다.
        /// 큰 해상도에서 UIScale을 올려둔 뒤 작은 해상도로 바꾸면 여기서 상한으로 내려간다.
        /// 부팅 시, 해상도 변경 시(SetInt), BACK/RESET 복원 시(RestoreValues) 자동 호출된다.
        /// </summary>
        public void ApplyUIScaleLimit()
        {
            ConfigSetting setting = FindSetting(SectionGeneral, KeyUIScale);
            if (setting == null)
            {
                return;
            }

            float max = MaxUIScale();
            setting.min = k_minUIScale;
            setting.max = max;

            // GetFloat는 읽을 때 클램프하므로 저장된 raw 값을 직접 보고 고친다.
            if (TryParseFloat(setting.value, out float value))
            {
                float clamped = Mathf.Clamp(value, k_minUIScale, max);
                if (!Mathf.IsEqualApprox(clamped, value))
                {
                    SetFloat(SectionGeneral, KeyUIScale, clamped);
                }
            }
        }
    }
}
