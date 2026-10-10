extends Node
## 메인메뉴. 데모 맵을 배경으로 미션 목록(공식 / 어드온)을 보여 주고, 고르면 브리핑으로 넘어간다.
## 왼쪽 아래에 OPTION, CREDIT, EXIT 버튼이 있고, ESC 는 종료 확인을 띄운다. OPTION 화면은 MenuOption 이 만든다.
## 클릭은 발사 키 바인딩과 무관하게 마우스 왼쪽 버튼이다.
## 버튼은 누른 자리에서 뗐을 때만 동작한다 (누른 채 벗어나면 취소).

const SCREEN_NAME := "mainmenu"
const BRIEFING_SCENE := "briefing"
# 화면 스크립트가 한 번에 물을 수 있는 미션 이름의 수.
const SCRIPT_MISSION_BATCH := 64

# ----- 층 (클수록 위) -----
const TITLE_ORDER := 1
const VERSION_ORDER := 2
const SCROLL_ORDER := 3
const MISSION_ORDER := 4
const MENU_ORDER := 5
const POINTER_ORDER := 9
const FADE_ORDER := 10

# ----- 타이틀 이미지: 화면 왼쪽 위 기준 -----
const TITLE := {"path": "data/title.dds", "x": 20, "y": -25, "w": 480, "h": 80}

# ----- 배경 카메라: 플레이어를 고정 오프셋·고정 각도로 따라간다 (원본 gamemain.cpp:760-767) -----
const CAM_OFFSET := Vector3(0.4, 2.2, 1.2)
const CAM_EULER := Vector3(25.0, 225.0, 0.0)
const CAM_FOV := 65.0

# ----- 들어올 때의 암전 해제와 입력 허용 시각 (초) -----
const FADE_TIME := 2.0
const CLICK_ALLOW_TIME := 0.2

# ----- 십자 커서 -----
const POINTER_COLOR := Color(1, 0, 0, 0.5)

# ----- 버전: 화면 오른쪽 위 기준 -----
const VERSION := {"x": -10, "y": -75, "w": 16, "h": 19, "color": Color(1, 1, 1), "shadow": Color(0, 0, 0)}

# ----- 스크롤바: 화면 오른쪽 아래 기준 -----
const SCROLL_TRACK := {"x": 0, "y": 39, "w": 20, "h": 300, "color": Color(0.5, 0.5, 0.5, 0.5)}
const SCROLL_BORDER := 3
const SCROLL_OUTLINE := {
	"normal": Color(0.6, 0.6, 0.251), "hover": Color(0.4, 0.6705, 0.5686), "pressed": Color(0.6, 0.3019, 0.2509),
}
const SCROLL_INNER := {
	"normal": Color(0.8, 0.8, 0.2509), "hover": Color(0.3803, 0.7686, 0.6392), "pressed": Color(0.8, 0.3019, 0.2509),
}

# ----- 미션 목록: 스크롤바 왼쪽. UP + 8칸 + DOWN = 10줄 -----
const MISSION_RECT := {"x": -20, "y": 39, "w": 340, "h": 300, "color": Color(0, 0, 0, 0.5)}
const ITEM_COUNT := 8
const ITEM_SPACING := 30
const ITEM_FONT := Vector2(20, 26)
const BUTTON_FONT := Vector2(25, 26)
const UP_TEXT := "<  UP  >"
const DOWN_TEXT := "< DOWN >"
const SHADOW_COLOR := Color(0, 0, 0)
const BUTTON_NORMAL := Color(1, 1, 1)
const BUTTON_HOVER := Color(0, 1, 1)
const BUTTON_DISABLED := Color(0.6, 0.6, 0.6)
const ITEM_NORMAL := Color(0.6, 0.6, 1)
const ITEM_HOVER := Color(1, 0.6, 0.6)

# ----- 공식 / 어드온 전환: 미션 목록 아래 -----
const SWITCH_RECT := {"x": 0, "y": 14, "w": 360, "h": 25, "color": Color(0, 0, 0, 0.5)}
const SWITCH_TO_ADDON := "ADD-ON MISSIONS >>"
const SWITCH_TO_OFFICIAL := "<< STANDARD MISSIONS"
const SWITCH_FONT := Vector2(17, 22)

# ----- 어드온 페이지 전환: 미션 목록 위. 어드온 탭이고 페이지가 둘 이상일 때만 보인다 -----
const PAGE_RECT := {"x": 0, "y": 339, "w": 360, "h": 25, "color": Color(0, 0, 0, 0.5)}
const PAGE_PREV := "<<"
const PAGE_NEXT := ">>"
const PAGE_ARROW_HIT := 60

# ----- 왼쪽 아래 버튼 -----
const MENU_ROW_HEIGHT := 25
const MENU_ITEMS := ["< OPTION >", "< CREDIT >", "<  EXIT  >"]
const MENU_SCREENS := ["option", "credit", "exit"]
const MENU_BG := {"x": 5, "y": 14, "w": 170, "color": Color(0, 0, 0, 0.5)}
const BACK_TEXT := "< BACK >"
const BACK_BG := {"x": 5, "y": 14, "w": 136, "color": Color(0, 0, 0, 0.5)}

# ----- CREDIT 창: 화면 가장자리에서의 여백과 글자 크기 범위 -----
const CREDIT_INSET := {"left": 152, "top": 109, "right": 14, "bottom": 14}
const CREDIT_BG := Color(0, 0, 0, 0.5)
const CREDIT_FONT_MIN := 1
const CREDIT_FONT_MAX := 80
const CREDIT_MARGIN := 8

# ----- 종료 확인 -----
const EXIT_QUESTION := "Do you want to quit the game?"
const EXIT_YES := "< EXIT >"
const EXIT_NO := "< ABORT >"
const EXIT_PANEL := {"font": Vector2(14, 19), "h": 130, "color": Color(0, 0, 0, 0.5)}
const EXIT_BUTTON_GAP := 20

# ----- 미션 로드 실패 문구 -----
# 미션을 눌렀는데 로드에 실패했을 때 타이틀 아래에 띠로 잠깐 띄운다 (원본의 "block data open failed" 에 해당). 원본에 없는 배치라 값은 임의로 정했다.
# y 는 화면 위에서 띠의 윗변까지의 거리(아래쪽이 −), show 는 보이는 시간, fade 는 사라지는 데 걸리는 시간 (초).
const LOAD_ERROR := {"order": 8, "font": 13, "y": -112, "h": 22, "color": Color(1.0, 0.35, 0.35), "background": Color(0, 0, 0, 0.7), "show": 5.0, "fade": 0.5}

# 화면을 다녀와도 유지되는 메뉴 상태.
static var s_is_addon := false
static var s_page := 0
static var s_official_scroll := 0
static var s_addon_scroll := 0

var _time := 0.0
var _left := false
var _load_error_panel: ColorRect
var _load_error: Label
var _load_error_time := 0.0
var _screen := "main"
var _is_addon := false
var _addon_exists := false
var _page := 0
var _scroll_index := 0
var _scrollable := false
var _grab_offset := 0.0

var _layers: Array[XopsLayer] = []
var _pointer_layer: XopsLayer
var _fade: ColorRect
var _pointer_h: ColorRect
var _pointer_v: ColorRect
var _track: ColorRect
var _thumb: ColorRect
var _thumb_inner: ColorRect
var _mission_rect: ColorRect
var _switch_bg: ColorRect
var _page_bg: ColorRect
var _menu_bg: ColorRect
var _back_bg: ColorRect
var _credit_panel: ColorRect
var _credit_label: Label
var _exit_panel: ColorRect
# OPTION 화면. 메뉴와는 따로 도는 덮개 화면이고 (기본 화면이든 화면 스크립트든), 열려 있는 동안 메뉴는 입력을 받지 않는다.
var _option: XopsOption
var _buttons := XopsButtons.new()
var _ui: CanvasLayer
# 이 화면을 맡은 화면 스크립트 (godotdata/ui 에 등록된 .sgd). 없으면 아래의 기본 화면을 그린다.
var _script: XopsScriptScreen

# 버튼 하나 = {"shadow": XopsText, "main": XopsText, "x": 기준 x, "y": 기준 y}.
var _up_slot: Dictionary
var _down_slot: Dictionary
var _item_slots: Array[Dictionary] = []
var _switch_slot: Dictionary
var _page_prev_slot: Dictionary
var _page_next_slot: Dictionary
var _page_name_slot: Dictionary
var _menu_slots: Array[Dictionary] = []
var _back_slot: Dictionary
var _exit_yes_slot: Dictionary
var _exit_no_slot: Dictionary


func _ready() -> void:
	InputManager.MouseCursorMode(true, false, true)
	Game.LoadDemo()

	_addon_exists = Game.AddonPageCount() > 1 or Game.AddonMissionCount(0) > 0
	_is_addon = s_is_addon and _addon_exists
	_page = clampi(s_page, 0, maxi(0, Game.AddonPageCount() - 1))
	_scroll_index = clampi(s_addon_scroll if _is_addon else s_official_scroll, 0, _max_index())

	_ui = CanvasLayer.new()
	add_child(_ui)

	var state: String = Dev.value("--ui-state", "")
	_script = XopsScriptScreen.start(_ui, SCREEN_NAME, _script_api(), {
		"version": Game.Version(), "credit": Game.CreditText(), "state": state,
		"official_count": Game.OfficialMissionCount(), "addon_pages": Game.AddonPageCount(), "addon_exists": _addon_exists,
	})
	_option = XopsOption.new(_ui, MENU_ORDER, Callable(self, "_apply_ui_scale"))
	if _script == null:
		_build_default()
	elif state.begins_with("option"):
		_open_option(state)


func _exit_tree() -> void:
	if _option != null:
		_option.stop()
	if _script != null:
		_script.stop()
		_script = null


## OPTION 화면을 연다. state 가 "option-탭" 이면 그 탭에서 시작한다 (개발용 시작 상태).
func _open_option(state := "") -> void:
	_screen = "option"
	_option.open(state.trim_prefix("option-").capitalize() if state.begins_with("option-") else "")


## 기본 화면을 만든다. 화면 스크립트가 없거나 실패했을 때 쓴다.
func _build_default() -> void:
	var ui := _ui
	var title_layer := _layer(ui, TITLE_ORDER)
	XopsUI.image(title_layer, XopsUI.TOP_LEFT, Game.LoadTexture(TITLE["path"]), TITLE["x"], TITLE["y"], TITLE["w"], TITLE["h"])

	var version_layer := _layer(ui, VERSION_ORDER)
	var version: String = Game.Version()
	XopsUI.text(version_layer, XopsUI.TOP_RIGHT, XopsUI.TOP_RIGHT, version,
		VERSION["x"] + 1, VERSION["y"] - 1, VERSION["w"], VERSION["h"], VERSION["shadow"])
	XopsUI.text(version_layer, XopsUI.TOP_RIGHT, XopsUI.TOP_RIGHT, version,
		VERSION["x"], VERSION["y"], VERSION["w"], VERSION["h"], VERSION["color"])

	_build_scroll(_layer(ui, SCROLL_ORDER))
	_build_mission_list(_layer(ui, MISSION_ORDER))
	_build_menu(_layer(ui, MENU_ORDER))

	_pointer_layer = XopsUI.layer(ui, POINTER_ORDER, true)
	_pointer_h = XopsUI.panel_stretch(_pointer_layer, XopsUI.Stretch.TOP, 0, 0, 0, 1, POINTER_COLOR)
	_pointer_v = XopsUI.panel_stretch(_pointer_layer, XopsUI.Stretch.LEFT, 0, 0, 1, 0, POINTER_COLOR)

	var error_layer := XopsUI.layer(ui, LOAD_ERROR["order"], true)
	_load_error_panel = XopsUI.panel_stretch(error_layer, XopsUI.Stretch.TOP, 0, LOAD_ERROR["y"], 0, LOAD_ERROR["h"], LOAD_ERROR["background"])
	_load_error = XopsUI.label(_load_error_panel, "", LOAD_ERROR["font"], LOAD_ERROR["color"])
	_load_error.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_load_error.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_load_error.clip_text = true
	_load_error.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_load_error_panel.visible = false

	var fade_layer := XopsUI.layer(ui, FADE_ORDER, true)
	_fade = XopsUI.panel_stretch(fade_layer, XopsUI.Stretch.FULL, 0, 0, 0, 0, Color.BLACK)

	_refresh_items()
	_update_scroll_thumb()
	_update_switch_text()
	var state: String = Dev.value("--ui-state", "")
	_set_screen(state if state in ["credit", "exit"] else "main")
	if state.begins_with("option"):
		_set_screen("option")
		_open_option(state)
	if state == "addon" and _addon_exists:
		_switch_tab(true)


func _process(delta: float) -> void:
	if _left:
		return

	if _script != null:
		if _process_script(delta):
			return

	_time += delta

	if Game.PlayerExists():
		Game.SetSceneCamera(Game.PlayerPosition() + CAM_OFFSET, CAM_EULER, CAM_FOV)

	_fade.color.a = maxf(0.0, 1.0 - _time / FADE_TIME)
	_update_load_error(delta)

	var mouse := _pointer_layer.get_local_mouse_position()
	_pointer_h.position.y = mouse.y
	_pointer_v.position.x = mouse.x

	# OPTION 이 열려 있는 동안에는 그 화면이 입력을 받는다. 닫히면 미션 목록으로 돌아온다.
	if _screen == "option":
		_option.update(delta)
		if not _option.is_open():
			_set_screen("main")
		return

	var allowed := _time >= CLICK_ALLOW_TIME
	var pressed: bool = allowed and InputManager.WasClickPressed()
	var clicked: bool = allowed and InputManager.WasClickReleased()
	var held: bool = InputManager.IsClickPressed()
	var escape: bool = allowed and InputManager.WasPressed("escape")
	if pressed:
		_buttons.press_capture = null

	match _screen:
		"main":
			_update_main(pressed, clicked, held, escape)
		"exit":
			_update_exit(pressed, clicked, held, escape)
		_:
			if _button(_back_slot, pressed, clicked, held) or escape:
				_set_screen("main")


# ============================================================
#  화면 구성
# ============================================================

func _layer(parent: Node, order: int) -> XopsLayer:
	var node := XopsUI.layer(parent, order, false)
	_layers.append(node)
	return node


func _build_scroll(layer: XopsLayer) -> void:
	_track = XopsUI.panel(layer, XopsUI.BOTTOM_RIGHT, SCROLL_TRACK["x"], SCROLL_TRACK["y"],
		SCROLL_TRACK["w"], SCROLL_TRACK["h"], SCROLL_TRACK["color"])

	_thumb = ColorRect.new()
	_thumb.color = SCROLL_OUTLINE["normal"]
	_thumb.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_track.add_child(_thumb)

	_thumb_inner = XopsUI.panel_stretch(_thumb, XopsUI.Stretch.FULL, 0, 0,
		-2 * SCROLL_BORDER, -2 * SCROLL_BORDER, SCROLL_INNER["normal"])


func _build_mission_list(layer: XopsLayer) -> void:
	_mission_rect = XopsUI.panel(layer, XopsUI.BOTTOM_RIGHT, MISSION_RECT["x"], MISSION_RECT["y"],
		MISSION_RECT["w"], MISSION_RECT["h"], MISSION_RECT["color"])

	var row := Vector2(MISSION_RECT["w"], ITEM_SPACING)
	_up_slot = _text_pair(_mission_rect, XopsUI.TOP_LEFT, XopsUI.TOP_LEFT, 0, 0, UP_TEXT, BUTTON_NORMAL, BUTTON_FONT, row)
	for i in ITEM_COUNT:
		_item_slots.append(_text_pair(_mission_rect, XopsUI.TOP_LEFT, XopsUI.TOP_LEFT,
			0, -(i + 1) * ITEM_SPACING, "", ITEM_NORMAL, ITEM_FONT, row))
	_down_slot = _text_pair(_mission_rect, XopsUI.TOP_LEFT, XopsUI.TOP_LEFT,
		0, -(ITEM_COUNT + 1) * ITEM_SPACING, DOWN_TEXT, BUTTON_NORMAL, BUTTON_FONT, row)

	_switch_bg = XopsUI.panel(layer, XopsUI.BOTTOM_RIGHT, SWITCH_RECT["x"], SWITCH_RECT["y"],
		SWITCH_RECT["w"], SWITCH_RECT["h"], SWITCH_RECT["color"])
	_switch_slot = _text_pair(_switch_bg, XopsUI.TOP_LEFT, XopsUI.TOP_LEFT, 0, 0, "", BUTTON_NORMAL, SWITCH_FONT,
		Vector2(SWITCH_RECT["w"], SWITCH_RECT["h"]))

	_page_bg = XopsUI.panel(layer, XopsUI.BOTTOM_RIGHT, PAGE_RECT["x"], PAGE_RECT["y"],
		PAGE_RECT["w"], PAGE_RECT["h"], PAGE_RECT["color"])
	var arrow_hit := Vector2(PAGE_ARROW_HIT, PAGE_RECT["h"])
	_page_prev_slot = _text_pair(_page_bg, XopsUI.MIDDLE_LEFT, XopsUI.MIDDLE_LEFT, 0, 0, PAGE_PREV, BUTTON_NORMAL, SWITCH_FONT, arrow_hit)
	_page_next_slot = _text_pair(_page_bg, XopsUI.MIDDLE_RIGHT, XopsUI.MIDDLE_RIGHT, 0, 0, PAGE_NEXT, BUTTON_NORMAL, SWITCH_FONT, arrow_hit)
	_page_name_slot = _text_pair(_page_bg, XopsUI.CENTER, XopsUI.CENTER, 0, 0, "", BUTTON_NORMAL, SWITCH_FONT,
		Vector2(PAGE_RECT["w"], PAGE_RECT["h"]))


func _build_menu(layer: XopsLayer) -> void:
	var count := MENU_ITEMS.size()
	_menu_bg = XopsUI.panel(layer, XopsUI.BOTTOM_LEFT, MENU_BG["x"], MENU_BG["y"],
		MENU_BG["w"], MENU_ROW_HEIGHT * count, MENU_BG["color"])
	var row := Vector2(MENU_BG["w"], MENU_ROW_HEIGHT)
	for i in count:
		# 기준점이 왼쪽 아래라 위쪽 줄일수록 y 가 크다. 글자는 줄의 세로 가운데에 놓는다.
		var y := (count - 1 - i) * MENU_ROW_HEIGHT
		_menu_slots.append(_row_button(_menu_bg, y, MENU_ITEMS[i], row))

	_build_back(layer)

	var inset := CREDIT_INSET
	_credit_panel = XopsUI.panel_stretch(layer, XopsUI.Stretch.FULL,
		(inset["left"] - inset["right"]) / 2.0, (inset["bottom"] - inset["top"]) / 2.0,
		-(inset["left"] + inset["right"]), -(inset["top"] + inset["bottom"]), CREDIT_BG)
	_credit_label = XopsUI.label(_credit_panel, Game.CreditText(), CREDIT_FONT_MAX, Color.WHITE)
	_credit_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_credit_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_credit_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	var question_width := EXIT_QUESTION.length() * (EXIT_PANEL["font"] as Vector2).x
	_exit_panel = XopsUI.panel(layer, XopsUI.CENTER, 0, 0, question_width + 40, EXIT_PANEL["h"], EXIT_PANEL["color"])
	_text_pair(_exit_panel, XopsUI.CENTER, XopsUI.CENTER, 0, EXIT_PANEL["h"] * 0.28, EXIT_QUESTION,
		BUTTON_NORMAL, EXIT_PANEL["font"], Vector2(question_width, MENU_ROW_HEIGHT))

	var yes_width := EXIT_YES.length() * SWITCH_FONT.x
	var no_width := EXIT_NO.length() * SWITCH_FONT.x
	var button_y: float = -EXIT_PANEL["h"] * 0.28
	_exit_yes_slot = _text_pair(_exit_panel, XopsUI.CENTER, XopsUI.CENTER,
		-(yes_width * 0.5 + EXIT_BUTTON_GAP * 0.5), button_y, EXIT_YES, BUTTON_NORMAL, SWITCH_FONT, Vector2(yes_width, MENU_ROW_HEIGHT))
	_exit_no_slot = _text_pair(_exit_panel, XopsUI.CENTER, XopsUI.CENTER,
		no_width * 0.5 + EXIT_BUTTON_GAP * 0.5, button_y, EXIT_NO, BUTTON_NORMAL, SWITCH_FONT, Vector2(no_width, MENU_ROW_HEIGHT))


## BACK 버튼 (CREDIT 과 OPTION 에서 쓴다).
func _build_back(layer: XopsLayer) -> void:
	_back_bg = XopsUI.panel(layer, XopsUI.BOTTOM_LEFT, BACK_BG["x"], BACK_BG["y"],
		BACK_BG["w"], MENU_ROW_HEIGHT, BACK_BG["color"])
	_back_slot = _row_button(_back_bg, 0, BACK_TEXT, Vector2(BACK_BG["w"], MENU_ROW_HEIGHT))


func _text_pair(parent: Control, pivot: Vector2, align: Vector2, x: float, y: float, value: String,
		color: Color, font: Vector2, hit: Vector2) -> Dictionary:
	return _buttons.text_pair(parent, pivot, align, x, y, value, color, font, hit)


func _row_button(parent: Control, y: float, value: String, row: Vector2) -> Dictionary:
	return _buttons.row_button(parent, y, value, row, SWITCH_FONT)


func _set_slot_text(slot: Dictionary, value: String) -> void:
	_buttons.set_text(slot, value)


func _set_slot_visible(slot: Dictionary, visible: bool) -> void:
	_buttons.set_visible(slot, visible)


# ============================================================
#  상태 갱신
# ============================================================

func _current_count() -> int:
	return Game.AddonMissionCount(_page) if _is_addon else Game.OfficialMissionCount()


func _current_name(index: int) -> String:
	return Game.AddonMissionName(_page, index) if _is_addon else Game.OfficialMissionName(index)


func _max_index() -> int:
	return maxi(0, _current_count() - ITEM_COUNT)


func _multiple_pages() -> bool:
	return Game.AddonPageCount() > 1


func _refresh_items() -> void:
	var count := _current_count()
	for i in ITEM_COUNT:
		var index := _scroll_index + i
		_set_slot_visible(_item_slots[i], index < count)
		if index < count:
			_set_slot_text(_item_slots[i], _current_name(index))


## 스크롤 손잡이의 높이와 위치를 미션 수와 스크롤 위치에 맞춘다. 스크롤이 필요 없으면 숨기고 홈을 목록 색으로 칠한다.
func _update_scroll_thumb() -> void:
	var count := _current_count()
	_scrollable = count > ITEM_COUNT
	_thumb.visible = _scrollable
	if not _scrollable:
		_track.color = MISSION_RECT["color"]
		return

	var bar_height: float = SCROLL_TRACK["h"] * float(ITEM_COUNT) / count
	var track_range: float = SCROLL_TRACK["h"] - bar_height
	_thumb.position = Vector2(0, track_range * _scroll_index / _max_index())
	_thumb.size = Vector2(SCROLL_TRACK["w"], bar_height)
	_track.color = SCROLL_TRACK["color"]


func _update_switch_text() -> void:
	_set_slot_text(_switch_slot, SWITCH_TO_OFFICIAL if _is_addon else SWITCH_TO_ADDON)


func _save_scroll() -> void:
	s_is_addon = _is_addon
	s_page = _page
	if _is_addon:
		s_addon_scroll = _scroll_index
	else:
		s_official_scroll = _scroll_index


func _switch_tab(to_addon: bool) -> void:
	_save_scroll()
	_is_addon = to_addon
	s_is_addon = to_addon
	_scroll_index = clampi(s_addon_scroll if _is_addon else s_official_scroll, 0, _max_index())
	_refresh_items()
	_update_scroll_thumb()
	_update_switch_text()
	_set_page_bar_visible(_is_addon and _multiple_pages())


func _set_page_bar_visible(visible: bool) -> void:
	_page_bg.visible = visible
	if visible:
		_set_slot_text(_page_name_slot, Game.AddonPageName(_page))


func _set_screen(screen: String) -> void:
	_screen = screen
	var main := screen == "main"
	_mission_rect.visible = main
	_track.visible = main
	_switch_bg.visible = main and _addon_exists
	_menu_bg.visible = main
	_back_bg.visible = screen == "credit"
	_credit_panel.visible = screen == "credit"
	_exit_panel.visible = screen == "exit"
	_set_page_bar_visible(main and _is_addon and _multiple_pages())
	if screen == "credit":
		_fit_credit_text.call_deferred()


## 설정의 UIScale 을 메뉴의 층들에 다시 적용한다 (OPTION 에서 SAVE 할 때와 닫으며 되돌릴 때 XopsOption 이 부른다).
func _apply_ui_scale() -> void:
	var ui_scale: float = ConfigManager.GetFloat("General", "UIScale", 1.0)
	for layer in _layers:
		layer.ui_scale = ui_scale
	if _script != null:
		_script.apply_ui_scale()


## 크레딧 글자 크기를 창 안에 들어가는 가장 큰 값으로 맞춘다.
func _fit_credit_text() -> void:
	var available := _credit_panel.size - Vector2(CREDIT_MARGIN, CREDIT_MARGIN) * 2.0
	var reference := 16
	var measured := XopsUI.os_font().get_multiline_string_size(_credit_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, reference)
	if measured.x <= 0.0 or measured.y <= 0.0:
		return
	var fit := clampi(int(minf(available.x / measured.x, available.y / measured.y) * reference), CREDIT_FONT_MIN, CREDIT_FONT_MAX)

	# 글상자의 실제 줄 높이는 위에서 잰 것보다 조금 클 수 있다. 넘치면 들어갈 때까지 줄인다.
	_credit_label.add_theme_font_size_override("font_size", fit)
	while fit > CREDIT_FONT_MIN and (_credit_label.get_minimum_size().y > available.y or _credit_label.get_minimum_size().x > available.x):
		fit -= 1
		_credit_label.add_theme_font_size_override("font_size", fit)


# ============================================================
#  입력
# ============================================================

func _owns_press(id: Object, hovered: bool, pressed: bool) -> bool:
	return _buttons.owns_press(id, hovered, pressed)


func _button(slot: Dictionary, pressed: bool, clicked: bool, held: bool, disabled := false) -> bool:
	return _buttons.button(slot, pressed, clicked, held, disabled)


func _set_pressed(slot: Dictionary, pressed: bool) -> void:
	_buttons.set_pressed(slot, pressed)


func _update_main(pressed: bool, clicked: bool, held: bool, escape: bool) -> void:
	if escape:
		_set_screen("exit")
		return

	var count := _current_count()
	var max_index := _max_index()

	# 스크롤바 끌기. 다른 요소보다 먼저 처리해야 끄는 동안 다른 요소가 반응하지 않는다.
	if _scrollable:
		var bar_height: float = SCROLL_TRACK["h"] * float(ITEM_COUNT) / count
		var track_range: float = SCROLL_TRACK["h"] - bar_height
		var from_top := _track.get_local_mouse_position().y
		if pressed and XopsUI.hovered(_track):
			_grab_offset = clampf(from_top - track_range * _scroll_index / max_index, 0.0, bar_height)
			_buttons.dragging = true
			_buttons.press_capture = _track
		if _buttons.dragging:
			if held:
				var ratio := clampf((from_top - _grab_offset) / track_range, 0.0, 1.0)
				_thumb.position.y = track_range * ratio
				var index := int(floor(ratio * max_index))
				if index != _scroll_index:
					_scroll_index = index
					_refresh_items()
			else:
				_buttons.dragging = false
				_update_scroll_thumb()

	var scrolled := false
	if _button(_up_slot, pressed, clicked, held, _scroll_index <= 0):
		_scroll_index -= 1
		scrolled = true
	if _button(_down_slot, pressed, clicked, held, _scroll_index >= max_index):
		_scroll_index += 1
		scrolled = true
	if scrolled:
		_refresh_items()
		_update_scroll_thumb()

	if _addon_exists and _button(_switch_slot, pressed, clicked, held):
		_switch_tab(not _is_addon)
		return

	if _is_addon and _multiple_pages():
		var page_count: int = Game.AddonPageCount()
		var direction := 0
		if _button(_page_prev_slot, pressed, clicked, held, _page <= 0):
			direction = -1
		if _button(_page_next_slot, pressed, clicked, held, _page >= page_count - 1):
			direction = 1
		if direction != 0:
			_page += direction
			_scroll_index = 0
			_save_scroll()
			_refresh_items()
			_update_scroll_thumb()
			_set_page_bar_visible(true)

	for i in _menu_slots.size():
		if _button(_menu_slots[i], pressed, clicked, held):
			_set_screen(MENU_SCREENS[i])
			if MENU_SCREENS[i] == "option":
				_open_option()
			return

	for i in ITEM_COUNT:
		if _scroll_index + i >= count:
			continue
		var slot := _item_slots[i]
		var main: XopsText = slot["main"]
		var hovered := not _buttons.dragging and main.is_hovered()
		var owned := _owns_press(main, hovered, pressed)
		main.color = ITEM_HOVER if hovered else ITEM_NORMAL
		_set_pressed(slot, hovered and held and owned)
		if clicked and hovered and owned:
			_load_mission(_scroll_index + i)
			return

	if _scrollable:
		var state := "pressed" if _buttons.dragging else ("hover" if XopsUI.hovered(_thumb) else "normal")
		_thumb.color = SCROLL_OUTLINE[state]
		_thumb_inner.color = SCROLL_INNER[state]


func _update_exit(pressed: bool, clicked: bool, held: bool, escape: bool) -> void:
	if _button(_exit_yes_slot, pressed, clicked, held):
		_left = true
		Game.Quit()
		return
	if _button(_exit_no_slot, pressed, clicked, held) or escape:
		_set_screen("main")


func _load_mission(index: int) -> void:
	_save_scroll()
	if Game.LoadMission(index, _is_addon, _page):
		_left = true
		Game.ChangeScene(BRIEFING_SCENE)
	else:
		# 로드에 실패하면 배경 맵이 내려가 있으므로 다시 올린다. 이유는 데모를 올리기 전에 읽어 둔다.
		var reason: String = Game.LastLoadError()
		Game.LoadDemo()
		_load_error.text = reason
		_load_error_panel.visible = true
		_load_error_time = LOAD_ERROR["show"]


## 미션 로드 실패 문구를 시간이 지나면 흐리게 해서 지운다.
func _update_load_error(delta: float) -> void:
	if not _load_error_panel.visible:
		return
	_load_error_time -= delta
	_load_error_panel.modulate.a = clampf(_load_error_time / LOAD_ERROR["fade"], 0.0, 1.0)
	if _load_error_time <= 0.0:
		_load_error_panel.visible = false


## 디버그 콘솔의 restart: 배경 맵을 처음부터 다시 돌린다. 카메라는 플레이어를 따라가므로 따로 되돌릴 것이 없다.
func console_restart() -> void:
	if not _left:
		Game.ReloadBackground()
		if _script != null:
			_script.call_optional("restart")


# ============================================================
#  화면 스크립트
# ============================================================

## 화면 스크립트의 frame 을 부른다. OPTION 이 열려 있는 동안에는 그 화면이 입력을 받는다.
## 반환: 스크립트가 이 프레임을 맡았으면 true. 실패했으면 기본 화면을 만들고 false.
func _process_script(delta: float) -> bool:
	_time += delta
	# OPTION 이 이번 프레임에 닫혔어도 이 프레임까지는 열려 있던 것으로 알린다.
	# 그러지 않으면 OPTION 을 닫은 ESC 를 메뉴 스크립트가 같은 프레임에 또 읽어 종료 확인을 띄운다.
	var option_open := _screen == "option"
	if option_open:
		_option.update(delta)
		if not _option.is_open():
			_screen = "main"

	var values := {"option_open": option_open, "player_exists": Game.PlayerExists()}
	if values["player_exists"]:
		var at: Vector3 = Game.PlayerPosition()
		values["player"] = [at.x, at.y, at.z]
	if _script.frame(values, delta):
		return true

	_script.stop()
	_script = null
	if _left:
		return true
	var was_option := _screen == "option"
	_build_default()
	if was_option:
		_set_screen("option")
	return false


## 메뉴가 화면 스크립트에 더 내주는 함수. 인자는 사전 하나다.
func _script_api() -> Dictionary:
	return {
		"camera": Callable(self, "_script_camera"),
		"mission_count": Callable(self, "_script_mission_count"),
		"missions": Callable(self, "_script_missions"),
		"page_name": Callable(self, "_script_page_name"),
		"load_mission": Callable(self, "_script_load_mission"),
		"open_option": Callable(self, "_script_open_option"),
		"quit": Callable(self, "_script_quit"),
	}


## 장면 카메라를 놓는다. position / euler 는 [x, y, z] (UnityXOPS 공간), fov 는 도.
func _script_camera(arguments: Dictionary) -> bool:
	var fov = arguments.get("fov", CAM_FOV)
	Game.SetSceneCamera(XopsScriptScreen.vector(arguments.get("position"), Vector3.ZERO),
		XopsScriptScreen.vector(arguments.get("euler"), CAM_EULER),
		clampf(float(fov), 1.0, 179.0) if (fov is int or fov is float) else CAM_FOV)
	return true


func _script_int(arguments: Dictionary, key: String) -> int:
	var value = arguments.get(key, 0)
	return value if value is int else 0


func _script_addon(arguments: Dictionary) -> bool:
	var value = arguments.get("addon", false)
	return value if value is bool else false


## 미션 수. addon 이 참이면 그 에드온 페이지(page)의 것, 아니면 공식 미션.
func _script_mission_count(arguments: Dictionary) -> int:
	return Game.AddonMissionCount(_script_int(arguments, "page")) if _script_addon(arguments) else Game.OfficialMissionCount()


## 미션 이름들: from 번째부터 count 개 (한 번에 64개까지).
func _script_missions(arguments: Dictionary) -> Array:
	var addon := _script_addon(arguments)
	var page := _script_int(arguments, "page")
	var from := maxi(0, _script_int(arguments, "from"))
	var total: int = Game.AddonMissionCount(page) if addon else Game.OfficialMissionCount()
	var names: Array = []
	for index in range(from, mini(total, from + clampi(_script_int(arguments, "count"), 0, SCRIPT_MISSION_BATCH))):
		names.append(Game.AddonMissionName(page, index) if addon else Game.OfficialMissionName(index))
	return names


func _script_page_name(arguments: Dictionary) -> String:
	return Game.AddonPageName(_script_int(arguments, "page"))


## 미션을 로드하고 브리핑으로 넘어간다. 실패하면 배경 맵을 다시 올리고 이유를 돌려준다.
## 반환: ok(넘어갔는지), error(실패한 이유. 영어 한 줄).
func _script_load_mission(arguments: Dictionary) -> Dictionary:
	if _left:
		return {"ok": false, "error": ""}
	if Game.LoadMission(_script_int(arguments, "index"), _script_addon(arguments), _script_int(arguments, "page")):
		_left = true
		Game.ChangeScene(BRIEFING_SCENE)
		return {"ok": true, "error": ""}
	var reason: String = Game.LastLoadError()
	Game.LoadDemo()
	return {"ok": false, "error": reason}


## OPTION 화면을 연다. 닫히면 frame 의 option_open 이 거짓으로 돌아온다.
func _script_open_option(_arguments: Dictionary) -> bool:
	if _left or _screen == "option":
		return false
	_open_option()
	return true


func _script_quit(_arguments: Dictionary) -> bool:
	if _left:
		return false
	_left = true
	Game.Quit()
	return true
