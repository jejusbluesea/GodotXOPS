using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 디버그 콘솔의 명령 표와 실행. 원본 OpenXOPS 의 디버그 콘솔(gamemain.cpp:3552-4750)에 해당한다.
    /// 화면(GDScript)은 한 줄을 GameBridge 에 넘기고 결과 글자를 받는다. 화면이 해야 하는 일(지우기, 닫기, 재시작, 화면 저장)은 TakeUiAction 으로 알린다.
    /// 사람은 MapLoader.Humans 의 인덱스로 가리킨다 (info 에 나오는 # 번호).
    /// 콘솔에 나오는 글자(사용법, 설명, 결과)는 모두 영어로 쓴다 (사용자 결정).
    /// 명령을 추가할 때는 생성자의 표에 한 줄을 더한다.
    /// </summary>
    public class DebugConsole
    {
        // 화면이 처리할 일의 이름.
        public const string UiActionClear = "clear";
        public const string UiActionExit = "exit";
        public const string UiActionRestart = "restart";
        public const string UiActionScreenshot = "screenshot";

        // help 가 한 줄에 늘어놓는 명령 수.
        private const int k_helpNamesPerLine = 8;

        /// <summary>
        /// 명령 하나. usage 는 인자까지 적은 사용법, help 는 한 줄 설명이다.
        /// </summary>
        private readonly struct Command
        {
            public readonly string name;
            public readonly string usage;
            public readonly string help;
            public readonly Func<string[], string> handler;

            public Command(string name, string usage, string help, Func<string[], string> handler)
            {
                this.name = name;
                this.usage = usage;
                this.help = help;
                this.handler = handler;
            }
        }

        private readonly List<Command> m_commands = new List<Command>();
        private string m_uiAction = string.Empty;
        private bool m_infoVisible;
        private bool m_noFight;
        private bool m_fogOff;
        // sky 명령으로 바꾼 하늘 번호. 바꾸지 않았으면 −1 (미션의 하늘 번호를 쓴다).
        private int m_skyIndex = -1;

        // info 로 켠 디버그 텍스트를 화면에 보일지.
        public bool InfoVisible => m_infoVisible;

        public DebugConsole()
        {
            Add("help", "help [command]", "List all commands. With a command name, show how to use it.", Help);
            Add("ver", "ver", "Show the game version.", _ => $"{DataManager.Instance.GlobalData.productName} {DataManager.Instance.GlobalData.Version}");
            Add("clear", "clear", "Clear the console text.", _ => RequestUi(UiActionClear, string.Empty));
            Add("exit", "exit", "Close the console.", _ => RequestUi(UiActionExit, string.Empty));

            Add("info", "info", "Toggle the debug text.", _ => Toggle(ref m_infoVisible, "Debug text"));
            Add("human", "human", "Show the number of humans and the survivors of each team.", _ => HumanSummary());
            Add("result", "result", "Show the mission statistics so far (shots, hits, headshots, kills, time).", _ => ResultSummary());
            Add("event", "event", "Show the point each event line is waiting on and the mission result.", _ => EventSummary());

            Add("nodamage", "nodamage [id]", "Toggle invincibility. Without an id, the player.", NoDamage);
            Add("treat", "treat [id]", "Restore HP to its initial value. Without an id, the player.", Treat);
            Add("teleport", "teleport <id> | <x> <y> <z>", "Move the player to that human, or to a position (the coordinates shown by info).", Teleport);
            Add("player", "player <id>", "Take control of that human.", SwitchPlayer);
            Add("weapon", "weapon <index> [ammo]", "Replace the current weapon of the player. Ammo is the total number of rounds; without it, the default for the human type.", GiveWeapon);
            Add("kill", "kill <id>", "Kill that human.", Kill);
            Add("flight", "flight", "Toggle flight mode for the player: move along the view direction, through blocks and humans, without gravity.", _ => ToggleFlight());

            Add("bot", "bot", "Toggle AI control of the player.", _ => ToggleBot());
            Add("nofight", "nofight", "Toggle no-fight mode for every AI.", _ => ToggleNoFight());
            Add("caution", "caution", "Put every AI on alert.", _ => Caution());
            Add("stop", "stop", "Toggle stopping every AI.", _ => ToggleAIStop());

            Add("comp", "comp", "End the mission as complete.", _ => ForceEnd(true));
            Add("fail", "fail", "End the mission as failed.", _ => ForceEnd(false));
            Add("estop", "estop", "Toggle stopping the mission events.", _ => ToggleEventStop());
            Add("f12", "f12", "Restart the mission.", _ => RequestUi(UiActionRestart, "Mission restarted"));

            Add("collider", "collider <human|weapon|object>", "Toggle drawing hit ranges: human hitboxes (green), weapon pickup ranges (red), object colliders (blue).", Collider);
            Add("fog", "fog", "Toggle the fog.", _ => ToggleFog());
            Add("sky", "sky <index>", "Change the sky (0 is none).", Sky);
            Add("ss", "ss", "Save a screenshot as PNG.", _ => RequestUi(UiActionScreenshot, string.Empty));
        }

        /// <summary>
        /// 명령 한 줄을 실행한다. 대소문자는 가리지 않고, 띄어쓰기로 명령과 인자를 나눈다.
        /// </summary>
        /// <param name="line">입력한 줄.</param>
        /// <returns>콘솔에 보여 줄 글자 (여러 줄일 수 있다). 보여 줄 것이 없으면 빈 문자열.</returns>
        public string Execute(string line)
        {
            string[] parts = (line ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return string.Empty;

            foreach (Command command in m_commands)
            {
                if (command.name != parts[0]) continue;

                var args = new string[parts.Length - 1];
                Array.Copy(parts, 1, args, 0, args.Length);
                return command.handler(args);
            }
            return $"Unknown command: {parts[0]} (type help for the list)";
        }

        /// <summary>
        /// 직전 명령이 화면에 맡긴 일을 꺼낸다. 꺼내면 비워진다.
        /// </summary>
        /// <returns>UiAction 상수 중 하나. 없으면 빈 문자열.</returns>
        public string TakeUiAction()
        {
            string action = m_uiAction;
            m_uiAction = string.Empty;
            return action;
        }

        /// <summary>
        /// 미션이 (다시) 시작될 때 부른다. 맵과 함께 사라지는 상태(비전투, 안개, 하늘)를 처음으로 되돌린다. 디버그 텍스트 표시는 유지한다.
        /// </summary>
        public void Reset()
        {
            m_noFight = false;
            m_fogOff = false;
            m_skyIndex = -1;
            m_uiAction = string.Empty;
        }

        /// <summary>
        /// 디버그 텍스트를 만든다. 미션, 플레이어 상태, 무기, AI 상태별 인원, 미션 결과를 여러 줄로 잇는다.
        /// </summary>
        /// <returns>표시할 문자열. 플레이어가 없으면 미션 이름만.</returns>
        public string BuildInfoText()
        {
            MapLoader loader = MapLoader.Instance;
            Human player = MapLoader.Player;
            var text = new StringBuilder();
            text.Append($"{loader.MissionFullname}  {Engine.GetFramesPerSecond():0} fps\n");
            if (player == null || !GodotObject.IsInstanceValid(player)) return text.ToString();

            HumanController controller = player.Controller;
            Vector3 position = controller.Position;
            Vector3 velocity = controller.MoveVelocity;
            float horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();
            text.Append($"Player #{MapLoader.PlayerIndex} {player.HumanData?.name}  team {player.Team}  HP {player.HP:0}{(player.Invincible ? " (invincible)" : string.Empty)}  {player.DeadState}\n");
            text.Append($"Position ({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})  yaw {controller.Yaw:0.0} pitch {controller.Pitch:0.0}\n");
            text.Append($"Speed horizontal {horizontalSpeed:0.00} vertical {velocity.Y:0.00} m/s  grounded {(controller.Grounded ? "yes" : "no")}\n");

            Weapon weapon = player.CurrentWeapon;
            text.Append($"Weapon [slot {player.SelectWeapon}] #{weapon.WeaponIndex} {weapon.Data.name}  {weapon.Magazine}/{weapon.Reserve}  aim error {player.CurrentErrorRange()}\n");

            CountHumans(out int alive, out int normal, out int caution, out int action);
            text.Append($"Humans {MapLoader.HumanCount} (alive {alive})  AI {(AIController.Enabled ? "on" : "stopped")}{(m_noFight ? " no-fight" : string.Empty)}  normal {normal} caution {caution} action {action}\n");
            text.Append($"Bullets {BulletManager.Instance.CountActive()}  dropped weapons {WeaponManager.Instance.CountActive()}  effects {EffectManager.Instance.CountActive()}  mission {ResultName()}");
            return text.ToString();
        }

        private void Add(string name, string usage, string help, Func<string[], string> handler)
        {
            m_commands.Add(new Command(name, usage, help, handler));
        }

        /// <summary>
        /// 화면이 처리할 일을 남긴다.
        /// </summary>
        /// <param name="action">UiAction 상수.</param>
        /// <param name="message">콘솔에 보여 줄 글자.</param>
        /// <returns>message 그대로.</returns>
        private string RequestUi(string action, string message)
        {
            m_uiAction = action;
            return message;
        }

        private static string Toggle(ref bool flag, string label)
        {
            flag = !flag;
            return $"{label} {(flag ? "on" : "off")}";
        }

        private string Help(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (Command command in m_commands)
                {
                    if (command.name == args[0]) return $"{command.usage} - {command.help}";
                }
                return $"Unknown command: {args[0]}";
            }

            var text = new StringBuilder("Commands (type help <command> for details)");
            for (int i = 0; i < m_commands.Count; i++)
            {
                text.Append(i % k_helpNamesPerLine == 0 ? "\n  " : "  ");
                text.Append(m_commands[i].name);
            }
            return text.ToString();
        }

        /// <summary>
        /// 인자에서 사람을 찾는다. 인자가 없으면 플레이어를 쓴다 (allowPlayer 가 true 일 때).
        /// </summary>
        /// <param name="args">명령의 인자.</param>
        /// <param name="allowPlayer">인자가 없을 때 플레이어를 돌려줄지.</param>
        /// <param name="human">찾은 사람.</param>
        /// <param name="index">그 사람의 목록 인덱스.</param>
        /// <param name="error">찾지 못했을 때 보여 줄 글자.</param>
        /// <returns>찾았으면 true.</returns>
        private static bool FindHuman(string[] args, bool allowPlayer, out Human human, out int index, out string error)
        {
            human = null;
            index = -1;
            error = string.Empty;

            if (args.Length == 0)
            {
                if (!allowPlayer)
                {
                    error = "A human id is required";
                    return false;
                }
                index = MapLoader.PlayerIndex;
            }
            else if (!int.TryParse(args[0], out index))
            {
                error = $"Not a number: {args[0]}";
                return false;
            }

            human = MapLoader.GetHuman(index);
            if (human == null || !GodotObject.IsInstanceValid(human))
            {
                human = null;
                error = $"No such human: {index} (0 to {MapLoader.HumanCount - 1})";
                return false;
            }
            return true;
        }

        private static void CountHumans(out int alive, out int normal, out int caution, out int action)
        {
            alive = normal = caution = action = 0;
            Human player = MapLoader.Player;
            foreach (Human human in MapLoader.Humans)
            {
                if (!human.Alive) continue;

                alive++;
                if (human == player) continue;
                switch (human.Brain.Mode)
                {
                    case AIBattleMode.Action: action++; break;
                    case AIBattleMode.Caution: caution++; break;
                    default: normal++; break;
                }
            }
        }

        private static string ResultName()
        {
            int result = EventManager.Instance.Result;
            return result == (int)MissionResult.Complete ? "complete" : result == (int)MissionResult.Failed ? "failed" : "in progress";
        }

        private static string HumanSummary()
        {
            var aliveByTeam = new SortedDictionary<int, int>();
            int alive = 0;
            foreach (Human human in MapLoader.Humans)
            {
                if (!human.Alive) continue;

                alive++;
                aliveByTeam[human.Team] = aliveByTeam.TryGetValue(human.Team, out int count) ? count + 1 : 1;
            }

            var text = new StringBuilder($"Humans {MapLoader.HumanCount}, alive {alive}, player #{MapLoader.PlayerIndex}");
            foreach (KeyValuePair<int, int> team in aliveByTeam)
            {
                text.Append($"\n  team {team.Key}: alive {team.Value}");
            }
            return text.ToString();
        }

        private static string ResultSummary()
        {
            MissionStats stats = MapLoader.Stats;
            return $"Shots {stats.Fire}  hits {stats.OnTargetInt} ({stats.AccuracyPercent:0.0}%)  headshots {stats.Headshot}  kills {stats.Kill}  time {stats.PlayTime:0.0}s";
        }

        private static string EventSummary()
        {
            EventManager events = EventManager.Instance;
            var text = new StringBuilder($"Mission {ResultName()}, events {(events.Running ? (events.LinesPaused ? "stopped" : "running") : "not running")}");
            for (int line = 0; line < events.LineCount; line++)
            {
                int cursor = events.LineCursor(line);
                RawPointData point = MapLoader.GetEventPoint(cursor);
                text.Append($"\n  line {line}: point {cursor} ({(point != null ? ((EventType)point.param0).ToString() : "none")})");
            }
            return text.ToString();
        }

        private static string NoDamage(string[] args)
        {
            if (!FindHuman(args, true, out Human human, out int index, out string error)) return error;

            human.SetInvincible(!human.Invincible);
            return $"#{index} invincibility {(human.Invincible ? "on" : "off")}";
        }

        private static string Treat(string[] args)
        {
            if (!FindHuman(args, true, out Human human, out int index, out string error)) return error;

            return human.RestoreHP() ? $"#{index} HP {human.HP:0}" : $"#{index} is dead";
        }

        private static string Teleport(string[] args)
        {
            Human player = MapLoader.Player;
            if (player == null) return "There is no player";

            // 인자가 셋이면 좌표다. info 가 보여 주는 것과 같은 좌표(Godot 공간, 미터, 발 기준)로 받는다.
            if (args.Length == 3)
            {
                if (!TryParseFloat(args[0], out float x) || !TryParseFloat(args[1], out float y) || !TryParseFloat(args[2], out float z))
                {
                    return "Three numbers are required: teleport <x> <y> <z>";
                }

                player.Controller.Teleport(new Vector3(x, y, z));
                return string.Format(CultureInfo.InvariantCulture, "Moved to ({0:0.00}, {1:0.00}, {2:0.00})", x, y, z);
            }
            if (args.Length != 1) return "Usage: teleport <id> | teleport <x> <y> <z>";

            if (!FindHuman(args, false, out Human human, out int index, out string error)) return error;

            player.Controller.Teleport(human.Controller.Position);
            return $"Moved to #{index}";
        }

        /// <summary>
        /// 소수점이 마침표인 수를 읽는다. OS 언어와 무관하게 같은 표기를 받는다.
        /// </summary>
        /// <param name="text">읽을 글자.</param>
        /// <param name="value">읽은 값.</param>
        /// <returns>유한한 수로 읽었으면 true.</returns>
        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
        }

        private static string SwitchPlayer(string[] args)
        {
            if (!FindHuman(args, false, out _, out int index, out string error)) return error;

            PlayerController controller = PlayerController.Current;
            if (controller == null) return "Not in a playable screen";

            return controller.SwitchPlayer(index) ? $"Now controlling #{index}" : $"Already controlling #{index}";
        }

        private static string GiveWeapon(string[] args)
        {
            Human player = MapLoader.Player;
            if (player == null || !player.Alive) return "There is no living player";

            int count = DataManager.Instance.WeaponParameterData.weaponData.Count;
            if (args.Length == 0 || !int.TryParse(args[0], out int weaponIndex) || weaponIndex < 0 || weaponIndex >= count)
            {
                return $"A weapon index is required (0 to {count - 1})";
            }

            // 탄 수를 주면 장탄수만큼 장전하고 나머지를 예비 탄으로 둔다. 주지 않으면 사람 종류의 초기 탄약 배수를 쓴다 (SetWeapon 의 기본).
            int magazine = -1;
            int reserve = -1;
            if (args.Length > 1)
            {
                if (!int.TryParse(args[1], out int ammo) || ammo < 0) return "Ammo must be a number of rounds (0 or more)";

                magazine = Mathf.Min(ammo, DataManager.Instance.WeaponParameterData.weaponData[weaponIndex].magazineSize);
                reserve = ammo - magazine;
            }

            player.SetWeapon(player.SelectWeapon, weaponIndex, magazine, reserve);
            if (player.CurrentWeapon.IsNone) player.DisableScope();
            Weapon weapon = player.CurrentWeapon;
            return $"Weapon #{weaponIndex} {weapon.Data.name}  {weapon.Magazine}/{weapon.Reserve}";
        }

        private static string Kill(string[] args)
        {
            if (!FindHuman(args, false, out Human human, out int index, out string error)) return error;
            if (!human.Alive) return $"#{index} is already dead";
            if (human.Invincible) return $"#{index} is invincible";

            human.ApplyDamage(human.HP);
            return $"#{index} killed";
        }

        private static string ToggleFlight()
        {
            Human player = MapLoader.Player;
            if (player == null || !player.Alive) return "There is no living player";

            HumanController controller = player.Controller;
            controller.SetFlight(!controller.Flight);
            return $"Flight {(controller.Flight ? "on" : "off")}";
        }

        private static string Collider(string[] args)
        {
            ColliderView view = GameBridge.Instance.ColliderView;
            switch (args.Length > 0 ? args[0] : string.Empty)
            {
                case "human": view.ShowHuman = !view.ShowHuman; break;
                case "weapon": view.ShowWeapon = !view.ShowWeapon; break;
                case "object": view.ShowObject = !view.ShowObject; break;
                case "": break;
                default: return "Usage: collider <human|weapon|object>";
            }
            return $"Collider human {(view.ShowHuman ? "on" : "off")}, weapon {(view.ShowWeapon ? "on" : "off")}, object {(view.ShowObject ? "on" : "off")}";
        }

        private static string ToggleBot()
        {
            AIController.DrivePlayer = !AIController.DrivePlayer;
            return $"Player AI {(AIController.DrivePlayer ? "on" : "off")}";
        }

        private string ToggleNoFight()
        {
            m_noFight = !m_noFight;
            AIController.SetNoFightAll(m_noFight);
            return $"No-fight {(m_noFight ? "on" : "off")}";
        }

        private static string Caution()
        {
            AIController.SetCautionAll();
            return "All AI on alert";
        }

        private static string ToggleAIStop()
        {
            AIController.Enabled = !AIController.Enabled;
            return $"AI {(AIController.Enabled ? "resumed" : "stopped")}";
        }

        private static string ForceEnd(bool complete)
        {
            return EventManager.Instance.ForceEnd(complete) ? (complete ? "Mission complete" : "Mission failed") : "No mission in progress";
        }

        private static string ToggleEventStop()
        {
            EventManager events = EventManager.Instance;
            events.LinesPaused = !events.LinesPaused;
            return $"Events {(events.LinesPaused ? "stopped" : "resumed")}";
        }

        private string ToggleFog()
        {
            m_fogOff = !m_fogOff;
            if (m_fogOff) MapLoader.ClearFog();
            else MapLoader.ApplySkyFog(m_skyIndex >= 0 ? m_skyIndex : MapLoader.Instance.SkyIndex);
            return $"Fog {(m_fogOff ? "off" : "on")}";
        }

        private string Sky(string[] args)
        {
            int count = DataManager.Instance.SkyData.skyTexturePath.Count;
            if (args.Length == 0 || !int.TryParse(args[0], out int skyIndex) || skyIndex < 0 || skyIndex >= count)
            {
                return $"A sky index is required (0 to {count - 1}, sky_data.json has {count} entries)";
            }

            m_skyIndex = skyIndex;
            MapLoader.LoadSkyData(skyIndex);
            // 하늘을 로드하면 안개가 그 번호의 색으로 다시 켜진다. 꺼 둔 상태면 다시 끈다.
            if (m_fogOff) MapLoader.ClearFog();
            return $"Sky {skyIndex}";
        }
    }
}
