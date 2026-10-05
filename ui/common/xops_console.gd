class_name XopsConsole
extends Node
## 디버그 콘솔 (원본 OpenXOPS 의 F11 콘솔). 설정 파일(config.json)의 General / AllowConsole 이 true 일 때만 메인게임이 만든다.
## F11 로 열고 닫는다. 열려 있는 동안 게임 입력을 막고(InputManager.InputBlocked) 친 글자를 한 줄로 모아 Enter 에 Game.ConsoleExecute 로 넘긴다.
## 명령과 그 결과는 C# 의 DebugConsole 이 맡고, 여기서는 화면과 글자 입력만 다룬다.
## 입력과 출력은 영어만 쓴다 (사용자 결정). 한글 입력 상태여도 그 키의 영문자가 들어간다.

const TOGGLE_KEY := KEY_F11
# 층의 순서. 메인게임의 모든 화면 요소보다 위다.
const ORDER := 50

# 배치와 모양. 좌표는 화면 높이를 480 으로 본 값이다 (확대되는 층).
const FONT_SIZE := 11
const VISIBLE_LINES := 14
const MARGIN_X := 8
const MARGIN_Y := 4
const PANEL_COLOR := Color(0, 0, 0, 0.6)
const TEXT_COLOR := Color(1, 1, 1)
const INPUT_COLOR := Color(1, 1, 0)
const INFO_COLOR := Color(0, 1, 0)
const INFO_LINES := 7
const PROMPT := "> "
const CURSOR := "_"
const CURSOR_BLINK := 0.5

# 기억해 두는 줄 수와 입력 한 줄의 최대 글자 수.
const MAX_LINES := 200
const MAX_HISTORY := 32
const MAX_INPUT := 120

var _layer: XopsLayer
var _panel: ColorRect
var _output: Label
var _input_label: Label
var _info: Label

var _open := false
# 닫을 때 입력 차단을 그 프레임의 _process 에서 푼다. 키 이벤트를 받는 도중에 풀면, 콘솔을 닫은 Esc 가 뒤이어 게임 쪽에 전달된다.
var _unblock_pending := false
var _line := ""
var _lines: Array[String] = []
var _history: Array[String] = []
var _history_index := -1
var _blink := 0.0


## 콘솔을 만들어 parent(화면 요소를 담는 CanvasLayer) 아래에 붙인다.
static func create(parent: Node) -> XopsConsole:
	var console := XopsConsole.new()
	parent.add_child(console)
	return console


func _ready() -> void:
	_layer = XopsUI.layer(self, ORDER, true)

	# 줄 높이는 글꼴마다 달라서(OS 언어로 글꼴을 고른다) 실제 값을 재서 쓴다.
	var line_height := ceili(_layer.os_font().get_height(FONT_SIZE))
	var panel_height := MARGIN_Y * 2 + line_height * (VISIBLE_LINES + 1)
	_panel = XopsUI.panel_stretch(_layer, XopsUI.Stretch.TOP, 0, 0, 0, panel_height, PANEL_COLOR)

	_output = XopsUI.label(_layer, "", FONT_SIZE, TEXT_COLOR)
	_output.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	_output.clip_text = true
	XopsUI.place_stretch(_output, XopsUI.Stretch.TOP, 0, -MARGIN_Y, -MARGIN_X * 2, line_height * VISIBLE_LINES)

	_input_label = XopsUI.label(_layer, PROMPT, FONT_SIZE, INPUT_COLOR)
	_input_label.clip_text = true
	XopsUI.place_stretch(_input_label, XopsUI.Stretch.TOP, 0, -MARGIN_Y - line_height * VISIBLE_LINES, -MARGIN_X * 2, line_height)

	# 디버그 텍스트(info)는 화면 왼쪽 위에 붙인다. 콘솔과 같은 자리라 콘솔 상자보다 먼저 그려서, 콘솔이 열려 있으면 반투명한 상자 뒤로 비쳐 보이게 한다.
	_info = XopsUI.label(_layer, "", FONT_SIZE, INFO_COLOR)
	XopsUI.place_stretch(_info, XopsUI.Stretch.TOP, 0, -MARGIN_Y, -MARGIN_X * 2, line_height * INFO_LINES)
	_info.visible = false
	_layer.move_child(_info, 0)

	# 글상자는 기본으로 줄 사이에 여백을 더 넣는다. 없애야 잰 줄 높이와 맞는다.
	for label: Label in [_output, _input_label, _info]:
		label.add_theme_constant_override("line_spacing", 0)

	_print("F11: close | help: list commands")
	_apply_open()


func _exit_tree() -> void:
	# 콘솔이 열린 채 화면이 바뀌어도(미션 종료, 재시작) 다음 화면의 입력이 막힌 채 남지 않게 한다.
	InputManager.InputBlocked = false


func _process(delta: float) -> void:
	if _unblock_pending:
		_unblock_pending = false
		InputManager.InputBlocked = false

	var show_info: bool = Game.ConsoleInfoVisible()
	_info.visible = show_info
	if show_info:
		_info.text = Game.ConsoleInfoText()

	if _open:
		_blink = fmod(_blink + delta, CURSOR_BLINK * 2.0)
		_input_label.text = PROMPT + _line + (CURSOR if _blink < CURSOR_BLINK else "")


func _input(event: InputEvent) -> void:
	var key := event as InputEventKey
	if key == null or not key.pressed:
		return

	if key.physical_keycode == TOGGLE_KEY:
		if not key.echo:
			_set_open(not _open)
		get_viewport().set_input_as_handled()
		return
	if not _open:
		return

	get_viewport().set_input_as_handled()
	match key.keycode:
		KEY_ENTER, KEY_KP_ENTER:
			if not key.echo:
				_submit()
		KEY_ESCAPE:
			_set_open(false)
		KEY_BACKSPACE:
			_line = _line.left(-1)
		KEY_UP:
			_recall(1)
		KEY_DOWN:
			_recall(-1)
		_:
			var character := _typed_character(key)
			if not character.is_empty() and _line.length() < MAX_INPUT:
				_line += character
	_blink = 0.0


## 키 입력을 콘솔에 넣을 글자로 바꾼다. 영어(ASCII)만 받는다.
## 한글 입력 상태에서는 글자 코드가 한글로 오거나 아예 오지 않으므로, 그때는 키 이름에서 영문자와 숫자를 얻는다.
func _typed_character(key: InputEventKey) -> String:
	if key.unicode >= 32 and key.unicode <= 126:
		return String.chr(key.unicode)
	if key.keycode >= KEY_A and key.keycode <= KEY_Z:
		return String.chr(key.keycode).to_lower()
	if key.keycode >= KEY_0 and key.keycode <= KEY_9:
		return String.chr(key.keycode)
	if key.keycode >= KEY_KP_0 and key.keycode <= KEY_KP_9:
		return String.chr(KEY_0 + key.keycode - KEY_KP_0)
	if key.keycode == KEY_SPACE:
		return " "
	return ""


func _set_open(value: bool) -> void:
	if _open == value:
		return
	_open = value
	if _open:
		_unblock_pending = false
		InputManager.InputBlocked = true
	else:
		_unblock_pending = true
	_apply_open()


func _apply_open() -> void:
	_panel.visible = _open
	_output.visible = _open
	_input_label.visible = _open


## 입력한 줄을 실행하고 결과를 찍는다.
func _submit() -> void:
	var command := _line.strip_edges()
	_line = ""
	_history_index = -1
	if command.is_empty():
		return

	_history.push_front(command)
	if _history.size() > MAX_HISTORY:
		_history.pop_back()

	_print(PROMPT + command)
	var result: String = Game.ConsoleExecute(command)
	if not result.is_empty():
		_print(result)

	match Game.ConsoleTakeAction():
		"clear":
			_lines.clear()
			_refresh_output()
		"exit":
			_set_open(false)
		"restart":
			Game.RestartMission()
		"screenshot":
			_screenshot()


## 위/아래 화살표로 전에 친 명령을 불러온다. step 이 +1 이면 더 옛것, −1 이면 더 최근 것.
func _recall(step: int) -> void:
	if _history.is_empty():
		return
	_history_index = clampi(_history_index + step, -1, _history.size() - 1)
	_line = "" if _history_index < 0 else _history[_history_index]


## 콘솔과 디버그 텍스트를 숨긴 화면을 저장한다.
func _screenshot() -> void:
	_layer.visible = false
	await RenderingServer.frame_post_draw
	var path: String = Game.SaveScreenshot()
	_layer.visible = true
	_print("Screenshot failed" if path.is_empty() else "Screenshot saved: " + path)


## 콘솔에 글자를 찍는다. 줄바꿈이 있으면 여러 줄로 나눈다.
func _print(text: String) -> void:
	for line in text.split("\n"):
		_lines.append(line)
	while _lines.size() > MAX_LINES:
		_lines.pop_front()
	_refresh_output()


func _refresh_output() -> void:
	var from := maxi(0, _lines.size() - VISIBLE_LINES)
	_output.text = "\n".join(_lines.slice(from))
