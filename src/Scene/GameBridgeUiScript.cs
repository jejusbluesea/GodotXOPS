using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    // GameBridge 의 화면 스크립트 담당 partial: 등록 파일(godotdata/ui/*.json)을 찾아 SafeGDScript 를 샌드박스에 올리고,
    // 화면(GDScript 의 XopsScriptScreen)이 그 노드를 부른다. 격리는 SandboxScript 가 건다.
    public partial class GameBridge
    {
        private const string k_uiPackFolder = "godotdata/ui";
        // 등록 파일 하나가 가질 수 있는 이미지 수의 상한.
        private const int k_uiMaxImages = 256;

        // 화면 스크립트의 함수 한 번이 만들 수 있는 값(사전, 배열, 글자)의 수. 확장의 기본값 100 으로는 요소 수십 개를 만드는 build 가 돌지 못한다.
        private const int k_uiMaxReferences = 8000;
        // 화면을 만드는 호출(init, build)의 실행 예산은 평소의 이 배수다. OPTION 처럼 요소가 200개를 넘는 화면은 평소 예산으로 만들지 못한다.
        private const int k_uiBuildBudgetScale = 8;
        // 프레임마다의 호출(frame)의 실행 예산도 스크립트 이벤트보다 크게 준다. 사전과 배열을 다루는 것도 예산을 쓰는데, 탭을 바꿀 때처럼 요소 수백 개를 한 프레임에 고치는 일이 있다.
        // 끝나지 않는 루프는 이 예산만큼(약 2초) 게임을 멈춘 뒤 끊기고, 그 화면은 기본 화면으로 돌아간다.
        private const int k_uiFrameBudgetScale = 4;

        /// <summary>
        /// 올려 둔 화면 스크립트 하나: 샌드박스와 그 등록 파일의 이미지 목록.
        /// </summary>
        private sealed class UiScriptSlot
        {
            public readonly SandboxScript script = new SandboxScript();
            public readonly List<string> imagePaths = new List<string>();
            public readonly Dictionary<int, Texture2D> images = new Dictionary<int, Texture2D>();
        }

        // 화면 이름 → 올려 둔 스크립트. 화면 위에 다른 화면이 덮일 수 있어서(메뉴 위의 OPTION) 여럿이 함께 올라가 있을 수 있다.
        private readonly Dictionary<string, UiScriptSlot> m_uiScripts = new Dictionary<string, UiScriptSlot>(StringComparer.OrdinalIgnoreCase);
        private string m_uiOverride = string.Empty;

        // 화면 스크립트가 실패해 기본 화면으로 되돌린 횟수 (개발용 인자가 종료 코드에 쓴다).
        public int UiScriptFailures { get; private set; }

        /// <summary>
        /// 등록 폴더(godotdata/ui)를 훑는 대신 이 등록 파일 하나, 또는 이 폴더의 등록 파일들만 쓰게 한다 (개발용 인자 --ui-script).
        /// </summary>
        /// <param name="registryPath">등록 파일 하나(.json)나 등록 파일들이 든 폴더의 경로 (exe 폴더 기준). 빈 문자열이면 평소대로 godotdata/ui 를 훑는다.</param>
        public void SetUiScriptOverride(string registryPath)
        {
            m_uiOverride = registryPath ?? string.Empty;
        }

        /// <summary>
        /// 그 화면을 맡겠다고 등록한 스크립트를 샌드박스에 올린다. 스크립트의 함수는 부르지 않는다.
        /// 같은 화면의 것이 이미 올라가 있으면 내리고 다시 올린다. 다른 화면의 것은 그대로 둔다.
        /// </summary>
        /// <param name="screen">화면 이름 (예: "maingame").</param>
        /// <returns>스크립트가 붙은 노드 (트리 밖). 등록이 없거나, 설정에서 꺼져 있거나, 올리지 못했으면 null.</returns>
        public Node UiScriptLoad(string screen)
        {
            if (string.IsNullOrEmpty(screen)) return null;
            UiScriptFree(screen);
            if (!ConfigManager.Instance.GetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowUiScript, true)) return null;

            UiPackData pack = FindUiPack(screen, out string source);
            if (pack == null) return null;

            var slot = new UiScriptSlot();
            if (!slot.script.Load(pack.scriptPath, "screen", out string error, k_uiMaxReferences))
            {
                Debugger.LogError($"{error} (registered in {source}). Using the built-in screen.", nameof(GameBridge));
                return null;
            }

            if (pack.images != null)
            {
                foreach (string path in pack.images)
                {
                    if (slot.imagePaths.Count >= k_uiMaxImages) break;
                    slot.imagePaths.Add(path ?? string.Empty);
                }
            }
            m_uiScripts[screen] = slot;
            return slot.script.Node;
        }

        /// <summary>
        /// 그 화면의 스크립트의 샌드박스가 지금까지 센 예외 횟수. 호출 앞뒤의 값을 견줘 그 호출이 실패했는지 안다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        /// <returns>예외 횟수. 올라가 있지 않으면 0.</returns>
        public int UiScriptExceptions(string screen)
        {
            return FindUiScript(screen)?.script.Exceptions() ?? 0;
        }

        /// <summary>
        /// 화면 스크립트가 실패한 이유를 로그(디버그 콘솔)에 남긴다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        /// <param name="message">이유 (영어).</param>
        public void UiScriptLogError(string screen, string message)
        {
            UiScriptFailures++;
            Debugger.LogError($"Screen script {UiScriptLabel(screen)}: {message}. Using the built-in screen.", nameof(GameBridge));
        }

        /// <summary>
        /// 화면 스크립트가 남기는 글을 로그(디버그 콘솔)에 경고로 남긴다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        /// <param name="message">글.</param>
        public void UiScriptLog(string screen, string message)
        {
            Debugger.LogWarning($"[{UiScriptLabel(screen)}] {message}", nameof(GameBridge));
        }

        /// <summary>
        /// 그 화면의 등록 파일의 이미지 목록에서 그 번호의 이미지를 텍스처로 읽는다. 한 번 읽은 것은 스크립트를 내릴 때까지 갖고 있는다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        /// <param name="index">이미지 목록의 번호 (0 부터).</param>
        /// <returns>텍스처. 없는 번호이거나 읽지 못하면 null.</returns>
        public Texture2D UiScriptImage(string screen, int index)
        {
            UiScriptSlot slot = FindUiScript(screen);
            if (slot == null || index < 0 || index >= slot.imagePaths.Count) return null;
            if (slot.images.TryGetValue(index, out Texture2D cached)) return cached;

            // 경로는 등록 파일이 정한 것이고 게임 폴더 밖을 가리키면 Resolve 가 거절한다.
            Texture2D texture = LoadTexture(slot.imagePaths[index]);
            if (texture == null) Debugger.LogWarning($"Screen script image {index} could not be read: {slot.imagePaths[index]}", nameof(GameBridge));
            slot.images[index] = texture;
            return texture;
        }

        /// <summary>
        /// 그 화면의 스크립트를 내린다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        public void UiScriptFree(string screen)
        {
            if (string.IsNullOrEmpty(screen) || !m_uiScripts.Remove(screen, out UiScriptSlot slot)) return;
            slot.script.Free();
        }

        /// <summary>
        /// 그 화면의 스크립트의 실행 예산을 정한다. 화면을 만드는 init / build 는 요소를 한꺼번에 만들므로 더 큰 예산을 주고, frame 은 평소 예산으로 돌린다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        /// <param name="building">true 면 화면을 만드는 호출의 예산, false 면 평소 예산.</param>
        public void UiScriptSetBuilding(string screen, bool building)
        {
            FindUiScript(screen)?.script.SetBudgetScale(building ? k_uiBuildBudgetScale : k_uiFrameBudgetScale);
        }

        private UiScriptSlot FindUiScript(string screen)
        {
            return !string.IsNullOrEmpty(screen) && m_uiScripts.TryGetValue(screen, out UiScriptSlot slot) ? slot : null;
        }

        private string UiScriptLabel(string screen)
        {
            return FindUiScript(screen)?.script.Label ?? screen ?? string.Empty;
        }

        /// <summary>
        /// 메인게임의 화면 스크립트에 프레임마다 넘기는 값. 스크립트가 하나씩 묻는 것보다 사전 하나로 넘기는 쪽이 싸다 (script_probe 로 잰 값).
        /// </summary>
        /// <returns>플레이어와 미션의 상태를 담은 사전. 키는 docs/modding.md 의 "화면 스크립트"에 있다.</returns>
        public Godot.Collections.Dictionary HudValues()
        {
            EventManager events = EventManager.Instance;
            Human player = Player;
            return new Godot.Collections.Dictionary
            {
                { "exists", PlayerExists() },
                { "alive", PlayerAlive() },
                { "index", PlayerIndex() },
                { "hp", PlayerHP() },
                { "armor", player != null ? player.Armor : 0f },
                { "armor_max", player != null ? player.MaxArmor : 0f },
                { "helmet", player != null ? player.Helmet : 0f },
                { "helmet_max", player != null ? player.MaxHelmet : 0f },
                { "magazine", Magazine() },
                { "reserve", Reserve() },
                { "weapon", WeaponName() },
                { "reloading", IsReloading() },
                { "switching", IsSwitchingWeapon() },
                { "first_person", IsFirstPerson() },
                { "crosshair", ShowsCrosshair() },
                { "error_range", ErrorRange() },
                { "scoping", IsScoping() },
                { "scope_index", ScopeIndex() },
                { "blind", GetWallBlind() },
                { "result", events.Result },
                { "start_count", events.StartCount },
                { "message_id", events.MessageId },
                { "message_text", events.MessageText },
                { "message_alpha", events.MessageAlpha },
                { "hud_visible", events.HudVisible },
            };
        }

        /// <summary>
        /// 그 화면을 등록한 파일을 찾는다. 같은 화면을 여럿이 등록했으면 파일 이름 순서로 처음 것을 쓰고 경고를 남긴다.
        /// </summary>
        /// <param name="screen">화면 이름.</param>
        /// <param name="source">찾은 등록 파일의 경로 (exe 폴더 기준).</param>
        /// <returns>등록 내용. 없으면 null.</returns>
        private UiPackData FindUiPack(string screen, out string source)
        {
            source = string.Empty;
            var files = new List<string>();
            if (!string.IsNullOrEmpty(m_uiOverride))
            {
                string full = GamePath.Resolve(m_uiOverride);
                if (full != null && Directory.Exists(full))
                {
                    files.AddRange(Directory.GetFiles(full, "*.json"));
                    files.Sort(StringComparer.OrdinalIgnoreCase);
                }
                else if (full != null && File.Exists(full)) files.Add(full);
                else Debugger.LogError($"Screen script registry open failed: {m_uiOverride}", nameof(GameBridge));
            }
            else
            {
                string folder = GamePath.Resolve(k_uiPackFolder);
                if (folder == null || !Directory.Exists(folder)) return null;
                files.AddRange(Directory.GetFiles(folder, "*.json"));
                files.Sort(StringComparer.OrdinalIgnoreCase);
            }

            UiPackData found = null;
            foreach (string fullPath in files)
            {
                string label = Path.GetRelativePath(GamePath.Root, fullPath).Replace('\\', '/');
                var data = new UiPackData();
                try
                {
                    if (!JsonData.Overwrite(EncodingHelper.ReadAllText(fullPath), data, label)) continue;
                }
                catch (IOException e)
                {
                    Debugger.LogError($"Screen script registry read failed: {label} ({e.Message})", nameof(GameBridge));
                    continue;
                }

                if (!string.Equals(data.screen, screen, StringComparison.OrdinalIgnoreCase)) continue;
                if (found != null)
                {
                    Debugger.LogWarning($"Screen '{screen}' is registered by both {source} and {label}. Using {source}.", nameof(GameBridge));
                    continue;
                }
                found = data;
                source = label;
            }
            return found;
        }
    }
}
