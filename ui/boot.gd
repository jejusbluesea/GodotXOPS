extends Node
## 시작 화면. 아무것도 그리지 않고 오프닝으로 넘어간다. 개발용 인자가 있으면 그 화면에서 시작한다 (Dev 참조).

const FIRST_SCENE := "opening"


func _ready() -> void:
	if Dev.has("--mission"):
		var index := int(Dev.value("--mission", "0"))
		var page := int(Dev.value("--page", "0"))
		Game.LoadMission(index, Dev.has("--addon"), page)

	Game.ChangeScene(Dev.value("--scene", FIRST_SCENE))
