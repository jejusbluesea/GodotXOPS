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
        private const int k_compileCount = 30;
        private const string k_processSource = "var frames : int = 0\n\nfunc _process(_delta):\n\tframes += 1\n\nfunc _physics_process(_delta):\n\tframes += 1\n\nfunc get_frames() -> int:\n\treturn frames\n";

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();
        private readonly List<string> m_log = new List<string>();
        private string m_folder;
        private Node m_processControl;
        private Node m_processOff;
        private Node m_processDisabled;
        private bool m_waiting;
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
            CheckContract();
            CheckHostile();
            CheckLimits();
            CheckMemory();
            CheckCompileTime();
            CheckOutsideTree();
            CheckApiBudget();

            // 샌드박스 안의 _process 가 도는지, 끄는 두 방법이 듣는지는 몇 프레임 뒤에 본다.
            m_processControl = Load("process.sgd", k_processSource, out _, out _);
            m_processOff = Load("process_off.sgd", k_processSource, out _, out _);
            if (m_processOff != null)
            {
                m_processOff.SetProcess(false);
                m_processOff.SetPhysicsProcess(false);
            }
            m_processDisabled = Load("process_disabled.sgd", k_processSource, out _, out _, configure: node => node.ProcessMode = ProcessModeEnum.Disabled);
            m_waiting = true;
        }

        public override void _Process(double delta)
        {
            if (!m_waiting) return;
            if (++m_frames < k_waitFrames) return;

            m_waiting = false;
            int control = m_processControl != null ? m_processControl.Call("get_frames").AsInt32() : -1;
            int off = m_processOff != null ? m_processOff.Call("get_frames").AsInt32() : -1;
            int disabled = m_processDisabled != null ? m_processDisabled.Call("get_frames").AsInt32() : -1;
            GD.Print($"샌드박스 안의 _process / _physics_process 가 {k_waitFrames} 프레임 동안 돈 횟수: 그대로 {control}, SetProcess(false) {off}, ProcessMode.Disabled {disabled}");
            Expect(off == 0, $"SetProcess(false) 뒤에도 샌드박스의 _process 가 돎 ({off})");
            Expect(disabled == 0, $"ProcessMode.Disabled 에서도 샌드박스의 _process 가 돎 ({disabled})");
            Expect(m_processDisabled != null && m_processDisabled.Call("get_frames").VariantType == Variant.Type.Int, "ProcessMode.Disabled 인 노드를 직접 부를 수 없음");
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
        /// <param name="inTree">false 면 노드를 트리에 넣지 않는다 (부른 쪽이 Free 한다).</param>
        /// <param name="configure">트리에 넣기 직전에 노드에 더 할 설정. 없으면 null.</param>
        /// <returns>스크립트가 붙은 노드. 컴파일하지 못했으면 null.</returns>
        private Node Load(string fileName, string source, out GodotObject sandbox, out string compileError, bool restricted = true, bool inTree = true, Action<Node> configure = null)
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
            configure?.Invoke(node);
            if (inTree) AddChild(node);

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
        /// 이벤트 스크립트의 계약 모양 그대로: 형식을 적지 않은 init(api) / tick(p, state), 이름 붙은 파라미터 사전, 출구 번호 반환, return 이 없을 때의 반환값.
        /// </summary>
        private void CheckContract()
        {
            Node node = Load("contract.sgd", @"var api

func init(a):
	api = a

func tick(p, state):
	state[""ticks""] = state.get(""ticks"", 0) + 1
	if api[""get_var""].call(p[""var""]) >= p[""value""]:
		return 0
	return -1

func no_return(p, state):
	state[""x""] = 1
", out GodotObject sandbox, out string error);
            if (node == null)
            {
                Expect(false, $"계약 모양의 스크립트 컴파일 실패: {error}");
                return;
            }

            var vars = new Dictionary<int, int> { [3] = 1 };
            var api = new Godot.Collections.Dictionary { ["get_var"] = Callable.From((int index) => vars.TryGetValue(index, out int value) ? value : 0) };
            node.Call("init", api);
            var p = new Godot.Collections.Dictionary { ["var"] = 3, ["value"] = 2 };
            var state = new Godot.Collections.Dictionary();
            Variant waiting = node.Call("tick", p, state);
            vars[3] = 2;
            Variant done = node.Call("tick", p, state);
            Expect(waiting.VariantType == Variant.Type.Int && waiting.AsInt32() == -1, $"tick 이 기다림(-1)을 돌려주지 않음 ({waiting.VariantType} {waiting})");
            Expect(done.VariantType == Variant.Type.Int && done.AsInt32() == 0, $"tick 이 출구 0 을 돌려주지 않음 ({done.VariantType} {done})");
            Expect(state.ContainsKey("ticks") && state["ticks"].AsInt32() == 2, "tick 이 state 에 쓴 값이 호스트에 보이지 않음");

            int exceptions = Counter(sandbox, "get_exceptions");
            Variant nothing = node.Call("no_return", p, state);
            GD.Print($"return 이 없는 함수의 반환: {nothing.VariantType}, 예외 +{Counter(sandbox, "get_exceptions") - exceptions}");
            Expect(nothing.VariantType != Variant.Type.Int, "return 이 없는 함수가 정수를 돌려줌 (출구 번호와 구분할 수 없다)");
        }

        /// <summary>
        /// 메모리 한도(memory_max)가 실행 예산과 별개로 듣는지. 명령 수는 적고 한 번에 크게 잡는 할당을 시켜 보고 결과를 적는다 (측정이라 실패로 치지 않는다).
        /// </summary>
        private void CheckMemory()
        {
            var attempts = new (string name, string source)[]
            {
                ("배열 resize 4,000,000", "func grab() -> int:\n\tvar a : Array = []\n\ta.resize(4000000)\n\treturn a.size()\n"),
                ("PackedByteArray resize 128 MB", "func grab() -> int:\n\tvar b : PackedByteArray = PackedByteArray()\n\tb.resize(134217728)\n\treturn b.size()\n"),
                ("문자열 두 배씩 32M 글자", "func grab() -> int:\n\tvar s : String = \"xxxxxxxxxxxxxxxx\"\n\tfor i in range(21):\n\t\ts = s + s\n\treturn s.length()\n"),
            };
            int index = 0;
            foreach ((string name, string source) in attempts)
            {
                Node node = Load($"memory_{index++}.sgd", source, out GodotObject sandbox, out string error);
                if (node == null)
                {
                    GD.Print($"[메모리] {name}: 컴파일 거절 ({FirstLine(error)})");
                    continue;
                }
                long before = Process.GetCurrentProcess().PrivateMemorySize64;
                int exceptions = Counter(sandbox, "get_exceptions");
                var watch = Stopwatch.StartNew();
                Variant result = node.Call("grab");
                double ms = watch.Elapsed.TotalMilliseconds;
                long grown = Process.GetCurrentProcess().PrivateMemorySize64 - before;
                bool blocked = Counter(sandbox, "get_exceptions") > exceptions;
                GD.Print($"[메모리] {name}: {(blocked ? "막힘" : "성공")} (반환 {result}, {ms:0} ms, 프로세스 메모리 {grown / 1048576.0:+0.0;-0.0} MB)");
                // 막히지 않는 것이 알려진 한계다 (배열·문자열은 호스트 쪽 Variant 라 memory_max 가 세지 않는다). 결과만 적고 실패로 치지 않는다.
                RemoveChild(node);
                node.Free();
            }
        }

        /// <summary>
        /// 스크립트가 많을 때의 로딩 비용: 같은 크기의 스크립트를 여러 개 컴파일해 인스턴스를 만든 시간과 메모리 증가.
        /// </summary>
        private void CheckCompileTime()
        {
            var nodes = new List<Node>();
            long before = Process.GetCurrentProcess().PrivateMemorySize64;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < k_compileCount; i++)
            {
                string source = "var api\n\nfunc init(a):\n\tapi = a\n\nfunc tick(p, state):\n\tvar total : int = " + i + "\n\tfor k in range(10):\n\t\ttotal += k * p[\"value\"]\n\tif total > 100:\n\t\treturn 0\n\treturn -1\n";
                Node node = Load($"compile_{i}.sgd", source, out _, out _);
                if (node != null) nodes.Add(node);
            }
            double ms = watch.Elapsed.TotalMilliseconds;
            long grown = Process.GetCurrentProcess().PrivateMemorySize64 - before;
            GD.Print($"스크립트 {k_compileCount}개 컴파일·인스턴스: 합계 {ms:0} ms (개당 {ms / k_compileCount:0.0} ms), 프로세스 메모리 {grown / 1048576.0:+0.0;-0.0} MB (개당 {grown / 1048576.0 / k_compileCount:0.00} MB)");
            Expect(nodes.Count == k_compileCount, $"{k_compileCount}개 가운데 {nodes.Count}개만 컴파일됨");
            foreach (Node node in nodes)
            {
                RemoveChild(node);
                node.Free();
            }

            // 비용이 어디서 나는지 나눠 본다: 컴파일만 / 같은 스크립트의 인스턴스만 / 메모리 한도를 낮춘 인스턴스.
            string shared = "func tick(p, state):\n\treturn p[\"value\"] + 1\n";
            watch.Restart();
            var scripts = new List<GodotObject>();
            for (int i = 0; i < k_compileCount; i++)
            {
                GodotObject script = ClassDB.Instantiate("SafeGDScript").AsGodotObject();
                script.Call("set_source_code", shared + "# " + i + "\n");
                scripts.Add(script);
            }
            GD.Print($"컴파일만 {k_compileCount}개: 개당 {watch.Elapsed.TotalMilliseconds / k_compileCount:0.0} ms");

            foreach (int memoryMax in new[] { 16, 4, 1 })
            {
                before = Process.GetCurrentProcess().PrivateMemorySize64;
                watch.Restart();
                var instances = new List<Node>();
                for (int i = 0; i < k_compileCount; i++)
                {
                    var node = new Node();
                    node.SetScript(scripts[0]);
                    node.Set("restrictions", true);
                    node.Set("execution_timeout", k_executionTimeout);
                    node.Set("memory_max", memoryMax);
                    instances.Add(node);
                }
                Variant value = instances[k_compileCount - 1].Call("tick", new Godot.Collections.Dictionary { ["value"] = 1 }, new Godot.Collections.Dictionary());
                ms = watch.Elapsed.TotalMilliseconds;
                grown = Process.GetCurrentProcess().PrivateMemorySize64 - before;
                GD.Print($"같은 스크립트의 인스턴스 {k_compileCount}개 (memory_max {memoryMax}): 개당 {ms / k_compileCount:0.0} ms, {grown / 1048576.0 / k_compileCount:0.00} MB, 호출 결과 {value}");
                foreach (Node node in instances) node.Free();
            }
        }

        /// <summary>
        /// 실행 예산(execution_timeout) 안에서 API(C# 함수)를 몇 번 부를 수 있는지 잰다. 예산마다 호출 수를 늘려 가며 끊기는 지점을 적는다 (측정이라 실패로 치지 않는다).
        /// </summary>
        private void CheckApiBudget()
        {
            const string source = @"var api

func init(a):
	api = a

func calls(n):
	var t = 0
	for i in range(n):
		t += api[""add""].call(i, 1)
	return t

func info(n):
	var t = 0
	for i in range(n):
		if api[""info""].call(i)[""alive""]:
			t += 1
	return t

func plain(n):
	var t = 0
	for i in range(n):
		t += i
	return t

func spin():
	var i = 0
	while true:
		i += 1
	return i
";
            var api = new Godot.Collections.Dictionary
            {
                ["add"] = Callable.From((int x, int y) => x + y),
                ["info"] = Callable.From((int index) => new Godot.Collections.Dictionary { ["alive"] = true, ["hp"] = 100f, ["x"] = 1f, ["y"] = 2f, ["z"] = 3f }),
            };
            foreach (int timeout in new[] { 2, 20, 200 })
            {
                Node node = Load($"budget_{timeout}.sgd", source, out GodotObject sandbox, out _, inTree: false, configure: n => n.Set("execution_timeout", timeout));
                if (node == null) continue;
                node.Call("init", api);
                foreach (string function in new[] { "plain", "calls", "info" })
                {
                    int passed = 0;
                    double us = 0.0;
                    foreach (int count in new[] { 10, 30, 100, 300, 1000, 3000, 10000, 100000 })
                    {
                        int exceptions = Counter(sandbox, "get_exceptions");
                        var watch = Stopwatch.StartNew();
                        node.Call(function, count);
                        double elapsed = watch.Elapsed.TotalMilliseconds * 1000.0;
                        if (Counter(sandbox, "get_exceptions") != exceptions) break;
                        passed = count;
                        us = elapsed / count;
                    }
                    GD.Print($"[예산 {timeout}] {function}: 반복 {passed}회까지 통과 (1회 {us:0.00} us)");
                }
                var spinWatch = Stopwatch.StartNew();
                node.Call("spin");
                GD.Print($"[예산 {timeout}] 끝나지 않는 루프가 끊길 때까지: {spinWatch.Elapsed.TotalMilliseconds:0} ms");
                node.Free();
            }
        }

        /// <summary>
        /// 노드를 트리에 넣지 않고도 부를 수 있는지, 그때도 격리가 듣는지. 되면 _ready / _process 가 아예 돌지 않는다.
        /// </summary>
        private void CheckOutsideTree()
        {
            string secretPath = Path.Combine(m_folder, "secret_outside.txt").Replace('\\', '/');
            File.WriteAllText(secretPath, k_secret);
            Node node = Load("outside.sgd", "func tick(p, state):\n\treturn p[\"value\"] + 1\n\nfunc attempt(secret : String) -> String:\n\treturn \"REACHED\" + FileAccess.get_file_as_string(secret)\n", out GodotObject sandbox, out string error, inTree: false);
            if (node == null)
            {
                Expect(false, $"트리 밖 점검용 스크립트 컴파일 실패: {error}");
                return;
            }
            Variant result = node.Call("tick", new Godot.Collections.Dictionary { ["value"] = 41 }, new Godot.Collections.Dictionary());
            Variant read = node.Call("attempt", secretPath);
            string text = read.VariantType == Variant.Type.String ? read.AsString() : string.Empty;
            GD.Print($"트리 밖 노드: tick 반환 {result.VariantType} {result}, 샌드박스 {(sandbox != null ? "있음" : "없음")}, 파일 읽기 {(text.StartsWith(k_reached, StringComparison.Ordinal) ? "닿음" : "막힘")}");
            Expect(result.VariantType == Variant.Type.Int && result.AsInt32() == 42, "트리에 넣지 않은 노드를 부를 수 없음");
            Expect(!text.StartsWith(k_reached, StringComparison.Ordinal), "트리에 넣지 않은 노드에서 격리가 뚫림 (파일 읽기)");
            node.Free();
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
