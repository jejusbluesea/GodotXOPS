extends Node
## 결과. 미션 성공·실패와 플레이어 통계를 보여 준다. F12 는 같은 미션 재시작, ESC 나 좌클릭은 메뉴로 돌아간다.

const SCREEN_NAME := "result"
const MENU_SCENE := "mainmenu"
const GAME_SCENE := "maingame"
const INPUT_ALLOW_TIME := 0.2
const RESTART_KEY := KEY_F12
# EventManager.Result 값: 1 = 클리어.
const RESULT_COMPLETE := 1

# ----- 층 (클수록 위) -----
const BACKGROUND_ORDER := 0
const CONTENT_ORDER := 1

# ----- 배경 -----
const BACKDROP_COLOR := Color(0, 0, 0, 1)
const TITLE_PATH := "data/title.dds"
# Lua 값은 0.012 인데 UnityXOPS 는 선형 색 공간에서 섞어 화면에서는 약 0.1 밝기로 보인다. 여기서는 sRGB 값으로 섞으므로 보이는 밝기를 적는다.
# 원본도 0.4 로 깔고 0.75 검정을 덮어 0.1 이다 (scene.cpp:200-201).
const TITLE_ALPHA := 0.1

# ----- RESULT 제목: 화면 위 가운데 기준. 진하기가 from → to 로 duration 초마다 되풀이된다 -----
const HEADING := {
	"text": "RESULT", "y": -30, "w": 50, "h": 42, "color": Color(1, 0, 1),
	"duration": 0.7, "alpha_from": 1.0, "alpha_to": 0.1585,
}

# ----- 미션 이름 -----
const FULLNAME := {"y": -90, "w": 18, "h": 25, "color": Color(0.502, 0.502, 1)}

# ----- 판정 -----
const VERDICT := {
	"y": -140, "w": 24, "h": 32,
	"success_text": "mission successful", "success_color": Color(0, 1, 0),
	"fail_text": "mission failure", "fail_color": Color(1, 0, 0),
}

# ----- 통계 줄: top 이 첫 줄, pitch 가 줄 간격 (음수면 아래로) -----
const INFO := {"top": -200, "pitch": -50, "w": 20, "h": 32, "color": Color(1, 1, 1)}

var _time := 0.0
var _finished := false
var _heading: XopsText
var _ui: CanvasLayer
# 이 화면을 맡은 화면 스크립트 (godotdata/ui 에 등록된 .sgd). 없으면 아래의 기본 화면을 그린다.
var _script: XopsScriptScreen


func _ready() -> void:
	InputManager.MouseCursorMode(true, false, false)

	_ui = CanvasLayer.new()
	add_child(_ui)

	_script = XopsScriptScreen.start(_ui, SCREEN_NAME,
		{"restart": Callable(self, "_script_restart"), "back": Callable(self, "_script_back")},
		{"fullname": Game.MissionFullname(), "complete": EventManager.Result == RESULT_COMPLETE, "stats": Game.GetStats()})
	if _script == null:
		_build_default()


func _exit_tree() -> void:
	if _script != null:
		_script.stop()
		_script = null


## 기본 화면을 만든다. 화면 스크립트가 없거나 실패했을 때 쓴다.
func _build_default() -> void:
	var ui := _ui
	var background := XopsUI.layer(ui, BACKGROUND_ORDER, true)
	XopsUI.panel_stretch(background, XopsUI.Stretch.FULL, 0, 0, 0, 0, BACKDROP_COLOR)
	XopsUI.image_stretch(background, XopsUI.Stretch.FULL, Game.LoadTexture(TITLE_PATH), 0, 0, 0, 0, Color(1, 1, 1, TITLE_ALPHA))

	var content := XopsUI.layer(ui, CONTENT_ORDER, true)
	_heading = _line(content, HEADING["text"], HEADING["y"], HEADING["w"], HEADING["h"], HEADING["color"])
	_line(content, Game.MissionFullname(), FULLNAME["y"], FULLNAME["w"], FULLNAME["h"], FULLNAME["color"])

	var complete: bool = EventManager.Result == RESULT_COMPLETE
	_line(content, VERDICT["success_text"] if complete else VERDICT["fail_text"],
		VERDICT["y"], VERDICT["w"], VERDICT["h"], VERDICT["success_color"] if complete else VERDICT["fail_color"])

	var lines := _stat_lines(Game.GetStats())
	for i in lines.size():
		_line(content, lines[i], INFO["top"] + INFO["pitch"] * i, INFO["w"], INFO["h"], INFO["color"])


func _process(delta: float) -> void:
	if _finished:
		return

	if _script != null:
		if _script.frame({}, delta):
			return
		_script.stop()
		_script = null
		if _finished:
			return
		_build_default()

	_time += delta

	_heading.set_alpha(lerpf(HEADING["alpha_from"], HEADING["alpha_to"], XopsUI.cycle(_time, HEADING["duration"])))

	if _time < INPUT_ALLOW_TIME:
		return

	if InputManager.WasKeyPressed(RESTART_KEY):
		_script_restart({})
	elif InputManager.WasPressed("escape") or InputManager.WasClickPressed():
		_script_back({})


## 같은 미션을 다시 시작한다 (화면 스크립트도 부른다). 다시 로드하지 못하면 메뉴로 간다.
func _script_restart(_arguments: Dictionary) -> bool:
	if _finished:
		return false
	_finished = true
	if Game.ReloadMission():
		Game.ChangeScene(GAME_SCENE)
	else:
		Game.UnloadMission()
		Game.ChangeScene(MENU_SCENE)
	return true


## 미션을 내리고 메뉴로 돌아간다 (화면 스크립트도 부른다).
func _script_back(_arguments: Dictionary) -> bool:
	if _finished:
		return false
	_finished = true
	Game.UnloadMission()
	Game.ChangeScene(MENU_SCENE)
	return true


## 화면 위 가운데 기준으로 한 줄을 놓는다.
func _line(parent: Control, value: String, y: float, w: float, h: float, color: Color) -> XopsText:
	return XopsUI.text(parent, XopsUI.TOP_CENTER, XopsUI.TOP_CENTER, value, 0, y, w, h, color)


## 통계를 화면에 표시할 줄 목록으로 만든다.
func _stat_lines(stats: Dictionary) -> Array[String]:
	var total := int(stats["playTime"])
	return [
		"Time  %dmin %dsec" % [total / 60, total % 60],
		"Rounds fired  %d" % stats["fire"],
		"Rounds on target  %d" % stats["onTarget"],
		"Accuracy rate  %.1f%%" % stats["accuracy"],
		"Kill  %d / HeadShot  %d" % [stats["kill"], stats["headshot"]],
	]
