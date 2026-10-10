class_name XopsScriptScreen
extends RefCounted
## 화면 스크립트(SafeGDScript, .sgd)의 실행기. 등록 파일(godotdata/ui/*.json)이 맡겠다고 한 화면을 기본 화면 대신 그 스크립트가 그리게 한다.
## 스크립트는 격리된 채로 돌고 (Game.UiScriptLoad 가 샌드박스에 올린다), 여기서 내준 함수(api)로만 화면 요소를 만든다.
## 스크립트에 노드를 넘기지 않는다. 요소는 번호로 가리키고, 스크립트가 준 값은 전부 여기서 형과 범위를 확인한다.
## 화면마다 다른 것(프레임마다의 값, 화면 전용 함수, 화면 전용 이미지)은 화면 스크립트(ui/*.gd)가 start 에 넘긴다.
## 계약: init(api) 한 번 → build(ctx) 한 번 → frame(v, delta) 를 프레임마다. 좌표는 XopsUI 와 같다 (기준점에서의 오프셋, +y 위).
## 스크립트가 실패하면(오류, 실행 예산 초과, 한도 초과) failed() 가 참이 되고, 화면은 기본 화면으로 되돌린다.

# 요소(층 포함) 수의 상한.
const MAX_ITEMS := 512
# 함수 한 번(init, build, frame)이 부를 수 있는 API 호출 수.
const MAX_CALLS := 2000
const MAX_TEXT_LENGTH := 512
# OS 글꼴 글상자는 브리핑 본문이나 크레딧처럼 긴 글을 담는다.
const MAX_LABEL_LENGTH := 16384
const MAX_LINES := 64
# 좌표와 크기는 이 범위로 자른다.
const COORD_LIMIT := 100000.0
const FONT_SIZE_LIMIT := 256
const LAYER_ORDER_LIMIT := 100

const KIND_LAYER := "layer"
const KIND_TEXT := "text"
const KIND_LABEL := "label"
const KIND_RECT := "rect"
const KIND_IMAGE := "image"
const KIND_LINES := "lines"

const STRETCH_NAMES := {
	"top": XopsUI.Stretch.TOP, "middle": XopsUI.Stretch.MIDDLE, "bottom": XopsUI.Stretch.BOTTOM,
	"left": XopsUI.Stretch.LEFT, "center": XopsUI.Stretch.CENTER, "right": XopsUI.Stretch.RIGHT, "full": XopsUI.Stretch.FULL,
}
const SOURCE_NONE := "none"
const SOURCE_SCOPE := "scope"
const SOURCE_WEAPON_VIEW := "weapon_view"
# 화면을 다녀와도 남는 저장 칸의 한도 (화면마다 키 32개, 글자는 256자).
const SAVED_KEYS := 32
const SAVED_TEXT_LENGTH := 256
const FIT_REFERENCE_SIZE := 16

# 화면 이름 → 스크립트가 save 로 남긴 값. 화면을 떠났다 돌아와도 남는다 (게임을 끄면 사라진다).
static var s_saved := {}

## 스크립트에 넘기는 함수 표. 화면이 자기 함수를 더 넣을 수 있다 (start 의 extra).
var api := {}

var _root: Node
var _node: Node
var _items := {}
var _next_id := 1
var _calls := 0
var _failure := ""
var _key_codes := {}
var _weapon_view_used := false
var _screen := ""
# 화면이 내주는 이미지: 이름 → 텍스처를 돌려주는 Callable. 스크립트는 요소의 source 에 그 이름을 적는다.
var _sources := {}
var _frame_count := 0
var _frame_usec := 0


## 그 화면을 등록한 스크립트가 있으면 올려서 init 과 build 를 부른다.
## root: 층을 붙일 노드. extra: 화면이 더 내주는 함수 (이름 → Callable). ctx: build 에 넘길 사전.
## sources: 화면이 내주는 이미지 (이름 → 텍스처를 돌려주는 Callable).
## 반환: 실행기. 등록이 없거나 올리지 못했거나 init / build 에서 실패했으면 null (기본 화면을 쓴다).
static func start(root: Node, screen: String, extra: Dictionary, ctx: Dictionary, sources := {}) -> XopsScriptScreen:
	var node: Node = Game.UiScriptLoad(screen)
	if node == null:
		return null

	var host := XopsScriptScreen.new()
	host._root = root
	host._node = node
	host._screen = screen
	host._sources = sources
	ctx["saved"] = (s_saved.get(screen, {}) as Dictionary).duplicate()
	host._build_api(extra)
	# 화면을 만드는 호출은 요소를 한꺼번에 만들므로 더 큰 실행 예산으로 돌린다.
	Game.UiScriptSetBuilding(screen, true)
	if not node.has_method("frame"):
		host._fail("frame(v, delta) is missing")
	elif node.has_method("init"):
		host._invoke("init", [host.api])
	if not host.failed() and node.has_method("build"):
		host._invoke("build", [host._with_screen(ctx)])
	if host.failed():
		host.stop()
		return null
	Game.UiScriptSetBuilding(screen, false)
	return host


## frame(v, delta) 를 부른다. v 에는 화면 크기 값이 더해진다. 반환: 실패했으면 false.
func frame(values: Dictionary, delta: float) -> bool:
	if failed():
		return false
	var began := Time.get_ticks_usec()
	var ok := _invoke("frame", [_with_screen(values), delta])
	_frame_usec += Time.get_ticks_usec() - began
	_frame_count += 1
	return ok


## frame 한 번에 든 평균 시간 (마이크로초). 개발용 인자 --ui-script-stats 가 종료할 때 찍는다.
func average_frame_usec() -> float:
	return float(_frame_usec) / maxi(_frame_count, 1)


func failed() -> bool:
	return not _failure.is_empty()


## 스크립트가 준 [x, y, z] 배열을 벡터로 바꾼다. 형식이 맞지 않으면 fallback.
static func vector(value, fallback: Vector3) -> Vector3:
	if not value is Array or (value as Array).size() != 3:
		return fallback
	for part in value:
		if not (part is int or part is float) or not is_finite(float(part)):
			return fallback
	return Vector3(value[0], value[1], value[2])


## 스크립트에 그 이름의 함수가 있으면 인자 없이 부른다 (디버그 콘솔의 restart 를 알릴 때 쓴다). 반환: 실패했으면 false.
func call_optional(function: String) -> bool:
	if failed():
		return false
	if not _node.has_method(function):
		return true
	return _invoke(function, [])


## 설정의 UIScale 을 픽셀 층들에 다시 적용한다 (OPTION 에서 값을 바꿨을 때).
func apply_ui_scale() -> void:
	var ui_scale: float = ConfigManager.GetFloat("General", "UIScale", 1.0)
	for id in _items:
		var item: Dictionary = _items[id]
		if item["kind"] == KIND_LAYER and not (item["node"] as XopsLayer).scaled:
			(item["node"] as XopsLayer).ui_scale = ui_scale


## 스크립트가 만든 것을 전부 지우고 샌드박스를 내린다.
func stop() -> void:
	for id in _items.keys():
		var item: Dictionary = _items[id]
		if item["kind"] == KIND_LAYER:
			var layer: Node = item["node"]
			if is_instance_valid(layer):
				if layer.get_parent() != null:
					layer.get_parent().remove_child(layer)
				layer.free()
	_items.clear()
	if _weapon_view_used:
		Game.FreeWeaponView()
		_weapon_view_used = false
	_node = null
	Game.UiScriptFree(_screen)


# ============================================================
#  호출
# ============================================================

func _invoke(function: String, arguments: Array) -> bool:
	_calls = 0
	var before: int = Game.UiScriptExceptions(_screen)
	_node.callv(function, arguments)
	if Game.UiScriptExceptions(_screen) != before:
		_fail("script error or execution limit in %s()" % function)
	return not failed()


func _fail(message: String) -> void:
	if _failure.is_empty():
		_failure = message
		Game.UiScriptLogError(_screen, message)


## API 함수의 첫머리에서 부른다. 호출 수를 세고, 이미 실패했으면 false.
func _enter() -> bool:
	if failed():
		return false
	_calls += 1
	if _calls > MAX_CALLS:
		_fail("too many API calls in one function")
		return false
	return true


## 화면 크기 값을 더한다: 픽셀 층(pixel_w, pixel_h)과 확대 층(scaled_w, scaled_h)의 크기, UIScale.
## 마우스 왼쪽 버튼의 상태도 더한다 (화면의 클릭은 발사 키 바인딩과 무관하다).
func _with_screen(values: Dictionary) -> Dictionary:
	var view := _root.get_viewport().get_visible_rect().size
	var ui_scale := maxf(ConfigManager.GetFloat("General", "UIScale", 1.0), 0.01)
	values["pixel_w"] = view.x / ui_scale
	values["pixel_h"] = view.y / ui_scale
	values["scaled_w"] = view.x / maxf(view.y, 1.0) * XopsLayer.BASE_HEIGHT
	values["scaled_h"] = XopsLayer.BASE_HEIGHT
	values["ui_scale"] = ui_scale
	values["click_pressed"] = InputManager.WasClickPressed()
	values["click_released"] = InputManager.WasClickReleased()
	values["click_held"] = InputManager.IsClickPressed()
	return values


func _build_api(extra: Dictionary) -> void:
	api = {
		"layer": Callable(self, "_api_layer"),
		"text": Callable(self, "_api_create").bind(KIND_TEXT),
		"label": Callable(self, "_api_create").bind(KIND_LABEL),
		"rect": Callable(self, "_api_create").bind(KIND_RECT),
		"image": Callable(self, "_api_create").bind(KIND_IMAGE),
		"lines": Callable(self, "_api_create").bind(KIND_LINES),
		"set": Callable(self, "_api_set"),
		"set_many": Callable(self, "_api_set_many"),
		"remove": Callable(self, "_api_remove"),
		"chr": Callable(self, "_api_chr"),
		"config_number": Callable(self, "_api_config_number"),
		"config_bool": Callable(self, "_api_config_bool"),
		"pressed": Callable(self, "_api_pressed"),
		"key_pressed": Callable(self, "_api_key_pressed"),
		"log": Callable(self, "_api_log"),
		"mouse": Callable(self, "_api_mouse"),
		"hovered": Callable(self, "_api_hovered"),
		"save": Callable(self, "_api_save"),
	}
	for name in extra:
		api[name] = Callable(self, "_api_extra").bind(extra[name])


## 화면이 내준 함수를 호출 수를 세면서 부른다. 인자는 사전 하나다 (없으면 빈 사전).
func _api_extra(arguments = null, target: Callable = Callable()) -> Variant:
	# 스크립트가 인자 없이 부르면 묶어 둔 함수가 첫 자리로 온다.
	if arguments is Callable:
		target = arguments
		arguments = null
	if not _enter() or not target.is_valid():
		return null
	return target.call(arguments if arguments is Dictionary else {})


# ============================================================
#  요소
# ============================================================

## 층을 만든다. order 가 클수록 위. scaled 가 참이면 화면 높이 480 기준으로 확대하고, 거짓이면 픽셀 × UIScale 이다.
func _api_layer(order = 0, scaled = false) -> int:
	if not _enter() or not _has_room():
		return 0
	var layer := XopsUI.layer(_root, clampi(_int(order), -LAYER_ORDER_LIMIT, LAYER_ORDER_LIMIT), _bool(scaled))
	return _register(KIND_LAYER, layer, 0)


func _api_create(layer_id = 0, props = null, kind: String = "") -> int:
	if not _enter() or not _has_room():
		return 0
	var layer_item: Dictionary = _items.get(_int(layer_id), {})
	if layer_item.is_empty() or layer_item["kind"] != KIND_LAYER:
		return 0

	var layer: XopsLayer = layer_item["node"]
	var node: Control
	match kind:
		KIND_TEXT:
			node = XopsUI.text(layer, XopsUI.TOP_LEFT, XopsUI.TOP_LEFT, "", 0, 0, 16, 16, Color.WHITE)
		KIND_LABEL:
			node = XopsUI.label(layer, "", 16, Color.WHITE)
		KIND_RECT:
			node = XopsUI.panel(layer, XopsUI.TOP_LEFT, 0, 0, 0, 0, Color.WHITE)
		KIND_IMAGE:
			node = XopsUI.image(layer, XopsUI.TOP_LEFT, null, 0, 0, 0, 0)
		KIND_LINES:
			node = XopsLines.new()
			layer.add_child(node)
		_:
			return 0

	var id := _register(kind, node, _int(layer_id))
	if props is Dictionary:
		_apply(_items[id], props)
	return id


func _api_set(id = 0, props = null) -> bool:
	if not _enter():
		return false
	var item: Dictionary = _items.get(_int(id), {})
	if item.is_empty() or item["kind"] == KIND_LAYER or not props is Dictionary:
		return false
	_apply(item, props)
	return true


## 여러 요소를 한 번에 고친다. changes 는 요소 번호 → 값 사전이다.
func _api_set_many(changes = null) -> int:
	if not _enter() or not changes is Dictionary:
		return 0
	var count := 0
	for id in changes:
		var item: Dictionary = _items.get(_int(id), {})
		var props = changes[id]
		if item.is_empty() or item["kind"] == KIND_LAYER or not props is Dictionary:
			continue
		_apply(item, props)
		count += 1
	return count


## 요소나 층을 지운다. 층을 지우면 그 안의 요소도 함께 지워진다.
func _api_remove(id = 0) -> bool:
	if not _enter():
		return false
	var key := _int(id)
	var item: Dictionary = _items.get(key, {})
	if item.is_empty():
		return false
	if item["kind"] == KIND_LAYER:
		for other in _items.keys():
			if _items[other]["layer"] == key:
				_items.erase(other)
	var node: Node = item["node"]
	_items.erase(key)
	if is_instance_valid(node):
		if node.get_parent() != null:
			node.get_parent().remove_child(node)
		node.free()
	return true


func _has_room() -> bool:
	if _items.size() >= MAX_ITEMS:
		_fail("too many screen elements (limit %d)" % MAX_ITEMS)
		return false
	return true


func _register(kind: String, node: Control, layer_id: int) -> int:
	var id := _next_id
	_next_id += 1
	_items[id] = {
		"kind": kind, "node": node, "layer": layer_id,
		"pivot": XopsUI.TOP_LEFT, "x": 0.0, "y": 0.0, "w": 0.0, "h": 0.0, "stretch": -1,
		"rgb": Color.WHITE, "alpha": 1.0, "hit": false, "fit": false, "fit_min": 1, "fit_max": 80, "fit_margin": 0.0,
	}
	return id


## 값 사전을 요소에 넣는다. 모르는 키와 그 요소에 맞지 않는 키는 무시한다.
func _apply(item: Dictionary, props: Dictionary) -> void:
	var kind: String = item["kind"]
	var node: Control = item["node"]
	var placed := false
	var colored := false
	var source = null
	var fit := false

	for key in props:
		var value = props[key]
		match key:
			"visible":
				node.visible = _bool(value)
			"x", "y", "w", "h":
				item[key] = _number(value)
				placed = true
			"pivot":
				item["pivot"] = _pivot(value)
				placed = true
			"stretch":
				item["stretch"] = STRETCH_NAMES.get(value, -1) if value is String else -1
				placed = true
			"color":
				item["rgb"] = Color.hex((_int(value) & 0xFFFFFF) << 8 | 0xFF)
				colored = true
			"alpha":
				item["alpha"] = clampf(_number(value), 0.0, 1.0)
				colored = true
			"text":
				if kind == KIND_TEXT or kind == KIND_LABEL:
					node.set("text", _text(value, MAX_LABEL_LENGTH if kind == KIND_LABEL else MAX_TEXT_LENGTH))
					fit = true
			"hit":
				item["hit"] = _bool(value)
			"hit_w":
				if kind == KIND_TEXT:
					(node as XopsText).hit_size = Vector2(_number(value), (node as XopsText).hit_size.y)
					item["hit"] = true
			"hit_h":
				if kind == KIND_TEXT:
					(node as XopsText).hit_size = Vector2((node as XopsText).hit_size.x, _number(value))
					item["hit"] = true
			"hit_pivot":
				if kind == KIND_TEXT:
					(node as XopsText).hit_pivot = _pivot(value)
			"valign":
				if kind == KIND_LABEL:
					(node as Label).vertical_alignment = clampi(_int(value), 0, 2) as VerticalAlignment
			"wrap":
				if kind == KIND_LABEL:
					(node as Label).autowrap_mode = TextServer.AUTOWRAP_WORD_SMART if _bool(value) else TextServer.AUTOWRAP_OFF
			"clip":
				if kind == KIND_LABEL:
					(node as Label).clip_text = _bool(value)
			"line_spacing":
				if kind == KIND_LABEL:
					node.add_theme_constant_override("line_spacing", clampi(_int(value), -FONT_SIZE_LIMIT, FONT_SIZE_LIMIT))
			"fit":
				if kind == KIND_LABEL:
					item["fit"] = _bool(value)
					fit = true
			"fit_min", "fit_max":
				if kind == KIND_LABEL:
					item[key] = clampi(_int(value), 1, FONT_SIZE_LIMIT)
					fit = true
			"fit_margin":
				if kind == KIND_LABEL:
					item["fit_margin"] = _number(value)
					fit = true
			"glyphs":
				if kind == KIND_TEXT and value is Array:
					var codes: Array = []
					for code in (value as Array).slice(0, MAX_TEXT_LENGTH):
						codes.append(clampi(_int(code), 0, 255))
					(node as XopsText).text = XopsUI.glyphs(codes)
			"font_w":
				if kind == KIND_TEXT:
					(node as XopsText).char_size = Vector2(clampf(_number(value), 0.0, FONT_SIZE_LIMIT), (node as XopsText).char_size.y)
			"font_h":
				if kind == KIND_TEXT:
					(node as XopsText).char_size = Vector2((node as XopsText).char_size.x, clampf(_number(value), 0.0, FONT_SIZE_LIMIT))
			"align":
				if kind == KIND_TEXT:
					(node as XopsText).align = _pivot(value)
			"size":
				if kind == KIND_LABEL:
					node.add_theme_font_size_override("font_size", clampi(_int(value), 1, FONT_SIZE_LIMIT))
			"halign":
				if kind == KIND_LABEL:
					(node as Label).horizontal_alignment = clampi(_int(value), 0, 2) as HorizontalAlignment
			"source":
				source = value

	if colored:
		_apply_color(item)
	if placed:
		_place(item)
	if source != null:
		_apply_source(item, source)
	# 글상자의 크기는 다음 프레임에 정해지므로 글자 크기 맞추기는 미룬다.
	if item["fit"] and (fit or placed):
		_fit_label.call_deferred(item)


## 글상자의 글자 크기를 상자 안에 들어가는 가장 큰 값으로 맞춘다 (fit 을 켠 OS 글자).
func _fit_label(item: Dictionary) -> void:
	var label := item["node"] as Label
	if not is_instance_valid(label) or not label.is_inside_tree():
		return
	var margin: float = item["fit_margin"]
	# 글상자의 size 는 글이 넘치면 글에 맞춰 늘어나 있으므로, 놓인 자리에서 크기를 다시 구한다.
	var box := Vector2(item["w"], item["h"])
	var parent_size := (label.get_parent() as Control).size
	var mode: int = item["stretch"]
	if mode == XopsUI.Stretch.TOP or mode == XopsUI.Stretch.MIDDLE or mode == XopsUI.Stretch.BOTTOM or mode == XopsUI.Stretch.FULL:
		box.x += parent_size.x
	if mode == XopsUI.Stretch.LEFT or mode == XopsUI.Stretch.CENTER or mode == XopsUI.Stretch.RIGHT or mode == XopsUI.Stretch.FULL:
		box.y += parent_size.y
	var available := box - Vector2(margin, margin) * 2.0
	var measured := XopsUI.os_font().get_multiline_string_size(label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, FIT_REFERENCE_SIZE)
	if measured.x <= 0.0 or measured.y <= 0.0 or available.x <= 0.0 or available.y <= 0.0:
		return
	var low: int = item["fit_min"]
	var high: int = maxi(low, item["fit_max"])
	var size := clampi(int(minf(available.x / measured.x, available.y / measured.y) * FIT_REFERENCE_SIZE), low, high)

	# 글상자의 실제 줄 높이는 위에서 잰 것보다 조금 클 수 있다. 넘치면 들어갈 때까지 줄인다.
	label.add_theme_font_size_override("font_size", size)
	while size > low and (label.get_minimum_size().y > available.y or label.get_minimum_size().x > available.x):
		size -= 1
		label.add_theme_font_size_override("font_size", size)


func _apply_color(item: Dictionary) -> void:
	var rgb: Color = item["rgb"]
	var color := Color(rgb.r, rgb.g, rgb.b, item["alpha"])
	var node: Control = item["node"]
	match item["kind"]:
		KIND_TEXT:
			(node as XopsText).color = color
		KIND_RECT:
			(node as ColorRect).color = color
		KIND_LABEL:
			node.add_theme_color_override("font_color", rgb)
			node.modulate.a = color.a
		KIND_IMAGE:
			node.modulate = color


func _place(item: Dictionary) -> void:
	var node: Control = item["node"]
	var kind: String = item["kind"]
	if kind == KIND_TEXT or kind == KIND_LINES:
		XopsUI.move(node, item["pivot"], item["x"], item["y"])
	elif item["stretch"] >= 0:
		XopsUI.place_stretch(node, item["stretch"], item["x"], item["y"], item["w"], item["h"])
	else:
		XopsUI.place(node, item["pivot"], item["x"], item["y"], item["w"], item["h"])


## 이미지와 선의 내용을 정한다. 이미지: 등록 파일의 이미지 번호, "scope"(지금 스코프의 그림), "weapon_view"(3D 무기 표시), "none".
## 선: "scope"(지금 스코프의 조준선), "none".
func _apply_source(item: Dictionary, source) -> void:
	var node: Control = item["node"]
	if item["kind"] == KIND_IMAGE:
		var texture: Texture2D = null
		if source is int:
			texture = Game.UiScriptImage(_screen, source)
		elif source is String and source == SOURCE_SCOPE:
			var scope: Dictionary = Game.ActiveScope()
			if not scope.is_empty():
				texture = Game.LoadTexture(scope["texturePath"])
		elif source is String and _sources.has(source):
			texture = (_sources[source] as Callable).call() as Texture2D
		elif source is String and source == SOURCE_WEAPON_VIEW:
			# 표시 크기 × UIScale 로 렌더링한다 (표시 크기 그대로면 확대될 때 계단이 진다).
			var side := maxf(item["w"], item["h"]) * maxf(1.0, ConfigManager.GetFloat("General", "UIScale", 1.0))
			texture = Game.CreateWeaponView(clampi(ceili(side), 16, 2048))
			_weapon_view_used = true
		(node as TextureRect).texture = texture
	elif item["kind"] == KIND_LINES:
		var lines: Array = []
		if source is String and source == SOURCE_SCOPE:
			var scope: Dictionary = Game.ActiveScope()
			if not scope.is_empty():
				lines = (scope["lines"] as Array).slice(0, MAX_LINES)
		(node as XopsLines).lines = lines


# ============================================================
#  조회
# ============================================================

## char.dds 의 글자 한 칸을 가리키는 글자 (코드 0 에서 255). 테두리 같은 글리프를 글자열에 섞을 때 쓴다.
func _api_chr(code = 0) -> String:
	if not _enter():
		return ""
	return String.chr(clampi(_int(code), 0, 255))


func _api_config_number(section = "", key = "") -> float:
	if not _enter() or not section is String or not key is String:
		return 0.0
	return ConfigManager.GetFloat(section, key, 0.0)


func _api_config_bool(section = "", key = "") -> bool:
	if not _enter() or not section is String or not key is String:
		return false
	return ConfigManager.GetBool(section, key, false)


## 입력 액션(설정의 키 바인딩 이름)이 이번 프레임에 눌렸는지.
func _api_pressed(action = "") -> bool:
	if not _enter() or not action is String:
		return false
	return InputManager.WasPressed(action)


## 키(Godot 키 이름, 예: "F2")가 이번 프레임에 눌렸는지.
func _api_key_pressed(key_name = "") -> bool:
	if not _enter() or not key_name is String:
		return false
	if not _key_codes.has(key_name):
		_key_codes[key_name] = OS.find_keycode_from_string(key_name)
	var code: int = _key_codes[key_name]
	return code != KEY_NONE and InputManager.WasKeyPressed(code)


func _api_log(message = "") -> void:
	if _enter():
		Game.UiScriptLog(_screen, _text(message))


## 마우스의 자리를 그 층의 좌표로 돌려준다: 층의 왼쪽 위(기준점 0)에서의 오프셋이고 아래로 갈수록 y 가 작아진다.
func _api_mouse(layer_id = 0) -> Dictionary:
	if not _enter():
		return {}
	var item: Dictionary = _items.get(_int(layer_id), {})
	if item.is_empty() or item["kind"] != KIND_LAYER:
		return {"x": 0.0, "y": 0.0}
	var at := (item["node"] as Control).get_local_mouse_position()
	return {"x": at.x, "y": -at.y}


## 마우스가 올라가 있는 요소들의 번호. 판정이 있는 요소만 본다: hit 를 켠 사각형·이미지, hit_w / hit_h 를 준 스프라이트 글자.
func _api_hovered() -> Array:
	var result: Array = []
	if not _enter():
		return result
	for id in _items:
		var item: Dictionary = _items[id]
		if not item["hit"]:
			continue
		var node: Control = item["node"]
		if item["kind"] == KIND_TEXT:
			if (node as XopsText).is_hovered():
				result.append(id)
		elif XopsUI.hovered(node):
			result.append(id)
	return result


## 화면을 떠났다 돌아와도 남길 값을 저장한다 (build 의 ctx["saved"] 로 돌아온다). 수, 참·거짓, 글자만 받는다.
func _api_save(values = null) -> bool:
	if not _enter() or not values is Dictionary:
		return false
	var kept := {}
	for key in values:
		if kept.size() >= SAVED_KEYS or not key is String:
			continue
		var value = values[key]
		if value is int or value is bool or (value is float and is_finite(value)):
			kept[key] = value
		elif value is String:
			kept[key] = (value as String).left(SAVED_TEXT_LENGTH)
	s_saved[_screen] = kept
	return true


# ============================================================
#  값 확인
# ============================================================

func _number(value) -> float:
	if value is int or value is float:
		var number := float(value)
		if is_finite(number):
			return clampf(number, -COORD_LIMIT, COORD_LIMIT)
	return 0.0


func _int(value) -> int:
	if value is int:
		return value
	if value is float and is_finite(value):
		return int(value)
	return 0


func _bool(value) -> bool:
	return value if value is bool else (value != 0 if value is int else false)


func _text(value, limit := MAX_TEXT_LENGTH) -> String:
	if value is String:
		return (value as String).left(limit)
	if value is int or value is float or value is bool:
		return str(value)
	return ""


## 기준점 번호(0 에서 8, 3×3. 0 왼쪽 위, 4 가운데, 8 오른쪽 아래)를 비율로 바꾼다.
func _pivot(value) -> Vector2:
	var index := clampi(_int(value), 0, 8)
	return Vector2((index % 3) * 0.5, (index / 3) * 0.5)
