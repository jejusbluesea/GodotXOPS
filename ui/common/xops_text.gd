class_name XopsText
extends Control
## char.dds 스프라이트 글꼴로 한 줄 글자를 그린다. 글자 코드가 그대로 16×16 칸의 번호다 (열 = 코드 % 16, 행 = 코드 / 16).
## 이 노드의 위치가 글자의 기준점이고, align 이 그 점에 대한 정렬이다 (0,0 = 점의 오른쪽 아래로, 0.5,0.5 = 가운데, 1,1 = 왼쪽 위로).
## hit_size 를 주면 마우스 판정용 사각형을 갖는다. 사각형은 기준점에서 hit_pivot 정렬로 놓인다.

const GRID := 16

## 모든 글자가 함께 쓰는 글꼴 텍스처. XopsUI 가 처음 한 번 읽어 넣는다.
static var font_texture: Texture2D

var text := "":
	set(value):
		if text != value:
			text = value
			queue_redraw()

## 글자 한 칸의 크기.
var char_size := Vector2(16, 16):
	set(value):
		if char_size != value:
			char_size = value
			queue_redraw()

var spacing := 0.0:
	set(value):
		spacing = value
		queue_redraw()

var align := Vector2.ZERO:
	set(value):
		align = value
		queue_redraw()

var color := Color.WHITE:
	set(value):
		if color != value:
			color = value
			queue_redraw()

var hit_size := Vector2.ZERO
var hit_pivot := Vector2.ZERO


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _draw() -> void:
	if font_texture == null or text.is_empty():
		return

	var count := text.length()
	var total := count * char_size.x + (count - 1) * spacing
	var origin := Vector2(-total * align.x, -char_size.y * align.y)
	var cell := Vector2(font_texture.get_width(), font_texture.get_height()) / GRID

	# 이 노드는 크기가 없어서, 기준점이 화면 밖이면 글자가 화면 안에 걸쳐 있어도 통째로 그려지지 않는다.
	# 그릴 범위를 직접 알려 준다 (화면 아래로 삐져나가게 놓는 HUD 테두리 아랫줄이 그 경우다).
	RenderingServer.canvas_item_set_custom_rect(get_canvas_item(), true, Rect2(origin, Vector2(total, char_size.y)))

	for i in count:
		var code := text.unicode_at(i)
		var source := Rect2(Vector2(code % GRID, code / GRID) * cell, cell)
		var target := Rect2(origin + Vector2(i * (char_size.x + spacing), 0), char_size)
		draw_texture_rect_region(font_texture, target, source, color)


## 투명도만 바꾼다.
func set_alpha(alpha: float) -> void:
	color = Color(color.r, color.g, color.b, alpha)


## 마우스가 판정 사각형 위에 있는지.
func is_hovered() -> bool:
	if not is_visible_in_tree():
		return false
	return Rect2(-hit_size * hit_pivot, hit_size).has_point(get_local_mouse_position())
