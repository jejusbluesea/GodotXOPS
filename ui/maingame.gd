extends Node
## 메인게임 화면(HUD). 체력·탄약·무기 표시, 조준선, 스코프, 이벤트 메시지, 피격 번쩍임, 벽 블라인드, 화면 암전, 미션 종료 문구를 그린다.
## F2 로 표시 방식을 바꾼다: 일반 → 간이 → 끔. F12 는 미션 재시작, ESC 는 메뉴로 나간다.
## 시점 전환(F1)과 스코프 입력은 PlayerController 가 처리하고, 여기서는 상태를 읽어 그리기만 한다.

const MENU_SCENE := "mainmenu"
const RESULT_SCENE := "result"
const UI_MODE_KEY := KEY_F2
const RESTART_KEY := KEY_F12
# 미션이 (다시) 시작된 뒤 이 시간 동안은 나가기와 재시작을 받지 않는다. 화면이 바뀌는 순간 눌려 있던 키가 새는 것을 막는다.
const INPUT_LOCK := 1.0
# EventManager.Result 값.
const RESULT_IN_PROGRESS := 0
const RESULT_COMPLETE := 1

# ----- 층 (클수록 위). 피격 번쩍임은 조준선과 HUD 아래라 그것들은 빨갛게 물들지 않는다 -----
const BLIND_ORDER := 0
const CENTER_ORDER := 1
const SCOPE_ORDER := 2
const MESSAGE_ORDER := 3
const FPS_ORDER := 4
const FLASH_ORDER := 5
const CROSSHAIR_ORDER := 6
const FRAME_ORDER := 7
const WEAPON_ORDER := 8
const HUD_ORDER := 9
const SIMPLE_ORDER := 10
const FADE_ORDER := 11
const ENDING_ORDER := 12

const MODES := ["normal", "simple", "off"]

# ----- 아래쪽 장식 테두리 (일반 표시 전용): char.dds 의 테두리 글리프 -----
const FRAME_FONT := Vector2(32, 32)
const FRAME_COLOR := Color(1, 1, 1, 0.75)
const FRAME_LINES := [
	{"pivot": XopsUI.BOTTOM_LEFT, "x": 15, "y": 73, "codes": [0xB3, 0xB4, 0xB4, 0xB4, 0xB4, 0xB4, 0xB4, 0xB5]},
	{"pivot": XopsUI.BOTTOM_LEFT, "x": 15, "y": 41, "codes": [0xC3, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC5]},
	{"pivot": XopsUI.BOTTOM_LEFT, "x": 15, "y": 23, "codes": [0xB3, 0xB4, 0xB4, 0xB6, 0xB7, 0xB7, 0xB7, 0xB8, 0xB9]},
	{"pivot": XopsUI.BOTTOM_LEFT, "x": 15, "y": -9, "codes": [0xC3, 0xC4, 0xC4, 0xC6, 0xC7, 0xC7, 0xC7, 0xC8, 0xC9]},
	{"pivot": XopsUI.BOTTOM_RIGHT, "x": 0, "y": 66, "codes": [0xB0, 0xB1, 0xB1, 0xB1, 0xB1, 0xB1, 0xB1, 0xB2]},
	{"pivot": XopsUI.BOTTOM_RIGHT, "x": 0, "y": 34, "codes": [0xC0, 0xC1, 0xC1, 0xC1, 0xC1, 0xC1, 0xC1, 0xC2]},
	{"pivot": XopsUI.BOTTOM_RIGHT, "x": 0, "y": 2, "codes": [0xD0, 0xD1, 0xD1, 0xD1, 0xD1, 0xD1, 0xD1, 0xD2]},
]

# ----- 일반 표시: 왼쪽 아래 STATE·체력 상태·탄약, 오른쪽 아래 무기 이름, 가운데 재장전·교체 -----
const STATE := {"x": 23, "y": 21, "font": Vector2(18, 24)}
const HP_Y := 21
const AMMO := {"x": 25, "y": 72, "font": Vector2(23, 24)}
const AMMO_MAGAZINE_GLYPH := 0xBB
const AMMO_RESERVE_GLYPH := 0xBA
const AMMO_RESERVE_MAX := 999
# x 는 테두리 상자(너비 256)의 왼쪽 끝에서 9 안쪽이다 (원본 HUDA_WEAPON_POSX + 9).
const WEAPON_NAME := {"x": -247, "y": 75, "font": Vector2(16, 20), "limit": 14}
const CENTER_TEXT := {"font": Vector2(32, 34), "x": 0, "y": -60, "shadow_x": 3, "shadow_y": -63, "shadow": Color(0.2, 0.2, 0.2)}

# ----- 간이 표시: 화면 가장자리 1픽셀 테두리(체력 색) + 왼쪽 아래 명판과 무기 이름 -----
const SIMPLE_PANEL := {"x": 8, "y": 7, "w": 219, "h": 25, "color": Color(0, 0, 0, 0.298)}
const SIMPLE_NAME := {"x": 8, "y": 9.5, "font": Vector2(16, 20), "limit": 10}

# ----- 3D 무기 표시: 화면 오른쪽 아래. 자리와 카메라는 UnityXOPS 공간 -----
const WEAPON_VIEW := {
	"size": 256,
	"main_position": Vector3(0, -4.3, 0), "main_scale": 0.8, "spin": 66.667,
	"sub_position": Vector3(2.5, -5, 0), "sub_scale": 0.4, "sub_yaw": 90.0,
	"camera_position": Vector3(0, 0, -10), "camera_euler": Vector3.ZERO, "camera_fov": 65.0,
}

# ----- 이벤트 메시지: 화면 아래에서 box_height 높이의 상자 위쪽에 가운데 정렬 -----
const MESSAGE := {"box_height": 140, "font_size": 18, "color": Color(1, 1, 1)}

# ----- 피격 번쩍임: hold 동안 가장 진하고 fade 동안 옅어진다 -----
const FLASH := {"color": Color(1, 0, 0, 0.5), "hold": 0.05, "fade": 0.1}

# ----- 화면 암전: 시작할 때 in_time 동안 밝아지고, 끝나면 out_time 동안 어두워진다 -----
const FADE := {"in_time": 2.0, "out_time": 3.5}

# ----- 미션 종료 문구: 나타남 → 유지 → 사라짐 -----
const ENDING := {
	"in_time": 1.0, "hold": 2.0, "out_time": 1.0, "font": Vector2(28, 32),
	"success_text": "objective complete", "success_color": Color(1, 0.5, 0),
	"fail_text": "mission failure", "fail_color": Color(1, 0, 0),
}

# ----- FPS: 화면 오른쪽 위 -----
const FPS := {"x": -10, "y": -10, "font": Vector2(18, 24), "color": Color(1, 0, 1), "interval": 0.5}

const DEFAULT_SCOPE_ASPECT := 4.0 / 3.0

var _ui: CanvasLayer
var _layers := {}
var _mode := "normal"
var _left := false
var _input_lock := INPUT_LOCK
var _play_time := 0.0
var _end_time := -1.0
var _last_start := -1

var _frames: Array[XopsText] = []
var _state: XopsText
var _hp: XopsText
var _ammo: XopsText
var _weapon_name: XopsText
var _weapon_view: TextureRect
var _weapon_yaw := 0.0
var _reload: Array[XopsText] = []
var _change: Array[XopsText] = []
var _simple_bars: Array[ColorRect] = []
var _simple_panel: ColorRect
var _simple_name: XopsText

var _crosshair: Array[ColorRect] = []
var _crosshair_gap := 0
var _crosshair_length := 10
var _crosshair_static := false
var _last_gap := -1

var _scope_image: TextureRect
var _scope_bars: Array[ColorRect] = []
var _scope_lines: XopsLines
var _scope_aspect := DEFAULT_SCOPE_ASPECT
var _scope_hides_crosshair := false
var _shown_scope := -1

var _message: Label
var _last_message := -1
var _flash: ColorRect
var _flash_timer := 0.0
var _flash_player := -1
var _blind: Array[ColorRect] = []
var _fade: ColorRect
var _ending: XopsText
var _fps: XopsText
var _fps_time := 0.0
var _fps_frames := 0

var _last_hp := -1.0
var _last_magazine := -1
var _last_reserve := -1
var _last_name := ""


func _ready() -> void:
	InputManager.MouseCursorMode(true, true, true)
	Game.BeginMission()

	_ui = CanvasLayer.new()
	add_child(_ui)

	_build_frames()
	_build_normal()
	_build_weapon_view()
	_build_center()
	_build_simple()
	_build_scope()
	_build_crosshair()
	_build_overlays()

	var start_mode := Dev.value("--ui-state", "normal")
	if start_mode in MODES:
		_mode = start_mode
	_apply_mode()


func _exit_tree() -> void:
	Game.FreeWeaponView()


func _process(delta: float) -> void:
	if _left:
		return

	# 입력을 먼저 본다. 재시작하면 바로 아래에서 알아채 같은 프레임에 화면이 검어진다.
	if _update_input(delta):
		return

	if EventManager.StartCount != _last_start:
		_last_start = EventManager.StartCount
		_reset_for_start()

	if InputManager.WasKeyPressed(UI_MODE_KEY):
		_mode = MODES[(MODES.find(_mode) + 1) % MODES.size()]
		_apply_mode()
		_invalidate_values()

	var hidden_by_scope := _apply_scope()
	var show_crosshair: bool = Game.IsFirstPerson() and Game.ShowsCrosshair() and not hidden_by_scope
	for bar in _crosshair:
		bar.visible = show_crosshair
	if show_crosshair:
		var gap: int = _crosshair_gap if _crosshair_static else _crosshair_gap + Game.ErrorRange()
		if gap != _last_gap:
			_last_gap = gap
			_spread_crosshair(gap)

	if Game.PlayerExists():
		if _mode == "normal":
			_update_normal_values()
		elif _mode == "simple":
			_update_simple_values()

		var reloading: bool = Game.IsReloading()
		for node in _reload:
			node.visible = reloading
		var changing: bool = Game.IsSwitchingWeapon()
		for node in _change:
			node.visible = changing

	_weapon_yaw = fmod(_weapon_yaw + WEAPON_VIEW["spin"] * delta, 360.0)
	Game.SetWeaponViewMain(WEAPON_VIEW["main_position"], WEAPON_VIEW["main_scale"], _weapon_yaw)

	_update_blind()
	_update_message()
	_update_flash(delta)
	_update_fade(delta)
	_update_fps(delta)


# ============================================================
#  화면 구성
# ============================================================

## 층을 만든다. scaled 가 true 면 화면 높이에 맞춰 확대하고, false 면 픽셀 1:1 에 설정의 UIScale 을 곱한다.
func _layer(order: int, scaled: bool) -> XopsLayer:
	if not _layers.has(order):
		_layers[order] = XopsUI.layer(_ui, order, scaled)
	return _layers[order]


func _build_frames() -> void:
	var layer := _layer(FRAME_ORDER, false)
	for line in FRAME_LINES:
		_frames.append(XopsUI.text(layer, line["pivot"], line["pivot"], XopsUI.glyphs(line["codes"]),
			line["x"], line["y"], FRAME_FONT.x, FRAME_FONT.y, FRAME_COLOR))


func _build_normal() -> void:
	var layer := _layer(HUD_ORDER, false)
	var state_font: Vector2 = STATE["font"]
	_state = XopsUI.text(layer, XopsUI.BOTTOM_LEFT, XopsUI.BOTTOM_LEFT, "STATE", STATE["x"], STATE["y"], state_font.x, state_font.y, Color(0, 1, 0))
	_hp = XopsUI.text(layer, XopsUI.BOTTOM_LEFT, XopsUI.BOTTOM_LEFT, "", _hp_x(100.0), HP_Y, state_font.x, state_font.y, Color(0, 1, 0))

	var ammo_font: Vector2 = AMMO["font"]
	_ammo = XopsUI.text(layer, XopsUI.BOTTOM_LEFT, XopsUI.BOTTOM_LEFT, "", AMMO["x"], AMMO["y"], ammo_font.x, ammo_font.y, Color.WHITE)

	# 무기 이름은 화면 오른쪽 가장자리 기준이라 화면비가 넓어져도 오른쪽에 붙는다.
	var name_font: Vector2 = WEAPON_NAME["font"]
	_weapon_name = XopsUI.text(layer, XopsUI.BOTTOM_RIGHT, XopsUI.BOTTOM_LEFT, "", WEAPON_NAME["x"], WEAPON_NAME["y"], name_font.x, name_font.y, Color.WHITE)


func _build_weapon_view() -> void:
	var size: int = WEAPON_VIEW["size"]
	var texture: Texture2D = Game.CreateWeaponView(size)
	_weapon_view = XopsUI.image(_layer(WEAPON_ORDER, false), XopsUI.BOTTOM_RIGHT, texture, 0, 0, size, size)
	Game.SetWeaponViewMain(WEAPON_VIEW["main_position"], WEAPON_VIEW["main_scale"], 0.0)
	Game.SetWeaponViewSub(WEAPON_VIEW["sub_position"], WEAPON_VIEW["sub_scale"], WEAPON_VIEW["sub_yaw"])
	Game.SetWeaponViewCamera(WEAPON_VIEW["camera_position"], WEAPON_VIEW["camera_euler"], WEAPON_VIEW["camera_fov"])


func _build_center() -> void:
	var layer := _layer(CENTER_ORDER, true)
	_reload = _center_pair(layer, "RELOADING")
	_change = _center_pair(layer, "CHANGING")


## 화면 가운데에 그림자와 본문 두 겹의 글자를 만든다. 처음에는 숨겨 둔다.
func _center_pair(layer: XopsLayer, value: String) -> Array[XopsText]:
	var font: Vector2 = CENTER_TEXT["font"]
	var shadow := XopsUI.text(layer, XopsUI.CENTER, XopsUI.CENTER, value,
		CENTER_TEXT["shadow_x"], CENTER_TEXT["shadow_y"], font.x, font.y, CENTER_TEXT["shadow"])
	var main := XopsUI.text(layer, XopsUI.CENTER, XopsUI.CENTER, value, CENTER_TEXT["x"], CENTER_TEXT["y"], font.x, font.y, Color.WHITE)
	shadow.visible = false
	main.visible = false
	return [shadow, main]


func _build_simple() -> void:
	var layer := _layer(SIMPLE_ORDER, false)
	_simple_bars = [
		XopsUI.panel_stretch(layer, XopsUI.Stretch.LEFT, 0, 0, 1, -2, Color(0, 1, 0)),
		XopsUI.panel_stretch(layer, XopsUI.Stretch.RIGHT, 0, 0, 1, -2, Color(0, 1, 0)),
		XopsUI.panel_stretch(layer, XopsUI.Stretch.TOP, 0, 0, 0, 1, Color(0, 1, 0)),
		XopsUI.panel_stretch(layer, XopsUI.Stretch.BOTTOM, 0, 0, 0, 1, Color(0, 1, 0)),
	]
	_simple_panel = XopsUI.panel(layer, XopsUI.BOTTOM_LEFT, SIMPLE_PANEL["x"], SIMPLE_PANEL["y"],
		SIMPLE_PANEL["w"], SIMPLE_PANEL["h"], SIMPLE_PANEL["color"])
	var font: Vector2 = SIMPLE_NAME["font"]
	_simple_name = XopsUI.text(layer, XopsUI.BOTTOM_LEFT, XopsUI.BOTTOM_LEFT, "", SIMPLE_NAME["x"], SIMPLE_NAME["y"], font.x, font.y, Color.WHITE)


func _build_scope() -> void:
	var layer := _layer(SCOPE_ORDER, true)
	_scope_image = XopsUI.image(layer, XopsUI.CENTER, null, 0, 0, 0, 0)
	for i in 4:
		_scope_bars.append(XopsUI.panel(layer, XopsUI.CENTER, 0, 0, 0, 0, Color.BLACK))
	_scope_lines = XopsLines.new()
	layer.add_child(_scope_lines)
	XopsUI.move(_scope_lines, XopsUI.CENTER, 0, 0)
	_set_scope_visible(false)


func _build_crosshair() -> void:
	_crosshair_gap = ConfigManager.GetInt("General", "aimGap", 3)
	_crosshair_length = ConfigManager.GetInt("General", "aimLength", 10)
	_crosshair_static = ConfigManager.GetBool("General", "StaticAim", false)
	var thick: int = ConfigManager.GetInt("General", "aimThick", 1)
	var color := Color(
		ConfigManager.GetFloat("General", "aimColorR", 1.0), ConfigManager.GetFloat("General", "aimColorG", 0.0),
		ConfigManager.GetFloat("General", "aimColorB", 0.0), ConfigManager.GetFloat("General", "aimColorA", 1.0))

	# 왼쪽, 오른쪽, 위, 아래 순서. 가로 막대는 길이 × 두께, 세로 막대는 두께 × 길이다.
	var layer := _layer(CROSSHAIR_ORDER, false)
	for i in 4:
		var size := Vector2(_crosshair_length, thick) if i < 2 else Vector2(thick, _crosshair_length)
		var bar := XopsUI.panel(layer, XopsUI.CENTER, 0, 0, size.x, size.y, color)
		bar.visible = false
		_crosshair.append(bar)


func _build_overlays() -> void:
	_message = XopsUI.label(_layer(MESSAGE_ORDER, true), "", MESSAGE["font_size"], MESSAGE["color"])
	_message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	XopsUI.place_stretch(_message, XopsUI.Stretch.BOTTOM, 0, 0, 0, MESSAGE["box_height"])
	_message.modulate.a = 0.0

	_flash = XopsUI.panel_stretch(_layer(FLASH_ORDER, false), XopsUI.Stretch.FULL, 0, 0, 0, 0, FLASH["color"])
	_flash.visible = false

	var fps_font: Vector2 = FPS["font"]
	_fps = XopsUI.text(_layer(FPS_ORDER, false), XopsUI.TOP_RIGHT, XopsUI.TOP_RIGHT, "", FPS["x"], FPS["y"], fps_font.x, fps_font.y, FPS["color"])

	# 위, 아래, 왼쪽, 오른쪽 순서 (Game.GetWallBlind 의 비트 순서와 같다). 크기는 켜질 때 화면 절반으로 맞춘다.
	var blind_layer := _layer(BLIND_ORDER, true)
	for mode in [XopsUI.Stretch.TOP, XopsUI.Stretch.BOTTOM, XopsUI.Stretch.LEFT, XopsUI.Stretch.RIGHT]:
		var box := XopsUI.panel_stretch(blind_layer, mode, 0, 0, 0, 0, Color.BLACK)
		box.visible = false
		_blind.append(box)

	_fade = XopsUI.panel_stretch(_layer(FADE_ORDER, true), XopsUI.Stretch.FULL, 0, 0, 0, 0, Color.BLACK)
	var ending_font: Vector2 = ENDING["font"]
	_ending = XopsUI.text(_layer(ENDING_ORDER, true), XopsUI.CENTER, XopsUI.CENTER, "", 0, 0, ending_font.x, ending_font.y, Color.WHITE)
	_ending.visible = false


# ============================================================
#  표시 방식
# ============================================================

func _apply_mode() -> void:
	var normal := _mode == "normal"
	for frame in _frames:
		frame.visible = normal
	_state.visible = normal
	_hp.visible = normal
	_ammo.visible = normal
	_weapon_name.visible = normal
	_weapon_view.visible = normal

	var simple := _mode == "simple"
	for bar in _simple_bars:
		bar.visible = simple
	_simple_panel.visible = simple
	_simple_name.visible = simple


## 다음 프레임에 체력·탄약·무기 이름을 다시 칠하게 한다.
func _invalidate_values() -> void:
	_last_hp = -1.0
	_last_magazine = -1
	_last_reserve = -1
	_last_name = ""


func _update_normal_values() -> void:
	var hp: float = Game.PlayerHP()
	if hp != _last_hp:
		_last_hp = hp
		var color := _state_color(hp)
		_state.color = color
		_hp.color = color
		_hp.text = _hp_text(hp)
		XopsUI.move(_hp, XopsUI.BOTTOM_LEFT, _hp_x(hp), HP_Y)

	var magazine: int = Game.Magazine()
	var reserve: int = Game.Reserve()
	if magazine != _last_magazine or reserve != _last_reserve:
		_last_magazine = magazine
		_last_reserve = reserve
		var reserve_text := "%d+" % AMMO_RESERVE_MAX if reserve > AMMO_RESERVE_MAX else str(reserve)
		_ammo.text = "%s%d %s%s" % [String.chr(AMMO_MAGAZINE_GLYPH), magazine, String.chr(AMMO_RESERVE_GLYPH), reserve_text]

	var weapon_name: String = Game.WeaponName()
	if weapon_name != _last_name or _weapon_name.text != weapon_name:
		_last_name = weapon_name
		_weapon_name.text = weapon_name
		_weapon_name.char_size = _name_font(weapon_name, WEAPON_NAME["font"], WEAPON_NAME["limit"])


func _update_simple_values() -> void:
	var hp: float = Game.PlayerHP()
	if hp != _last_hp:
		_last_hp = hp
		var color := _state_color(hp)
		for bar in _simple_bars:
			bar.color = color

	var weapon_name: String = Game.WeaponName()
	if weapon_name != _last_name or _simple_name.text != weapon_name:
		_last_name = weapon_name
		_simple_name.text = weapon_name
		_simple_name.char_size = _name_font(weapon_name, SIMPLE_NAME["font"], SIMPLE_NAME["limit"])


## 체력에 따른 색: 100 이상 초록 → 50 노랑 → 0 빨강.
func _state_color(hp: float) -> Color:
	if hp >= 100.0:
		return Color(0, 1, 0)
	if hp >= 50.0:
		return Color((100.0 - hp) / 50.0, 1, 0)
	if hp > 0.0:
		return Color(1, hp / 50.0, 0)
	return Color(1, 0, 0)


func _hp_text(hp: float) -> String:
	if hp >= 80.0:
		return "FINE"
	if hp >= 40.0:
		return "CAUTION"
	if hp > 0.0:
		return "DANGER"
	return "DEAD"


## 체력 상태 글자의 x 위치. 글자 수에 맞춰 테두리 안 가운데로 온다.
func _hp_x(hp: float) -> float:
	if hp >= 80.0:
		return 155.0
	if hp >= 40.0:
		return 135.0
	if hp > 0.0:
		return 140.0
	return 155.0


## 무기 이름이 limit 글자를 넘으면 글자 폭만 줄여 같은 너비에 맞춘다.
func _name_font(value: String, font: Vector2, limit: int) -> Vector2:
	if value.length() <= limit:
		return font
	return Vector2(font.x * limit / value.length(), font.y)


# ============================================================
#  조준선과 스코프
# ============================================================

## 막대의 안쪽 끝이 화면 가운데에서 gap 만큼 떨어지게 놓는다.
func _spread_crosshair(gap: int) -> void:
	var offset := gap + _crosshair_length * 0.5
	var points := [Vector2(-offset, 0), Vector2(offset, 0), Vector2(0, offset), Vector2(0, -offset)]
	for i in 4:
		var bar := _crosshair[i]
		XopsUI.place(bar, XopsUI.CENTER, points[i].x, points[i].y, bar.size.x, bar.size.y)


func _set_scope_visible(visible: bool) -> void:
	_scope_image.visible = visible
	_scope_lines.visible = visible
	for bar in _scope_bars:
		bar.visible = visible


## 스코프 표시를 지금 상태에 맞춘다. 반환: 스코프가 조준선을 대신하므로 조준선을 숨겨야 하는지.
func _apply_scope() -> bool:
	# 3인칭에서는 스코프 상태가 켜져 있어도 그리지 않는다.
	var index := -1
	if Game.IsScoping() and Game.IsFirstPerson():
		index = Game.ScopeIndex()

	if index != _shown_scope:
		_set_scope_visible(false)
		if index >= 0:
			var scope: Dictionary = Game.ActiveScope()
			if scope.is_empty():
				index = -1
			else:
				var aspect: float = scope["aspect"]
				_scope_aspect = aspect if aspect > 0.0 else DEFAULT_SCOPE_ASPECT
				_scope_hides_crosshair = scope["hideCrosshair"]
				_scope_image.texture = Game.LoadTexture(scope["texturePath"])
				_scope_lines.lines = scope["lines"]
				_set_scope_visible(true)
		_shown_scope = index

	if _shown_scope < 0:
		return false

	_layout_scope()
	return _scope_hides_crosshair


## 스코프 그림을 제 비율로 화면에 가장 크게 넣고, 남는 바깥을 검은 막대로 채운다.
func _layout_scope() -> void:
	var size := (_layers[SCOPE_ORDER] as XopsLayer).size
	if size.x <= 0.0 or size.y <= 0.0:
		return

	var scope := Vector2(size.y * _scope_aspect, size.y)
	if size.x / size.y < _scope_aspect:
		scope = Vector2(size.x, size.x / _scope_aspect)

	# 1픽셀도 안 되는 자투리는 비율 값의 반올림 오차이므로 꽉 채운다.
	var gap := (size - scope) / 2.0
	if gap.x < 0.5:
		scope.x = size.x
		gap.x = 0.0
	if gap.y < 0.5:
		scope.y = size.y
		gap.y = 0.0

	XopsUI.place(_scope_image, XopsUI.CENTER, 0, 0, scope.x, scope.y)
	XopsUI.place(_scope_bars[0], XopsUI.CENTER, -(scope.x + gap.x) / 2.0, 0, gap.x, size.y)
	XopsUI.place(_scope_bars[1], XopsUI.CENTER, (scope.x + gap.x) / 2.0, 0, gap.x, size.y)
	XopsUI.place(_scope_bars[2], XopsUI.CENTER, 0, (scope.y + gap.y) / 2.0, size.x, gap.y)
	XopsUI.place(_scope_bars[3], XopsUI.CENTER, 0, -(scope.y + gap.y) / 2.0, size.x, gap.y)


# ============================================================
#  화면 효과
# ============================================================

## 카메라가 벽에 묻힌 방향의 화면 절반을 검게 덮는다. 여러 방향이 함께 켜질 수 있다.
func _update_blind() -> void:
	var bits: int = Game.GetWallBlind()
	if bits != 0:
		var half := (_layers[BLIND_ORDER] as XopsLayer).size / 2.0
		XopsUI.place_stretch(_blind[0], XopsUI.Stretch.TOP, 0, 0, 0, half.y)
		XopsUI.place_stretch(_blind[1], XopsUI.Stretch.BOTTOM, 0, 0, 0, half.y)
		XopsUI.place_stretch(_blind[2], XopsUI.Stretch.LEFT, 0, 0, half.x, 0)
		XopsUI.place_stretch(_blind[3], XopsUI.Stretch.RIGHT, 0, 0, half.x, 0)
	for i in 4:
		_blind[i].visible = (bits & (1 << i)) != 0


## 이벤트 메시지. 글은 바뀔 때만 다시 쓰고, 진하기는 EventManager 가 계산한 값을 매 프레임 쓴다.
func _update_message() -> void:
	var id: int = EventManager.MessageId
	if id < 0:
		_last_message = -1
		_message.modulate.a = 0.0
		return

	if id != _last_message:
		_last_message = id
		_message.text = EventManager.MessageText
	_message.modulate.a = EventManager.MessageAlpha


## 피격 번쩍임. 피격 표시는 확인하면 지워지므로 여기서만 확인한다.
func _update_flash(delta: float) -> void:
	# 조작 대상이 바뀌면 그 사람이 갖고 있던 피격 표시를 흘려 버린다.
	var index: int = Game.PlayerIndex()
	if index != _flash_player:
		_flash_player = index
		Game.ConsumeHit()
	elif Game.ConsumeHit() and Game.PlayerAlive():
		_flash_timer = FLASH["hold"] + FLASH["fade"]

	var intensity := 0.0
	if _flash_timer > 0.0:
		_flash_timer = maxf(0.0, _flash_timer - delta)
		intensity = _flash_timer / FLASH["fade"] if FLASH["fade"] > 0.0 and _flash_timer < FLASH["fade"] else 1.0

	_flash.visible = intensity > 0.0
	if intensity > 0.0:
		_flash.color.a = (FLASH["color"] as Color).a * intensity


## 미션이 처음부터 다시 시작될 때: 종료 문구를 걷고 검은 화면에서 다시 밝아지게 한다.
func _reset_for_start() -> void:
	_play_time = 0.0
	_end_time = -1.0
	_input_lock = INPUT_LOCK
	_ending.visible = false
	_fade.color.a = 1.0
	_invalidate_values()


## 화면 암전과 종료 문구를 진행한다. 둘 다 끝나면 결과 화면으로 넘어간다.
func _update_fade(delta: float) -> void:
	var result: int = EventManager.Result
	if result == RESULT_IN_PROGRESS:
		_play_time += delta
		_fade.color.a = 1.0 - clampf(_play_time / FADE["in_time"], 0.0, 1.0)
		return

	if _end_time < 0.0:
		_end_time = 0.0
		var complete := result == RESULT_COMPLETE
		_ending.text = ENDING["success_text"] if complete else ENDING["fail_text"]
		_ending.color = ENDING["success_color"] if complete else ENDING["fail_color"]
		_ending.visible = true

	_end_time += delta
	_fade.color.a = clampf(_end_time / FADE["out_time"], 0.0, 1.0)
	_ending.set_alpha(_ending_alpha(_end_time))

	var text_total: float = ENDING["in_time"] + ENDING["hold"] + ENDING["out_time"]
	if _end_time >= maxf(FADE["out_time"], text_total):
		_left = true
		Game.UnloadMap()
		Game.ChangeScene(RESULT_SCENE)


func _ending_alpha(time: float) -> float:
	var in_time: float = ENDING["in_time"]
	var hold: float = ENDING["hold"]
	var out_time: float = ENDING["out_time"]
	if time < in_time:
		return time / in_time
	if time < in_time + hold:
		return 1.0
	if time < in_time + hold + out_time:
		return 1.0 - (time - in_time - hold) / out_time
	return 0.0


func _update_fps(delta: float) -> void:
	var show: bool = ConfigManager.GetBool("General", "ShowFPS", false)
	_fps.visible = show
	if not show:
		return

	_fps_time += delta
	_fps_frames += 1
	if _fps_time >= FPS["interval"]:
		var frame := _fps_time / _fps_frames
		_fps.text = "%d FPS (%d ms)" % [roundi(1.0 / frame), roundi(frame * 1000.0)]
		_fps_time = 0.0
		_fps_frames = 0


## 나가기와 재시작 입력을 처리한다. 반환: 화면을 떠났으면 true.
func _update_input(delta: float) -> bool:
	if _input_lock > 0.0:
		_input_lock -= delta
		return false

	if InputManager.WasPressed("escape"):
		_left = true
		Game.UnloadMission()
		Game.ChangeScene(MENU_SCENE)
		return true

	if InputManager.WasKeyPressed(RESTART_KEY):
		Game.RestartMission()
	return false
