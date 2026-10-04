extends Node
## 개발용 명령행 인자를 처리한다 (Autoload 이름 Dev). 인자는 "--" 뒤에 준다.
##   --window 너비x높이   설정 파일의 화면 설정 대신 그 크기의 창으로 띄운다.
##   --scene 이름         오프닝 대신 그 화면에서 시작한다 (mainmenu, briefing, maingame, result).
##   --mission 번호 [--addon] [--page 번호]   시작하기 전에 그 미션을 로드한다.
##   --ui-shot 경로.png   화면을 PNG 로 저장하고 종료한다. --ui-time 초 로 찍는 시각을 정한다 (기본 1.5).
##   --ui-state 값        화면마다 정해 둔 상태로 시작한다 (메뉴: credit / exit / addon, 메인게임: simple / off).
##   --ui-quit 초         그 시간이 지나면 종료한다. 헤드리스로 화면 스크립트에 오류가 없는지 볼 때 쓴다.

const DEFAULT_SHOT_TIME := 1.5

var _args: PackedStringArray
var _shot_path := ""
var _shot_time := DEFAULT_SHOT_TIME
var _quit_time := -1.0
var _elapsed := 0.0


func _ready() -> void:
	_args = OS.get_cmdline_user_args()

	var window := value("--window", "")
	if not window.is_empty():
		var parts := window.split("x")
		if parts.size() == 2:
			Game.SetDevWindow(int(parts[0]), int(parts[1]))

	_shot_path = value("--ui-shot", "")
	_shot_time = float(value("--ui-time", str(DEFAULT_SHOT_TIME)))
	_quit_time = float(value("--ui-quit", "-1"))
	set_process(not _shot_path.is_empty() or _quit_time >= 0.0)


func _process(delta: float) -> void:
	_elapsed += delta
	if _quit_time >= 0.0 and _elapsed >= _quit_time:
		get_tree().quit(0)
		return
	if _shot_path.is_empty() or _elapsed < _shot_time:
		return

	set_process(false)
	await RenderingServer.frame_post_draw
	var error := get_viewport().get_texture().get_image().save_png(_shot_path)
	print("화면 저장 %s: %s" % ["성공" if error == OK else "실패", _shot_path])
	get_tree().quit(0 if error == OK else 1)


## 인자가 있는지.
func has(arg_name: String) -> bool:
	return _args.has(arg_name)


## 인자 바로 뒤의 값. 없으면 fallback.
func value(arg_name: String, fallback: String) -> String:
	var index := _args.find(arg_name)
	if index >= 0 and index + 1 < _args.size():
		return _args[index + 1]
	return fallback
