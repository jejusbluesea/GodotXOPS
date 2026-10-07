using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 스크립트 이벤트가 게임을 조작하는 유일한 창구. 함수들을 사전에 담아 스크립트의 init(api) 에 넘기고, 스크립트는 api["이름"].call(...) 로 부른다.
    /// 값(정수, 실수, 불, 문자열, 값만 담은 사전)만 주고받는다. 노드나 객체를 넘기지 않는다 (넘긴 객체는 샌드박스의 제한과 무관하게 접근된다).
    /// 파일 경로를 받는 함수를 두지 않는다. 사람은 MapLoader.Humans 의 인덱스로, 포인트·소물은 식별번호로, 데이터는 번호로 가리킨다.
    /// 좌표는 Godot 공간의 미터(디버그 콘솔의 info 와 같다), 각도는 사람 기준 yaw(도)다.
    /// </summary>
    public sealed class EventApi
    {
        // 이벤트 함수 한 번이 부를 수 있는 API 호출 수. 넘으면 그 이벤트는 실패로 친다 (호스트 쪽 일을 끝없이 시키는 것을 막는다).
        private const int k_maxCallsPerEvent = 1000;
        // 로그 한 줄의 최대 글자 수.
        private const int k_maxLogChars = 200;

        // 키 이름 가운데 줄여 쓰는 것 (OPTION 화면의 표기와 같다). 그 밖의 키는 이름을 대문자로 쓴다.
        private static readonly Dictionary<string, string> s_keyLabels = new Dictionary<string, string>
        {
            ["leftButton"] = "LMB", ["rightButton"] = "RMB", ["middleButton"] = "MMB",
            ["leftShift"] = "LSHIFT", ["rightShift"] = "RSHIFT", ["leftCtrl"] = "LCTRL", ["rightCtrl"] = "RCTRL",
            ["leftAlt"] = "LALT", ["rightAlt"] = "RALT", ["upArrow"] = "UP", ["downArrow"] = "DOWN", ["leftArrow"] = "LEFT", ["rightArrow"] = "RIGHT",
        };

        private readonly EventManager m_events;
        private int m_calls;

        // 스크립트에 넘기는 함수 표.
        public Godot.Collections.Dictionary Table { get; }
        // 지금 이벤트 함수가 호출 한도를 넘었는지.
        public bool OverBudget => m_calls > k_maxCallsPerEvent;

        /// <summary>
        /// 함수 표를 만든다.
        /// </summary>
        /// <param name="events">이벤트 매니저.</param>
        public EventApi(EventManager events)
        {
            m_events = events;
            Table = new Godot.Collections.Dictionary
            {
                // 조회
                ["human_count"] = Callable.From(() => Enter() ? MapLoader.HumanCount : 0),
                ["human"] = Callable.From((int index) => Enter() ? HumanInfo(index) : new Godot.Collections.Dictionary()),
                ["find_human"] = Callable.From((int id) => Enter() ? FindHuman(id) : -1),
                ["player"] = Callable.From(() => Enter() ? MapLoader.PlayerIndex : -1),
                ["team_alive"] = Callable.From((int team) => Enter() ? TeamAlive(team) : 0),
                ["object"] = Callable.From((int id) => Enter() ? ObjectInfo(id) : new Godot.Collections.Dictionary()),
                ["tick"] = Callable.From(() => Enter() ? m_events.MissionTicks : 0),
                ["look_angle"] = Callable.From((int index, float x, float y, float z) => Enter() ? LookAngle(index, new Vector3(x, y, z)) : 180f),

                // 사람
                ["set_team"] = Callable.From((int index, int team) => { if (Enter()) MapLoader.GetHuman(index)?.SetTeam(team); }),
                ["damage"] = Callable.From((int index, float amount) => { if (Enter()) MapLoader.GetHuman(index)?.ApplyDamage(amount); }),
                ["kill"] = Callable.From((int index) => { if (Enter()) Kill(index); }),
                ["teleport"] = Callable.From((int index, float x, float y, float z) => { if (Enter()) Teleport(index, new Vector3(x, y, z)); }),
                ["give_weapon"] = Callable.From((int index, int slot, int weapon, int bullets) => { if (Enter()) GiveWeapon(index, slot, weapon, bullets); }),
                ["spawn_human"] = Callable.From((int infoId, float x, float y, float z, float yaw, int id, int pathId) =>
                    Enter() ? MapLoader.SpawnHuman(infoId, new Vector3(x, y, z), yaw, id, pathId) : -1),

                // 맵
                ["spawn_weapon"] = Callable.From((int weapon, int bullets, float x, float y, float z, float yaw) =>
                    Enter() && SpawnWeapon(weapon, bullets, new Vector3(x, y, z), yaw)),
                ["spawn_object"] = Callable.From((int objectIndex, int id, float x, float y, float z, float yaw, bool snap) =>
                    Enter() && MapLoader.SpawnSmallObject(objectIndex, id, new Vector3(x, y, z), yaw, snap)),
                ["destroy_object"] = Callable.From((int id) => { if (Enter()) MapLoader.SearchSmallObject(id)?.Break(); }),
                ["set_path_mode"] = Callable.From((int pathId, int mode) => { if (Enter()) SetPathMode(pathId, mode); }),
                ["effect"] = Callable.From((int effect, float x, float y, float z) => { if (Enter()) PlayEffect(effect, new Vector3(x, y, z)); }),

                // 미션
                ["message"] = Callable.From((int id) => { if (Enter()) m_events.ShowMessage(id); }),
                ["message_text"] = Callable.From((int id) => Enter() ? MessageText(id) : string.Empty),
                ["hud_text"] = Callable.From((int slot, string text, Godot.Collections.Dictionary options) => { if (Enter()) m_events.SetHudText(slot, text, options); }),
                ["hud_clear"] = Callable.From((int slot) => { if (Enter()) m_events.ClearHudText(slot); }),
                ["interact_pressed"] = Callable.From(() => Enter() && m_events.InteractPressed),
                ["key_name"] = Callable.From((string action) => Enter() ? KeyLabel(action) : string.Empty),
                ["set_auto_judge"] = Callable.From((bool enabled) => { if (Enter()) m_events.AutoJudge = enabled; }),
                ["end_mission"] = Callable.From((bool complete) => { if (Enter()) m_events.ForceEnd(complete); }),
                ["line_count"] = Callable.From(() => Enter() ? m_events.LineCount : 0),
                ["start_line"] = Callable.From((int line, int pointId) => { if (Enter()) m_events.StartLine(line, pointId); }),
                ["stop_line"] = Callable.From((int line) => { if (Enter()) m_events.StopLine(line); }),

                // 기타
                ["get_var"] = Callable.From((int index) => Enter() ? m_events.GetVariable(index) : 0),
                ["set_var"] = Callable.From((int index, int value) => { if (Enter()) m_events.SetVariable(index, value); }),
                ["random"] = Callable.From((int count) => Enter() && count > 0 ? GameRandom.Gameplay.Range(0, count) : 0),
                ["log"] = Callable.From((string text) => { if (Enter()) Log(text); }),
            };
        }

        /// <summary>
        /// 이벤트 함수를 부르기 직전에 호출 수를 0 으로 되돌린다.
        /// </summary>
        public void BeginCall()
        {
            m_calls = 0;
        }

        /// <summary>
        /// API 호출 하나를 센다.
        /// </summary>
        /// <returns>한도 안이면 true. 넘었으면 false 이고 그 호출은 아무 일도 하지 않는다.</returns>
        private bool Enter()
        {
            return ++m_calls <= k_maxCallsPerEvent;
        }

        /// <summary>
        /// 사람 하나의 상태를 값만 담은 사전으로 만든다.
        /// </summary>
        /// <param name="index">MapLoader.Humans 의 인덱스.</param>
        /// <returns>alive, hp, team, x, y, z, yaw, pitch, weapon(든 무기의 번호), id(식별번호). 없는 인덱스면 빈 사전.</returns>
        private static Godot.Collections.Dictionary HumanInfo(int index)
        {
            var result = new Godot.Collections.Dictionary();
            Human human = MapLoader.GetHuman(index);
            if (human == null) return result;

            Vector3 position = human.Controller.Position;
            result["alive"] = human.Alive;
            result["hp"] = human.HP;
            result["team"] = human.Team;
            result["x"] = position.X;
            result["y"] = position.Y;
            result["z"] = position.Z;
            result["yaw"] = human.Controller.Yaw;
            result["pitch"] = human.Controller.Pitch;
            result["weapon"] = human.CurrentWeapon.WeaponIndex;
            result["id"] = human.Identifier;
            return result;
        }

        /// <summary>
        /// 식별번호로 사람을 찾는다.
        /// </summary>
        /// <param name="id">식별번호.</param>
        /// <returns>첫 매치의 인덱스. 없으면 −1.</returns>
        private static int FindHuman(int id)
        {
            for (int i = 0; i < MapLoader.HumanCount; i++)
            {
                if (MapLoader.Humans[i].Identifier == id) return i;
            }
            return -1;
        }

        /// <summary>
        /// 한 팀의 살아 있는 사람 수를 센다.
        /// </summary>
        /// <param name="team">팀 번호.</param>
        /// <returns>살아 있는 사람 수.</returns>
        private static int TeamAlive(int team)
        {
            int count = 0;
            foreach (Human human in MapLoader.Humans)
            {
                if (human.Team == team && human.Alive) count++;
            }
            return count;
        }

        /// <summary>
        /// 소물 하나의 상태를 값만 담은 사전으로 만든다.
        /// </summary>
        /// <param name="id">식별번호.</param>
        /// <returns>exists, destroyed, hp. 없는 소물이면 exists 가 false.</returns>
        private static Godot.Collections.Dictionary ObjectInfo(int id)
        {
            SmallObject smallObject = MapLoader.SearchSmallObject(id);
            return new Godot.Collections.Dictionary
            {
                ["exists"] = smallObject != null,
                ["destroyed"] = smallObject != null && smallObject.IsDestroyed,
                ["hp"] = smallObject != null ? smallObject.HP : 0f,
            };
        }

        /// <summary>
        /// 사람을 죽인다. 무적인 사람은 죽지 않는다.
        /// </summary>
        /// <param name="index">사람 인덱스.</param>
        private static void Kill(int index)
        {
            Human human = MapLoader.GetHuman(index);
            human?.ApplyDamage(human.HP);
        }

        /// <summary>
        /// 사람을 옮긴다.
        /// </summary>
        /// <param name="index">사람 인덱스.</param>
        /// <param name="position">새 위치 (발밑).</param>
        private static void Teleport(int index, Vector3 position)
        {
            if (!position.IsFinite()) return;
            MapLoader.GetHuman(index)?.Controller.Teleport(position);
        }

        /// <summary>
        /// 사람의 무기 슬롯 하나를 바꾼다. 어느 슬롯을 들고 있는지는 바꾸지 않는다.
        /// </summary>
        /// <param name="index">사람 인덱스.</param>
        /// <param name="slot">슬롯 번호. 1 은 미션을 시작할 때 들고 있는 주 무기 슬롯, 0 은 보조 무기 슬롯이다.</param>
        /// <param name="weapon">무기 번호. 없는 번호면 아무 일도 하지 않는다.</param>
        /// <param name="bullets">전체 탄 수. 음수면 사람 종류의 기본값.</param>
        private static void GiveWeapon(int index, int slot, int weapon, int bullets)
        {
            Human human = MapLoader.GetHuman(index);
            DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
            if (human == null || !human.Alive || !list.Has(weapon) || slot < 0 || slot >= Human.WeaponSlotCount) return;

            if (bullets < 0)
            {
                human.SetWeapon(slot, weapon);
                return;
            }
            int magazine = Mathf.Min(bullets, list[weapon].magazineSize);
            human.SetWeapon(slot, weapon, magazine, bullets - magazine);
        }

        /// <summary>
        /// 떨어진 무기를 놓는다.
        /// </summary>
        /// <param name="weapon">무기 번호.</param>
        /// <param name="bullets">전체 탄 수.</param>
        /// <param name="position">위치.</param>
        /// <param name="yaw">방향 (도).</param>
        /// <returns>놓았으면 true. 없는 번호, 맨손, 떨어진 무기가 가득 찼을 때는 false.</returns>
        private static bool SpawnWeapon(int weapon, int bullets, Vector3 position, float yaw)
        {
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (!WeaponManager.Loaded || !parameter.weaponData.Has(weapon) || weapon == parameter.weaponGeneralData.noneWeaponIndex) return false;
            if (!position.IsFinite()) return false;

            int magazine = Mathf.Clamp(bullets, 0, parameter.weaponData[weapon].magazineSize);
            return WeaponManager.Instance.Spawn(weapon, magazine, Mathf.Max(0, bullets - magazine), position, yaw, Vector3.Zero);
        }

        /// <summary>
        /// 경로 포인트의 이동 모드를 바꾼다 (원본 이벤트 14 는 걷기 0 으로만 바꾼다). 그 포인트에 있거나 앞으로 올 AI 가 새 모드로 움직인다.
        /// 랜덤 분기 포인트는 바꾸지 않는다 (그 포인트의 P2 는 모드가 아니라 갈림길의 한쪽 번호다).
        /// </summary>
        /// <param name="pathId">경로 포인트의 식별번호.</param>
        /// <param name="mode">이동 모드 (경로 포인트의 P2): 0 걷기, 1 달리기, 2 대기, 3 추적, 4 경계 대기, 5 5초 정지, 6 수류탄 투척, 7 우선적 달리기.</param>
        private static void SetPathMode(int pathId, int mode)
        {
            RawPointData path = MapLoader.GetPoint(MapLoader.PointAIPath, pathId);
            if (path != null) path.param1 = mode;
        }

        /// <summary>
        /// 이펙트를 낸다.
        /// </summary>
        /// <param name="effect">이펙트 번호.</param>
        /// <param name="position">위치.</param>
        private static void PlayEffect(int effect, Vector3 position)
        {
            if (!EffectManager.Loaded || !position.IsFinite()) return;
            if (!DataManager.Instance.EffectParameterData.effectData.Has(effect)) return;

            EffectManager.Instance.Play(effect, position);
        }

        /// <summary>
        /// 사람의 시선이 한 점에서 얼마나 벗어나 있는지 구한다. 눈 위치에서 조준 방향과 그 점을 향한 방향 사이의 각도다.
        /// </summary>
        /// <param name="index">사람 인덱스.</param>
        /// <param name="target">바라볼 점.</param>
        /// <returns>각도 (도, 0 이면 정확히 바라봄). 없는 사람이면 180.</returns>
        private static float LookAngle(int index, Vector3 target)
        {
            Human human = MapLoader.GetHuman(index);
            if (human == null || !target.IsFinite()) return 180f;

            HumanController controller = human.Controller;
            Vector3 toTarget = target - (controller.Position + Vector3.Up * controller.CameraHeight);
            if (toTarget.LengthSquared() < Mathf.Epsilon) return 0f;
            return Mathf.RadToDeg(Coord.AimDirection(controller.Yaw, controller.Pitch).AngleTo(toTarget));
        }

        /// <summary>
        /// 미션의 메시지 문구를 돌려준다. 문구 안의 {액션 이름}은 그 액션에 지금 묶인 키 이름으로 바뀐다 (예: {interact} → F).
        /// </summary>
        /// <param name="id">메시지 번호.</param>
        /// <returns>문구. 없는 번호면 빈 문자열.</returns>
        private static string MessageText(int id)
        {
            string text = MapLoader.GetMessageText(id);
            if (text.IndexOf('{') < 0) return text;

            foreach (string action in InputManager.Instance.GetActionNames())
            {
                text = text.Replace("{" + action + "}", KeyLabel(action), System.StringComparison.OrdinalIgnoreCase);
            }
            return text;
        }

        /// <summary>
        /// 액션에 지금 묶인 키의 이름을 화면에 쓸 표기로 돌려준다. 영문 대문자와 숫자뿐이라 스프라이트 글꼴로도 그릴 수 있다.
        /// </summary>
        /// <param name="action">액션 이름 (예: interact).</param>
        /// <returns>키 이름 (예: F, LMB). 없는 액션이거나 묶인 키가 없으면 물음표.</returns>
        private static string KeyLabel(string action)
        {
            string path = string.IsNullOrEmpty(action) ? string.Empty : InputManager.Instance.GetActionBinding(action);
            string key = path.Substring(path.LastIndexOf('/') + 1);
            if (key.Length == 0) return "?";
            return s_keyLabels.TryGetValue(key, out string label) ? label : key.ToUpperInvariant();
        }

        /// <summary>
        /// 디버그 콘솔에 한 줄을 남긴다.
        /// </summary>
        /// <param name="text">글자. 길면 자른다.</param>
        private static void Log(string text)
        {
            text ??= string.Empty;
            if (text.Length > k_maxLogChars) text = text.Substring(0, k_maxLogChars);
            Debugger.Log(text, "Event");
        }
    }
}
