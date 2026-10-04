class_name XopsLines
extends Control
## 선분 묶음을 그린다 (스코프 조준선). 이 노드의 위치가 원점이고, 선분 좌표는 위쪽이 + 다.
## lines 의 각 항목은 x1, y1, x2, y2, color, width 를 가진 사전이다.

var lines: Array = []:
	set(value):
		lines = value
		queue_redraw()


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _draw() -> void:
	for line in lines:
		var from := Vector2(line["x1"], -line["y1"])
		var to := Vector2(line["x2"], -line["y2"])
		draw_line(from, to, line["color"], maxf(1.0, line["width"]))
