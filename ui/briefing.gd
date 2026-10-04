extends Node
## 브리핑. 미션 이미지, 미션 이름, 브리핑 본문을 보여 준다. 좌클릭이면 미션 시작, ESC 면 메뉴로 돌아간다.

const MENU_SCENE := "mainmenu"
const GAME_SCENE := "maingame"
# 들어온 뒤 이 시간이 지나야 입력을 받는다. 앞 화면에서 누른 입력이 새어 들어오지 않게 한다.
const INPUT_ALLOW_TIME := 0.2

# ----- 층 (클수록 위) -----
const BACKGROUND_ORDER := 0
const CONTENT_ORDER := 1
const CLICK_ORDER := 2

# ----- 배경 -----
const BACKDROP_COLOR := Color(0, 0, 0, 1)
const TITLE_PATH := "data/title.dds"
# Lua 값은 0.012 인데 UnityXOPS 는 선형 색 공간에서 섞어 화면에서는 약 0.1 밝기로 보인다. 여기서는 sRGB 값으로 섞으므로 보이는 밝기를 적는다.
# 원본도 0.4 로 깔고 0.75 검정을 덮어 0.1 이다 (scene.cpp:200-201).
const TITLE_ALPHA := 0.1

# ----- 미션 이미지: 화면 왼쪽 가운데 기준 -----
# 이미지가 한 장이면 SINGLE 자리, 두 장이면 FIRST / SECOND 자리에 놓는다.
const IMAGE_SIZE := Vector2(160, 150)
const IMAGE_SINGLE := Vector2(40, -15)
const IMAGE_FIRST := Vector2(40, 35)
const IMAGE_SECOND := Vector2(40, -135)

# ----- BRIEFING 제목: 화면 위 가운데 기준. 진하기가 from → to 로 duration 초마다 되풀이된다 -----
const HEADING := {
	"text": "BRIEFING", "x": 0, "y": -30, "w": 60, "h": 42, "color": Color(1, 1, 0),
	"duration": 0.7, "alpha_from": 1.0, "alpha_to": 0.1585,
}

# ----- 미션 이름 -----
const FULLNAME := {"x": 0, "y": -90, "w": 18, "h": 25, "color": Color(1, 0.5, 0)}

# ----- 브리핑 본문 (OS 글꼴): 화면 왼쪽 위 기준 글상자 -----
const BODY := {"x": 230, "y": -175, "w": 370, "h": 600, "font_size": 16, "line_spacing": -3, "color": Color(1, 1, 1)}

# ----- 클릭 안내: 화면 오른쪽 아래 기준. 두 겹이고 위 겹만 커지며 흐려지기를 되풀이한다 -----
const CLICK := {
	"text": "LEFT CLICK TO BEGIN", "x": -220, "y": 35, "color": Color(1, 1, 1),
	"size": Vector2(18, 26),
	"pulse_from": Vector2(18, 24), "pulse_to": Vector2(26, 58),
	"duration": 1.0, "alpha_from": 1.0, "alpha_to": 0.142,
}

var _time := 0.0
var _finished := false
var _heading: XopsText
var _click_pulse: XopsText


func _ready() -> void:
	InputManager.MouseCursorMode(true, false, false)

	var ui := CanvasLayer.new()
	add_child(ui)

	var background := XopsUI.layer(ui, BACKGROUND_ORDER, true)
	XopsUI.panel_stretch(background, XopsUI.Stretch.FULL, 0, 0, 0, 0, BACKDROP_COLOR)
	XopsUI.image_stretch(background, XopsUI.Stretch.FULL, Game.LoadTexture(TITLE_PATH), 0, 0, 0, 0, Color(1, 1, 1, TITLE_ALPHA))

	var content := XopsUI.layer(ui, CONTENT_ORDER, true)
	var image0: Texture2D = Game.MissionImage(0)
	var image1: Texture2D = Game.MissionImage(1)
	if image1 != null:
		_add_image(content, image0, IMAGE_FIRST)
		_add_image(content, image1, IMAGE_SECOND)
	else:
		_add_image(content, image0, IMAGE_SINGLE)

	_heading = XopsUI.text(content, XopsUI.TOP_CENTER, XopsUI.TOP_CENTER, HEADING["text"],
		HEADING["x"], HEADING["y"], HEADING["w"], HEADING["h"], HEADING["color"])
	XopsUI.text(content, XopsUI.TOP_CENTER, XopsUI.TOP_CENTER, Game.MissionFullname(),
		FULLNAME["x"], FULLNAME["y"], FULLNAME["w"], FULLNAME["h"], FULLNAME["color"])

	var body := XopsUI.label(content, Game.MissionBriefing(), BODY["font_size"], BODY["color"])
	body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	body.add_theme_constant_override("line_spacing", BODY["line_spacing"])
	XopsUI.place(body, XopsUI.TOP_LEFT, BODY["x"], BODY["y"], BODY["w"], BODY["h"])

	# 클릭 안내는 화면 크기와 무관하게 고정 크기다 (설정의 UIScale 만 따른다).
	var click := XopsUI.layer(ui, CLICK_ORDER, false)
	var click_size: Vector2 = CLICK["size"]
	XopsUI.text(click, XopsUI.BOTTOM_RIGHT, XopsUI.CENTER, CLICK["text"],
		CLICK["x"], CLICK["y"], click_size.x, click_size.y, CLICK["color"])
	var pulse_size: Vector2 = CLICK["pulse_from"]
	_click_pulse = XopsUI.text(click, XopsUI.BOTTOM_RIGHT, XopsUI.CENTER, CLICK["text"],
		CLICK["x"], CLICK["y"], pulse_size.x, pulse_size.y, CLICK["color"])


func _process(delta: float) -> void:
	if _finished:
		return
	_time += delta

	_heading.set_alpha(lerpf(HEADING["alpha_from"], HEADING["alpha_to"], XopsUI.cycle(_time, HEADING["duration"])))

	var spread := XopsUI.cycle(_time, CLICK["duration"])
	_click_pulse.set_alpha(lerpf(CLICK["alpha_from"], CLICK["alpha_to"], spread))
	_click_pulse.char_size = (CLICK["pulse_from"] as Vector2).lerp(CLICK["pulse_to"], spread)

	if _time < INPUT_ALLOW_TIME:
		return

	if InputManager.WasPressed("escape"):
		_finished = true
		Game.UnloadMission()
		Game.ChangeScene(MENU_SCENE)
	elif InputManager.WasClickPressed():
		_finished = true
		Game.ChangeScene(GAME_SCENE)


func _add_image(parent: Control, texture: Texture2D, slot: Vector2) -> void:
	if texture != null:
		XopsUI.image(parent, XopsUI.MIDDLE_LEFT, texture, slot.x, slot.y, IMAGE_SIZE.x, IMAGE_SIZE.y)
