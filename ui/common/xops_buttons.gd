class_name XopsButtons
extends RefCounted
## 글자 버튼 묶음의 공용 처리: 그림자와 본문 두 겹의 글자, 색(평소 / 마우스 올림 / 못 누름), 눌림 표시, 클릭 판정.
## 버튼은 누른 자리에서 뗐을 때만 동작한다 (누른 채 벗어나면 취소). 그래서 "누른 순간 그 아래 있던 요소"를 묶음마다 하나 기억한다.
## 메뉴와 OPTION 화면이 각자 하나씩 갖는다.
## 버튼 하나 = {"shadow": XopsText, "main": XopsText, "pivot": 기준점, "x": 기준 x, "y": 기준 y}.

const SHADOW_COLOR := Color(0, 0, 0)
const NORMAL := Color(1, 1, 1)
const HOVER := Color(0, 1, 1)
const DISABLED := Color(0.6, 0.6, 0.6)

# 마우스를 누른 순간 그 아래 있던 요소. 뗄 때 같은 요소 위에 있어야 클릭이 된다.
var press_capture: Object = null
# 스크롤바 같은 것을 끄는 중이면 버튼은 마우스가 올라와도 반응하지 않는다.
var dragging := false


## 그림자와 본문 두 겹으로 된 글자 버튼을 만든다. 판정 사각형은 기준점에서 pivot 정렬로 hit 크기만큼이다.
func text_pair(parent: Control, pivot: Vector2, align: Vector2, x: float, y: float, value: String,
		color: Color, font: Vector2, hit: Vector2) -> Dictionary:
	var shadow := XopsUI.text(parent, pivot, align, value, x + 1, y - 1, font.x, font.y, SHADOW_COLOR)
	var main := XopsUI.text(parent, pivot, align, value, x, y, font.x, font.y, color)
	main.hit_size = hit
	main.hit_pivot = pivot
	return {"shadow": shadow, "main": main, "pivot": pivot, "x": x, "y": y}


## 왼쪽 아래 기준의 버튼 한 줄: 판정은 줄 전체, 글자는 줄의 세로 가운데.
func row_button(parent: Control, y: float, value: String, row: Vector2, font: Vector2) -> Dictionary:
	var slot := text_pair(parent, XopsUI.BOTTOM_LEFT, XopsUI.MIDDLE_LEFT, 0, y + row.y * 0.5, value, NORMAL, font, row)
	(slot["main"] as XopsText).hit_pivot = XopsUI.MIDDLE_LEFT
	return slot


func set_text(slot: Dictionary, value: String) -> void:
	(slot["shadow"] as XopsText).text = value
	(slot["main"] as XopsText).text = value


func set_visible(slot: Dictionary, visible: bool) -> void:
	(slot["shadow"] as XopsText).visible = visible
	(slot["main"] as XopsText).visible = visible


## 누른 순간 이 요소 위였으면 누름을 이 요소가 갖는다. 반환: 지금 누름을 이 요소가 갖고 있는지.
func owns_press(id: Object, hovered: bool, pressed: bool) -> bool:
	if pressed and hovered:
		press_capture = id
	return press_capture == id


## 버튼 하나의 색·눌림 표시를 갱신하고, 클릭됐는지 돌려준다.
func button(slot: Dictionary, pressed: bool, clicked: bool, held: bool, disabled := false) -> bool:
	var main: XopsText = slot["main"]
	var hovered := not dragging and main.is_hovered()
	var owned := owns_press(main, hovered, pressed)

	main.color = DISABLED if disabled else (HOVER if hovered else NORMAL)
	set_pressed(slot, hovered and held and owned and not disabled)
	return clicked and hovered and owned and not disabled


## 눌린 동안 본문을 그림자 자리로 옮겨 눌린 것처럼 보이게 한다.
func set_pressed(slot: Dictionary, pressed: bool) -> void:
	var offset := 1.0 if pressed else 0.0
	XopsUI.move(slot["main"], slot["pivot"], slot["x"] + offset, slot["y"] - offset)
