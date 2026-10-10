class_name XopsOption
extends RefCounted
## OPTION(설정) 화면을 어느 화면 위에든 덮어 띄우는 창구. 지금은 메뉴가 쓰고, 나중에 다른 화면(일시정지 메뉴 등)도 같은 호출로 연다.
## 여는 쪽은 open() 을 부르고, 열려 있는 동안 프레임마다 update() 를 부르며 자기 입력은 받지 않는다. BACK·ESC·되돌리기는 여기서 처리한다.
## "option" 화면을 등록한 화면 스크립트(.sgd)가 있으면 그것이 그리고, 없거나 실패하면 기본 화면(MenuOption)이 그린다.
## 값은 바꾸는 즉시 ConfigManager 에 들어가고 SAVE 가 파일에 쓴다. 닫으면(BACK, ESC) 저장하지 않은 변경을 되돌린다.

const SCREEN_NAME := "option"
const BACK_TEXT := "< BACK >"
const BACK_BG := {"x": 5, "y": 14, "w": 136, "h": 25, "color": Color(0, 0, 0, 0.5)}
const BACK_FONT := Vector2(17, 22)
# 스크립트가 고칠 수 없는 설정: 콘솔과 스크립트를 허용할지는 유저가 파일에서만 바꾼다.
const PROTECTED_KEYS := ["allowconsole", "alloweventscript", "allowuiscript"]
# 해상도는 고를 수 있는 목록의 자리로만 바꾼다 (resolution_set).
const RESOLUTION_SECTION := "Graphic"
const RESOLUTION_KEY := "resolution"

var _root: Node
var _order := 0
var _on_scale: Callable
var _open := false

# 기본 화면. 처음 필요할 때 만든다.
var _layer: XopsLayer
var _buttons: XopsButtons
var _builtin: MenuOption
var _back_bg: ColorRect
var _back_slot: Dictionary

# 화면 스크립트. 열려 있는 동안만 올라가 있다.
var _script: XopsScriptScreen
var _close_requested := false
var _listening := ""
var _captured := false


## root: 층을 붙일 노드. order: 기본 화면의 층 순서. on_scale: UIScale 이 바뀌었을 때(SAVE, 되돌리기) 여는 쪽이 자기 층을 맞추도록 부르는 함수.
func _init(root: Node, order: int, on_scale: Callable) -> void:
	_root = root
	_order = order
	_on_scale = on_scale


func is_open() -> bool:
	return _open


## 화면을 연다. tab 을 주면 그 탭에서 시작한다 (General, Input, Graphic, Sound).
func open(tab := "") -> void:
	if _open:
		return
	_open = true
	_close_requested = false
	_listening = ""
	_script = XopsScriptScreen.start(_root, SCREEN_NAME, _script_api(), {"tab": tab, "sections": Array(ConfigManager.GetSectionNames())})
	if _script == null:
		_show_builtin(tab)


## 저장하지 않은 변경을 되돌리고 닫는다.
func close() -> void:
	if not _open:
		return
	_open = false
	ConfigManager.RevertToSaved()
	_scale_changed()
	if _script != null:
		_script.stop()
		_script = null
	if _builtin != null:
		_builtin.set_visible(false)
		_back_bg.visible = false


## 한 프레임을 처리한다. 열려 있는 동안 여는 쪽이 부른다.
func update(delta: float) -> void:
	if not _open:
		return

	if _script != null:
		_update_script(delta)
		return

	var pressed: bool = InputManager.WasClickPressed()
	var clicked: bool = InputManager.WasClickReleased()
	var held: bool = InputManager.IsClickPressed()
	if pressed:
		_buttons.press_capture = null
	if _buttons.button(_back_slot, pressed, clicked, held) or InputManager.WasPressed("escape"):
		close()
	else:
		_builtin.update(delta, pressed, clicked, held)


## 설정의 UIScale 을 이 화면의 층에 다시 적용한다.
func apply_ui_scale() -> void:
	if _layer != null:
		_layer.ui_scale = ConfigManager.GetFloat("General", "UIScale", 1.0)
	if _script != null:
		_script.apply_ui_scale()


## 화면을 떠날 때: 열려 있으면 닫고 스크립트를 내린다.
func stop() -> void:
	close()


func _show_builtin(tab: String) -> void:
	if _builtin == null:
		_layer = XopsUI.layer(_root, _order, false)
		_buttons = XopsButtons.new()
		_back_bg = XopsUI.panel(_layer, XopsUI.BOTTOM_LEFT, BACK_BG["x"], BACK_BG["y"], BACK_BG["w"], BACK_BG["h"], BACK_BG["color"])
		_back_slot = _buttons.row_button(_back_bg, 0, BACK_TEXT, Vector2(BACK_BG["w"], BACK_BG["h"]), BACK_FONT)
		_builtin = MenuOption.new(_buttons, _layer, Callable(self, "_scale_changed"))
	_back_bg.visible = true
	_builtin.set_visible(true)
	if not tab.is_empty():
		_builtin.select_tab(tab)


## UIScale 이 바뀌었을 수 있을 때: 이 화면의 층과 여는 쪽의 층을 맞춘다.
func _scale_changed() -> void:
	apply_ui_scale()
	if _on_scale.is_valid():
		_on_scale.call()


# ============================================================
#  화면 스크립트
# ============================================================

func _update_script(delta: float) -> void:
	# 키 바인딩을 기다리는 중이면 처음 눌린 키나 마우스 버튼을 여기서 받아 넣는다. 스크립트는 받는 중인지와 받았는지만 안다.
	_captured = false
	if not _listening.is_empty():
		var path: String = InputManager.GetFirstPressedKeyPath()
		if not path.is_empty():
			InputManager.SetActionBinding(_listening, path)
			_listening = ""
			_captured = true

	var ok := _script.frame({"bind_listening": _listening, "bind_captured": _captured}, delta)
	if not _open:
		return
	if not ok:
		# 스크립트가 실패하면 바꾸던 값을 되돌리고 기본 화면으로 잇는다.
		_script.stop()
		_script = null
		_listening = ""
		ConfigManager.RevertToSaved()
		_scale_changed()
		_show_builtin("")
	elif _close_requested:
		close()


## OPTION 이 화면 스크립트에 더 내주는 함수. 인자는 사전 하나다.
func _script_api() -> Dictionary:
	return {
		"config_set": Callable(self, "_script_config_set"),
		"config_range": Callable(self, "_script_config_range"),
		"config_save": Callable(self, "_script_config_save"),
		"config_reset": Callable(self, "_script_config_reset"),
		"resolution": Callable(self, "_script_resolution"),
		"resolution_set": Callable(self, "_script_resolution_set"),
		"actions": Callable(self, "_script_actions"),
		"binding": Callable(self, "_script_binding"),
		"bind_listen": Callable(self, "_script_bind_listen"),
		"close": Callable(self, "_script_close"),
	}


func _script_text(arguments: Dictionary, key: String) -> String:
	var value = arguments.get(key, "")
	return value if value is String else ""


## 설정 값 하나를 바꾼다 (SAVE 전까지는 파일에 쓰지 않는다). 형이 맞아야 하고 범위는 게임이 자른다.
## 콘솔·스크립트 허용 설정과 해상도는 여기서 바꿀 수 없다. 반환: 바꿨으면 true.
func _script_config_set(arguments: Dictionary) -> bool:
	var section := _script_text(arguments, "section")
	var key := _script_text(arguments, "key")
	var value = arguments.get("value")
	if key.to_lower() in PROTECTED_KEYS:
		return false
	if section.to_lower() == RESOLUTION_SECTION.to_lower() and key.to_lower() == RESOLUTION_KEY.to_lower():
		return false

	match ConfigManager.GetSettingType(section, key):
		"bool":
			if not value is bool:
				return false
			ConfigManager.SetBool(section, key, value)
		"int":
			if not (value is int or (value is float and is_finite(value))):
				return false
			ConfigManager.SetInt(section, key, int(value))
		"float":
			if not (value is int or value is float) or not is_finite(float(value)):
				return false
			ConfigManager.SetFloat(section, key, float(value))
		_:
			return false
	return true


## 설정의 허용 범위: min, max. 범위는 매번 다시 묻는다 (UIScale 의 상한이 해상도를 따라 바뀐다).
func _script_config_range(arguments: Dictionary) -> Dictionary:
	var section := _script_text(arguments, "section")
	var key := _script_text(arguments, "key")
	return {"min": ConfigManager.GetMin(section, key), "max": ConfigManager.GetMax(section, key)}


## 설정을 파일에 쓰고 창 모드·해상도·UIScale 을 적용한다.
func _script_config_save(_arguments: Dictionary) -> bool:
	ConfigManager.Save()
	ConfigManager.ApplyGraphic()
	_scale_changed()
	return true


## 설정과 키 바인딩을 처음 값으로 되돌린다 (SAVE 전까지는 파일에 쓰지 않는다).
func _script_config_reset(_arguments: Dictionary) -> bool:
	ConfigManager.ResetToDefaults()
	return true


func _resolution_position() -> int:
	var current: int = ConfigManager.GetInt(RESOLUTION_SECTION, RESOLUTION_KEY, 0)
	for i in ConfigManager.ResolutionOptionCount:
		if ConfigManager.ResolutionOptionIndexAt(i) == current:
			return i
	return 0


## 해상도: position(고를 수 있는 목록에서 지금의 자리), count(목록의 수), label(지금 해상도의 이름).
func _script_resolution(_arguments: Dictionary) -> Dictionary:
	var position := _resolution_position()
	return {"position": position, "count": ConfigManager.ResolutionOptionCount, "label": ConfigManager.ResolutionOptionLabelAt(position)}


## 해상도를 목록의 그 자리의 것으로 바꾼다 (SAVE 때 적용된다).
func _script_resolution_set(arguments: Dictionary) -> bool:
	var position = arguments.get("position")
	if not position is int or position < 0 or position >= ConfigManager.ResolutionOptionCount:
		return false
	ConfigManager.SetInt(RESOLUTION_SECTION, RESOLUTION_KEY, ConfigManager.ResolutionOptionIndexAt(position))
	return true


## 키를 바꿀 수 있는 입력 액션의 이름들.
func _script_actions(_arguments: Dictionary) -> Array:
	return Array(InputManager.GetActionNames())


## 그 액션에 묶인 키의 경로 (예: "<Keyboard>/leftShift"). 없으면 빈 글자.
func _script_binding(arguments: Dictionary) -> String:
	var action := _script_text(arguments, "action")
	return InputManager.GetActionBinding(action) if InputManager.HasAction(action) else ""


## 그 액션의 키를 다음에 눌리는 키나 마우스 버튼으로 바꾸기 시작한다. 빈 이름이면 그만둔다.
## 받는 동안 frame 의 bind_listening 이 그 이름이고, 받은 프레임에 bind_captured 가 참이다.
func _script_bind_listen(arguments: Dictionary) -> bool:
	var action := _script_text(arguments, "action")
	if not action.is_empty() and not InputManager.HasAction(action):
		return false
	_listening = action
	return true


## 저장하지 않은 변경을 되돌리고 화면을 닫는다 (이 프레임이 끝난 뒤).
func _script_close(_arguments: Dictionary) -> bool:
	_close_requested = true
	return true
