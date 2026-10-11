using Godot;

namespace GodotXOPS
{
    // EventManager 의 화면 이미지 담당 partial: 이벤트가 이미지 목록의 번호로 화면에 놓는 그림. 화면 글자(EventManagerHud)와 같은 방식의 칸이다.
    // 화면 스크립트와 무관하게 메인게임이 늘 그리고, 글자보다 아래에 그린다.
    public partial class EventManager
    {
        // 이벤트가 화면에 놓을 수 있는 이미지 칸의 수.
        public const int HudImageSlotCount = 16;

        // 한 변의 상한 (화면 높이 480 기준). 화면보다 훨씬 큰 값을 막는다.
        private const float k_imageMaxSize = 4096f;
        // 서서히 나타나고 사라지는 시간의 상한 (초).
        private const float k_imageMaxFadeSeconds = 60f;

        /// <summary>
        /// 화면 이미지 칸 하나.
        /// </summary>
        private sealed class HudImageSlot
        {
            public bool active;
            public int image;
            public int anchor;
            public float x;
            public float y;
            public float width;
            public float height;
            public Color color = Colors.White;
            // 남은 틱. 음수면 지울 때까지 남는다.
            public int ticksLeft;
            // 나타나고 사라지는 데 걸리는 틱. 0 이면 바로다.
            public int fadeTicks;
            // 놓인 뒤 지난 틱 (나타나는 중의 진하기).
            public int age;
            // 지우기로 사라지는 중이면 남은 틱과 그 전체. 아니면 -1.
            public int closeLeft = -1;
            public int closeTotal;
            // 진하기의 배율 (0 에서 1). 지난 틱과 이번 틱의 값을 프레임이 보간한다.
            public float fadePrevious = 1f;
            public float fadeNow = 1f;
            // 놓인 순서. 작을수록 먼저 놓였고 뒤에 그려진다.
            public long order;
        }

        private readonly HudImageSlot[] m_hudImages = new HudImageSlot[HudImageSlotCount];
        private long m_hudImageOrder;
        private int m_hudImageRevision;

        // 화면 이미지의 구성이 바뀔 때마다 오르는 값. HUD 는 이 값이 바뀌었을 때만 HudImages 를 다시 읽는다. 진하기는 HudImageFade 로 프레임마다 읽는다.
        public int HudImageRevision => m_hudImageRevision;

        /// <summary>
        /// 화면에 보이는 이미지들을 놓인 순서대로 돌려준다 (먼저 놓인 것이 앞. HUD 는 이 순서대로 그려서 나중 것이 위에 온다).
        /// 좌표와 크기는 화면 높이를 480 으로 본 값이고, 기준점에서 +x 오른쪽, +y 위쪽이다.
        /// </summary>
        /// <returns>칸마다 slot, image(이미지 목록의 번호), anchor, x, y, width, height, color 를 담은 사전의 배열. 너비나 높이가 0 이면 그리는 쪽이 이미지의 크기와 비율로 정한다.</returns>
        public Godot.Collections.Array HudImages()
        {
            var result = new Godot.Collections.Array();
            long last = -1;
            while (true)
            {
                int next = -1;
                for (int i = 0; i < HudImageSlotCount; i++)
                {
                    HudImageSlot slot = m_hudImages[i];
                    if (slot == null || !slot.active || slot.order <= last) continue;
                    if (next < 0 || slot.order < m_hudImages[next].order) next = i;
                }
                if (next < 0) break;

                HudImageSlot found = m_hudImages[next];
                last = found.order;
                result.Add(new Godot.Collections.Dictionary
                {
                    ["slot"] = next,
                    ["image"] = found.image,
                    ["anchor"] = found.anchor,
                    ["x"] = found.x,
                    ["y"] = found.y,
                    ["width"] = found.width,
                    ["height"] = found.height,
                    ["color"] = found.color,
                });
            }
            return result;
        }

        /// <summary>
        /// 그 칸의 이미지가 나타나거나 사라지는 중의 진하기 배율. 틱 사이를 보간한 값이고, 이미지의 색에 든 투명도에 곱해 쓴다.
        /// </summary>
        /// <param name="slot">칸 번호.</param>
        /// <returns>0 에서 1. 비어 있는 칸이면 0.</returns>
        public float HudImageFade(int slot)
        {
            if (slot < 0 || slot >= HudImageSlotCount || m_hudImages[slot] == null || !m_hudImages[slot].active) return 0f;

            HudImageSlot target = m_hudImages[slot];
            return Mathf.Lerp(target.fadePrevious, target.fadeNow, SimClock.EventAlpha);
        }

        /// <summary>
        /// 화면의 한 칸에 이미지를 놓는다. 같은 칸에 다시 놓으면 내용만 바뀌고 그리는 순서는 그대로다 (나타나는 중이던 진하기도 이어진다).
        /// </summary>
        /// <param name="slot">칸 번호 (0 부터 HudImageSlotCount 미만).</param>
        /// <param name="image">이미지 목록의 번호 (10000 이상은 미션의 목록). 목록에 없는 번호면 놓지 않는다.</param>
        /// <param name="options">
        /// 선택 값. 없는 키는 기본값이다: anchor(0~8, 기본 4 화면 가운데), x·y(기준점에서의 오프셋, 화면 높이 480 기준),
        /// width·height(0 이면 이미지의 크기와 비율을 따른다), color(0xRRGGBB 정수, 이미지에 곱하는 색. 기본 흰색), alpha(0~1),
        /// seconds(보이는 시간. 0 이면 지울 때까지), fade(나타나고 사라지는 데 걸리는 초. 0 이면 바로).
        /// </param>
        /// <returns>놓았으면 true.</returns>
        public bool SetHudImage(int slot, int image, Godot.Collections.Dictionary options)
        {
            if (slot < 0 || slot >= HudImageSlotCount) return false;
            if (!DataManager.Instance.ImageParameterData.imageData.Has(image))
            {
                Debugger.LogWarning($"Event image {image} is not in the image list", "Event");
                return false;
            }

            int anchor = Mathf.Clamp(ReadInt(options, "anchor", 4), 0, k_hudAnchorCount - 1);
            float x = ReadFloat(options, "x", 0f);
            float y = ReadFloat(options, "y", 0f);
            float width = Mathf.Clamp(ReadFloat(options, "width", 0f), 0f, k_imageMaxSize);
            float height = Mathf.Clamp(ReadFloat(options, "height", 0f), 0f, k_imageMaxSize);
            int rgb = ReadInt(options, "color", 0xFFFFFF);
            var color = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, Mathf.Clamp(ReadFloat(options, "alpha", 1f), 0f, 1f));
            float seconds = ReadFloat(options, "seconds", 0f);
            int ticks = seconds > 0f ? Mathf.Max(1, Mathf.RoundToInt(seconds * SimClock.FrameRate)) : -1;
            int fadeTicks = Mathf.RoundToInt(Mathf.Clamp(ReadFloat(options, "fade", 0f), 0f, k_imageMaxFadeSeconds) * SimClock.FrameRate);

            HudImageSlot target = m_hudImages[slot] ??= new HudImageSlot();
            bool changed = !target.active || target.image != image || target.anchor != anchor || target.x != x || target.y != y
                || target.width != width || target.height != height || target.color != color;
            if (!target.active)
            {
                target.order = ++m_hudImageOrder;
                target.age = 0;
                target.fadePrevious = fadeTicks > 0 ? 0f : 1f;
                target.fadeNow = target.fadePrevious;
            }
            target.active = true;
            target.image = image;
            target.anchor = anchor;
            target.x = x;
            target.y = y;
            target.width = width;
            target.height = height;
            target.color = color;
            target.ticksLeft = ticks;
            target.fadeTicks = fadeTicks;
            target.closeLeft = -1;
            if (changed) m_hudImageRevision++;
            return true;
        }

        /// <summary>
        /// 화면의 이미지 칸을 지운다.
        /// </summary>
        /// <param name="slot">칸 번호. 음수면 전부 지운다.</param>
        /// <param name="fadeSeconds">사라지는 데 걸리는 초. 0 이하면 바로 지운다.</param>
        public void ClearHudImage(int slot, float fadeSeconds)
        {
            int fadeTicks = float.IsFinite(fadeSeconds) ? Mathf.RoundToInt(Mathf.Clamp(fadeSeconds, 0f, k_imageMaxFadeSeconds) * SimClock.FrameRate) : 0;
            for (int i = 0; i < HudImageSlotCount; i++)
            {
                HudImageSlot target = m_hudImages[i];
                if ((slot >= 0 && i != slot) || target == null || !target.active) continue;

                if (fadeTicks <= 0)
                {
                    target.active = false;
                    m_hudImageRevision++;
                }
                else if (target.closeLeft < 0)
                {
                    target.closeLeft = fadeTicks;
                    target.closeTotal = fadeTicks;
                }
            }
        }

        /// <summary>
        /// 틱의 끝에: 이미지의 남은 시간을 줄이고 진하기를 다시 구한다. 다 된 것은 지운다.
        /// </summary>
        private void TickHudImages()
        {
            for (int i = 0; i < HudImageSlotCount; i++)
            {
                HudImageSlot slot = m_hudImages[i];
                if (slot == null || !slot.active) continue;

                slot.age++;
                bool done = slot.ticksLeft >= 0 && --slot.ticksLeft <= 0;
                if (slot.closeLeft >= 0 && --slot.closeLeft <= 0) done = true;
                if (done)
                {
                    slot.active = false;
                    m_hudImageRevision++;
                    continue;
                }

                // 나타나는 중, 정해진 시간이 끝나 가는 중, 지우기로 사라지는 중 가운데 가장 옅은 값을 쓴다.
                float fade = 1f;
                if (slot.fadeTicks > 0)
                {
                    fade = Mathf.Min(fade, (float)slot.age / slot.fadeTicks);
                    if (slot.ticksLeft >= 0) fade = Mathf.Min(fade, (float)slot.ticksLeft / slot.fadeTicks);
                }
                if (slot.closeLeft >= 0) fade = Mathf.Min(fade, (float)slot.closeLeft / slot.closeTotal);
                slot.fadePrevious = slot.fadeNow;
                slot.fadeNow = Mathf.Clamp(fade, 0f, 1f);
            }
        }

        /// <summary>
        /// 화면 이미지를 전부 지운다 (미션 시작, 종료, 맵 내리기).
        /// </summary>
        private void ResetHudImages()
        {
            ClearHudImage(-1, 0f);
        }
    }
}
