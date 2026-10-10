using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// SafeGDScript(.sgd) 파일 하나를 Godot Sandbox 안에 올린 것. 스크립트 이벤트 묶음과 화면 스크립트가 함께 쓴다.
    /// 외부에서 받은 스크립트는 반드시 여기로만 올린다. 일반 GDScript 로 컴파일하지 않고, 격리를 걸 수 없으면 로드를 거절한다.
    /// 노드를 트리에 넣지 않으므로 스크립트의 _ready / _process 는 돌지 않고, 게임이 부를 때만 실행된다.
    /// </summary>
    public sealed class SandboxScript
    {
        public const string ScriptExtension = ".sgd";

        // 호출 한 번의 실행 예산 (확장의 기본값과 같다). 넘으면 그 호출이 끊기고 실패로 친다.
        // 스크립트 안의 계산은 거의 들지 않고 API 호출이 예산을 쓴다: 이 값에서 단순한 호출은 약 1000번, 사전을 돌려주는 호출은 약 300번까지 된다 (script_probe 로 잰 값).
        // 끝나지 않는 루프는 이 예산을 다 쓸 때까지 게임을 멈춘다.
        private const int k_executionTimeout = 200;
        // 샌드박스 안의 메모리 한도 (MB). 배열과 문자열은 호스트 쪽 값이라 이 한도에 잡히지 않는다 (알려진 한계).
        private const int k_memoryMax = 16;
        // 스크립트 파일 크기의 상한 (바이트).
        private const long k_maxSourceBytes = 256 * 1024;

        /// <summary>
        /// 컴파일해 둔 스크립트. 파일이 바뀌지 않았으면 다시 컴파일하지 않는다 (컴파일이 수십 ms 걸린다).
        /// </summary>
        private sealed class Compiled
        {
            public DateTime writeTime;
            public long size;
            public GodotObject script;
        }

        private static readonly Dictionary<string, Compiled> s_compiled = new Dictionary<string, Compiled>(StringComparer.OrdinalIgnoreCase);

        private GodotObject m_sandbox;
        private GodotObject m_script;

        // 스크립트가 붙은 노드 (트리 밖). 올리지 않았으면 null.
        public Node Node { get; private set; }
        // 로그에 쓰는 이름 (스크립트의 exe 폴더 기준 경로).
        public string Label { get; private set; } = string.Empty;

        // Godot Sandbox 확장이 로드돼 있는지.
        public static bool Available => ClassDB.ClassExists("SafeGDScript") && ClassDB.ClassExists("Sandbox");

        /// <summary>
        /// 스크립트 파일을 읽어 샌드박스에 올린다. 스크립트의 함수는 부르지 않는다.
        /// </summary>
        /// <param name="relativePath">스크립트 경로 (exe 폴더 기준, .sgd).</param>
        /// <param name="kind">오류 문구에 쓰는 스크립트의 쓰임새 (영어 한 단어. 예: "event").</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <param name="maxReferences">호출 한 번이 만들 수 있는 값(사전, 배열, 글자 등 호스트 쪽 값)의 수. 0 이면 확장의 기본값(100)을 둔다.</param>
        /// <returns>올렸으면 true.</returns>
        public bool Load(string relativePath, string kind, out string error, int maxReferences = 0)
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
                error = $"{kind} script must be a {ScriptExtension} file: {Label}";
                return false;
            }
            string fullPath = GamePath.Resolve(Label);
            if (fullPath == null || !File.Exists(fullPath))
            {
                error = $"{kind} script open failed: {Label}";
                return false;
            }

            if (!Compile(fullPath, kind, out m_script, out error)) return false;

            // 제한은 스크립트를 붙인 직후, 무엇도 부르기 전에 건다. 걸리지 않았으면 쓰지 않는다.
            Node = new Node { Name = "SandboxScript" };
            Node.SetScript(Variant.From(m_script));
            Node.Set("restrictions", true);
            Node.Set("execution_timeout", k_executionTimeout);
            Node.Set("memory_max", k_memoryMax);
            if (maxReferences > 0) Node.Set("references_max", maxReferences);
            Variant sandbox = m_script.Call("get_sandbox_for", Node);
            m_sandbox = sandbox.VariantType == Variant.Type.Object ? sandbox.AsGodotObject() : null;
            if (m_sandbox == null || !Node.Get("restrictions").AsBool())
            {
                Free();
                error = $"{kind} script could not be isolated: {Label}";
                return false;
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
            return Node != null && !string.IsNullOrEmpty(function) && Node.HasMethod(function);
        }

        /// <summary>
        /// 샌드박스가 지금까지 센 예외 횟수. 스크립트 안의 오류, 막힌 호출, 실행 예산 초과가 모두 여기에 잡힌다.
        /// 호출 앞뒤의 값을 견줘 그 호출이 실패했는지 안다.
        /// </summary>
        /// <returns>예외 횟수. 올리지 않았으면 0.</returns>
        public int Exceptions()
        {
            return m_sandbox != null ? m_sandbox.Call("get_exceptions").AsInt32() : 0;
        }

        /// <summary>
        /// 샌드박스를 내린다.
        /// </summary>
        public void Free()
        {
            if (Node != null && GodotObject.IsInstanceValid(Node)) Node.Free();
            Node = null;
            m_sandbox = null;
            m_script = null;
        }

        /// <summary>
        /// 스크립트 파일을 컴파일한다. 같은 파일을 전에 컴파일했고 그 뒤로 바뀌지 않았으면 그것을 돌려준다.
        /// </summary>
        /// <param name="fullPath">스크립트 전체 경로.</param>
        /// <param name="kind">오류 문구에 쓰는 스크립트의 쓰임새.</param>
        /// <param name="script">컴파일된 SafeGDScript.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>컴파일했으면 true.</returns>
        private bool Compile(string fullPath, string kind, out GodotObject script, out string error)
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
                    error = $"{kind} script is larger than {k_maxSourceBytes / 1024} KB: {Label}";
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
                error = $"{kind} script read failed: {Label} ({e.Message})";
                return false;
            }

            script = ClassDB.Instantiate("SafeGDScript").AsGodotObject();
            script.Call("set_source_code", source);
            string compileError = script.Call("get_compile_error").AsString();
            if (!string.IsNullOrEmpty(compileError))
            {
                script = null;
                int lineEnd = compileError.IndexOf('\n');
                error = $"{kind} script compile error: {Label} ({(lineEnd < 0 ? compileError : compileError.Substring(0, lineEnd))})";
                return false;
            }

            s_compiled[fullPath] = new Compiled { writeTime = writeTime, size = size, script = script };
            error = null;
            return true;
        }
    }
}
