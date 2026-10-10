using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 스크립트 이벤트 묶음 하나의 실행기. SafeGDScript(.sgd) 파일 하나를 Godot Sandbox 안에서 돌린다.
    /// 외부에서 받은 스크립트는 반드시 여기로만 돌린다. 일반 GDScript 로 컴파일하지 않고, 격리를 걸 수 없으면 로드를 거절한다.
    /// 노드를 트리에 넣지 않으므로 스크립트의 _ready / _process 는 돌지 않고, 이벤트 틱에서 부를 때만 실행된다.
    /// </summary>
    public sealed class ScriptEventPack
    {
        public const string ScriptExtension = SandboxScript.ScriptExtension;

        private readonly EventApi m_api;
        private readonly SandboxScript m_sandbox = new SandboxScript();

        // 로그에 쓰는 이름 (스크립트의 exe 폴더 기준 경로).
        public string Label => m_sandbox.Label;
        // 이 묶음의 스크립트가 쓰는 API.
        public EventApi Api => m_api;

        /// <summary>
        /// 묶음을 만든다. Load 를 불러야 쓸 수 있다.
        /// </summary>
        /// <param name="events">API 가 조작할 이벤트 매니저.</param>
        public ScriptEventPack(EventManager events)
        {
            m_api = new EventApi(events);
        }

        // Godot Sandbox 확장이 로드돼 있는지.
        public static bool Available => SandboxScript.Available;

        /// <summary>
        /// 스크립트 파일을 읽어 샌드박스에 올리고 init(api) 를 부른다.
        /// </summary>
        /// <param name="relativePath">스크립트 경로 (exe 폴더 기준, .sgd).</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>올렸으면 true.</returns>
        public bool Load(string relativePath, out string error)
        {
            if (!m_sandbox.Load(relativePath, "event", out error)) return false;

            if (m_sandbox.HasFunction("init"))
            {
                int exceptions = m_sandbox.Exceptions();
                m_sandbox.Node.Call("init", m_api.Table);
                if (m_sandbox.Exceptions() != exceptions)
                {
                    Free();
                    error = $"event script failed in init(): {Label}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 스크립트에 그 이름의 함수가 있는지 본다.
        /// </summary>
        /// <param name="function">함수 이름.</param>
        /// <returns>있으면 true.</returns>
        public bool HasFunction(string function)
        {
            return m_sandbox.HasFunction(function);
        }

        /// <summary>
        /// 이벤트 함수를 부른다.
        /// </summary>
        /// <param name="function">함수 이름.</param>
        /// <param name="parameters">이름 붙은 파라미터 사전.</param>
        /// <param name="state">줄의 저장 칸.</param>
        /// <param name="result">함수가 돌려준 정수.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>함수가 끝까지 돌고 정수를 돌려줬으면 true. 스크립트 안의 오류, 실행 예산 초과, API 호출 한도 초과, 정수가 아닌 반환값은 false.</returns>
        public bool Call(string function, Godot.Collections.Dictionary parameters, Godot.Collections.Dictionary state, out int result, out string error)
        {
            result = 0;
            if (m_sandbox.Node == null)
            {
                error = "script is not loaded";
                return false;
            }

            m_api.BeginCall();
            int exceptions = m_sandbox.Exceptions();
            Variant value = m_sandbox.Node.Call(function, parameters, state);
            if (m_sandbox.Exceptions() != exceptions)
            {
                error = "script error or execution limit";
                return false;
            }
            if (m_api.OverBudget)
            {
                error = "too many API calls in one event";
                return false;
            }
            if (value.VariantType != Variant.Type.Int)
            {
                error = $"returned {value.VariantType} instead of an exit number";
                return false;
            }

            result = value.AsInt32();
            error = null;
            return true;
        }

        /// <summary>
        /// 샌드박스를 내린다.
        /// </summary>
        public void Free()
        {
            m_sandbox.Free();
        }
    }
}
