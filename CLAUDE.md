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

- `src/` — C#. `Utility/`(공용), `IO/`(파일 로더), `Dev/`(점검 도구). 이후 `Data/`, `Map/` 등이 UnityXOPS `Runtime/` 구조를 따라 추가된다.
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
