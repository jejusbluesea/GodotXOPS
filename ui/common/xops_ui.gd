class_name XopsUI
extends RefCounted
## 화면 요소를 만드는 공용 도우미.
## 배치 규칙: 기준점(pivot)은 부모 사각형 안의 한 점이고, x·y 는 그 점에서의 오프셋이다 (+x 오른쪽, +y 위쪽).
## 화면별 수치가 UnityXOPS 의 화면 좌표(아래에서 위로) 기준이라 그 규칙을 그대로 받고 여기서 Godot 좌표로 바꾼다.

# 기준점 / 정렬 (부모 사각형 안의 비율, 위에서 아래로).
const TOP_LEFT := Vector2(0, 0)
const TOP_CENTER := Vector2(0.5, 0)
const TOP_RIGHT := Vector2(1, 0)
const MIDDLE_LEFT := Vector2(0, 0.5)
const CENTER := Vector2(0.5, 0.5)
const MIDDLE_RIGHT := Vector2(1, 0.5)
const BOTTOM_LEFT := Vector2(0, 1)
const BOTTOM_CENTER := Vector2(0.5, 1)
const BOTTOM_RIGHT := Vector2(1, 1)

# 펼침 방식. 펼쳐지는 축의 크기는 "부모 크기 + 값" 이다 (0 이면 꽉 채움, 음수면 안쪽으로 줄임).
enum Stretch { TOP, MIDDLE, BOTTOM, LEFT, CENTER, RIGHT, FULL }

const FONT_PATH := "data/char.dds"

static var _os_font: Font


## 화면 요소를 담을 층을 만든다. order 가 클수록 위에 그려진다.
static func layer(parent: Node, order: int, scaled: bool) -> XopsLayer:
	var node := XopsLayer.new()
	node.scaled = scaled
	node.z_index = order
	if not scaled:
		node.ui_scale = ConfigManager.GetFloat("General", "UIScale", 1.0)
	parent.add_child(node)
	return node


## 스프라이트 글자를 만든다. pivot 은 부모 안의 기준점, align 은 그 점에 대한 글자 정렬이다.
static func text(parent: Control, pivot: Vector2, align: Vector2, value: String,
		x: float, y: float, w: float, h: float, color: Color) -> XopsText:
	if XopsText.font_texture == null:
		XopsText.font_texture = Game.LoadTexture(FONT_PATH)

	var node := XopsText.new()
	node.text = value
	node.char_size = Vector2(w, h)
	node.align = align
	node.color = color
	parent.add_child(node)
	move(node, pivot, x, y)
	return node


## 크기가 없는 요소(글자, 선분)를 기준점에서의 오프셋에 놓는다.
static func move(node: Control, pivot: Vector2, x: float, y: float) -> void:
	node.anchor_left = pivot.x
	node.anchor_right = pivot.x
	node.anchor_top = pivot.y
	node.anchor_bottom = pivot.y
	node.offset_left = x
	node.offset_right = x
	node.offset_top = -y
	node.offset_bottom = -y


## 단색 사각형을 만든다. 사각형의 pivot 쪽 모서리(또는 가운데)가 기준점에 놓인다.
static func panel(parent: Control, pivot: Vector2, x: float, y: float, w: float, h: float, color: Color) -> ColorRect:
	var node := ColorRect.new()
	node.color = color
	node.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(node)
	place(node, pivot, x, y, w, h)
	return node


## 부모에 맞춰 펼쳐지는 단색 사각형을 만든다.
static func panel_stretch(parent: Control, mode: Stretch, x: float, y: float, w: float, h: float, color: Color) -> ColorRect:
	var node := ColorRect.new()
	node.color = color
	node.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(node)
	place_stretch(node, mode, x, y, w, h)
	return node


## 이미지를 만든다. 텍스처는 사각형에 맞춰 늘어난다.
static func image(parent: Control, pivot: Vector2, texture: Texture2D,
		x: float, y: float, w: float, h: float, color := Color.WHITE) -> TextureRect:
	var node := _texture_rect(texture, color)
	parent.add_child(node)
	place(node, pivot, x, y, w, h)
	return node


## 부모에 맞춰 펼쳐지는 이미지를 만든다.
static func image_stretch(parent: Control, mode: Stretch, texture: Texture2D,
		x: float, y: float, w: float, h: float, color := Color.WHITE) -> TextureRect:
	var node := _texture_rect(texture, color)
	parent.add_child(node)
	place_stretch(node, mode, x, y, w, h)
	return node


## 사각형을 기준점에 놓는다.
static func place(node: Control, pivot: Vector2, x: float, y: float, w: float, h: float) -> void:
	node.anchor_left = pivot.x
	node.anchor_right = pivot.x
	node.anchor_top = pivot.y
	node.anchor_bottom = pivot.y
	node.offset_left = x - w * pivot.x
	node.offset_top = -y - h * pivot.y
	node.offset_right = node.offset_left + w
	node.offset_bottom = node.offset_top + h


## 사각형을 부모에 맞춰 펼친다.
static func place_stretch(node: Control, mode: Stretch, x: float, y: float, w: float, h: float) -> void:
	var horizontal := mode == Stretch.TOP or mode == Stretch.MIDDLE or mode == Stretch.BOTTOM or mode == Stretch.FULL
	var vertical := mode == Stretch.LEFT or mode == Stretch.CENTER or mode == Stretch.RIGHT or mode == Stretch.FULL

	if horizontal:
		node.anchor_left = 0.0
		node.anchor_right = 1.0
		node.offset_left = x - w * 0.5
		node.offset_right = x + w * 0.5
	else:
		var ax := 0.0 if mode == Stretch.LEFT else (1.0 if mode == Stretch.RIGHT else 0.5)
		node.anchor_left = ax
		node.anchor_right = ax
		node.offset_left = x - w * ax
		node.offset_right = node.offset_left + w

	if vertical:
		node.anchor_top = 0.0
		node.anchor_bottom = 1.0
		node.offset_top = -y - h * 0.5
		node.offset_bottom = -y + h * 0.5
	else:
		var ay := 0.0 if mode == Stretch.TOP else (1.0 if mode == Stretch.BOTTOM else 0.5)
		node.anchor_top = ay
		node.anchor_bottom = ay
		node.offset_top = -y - h * ay
		node.offset_bottom = node.offset_top + h


## OS 글꼴로 쓰는 글상자를 만든다 (브리핑 본문, 이벤트 메시지, 크레딧). 위치와 크기는 받은 쪽이 place 로 정한다.
static func label(parent: Control, value: String, font_size: int, color: Color) -> Label:
	var node := Label.new()
	node.text = value
	node.mouse_filter = Control.MOUSE_FILTER_IGNORE
	node.add_theme_font_override("font", os_font())
	node.add_theme_font_size_override("font_size", font_size)
	node.add_theme_color_override("font_color", color)
	parent.add_child(node)
	return node


## 본문용 OS 글꼴. 한국어는 맑은 고딕, 일본어는 Yu Gothic, 그 밖에는 Segoe UI 를 먼저 찾는다.
static func os_font() -> Font:
	if _os_font == null:
		var font := SystemFont.new()
		var language := OS.get_locale_language()
		if language == "ko":
			font.font_names = PackedStringArray(["Malgun Gothic", "Segoe UI", "sans-serif"])
		elif language == "ja":
			font.font_names = PackedStringArray(["Yu Gothic", "Meiryo", "Segoe UI", "sans-serif"])
		else:
			font.font_names = PackedStringArray(["Segoe UI", "Malgun Gothic", "Yu Gothic", "sans-serif"])
		_os_font = font
	return _os_font


## 마우스가 사각형 요소 위에 있는지.
static func hovered(node: Control) -> bool:
	if not node.is_visible_in_tree():
		return false
	return Rect2(Vector2.ZERO, node.size).has_point(node.get_local_mouse_position())


## 글자 코드 목록을 문자열로 만든다 (char.dds 의 테두리 글리프 등).
static func glyphs(codes: Array) -> String:
	var result := ""
	for code in codes:
		result += String.chr(code)
	return result


## 0→1 을 duration 초마다 되풀이하는 진행도.
static func cycle(time: float, duration: float) -> float:
	return fmod(time, duration) / duration


static func _texture_rect(texture: Texture2D, color: Color) -> TextureRect:
	var node := TextureRect.new()
	node.texture = texture
	node.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	node.stretch_mode = TextureRect.STRETCH_SCALE
	node.modulate = color
	node.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return node
