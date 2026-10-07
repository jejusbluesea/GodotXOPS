extends Node
## 개발용 명령행 인자를 처리한다 (Autoload 이름 Dev). 인자는 "--" 뒤에 준다.
##   --window 너비x높이   설정 파일의 화면 설정 대신 그 크기의 창으로 띄운다.
##   --scene 이름         오프닝 대신 그 화면에서 시작한다 (mainmenu, briefing, maingame, result).
##   --mission 번호 [--addon] [--page 번호]   시작하기 전에 그 미션을 로드한다.
##   --ui-shot 경로.png   화면을 PNG 로 저장하고 종료한다. --ui-time 초 로 찍는 시각을 정한다 (기본 1.5).
##   --ui-state 값        화면마다 정해 둔 상태로 시작한다 (메뉴: credit / exit / addon / option / option-input / option-graphic / option-sound, 메인게임: simple / off).
##                        console 은 어느 화면에서든 설정과 무관하게 디버그 콘솔을 허용한다.
## 디버그 콘솔(XopsConsole)도 여기서 만든다. 화면(씬)이 바뀌어도 남아 있어야 해서 Autoload 아래에 둔다.
##   --ui-quit 초         그 시간이 지나면 종료한다. 헤드리스로 화면 스크립트에 오류가 없는지 볼 때 쓴다.
##   --ui-click "목록"    가짜 입력을 차례로 넣는다. 띄어쓰기로 나눈 항목마다 "x,y"(그 자리를 클릭), "x,y,초"(그 시간 동안 누르고 있기),
##                        "key:이름"(키 한 번. 이름은 Godot 키 이름, 예: key:Q), "text:글자"(글자를 차례로 친다. 띄어쓰기는 key:Space 로 넣는다. 콘솔에 명령을 칠 때 쓴다).
##                        좌표는 창 픽셀이다 (실제 마우스 커서를 옮기므로 도는 동안 마우스를 건드리지 않는다). 화면의 버튼을 눌러 본 결과를 --ui-shot 으로 볼 때 쓴다.

const DEFAULT_SHOT_TIME := 1.5
# 콘솔을 담는 CanvasLayer 의 순서. 화면 요소와 밝기·감마 사각형(100)보다 위다.
const CONSOLE_LAYER := 110
# 가짜 입력: 첫 항목을 넣는 시각, 누르기 전에 마우스를 옮겨 두는 시간, 기본으로 누르고 있는 시간, 항목 사이 간격 (초).
const CLICK_START := 0.6
const CLICK_MOVE_LEAD := 0.1
const CLICK_HOLD := 0.1
const CLICK_GAP := 0.2

var _args: PackedStringArray
var _shot_path := ""
var _shot_time := DEFAULT_SHOT_TIME
var _quit_time := -1.0
var _elapsed := 0.0
# 넣을 가짜 입력 이벤트. 항목 = {"time": 넣을 시각, "event": InputEvent}. 시각 순서로 들어 있다.
var _fake_events: Array[Dictionary] = []


func _ready() -> void:
	_args = OS.get_cmdline_user_args()

	var window := value("--window", "")
	if not window.is_empty():
		var parts := window.split("x")
		if parts.size() == 2:
			Game.SetDevWindow(int(parts[0]), int(parts[1]))

	_shot_path = value("--ui-shot", "")
	_shot_time = float(value("--ui-time", str(DEFAULT_SHOT_TIME)))
	_quit_time = float(value("--ui-quit", "-1"))
	_build_fake_events(value("--ui-click", ""))

	# 콘솔은 설정 파일의 AllowConsole 로 허용한다. --ui-state console 은 설정 파일을 고치지 않고 화면을 확인할 때 쓴다.
	if ConfigManager.GetBool("General", "AllowConsole", false) or value("--ui-state", "") == "console":
		var console_layer := CanvasLayer.new()
		console_layer.layer = CONSOLE_LAYER
		add_child(console_layer)
		XopsConsole.create(console_layer)
	set_process(not _shot_path.is_empty() or _quit_time >= 0.0 or not _fake_events.is_empty())


func _process(delta: float) -> void:
	_elapsed += delta
	while not _fake_events.is_empty() and _fake_events[0]["time"] <= _elapsed:
		var event: InputEvent = _fake_events.pop_front()["event"]
		# 화면 요소는 실제 커서 위치로 마우스 자리를 읽으므로 커서도 함께 옮긴다.
		if event is InputEventMouseMotion:
			get_viewport().warp_mouse(event.position)
		Input.parse_input_event(event)
	if _quit_time >= 0.0 and _elapsed >= _quit_time:
		get_tree().quit(0)
		return
	if _shot_path.is_empty() or _elapsed < _shot_time:
		return

	set_process(false)
	await RenderingServer.frame_post_draw
	var error := get_viewport().get_texture().get_image().save_png(_shot_path)
	print("화면 저장 %s: %s" % ["성공" if error == OK else "실패", _shot_path])
	get_tree().quit(0 if error == OK else 1)


## --ui-click 의 항목들을 시각이 붙은 입력 이벤트 목록으로 바꾼다.
func _build_fake_events(list: String) -> void:
	var time := CLICK_START
	for item in list.split(" ", false):
		if item.begins_with("key:"):
			var keycode := OS.find_keycode_from_string(item.trim_prefix("key:"))
			for is_pressed: bool in [true, false]:
				var key := InputEventKey.new()
				key.physical_keycode = keycode
				key.keycode = keycode
				# 띄어쓰기는 글자로도 들어가야 콘솔에 칠 수 있다.
				if keycode == KEY_SPACE:
					key.unicode = KEY_SPACE
				key.pressed = is_pressed
				_fake_events.append({"time": time, "event": key})
				time += CLICK_HOLD
		elif item.begins_with("text:"):
			for character in item.trim_prefix("text:"):
				var typed := InputEventKey.new()
				typed.unicode = character.unicode_at(0)
				typed.pressed = true
				_fake_events.append({"time": time, "event": typed})
				time += CLICK_HOLD
		else:
			var parts := item.split(",")
			if parts.size() < 2:
				continue
			var at := Vector2(float(parts[0]), float(parts[1]))
			var motion := InputEventMouseMotion.new()
			motion.position = at
			motion.global_position = at
			_fake_events.append({"time": time, "event": motion})
			time += CLICK_MOVE_LEAD
			for is_pressed: bool in [true, false]:
				var button := InputEventMouseButton.new()
				button.button_index = MOUSE_BUTTON_LEFT
				button.pressed = is_pressed
				button.position = at
				button.global_position = at
				_fake_events.append({"time": time, "event": button})
				if is_pressed:
					time += float(parts[2]) if parts.size() > 2 else CLICK_HOLD
		time += CLICK_GAP


## 인자가 있는지.
func has(arg_name: String) -> bool:
	return _args.has(arg_name)


## 인자 바로 뒤의 값. 없으면 fallback.
func value(arg_name: String, fallback: String) -> String:
	var index := _args.find(arg_name)
	if index >= 0 and index + 1 < _args.size():
		return _args[index + 1]
	return fallback
