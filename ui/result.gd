extends Node
## 결과. 미션 성공·실패와 플레이어 통계를 보여 준다. F12 는 같은 미션 재시작, ESC 나 좌클릭은 메뉴로 돌아간다.

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
const TITLE_ALPHA := 0.012

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


func _ready() -> void:
	InputManager.MouseCursorMode(true, false, false)

	var ui := CanvasLayer.new()
	add_child(ui)

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
	_time += delta

	_heading.set_alpha(lerpf(HEADING["alpha_from"], HEADING["alpha_to"], XopsUI.cycle(_time, HEADING["duration"])))

	if _time < INPUT_ALLOW_TIME:
		return

	if InputManager.WasKeyPressed(RESTART_KEY):
		_finished = true
		if Game.ReloadMission():
			Game.ChangeScene(GAME_SCENE)
		else:
			Game.UnloadMission()
			Game.ChangeScene(MENU_SCENE)
	elif InputManager.WasPressed("escape") or InputManager.WasPressed("fire"):
		_finished = true
		Game.UnloadMission()
		Game.ChangeScene(MENU_SCENE)


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
