using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 스크립트 이벤트 한 종류의 처리기. 등록 정보대로 포인트의 칸에서 파라미터를 꺼내 묶음의 함수를 부르고, 돌려받은 출구 번호를 넘긴다.
    /// </summary>
    public sealed class ScriptEventHandler : IEventHandler
    {
        // 스크립트가 받는 사전에 게임이 채워 넣는 키. 파라미터 이름으로 쓸 수 없다.
        private static readonly string[] s_reservedNames = { "id", "x", "y", "z", "yaw" };

        private enum SlotSource
        {
            P2,
            P3,
            Extra,
        }

        private enum SlotKind
        {
            Int,
            Float,
            Bool,
        }

        /// <summary>
        /// 포인트의 칸 하나.
        /// </summary>
        private struct Slot
        {
            public string name;
            public SlotSource source;
            public int index;
            public SlotKind kind;
        }

        private readonly ScriptEventPack m_pack;
        private readonly int m_type;
        private readonly string m_function;
        private readonly Slot[] m_parameters;
        private readonly Slot[] m_exits;
        // 포인트마다 한 번 만들어 두는 파라미터 사전. 포인트의 값은 미션 중에 바뀌지 않는다.
        private readonly Dictionary<RawPointData, Godot.Collections.Dictionary> m_parameterCache = new Dictionary<RawPointData, Godot.Collections.Dictionary>();

        private ScriptEventHandler(ScriptEventPack pack, EventDefinitionData definition, Slot[] parameters, Slot[] exits)
        {
            m_pack = pack;
            m_type = definition.type;
            m_function = definition.function;
            m_parameters = parameters;
            m_exits = exits;
        }

        /// <summary>
        /// 등록 정보로 처리기를 만든다. 묶음은 이미 로드돼 있어야 한다.
        /// </summary>
        /// <param name="pack">이 이벤트의 함수가 들어 있는 묶음.</param>
        /// <param name="definition">등록 정보.</param>
        /// <param name="handler">만든 처리기.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>등록 정보가 맞고 함수가 있으면 true.</returns>
        public static bool Create(ScriptEventPack pack, EventDefinitionData definition, out ScriptEventHandler handler, out string error)
        {
            handler = null;
            if (!pack.HasFunction(definition.function))
            {
                error = $"event {definition.type}: function \"{definition.function}\" is not in {pack.Label}";
                return false;
            }

            var parameters = new Slot[definition.parameters.Count];
            for (int i = 0; i < parameters.Length; i++)
            {
                EventSlotData data = definition.parameters[i];
                if (string.IsNullOrEmpty(data.name) || Array.IndexOf(s_reservedNames, data.name) >= 0)
                {
                    error = $"event {definition.type}: parameter name \"{data.name}\" is empty or reserved";
                    return false;
                }
                if (!ParseSlot(data, out parameters[i]))
                {
                    error = $"event {definition.type}: parameter \"{data.name}\" has an unknown slot \"{data.slot}\"";
                    return false;
                }
            }

            // 출구를 적지 않았으면 원본 이벤트처럼 P3 하나다.
            Slot[] exits;
            if (definition.exits.Count == 0)
            {
                exits = new[] { new Slot { name = "next", source = SlotSource.P3 } };
            }
            else
            {
                exits = new Slot[definition.exits.Count];
                for (int i = 0; i < exits.Length; i++)
                {
                    if (!ParseSlot(definition.exits[i], out exits[i]) || exits[i].kind != SlotKind.Int)
                    {
                        error = $"event {definition.type}: exit {i} needs an integer slot, but has \"{definition.exits[i].slot}\"";
                        return false;
                    }
                }
            }

            handler = new ScriptEventHandler(pack, definition, parameters, exits);
            error = null;
            return true;
        }

        public int Tick(EventManager events, EventLine line, RawPointData point)
        {
            line.State ??= new Godot.Collections.Dictionary();
            if (!m_parameterCache.TryGetValue(point, out Godot.Collections.Dictionary parameters))
            {
                parameters = BuildParameters(point);
                m_parameterCache[point] = parameters;
            }

            if (!m_pack.Call(m_function, parameters, line.State, out int exit, out string error))
            {
                Debugger.LogError($"Event {m_type} \"{m_function}\" failed at point {point.param3}, line {line.Index} stopped: {error} ({m_pack.Label})", nameof(EventManager));
                return IEventHandler.Failed;
            }
            if (exit != IEventHandler.Wait && (exit < 0 || exit >= m_exits.Length))
            {
                Debugger.LogError($"Event {m_type} \"{m_function}\" returned exit {exit} at point {point.param3}, but it has {m_exits.Length}; line {line.Index} stopped ({m_pack.Label})", nameof(EventManager));
                return IEventHandler.Failed;
            }
            return exit;
        }

        public bool TryGetNext(RawPointData point, int exit, out int next)
        {
            next = 0;
            if (exit < 0 || exit >= m_exits.Length) return false;

            next = ReadInt(point, m_exits[exit]);
            return true;
        }

        /// <summary>
        /// 스크립트에 넘길 파라미터 사전을 만든다. 등록 정보의 이름으로 포인트의 칸 값을 넣고, 포인트의 식별번호·위치·방향을 더한다.
        /// 위치는 Godot 공간의 미터, 방향은 사람 기준 yaw(도)다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <returns>파라미터 사전.</returns>
        private Godot.Collections.Dictionary BuildParameters(RawPointData point)
        {
            var result = new Godot.Collections.Dictionary
            {
                ["id"] = point.param3,
                ["x"] = point.position.X,
                ["y"] = point.position.Y,
                ["z"] = point.position.Z,
                ["yaw"] = point.look,
            };
            foreach (Slot slot in m_parameters)
            {
                switch (slot.kind)
                {
                    case SlotKind.Float: result[slot.name] = point.GetExtraFloat(slot.index); break;
                    case SlotKind.Bool: result[slot.name] = point.GetExtraBool(slot.index); break;
                    default: result[slot.name] = ReadInt(point, slot); break;
                }
            }
            return result;
        }

        /// <summary>
        /// 칸의 값을 정수로 읽는다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <param name="slot">칸.</param>
        /// <returns>값. 없는 추가 파라미터 칸은 0.</returns>
        private static int ReadInt(RawPointData point, Slot slot)
        {
            switch (slot.source)
            {
                case SlotSource.P2: return point.param1;
                case SlotSource.P3: return point.param2;
                default: return point.GetExtraInt(slot.index);
            }
        }

        /// <summary>
        /// 등록 정보의 칸 표기를 읽는다. "p2", "p3", "e0" 부터의 추가 파라미터. 실수와 불은 추가 파라미터 칸에서만 된다.
        /// </summary>
        /// <param name="data">등록 정보의 칸.</param>
        /// <param name="slot">읽은 칸.</param>
        /// <returns>표기가 맞으면 true.</returns>
        private static bool ParseSlot(EventSlotData data, out Slot slot)
        {
            slot = new Slot { name = data.name ?? string.Empty };
            string text = (data.slot ?? string.Empty).Trim().ToLowerInvariant();
            switch (data.kind)
            {
                case "float": slot.kind = SlotKind.Float; break;
                case "bool": slot.kind = SlotKind.Bool; break;
                default: slot.kind = SlotKind.Int; break;
            }

            if (text == "p2" || text == "p3")
            {
                slot.source = text == "p2" ? SlotSource.P2 : SlotSource.P3;
                return slot.kind == SlotKind.Int;
            }
            if (text.Length > 1 && text[0] == 'e' && int.TryParse(text.AsSpan(1), out int index) && index >= 0)
            {
                slot.source = SlotSource.Extra;
                slot.index = index;
                return true;
            }
            return false;
        }
    }
}
