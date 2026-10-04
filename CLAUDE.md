# CLAUDE.md

## Project Overview

**GodotXOPS** — Godot 4.7.2 (.NET) 프로젝트. 일본 인디 FPS XOPS(2000년)의 오픈소스 구현 OpenXOPS를, 그 Unity 포팅본인 UnityXOPS를 참고해 Godot으로 옮긴다.

- 참고 원본: `C:\Users\twoj2\Desktop\Project\UnityXOPS` (브랜치 `QoL-road-to-multiplay(0.4)`), C++ 원본은 그 안의 `OpenXOPS/`
- 첫 목표: **완전 포팅**. 편의성 현대화(인게임 설정, 일시정지 메뉴, 체크포인트)와 모딩은 포팅이 끝난 뒤에 한다.

## 확정된 설계

- **언어**: 게임플레이·데이터·로더·매니저는 C#. 화면 5종(오프닝, 메뉴, 브리핑, HUD, 결과)의 배치·연출·입력은 GDScript.
  - GDScript는 UI용 창구 Autoload만 호출한다. 게임플레이 노드를 직접 만지지 않는다.
  - 창구의 public 멤버는 Godot Variant 호환 타입과 시그널만 쓴다 (GDScript는 C# static 멤버와 순수 C# 클래스를 못 본다).
- **포팅 기준**: UnityXOPS 0.4 HEAD의 `Runtime/Map`, `Runtime/Data`. `SimClock`(33.333Hz 단일 틱)까지 포함. `Runtime/Modding`과 Lua는 전부 제외.
- **씬 UI 기준**: UnityXOPS `origin/first-release(0.1)`, `origin/refactoring(0.2)` 브랜치의 `Runtime/Scene`. 수치는 0.3 이후 Lua를 참고하고, 나중에 외부 데이터로 뺄 수 있게 화면별로 한 곳에 모은다.
- **이동·충돌**: 엔진 물리 컨트롤러(CharacterBody3D, RigidBody3D)를 쓰지 않는다. 원본 `collision.cpp` 기반 로직을 직접 계산한다. 원본 조작감 재현이 목적이다.
- **싱글톤**: `Singleton<T>`를 상속하고 `project.godot`의 Autoload로 등록한다. 등록 순서가 초기화 순서다. 노드일 필요가 없는 것(파일 로더 등)은 static 클래스로 둔다.
- **에셋 형식**: 모델은 `.x` 전용 (glTF/FBX 금지). 3D 애니메이션 미사용. 이미지와 WAV는 Godot 내장 런타임 로더로 읽는다.
- **웹 익스포트는 하지 않는다.** 렌더러는 Mobile.

## 폴더

- `src/` — C#. `Utility/`(공용), `IO/`(파일 로더), `Data/`(데이터 클래스와 `DataManager`), `Dev/`(점검 도구). 이후 `Map/` 등이 UnityXOPS `Runtime/` 구조를 따라 추가된다.
- `scenes/` — `.tscn`. 화면은 씬 파일 단위로 나눈다.
- `ui/` — GDScript UI.
- `shaders/` — `.gdshader`.
- `data/`, `addon/` — 원본 XOPS 에셋. **저작권상 커밋 금지** (`.gitignore` 처리됨). 로컬에만 둔다.
- `godotdata/` — 외부 게임 데이터 JSON (UnityXOPS의 `unitydata/`). 커밋 대상.
- `data/`, `addon/`, `godotdata/`에는 `.gdignore`가 있어 Godot이 임포트하지 않는다. 런타임에 `GamePath.Resolve()`로 전체 경로를 얻어 파일로 직접 읽는다.

## 좌표 변환

변환은 반드시 `src/Utility/Coord.cs`를 거친다. 다른 곳에서 축 부호를 직접 뒤집지 않는다.

- OpenXOPS → Godot: `(-x, y, z) × 0.1` (`Coord.FromXops`)
- UnityXOPS → Godot: `(x, y, -z)` (`Coord.FromUnity`). `godotdata` JSON의 위치·오프셋과 `.x` 정점이 여기에 해당한다.
- UnityXOPS 오일러 각(도) → Godot: `(-x, -y, z)` 라디안, YXZ 순서 (`Coord.FromUnityEuler`)
- 삼각형 와인딩과 UV는 뒤집지 않는다.
- `godotdata` JSON 값은 UnityXOPS 공간 그대로 둔다. 로드해서 쓰는 지점에서 변환한다.

## 빌드와 실행

```bash
dotnet build GodotXOPS.csproj
```

```bash
"C:/Users/twoj2/Desktop/Game Engine/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . res://scenes/dev/loader_check.tscn
```

두 번째 명령은 `data/`와 `addon/`의 모든 이미지·사운드·모델을 로더로 읽어 보는 점검이다. 실패가 있으면 종료 코드 1.

같은 방식으로 실행하는 점검·도구 씬:

- `res://scenes/dev/data_check.tscn` — `godotdata/` JSON을 로드된 값과 키 단위로 대조한다. 데이터 클래스에 없는 키나 값 불일치가 있으면 종료 코드 1. 데이터 클래스나 JSON을 고친 뒤에 돌린다.
- `res://scenes/dev/asset_viewer.tscn` — 에셋을 눈으로 확인하는 뷰어 (`--headless` 없이 실행).

- `res://scenes/dev/config_input_check.tscn` — 설정 읽기/쓰기/되돌리기와 입력 조회를 가짜 입력 이벤트로 확인한다. 설정 파일은 저장하지 않는다.

- `res://scenes/dev/map_viewer.tscn` — 미션을 골라 블록·스카이를 띄우고 자유 카메라로 확인한다 (`--headless` 없이 실행). 인자는 `--` 뒤에 준다:
  - `--selftest` — 모든 미션을 로드해 보고 종료 (헤드리스 가능).
  - `--mission 번호 [--addon] --screenshot 경로.png [--cam x,y,z,yaw,pitch]` — 화면을 PNG로 저장하고 종료. 렌더링을 고친 뒤 결과를 직접 확인할 때 쓴다.

## 맵과 렌더링

- `MapLoader`(Autoload)가 블록·스카이·미션 정보를 들고 있고, 씬이 바뀌어도 유지된다. 로드 함수는 이전 것을 먼저 언로드한다.
- 블록 충돌은 `MapLoader.RaycastBlock` / `IsInsideBlock`으로 직접 계산한다 (엔진 물리 미사용). 블록 앞면만 맞는다.
- 추가 충돌(Additional Collision, `MapLoader.AdjustCollision`): 캐릭터-맵 충돌에서 중심축 0.9 m / 1.3 m 높이의 추가 검사 2점은 **이 플래그가 켜진 미션에서만** 돈다 (원본 `human::CollisionMap`의 `AddCollisionFlag`). UnityXOPS `HumanController`는 플래그를 무시하고 항상 검사하는데, 이는 잘못 옮긴 것이므로 따라 하지 않는다.
- 머티리얼은 `MaterialManager`의 `Create*Material`로 만든다. `alpha_clip_blend` 셰이더는 원본처럼 sRGB 값 그대로 곱하고 섞은 뒤 마지막에만 선형으로 바꾼다. 텍스처 유니폼에 `source_color`를 붙이지 않는다.
- 안개는 Godot 환경 안개가 아니라 전역 셰이더 변수(`xops_fog_color`, `xops_fog_range`)로 셰이더가 직접 계산한다 (원본의 선형 안개 재현).
- 깊이 테스트를 끈 머티리얼(스카이)은 반투명 패스로 들어가므로 `RenderPriority`로 그리는 순서를 정한다.
- 원본 좌표에서 외적으로 법선을 구하는 코드를 옮길 때는 피연산자 순서를 뒤집는다 (원본은 왼손, Godot은 오른손 좌표계).
- 런타임에 만든 노드를 같은 프레임 안에 교체할 때는 `QueueFree` 대신 `RemoveChild` + `Free`를 쓴다. 트리에서 뗀 노드를 `QueueFree`만 해 두고 종료하면 종료 시 치명 오류가 난다.

## Autoload 순서

`ConfigManager` → `DataManager` → `InputManager` → `MaterialManager` → `MapLoader`. `InputManager`는 `ConfigManager`의 바인딩을 읽으므로 뒤에 와야 한다. 매니저를 추가할 때 의존 순서대로 `project.godot`의 `[autoload]`에 넣는다.

## 설정과 입력

- `godotdata/config.json`은 `ConfigManager`가 읽고 쓴다. 파일이 없으면 코드 기본값(`ConfigManagerDefault.cs`)으로 새로 만든다. 새 설정은 기본값 목록에 추가하면 기존 파일에도 자동으로 병합된다.
- `ConfigManager.ApplyGraphic`이 부팅 때 창 모드와 렌더 해상도를 적용한다(기본: 전체화면, 640×480 렌더, 화면비가 다르면 검은 띠). 도구 씬은 `_Ready`에서 창 설정을 되돌린다(`AssetViewer` 참조).
- 입력은 `InputManager`를 거쳐 읽는다: `IsPressed` / `WasPressed` / `WasReleased`(버튼), `ReadVector`(move, look), `IsKeyPressed` / `WasKeyPressed`(치트 키 등 바인딩 밖의 키). Godot `Input`을 직접 부르지 않는다.
- `ReadVector`는 X 오른쪽 +, Y 위쪽(전진) + 다. look은 마우스 이동량(픽셀)이다.
- 바인딩 경로는 `<Keyboard>/w`, `<Mouse>/leftButton` 형식이고 `InputPath`가 Godot 이벤트로 변환한다. 키보드는 물리 키 위치 기준이다.

## 데이터 JSON

- 데이터 클래스의 public 필드 이름이 곧 JSON 키다 (camelCase, UnityXOPS와 동일). 이름을 바꾸면 파일과 어긋난다.
- 읽기는 `JsonData.Overwrite`만 쓴다. 파일에 있는 최상위 키만 덮어쓰고, 파일이 없거나 깨져도 기본값으로 진행한다.
- 컨테이너의 리스트·중첩 객체 필드는 이니셜라이저로 초기화해 소비자가 null을 만나지 않게 한다.

## Code Conventions (C#)

- 네임스페이스 `GodotXOPS` (로더는 `GodotXOPS.IO`).
- private 필드 `m_`, private static 필드 `s_`, private const `k_` 접두사.
- 코드 정렬용 수직 공백 금지 (`private float          m_member` 같은 것).
- 주석은 한국어. 클래스와 함수에만 작성한다.
  - 클래스는 클래스 설명만, 함수는 인자와 반환 값까지.
  - Godot 가상 함수(`_Ready`, `_Process` 등)에는 주석을 달지 않는다.
  - 그 외 위치에는 이해하기 어려운 부분에만.
- `partial class`로 관심사별 파일 분리. Godot 노드 클래스는 `partial`이 필수다.
- 에디터 전용 로그는 `Debugger`를 쓴다 (`[Conditional("TOOLS")]`로 익스포트 빌드에서 제거됨).
- Godot 4.7 기준 deprecated API 사용 금지.
