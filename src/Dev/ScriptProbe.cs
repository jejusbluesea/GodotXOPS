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
        // 틱 비용 측정: 사람 수의 상한, 평균을 낼 틱 수, 한 틱의 길이 (ms, 33.333Hz).
        private const int k_tickHumansMax = 128;
        private const int k_tickRounds = 200;
        private const double k_tickMs = 30.0;
        // 이보다 오래 걸린 틱을 "튄 틱"으로 센다 (ms).
        private const double k_tickSlowMs = 10.0;
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
            CheckFrameCost();
            CheckTickCost();

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
        /// 화면 스크립트를 프레임마다 한 번 부를 때의 비용을 잰다 (측정이라 실패로 치지 않는다).
        /// 값을 얻는 방법 둘(스크립트가 API 로 하나씩 묻기 / 게임이 사전 하나로 넘기기)과, 화면 요소를 고치는 호출의 수(바뀐 것만 / 전부)를 바꿔 가며 본다.
        /// </summary>
        private void CheckFrameCost()
        {
            const string source = @"var api
var last = {}
var keys = [""hp"", ""magazine"", ""reserve"", ""weapon"", ""reloading"", ""switching"", ""scoping"", ""first_person"", ""error_range"", ""blind"", ""result"", ""alive""]

func init(a):
	api = a

func empty(delta):
	return 0

func pull(delta, force):
	var sets = 0
	for k in keys:
		var v = api[""get""].call(k)
		if force or not last.has(k) or last[k] != v:
			last[k] = v
			api[""set""].call(sets, k, v)
			sets += 1
	return sets

func push(v, force):
	var sets = 0
	for k in keys:
		var x = v[k]
		if force or not last.has(k) or last[k] != x:
			last[k] = x
			api[""set""].call(sets, k, x)
			sets += 1
	return sets

func push_batch(v, force):
	var out = {}
	for k in keys:
		var x = v[k]
		if force or not last.has(k) or last[k] != x:
			last[k] = x
			out[k] = x
	if out.size() > 0:
		api[""set_many""].call(out)
	return out.size()
";
            var values = new Godot.Collections.Dictionary
            {
                ["hp"] = 100f, ["magazine"] = 30, ["reserve"] = 90, ["weapon"] = "MP5", ["reloading"] = false, ["switching"] = false,
                ["scoping"] = false, ["first_person"] = true, ["error_range"] = 3, ["blind"] = 0, ["result"] = 0, ["alive"] = true,
            };
            int setCalls = 0;
            var api = new Godot.Collections.Dictionary
            {
                ["get"] = Callable.From((string key) => values[key]),
                ["set"] = Callable.From((int id, string property, Variant value) => { setCalls++; }),
                ["set_many"] = Callable.From((Godot.Collections.Dictionary changes) => { setCalls += changes.Count; }),
            };

            Node node = Load("frame.sgd", source, out GodotObject sandbox, out string error, inTree: false, configure: n => n.Set("execution_timeout", 200));
            if (node == null)
            {
                GD.Print($"[프레임] 컴파일 실패: {FirstLine(error)}");
                return;
            }
            node.Call("init", api);

            void Measure(string label, Func<Variant> call)
            {
                call();
                int exceptions = Counter(sandbox, "get_exceptions");
                setCalls = 0;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < k_benchCalls; i++) call();
                double us = watch.Elapsed.TotalMilliseconds * 1000.0 / k_benchCalls;
                bool failed = Counter(sandbox, "get_exceptions") != exceptions;
                GD.Print($"[프레임] {label}: 1회 {us:0.0} us, 요소 고치기 {setCalls / (double)k_benchCalls:0.0}회{(failed ? " (예외 발생)" : "")}");
            }

            Measure("빈 함수", () => node.Call("empty", 0.016));
            Measure("API 로 12개 묻기, 바뀐 것 없음", () => node.Call("pull", 0.016, false));
            Measure("API 로 12개 묻기, 12개 전부 고침", () => node.Call("pull", 0.016, true));
            Measure("사전으로 12개 받기, 바뀐 것 없음", () => node.Call("push", values, false));
            Measure("사전으로 12개 받기, 12개 전부 고침", () => node.Call("push", values, true));
            Measure("사전으로 12개 받기, 12개를 한 번에 고침", () => node.Call("push_batch", values, true));
            node.Free();
        }

        /// <summary>
        /// 사람마다 틱마다 한 번씩 스크립트를 부를 때의 비용을 잰다 (측정이라 실패로 치지 않는다. 사람 종류·무기에 스크립트를 붙이는 설계의 전제).
        /// 사람 수, 샌드박스를 나누는 방법(하나를 모두가 같이 쓰기 / 사람마다 하나), 값을 주고받는 모양을 바꿔 가며 한 틱의 합계를 본다.
        /// API 쪽 함수는 값만 돌려주는 빈 껍데기라, 여기서 나오는 시간은 스크립트를 부르고 값을 건네는 비용뿐이다 (레이캐스트 같은 게임 쪽 계산은 들어 있지 않다).
        /// </summary>
        private void CheckTickCost()
        {
            const string source = @"var api

func init(a):
	api = a

func empty(id):
	return 0

func regen_value(hp, max_hp, since_hit):
	if since_hit >= 100 and hp < max_hp:
		return hp + 1
	return hp

func regen_dict(h, state):
	var t = 0
	if state.has(""t""):
		t = state[""t""]
	t += 1
	state[""t""] = t
	if t >= 10 and h[""hp""] < h[""max_hp""]:
		state[""t""] = 0
		return h[""hp""] + 1
	return -1

func regen_api(id):
	var h = api[""human""].call(id)
	if h[""alive""] and h[""hp""] < h[""max_hp""]:
		api[""set_hp""].call(id, h[""hp""] + 1)
	return 0

func regen_all(hps, max_hp):
	var out = []
	for i in range(hps.size()):
		var hp = hps[i]
		if hp < max_hp:
			hp += 1
		out.append(hp)
	return out

func ai_host(id):
	var target = api[""nearest_enemy""].call(id)
	if target < 0:
		return 0
	if api[""can_see""].call(id, target):
		api[""input""].call(id, 1.0, 0.0, true)
	return 1

func ai_script(id, xs, zs, teams):
	var best = -1
	var best_d = 1.0e30
	var mx = xs[id]
	var mz = zs[id]
	var team = teams[id]
	for i in range(xs.size()):
		if teams[i] != team:
			var dx = xs[i] - mx
			var dz = zs[i] - mz
			var d = dx * dx + dz * dz
			if d < best_d:
				best_d = d
				best = i
	if best < 0:
		return 0
	if api[""can_see""].call(id, best):
		api[""input""].call(id, atan2(xs[best] - mx, zs[best] - mz), 0.0, true)
	return 1
";
            GodotObject script = ClassDB.Instantiate("SafeGDScript").AsGodotObject();
            script.Call("set_source_code", source);
            string error = script.Call("get_compile_error").AsString();
            if (!string.IsNullOrEmpty(error))
            {
                GD.Print($"[틱] 컴파일 실패: {FirstLine(error)}");
                return;
            }

            int apiCalls = 0;
            var info = new Godot.Collections.Dictionary { ["alive"] = true, ["hp"] = 80f, ["max_hp"] = 100f, ["x"] = 1f, ["y"] = 2f, ["z"] = 3f };
            var api = new Godot.Collections.Dictionary
            {
                ["human"] = Callable.From((int id) => { apiCalls++; return info; }),
                ["set_hp"] = Callable.From((int id, float hp) => { apiCalls++; }),
                ["nearest_enemy"] = Callable.From((int id) => { apiCalls++; return id ^ 1; }),
                ["can_see"] = Callable.From((int id, int target) => { apiCalls++; return true; }),
                ["input"] = Callable.From((int id, float yaw, float pitch, bool fire) => { apiCalls++; }),
            };

            var nodes = new List<Node>();
            var sandboxes = new List<GodotObject>();
            long before = Process.GetCurrentProcess().PrivateMemorySize64;
            var buildWatch = Stopwatch.StartNew();
            for (int i = 0; i < k_tickHumansMax; i++)
            {
                var node = new Node();
                node.SetScript(script);
                node.Set("restrictions", true);
                node.Set("execution_timeout", 200);
                node.Set("memory_max", k_memoryMax);
                Variant found = script.Call("get_sandbox_for", node);
                sandboxes.Add(found.VariantType == Variant.Type.Object ? found.AsGodotObject() : null);
                node.Call("init", api);
                nodes.Add(node);
            }
            long grown = Process.GetCurrentProcess().PrivateMemorySize64 - before;
            GD.Print($"[틱] 같은 스크립트의 샌드박스 {k_tickHumansMax}개 만들기: 개당 {buildWatch.Elapsed.TotalMilliseconds / k_tickHumansMax:0.00} ms, {grown / 1048576.0 / k_tickHumansMax:0.00} MB");

            int Exceptions()
            {
                int total = 0;
                foreach (GodotObject sandbox in sandboxes) total += Counter(sandbox, "get_exceptions");
                return total;
            }

            // 한 틱 = 사람 수만큼의 호출. separate 면 사람마다 자기 샌드박스를, 아니면 전부 0번 샌드박스를 부른다.
            string Run(int humans, bool separate, Action<Node, int> call)
            {
                for (int h = 0; h < humans; h++) call(nodes[separate ? h : 0], h);
                int exceptions = Exceptions();
                apiCalls = 0;
                double total = 0.0;
                double worst = 0.0;
                int slow = 0;
                int collections = GC.CollectionCount(0);
                int fullCollections = GC.CollectionCount(2);
                for (int tick = 0; tick < k_tickRounds; tick++)
                {
                    var watch = Stopwatch.StartNew();
                    for (int h = 0; h < humans; h++) call(nodes[separate ? h : 0], h);
                    double ms = watch.Elapsed.TotalMilliseconds;
                    total += ms;
                    if (ms > worst) worst = ms;
                    if (ms > k_tickSlowMs) slow++;
                }
                double average = total / k_tickRounds;
                string failed = Exceptions() != exceptions ? " (예외 발생)" : string.Empty;
                return $"{average:0.000} ms (최대 {worst:0.00}, {k_tickSlowMs:0} ms 넘은 틱 {slow}, GC {GC.CollectionCount(0) - collections}/{GC.CollectionCount(2) - fullCollections}, 1회 {average * 1000.0 / humans:0.0} us, 틱의 {average / k_tickMs * 100.0:0.00}%, API {apiCalls / (double)k_tickRounds / humans:0.0}회){failed}";
            }

            foreach (int humans in new[] { 1, 16, 64, k_tickHumansMax })
            {
                var states = new List<Godot.Collections.Dictionary>();
                var hps = new Godot.Collections.Array();
                var xs = new float[humans];
                var zs = new float[humans];
                var teams = new int[humans];
                for (int h = 0; h < humans; h++)
                {
                    states.Add(new Godot.Collections.Dictionary());
                    hps.Add(80f);
                    xs[h] = h * 1.5f;
                    zs[h] = (h % 7) * 2.0f;
                    teams[h] = h % 2;
                }
                Variant packedX = xs;
                Variant packedZ = zs;
                Variant packedTeams = teams;

                var rows = new (string label, Action<Node, int> call)[]
                {
                    ("빈 함수", (node, h) => node.Call("empty", h)),
                    ("체력 재생, 수 3개를 주고 수 하나를 받음", (node, h) => node.Call("regen_value", 80f, 100f, 120)),
                    ("체력 재생, 사전(값 6개)과 저장 칸을 줌", (node, h) => node.Call("regen_dict", new Godot.Collections.Dictionary { ["alive"] = true, ["hp"] = 80f, ["max_hp"] = 100f, ["x"] = 1f, ["y"] = 2f, ["z"] = 3f }, states[h])),
                    ("체력 재생, API 로 묻고 API 로 고침", (node, h) => node.Call("regen_api", h)),
                    ("AI 흉내, 찾기는 게임이 하고 API 3번", (node, h) => node.Call("ai_host", h)),
                    ("AI 흉내, 스크립트가 전원의 배열을 훑고 API 2번", (node, h) => node.Call("ai_script", h, packedX, packedZ, packedTeams)),
                };
                foreach (var row in rows)
                {
                    GD.Print($"[틱] {humans}명, {row.label}");
                    GD.Print($"[틱]     한 샌드박스: {Run(humans, false, row.call)}");
                    if (humans > 1) GD.Print($"[틱]     사람마다 샌드박스: {Run(humans, true, row.call)}");
                }

                // 한 틱에 한 번만 부르고 전원의 값을 배열로 주고받는 모양.
                int batchExceptions = Exceptions();
                Variant batchResult = nodes[0].Call("regen_all", hps, 100f);
                Expect(batchResult.VariantType == Variant.Type.Array && batchResult.AsGodotArray().Count == humans, $"한 번의 호출로 받은 배열의 크기가 사람 수와 다름 ({humans}명)");
                var batchWatch = Stopwatch.StartNew();
                for (int tick = 0; tick < k_tickRounds; tick++) nodes[0].Call("regen_all", hps, 100f);
                double batchMs = batchWatch.Elapsed.TotalMilliseconds / k_tickRounds;
                GD.Print($"[틱] {humans}명, 체력 재생을 한 번의 호출로 (배열을 주고 배열을 받음): {batchMs:0.000} ms (틱의 {batchMs / k_tickMs * 100.0:0.00}%){(Exceptions() != batchExceptions ? " (예외 발생)" : string.Empty)}");
            }

            foreach (Node node in nodes) node.Free();
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
