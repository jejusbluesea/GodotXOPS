using System;
using System.Collections.Generic;
using System.IO;
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
        public const string ScriptExtension = ".sgd";

        // 호출 한 번의 실행 예산 (확장의 기본값과 같다). 넘으면 그 호출이 끊기고 실패로 친다.
        // 스크립트 안의 계산은 거의 들지 않고 API 호출이 예산을 쓴다: 이 값에서 단순한 호출은 약 1000번, 사전을 돌려주는 호출은 약 300번까지 된다 (script_probe 로 잰 값).
        // 끝나지 않는 루프는 이 예산을 다 쓸 때까지 게임을 멈춘다 (한 번이고, 그 뒤 그 줄은 멈춘다).
        private const int k_executionTimeout = 200;
        // 샌드박스 안의 메모리 한도 (MB). 배열과 문자열은 호스트 쪽 값이라 이 한도에 잡히지 않는다 (알려진 한계).
        private const int k_memoryMax = 16;
        // 스크립트 파일 크기의 상한 (바이트).
        private const long k_maxSourceBytes = 256 * 1024;

        /// <summary>
        /// 컴파일해 둔 스크립트. 파일이 바뀌지 않았으면 미션을 다시 시작할 때 다시 컴파일하지 않는다 (컴파일이 수십 ms 걸린다).
        /// </summary>
        private sealed class Compiled
        {
            public DateTime writeTime;
            public long size;
            public GodotObject script;
        }

        private static readonly Dictionary<string, Compiled> s_compiled = new Dictionary<string, Compiled>(StringComparer.OrdinalIgnoreCase);

        private readonly EventApi m_api;
        private Node m_node;
        private GodotObject m_sandbox;
        private GodotObject m_script;

        // 로그에 쓰는 이름 (스크립트의 exe 폴더 기준 경로).
        public string Label { get; private set; } = string.Empty;
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
        public static bool Available => ClassDB.ClassExists("SafeGDScript") && ClassDB.ClassExists("Sandbox");

        /// <summary>
        /// 스크립트 파일을 읽어 샌드박스에 올리고 init(api) 를 부른다.
        /// </summary>
        /// <param name="relativePath">스크립트 경로 (exe 폴더 기준, .sgd).</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>올렸으면 true.</returns>
        public bool Load(string relativePath, out string error)
        {
            Free();
            Label = relativePath ?? string.Empty;

            if (!Available)
            {
                error = "Godot Sandbox extension is not loaded";
                return false;
            }
            if (!string.Equals(Path.GetExtension(Label), ScriptExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = $"event script must be a {ScriptExtension} file: {Label}";
                return false;
            }
            string fullPath = GamePath.Resolve(Label);
            if (fullPath == null || !File.Exists(fullPath))
            {
                error = $"event script open failed: {Label}";
                return false;
            }

            if (!Compile(fullPath, out m_script, out error)) return false;

            // 제한은 스크립트를 붙인 직후, 무엇도 부르기 전에 건다. 걸리지 않았으면 쓰지 않는다.
            m_node = new Node { Name = "EventScript" };
            m_node.SetScript(Variant.From(m_script));
            m_node.Set("restrictions", true);
            m_node.Set("execution_timeout", k_executionTimeout);
            m_node.Set("memory_max", k_memoryMax);
            Variant sandbox = m_script.Call("get_sandbox_for", m_node);
            m_sandbox = sandbox.VariantType == Variant.Type.Object ? sandbox.AsGodotObject() : null;
            if (m_sandbox == null || !m_node.Get("restrictions").AsBool())
            {
                Free();
                error = $"event script could not be isolated: {Label}";
                return false;
            }

            if (m_node.HasMethod("init"))
            {
                int exceptions = Exceptions();
                m_node.Call("init", m_api.Table);
                if (Exceptions() != exceptions)
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
            return m_node != null && !string.IsNullOrEmpty(function) && m_node.HasMethod(function);
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
            if (m_node == null)
            {
                error = "script is not loaded";
                return false;
            }

            m_api.BeginCall();
            int exceptions = Exceptions();
            Variant value = m_node.Call(function, parameters, state);
            if (Exceptions() != exceptions)
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
            if (m_node != null && GodotObject.IsInstanceValid(m_node)) m_node.Free();
            m_node = null;
            m_sandbox = null;
            m_script = null;
        }

        /// <summary>
        /// 샌드박스가 지금까지 센 예외 횟수. 스크립트 안의 오류, 막힌 호출, 실행 예산 초과가 모두 여기에 잡힌다.
        /// </summary>
        /// <returns>예외 횟수.</returns>
        private int Exceptions()
        {
            return m_sandbox.Call("get_exceptions").AsInt32();
        }

        /// <summary>
        /// 스크립트 파일을 컴파일한다. 같은 파일을 전에 컴파일했고 그 뒤로 바뀌지 않았으면 그것을 돌려준다.
        /// </summary>
        /// <param name="fullPath">스크립트 전체 경로.</param>
        /// <param name="script">컴파일된 SafeGDScript.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>컴파일했으면 true.</returns>
        private bool Compile(string fullPath, out GodotObject script, out string error)
        {
            script = null;
            string source;
            DateTime writeTime;
            long size;
            try
            {
                var info = new FileInfo(fullPath);
                writeTime = info.LastWriteTimeUtc;
                size = info.Length;
                if (size > k_maxSourceBytes)
                {
                    error = $"event script is larger than {k_maxSourceBytes / 1024} KB: {Label}";
                    return false;
                }
                if (s_compiled.TryGetValue(fullPath, out Compiled cached) && cached.writeTime == writeTime && cached.size == size)
                {
                    script = cached.script;
                    error = null;
                    return true;
                }
                source = File.ReadAllText(fullPath);
            }
            catch (IOException e)
            {
                error = $"event script read failed: {Label} ({e.Message})";
                return false;
            }

            script = ClassDB.Instantiate("SafeGDScript").AsGodotObject();
            script.Call("set_source_code", source);
            string compileError = script.Call("get_compile_error").AsString();
            if (!string.IsNullOrEmpty(compileError))
            {
                script = null;
                int lineEnd = compileError.IndexOf('\n');
                error = $"event script compile error: {Label} ({(lineEnd < 0 ? compileError : compileError.Substring(0, lineEnd))})";
                return false;
            }

            s_compiled[fullPath] = new Compiled { writeTime = writeTime, size = size, script = script };
            error = null;
            return true;
        }
    }
}
