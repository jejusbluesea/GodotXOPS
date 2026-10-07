using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 시제품 씬 스크립트. Godot Sandbox 의 SafeGDScript(.sgd)를 C# 에서 로드·호출할 수 있는지, 격리와 자원 제한이 실제로 듣는지 확인한다 (스크립트로 만드는 이벤트의 전제).
    /// 게임 폴더의 외부 .sgd 파일을 텍스트로 읽어 컴파일하고, 빠져나가려는 스크립트들이 전부 막히는지 본다. 익스포트 빌드에서도 돌려 본다.
    /// 파일은 build/script_probe/ 에 쓰고 끝나면 지운다. 문제가 있으면 종료 코드 1.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/script_probe.tscn
    /// 익스포트 빌드: GodotXOPS.exe --headless -- --scene dev/script_probe
    /// </summary>
    public partial class ScriptProbe : Node
    {
        private const string k_workFolder = "build/script_probe";
        private const string k_reached = "REACHED";
        private const string k_secret = "probe-secret-content";
        // 한 호출의 실행 예산 (백만 명령 단위). 무한 루프가 이만큼 돌고 끊긴다.
        private const int k_executionTimeout = 20;
        private const int k_memoryMax = 16;
        private const int k_waitFrames = 5;
        private const int k_benchCalls = 2000;

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();
        private readonly List<string> m_log = new List<string>();
        private string m_folder;
        private Node m_processNode;
        private int m_frames;

        public override void _Ready()
        {
            m_folder = GamePath.Resolve(k_workFolder);
            Directory.CreateDirectory(m_folder);
            GD.Print($"에디터 빌드: {OS.HasFeature("editor")}, 게임 폴더: {GamePath.Root}");

            if (!ClassDB.ClassExists("SafeGDScript") || !ClassDB.ClassExists("Sandbox"))
            {
                Expect(false, "Godot Sandbox 확장이 로드되지 않음 (SafeGDScript / Sandbox 클래스 없음)");
                Finish();
                return;
            }

            CheckBasics();
            CheckHostile();
            CheckLimits();

            // 샌드박스 안의 _process 가 도는지는 몇 프레임 뒤에 본다.
            m_processNode = Load("process.sgd", @"var frames : int = 0

func _process(_delta):
	frames += 1

func get_frames() -> int:
	return frames
", out _, out _);
        }

        public override void _Process(double delta)
        {
            if (m_processNode == null) return;
            if (++m_frames < k_waitFrames) return;

            int frames = m_processNode.Call("get_frames").AsInt32();
            GD.Print($"샌드박스 안의 _process 가 {k_waitFrames} 프레임 동안 돈 횟수: {frames}");
            m_processNode.SetProcess(false);
            int before = m_processNode.Call("get_frames").AsInt32();
            m_processNode = null;
            Finish();
        }

        private void Finish()
        {
            SetProcess(false);
            try
            {
                Directory.Delete(m_folder, true);
            }
            catch (IOException)
            {
            }

            GD.Print($"샌드박스 시제품 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems) GD.Print($"문제: {problem}");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }

        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// 소스를 게임 폴더의 .sgd 파일로 쓰고, 그 파일을 텍스트로 읽어 샌드박스 스크립트로 컴파일해 제한을 건 노드에 붙인다.
        /// 제한은 노드를 트리에 넣기 전에 건다.
        /// </summary>
        /// <param name="fileName">파일 이름.</param>
        /// <param name="source">SafeGDScript 소스.</param>
        /// <param name="sandbox">그 노드의 샌드박스 (예외·타임아웃 횟수를 읽는 용도). 얻지 못하면 null.</param>
        /// <param name="compileError">컴파일 오류 문구. 없으면 빈 문자열.</param>
        /// <param name="restricted">false 면 제한을 걸지 않는다 (점검이 뚫림을 잡아내는지 보는 대조군 전용).</param>
        /// <returns>스크립트가 붙은 노드. 컴파일하지 못했으면 null.</returns>
        private Node Load(string fileName, string source, out GodotObject sandbox, out string compileError, bool restricted = true)
        {
            sandbox = null;
            string path = Path.Combine(m_folder, fileName);
            File.WriteAllText(path, source);

            GodotObject script = ClassDB.Instantiate("SafeGDScript").AsGodotObject();
            script.Call("set_source_code", File.ReadAllText(path));
            compileError = script.Call("get_compile_error").AsString();
            if (!string.IsNullOrEmpty(compileError)) return null;

            var node = new Node { Name = Path.GetFileNameWithoutExtension(fileName) };
            node.SetScript(script);
            node.Set("restrictions", restricted);
            node.Set("execution_timeout", k_executionTimeout);
            node.Set("memory_max", k_memoryMax);
            AddChild(node);

            Variant found = script.Call("get_sandbox_for", node);
            sandbox = found.VariantType == Variant.Type.Object ? found.AsGodotObject() : null;
            return node;
        }

        private static int Counter(GodotObject sandbox, string method)
        {
            return sandbox != null ? sandbox.Call(method).AsInt32() : -1;
        }

        /// <summary>
        /// 이벤트에 필요한 기본 동작: 함수 호출과 반환값, 호출 사이에 남는 변수, 넘긴 사전, 게임이 준 함수(API) 호출, 호출 한 번의 비용.
        /// </summary>
        private void CheckBasics()
        {
            Node node = Load("basic.sgd", @"var api : Dictionary = {}
var calls : int = 0

func event_init(granted : Dictionary) -> void:
	api = granted

func execute(a : int, b : int, c : int, state : Dictionary) -> int:
	calls += 1
	state[""calls""] = calls
	return a + b * 10 + c * 100

func get_calls() -> int:
	return calls

func use_api(x : int, y : int) -> int:
	api[""log""].call(""hello from sandbox"")
	return api[""add""].call(x, y)

func read_state(state : Dictionary) -> int:
	return state[""value""]

func float_work() -> float:
	var total : float = 0.0
	for i in range(1000):
		total += sin(float(i) * 0.37) * 1.0001
	return total
", out GodotObject sandbox, out string error);
            if (node == null)
            {
                Expect(false, $"기본 스크립트 컴파일 실패: {error}");
                return;
            }
            Expect(sandbox != null, "노드의 샌드박스를 얻지 못함 (get_sandbox_for)");
            Expect(node.HasMethod("execute") && !node.HasMethod("no_such_function"), "has_method 로 함수의 유무를 알 수 없음");

            var state = new Godot.Collections.Dictionary();
            int first = node.Call("execute", 1, 2, 3, state).AsInt32();
            node.Call("execute", 1, 2, 3, state);
            Expect(first == 321, $"execute 의 결과가 다름 ({first})");
            Expect(node.Call("get_calls").AsInt32() == 2, "스크립트의 변수가 호출 사이에 남지 않음");
            bool stateWritten = state.ContainsKey("calls") && state["calls"].AsInt32() == 2;
            GD.Print($"스크립트가 넘겨받은 사전에 쓴 값이 호스트에 보이는가: {stateWritten}");

            state["value"] = 77;
            Expect(node.Call("read_state", state).AsInt32() == 77, "넘긴 사전의 값을 스크립트가 읽지 못함");

            var api = new Godot.Collections.Dictionary
            {
                ["log"] = Callable.From((string message) => m_log.Add(message)),
                ["add"] = Callable.From((int x, int y) => x + y),
            };
            node.Call("event_init", api);
            int sum = node.Call("use_api", 20, 22).AsInt32();
            Expect(sum == 42, $"스크립트가 C# 함수(API)를 불러 받은 값이 다름 ({sum})");
            Expect(m_log.Count == 1 && m_log[0] == "hello from sandbox", "스크립트가 부른 C# 함수가 호출되지 않음");

            // 같은 계산을 새 샌드박스에서 다시 돌려 비트까지 같은지 본다 (같은 컴퓨터 안에서만 확인된다).
            double a = node.Call("float_work").AsDouble();
            double b = node.Call("float_work").AsDouble();
            Expect(BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b), "같은 실수 계산의 결과가 호출마다 다름");

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < k_benchCalls; i++) node.Call("execute", 1, 2, 3, state);
            GD.Print($"execute 호출 1회: {watch.Elapsed.TotalMilliseconds * 1000.0 / k_benchCalls:0.0} us (컴파일 누적 시간 {Counter(sandbox, "get_accumulated_startup_time")})");
            Expect(Counter(sandbox, "get_exceptions") == 0 && Counter(sandbox, "get_timeouts") == 0, "정상 호출에서 예외나 타임아웃이 셈");
        }

        /// <summary>
        /// 빠져나가려는 스크립트들. 하나씩 따로 컴파일해 부르고, 컴파일이 거절됐는지 / 호출이 막혔는지 / 닿았는지를 적는다.
        /// 파일과 프로그램 실행은 점검 폴더 안의 표시 파일로만 확인한다.
        /// </summary>
        private void CheckHostile()
        {
            string secretPath = Path.Combine(m_folder, "secret.txt").Replace('\\', '/');
            string pwnedPath = Path.Combine(m_folder, "pwned.txt").Replace('\\', '/');
            File.WriteAllText(secretPath, k_secret);

            var attempts = new (string name, string body)[]
            {
                ("파일 쓰기", "var f = FileAccess.open(path, FileAccess.WRITE)\n\tf.store_string(\"x\")\n\tf.close()\n\treturn \"REACHED\""),
                ("파일 읽기", "return \"REACHED\" + FileAccess.get_file_as_string(secret)"),
                ("폴더 목록", "return \"REACHED\" + str(DirAccess.get_files_at(secret.get_base_dir()))"),
                ("프로그램 실행", "OS.execute(\"cmd.exe\", [\"/c\", \"echo x > \" + path])\n\treturn \"REACHED\""),
                ("프로세스 만들기", "OS.create_process(\"cmd.exe\", [\"/c\", \"echo x > \" + path])\n\treturn \"REACHED\""),
                ("환경 변수", "return \"REACHED\" + OS.get_environment(\"USERNAME\")"),
                ("클립보드", "return \"REACHED\" + DisplayServer.clipboard_get()"),
                ("씬 트리", "return \"REACHED\" + str(get_tree().root.get_child_count())"),
                ("루트 노드", "return \"REACHED\" + str(get_node(\"/root\").get_child_count())"),
                ("부모 노드", "return \"REACHED\" + str(get_parent().name)"),
                ("메인 루프", "return \"REACHED\" + str(Engine.get_main_loop().root.get_child_count())"),
                ("Autoload 이름", "return \"REACHED\" + str(Game.LastLoadError())"),
                ("Autoload 노드", "return \"REACHED\" + str(get_node(\"/root/Game\").call(\"LastLoadError\"))"),
                ("스크립트 로드", "var s = load(\"res://ui/boot.gd\")\n\treturn \"REACHED\" + s.resource_path"),
                ("클래스 생성", "var n = ClassDB.instantiate(\"HTTPRequest\")\n\treturn \"REACHED\" + n.get_class()"),
                ("HTTPRequest.new", "var n = HTTPRequest.new()\n\treturn \"REACHED\" + n.get_class()"),
                ("일반 GDScript 컴파일", "var g = GDScript.new()\n\tg.source_code = \"extends RefCounted\\nfunc f():\\n\\treturn 1\\n\"\n\tg.reload()\n\treturn \"REACHED\" + str(g.new().f())"),
                ("Expression", "var e = Expression.new()\n\te.parse(\"OS.get_environment(\\\"USERNAME\\\")\")\n\treturn \"REACHED\" + str(e.execute())"),
                ("Callable 뒤의 객체", "return \"REACHED\" + api[\"log\"].get_object().get_class()"),
                ("자기 노드 이름 바꾸기", "set_name(\"pwned\")\n\treturn \"REACHED\""),
                ("게임 종료", "get_tree().quit(3)\n\treturn \"REACHED\""),
            };

            var api = new Godot.Collections.Dictionary { ["log"] = Callable.From((string message) => m_log.Add(message)) };
            foreach ((string name, string body) in attempts)
            {
                string source = "var api : Dictionary = {}\n\nfunc event_init(granted : Dictionary) -> void:\n\tapi = granted\n\nfunc attempt(path : String, secret : String) -> String:\n\t" + body + "\n";
                Node node = Load($"hostile_{m_checks}.sgd", source, out GodotObject sandbox, out string error);
                string outcome;
                bool reached = false;
                if (node == null)
                {
                    outcome = $"컴파일 거절 ({FirstLine(error)})";
                }
                else
                {
                    node.Call("event_init", api);
                    int exceptions = Counter(sandbox, "get_exceptions");
                    Variant result = node.Call("attempt", pwnedPath, secretPath);
                    string text = result.VariantType == Variant.Type.String ? result.AsString() : string.Empty;
                    reached = text.StartsWith(k_reached, StringComparison.Ordinal);
                    outcome = reached ? $"닿음: {text}" : $"호출 막힘 (반환 {result.VariantType}, 예외 {Counter(sandbox, "get_exceptions") - exceptions}회)";
                    RemoveChild(node);
                    node.Free();
                }
                if (File.Exists(pwnedPath))
                {
                    reached = true;
                    outcome += " + 표시 파일이 만들어짐";
                    File.Delete(pwnedPath);
                }
                GD.Print($"[{(reached ? "뚫림" : "막힘")}] {name}: {outcome}");
                Expect(!reached, $"격리가 뚫림 — {name}: {outcome}");
            }
            Expect(Name != "pwned" && GetTree().Root != null, "점검 노드가 바뀜");

            // 대조군: 제한을 걸지 않으면 같은 시도가 닿아야 한다. 닿지 않으면 위의 "막힘"이 격리 덕분인지 알 수 없다.
            Node open = Load("control.sgd", "func attempt(secret : String) -> String:\n\treturn \"REACHED\" + FileAccess.get_file_as_string(secret)\n", out _, out string controlError, false);
            string controlResult = open != null ? open.Call("attempt", secretPath).AsString() : controlError;
            GD.Print($"대조군(제한 없음)의 파일 읽기: {controlResult}");
            Expect(controlResult == k_reached + k_secret, "대조군: 제한을 걸지 않았는데도 파일을 읽지 못함 (점검이 뚫림을 구분하지 못한다)");
            if (open != null)
            {
                RemoveChild(open);
                open.Free();
            }
        }

        /// <summary>
        /// 자원 제한과 실패 통지: 무한 루프, 끝없는 재귀, 메모리 폭주, 실행 오류, 문법 오류. 실패 뒤에도 같은 노드를 계속 부를 수 있어야 한다.
        /// </summary>
        private void CheckLimits()
        {
            Node node = Load("limits.sgd", @"func spin() -> int:
	var i : int = 0
	while true:
		i += 1
	return i

func recurse(n : int) -> int:
	return recurse(n + 1)

func hog() -> int:
	var items : Array = []
	while true:
		items.append(""xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"" + str(items.size()))
	return items.size()

func fail(state : Dictionary) -> int:
	var missing = state.get(""missing"")
	return missing.no_such_function()

func ok() -> int:
	return 5
", out GodotObject sandbox, out string error);
            if (node == null)
            {
                Expect(false, $"제한 점검용 스크립트 컴파일 실패: {error}");
                return;
            }

            foreach (string function in new[] { "spin", "recurse", "hog", "fail" })
            {
                int exceptions = Counter(sandbox, "get_exceptions");
                int timeouts = Counter(sandbox, "get_timeouts");
                var watch = Stopwatch.StartNew();
                Variant result = function == "fail" ? node.Call(function, new Godot.Collections.Dictionary()) : function == "recurse" ? node.Call(function, 0) : node.Call(function);
                double ms = watch.Elapsed.TotalMilliseconds;
                int newExceptions = Counter(sandbox, "get_exceptions") - exceptions;
                int newTimeouts = Counter(sandbox, "get_timeouts") - timeouts;
                GD.Print($"{function}: {ms:0} ms 뒤 돌아옴, 반환 {result.VariantType} ({result}), 예외 +{newExceptions}, 타임아웃 +{newTimeouts}");
                Expect(newExceptions + newTimeouts > 0, $"{function}: 실패가 예외·타임아웃 횟수로 드러나지 않음");
                Expect(node.Call("ok").AsInt32() == 5, $"{function}: 실패한 뒤 같은 노드를 다시 부를 수 없음");
            }

            Node broken = Load("broken.sgd", "func execute(:\n\treturn\n", out _, out string compileError);
            GD.Print($"문법 오류의 컴파일 오류 문구: {FirstLine(compileError)}");
            Expect(broken == null, "문법이 틀린 스크립트가 컴파일됨");
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int end = text.IndexOf('\n');
            return end < 0 ? text : text.Substring(0, end);
        }
    }
}
