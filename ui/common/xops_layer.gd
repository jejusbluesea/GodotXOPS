class_name XopsLayer
extends Control
## 화면 요소를 담는 층. 렌더 해상도가 설정마다 달라서(4:3, 16:9, 640×480 ~ 4K) 두 가지 배율 방식을 둔다.
## scaled = true: 화면 높이를 480 으로 보고 확대한다. 너비는 화면비를 따라 늘어난다.
## scaled = false: 화면 픽셀 1:1 에 ui_scale 을 곱한다 (설정의 UIScale).
## 층 안의 좌표는 배율이 적용되기 전의 값이다.

const BASE_HEIGHT := 480.0

var scaled := false
var ui_scale := 1.0


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _ready() -> void:
	refresh()


func _process(_delta: float) -> void:
	refresh()


## 지금 화면 크기에 맞춰 배율과 크기를 다시 맞춘다.
func refresh() -> void:
	var view := get_viewport_rect().size
	var factor := view.y / BASE_HEIGHT if scaled else maxf(ui_scale, 0.01)
	position = Vector2.ZERO
	scale = Vector2(factor, factor)
	size = view / factor
