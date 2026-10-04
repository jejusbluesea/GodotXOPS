class_name MenuOption
extends RefCounted
## 메인메뉴의 OPTION 화면. 섹션 바(General / Input / Graphic / Sound)에서 탭을 고르고, 탭마다 설정 줄을 세로로 늘어놓는다.
## 값은 바꾸는 즉시 ConfigManager 에 들어가 화면에 반영되고(밝기·감마, 음량), SAVE 를 눌러야 파일에 남는다.
## 창 모드·해상도·VSync 와 UIScale 은 SAVE 때 적용한다 (UIScale 을 바로 적용하면 누르고 있던 화살표가 손 밑에서 움직인다). BACK 은 저장하지 않은 변경을 되돌린다 (메뉴 쪽이 처리).
## 버튼의 색·눌림·클릭 판정은 메뉴의 것을 그대로 쓴다.

# ----- 글자와 줄 -----
const FONT := Vector2(17, 22)
const ROW_HEIGHT := 25
const NORMAL := Color(1, 1, 1)
const HOVER := Color(0, 1, 1)
const DISABLED := Color(0.6, 0.6, 0.6)
const PANEL_COLOR := Color(0, 0, 0, 0.5)

# ----- 섹션 바: 화면 왼쪽 위 기준. 타이틀(y -25, 높이 80) 아래 10 -----
const LEFT := 20
const SECTION_BAR := {"y": -115, "w": 600}
const SECTION_PER_PAGE := 3
const ARROW_PREV := "<<"
const ARROW_NEXT := ">>"

# ----- 설정 줄: 섹션 바 아래 10 에서 시작, 줄 간격 30 -----
const ROW_TOP := -150
const ROW_PITCH := 30
const GAP := 10
const LABEL_GAP := 40
const CHECK_ON := "[*]"
const CHECK_OFF := "[ ]"

# ----- 화살표를 누르고 있을 때: 이 시간 뒤부터 이 간격으로 되풀이 (초) -----
const HOLD_DELAY := 0.5
const HOLD_INTERVAL := 0.03

# ----- General -----
const UISCALE := {"label": "UIScale", "chars": 5, "step": 0.1, "format": "%.1f"}
const AIM_LABEL := "Aim"
const AIM_VALUE_CHARS := 5
const AIM_UNIT_GAP := 10
const AIM_ROWS := [
	[{"label": "Length", "key": "aimLength"}],
	[{"label": "Gap", "key": "aimGap"}, {"label": "Thick", "key": "aimThick"}],
]
const COLOR_LABEL := "Color"
const COLOR_VALUE_CHARS := 7
const COLOR_CUSTOM := "Custom"
const COLOR_PRESETS := [
	{"name": "White", "color": Color(1, 1, 1)},
	{"name": "Black", "color": Color(0, 0, 0)},
	{"name": "Red", "color": Color(1, 0, 0)},
	{"name": "Green", "color": Color(0, 1, 0)},
	{"name": "Blue", "color": Color(0, 0, 1)},
	{"name": "Cyan", "color": Color(0, 1, 1)},
	{"name": "Magenta", "color": Color(1, 0, 1)},
	{"name": "Yellow", "color": Color(1, 1, 0)},
]

# ----- Input -----
const SENSITIVITY := {"label": "Sensitivity", "chars": 6, "step": 0.01, "format": "%.2f"}
const INVERT_LABEL := "Invert Mouse"
# 바꿀 수 있는 액션 (버튼 하나짜리만). 한 줄에 둘씩 놓는다.
const BIND_ACTIONS := ["jump", "walk", "drop", "fire", "zoom", "previous", "next", "reload", "first", "second", "interact"]
const BIND_PER_ROW := 2
const BIND_LABEL_CHARS := 8
const BIND_KEY_CHARS := 7
const BIND_LABEL_GAP := 15
const BIND_UNIT_GAP := 30
const BIND_LISTENING := "[...]"
# 바인딩 경로의 마지막 토막 → 짧은 표시. 없으면 대문자로 바꿔 쓴다.
const BIND_ABBREV := {
	"leftButton": "LMB", "rightButton": "RMB", "middleButton": "MMB",
	"leftShift": "LShift", "rightShift": "RShift", "leftCtrl": "LCtrl", "rightCtrl": "RCtrl",
	"leftAlt": "LAlt", "rightAlt": "RAlt", "space": "Space", "escape": "Esc", "enter": "Enter",
	"tab": "Tab", "upArrow": "Up", "downArrow": "Down", "leftArrow": "Left", "rightArrow": "Right",
}

# ----- Graphic -----
const RESOLUTION := {"label": "Resolution", "chars": 11}
const FRAME_LIMIT := {"label": "FrameLimit", "chars": 5, "step": 1}
const BRIGHT := {"label": "Bright", "chars": 6, "step": 0.01, "format": "%.2f"}
const GAMMA := {"label": "Gamma", "chars": 5, "step": 0.1, "format": "%.1f"}
const FOV := {"label": "FOV", "chars": 4, "step": 1}

# ----- Sound -----
const MASTER_VOLUME := {"label": "MasterVolume", "chars": 6, "step": 0.01, "format": "%.2f"}

# ----- SAVE / RESET: 화면 오른쪽 아래 기준 (BACK 과 같은 여백) -----
const SAVE_TEXT := "< SAVE >"
const RESET_TEXT := "< RESET >"
const SAVE_RESET := {"x": -5, "y": 14}

var _menu: Node
var _layer: XopsLayer
var _visible := false

var _section_bar: ColorRect
var _section_prev: Dictionary
var _section_next: Dictionary
var _section_slots: Array[Dictionary] = []
var _section_names: PackedStringArray
var _section_page := 0
var _selected := ""

# 탭 이름 → 그 탭의 줄 배경들.
var _panels := {"General": [], "Input": [], "Graphic": [], "Sound": []}
# 탭 이름 → 체크박스 / 값 조절 줄 목록.
var _checks := {"General": [], "Input": [], "Graphic": [], "Sound": []}
var _steppers := {"General": [], "Input": [], "Graphic": [], "Sound": []}

var _color_prev: Dictionary
var _color_value: Dictionary
var _color_next: Dictionary
var _resolution_prev: Dictionary
var _resolution_value: Dictionary
var _resolution_next: Dictionary
var _bind_slots := {}
var _bind_listening := ""

var _save_reset_bg: ColorRect
var _save_slot: Dictionary
var _reset_slot: Dictionary


func _init(menu: Node, layer: XopsLayer) -> void:
	_menu = menu
	_layer = layer
	_build_section_bar()
	_build_general()
	_build_input()
	_build_graphic()
	_build_sound()
	_build_save_reset()
	set_visible(false)


# ============================================================
#  화면 구성
# ============================================================

func _build_section_bar() -> void:
	_section_bar = XopsUI.panel(_layer, XopsUI.TOP_LEFT, LEFT, SECTION_BAR["y"], SECTION_BAR["w"], ROW_HEIGHT, PANEL_COLOR)
	var arrow_width := ARROW_PREV.length() * FONT.x
	var slot_width: float = (SECTION_BAR["w"] - arrow_width * 2) / SECTION_PER_PAGE
	_section_prev = _menu._text_pair(_section_bar, XopsUI.TOP_LEFT, XopsUI.TOP_LEFT, 0, 0, ARROW_PREV, NORMAL, FONT,
		Vector2(arrow_width, ROW_HEIGHT))
	for i in SECTION_PER_PAGE:
		_section_slots.append(_cell(_section_bar, arrow_width + slot_width * i, slot_width, ""))
	_section_next = _menu._text_pair(_section_bar, XopsUI.TOP_RIGHT, XopsUI.TOP_RIGHT, 0, 0, ARROW_NEXT, NORMAL, FONT,
		Vector2(arrow_width, ROW_HEIGHT))

	_section_names = ConfigManager.GetSectionNames()
	_selected = _section_names[0] if _section_names.size() > 0 else ""


func _build_general() -> void:
	_checkbox("General", LEFT, _row_y(0), "ShowFPS", "General", "ShowFPS")
	_stepper("General", LEFT, _row_y(1), UISCALE, "General", "UIScale", false, LABEL_GAP)

	var header_width := AIM_LABEL.length() * FONT.x
	var header := _row_panel("General", LEFT, _row_y(3), header_width)
	_cell(header, 0, header_width, AIM_LABEL)

	var row_index := 4
	for row: Array in AIM_ROWS:
		var row_width := 0.0
		for i in row.size():
			row_width += _selector_width(row[i]["label"], AIM_VALUE_CHARS, LABEL_GAP) + (AIM_UNIT_GAP if i > 0 else 0)
		var bg := _row_panel("General", LEFT, _row_y(row_index), row_width)
		var x := 0.0
		for unit: Dictionary in row:
			var spec := {"label": unit["label"], "chars": AIM_VALUE_CHARS, "step": 1}
			_steppers["General"].append(_stepper_cells(bg, x, spec, "General", unit["key"], true, LABEL_GAP))
			x += _selector_width(unit["label"], AIM_VALUE_CHARS, LABEL_GAP) + AIM_UNIT_GAP
		if row_index == 4:
			# 길이 줄 오른쪽: 조준선을 고정할지 (끄면 조준 오차만큼 벌어진다).
			_checkbox("General", LEFT + row_width + AIM_UNIT_GAP, _row_y(row_index), "Static", "General", "StaticAim")
		row_index += 1

	var color_width := _selector_width(COLOR_LABEL, COLOR_VALUE_CHARS, LABEL_GAP)
	var color_bg := _row_panel("General", LEFT, _row_y(row_index), color_width)
	var cells := _selector_cells(color_bg, 0, COLOR_LABEL, COLOR_VALUE_CHARS, LABEL_GAP)
	_color_prev = cells["prev"]
	_color_value = cells["value"]
	_color_next = cells["next"]


func _build_input() -> void:
	_stepper("Input", LEFT, _row_y(0), SENSITIVITY, "Input", "sensitivity", false, LABEL_GAP)
	_checkbox("Input", LEFT, _row_y(1), INVERT_LABEL, "Input", "invertY")

	var label_width := BIND_LABEL_CHARS * FONT.x
	var key_width := BIND_KEY_CHARS * FONT.x
	var unit_width := label_width + BIND_LABEL_GAP + key_width
	var row_width := BIND_PER_ROW * unit_width + (BIND_PER_ROW - 1) * BIND_UNIT_GAP
	var bg: ColorRect
	for i in BIND_ACTIONS.size():
		var column := i % BIND_PER_ROW
		if column == 0:
			@warning_ignore("integer_division")
			bg = _row_panel("Input", LEFT, _row_y(2 + i / BIND_PER_ROW), row_width)
		var x := column * (unit_width + BIND_UNIT_GAP)
		_cell(bg, x, label_width, BIND_ACTIONS[i], XopsUI.MIDDLE_LEFT)
		_bind_slots[BIND_ACTIONS[i]] = _cell(bg, x + label_width + BIND_LABEL_GAP, key_width, "")


func _build_graphic() -> void:
	_checkbox("Graphic", LEFT, _row_y(0), "Fullscreen", "Graphic", "fullscreen")

	var resolution_bg := _row_panel("Graphic", LEFT, _row_y(1),
		_selector_width(RESOLUTION["label"], RESOLUTION["chars"], LABEL_GAP))
	var cells := _selector_cells(resolution_bg, 0, RESOLUTION["label"], RESOLUTION["chars"], LABEL_GAP)
	_resolution_prev = cells["prev"]
	_resolution_value = cells["value"]
	_resolution_next = cells["next"]

	_checkbox("Graphic", LEFT, _row_y(2), "VSync", "Graphic", "vsync")
	_checkbox("Graphic", LEFT + _checkbox_width("VSync") + GAP, _row_y(2), "LimitFrame", "Graphic", "limitFrame")
	_stepper("Graphic", LEFT, _row_y(3), FRAME_LIMIT, "Graphic", "frameLimit", true, LABEL_GAP)
	_stepper("Graphic", LEFT, _row_y(4), BRIGHT, "Graphic", "brightness", false, GAP)
	_stepper("Graphic", LEFT + _selector_width(BRIGHT["label"], BRIGHT["chars"], GAP) + GAP, _row_y(4),
		GAMMA, "Graphic", "gamma", false, GAP)
	_stepper("Graphic", LEFT, _row_y(5), FOV, "Graphic", "fov", true, LABEL_GAP)


func _build_sound() -> void:
	_stepper("Sound", LEFT, _row_y(0), MASTER_VOLUME, "Sound", "MasterVolume", false, LABEL_GAP)


func _build_save_reset() -> void:
	var save_width := SAVE_TEXT.length() * FONT.x
	var reset_width := RESET_TEXT.length() * FONT.x
	_save_reset_bg = XopsUI.panel(_layer, XopsUI.BOTTOM_RIGHT, SAVE_RESET["x"], SAVE_RESET["y"],
		save_width + reset_width, ROW_HEIGHT, PANEL_COLOR)
	_save_slot = _cell(_save_reset_bg, 0, save_width, SAVE_TEXT)
	_reset_slot = _cell(_save_reset_bg, save_width, reset_width, RESET_TEXT)


func _row_y(index: int) -> float:
	return ROW_TOP - index * ROW_PITCH


## 한 줄의 배경을 만들어 그 탭의 목록에 넣는다.
func _row_panel(tab: String, x: float, y: float, width: float) -> ColorRect:
	var bg := XopsUI.panel(_layer, XopsUI.TOP_LEFT, x, y, width, ROW_HEIGHT, PANEL_COLOR)
	_panels[tab].append(bg)
	return bg


## 줄 배경 안의 한 칸(왼쪽에서 x, 너비 w)에 글자를 놓는다. 판정은 그 칸 전체다.
func _cell(parent: Control, x: float, w: float, value: String, align := XopsUI.CENTER) -> Dictionary:
	var slot: Dictionary = _menu._text_pair(parent, XopsUI.TOP_LEFT, align, x + w * align.x, -ROW_HEIGHT * 0.5,
		value, NORMAL, FONT, Vector2(w, ROW_HEIGHT))
	(slot["main"] as XopsText).hit_pivot = align
	return slot


func _checkbox_width(label: String) -> float:
	return label.length() * FONT.x + LABEL_GAP + CHECK_OFF.length() * FONT.x


func _selector_width(label: String, value_chars: int, label_gap: float) -> float:
	var arrow_width := ARROW_PREV.length() * FONT.x
	return label.length() * FONT.x + label_gap + arrow_width + GAP + value_chars * FONT.x + GAP + arrow_width


## "이름 [*]" 한 줄을 만든다.
func _checkbox(tab: String, x: float, y: float, label: String, section: String, key: String) -> void:
	var label_width := label.length() * FONT.x
	var bg := _row_panel(tab, x, y, _checkbox_width(label))
	_cell(bg, 0, label_width, label)
	var slot := _cell(bg, label_width + LABEL_GAP, CHECK_OFF.length() * FONT.x, CHECK_OFF)
	_checks[tab].append({"slot": slot, "section": section, "key": key})


## "이름 << [값] >>" 의 글자 칸들을 배경 안의 x 에서부터 만든다.
func _selector_cells(bg: Control, x: float, label: String, value_chars: int, label_gap: float) -> Dictionary:
	var label_width := label.length() * FONT.x
	var arrow_width := ARROW_PREV.length() * FONT.x
	var value_width := value_chars * FONT.x
	_cell(bg, x, label_width, label)
	var cx := x + label_width + label_gap
	return {
		"prev": _cell(bg, cx, arrow_width, ARROW_PREV),
		"value": _cell(bg, cx + arrow_width + GAP, value_width, ""),
		"next": _cell(bg, cx + arrow_width + GAP + value_width + GAP, arrow_width, ARROW_NEXT),
	}


## 숫자 설정 하나를 조절하는 칸들을 만든다. spec 은 위 상수 표의 한 항목이다.
func _stepper_cells(bg: Control, x: float, spec: Dictionary, section: String, key: String, is_int: bool,
		label_gap: float) -> Dictionary:
	var stepper := _selector_cells(bg, x, spec["label"], spec["chars"], label_gap)
	stepper["section"] = section
	stepper["key"] = key
	stepper["step"] = spec["step"]
	stepper["format"] = spec.get("format", "%d")
	stepper["is_int"] = is_int
	stepper["hold_direction"] = 0
	stepper["hold_time"] = 0.0
	return stepper


## 자기 배경을 가진 숫자 설정 한 줄을 만든다.
func _stepper(tab: String, x: float, y: float, spec: Dictionary, section: String, key: String, is_int: bool,
		label_gap: float) -> void:
	var bg := _row_panel(tab, x, y, _selector_width(spec["label"], spec["chars"], label_gap))
	_steppers[tab].append(_stepper_cells(bg, 0, spec, section, key, is_int, label_gap))


# ============================================================
#  표시 갱신
# ============================================================

## 화면을 보이거나 숨긴다. 보일 때 모든 값을 다시 읽는다.
func set_visible(visible: bool) -> void:
	_visible = visible
	_section_bar.visible = visible
	_save_reset_bg.visible = visible
	_bind_listening = ""
	_show_tab()


## 탭을 고른다. 없는 이름이면 그대로 둔다.
func select_tab(tab: String) -> void:
	if tab in _section_names:
		_selected = tab
		@warning_ignore("integer_division")
		_section_page = _section_names.find(tab) / SECTION_PER_PAGE
		_show_tab()


## 고른 탭의 줄만 보이게 하고 값을 다시 읽는다.
func _show_tab() -> void:
	for tab: String in _panels:
		for bg: ColorRect in _panels[tab]:
			bg.visible = _visible and tab == _selected
	if _visible:
		_refresh_section_bar()
		refresh()


## 모든 탭의 표시를 설정 값에 맞춘다 (탭을 열 때, RESET 뒤).
func refresh() -> void:
	for tab: String in _checks:
		for check: Dictionary in _checks[tab]:
			_menu._set_slot_text(check["slot"], CHECK_ON if ConfigManager.GetBool(check["section"], check["key"], false) else CHECK_OFF)
		for stepper: Dictionary in _steppers[tab]:
			_refresh_stepper(stepper)
	_refresh_color()
	_refresh_resolution()
	for action: String in BIND_ACTIONS:
		_refresh_bind(action)


func _refresh_section_bar() -> void:
	for i in SECTION_PER_PAGE:
		var index := _section_page * SECTION_PER_PAGE + i
		_menu._set_slot_visible(_section_slots[i], index < _section_names.size())
		if index < _section_names.size():
			_menu._set_slot_text(_section_slots[i], _section_names[index])


func _stepper_value(stepper: Dictionary) -> float:
	if stepper["is_int"]:
		return ConfigManager.GetInt(stepper["section"], stepper["key"], 0)
	return ConfigManager.GetFloat(stepper["section"], stepper["key"], 0.0)


func _refresh_stepper(stepper: Dictionary) -> void:
	var value := _stepper_value(stepper)
	var shown: String = stepper["format"] % (int(value) if stepper["is_int"] else value)
	_menu._set_slot_text(stepper["value"], "[" + shown + "]")


## 지금 조준선 색과 같은 프리셋의 번호. 없으면 -1.
func _color_index() -> int:
	var current := Color(ConfigManager.GetFloat("General", "aimColorR", 1.0),
		ConfigManager.GetFloat("General", "aimColorG", 0.0), ConfigManager.GetFloat("General", "aimColorB", 0.0))
	for i in COLOR_PRESETS.size():
		if current.is_equal_approx(COLOR_PRESETS[i]["color"]):
			return i
	return -1


## 색 이름을 지금 조준선 색으로 쓴다.
func _refresh_color() -> void:
	var index := _color_index()
	_menu._set_slot_text(_color_value, COLOR_PRESETS[index]["name"] if index >= 0 else COLOR_CUSTOM)
	(_color_value["main"] as XopsText).color = Color(ConfigManager.GetFloat("General", "aimColorR", 1.0),
		ConfigManager.GetFloat("General", "aimColorG", 0.0), ConfigManager.GetFloat("General", "aimColorB", 0.0))


## 지금 해상도가 선택지 목록의 몇 번째인지. 목록에 없으면 0.
func _resolution_position() -> int:
	var current: int = ConfigManager.GetInt("Graphic", "resolution", 0)
	for i in ConfigManager.ResolutionOptionCount:
		if ConfigManager.ResolutionOptionIndexAt(i) == current:
			return i
	return 0


func _refresh_resolution() -> void:
	_menu._set_slot_text(_resolution_value, "[" + ConfigManager.ResolutionOptionLabelAt(_resolution_position()) + "]")


func _refresh_bind(action: String) -> void:
	var shown := BIND_LISTENING
	if _bind_listening != action:
		var path: String = InputManager.GetActionBinding(action)
		var key := path.get_slice("/", path.get_slice_count("/") - 1)
		shown = "[" + ("?" if key.is_empty() else BIND_ABBREV.get(key, key.to_upper())) + "]"
	_menu._set_slot_text(_bind_slots[action], shown)


# ============================================================
#  입력
# ============================================================

## 한 프레임의 입력을 처리한다. pressed / clicked / held 는 마우스 왼쪽 버튼의 눌림 / 뗌 / 유지.
func update(delta: float, pressed: bool, clicked: bool, held: bool) -> void:
	_update_section_bar(pressed, clicked, held)

	for check: Dictionary in _checks.get(_selected, []):
		if _menu._button(check["slot"], pressed, clicked, held):
			var value: bool = not ConfigManager.GetBool(check["section"], check["key"], false)
			ConfigManager.SetBool(check["section"], check["key"], value)
			_menu._set_slot_text(check["slot"], CHECK_ON if value else CHECK_OFF)
	for stepper: Dictionary in _steppers.get(_selected, []):
		_update_stepper(stepper, delta, pressed, held)

	match _selected:
		"General":
			_update_color(pressed, clicked, held)
		"Input":
			_update_binds(pressed, clicked)
		"Graphic":
			_update_resolution(pressed, clicked, held)

	if _menu._button(_save_slot, pressed, clicked, held):
		ConfigManager.Save()
		ConfigManager.ApplyGraphic()
		_menu._apply_ui_scale()
	if _menu._button(_reset_slot, pressed, clicked, held):
		ConfigManager.ResetToDefaults()
		refresh()


func _update_section_bar(pressed: bool, clicked: bool, held: bool) -> void:
	var count := _section_names.size()
	var page_count := maxi(1, ceili(float(count) / SECTION_PER_PAGE))
	var direction := _arrows(_section_prev, _section_next, _section_page > 0, _section_page < page_count - 1, pressed, clicked, held)

	for i in SECTION_PER_PAGE:
		var index := _section_page * SECTION_PER_PAGE + i
		if index >= count:
			continue
		# 고른 섹션은 회색으로 두고 누를 수 없다.
		if _menu._button(_section_slots[i], pressed, clicked, held, _section_names[index] == _selected):
			_selected = _section_names[index]
			_bind_listening = ""
			_show_tab()

	if direction != 0:
		_section_page += direction
		_refresh_section_bar()


## << >> 한 쌍을 처리하고 눌린 방향(-1 / 0 / 1)을 돌려준다.
func _arrows(prev: Dictionary, next: Dictionary, can_prev: bool, can_next: bool, pressed: bool, clicked: bool, held: bool) -> int:
	var direction := 0
	if _menu._button(prev, pressed, clicked, held, not can_prev):
		direction = -1
	if _menu._button(next, pressed, clicked, held, not can_next):
		direction = 1
	return direction


## 숫자 설정의 화살표. 누르면 바로 한 칸, 누르고 있으면 HOLD_DELAY 뒤부터 HOLD_INTERVAL 마다 한 칸씩 바뀐다.
## 범위는 매번 다시 읽는다 (UIScale 의 상한이 해상도를 따라 바뀐다).
func _update_stepper(stepper: Dictionary, delta: float, pressed: bool, held: bool) -> void:
	var value := _stepper_value(stepper)
	var half_step: float = stepper["step"] * 0.5
	var at_min: bool = value <= ConfigManager.GetMin(stepper["section"], stepper["key"]) + half_step
	var at_max: bool = value >= ConfigManager.GetMax(stepper["section"], stepper["key"]) - half_step

	var prev: XopsText = stepper["prev"]["main"]
	var next: XopsText = stepper["next"]["main"]
	var prev_hovered := prev.is_hovered()
	var next_hovered := next.is_hovered()
	var prev_owned: bool = _menu._owns_press(prev, prev_hovered, pressed)
	var next_owned: bool = _menu._owns_press(next, next_hovered, pressed)
	prev.color = DISABLED if at_min else (HOVER if prev_hovered else NORMAL)
	next.color = DISABLED if at_max else (HOVER if next_hovered else NORMAL)
	_menu._set_pressed(stepper["prev"], prev_hovered and held and prev_owned and not at_min)
	_menu._set_pressed(stepper["next"], next_hovered and held and next_owned and not at_max)

	var direction := 0
	if held and prev_hovered and prev_owned and not at_min:
		direction = -1
	elif held and next_hovered and next_owned and not at_max:
		direction = 1

	if direction != stepper["hold_direction"]:
		stepper["hold_direction"] = direction
		stepper["hold_time"] = 0.0
		if direction != 0:
			_step(stepper, direction)
	elif direction != 0:
		stepper["hold_time"] += delta
		while stepper["hold_time"] >= HOLD_DELAY:
			_step(stepper, direction)
			stepper["hold_time"] -= HOLD_INTERVAL


func _step(stepper: Dictionary, direction: int) -> void:
	var step: float = stepper["step"]
	if stepper["is_int"]:
		ConfigManager.SetInt(stepper["section"], stepper["key"], int(_stepper_value(stepper)) + direction * int(step))
	else:
		ConfigManager.SetFloat(stepper["section"], stepper["key"], snappedf(_stepper_value(stepper) + direction * step, step))
	_refresh_stepper(stepper)


func _update_color(pressed: bool, clicked: bool, held: bool) -> void:
	var index := _color_index()
	var direction := _arrows(_color_prev, _color_next, index > 0, index < COLOR_PRESETS.size() - 1, pressed, clicked, held)
	if direction == 0:
		return
	# 프리셋에 없는 색에서 >> 를 누르면 첫 프리셋으로 간다.
	var preset: Color = COLOR_PRESETS[0 if index < 0 else index + direction]["color"]
	ConfigManager.SetFloat("General", "aimColorR", preset.r)
	ConfigManager.SetFloat("General", "aimColorG", preset.g)
	ConfigManager.SetFloat("General", "aimColorB", preset.b)
	ConfigManager.SetFloat("General", "aimColorA", 1.0)
	_refresh_color()


func _update_resolution(pressed: bool, clicked: bool, held: bool) -> void:
	var position := _resolution_position()
	var count: int = ConfigManager.ResolutionOptionCount
	var direction := _arrows(_resolution_prev, _resolution_next, position > 0, position < count - 1, pressed, clicked, held)
	if direction != 0:
		ConfigManager.SetInt("Graphic", "resolution", ConfigManager.ResolutionOptionIndexAt(position + direction))
		_refresh_resolution()


## 키 바인딩: 칸을 클릭하면 기다리는 상태가 되고, 그 뒤 처음 눌린 키나 마우스 버튼으로 바꾼다.
func _update_binds(pressed: bool, clicked: bool) -> void:
	var captured := false
	if not _bind_listening.is_empty():
		var path: String = InputManager.GetFirstPressedKeyPath()
		if not path.is_empty():
			InputManager.SetActionBinding(_bind_listening, path)
			var done := _bind_listening
			_bind_listening = ""
			_refresh_bind(done)
			captured = true

	for action: String in BIND_ACTIONS:
		var main: XopsText = _bind_slots[action]["main"]
		var hovered := main.is_hovered()
		var owned: bool = _menu._owns_press(main, hovered, pressed)
		main.color = DISABLED if _bind_listening == action else (HOVER if hovered else NORMAL)
		if clicked and _bind_listening.is_empty() and hovered and owned:
			_bind_listening = action
			_refresh_bind(action)

	# 마우스 버튼으로 바꾼 경우, 그 누름을 뗄 때 같은 칸이 다시 기다리는 상태로 들어가지 않게 누름을 버린다.
	if captured:
		_menu._press_capture = null
