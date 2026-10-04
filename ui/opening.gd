extends Node
## 오프닝. 배경 맵을 천천히 훑는 카메라 위로 제작 표시가 차례로 나타났다 사라진다. ESC 나 좌클릭으로 건너뛴다.
## 좌표와 각도는 UnityXOPS 공간 기준이다.

const NEXT_SCENE := "mainmenu"

# ----- 층 (클수록 위) -----
const FADE_ORDER := 0
const TEXT_ORDER := 1
const LETTERBOX_ORDER := 2

# ----- 위아래 검은 띠: 화면 높이(480)에 대한 비율 -----
const LETTERBOX_RATIO := 0.12

# ----- 화면 암전 시각 (초) -----
const FADE_IN_DURATION := 2.0
const FADE_OUT_START := 11.0
const FADE_OUT_DURATION := 4.0
const END_TIME := 15.0 + 1.1

# ----- 카메라 시작 상태 -----
const CAM_FOV := 65.0
const CAM_POSITION := Vector3(0.5, 5.8, -2.9)
const CAM_EULER := Vector3(-12.0, 64.0, 0.0)

# ----- 카메라가 밀리며 도는 연출 -----
# accel_start ~ accel_end: 목표 속도까지 가속, constant_end 까지 등속 (음수면 끝까지), 그 뒤 감쇠.
# target: 목표 속도 (초당). smooth: 원본 33.333fps 기준 프레임당 감쇠 계수.
const POSITION_ANIM := {
	"accel_start": 4.0, "accel_end": 5.0, "constant_end": -1.0,
	"target": Vector3(0.0, -0.1667, -0.2667), "smooth": 0.8,
}
const ROTATION_ANIM := {
	"accel_start": 2.6, "accel_end": 3.6, "constant_end": 5.0,
	"target": Vector3(20.20, -30.0, 0.0), "smooth": 0.8,
}
const REFERENCE_FPS := 33.333

# ----- 제작 표시 -----
# pivot: 화면 기준점이자 글자 정렬. x, y: 기준점에서의 오프셋. size: 글자 크기.
# fade: 나타나기 시작 / 다 나타남 / 사라지기 시작 / 다 사라짐 (초).
const TEXTS := [
	{"text": "Project GodotXOPS", "pivot": XopsUI.BOTTOM_CENTER, "x": 0, "y": 120, "size": Vector2(22, 22),
		"color": Color(1, 1, 1), "fade": [0.5, 1.5, 3.0, 4.0]},
	{"text": "Original by", "pivot": XopsUI.TOP_LEFT, "x": 60, "y": -120, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [4.5, 5.5, 7.5, 8.5]},
	{"text": "nine-two", "pivot": XopsUI.TOP_LEFT, "x": 100, "y": -150, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [5.0, 6.0, 8.0, 9.0]},
	{"text": "TENNKUU", "pivot": XopsUI.TOP_LEFT, "x": 100, "y": -180, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [5.0, 6.0, 8.0, 9.0]},
	{"text": "GodotXOPS by", "pivot": XopsUI.TOP_RIGHT, "x": -100, "y": -270, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [7.0, 8.0, 10.0, 11.0]},
	{"text": "JayTwoGames", "pivot": XopsUI.TOP_RIGHT, "x": -70, "y": -300, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [7.5, 8.5, 10.5, 11.5]},
	{"text": "JJL", "pivot": XopsUI.TOP_RIGHT, "x": -70, "y": -330, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [7.5, 8.5, 10.5, 11.5]},
	{"text": "dlwowlsgod", "pivot": XopsUI.TOP_RIGHT, "x": -70, "y": -360, "size": Vector2(20, 20),
		"color": Color(1, 1, 1), "fade": [7.5, 8.5, 10.5, 11.5]},
	{"text": "X OPERATIONS", "pivot": XopsUI.CENTER, "x": 0, "y": 0, "size": Vector2(22, 22),
		"color": Color(1, 0, 0), "fade": [12.0, 13.0, 15.0, 16.0]},
]

var _time := 0.0
var _finished := false
var _cam_position := CAM_POSITION
var _cam_euler := CAM_EULER
var _position_speed := Vector3.ZERO
var _rotation_speed := Vector3.ZERO
var _fade: ColorRect
var _texts: Array[XopsText] = []


func _ready() -> void:
	InputManager.MouseCursorMode(true, false, true)
	Game.LoadOpening()
	Game.SetSceneCamera(_cam_position, _cam_euler, CAM_FOV)

	var ui := CanvasLayer.new()
	add_child(ui)

	var fade_layer := XopsUI.layer(ui, FADE_ORDER, true)
	_fade = XopsUI.panel_stretch(fade_layer, XopsUI.Stretch.FULL, 0, 0, 0, 0, Color.BLACK)

	var text_layer := XopsUI.layer(ui, TEXT_ORDER, true)
	for data in TEXTS:
		var color: Color = data["color"]
		var size: Vector2 = data["size"]
		_texts.append(XopsUI.text(text_layer, data["pivot"], data["pivot"], data["text"],
			data["x"], data["y"], size.x, size.y, Color(color.r, color.g, color.b, 0.0)))

	var letterbox_layer := XopsUI.layer(ui, LETTERBOX_ORDER, true)
	var thickness := LETTERBOX_RATIO * XopsLayer.BASE_HEIGHT
	XopsUI.panel_stretch(letterbox_layer, XopsUI.Stretch.TOP, 0, 0, 0, thickness, Color.BLACK)
	XopsUI.panel_stretch(letterbox_layer, XopsUI.Stretch.BOTTOM, 0, 0, 0, thickness, Color.BLACK)


func _process(delta: float) -> void:
	if _finished:
		return
	_time += delta

	_position_speed = _update_speed(_position_speed, POSITION_ANIM, delta)
	_rotation_speed = _update_speed(_rotation_speed, ROTATION_ANIM, delta)
	_cam_position += _position_speed * delta
	_cam_euler += _rotation_speed * delta
	Game.SetSceneCamera(_cam_position, _cam_euler, CAM_FOV)

	_fade.color.a = _fade_value(_time)
	for i in _texts.size():
		var fade: Array = TEXTS[i]["fade"]
		_texts[i].set_alpha(_fade_alpha(_time, fade[0], fade[1], fade[2], fade[3]))

	if _time > END_TIME or InputManager.WasPressed("escape") or InputManager.WasClickPressed():
		_finish()


## 속도를 연출 설정에 따라 갱신한다: 가속 → 등속 → 감쇠.
func _update_speed(speed: Vector3, anim: Dictionary, delta: float) -> Vector3:
	var decay := clampf(pow(anim["smooth"], delta * REFERENCE_FPS), 0.0, 1.0)
	var target: Vector3 = anim["target"]

	if _time < anim["accel_start"]:
		return Vector3.ZERO
	if _time < anim["accel_end"]:
		return target + (speed - target) * decay
	if anim["constant_end"] < 0.0 or _time < anim["constant_end"]:
		return target
	return speed * decay


## 화면 암전의 진하기 (0 투명 ~ 1 검정).
func _fade_value(time: float) -> float:
	if time < FADE_IN_DURATION:
		return 1.0 - time / FADE_IN_DURATION
	if time < FADE_OUT_START:
		return 0.0
	if time < FADE_OUT_START + FADE_OUT_DURATION:
		return (time - FADE_OUT_START) / FADE_OUT_DURATION
	return 1.0


## 나타났다 사라지는 글자의 진하기.
func _fade_alpha(time: float, in_start: float, in_end: float, out_start: float, out_end: float) -> float:
	if time < in_start:
		return 0.0
	if time < in_end:
		return (time - in_start) / (in_end - in_start)
	if time < out_start:
		return 1.0
	if time < out_end:
		return 1.0 - (time - out_start) / (out_end - out_start)
	return 0.0


func _finish() -> void:
	_finished = true
	_fade.color.a = 1.0
	Game.ChangeScene(NEXT_SCENE)
