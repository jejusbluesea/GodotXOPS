# CLAUDE.md

> **세션을 시작하면 프로젝트 루트의 `TODO.md`를 먼저 읽는다.** 진행 상황, 다음 작업, 아직 옮기지 않은 것이 적혀 있다. 작업을 끝내면 그 파일을 갱신한다.

## Project Overview

**GodotXOPS** — Godot 4.7.2 (.NET) 프로젝트. 일본 인디 FPS XOPS(2000년)의 오픈소스 구현 OpenXOPS를, 그 Unity 포팅본인 UnityXOPS를 참고해 Godot으로 옮긴다.

- 참고 원본: `C:\Users\twoj2\Desktop\Project\UnityXOPS` (브랜치 `QoL-road-to-multiplay(0.4)`), C++ 원본은 그 안의 `OpenXOPS/`
- 첫 목표: **완전 포팅**. 편의성 현대화(인게임 설정, 일시정지 메뉴, 체크포인트)와 모딩은 포팅이 끝난 뒤에 한다. 포팅은 10단계까지 끝났고 1.0.0 을 릴리즈했다 (2026-10-05, 태그 `v1.0.0`). 최신 릴리즈는 1.1.0 이다 (2026-10-10, 태그 `v1.1.0`, 커밋 `45f3c84`). 다음은 1.2.0 이고 작업 중이다: 화면(씬 UI)을 `.sgd` 로 바꿀 수 있게 한다 (아래 "화면 스크립트", `ROADMAP.md`, `TODO.md`).
- **1.1.0 다음의 방향** (사용자 결정, 2026-10-10): 쇼케이스를 만들어 배포에 넣기로 한 것은 취소했다. GodotXOPS 는 쇼케이스 없이 그대로 공개하면서 간다. 여기에 넣는 것은 **원본과 크게 달라지지 않으면서 누구나 커스터마이징에 쓸 만한 범용 기능**이다. 기본 데이터와 기본 미션은 원본 그대로 둔다.

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

- `src/` — C#. `Utility/`(공용), `IO/`(파일 로더와 확장 형식의 읽기·쓰기), `Data/`(데이터 클래스와 `DataManager`), `Dev/`(점검 도구). 이후 `Map/` 등이 UnityXOPS `Runtime/` 구조를 따라 추가된다.
- `src/Editor/`, `scenes/editor.tscn` — 에디터 (아래 "에디터"). 전부 C# 이고 Godot 기본 컨트롤을 코드로 만든다.
- `scenes/` — `.tscn`. 화면은 씬 파일 단위로 나눈다.
- `ui/` — GDScript UI. 화면별 스크립트와 `ui/common/`의 공용 도우미.
- `shaders/` — `.gdshader`.
- `data/`, `addon/` — 원본 XOPS 에셋. **저작권상 커밋 금지** (`.gitignore` 처리됨). 로컬에만 둔다.
- `editor_temp/` — 맵 에디터의 플레이 테스트가 쓰는 임시 파일. 커밋하지 않는다 (`.gitignore`, 만들 때 `.gdignore` 를 함께 쓴다).
- `godotdata/` — 외부 게임 데이터 JSON (UnityXOPS의 `unitydata/`). 커밋 대상.
- `addons/godot_sandbox/` — Godot Sandbox 확장 (GDExtension, v0.60, BSD-3-Clause. 원본 에셋 폴더 `addon/` 과 다른 폴더다). 스크립트로 만드는 이벤트를 격리해 돌리는 SafeGDScript 의 실행기다. Windows x86_64 용 DLL 과 `gdscript.elf` 만 넣었고 커밋한다 (사용자 결정. 버전을 고정한다). 에디터 플러그인과 테스트 파일은 뺐다. 저장소를 새로 받으면 `--headless --import` 를 한 번 돌려야 확장이 등록된다. **외부에서 받은 스크립트는 이 샌드박스로만 돌린다. 일반 GDScript 로 컴파일하지 않는다.**
- `dist/` — 릴리즈 파일에 함께 넣는 것: `GodotXOPS_Editor.bat`(에디터 실행, 사용자 결정), `THIRD_PARTY_NOTICES.txt`(Godot Engine, Godot Sandbox, .NET 런타임의 라이선스 전문. 엔진이나 확장의 버전을 올리면 다시 만든다). 외부 라이브러리의 고지는 README 와 이 파일에만 적고 `global.json` 과 메뉴의 크레딧에는 넣지 않는다 (사용자 결정). `.gdignore` 가 있다.
- `data/`, `addon/`, `godotdata/`에는 `.gdignore`가 있어 Godot이 임포트하지 않는다. 런타임에 `GamePath.Resolve()`로 전체 경로를 얻어 파일로 직접 읽는다.

## 좌표 변환

변환은 반드시 `src/Utility/Coord.cs`를 거친다. 다른 곳에서 축 부호를 직접 뒤집지 않는다.

- OpenXOPS → Godot: `(-x, y, z) × 0.1` (`Coord.FromXops`)
- UnityXOPS → Godot: `(x, y, -z)` (`Coord.FromUnity`). `godotdata` JSON의 위치·오프셋과 `.x` 정점이 여기에 해당한다.
- UnityXOPS 오일러 각(도) → Godot: `(-x, -y, z)` 라디안, YXZ 순서 (`Coord.FromUnityEuler`)
- 삼각형 와인딩과 UV는 뒤집지 않는다.
- 포인트(`RawPointData`)의 `look`은 사람 기준 yaw 다 (원본 방향 + 180°). 원본은 사람만 방향에 π 를 더해 그리므로 (object.cpp:2158), 원본 방향 그대로 그리는 소물에는 `look − 180`을 쓴다 (object.cpp:2765. UnityXOPS 는 소물에도 `look`을 그대로 써서 반대로 놓인다). **무기는 맵에 놓인 것도 사람이 버린 것도 `look`(사람 기준 yaw) 그대로다.** 원본 코드만 보면 맵에 놓인 무기도 소물처럼 180° 를 빼야 할 것 같지만, 그렇게 하면 화면에서 원본과 반대로 놓인다 (사용자가 원본과 대조해 확인, 2026-10-07. 무기 모델을 놓는 회전이 사람 기준 yaw 에 맞춰져 있다). 다시 "고치지" 않는다.
- PD2 의 방향은 "그 자리에 놓이는 것의 yaw"다. 사람과 무기는 `look`과 같고 소물은 `look − 180`이다. PD2 로더가 종류별로 `look`으로 맞춘다 (`MapLoader.LookOffset`).
- `godotdata` JSON 값은 UnityXOPS 공간 그대로 둔다. 로드해서 쓰는 지점에서 변환한다.

## 빌드와 실행

**사용자에게 보여 주는 명령은 PowerShell 기준으로, 블록 하나가 그대로 돌아가게 쓴다** (사용자 결정). 사용자는 Claude Code 에서 코드 블록의 재생 버튼을 눌러 돌린다. 그래서:

- **한 줄짜리 완결된 명령으로 쓴다.** 앞 줄에 변수를 선언해 두고 뒷줄에서 쓰거나, `foreach` 로 여러 개를 묶지 않는다. 실행 파일 경로는 매번 전부 적는다. 여러 개를 돌려야 하면 **블록을 따로** 쓴다 (블록마다 재생 버튼이 생긴다).
- 따옴표로 감싼 실행 파일 경로 앞에는 호출 연산자 `&` 를 붙인다.
- 게임에 넘기는 인자 구분자는 `'--'` 로 감싼다 (PowerShell 이 삼키지 않게).
- Windows PowerShell 5.1 에는 `&&` 와 `||` 가 없다. 이어서 돌리려면 `;`, 성공했을 때만 돌리려면 `if ($?) { ... }`.
- 경로 구분자는 슬래시·역슬래시 둘 다 동작한다. 이 문서의 형식대로 슬래시를 쓴다.

```powershell
dotnet build GodotXOPS.csproj
```

```powershell
& "C:/Users/twoj2/Desktop/Game Engine/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . res://scenes/dev/loader_check.tscn
```

두 번째 명령은 `data/`와 `addon/`의 모든 이미지·사운드·모델을 로더로 읽어 보는 점검이다. 실패가 있으면 종료 코드 1.

같은 방식으로 실행하는 점검·도구 씬:

- `res://scenes/dev/data_check.tscn` — `godotdata/` JSON을 로드된 값과 키 단위로 대조한다. 데이터 클래스에 없는 키나 값 불일치가 있으면 종료 코드 1. 데이터 클래스나 JSON을 고친 뒤에 돌린다.
- `res://scenes/dev/asset_viewer.tscn` — 에셋을 눈으로 확인하는 뷰어 (`--headless` 없이 실행).

- `res://scenes/dev/config_input_check.tscn` — 설정 읽기/쓰기/되돌리기와 입력 조회를 가짜 입력 이벤트로 확인한다. 설정 파일은 저장하지 않는다.

- `res://scenes/dev/map_viewer.tscn` — 미션을 골라 블록·스카이를 띄우고 자유 카메라로 확인한다 (`--headless` 없이 실행). 인자는 `--` 뒤에 준다:
  - `--selftest` — 모든 미션을 로드해 보고 종료 (헤드리스 가능).
  - `--mission 번호 [--addon] --screenshot 경로.png [--cam x,y,z,yaw,pitch]` — 화면을 PNG로 저장하고 종료. 렌더링을 고친 뒤 결과를 직접 확인할 때 쓴다.

- `res://scenes/dev/play_test.tscn` — 미션의 맵과 사람을 로드하고 플레이어를 직접 조작한다 (`--headless` 없이 실행). 인자: `--selftest`(모든 미션에서 틱을 돌려 사람이 맵 아래로 빠지지 않는지 확인, 헤드리스 가능), `--mission 번호 [--addon] [--third] [--walk] [--fire] [--weapon 번호] [--hitbox] [--look yaw,pitch] [--pos x,y,z] [--drop] --screenshot 경로.png`.

- `res://scenes/dev/weapon_check.tscn` — 무기·총알·히트박스·떨어진 무기·소물·통계와 이펙트·소리 호출을 수치로 확인한다 (헤드리스). 사람들을 블록이 없는 공중으로 옮겨 놓고 무기 틱과 총알 틱만 직접 돌린다. 무기, 총알, 피격 코드를 고친 뒤에 돌린다.

- `res://scenes/dev/ai_check.tscn` — AI(시야·청각·경계·조준 예측·무기 운용·좀비·경로·복제)와 미션 이벤트·판정을 수치로 확인한다 (헤드리스). 점검용 PD1 을 임시 폴더에 만들어 로드하고, 사람들을 공중에 놓은 채 AI 틱과 무기 틱만 직접 돌린다. AI, 이벤트, 포인트 조회 코드를 고친 뒤에 돌린다.

- `res://scenes/dev/effect_viewer.tscn` — 이펙트 프리셋을 골라 눈으로 보고(`--effect 번호`, `--additive`, `--screenshot 경로.png`), `--selftest` 로 재생 수·풀 증가·블렌드 모드·발광 감쇠·면 위 재생을 수치로 확인한다 (헤드리스 가능). 이펙트 데이터나 `EffectManager` 를 고친 뒤에 돌린다.

- `res://scenes/dev/bd2_check.tscn` — 모든 미션의 BD1 을 BD2 로 바꿔 쓰고 읽어 블록·메시·판정이 같은지 대조하고, 블록 플래그·그리지 않는 면·재질(착탄, 발소리와 박자)·깨진 파일을 확인한다 (헤드리스). `-- --convert 입력.bd1 출력.bd2` 는 파일 하나를 변환한다. 블록 로더나 충돌 조회, `BD2File` 을 고친 뒤에 돌린다. `map_viewer` 의 `--file 경로` 로 BD1 / BD2 파일 하나를 띄워 볼 수 있다.

- `res://scenes/dev/mif2_check.tscn` — 모든 공식 미션을 확장 형식으로 바꿔 미션 정보와 같은 난수 씨앗으로 돌린 100틱의 결과가 원본과 같은지 대조하고, 에드온 데이터와 10000 번호 규칙, MIF2 의 형식 오류, 미션 목록 스캔, MIF 추가 사물의 변환을 확인한다 (헤드리스). `-- --convert-official 번호|all 출력폴더` 와 `-- --convert 입력.mif 출력폴더` 는 원본 미션을 확장 형식 한 벌로 바꾼다. 미션 로드, 에드온 데이터, 데이터 목록을 번호로 쓰는 코드를 고친 뒤에 돌린다.

- `res://scenes/dev/pd2_check.tscn` — 모든 미션의 PD1 을 PD2 로 바꿔 쓰고 읽어 포인트와 스폰 결과가 같은지 대조하고, 넓은 파라미터·추가 파라미터·방향·이벤트 줄 수·깨진 파일을 확인한다 (헤드리스). `-- --convert 입력.pd1 출력.pd2` 는 파일 하나를 변환한다. 포인트 로더, `PD2File`, 이벤트 줄을 고친 뒤에 돌린다.

- `res://scenes/dev/block_bench.tscn` — 블록 수를 늘려 가며(상자 블록을 맵 옆에 더 얹는다) 한 틱의 시간과 레이·내부 판정 한 번의 시간을 잰다 (헤드리스). 점검이 아니라 측정이라 항상 종료 코드 0 이다. 충돌 조회의 속도를 고칠 때 앞뒤로 돌려 비교한다.

- `res://scenes/dev/script_probe.tscn` — Godot Sandbox 의 SafeGDScript(`.sgd`)를 C# 에서 로드·호출하고, 빠져나가려는 스크립트 21가지와 자원 제한(무한 루프, 재귀, 배열 폭주), 실패 통지를 확인하는 시제품이다 (헤드리스). 익스포트 빌드에서는 `GodotXOPS.exe --headless -- --scene dev/script_probe`. 확장의 버전을 올리거나 샌드박스 연결을 고친 뒤에 돌린다.

- `res://scenes/dev/event_check.tscn` — 스크립트 이벤트를 수치로 확인한다 (헤드리스). 점검용 묶음과 PD2·MIF2 를 `build/event_check/` 에 만들어 로드하고 틱을 직접 돌린다: 파라미터, 출구, 줄의 저장 칸, 미션 변수, API, 화면 글자와 Interact, 실패한 줄만 멈추는지, 로드 때 거절되는 경우, 기본 제공 묶음 전부, 모딩 문서의 예제 스크립트. 이벤트, `EventApi`, 샌드박스 연결, `godotdata/event/` 를 고친 뒤에 돌린다.

- `res://scenes/dev/ui_check.tscn` — 화면이 쓰는 창구 `Game`의 값과 화면 전환 흐름(로드 → 시작 → 재시작 → 내리기)을 수치로 확인한다 (헤드리스). 창구나 화면 흐름을 고친 뒤에 돌린다.

게임 자체는 씬을 지정하지 않고 실행한다 (`--path .`만). 화면을 고친 뒤에는 개발용 인자("--" 뒤)로 직접 확인한다:

- `--window 너비x높이` — 설정 파일의 전체화면 대신 그 크기의 창으로 띄운다.
- `--scene 이름 [--mission 번호 [--addon] [--page 번호]]` — 그 화면에서 시작한다 (`mainmenu`, `briefing`, `maingame`, `result`).
- `--ui-shot 경로.png [--ui-time 초]` — 화면을 PNG 로 저장하고 종료한다.
- `--ui-state 값` — 메뉴는 `credit` / `exit` / `addon` / `option` / `option-input` / `option-graphic` / `option-sound`, 메인게임은 `simple` / `off` / `console` 상태로 시작한다. `console`은 어느 화면에서든 설정 파일과 무관하게 디버그 콘솔을 허용한다 (콘솔을 화면으로 확인할 때 `--ui-click "key:F11 text:help key:Enter"`와 함께 쓴다).
- `--ui-click "목록"` — 가짜 입력을 차례로 넣는다: `x,y`(클릭), `x,y,초`(누르고 있기), `key:이름`(키 한 번), `text:글자`(글자를 차례로 친다. 띄어쓰기는 `key:Space`). 좌표는 창 픽셀이고 실제 커서를 옮긴다. 버튼을 눌러 본 결과를 `--ui-shot`으로 볼 때 쓴다 (`--window 640x480`과 함께).
- `--ui-quit 초` — 그 시간 뒤 종료한다. `--headless`와 함께 써서 화면 스크립트에 오류가 없는지 본다. `.sgd` 화면 스크립트가 실패해 기본 화면으로 돌아갔으면 종료 코드 1 이다.
- `--ui-script 경로.json` — `.sgd` 화면 스크립트의 등록 파일 하나를 지정한다 (`godotdata/ui` 를 훑지 않는다). `--ui-script-stats` 는 메인게임을 떠날 때 `frame` 한 번의 평균 시간을 찍는다.

`play_test.tscn` 은 AI 와 이벤트를 켠 채로 돈다. `--noai` 로 끄고 시작하고, 창에서는 F2(AI 정지/재개), F4(전원 비전투), End(전원 경계), F9+↑/↓(복제), Insert(플레이어 무적), Home(디버그 텍스트 켜기/끄기)을 쓴다. `--invincible`, `--notext` 로 켜고 끈 채 시작할 수 있다. AI 가 꺼져 있어야 하는 점검 도구는 `AIController.Enabled = false` 로 둔다 (`WeaponCheck` 참조).

## 익스포트 빌드

```powershell
& "C:/Users/twoj2/Desktop/Game Engine/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . --export-release "Windows Desktop" build/windows/GodotXOPS.exe
```

- 프리셋은 `export_presets.cfg`의 "Windows Desktop" 이다. `build/`는 커밋하지 않는다 (`.gitignore`, `.gdignore`).
- 결과물: `GodotXOPS.exe`, `GodotXOPS.pck`, `data_GodotXOPS_windows_x86_64/`(.NET 런타임과 어셈블리. 게임 데이터 `data/`와 다른 폴더다).
- 실행 파일 옆에 `data/`, `addon/`, `godotdata/`, `addon.json`이 있어야 한다 (`GamePath.Root`가 익스포트 빌드에서는 실행 파일 폴더다). 샌드박스 확장의 DLL 도 실행 파일 옆에 나온다.
- 릴리즈 폴더(`build/버전/GodotXOPS/`)에는 익스포트 결과물, `godotdata/`, `addon.json`, `dist/` 의 두 파일, `LICENSE`(`LICENSE.txt` 로)를 넣는다. `data/` 와 `addon/` 은 넣지 않는다.
- 익스포트 뒤 Godot 프로세스가 한동안 종료되지 않을 때가 있으므로 백그라운드로 돌린다.
- 개발용 인자("--" 뒤)는 빌드에서도 동작한다. 뽑은 뒤 `GodotXOPS.exe --headless -- --scene mainmenu --ui-quit 2`의 종료 코드로 뜨는지 본다.
- 아이콘은 `xops.png`(`config/icon`)이고 실행 파일에도 들어간다. 부트 스플래시는 로고 없이 검은 배경이다 (끄는 설정은 없다).

## 시뮬레이션과 캐릭터

- 게임플레이는 `SimClock`(Autoload)의 33.333Hz 틱에서만 진행한다. 틱 대상은 `ISimTickable`을 구현해 `SimClock.Register`로 등록하고, `SimOrder`가 한 틱 안의 순서다 (움직이는 블록 5 → 사람 10 → 떨어진 무기 30 → 움직이는 소물 35 → 총알 40 → 인간간 충돌 100 → AI 200 → 미션 판정·이벤트 300). `SimClock.TickEnabled`가 false면 멈춘다.
- 사람 틱(10) 안의 순서는 원본 한 프레임과 같다: 무기 입력 소비(발사 등) → 무기 카운터 감소·조준 오차 갱신(`Human.TickWeapon`) → 이동·충돌 → 사망 상태 → 다리·팔 동작. 총알은 이동 전 위치에서 나간다.
- 시간 카운터(발사 간격, 재장전, 전환, 탄환 수명)는 정수 틱으로 센다. 데이터의 초 단위 값은 `RoundToInt(초 × SimClock.FrameRate)`로 바꾼다. 실수 초를 틱마다 빼면 잔차 때문에 원본보다 한 틱 늦어진다.
- 게임 결과에 영향을 주는 난수는 `GameRandom.Gameplay`(틱에서만), 연출용 난수는 `GameRandom.Visual`을 쓴다.
- `Human`(Node3D)은 데이터와 시각만 갖는다. 논리 위치·이동·충돌·사망 상태는 `human.Controller`(`HumanController`, 노드가 아닌 순수 클래스)가 갖고, 노드의 transform은 틱 사이를 보간한 시각 전용 값이다. 판정에는 `Controller.Position`을 쓴다.
- 캐릭터 각도는 UnityXOPS 규약으로 든다: 도 단위, yaw는 오른쪽으로 돌수록 +, pitch는 아래를 볼수록 +. 방향 벡터와 노드 회전은 `Coord.YawForward` / `YawRight` / `AimDirection` / `FromUnityEuler`로 만든다.
- 프레임 처리 순서(`ProcessPriority`): `InputManager`(최소) → `SimClock`(-200) → 일반 노드(0) → `Human` 시각 보간(50) → `PlayerController` 입력·카메라(100).
- 이동·충돌 코드는 UnityXOPS와 원본 `object.cpp`를 함께 대조해 옮긴다. 둘이 다르면 원본을 따르고 사용자에게 알린다. 지금까지 원본대로 고친 것: 추가 충돌 플래그, NPC 접지 판정(중심 + 진행 방향 한 점), 플레이어 전용 이동 경로 레이 검사, 비정상 이동량 되돌리기, 정지 시 급경사 미끄러짐 확률 건너뛰기, 점프 입력을 같은 틱에 소비, 매몰 판정 높이(키 − 0.06), 낙하 3분할 계수 0.33.

## 무기와 총알

- `Weapon`(순수 클래스)은 종류와 탄약만 갖는다. 모델은 `WeaponVisual`, 발사 간격·재장전·전환 카운터와 조준 오차는 `Human`(`HumanWeapon.cs`, `HumanAim.cs`)이 갖는다. 슬롯은 항상 2개이고 빈 슬롯은 맨손 무기(`IsNone`)다.
- 무기 동작은 데이터 필드로만 가른다. 코드에 무기 번호를 직접 쓰지 않는다 (`weaponGeneralData`의 `noneWeaponIndex`, `grenadeWeaponIndex`만 참조). 유저가 JSON 수정만으로 새 무기를 만들 수 있어야 한다.
- 총알 판정은 원본 `ObjectManager::CollideBullet` 방식이다: 한 틱 경로를 0.25 m 간격 점으로 나눠 점마다 사람(`HumanHitbox.Contains`, 수직 원기둥) → 소물 → 맵(`MapLoader.IsInsideBlock`) 순으로 검사한다. 선분-도형 교차로 바꾸지 않는다 (스치는 탄의 명중률과 벽 관통 결과가 달라진다).
- `BulletManager`(Autoload)가 탄환 풀 160개와 모델 노드를 갖는다. 탄환 모델은 보간된 위치가 총구에서 `bulletBoundAdjust`만큼 멀어진 뒤부터 보인다 (높은 프레임에서 사수의 머리를 뚫고 보이는 것을 막는 UnityXOPS의 연출, 판정과 무관).
- 무기·총알에서 원본대로 고친 것: 점 샘플링 판정, 피격 데미지의 부위별 난수 가산, 조준 오차·산탄 확산의 정수 난수, 연속 발사 수 제한(단발), 입력 처리 순서(발사 → 재장전 → 슬롯 → 종류 전환 → 스코프), 수류탄 이동 순서(이동 → 감쇠·중력)와 반사 시 위치 유지, 초기 예비 탄(장탄수 × (배수 − 1)).
- 원본이 아니라 UnityXOPS가 고친 동작을 따르는 것 (사용자 결정): 피격 시 조준 흐트러짐은 더 큰 쪽 유지(원본은 대입), 가득 찬 탄창은 재장전 불가(원본은 허용), 폭풍은 항상 멀어지는 쪽(원본 식은 폭발이 위에 있으면 끌어당김), 반동 오차는 항상 누적(원본은 조준선이 보일 때만).

## 떨어진 무기, 소물, 이펙트, 소리

- 게임 결과에 영향을 주는 것은 틱에서, 연출은 렌더 프레임에서 한다. 떨어진 무기의 낙하·줍기(`WeaponManager`)와 소물의 내구력·파괴 판정은 틱이고, 이펙트(`EffectManager`)·부서진 소물이 튀는 움직임·소리 볼륨 갱신은 렌더 프레임이다. 연출에는 `GameRandom.Visual`만 쓴다.
- 떨어진 무기 200, 탄환 160 은 원본 상수이고 가득 차면 새로 만들지 않고 버린다 (게임 결과에 영향을 주므로 가변으로 바꾸지 않는다). 이펙트 풀만 데이터로 정하고 모자라면 묶음 단위로 늘린다 (`effect_data.json`의 `poolInitialSize`, `poolGrowStep`, `poolMaxSize`). 맵을 내릴 때(`MapLoader.UnloadPointData`) 풀을 모두 비운다.
- 틱에서 일어난 일의 이펙트를 무기 모델 위치에 맞춰야 하면(총구 화염, 탄피) 틱에서는 표시만 해 두고 `Human._Process`에서 낸다. 무기 모델은 틱 사이를 보간해 움직이므로 틱에서 내면 어긋난다.
- 이펙트 프리셋·텍스처는 `effect_data.json`(`effectData`, `effectTextureData`), 호출하는 쪽은 인덱스(무기 모델·탄환·사람 종류 데이터에 있다)와 위치만 넘긴다. 이펙트 머티리얼은 `MaterialManager.CreateEffectMaterial`, 투명도는 인스턴스 유니폼 `effect_alpha`다.
- 블록 면 위에서 나는 이펙트(착탄, 벽 혈흔)는 `EffectManager.PlayOnSurface(번호, 위치, 면 위의 점, 법선)`로 낸다. 빌보드 emitter 는 `Play`와 똑같이 나오고, `NoBillboard` emitter(데칼)만 면에 눕혀 `decalSurfaceOffset`만큼 띄운다. 법선은 판정이 이미 구한 레이캐스트에서 받는다 (이펙트 때문에 판정을 바꾸지 않는다). 기본 데이터에 탄흔은 없다 (원본에 없다. 에드온이 넣는다).
- 이펙트의 블렌드 모드는 emitter 의 `blendMode`다. Godot 의 `blend_mix` / `blend_add` 는 컴파일 타임 설정이라 셰이더가 두 개이고(`effect_blend`, `effect_blend_add`), 머티리얼 캐시 키가 (텍스처 번호, 블렌드 모드)다. 가산일 때만 `brightness` 가 발광 세기로 쓰이고 인스턴스 유니폼 `effect_bright` 로 들어간다. 원본에는 가산이 없다.
- 소리는 `SoundManager.PlayAt(경로, 위치, 볼륨)`으로 낸다. `AudioStreamPlayer3D`를 쓰지 않는다 (원본의 선형 감쇠를 낼 수 없다). 헤드리스에서는 실제 재생을 하지 않는다.
- 소리가 나는 자리에서는 `WorldSound.EmitPointSound`로 AI 에게도 알린다 (듣는 거리는 `aiHear*` 데이터).

## AI, 이벤트, 미션 판정

- `AIBrain`(순수 클래스, `src/Map/Human/AI/`의 partial 6개)을 `Human`이 하나씩 갖는다 (`human.Brain`). `AIController`(순수 `ISimTickable`, SimOrder 200)가 틱마다 `MapLoader.Player`만 건너뛰고 전부 돌린다. `AIController.DrivePlayer`를 켜면 플레이어도 AI 가 움직인다 (메뉴 데모용).
- AI 는 사람에게 `Controller.SetInput`(이동·조준)과 `Human`의 무기 함수(`ShotWeapon`, `ReloadWeapon`, `SetSelectWeapon`, `DropCurrentWeapon`, `ApplyWeaponAction`)로만 손을 댄다. AI 가 넣은 이동 입력은 다음 틱의 이동이 소비한다.
- 계산은 원본 `ai.cpp` 기준이다. 다만 아래는 사용자 결정으로 원본과 다르다:
  - 회전·경로 이동 의사는 매 틱 새로 정한다. 틱을 넘어 유지되는 것은 두리번거림과 전투 중 회피 이동뿐이다 (원본은 모든 플래그를 유지하고 확률로 해제한다).
  - 아군 시체를 보고 경계하는 조건(`CheckCorpse`)은 넣지 않는다.
  - 좀비는 적의 이동을 앞질러 겨누지 않는다 (원본 `AItrackability` 는 데이터에 없다).
  - 맨손인 사람의 팔은 전투(좀비 공격, 항복) 중에만 조준 방향을 따른다. 전투가 끝나면 팔이 고정 자세의 각도까지 내려온 뒤에 고정 자세로 바꾼다 (바로 바꾸면 한 틱 만에 튄다).
    죽은 사람의 맨손 팔은 고정 자세를 풀고 팔 각도를 따른다: 원본대로 틱마다 6° 씩 ±90° 까지 간다 (`HumanController.TickDeadArm`, object.cpp:1170-1180. 죽는 순간 수평 이상이면 위로).
  - AI 는 무기 관리 때 스코프를 해제하지 않는다.
  - 전투 중 수류탄을 든 AI 가 확률로 다른 무기로 바꾸는 분기(ai.cpp:1059-1089)는 넣지 않는다. 수류탄을 다 던진 뒤에 바꾼다.
  - 경로·이벤트 포인트는 종류별로 찾는다 (아래).
- 원본대로 둔 것: 적의 이동을 앞질러 겨누기와 수류탄 높이 보정, 원거리 발견의 1/4 확정, 경계가 끝나면 시작한 자리로 돌아가기, 원거리 교전 중 근거리 적 재탐색, 경로의 수류탄 투척, 단발/연발 전환, 점프 판정, 좀비 공격은 겨눈 적 한 명만. 원본의 "끼었을 때 좌우로 돌기"는 조건이 늘 거짓이라 실행되지 않는 코드여서 옮기지 않았다.
- 시야와 사선은 블록만 가린다 (`MapLoader.RaycastBlock`). 사람과 소물은 가리지 않는다.
- 경로와 이벤트의 다음 포인트는 **종류별로** 찾는다 (`MapLoader.GetPathPoint`, `GetEventPoint`). 원본 `SearchPointdata`는 종류와 무관하게 같은 번호의 첫 포인트를 찾아서 번호가 겹치면 줄이 끊기는데, 원본의 버그로 보고 따르지 않는다 (사용자 결정).
- 소리 신호(`Human.NotifyThreatHeard`)는 `AIController`가 매 틱 비운다. 원본보다 한 틱 빨리 듣는다 (원본은 이중 버퍼라 다음 프레임에 듣는다).
- 발소리는 `HumanController`가 매 틱 `WorldSound.EmitFootstep(사람, 종류)`로 다른 팀 AI 에게 알린다 (달리는 소리만. 원본도 WAV 를 재생하지 않는다). 들리는 소리는 따로 `WorldSound.PlayFootstep`이 낸다: 다리 애니메이션이 `footstepPhase`를 지나는 틱(`HumanVisual.FootstepDue`)과 착지한 틱에, 발밑 면의 재질에서 소리를 골라 재생한다. 기본 데이터의 0번 재질에는 발소리가 없어서 BD1 맵에서는 나지 않는다. AI 청각과 판정에는 영향이 없다.
- 포인트 데이터는 확장자로 PD1 / PD2 로더가 갈리고 읽은 뒤에는 같은 구조다 (`RawPointData`). PD2 는 파라미터가 int32 이고 포인트마다 추가 파라미터(`extra`, 4바이트 칸)를 갖는다. 칸은 `GetExtraInt` / `GetExtraFloat` / `GetExtraBool`(0 이면 거짓)로 읽고, 없는 칸은 기본값이다. PD2 의 읽기·쓰기는 `src/IO/PD2File.cs`, 구조는 `docs/modding.md` 에 있다.
- 이벤트 줄 수와 시작 식별번호는 포인트 데이터가 정한다 (`MapLoader.EventEntryIds`). PD1 은 항상 156, 146, 136 세 줄이고 PD2 는 파일에 적힌 만큼이다. `EventManager.BeginMission`이 그 목록으로 줄을 만든다.
- **원본의 제한은 원본 형식(PD1)에만 건다** (사용자 결정으로 푼 것. PD1 까지 풀면 원본과 다른 틱에 이벤트가 일어난다): 메시지 16개와 한 틱에 한 줄이 처리하는 이벤트 6개는 `MapLoader.PointDataExtended` 가 false 일 때만이다. PD2 는 기다리는 이벤트를 만날 때까지 한 틱에 다 처리하고, 이번 틱에 이미 지난 포인트로 돌아오면 다음 틱으로 넘긴다 (바로 넘어가는 이벤트의 고리에서 틱이 끝나지 않는 것을 막는다). 포인트 종류 번호에는 제한이 없다 (종류별 사전).
- `EventManager`(Autoload, SimOrder 300)가 이벤트 줄들과 자동 판정을 돌린다. `BeginMission()`을 부른 뒤에만 돌고 맵을 내리면 멈춘다. UI(GDScript)는 시그널 `MessageShown(id, text)`, `MissionEnded(complete)`와 프로퍼티 `Result`, `EndTicks`, `MessageId`, `MessageText`, `MessageAlpha`, `StartCount`를 쓴다.

## 스크립트 이벤트

- 포인트 종류 10 에서 19 는 `BuiltinEventHandler`(원본 그대로), **20 이상은 스크립트 이벤트**다 (`ScriptEventHandler`). `EventManager` 는 처리기 인터페이스 `IEventHandler` 만 안다: `Tick` 이 출구 번호를 돌려주고(−1 기다림, −2 실패) `TryGetNext` 가 그 출구의 다음 식별번호를 준다. 실패한 줄만 멈춘다 (`EventLine.Stopped`).
- **스크립트 이벤트는 PD2 에서만 돈다.** PD2 에 20 이상의 종류가 있는데 등록한 묶음이 없으면 `LoadPointData` 가 실패한다. PD1 은 전과 같다.
- 묶음 하나 = 등록 JSON(`EventPackData`) 하나 + `.sgd` 하나 = `ScriptEventPack` 하나. 종류마다 그 안의 함수 하나다 (스크립트를 종류마다 따로 컴파일하면 하나에 약 150 ms, 32 MB 가 든다). 설치형은 `godotdata/event/*.json`(번호 20 에서 9999. 기본 제공 묶음 `base.json` 이 20 에서 99 를 쓴다. 지금 36종), 미션 전용은 MIF2 의 `addonEventDataPath`(10000 이상). **종류 번호는 항목마다 `type` 으로 적는다** (다른 에드온 데이터처럼 목록의 순서가 번호가 아니다).
- 계약 (사용자 결정): `init(api)` 한 번, 이벤트마다 `함수(p, state)`. 반환값은 출구 번호. `p` 는 등록 JSON 의 이름으로 채운 파라미터 사전(+ `id`, `x`, `y`, `z`, `yaw`), `state` 는 줄이 그 포인트에 머무는 동안의 저장 칸이다. 칸 표기는 원본 이름이다: `p2`(= `param1`), `p3`(= `param2`, 기본 출구), `e0` 부터 추가 파라미터.
- 분기는 출구 여러 개로, AND / OR 는 미션 변수(`GetVariable` / `SetVariable`, 정수)로 만든다. 반복은 전용 이벤트 없이 이벤트 체인의 고리로 만든다 (사용자 결정).
- **외부 스크립트는 `ScriptEventPack` 으로만 돌린다.** 격리(`restrictions`)를 걸 수 없으면 로드를 거절하고, 일반 GDScript 로 대신 돌리지 않는다. 샌드박스 노드는 트리에 넣지 않는다 (`_ready` / `_process` 가 돌지 않게).
- `EventApi` 는 값만 주고받는다. 노드·객체를 넘기지 않고, 파일 경로 인자를 받지 않고, UI 용 `Game` 을 넘기지 않는다. 스크립트가 준 값은 받는 쪽에서 확인한다. 함수를 더하면 `docs/modding.md` 의 API 표도 고친다.
- 사람 스폰은 맵 로드와 같이 **사람 정보 포인트(종류 4)를 바탕으로** 한다 (`MapLoader.SpawnHuman(infoId, ...)`. 사용자 결정). 무기 슬롯은 코드의 1 이 주 무기(시작할 때 드는 슬롯), 0 이 보조 무기다.
- 실행 예산(`execution_timeout`)은 확장의 기본값 200 이다. 스크립트 안의 계산은 거의 들지 않고 API 호출이 예산을 쓴다 (단순한 호출 약 1000번, 사전을 돌려주는 호출 약 300번). `memory_max` 는 배열·문자열을 세지 않는다 (알려진 한계, 받아들이기로 했다).
- **이벤트가 화면에 놓는 글자는 범용 칸이다** (사용자 결정: UI 에 제약을 걸지 않는다). `EventManager.SetHudText` / `ClearHudText`, 칸 32개, OS 글꼴과 `char.dds` 둘 다, 기준점 3×3 과 오프셋(화면 높이 480 기준, +y 위). 먼저 놓은 것이 뒤에 그려진다. 안내나 타이머를 고정 HUD 요소로 만들지 않는다. HUD 는 `HudRevision` 이 바뀔 때만 `HudTexts()` 를 읽는다.
- Interact 키는 사람이 아니라 이벤트가 받는다: `PlayerController` → `EventManager.QueueInteract()` → 다음 틱 한 번 `InteractPressed`.
- **소물의 트윈** (이벤트 55 Tween Object, 사용자 요청 2026-10-09: 순간이동이 아니라 시간에 걸쳐, 충돌 무시, 움직이면서 부서질 수 있게): `SmallObject.StartTween`. 움직이는 동안만 `SimClock` 에 등록돼 틱(35)에서 논리 위치를 옮기고 노드는 틱 사이를 보간한다. 부서지면 멈춘다. 사람은 소물과 부딪치지 않으므로 위에 선 사람은 같이 움직이지 않는다. 이벤트 줄은 기다리지 않는다. 방향 값은 소물 자신의 yaw 다 (소물 포인트의 PD2 방향과 같다. `spawn_object` 의 yaw 는 사람 기준이라 180° 다르다).
- **블록 움직이기와 끄기** (이벤트 58 Move Block / 59 Toggle Block, 사용자 요청 2026-10-09. `src/Map/Block/BlockMotion.cs`): 블록은 자기 위치나 회전이 없으므로 **처음 모양 기준의 변위**(이동량과 오일러 각)를 블록이 들고, 바뀔 때마다 면의 법선·중심·범위 상자를 처음 모양에서 다시 구한다 (`ApplyBlockTransform`). 그래서 블록의 판정 정보를 로드 뒤에 바꾸는 코드는 `base*` 값도 함께 봐야 한다. 움직이는 블록이 있을 때만 틱(5)에 등록하고 메시 노드는 틱 사이를 보간한다. 끈 블록은 `layerMask` 0 이고 판정 목록은 블록 번호 순서로 다시 만든다 (순서가 같아야 결과가 같다). **블록은 파일 안의 순번으로 가리킨다** (사용자 결정. BD2 에 식별번호를 넣는 것은 형식 변경이라 미뤘다). **사람은 블록과 함께 움직이지 않고, 끼거나 올라탄 경우는 평소의 충돌 처리에 맡긴다** (사용자 결정). 미션을 다시 시작하면(`UnloadPointData`) `ResetBlockMotion` 이 되돌린다. 스크립트의 파라미터 이름 `x`, `y`, `z`, `yaw`, `id` 는 포인트의 값으로 예약돼 있어 쓸 수 없다 (그래서 dx, dy, dz, turn).
- **이벤트의 소리** (이벤트 56 Play Sound / 57 Stop Sound, 사용자 결정): 스크립트는 파일 경로를 받지 않으므로 소리 목록(`SoundParameterData`, `godotdata/sound_data.json`, 미션은 MIF2 의 `addonSoundDataPath` 로 10000 부터)의 번호로 재생한다. 기본 목록에는 원본의 효과음을 파일 이름 순서로 넣어 두었다. `SoundManager.PlaySlot` / `StopSlot` 의 칸 16개이고, 되풀이하는 소리는 멈추거나 맵을 내릴 때까지 다시 재생한다. 없는 번호와 읽지 못한 파일은 경고만 남기고 줄은 멈추지 않는다. AI 는 이 소리를 듣지 않는다.
- 기본 제공 이벤트를 고치면 `base.json`, `base.sgd`, `docs/modding.md` 의 표, `event_check` 를 함께 고친다. 번호는 20개 단위로 끊는다 (20 대기, 40 동작, 60 흐름, 70 화면 글자. 사용자 결정).

## 화면 스크립트

- 1.2.0 의 작업이다 (사용자 결정, 2026-10-10): UnityXOPS 의 Lua 처럼 `.sgd` 로 화면을 바꿀 수 있게 한다. **기본 화면은 지금의 GDScript 이고, `godotdata/ui/*.json` 에 등록된 화면만 스크립트가 대신 그린다.** 대상은 화면 5종(오프닝, 메뉴, 브리핑, 메인게임, 결과)이고 OPTION 과 디버그 콘솔은 뺀다. **지금은 메인게임(HUD)만 연결돼 있다.**
- 샌드박스에 올리는 것은 `SandboxScript`(`src/Scripting/`)이고 `ScriptEventPack` 과 함께 쓴다. `Game.UiScriptLoad(화면 이름)` 이 등록을 찾아 올린 노드(트리 밖)를 돌려주고, GDScript 의 `XopsScriptScreen`(`ui/common/xops_script_screen.gd`)이 그 노드를 부르며 요소를 `XopsUI` 로 만든다. 이것도 외부 스크립트이므로 격리 없이 돌리지 않는다.
- 계약: `init(api)` → `build(ctx)` → 프레임마다 `frame(v, delta)`. **값은 게임이 사전 하나로 넘기고(`Game.HudValues()`), 요소는 번호로 가리키며 `set_many` 로 묶어 고친다** (API 호출 횟수가 비용이다. `script_probe` 의 `CheckFrameCost`). 스크립트에 노드를 넘기지 않고, 스크립트가 준 값은 `XopsScriptScreen` 이 형과 범위를 확인한다.
- 스크립트는 파일 경로를 받지 않는다. 이미지는 등록 파일의 `images` 목록의 번호로 가리킨다.
- **실패하면 그 화면을 기본 GDScript 화면으로 되돌린다** (예외, 실행 예산, 요소 512개, 호출 2000번). 화면 쪽은 `frame` 이 false 를 돌려주면 스크립트를 내리고 기본 화면을 만든다.
- **이벤트가 놓는 글자는 화면 스크립트와 별개다** (사용자 결정): 게임이 늘 그린다. **미션은 HUD 를 들고 오지 않는다** (사용자 결정): 화면 스크립트는 설치형뿐이고 MIF2 에 넣지 않는다.
- 화면 스크립트는 `references_max` 를 8000 으로 올린다 (기본 100 으로는 `build` 가 돌지 못한다). 스크립트 이벤트는 기본값 그대로다.
- 설정 `General` / `AllowUiScript`(기본 true)는 `AllowEventScript` 처럼 OPTION 화면에 없고 RESET 이 건드리지 않는다.
- 예제 `godotdata/ui/samples/hud.sgd` 는 기본 HUD 를 그대로 옮긴 것이다. **`ui/maingame.gd` 의 기본 HUD 를 고치면 이 예제도 같이 고친다.** 확인은 `--ui-script godotdata/ui/samples/hud.json` 과 `--ui-shot` 으로 기본 화면과 픽셀을 견준다 (3D 무기 표시는 도는 중이라 조금 다르다). API 를 고치면 `docs/modding.md` 의 "화면 스크립트"도 고친다.

## 에디터

- **에디터는 하나다** (사용자 결정, 2026-10-08. 전에 셋으로 나누기로 한 것을 취소했다): 블록(BD2), 포인트(PD2), 미션(MIF2), 에셋(`godotdata/` 의 JSON 과 에드온에 넣을 JSON)을 모드로 오간다. **3D 화면은 블록과 포인트 모드에서만 보이고, 미션과 에셋 모드는 3D 화면을 덮는 별도의 화면이다.** 씬은 `scenes/editor.tscn` 하나이고 실행 인자로 들어간다 (`-- --scene editor`). 주 클래스는 `XopsEditor` 이고 관심사별 partial(`XopsEditor*.cs`)로 나뉜다. 원본 형식(BD1, PD1, MIF)은 편집하지 않고 변환해 가져온다.
- 미션 모드(`XopsEditorMission.cs`): 문서가 미션의 설정(`MapDocument.Mission`, 늘 있다)을 들고, 고칠 때는 `EditMission` 을 거친다 (JSON 으로 뜬 전후를 `ActionCommand` 로 기록). 블록·포인트의 경로는 저장할 때 문서의 경로를 적는다. 하늘과 이벤트 묶음이 바뀌면 `ApplyMission` 이 3D 화면과 이벤트 목록을 다시 맞춘다.
- 이벤트 편집 (`XopsEditorEvents.cs`, 사용자 결정: 권고안 그대로): 이벤트도 포인트이므로 포인트 모드에서 고친다. 이벤트 종류의 이름과 칸은 `EventCatalog` 가 준다 (원본 10 에서 19 는 코드에서, 스크립트 이벤트는 등록 JSON 에서. 스크립트는 로드하지 않는다). 칸은 `kind` 로 입력 방식을 고른다 (목록에서 고르기, 화면에서 고르기). 노드 그래프 화면은 만들지 않고 3D 의 연결선(`LinkOverlay`)과 목록의 이벤트 보기로 줄을 보여 준다. **가리키는 이벤트가 없는 출구는 오류가 아니다** (줄이 거기서 끝난다). 이벤트의 식별번호는 종류가 달라도 이벤트 전체에서 겹치지 않게 준다 (`NextEventId`). 미션 변수에 이름을 붙이는 것은 아직 없다 (저장할 자리를 정해야 한다).
- 에셋 모드(`XopsEditorAssets.cs`, `AssetFile`. 사용자 결정: 밖으로 뺀 데이터 전부를 고친다): 데이터 클래스를 리플렉션으로 훑어 키와 값의 트리를 만든다 (종류마다 화면을 따로 만들지 않는다. 데이터 클래스에 필드를 더하면 에디터에 바로 나온다). **파일에 있던 최상위 키만 다시 쓴다** (`AssetFile.Fields`. 기본 데이터는 한 클래스를 여러 파일에 나눠 적고 에드온 데이터는 목록 섹션만 갖는다). 파일의 종류는 키로 알아낸다 (`AssetFile.Kinds`. 종류를 더하면 여기에 한 줄). 고칠 때는 `EditAsset` 을 거친다 (파일 내용의 JSON 전후를 기록). `config.json` 은 넣지 않는다 (설정이고 OPTION 화면이 있다). 기본 데이터를 저장하면 `DataManager.Reload()` 로 다시 읽는다.
- 에셋 모드의 번호 칸(`AssetReference`, `XopsEditorReferences.cs`): "이 필드는 저 목록의 번호다"는 표를 에디터가 따로 갖는다 (게임은 쓰지 않는다). **데이터 클래스에 다른 목록을 가리키는 번호 필드를 더하면 `AssetReference` 의 표에도 한 줄을 더한다.** 트리의 세 번째 칸이 이름을 보여 주고 누르면 목록에서 고른다. 목록은 `ListOf`(열려 있는 파일의 편집 → 게임의 데이터)와 `AddonListOf`(미션의 에드온 파일, 10000 부터)로 찾는다.
- 에셋 모드의 미리 보기(`XopsEditorPreview.cs`): 자기만의 3D 공간을 가진 `SubViewport` 에 선택한 항목의 모델을 게임과 같은 코드로 조립한다 (`WeaponVisual.BuildModelParts`. 사람은 `HumanVisual.CreateHumanVisual` 의 배치를 따라 직접 조립한다: `Human` 노드를 만들지 않는다). 종류를 더하려면 `ShowPreviewModel` 의 분기에 더한다. 번호로 가리키는 것은 `LookupData` 로 찾는다 (열려 있는 파일의 편집 → 게임의 데이터, 10000 이상은 미션의 에드온 파일). 같은 항목을 고치는 동안에는 시점을 다시 맞추지 않는다. 팔과 다리는 메시 목록에서 넘겨 본다 (`m_previewArm`, `m_previewLeg`). 무기를 쥔 자세(`AddHeldWeapon`, 사용자 결정: 사람이 아니라 무기 쪽에 팔을 붙이고 텍스처 없이, 켜고 끌 수 있게)는 게임의 `HumanVisual` 을 몸통·다리를 감춘 채 그대로 쓴다 (`CreateHumanVisual(null, 첫 사람)` → `ApplyArmModel` → `WeaponVisual.Build`). 사람의 히트박스(`BuildHitboxWire`)는 `HumanHitbox.Contains` 와 같은 자리에 그리므로 그 계산을 고치면 같이 고친다. 미리 보기의 사람 모델은 게임처럼 Y 180° 돌려 놓는다 (히트박스와 방향이 맞게).
- 이펙트의 미리 보기는 `EffectPreview`(에디터 전용)가 되풀이해 재생한다. `EffectManager` 는 게임의 3D 공간과 읽어 둔 데이터에 묶여 있어서, 미리 보기 공간에서 고치는 중인 데이터로 재생하려고 같은 계산을 따로 갖는다. **`EffectManager` 의 `Spawn` / `Tick` / `ApplyTransform` 을 고치면 `EffectPreview` 도 같이 고친다** (맵 충돌은 미리 보지 않는다).
- `DataManager.Reload()` 는 데이터 객체를 새것으로 바꾼다. 맵이 로드돼 있지 않을 때만 부른다 (에디터는 사람을 스폰하지 않으므로 괜찮다).
- 값 몇 개를 통째로 바꾸는 편집(줄의 시작 번호, 메시지, 미션의 설정, 데이터 파일)은 `ActionCommand` 로 기록한다. `AfterHistoryStep` 이 그 `Changed` 로 무엇을 다시 그릴지 고른다.
- **파일의 내용(모델)을 편집한다** (`MapDocument`). 게임 상태를 편집하지 않고 사람을 스폰하지 않는다. 블록만 `MapLoader.LoadBlockData` 로 띄우고 포인트는 표식(`PointMarkers`)으로 그린다.
- "화면은 GDScript, 창구만 부른다" 규칙의 예외다. 전부 C# 이고 `char.dds` 가 아니라 Godot 기본 컨트롤을 쓴다. 화면의 글자는 영어다.
- **조작은 블렌더의 기본 키를 따른다** (사용자 결정. 3D CAD 식 조작은 쓰지 않는다): 가운데 버튼으로 시점(`EditorCamera`), 넘버패드 시점, 왼쪽 클릭·Shift·사각형 선택, A / Alt+A, Alt+Z(X-RAY), 앞으로 G / R / S 변형. **키로 하는 일은 메뉴나 버튼으로도 할 수 있어야 한다** (사용자 결정). 날아다니기(오른쪽 버튼을 누른 채 이동 키)는 함께 둔다. 오른쪽 버튼을 누르고 있는 동안에는 다른 키를 받지 않는다.
- 편집은 전부 되돌릴 수 있어야 한다: 바꾼 뒤 `IEditorCommand` 를 `EditorHistory` 에 넣는다 (포인트의 값은 `PointChangeCommand`). 문서의 포인트 객체는 표식과 목록이 가리키므로 객체를 바꾸지 않고 값만 옮긴다 (`PointChangeCommand.Copy`).
- 변형(G / R)은 진행 중인 상태(`Transforming`)다: 그동안 클릭은 확정, 오른쪽 버튼은 취소이고 선택과 날아다니기는 막힌다. 마우스로 옮기기는 화면과 나란한 면 위에서, 축을 묶으면 그 축 위에서 한다. 포인트의 돌리기는 세로축 둘레뿐이다 (방향이 yaw 하나다).
- **키는 물리 키 위치(`PhysicalKeycode`)로 읽는다.** 글자 코드로 읽으면 한글 입력 상태에서 Alt+Z 같은 키가 듣지 않는다 (사용자가 겪었다).
- **스냅은 하나로 합쳤다** (`XopsEditorSnap.cs`, 사용자 결정 2026-10-08: 전의 Surface 와 Grid 체크를 없애고 Snap 켜기 + 대상 메뉴로): 대상은 `SnapTarget`(Grid, Vertex, Edge, EdgeCenter, Face, FaceCenter. 여러 개를 함께 켠다)이고 **포인트 모드와 블록 모드가 따로 기억한다** (기본: 포인트 Face, 블록 Vertex. 사람 같은 포인트는 바닥에 둬야 한다). Ctrl 은 켜고 끈 상태를 잠깐 뒤집는다. 찾는 순서는 `FindSnapPoint`: 점(꼭짓점·모서리 가운데·면 가운데) → 모서리 → 면 → 격자. 대상에 맞춰지는 것은 변형을 시작할 때 마우스에 가장 가까웠던 것(`m_transformAnchor`), 격자의 기준은 전처럼 맨 앞의 것이다 (움직이지 않은 축은 그대로). 옮기는 블록 자신은 대상에서 뺀다. 블록의 면과 가려짐은 문서의 블록에서 직접 계산한다 (`PickFace` 의 건너뛰기 인자. 화면의 블록은 옮기기 전의 모양이다). 포인트의 Face 는 게임의 판정(`SurfaceUnder`)을 쓴다. 새 포인트도 마우스 아래의 면에 놓인다. 단위는 화면에서 바꾼다 (기본 0.1 m).
- **정해진 시점(정면·측면·위와 반대쪽)은 늘 직교다** (사용자 결정): 들어가면 직교로 바꾸고(`EnterFixedView`), **직교에서 시점을 돌리거나 날아다니면 어떻게 켠 직교든 원근으로 넘어간다** (`LeaveFixedView`. 사용자 결정: 직교에서 돌아가는 화면은 자연스럽지 않다). 옮기기와 확대·축소는 직교를 유지한다. 직교에서는 카메라 노드를 중심에서 더 물려 둔다 (`EditorCamera` 의 `k_orthographicBackoff`. 물리지 않으면 중심보다 가까운 것이 잘린다). 그래서 카메라의 자리가 필요하면 노드의 위치가 아니라 시점의 상태에서 구한다.
- **격자 표시** (`EditorGrid`, 사용자 결정): 직교 시점에서는 보는 방향에 가장 가까운 축에 수직인 면에, 블록 위로 겹쳐 그린다 (깊이 검사를 끈다. 맵 안을 볼 때 블록 뒤에 그리면 보이지 않는다). 원근 시점의 바닥 격자는 기본으로 끄고 View 메뉴에서 켠다 (맵의 바닥 높이가 제각각이다). 간격은 격자 단위이고 촘촘해지면 10배씩 늘린다.
- 겹친 꼭짓점·모서리 가운데 하나를 고르는 것은 Ctrl + 클릭과 함께 화면의 Ask overlap 체크로도 된다 (사용자 결정: 단축키로만 주면 안 된다).
- 포인트의 수나 순서를 바꾸는 편집(놓기, 복제, 지우기)은 `PointListCommand`(목록 전체의 전후)로, 그 뒤에는 `RebuildPoints` 로 표식과 목록을 다시 만든다. 저장은 덮어쓰기 전의 파일을 `.bak` 으로 남긴다.
- X-RAY 의 키는 블렌더와 같은 Alt+Z 하나다. 사용자의 PC 에서는 GeForce Experience 의 오버레이가 Alt+Z 를 먼저 가져가서 듣지 않았는데, 다른 사람에게는 되므로 대체 키를 두지 않기로 했다 (사용자 결정. 체크 상자로 켤 수 있다).
- **X-RAY**: 켜면 가려진 것도 **보이고**(표식의 깊이 검사를 끈다. 사용자 결정) 선택된다. 끄면 보이는 것만 선택한다. 가려졌는지는 `MapLoader.RaycastBlock(BlockLayer.Sight, ...)` 로 본다.
- 블록 편집 (사용자 결정): 점·선·면·블록 단위로 선택한다 (`BlockElement`, 키 1 에서 4). 블록은 꼭짓점 8개짜리 육면체이고 블록끼리 꼭짓점을 공유하지 않으므로, 클릭은 같은 자리에 겹친 것을 함께 선택하고 **Ctrl + 클릭으로 특정 블록의 것만 고를 수 있다** (`CollectCoincident`, 겹친 것 메뉴). 블록 하나를 쪼개거나(Loop Cut 같은 것) 육면체를 벗어나게 하는 기능은 넣지 않는다. 그래서 지우기와 복제는 블록 전체에만 듣는다.
- 블록 요소의 키는 `블록 번호 × ElementStride + 요소 번호`다. 변형은 선택한 요소의 꼭짓점들을 움직이고(`CollectVertices`), 포인트와 같은 코드(`XopsEditorTransform.cs`)를 쓴다.
- 화면의 블록은 문서(`MapDocument.Blocks`)를 `MapLoader.LoadBlockData(BD2File)` 로 넘겨 만든다. 블록을 고친 뒤에는 `RebuildBlocks`. 옮기는 도중에는 메시를 다시 만들지 않고 덧그림(`BlockOverlay`)만 다시 그린다. 문서의 배열을 로더가 그대로 가리키므로 블록의 값은 배열을 바꾸지 않고 옮긴다 (`BlockChangeCommand.Copy`).
- 블록의 값(`XopsEditorBlockInspector.cs`): 통과 플래그는 선택한 요소가 속한 블록에, 텍스처·재질·UV 는 면 단위면 선택한 면에, 블록 단위면 여섯 면 전부에 넣는다 (`TargetFaces`). 고칠 때는 `EditBlocks` / `EditFaces` 를 거쳐 되돌리기 기록과 다시 만들기를 함께 한다.
- 텍스처 목록은 문서가 갖고(`MapDocument.Textures`) 블록과 함께 저장한다. 자리를 지우지 않는다 (번호가 밀린다). 다른 이름으로 저장하면 텍스처 목록도 블록 파일 옆의 `이름_textures.json` 으로 따로 쓴다. 로더에는 `LoadBlockData(BD2File, BlockTextureListData)` 로 문서의 목록을 넘긴다 (파일의 목록을 다시 읽지 않게).
- 면을 고르는 레이는 문서의 블록에서 직접 계산한다 (`PickFace`). `MapLoader.RaycastBlock` 은 통과 플래그가 켜진 블록을 맞히지 못한다.
- **플레이 테스트** (`XopsEditorPlay.cs`, F5): 문서를 `editor_temp/` 의 임시 파일 한 벌로 쓰고(`WritePlayFiles`. 문서의 경로와 "바뀜" 표시는 건드리지 않는다) `Game.LoadMissionFile` 로 로드한 뒤 `Game.HoldSceneAndChange` 로 메인게임에 넘어간다. **에디터 씬은 지우지 않고 트리에서 떼어 `GameBridge` 가 맡아 둔다** (`GameBridgeHold.cs`). 그래서 편집 내용·되돌리기 기록·시점이 그대로 돌아온다. 메인게임(`ui/maingame.gd`)은 `Game.HasHeldScene()` 이면 메뉴·결과 화면 대신 `Game.ReturnToHeldScene()` 으로 나간다. 에디터는 떨어져 있는 동안(`m_playing`) `_ExitTree` 에서 블록을 내리지 않고, 돌아오면 `ResumeFromPlay` 가 창과 3D 화면을 되돌린다. 미션의 설정은 문서가 든 미션 파일의 내용(`MapDocument.Mission`)을 그대로 쓴다.
- **원본 가져오기**: BD1 / PD1 하나는 `MapLoader.ConvertBD1` / `ConvertPD1` 으로 바꿔 이름 없는 문서로 연다 (파일을 쓰지 않는다). 미션(MIF, 공식 미션)은 `ConvertMissionToExtended` 로 한 벌을 쓰고 그것을 연다 (미션 파일과 추가 사물의 데이터가 함께 있어야 해서).
- 포인트의 모델 (`XopsEditorPointModels.cs`, 사용자 요청): 사람·무기·소물 포인트는 표식의 상자 대신 실제 모델을 보여 준다 (Models 체크, 기본 켜짐). `PointMarkers.ModelFactory` 로 `BuildPointModel` 을 넘기고, 조립은 에셋 미리 보기와 같은 코드(`BuildHumanModel`, `AddObjectModel`, `LookupData`)를 쓴다. **여전히 사람을 스폰하지 않는다** (`Human` 노드를 만들지 않는다). 사람은 게임의 `HumanVisual` 을 그대로 써서 무기를 쥐여 준다 (`BuildArmedHumanModel`, 사용자 요청: 종류 1 과 6 에 따라 든 무기와 팔 모양이 달라야 한다). 슬롯은 `Human.EquipInitialWeapons` 와 같고(주 무기 `weaponIndex1`, 종류 6 은 주 무기 없음), **주 무기가 없으면 맨손(None 무기)과 그 팔 자세 그대로 둔다. 보조 무기로 바꿔 보여 주지 않는다** (사용자 결정). 에드온 번호의 모델을 쓰는 사람만 전의 방식(무기 없음, 팔의 첫 메시, 다리는 Idle 의 첫 프레임)으로 그린다: 에디터는 에드온 데이터를 게임에 붙이지 않는다. 무기는 `WeaponManager.Spawn` 과 같은 자세(옆으로 눕힘), 소물은 `SmallObject.CreateObject` 와 같은 배치이므로 그쪽을 고치면 같이 고친다. 선택과 가려짐 판정은 전처럼 상자의 가운데로 한다 (모델의 모양으로 고르지 않는다). 모델은 깊이 검사를 하므로 X-RAY 에서는 상자를 함께 그린다.
- 포인트 편집 (사용자 결정): 포인트 단위로만 선택한다. 여러 개를 선택하면 무엇을 고칠지 드롭다운으로 고른다.
- 에디터는 안개를 끄고(`MapLoader.ClearFog`) 카메라의 far 를 늘린다. 게임의 안개와 far 는 맵 전체를 멀리서 보기에 짧다.
- 표식처럼 단색으로 그리는 3D 는 `StandardMaterial3D` 를 반투명 패스(`Transparency = Alpha`)로 쓴다. 불투명 패스의 단색 머티리얼은 이 프로젝트에서 거의 검게 나온다 (원인은 확인하지 못했다).
- 에디터를 고친 뒤에는 `-- --scene editor --selftest`(헤드리스 가능)를 돌리고, 화면은 `--open 미션.mif2 --select 번호 --focus --screenshot 경로.png` 로 직접 본다. 시험용 맵은 `mif2_check` 의 `--convert-official 번호 build/editor_sample` 로 만든다.

## 화면 (씬 UI)

- 화면은 `scenes/`의 씬 6개다: `boot` → `opening` → `mainmenu` → `briefing` → `maingame` → `result`. 각 씬은 루트 노드와 `ui/`의 GDScript 하나만 갖고, 화면 요소는 스크립트가 `_ready`에서 코드로 만든다. `maingame`만 `PlayerController`(C#) 노드를 자식으로 둔다.
- **수치는 화면별 GDScript 맨 위 상수 표에 모은다** (위치, 글자 크기, 색, 시간). 값의 출처는 UnityXOPS 0.4의 `unitydata/scene/*.lua`다. 다만 Lua 의 반투명 값은 Unity 가 선형 색 공간에서 섞은 것이라, 아주 옅은 값은 그대로 쓰면 보이지 않는다 (브리핑·결과 배경의 타이틀은 Lua 0.012 → 0.1). 나중에 외부 데이터로 뺄 때 그 표만 옮기면 된다.
- 공용 도우미는 `ui/common/`: `XopsUI`(요소 만들기·배치), `XopsText`(`char.dds` 스프라이트 글자), `XopsLayer`(층과 배율), `XopsLines`(스코프 조준선), `xops_dev.gd`(Autoload `Dev`, 개발용 인자).
- **배치 좌표는 UnityXOPS 화면 좌표 그대로다**: 기준점(화면이나 부모 안의 한 점)에서의 오프셋, +x 오른쪽, +y **위쪽**. `XopsUI`가 Godot 좌표로 바꾼다. 화면 스크립트에서 y 부호를 직접 뒤집지 않는다.
- **층의 배율은 두 가지다** (`XopsUI.layer(parent, order, scaled)`): `scaled = true`는 화면 높이를 480으로 보고 확대하고, `false`는 픽셀 1:1에 설정의 `UIScale`을 곱한다. 어느 요소가 어느 쪽인지는 Lua 를 따른다 (HUD·메뉴는 픽셀, 스코프·중앙 문구·암전은 확대).
- **GDScript 는 창구 Autoload 만 부른다**: `Game`(`GameBridge`), `EventManager`, `ConfigManager`, `InputManager`. 게임플레이 노드를 직접 만지지 않는다. 창구의 좌표·각도 인자는 UnityXOPS 공간이다 (카메라 위치, 무기 표시 자리).
- 미션 로드가 실패한 이유는 `Game.LastLoadError()`(영어 한 줄, 로드 중 처음 남은 에러 로그)로 읽는다. 메뉴는 미션을 눌렀는데 실패하면 타이틀 아래에 그 문구를 잠깐 띄운다 (`LOAD_ERROR` 상수. 원본의 "block data open failed" 에 해당).
- `Game`이 하는 일: 화면 전환(`ChangeScene`), 맵 로드(`LoadOpening`, `LoadDemo`, `ReloadBackground`, `LoadMission`, `LoadMissionFile`, `LoadMapFiles`, `BeginMission`, `RestartMission`, `ReloadMission`, `UnloadMap`, `UnloadMission`), 장면 카메라, 벽 블라인드 판정, 미션 목록·브리핑·통계 조회, HUD 가 읽는 플레이어 값, 3D 무기 표시(`HudWeaponView`), 밝기·감마 사각형.
- 오프닝과 메뉴 배경은 `AIController.DrivePlayer = true`로 플레이어까지 AI 가 움직이고 이벤트는 돌지 않는다. 메인게임이 들어올 때 `Game.BeginMission()`이 되돌린다.
- 시점 전환(F1)과 스코프 입력은 `PlayerController`가 처리한다. HUD 는 상태를 읽어 그리기만 한다.
- OS 글꼴 글상자(`XopsUI.label`: 브리핑 본문, 이벤트 메시지, 크레딧)는 자기가 속한 층의 글꼴(`XopsLayer.os_font()`)을 쓴다. 층이 배율만큼 `oversampling`을 맞춰 두므로 확대돼도 흐려지지 않는다. 공용 `XopsUI.os_font()`를 글상자에 직접 넣으면 높은 해상도에서 흐려진다.
- 글꼴은 OS 언어로 고른다 (한국어 맑은 고딕, 일본어 Yu Gothic, 그 밖 Segoe UI. 없는 글자는 시스템의 다른 글꼴로 넘어간다). 텍스트 파일은 BOM → 유효한 UTF-8 → OS 언어의 옛 코드 페이지(한국어 CP949, 일본어 Shift-JIS, 그 밖 1252) 순으로 읽는다 (`EncodingHelper`). UnityXOPS 와 같은 방식이다.
- 크기가 없는 노드에 직접 그리는 요소(`XopsText`)는 그릴 범위를 `RenderingServer.canvas_item_set_custom_rect`로 알려 줘야 한다. 그러지 않으면 기준점이 화면 밖일 때 화면 안에 걸친 부분까지 통째로 그려지지 않는다 (HUD 의 STATE 상자 아랫줄이 그렇게 사라졌었다).
- `PlayerController`는 입력을 넣은 직후 `Controller.ApplyVisual()`을 한 번 더 부른다. 사람 노드의 회전·팔 각도가 `PlayerController`보다 먼저 갱신되므로, 다시 맞추지 않으면 1인칭 팔이 카메라보다 한 프레임 늦게 돈다.
- 3D 무기 표시는 자기만의 3D 공간을 가진 뷰포트다. 무기 모델이 맵과 같은 안개 셰이더를 쓰므로 공간 전체를 1/100로 줄여 카메라 바로 앞에 둔다. 뷰포트는 표시 크기 × UIScale 로 렌더링한다 (표시 크기 그대로면 확대될 때 계단이 진다).
- 메뉴의 OPTION(설정 화면)은 `ui/mainmenu_option.gd`(`MenuOption`)가 만든다. 구성과 수치는 UnityXOPS 0.4의 `mainmenu.lua`다. 탭은 General / Input / Graphic / Sound. 값은 바꾸는 즉시 `ConfigManager`에 들어가 화면에 반영되고(밝기·감마, 음량, 키 바인딩), SAVE 가 파일에 쓰고 `ApplyGraphic`을 부른다. UIScale 은 SAVE 때 적용한다 (바로 적용하면 누르고 있던 화살표가 움직인다. 사용자 결정). BACK 과 ESC 는 `RevertToSaved`로 되돌린다. playerName 은 쓰는 곳이 없어 화면에 넣지 않았다 (사용자 결정).
- 디버그 콘솔(원본의 F11 콘솔)은 `config.json`의 `General` / `AllowConsole`이 true 일 때만 Autoload `Dev`가 만든다 (사용자 결정. 실행 인자가 아니라 설정이다). **씬에 속하지 않아서 오프닝·메뉴·브리핑·메인게임·결과 어디서든 열리고, 명령은 그때 로드돼 있는 맵(배경 맵 포함)에 그대로 적용된다** (사용자 결정. 메뉴에서도 AI 가 돌고 있으므로 `kill` 같은 명령이 먹어야 한다). 콘솔에는 `Debugger` 의 로그도 나온다 (경고 주황, 에러 빨강. `Game.ConsoleTakeLogs`). 이 설정은 OPTION 화면에 넣지 않고 유저가 파일을 직접 고친다. OPTION 의 RESET 도 이 값은 건드리지 않는다 (`ConfigManager.ResetToDefaults`). 화면과 글자 입력은 `ui/common/xops_console.gd`(`XopsConsole`), 명령 표와 실행은 `src/Scene/DebugConsole.cs`(순수 클래스)이고 `Game.ConsoleExecute`로 잇는다. 명령을 추가할 때는 `DebugConsole` 생성자의 표에 한 줄을 더하고 `docs/development.md`의 명령 표도 고친다. 화면이 해야 하는 일(지우기, 재시작, 화면 저장, 씬 전환 `scene:이름`)은 명령이 `Game.ConsoleTakeAction`으로 넘긴다. `restart`는 지금 씬의 `console_restart()`를 부른다 (메인게임은 미션, 오프닝은 연출 전체, 메뉴는 배경 맵. 그 함수가 없는 씬은 다시 시작할 것이 없다). `loadmap 블록 포인트 [하늘]`, `loadmission 번호 [skipbriefing]`(공식 미션의 목록 인덱스), `loadmissionmif 미션파일 [skipbriefing]`은 맵을 로드해 메인게임(또는 브리핑)으로 넘어간다 (`Game.LoadMapFiles`, `LoadMission`, `LoadMissionFile`). skipbriefing 의 기본값은 true 다. 명령 이름만 소문자로 바꾸고 인자는 대소문자를 그대로 넘긴다 (파일 경로). 큰따옴표로 묶으면 띄어쓰기가 있어도 인자 하나다. 글자는 OS 글꼴이다 (사용자 결정. `char.dds`는 글자 크기 문제가 있다). **콘솔의 입력과 출력(사용법, 설명, 결과, 디버그 텍스트)은 영어만 쓴다** (사용자 결정). `ui_check`가 출력에 영어가 아닌 글자가 있는지 본다. 사람은 `MapLoader.Humans`의 인덱스로 가리킨다. 좌표를 받는 명령(`teleport x y z`)은 `info`가 보여 주는 것과 같은 Godot 공간의 미터 값을 받는다 (사용자 결정. 맵을 만드는 사람은 원본 XOPS 좌표를 잘 쓰지 않는다).
- 콘솔의 `flight`(비행 모드)는 `HumanController.SetFlight`로 켠다. 원본에 없는 기능이고, 켜져 있고 살아 있는 동안 `Tick`이 평소의 이동·충돌 대신 `TickFlight`만 돈다 (기존 이동 코드는 건드리지 않는다). 시선 방향으로 움직이고 점프는 무시하며, `HumanCollision`도 그 사람을 건너뛴다. 총알 판정은 그대로다. 날고 있는 동안은 접지한 것으로 친다 (`Grounded` = true, 공중 조준 오차 없음. 사용자 결정). 끄면 그 자리에서 공중 상태가 되고 평소의 이동이 접지 여부를 다시 구한다.
- 콘솔의 `collider`는 `src/Scene/ColliderView.cs`(`Game`의 자식 노드)가 그린다. 판정 코드와 같은 식으로 자리를 구해서 그리므로, 판정 계산(`HumanHitbox.Contains`, `WeaponManager.TickPickup`, `SmallObject.Contains`)을 고치면 이 표시도 같이 고친다.
- 글자를 입력받는 화면은 열려 있는 동안 `InputManager.InputBlocked`를 켠다. 켜져 있으면 `InputManager`의 모든 조회가 "안 눌림"을 돌려준다. 끌 때는 키 이벤트를 받는 도중이 아니라 `_process`에서 끈다 (도중에 끄면 콘솔을 닫은 Esc 가 게임 쪽에서 "메뉴로 나가기"로 읽힌다).
- 화면의 클릭은 `InputManager.WasClickPressed` / `WasClickReleased` / `IsClickPressed`(마우스 왼쪽 버튼 고정)로 읽는다. `"fire"` 액션으로 읽으면 발사 키를 바꿨을 때 메뉴를 누를 수 없게 된다.

## 맵과 렌더링

- `MapLoader`(Autoload)가 블록·스카이·미션 정보를 들고 있고, 씬이 바뀌어도 유지된다. 로드 함수는 이전 것을 먼저 언로드한다.
- 블록 충돌은 `MapLoader.RaycastBlock` / `IsInsideBlock`으로 직접 계산한다 (엔진 물리 미사용). 블록 앞면만 맞는다.
- 충돌 조회는 블록을 하나씩 보되 범위 상자로 먼저 거른다. **거르는 것은 결과를 바꾸지 않아야 한다.** 내부 판정과 사람 충돌은 8정점 범위(`boundsMin` / `boundsMax`, 원본과 같다), 레이는 따로 구한 범위(`rayBoundsMin` / `rayBoundsMax`, `Block.ComputeRayBounds`)를 쓴다. 레이 판정은 "한 면의 평면과 만나고 나머지 면의 안쪽"이라 면이 뒤틀린 블록에서는 맞는 자리가 8정점 범위를 벗어날 수 있어서, 레이를 8정점 범위로 거르면 결과가 달라진다. 범위를 구할 수 없는 블록(`rayBounded` = false)은 거르지 않는다. 조회 속도를 고치면 `bd2_check`(전부 훑은 결과와 대조)와 `block_bench`(앞뒤 시간)를 돌린다.
- **충돌 조회는 첫 인자로 판정 종류(`BlockLayer`)를 받는다. 기본값을 두지 않는다** (새 호출 지점을 컴파일러가 잡게 한다). `Human`: 사람의 이동·맵 충돌, 매몰, 발밑·이동 경로 레이, 벽 블라인드, 3인칭 카메라, 떨어진 무기와 소물의 바닥, AI 의 점프·낭떠러지 확인. `Bullet`: 총알 소멸, 수류탄 반사, 혈흔 입자. `Sight`: AI 시야·사선, 폭발 가림. `MapLoader` 가 판정별 블록 목록 셋을 들고 있다 (`GetBlockColliders`).
- **블록 재질** (`godotdata/block_material_data.json`, `MapLoader.GetFaceMaterial(블록, 면)`): BD2 의 면 재질 번호가 재질 목록을 가리키고 −1 은 `MapLoader.DefaultBlockMaterial`(기본 0, 나중에 MIF2 가 정한다)이다. **BD1 블록은 면 재질 번호가 없고 모든 면이 0번 재질이다. 0번에 원본의 벽 착탄 연기와 착탄음이 들어 있다** (사용자 결정. 탄환 데이터의 `wallHitEffectIndex` 는 없앴고 `wallHitSounds` 는 수류탄이 튕기는 소리 `bounceSounds` 로 바꿨다). 블록에 맞은 탄은 항상 재질의 `hitEffect`·`bulletHoleEffect`·`hitSounds`를 쓰고(`GetFaceMaterial` 은 null 을 돌려주지 않는다), 탄흔은 한 틱에 한 번만 남긴다. 총알이 통과하는 블록(`PassBullet`)에서는 아무것도 내지 않는다 (물 같은 특수 블록은 나중에 BD2 를 확장할 때 한다. 사용자 결정). 기본 데이터의 재질은 0번 하나다. 오브젝트와 사람에 맞았을 때의 이펙트는 지금처럼 탄환 데이터가 정한다 (오브젝트도 재질을 쓰게 할지는 나중에 정한다).
- 블록 데이터는 확장자로 BD1 / BD2 로더가 갈리고 읽은 뒤에는 같은 구조다 (`RawBlockData` → `BuildBlock` → `Block`). BD1 의 UV 한 칸 회전과 좌표 변환은 BD1 로더 안에서 끝낸다. **판형 블록 추론(정점 모양이 유효한 입체가 아닌 블록은 어느 판정에도 걸리지 않는다)은 BD1 과 BD2 가 함께 쓴다** (사용자 결정 2026-10-08, 오리지널리티: BD2 에서는 플래그와 관계없이 모든 판정을 무시한다. 전에는 BD1 에만 있었다). 그 밖의 블록은 BD2 면 블록 플래그(`BD2File.PassHuman` / `PassBullet` / `PassSight`, 켜면 통과)대로 한다. `Block.boardShape` 가 그 결과이고 에디터가 선택한 블록에 알려 준다. **플래그는 통과 여부만 담고 나머지 비트는 예약이다** (사용자 결정. 읽을 때 무시한다). BD2 의 읽기·쓰기는 `src/IO/BD2File.cs`(게임 싱글톤과 무관), 구조는 `docs/modding.md` 에 있다.
- 추가 충돌(Additional Collision, `MapLoader.AdjustCollision`): 캐릭터-맵 충돌에서 중심축 0.9 m / 1.3 m 높이의 추가 검사 2점은 **이 플래그가 켜진 미션에서만** 돈다 (원본 `human::CollisionMap`의 `AddCollisionFlag`). UnityXOPS `HumanController`는 플래그를 무시하고 항상 검사하는데, 이는 잘못 옮긴 것이므로 따라 하지 않는다.
- 머티리얼은 `MaterialManager`의 `Create*Material`로 만든다. `alpha_clip_blend` 셰이더는 원본처럼 sRGB 값 그대로 곱하고 섞은 뒤 마지막에만 선형으로 바꾼다. 텍스처 유니폼에 `source_color`를 붙이지 않는다.
- 안개는 Godot 환경 안개가 아니라 전역 셰이더 변수(`xops_fog_color`, `xops_fog_range`)로 셰이더가 직접 계산한다 (원본의 선형 안개 재현).
- 어두운 화면(`MapLoader.DarkScreen`) 미션은 블록 면 명도를 낮추고(가산값 0.5 → 0.3), 스카이와 모델(사람·무기·소물·탄환)에 전역 셰이더 변수 `xops_model_brightness` = 0.8 을 곱한다 (원본 `RenderModel`의 darkflag). 0.8 은 코드 상수다 (사용자 결정). 블록은 머티리얼 유니폼 `dark_apply` = 0 으로, HUD 의 무기 표시는 인스턴스 유니폼 `dark_exempt` = 1 로 빠진다.
- 깊이 테스트를 끈 머티리얼(스카이)은 반투명 패스로 들어가므로 `RenderPriority`로 그리는 순서를 정한다.
- 원본 좌표에서 외적으로 법선을 구하는 코드를 옮길 때는 피연산자 순서를 뒤집는다 (원본은 왼손, Godot은 오른손 좌표계).
- 런타임에 만든 노드를 같은 프레임 안에 교체할 때는 `QueueFree` 대신 `RemoveChild` + `Free`를 쓴다. 트리에서 뗀 노드를 `QueueFree`만 해 두고 종료하면 종료 시 치명 오류가 난다.

## 문서

유저가 읽는 문서는 `README.md`(한국어, 원문), `README.en.md`, `README.ja.md`, `ROADMAP.md`, `docs/modding.md`, `docs/development.md`다. **코드나 데이터를 고치면 같은 작업 안에서 해당 문서도 고친다.** 문서가 실제 동작과 어긋난 채로 커밋하지 않는다.

- `docs/modding.md`를 고쳐야 하는 변경: 스크립트 이벤트의 API·기본 제공 이벤트·등록 파일의 키, `godotdata/` JSON 의 키 추가·삭제·이름 변경, 값의 뜻이나 단위 변경, 열거형 값, 파일 추가, 지원하는 파일 형식, 에드온 페이지 방식, 풀 크기 같은 제한.
- `docs/development.md`를 고쳐야 하는 변경: 빌드·익스포트 방법, 폴더 구조, 틱 순서(`SimOrder`)와 프레임 순서, Autoload 순서, 점검 씬과 인자, 개발용 실행 인자, 디버그 콘솔의 명령, 원본과 다르게 하기로 한 동작, 코드 규칙. 버전 규칙은 문서에 넣지 않는다 (사용자 결정. `TODO.md`에만 있다).
- `ROADMAP.md`(한국어)는 버전별 현황이다: 버전, 이름, 상태(설계 중 → 작업 중 → 릴리즈됨), 항목 체크리스트. 버전에 넣을 것이 정해지거나 항목이 끝나거나 릴리즈하면 고친다. 항목은 유저가 읽는 수준으로만 적고, 설계의 세부 결정은 `TODO.md`에 둔다.
- `README.md`를 고쳐야 하는 변경: 기능 목록, 설치 방법, 기본 키, 최신 릴리즈 버전, 앞으로 할 것(`ROADMAP.md`와 맞춘다). 고치면 `README.en.md`와 `README.ja.md`도 같은 내용으로 고친다 (두 번역은 맨 위에 AI 번역임을 알린다).
- 키의 뜻은 이름으로 추정하지 않고 그 값을 쓰는 코드를 확인해서 적는다.
- 한국어 문서의 용어: "에드온", "오브젝트"(소물), 오브젝트의 `hp`는 "체력". 코드 주석과 이 파일·`TODO.md`는 "어드온", "소물" 그대로 쓴다.
- 한 줄에 물결표(`~`)를 두 번 쓰지 않는다 (GitHub 이 그 사이를 취소선으로 그린다). 범위는 "0 에서 255 사이"처럼 쓴다.
- `LICENSE`와 `godotdata/global.json`의 라이선스 문구는 같게 유지한다.

## Autoload 순서

`ConfigManager` → `DataManager` → `InputManager` → `MaterialManager` → `SimClock` → `MapLoader` → `BulletManager` → `WeaponManager` → `EffectManager` → `SoundManager` → `EventManager` → `Game`(`GameBridge`) → `Dev`(GDScript). `InputManager`는 `ConfigManager`의 바인딩을 읽으므로 뒤에 와야 한다. 매니저를 추가할 때 의존 순서대로 `project.godot`의 `[autoload]`에 넣는다.

## 설정과 입력

- `General` / `AllowEventScript`(기본 true)는 `AllowConsole` 처럼 OPTION 화면에 없고 RESET 이 건드리지 않는다. 꺼져 있으면 스크립트 이벤트를 쓰는 미션은 로드하지 않는다.
- `godotdata/config.json`은 `ConfigManager`가 읽고 쓴다. 파일이 없으면 코드 기본값(`ConfigManagerDefault.cs`)으로 새로 만든다. 새 설정은 기본값 목록에 추가하면 기존 파일에도 자동으로 병합된다.
- `ConfigManager.ApplyGraphic`이 부팅 때 창 모드와 렌더 해상도를 적용한다(기본: 전체화면, 640×480 렌더, 화면비가 다르면 검은 띠). 도구 씬은 `_Ready`에서 창 설정을 되돌린다(`AssetViewer` 참조).
- 입력은 `InputManager`를 거쳐 읽는다: `IsPressed` / `WasPressed` / `WasReleased`(버튼), `ReadVector`(move, look), `IsKeyPressed` / `WasKeyPressed`(치트 키 등 바인딩 밖의 키), `IsClickPressed` / `WasClickPressed` / `WasClickReleased`(화면 클릭). Godot `Input`을 직접 부르지 않는다.
- `ReadVector`는 X 오른쪽 +, Y 위쪽(전진) + 다. look은 마우스 이동량(픽셀)이다.
- 바인딩 경로는 `<Keyboard>/w`, `<Mouse>/leftButton` 형식이고 `InputPath`가 Godot 이벤트로 변환한다. 키보드는 물리 키 위치 기준이다.

## 확장 미션(MIF2)과 에드온 데이터

- 미션 파일은 확장자로 MIF / MIF2 로더가 갈린다 (`MapLoader.LoadMissionFile`). **MIF2(JSON, `ExtendedMissionData`)는 BD2 와 PD2 만 받는다.** 원본 형식을 적으면 로드 오류다 (사용자 결정. 레거시는 MIF 로 쓴다). 경로는 전부 exe 폴더 기준이다. 미션 목록(`DataManager.ScanAddonMifs`)은 한 폴더의 `.mif` 와 `.mif2` 를 파일 이름 순서로 함께 모은다.
- **번호로 가리키는 데이터 목록은 `DataList<T>` 다. 10000 미만은 기본 데이터, 10000 이상은 미션의 에드온 데이터의 (번호 − 10000) 번째다** (사용자 결정). 포인트의 번호도 데이터 안의 상호 참조(`modelIndex` 등)도 같은 규칙이라 로드할 때 번호를 옮기지 않는다.
  - 범위 검사는 `list.Has(번호)` 로 한다. `번호 < list.Count` 로 하면 에드온 번호를 전부 거절한다.
  - 목록을 `List<T>` 나 `IList<T>` 로 받아 인덱싱하지 않는다. 그러면 에드온 번호에서 예외가 난다 (인덱서를 `new` 로 가렸기 때문에 컴파일러가 잡지 못한다). `DataList<T>` 나 `var` 로 받는다.
  - 범위를 벗어난 값을 가까운 끝으로 보던 목록(AI 레벨, 몸 크기, 히트박스)은 `GetClamped`, 목록을 도는 것(치트 무기 넘기기)은 `Neighbor` 를 쓴다.
  - 번호로 캐시하는 것은 에드온이 바뀔 때 비워야 한다 (지금은 `EffectManager` 의 머티리얼 캐시뿐. `ClearAddonMaterials`).
- 에드온 데이터 파일은 종류마다 하나이고(MIF2 의 `addonHumanDataPath` 등), 기본 데이터의 컨테이너 클래스와 같은 키를 쓰는 JSON 이다. **목록 섹션만 읽고 전역 설정(GeneralData 등)은 쓰지 않는다** (사용자 결정). `MapLoader.LoadAddonData` 가 `LoadPointData` 안에서 붙이고 `UnloadPointData` 가 뗀다. 미션을 다시 시작하면 파일을 다시 읽는다.
- 원본 MIF 의 추가 사물(예약 자리 `addonObjectIndex` 를 미션마다 덮어쓰는 방식)은 MIF 에만 남는다. MIF2 에서는 에드온 오브젝트 데이터로 적는다. 변환기(`MapLoader.ConvertMissionToExtended`)가 옮기고 소물 포인트의 번호를 10000 으로 바꾼다.
- JSON 을 쓸 때는 한글 같은 글자를 `\uXXXX` 로 바꾸지 않는다 (`JsonData.Options` 의 Encoder. 사람이 고치는 파일이다).

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
- 로그는 `Debugger`(`LogWarning`, `LogError`)를 쓴다. 메모리에 쌓여 디버그 콘솔에 색으로 나오고(익스포트 빌드에서도), 에디터에서는 Godot 출력 창에도 찍힌다. **콘솔에 나오므로 메시지는 영어로 쓴다** (콘솔의 글자는 영어만 쓴다는 사용자 결정). 매 틱 불리는 자리에는 넣지 않는다 (빌드에서도 문자열을 만든다).
- Godot 4.7 기준 deprecated API 사용 금지. 빌드 경고 0개를 유지한다.
- 주석 형식: 클래스와 함수는 `/// <summary>`, 인자는 `/// <param>`, 반환 값은 `/// <returns>`. 한 줄짜리 프로퍼티·필드 설명은 `//` 한 줄.
- 주석에는 "무엇이고 왜 그런지"를 쓴다. 원본에서 온 값이나 로직에는 출처를 적는다 (예: `원본 object.cpp:1607-1644`, `HUMAN_MAPCOLLISION_CLIMBHEIGHT`). UnityXOPS에서 옮겼다는 사실 자체는 적지 않는다.
- 중괄호는 항상 새 줄에 연다. 한 줄 `if`는 조기 반환·`continue` 같은 짧은 가드에만 쓴다.
- 데이터 클래스(`src/Data/`)의 public 필드는 JSON 키와 같은 camelCase다. 그 밖의 public 멤버는 PascalCase.
- 숫자 상수는 이름 붙은 `k_` 상수로 빼고 단위(미터, 초, 도)와 원본 값을 주석에 적는다. 원본 길이 단위는 ×0.1 해서 미터로 쓴다.
- 파일 하나에 주 클래스 하나. 폴더 구조는 UnityXOPS `Runtime/`을 따른다 (`src/Map/Human/`, `src/Map/Weapon/` 등).
- 노드일 필요가 없는 것은 순수 C# 클래스로 둔다 (`HumanController`, `HumanCollision` 참조). 매니저만 `Singleton<T>` + Autoload.
- Godot이 만드는 `.cs.uid` 파일은 커밋한다.

## 작업 방식

- **UnityXOPS가 JSON으로 빼 둔 값은 "원본 동작을 유지하면서 유저가 고칠 수 있게 한 것"이다.** 원본은 값이 코드에 박혀 있어 수정할 수 있는 것이 너무 적었다. JSON 필드가 있으면 그 값이 기준이고, 원본 상수로 하드코딩하거나 "원본과 다르다"며 JSON을 고치지 않는다. 모더용으로 열어 둔 필드(히트박스 회전, 좌우 팔 분기 등)도 원본에 없다는 이유로 무시하지 않는다. 원본과 대조해 사용자에게 올릴 것은 데이터와 무관한 계산 방식의 차이뿐이다.

- **구현 전에 설계를 먼저 논의한다.** 선택지가 있으면 권고안과 함께 제시하고 사용자의 결정을 받는다.
- **커밋과 푸시는 사용자가 요청할 때만 한다.** `main`에 직접 커밋하고, 메시지는 한국어로 쓴다.
- **조작감에 직결되는 코드(이동, 충돌, 무기, 총알, AI)는 UnityXOPS 코드와 원본 C++를 함께 대조한다.** 둘이 다르면 원본을 따르고, 무엇이 달랐는지 사용자에게 표로 알린다. 원본 분석에는 `openxops-analyzer` 에이전트를 쓸 수 있다 (사용자가 에이전트 사용을 요청한 경우).
- **구현한 것은 점검 씬으로 직접 검증한다.** 새 시스템에는 `src/Dev/`에 점검 씬을 추가하거나 기존 씬에 `--selftest`를 확장한다. 화면에 보이는 것은 `--screenshot`으로 PNG를 저장해 직접 확인하고, 그 뒤 창을 띄워 사용자가 확인하게 한다.
- **확인하지 못한 것은 확인하지 못했다고 보고한다.** 조작감처럼 직접 판단할 수 없는 것은 사용자에게 확인을 요청한다.
- **사용자에게 적어 주는 명령은 PowerShell 기준이고, 블록 하나가 재생 버튼만 눌러도 돌아가야 한다** (사용자 결정, 2026-10-06). Bash 문법이거나 앞 줄의 변수에 기대는 명령은 사용자가 그대로 돌려 볼 수 없다. 형식은 "빌드와 실행" 맨 위에 있다.
- 작업을 마칠 때마다 기존 점검 씬 전부를 다시 돌려 통과를 확인한다 (`TODO.md`의 목록 참조).
- PowerShell로 파일을 고칠 때는 UTF-8(BOM 없음)로 읽고 쓴다. `Get-Content`는 `-Encoding UTF8` 없이 쓰면 한글이 깨져 보인다.
