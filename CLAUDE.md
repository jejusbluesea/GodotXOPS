# CLAUDE.md

> **세션을 시작하면 프로젝트 루트의 `TODO.md`를 먼저 읽는다.** 진행 상황, 다음 작업, 아직 옮기지 않은 것이 적혀 있다. 작업을 끝내면 그 파일을 갱신한다.

## Project Overview

**GodotXOPS** — Godot 4.7.2 (.NET) 프로젝트. 일본 인디 FPS XOPS(2000년)의 오픈소스 구현 OpenXOPS를, 그 Unity 포팅본인 UnityXOPS를 참고해 Godot으로 옮긴다.

- 참고 원본: `C:\Users\twoj2\Desktop\Project\UnityXOPS` (브랜치 `QoL-road-to-multiplay(0.4)`), C++ 원본은 그 안의 `OpenXOPS/`
- 첫 목표: **완전 포팅**. 편의성 현대화(인게임 설정, 일시정지 메뉴, 체크포인트)와 모딩은 포팅이 끝난 뒤에 한다. 포팅은 10단계까지 끝났고 1.0.0 을 릴리즈했다 (2026-10-05, 태그 `v1.0.0`). 최신 릴리즈는 1.0.1 이다 (2026-10-05, 태그 `v1.0.1`). 다음은 1.1.0 이다 (`ROADMAP.md`, `TODO.md`).

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
- `ui/` — GDScript UI. 화면별 스크립트와 `ui/common/`의 공용 도우미.
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
- PD1 포인트의 `look`은 사람 기준 yaw 다 (원본 방향 + 180°). 원본은 사람만 방향에 π 를 더해 그리므로, 소물처럼 원본 방향 그대로 그리는 것에는 `look − 180`을 쓴다 (UnityXOPS 는 소물에도 `look`을 그대로 써서 반대로 놓인다).
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

- `res://scenes/dev/effect_viewer.tscn` — 이펙트 프리셋을 골라 눈으로 보고(`--effect 번호`, `--additive`, `--screenshot 경로.png`), `--selftest` 로 재생 수·풀 증가·블렌드 모드·발광 감쇠를 수치로 확인한다 (헤드리스 가능). 이펙트 데이터나 `EffectManager` 를 고친 뒤에 돌린다.

- `res://scenes/dev/ui_check.tscn` — 화면이 쓰는 창구 `Game`의 값과 화면 전환 흐름(로드 → 시작 → 재시작 → 내리기)을 수치로 확인한다 (헤드리스). 창구나 화면 흐름을 고친 뒤에 돌린다.

게임 자체는 씬을 지정하지 않고 실행한다 (`--path .`만). 화면을 고친 뒤에는 개발용 인자("--" 뒤)로 직접 확인한다:

- `--window 너비x높이` — 설정 파일의 전체화면 대신 그 크기의 창으로 띄운다.
- `--scene 이름 [--mission 번호 [--addon] [--page 번호]]` — 그 화면에서 시작한다 (`mainmenu`, `briefing`, `maingame`, `result`).
- `--ui-shot 경로.png [--ui-time 초]` — 화면을 PNG 로 저장하고 종료한다.
- `--ui-state 값` — 메뉴는 `credit` / `exit` / `addon` / `option` / `option-input` / `option-graphic` / `option-sound`, 메인게임은 `simple` / `off` / `console` 상태로 시작한다. `console`은 설정 파일과 무관하게 디버그 콘솔을 허용한다 (콘솔을 화면으로 확인할 때 `--ui-click "key:F11 text:help key:Enter"`와 함께 쓴다).
- `--ui-click "목록"` — 가짜 입력을 차례로 넣는다: `x,y`(클릭), `x,y,초`(누르고 있기), `key:이름`(키 한 번), `text:글자`(글자를 차례로 친다. 띄어쓰기는 `key:Space`). 좌표는 창 픽셀이고 실제 커서를 옮긴다. 버튼을 눌러 본 결과를 `--ui-shot`으로 볼 때 쓴다 (`--window 640x480`과 함께).
- `--ui-quit 초` — 그 시간 뒤 종료한다. `--headless`와 함께 써서 화면 스크립트에 오류가 없는지 본다.

`play_test.tscn` 은 AI 와 이벤트를 켠 채로 돈다. `--noai` 로 끄고 시작하고, 창에서는 F2(AI 정지/재개), F4(전원 비전투), End(전원 경계), F9+↑/↓(복제), Insert(플레이어 무적), Home(디버그 텍스트 켜기/끄기)을 쓴다. `--invincible`, `--notext` 로 켜고 끈 채 시작할 수 있다. AI 가 꺼져 있어야 하는 점검 도구는 `AIController.Enabled = false` 로 둔다 (`WeaponCheck` 참조).

## 익스포트 빌드

```powershell
& "C:/Users/twoj2/Desktop/Game Engine/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . --export-release "Windows Desktop" build/windows/GodotXOPS.exe
```

- 프리셋은 `export_presets.cfg`의 "Windows Desktop" 이다. `build/`는 커밋하지 않는다 (`.gitignore`, `.gdignore`).
- 결과물: `GodotXOPS.exe`, `GodotXOPS.pck`, `data_GodotXOPS_windows_x86_64/`(.NET 런타임과 어셈블리. 게임 데이터 `data/`와 다른 폴더다).
- 실행 파일 옆에 `data/`, `addon/`, `godotdata/`, `addon.json`이 있어야 한다 (`GamePath.Root`가 익스포트 빌드에서는 실행 파일 폴더다).
- 익스포트 뒤 Godot 프로세스가 한동안 종료되지 않을 때가 있으므로 백그라운드로 돌린다.
- 개발용 인자("--" 뒤)는 빌드에서도 동작한다. 뽑은 뒤 `GodotXOPS.exe --headless -- --scene mainmenu --ui-quit 2`의 종료 코드로 뜨는지 본다.
- 아이콘은 `xops.png`(`config/icon`)이고 실행 파일에도 들어간다. 부트 스플래시는 로고 없이 검은 배경이다 (끄는 설정은 없다).

## 시뮬레이션과 캐릭터

- 게임플레이는 `SimClock`(Autoload)의 33.333Hz 틱에서만 진행한다. 틱 대상은 `ISimTickable`을 구현해 `SimClock.Register`로 등록하고, `SimOrder`가 한 틱 안의 순서다 (사람 10 → 떨어진 무기 30 → 총알 40 → 인간간 충돌 100 → AI 200 → 미션 판정·이벤트 300). `SimClock.TickEnabled`가 false면 멈춘다.
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
- 떨어진 무기 200, 탄환 160 은 원본 상수이고 가득 차면 새로 만들지 않고 버린다 (게임 결과에 영향을 주므로 가변으로 바꾸지 않는다). 이펙트 풀만 데이터로 정하고 모자라면 묶음 단위로 늘린다 (`effect_parameter_data.json`의 `poolInitialSize`, `poolGrowStep`, `poolMaxSize`). 맵을 내릴 때(`MapLoader.UnloadPointData`) 풀을 모두 비운다.
- 틱에서 일어난 일의 이펙트를 무기 모델 위치에 맞춰야 하면(총구 화염, 탄피) 틱에서는 표시만 해 두고 `Human._Process`에서 낸다. 무기 모델은 틱 사이를 보간해 움직이므로 틱에서 내면 어긋난다.
- 이펙트 프리셋·텍스처는 `effect_parameter_data.json`, 호출하는 쪽은 인덱스(무기 모델·탄환·사람 종류 데이터에 있다)와 위치만 넘긴다. 이펙트 머티리얼은 `MaterialManager.CreateEffectMaterial`, 투명도는 인스턴스 유니폼 `effect_alpha`다.
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
  - AI 는 무기 관리 때 스코프를 해제하지 않는다.
  - 전투 중 수류탄을 든 AI 가 확률로 다른 무기로 바꾸는 분기(ai.cpp:1059-1089)는 넣지 않는다. 수류탄을 다 던진 뒤에 바꾼다.
  - 경로·이벤트 포인트는 종류별로 찾는다 (아래).
- 원본대로 둔 것: 적의 이동을 앞질러 겨누기와 수류탄 높이 보정, 원거리 발견의 1/4 확정, 경계가 끝나면 시작한 자리로 돌아가기, 원거리 교전 중 근거리 적 재탐색, 경로의 수류탄 투척, 단발/연발 전환, 점프 판정, 좀비 공격은 겨눈 적 한 명만. 원본의 "끼었을 때 좌우로 돌기"는 조건이 늘 거짓이라 실행되지 않는 코드여서 옮기지 않았다.
- 시야와 사선은 블록만 가린다 (`MapLoader.RaycastBlock`). 사람과 소물은 가리지 않는다.
- 경로와 이벤트의 다음 포인트는 **종류별로** 찾는다 (`MapLoader.GetPathPoint`, `GetEventPoint`). 원본 `SearchPointdata`는 종류와 무관하게 같은 번호의 첫 포인트를 찾아서 번호가 겹치면 줄이 끊기는데, 원본의 버그로 보고 따르지 않는다 (사용자 결정).
- 소리 신호(`Human.NotifyThreatHeard`)는 `AIController`가 매 틱 비운다. 원본보다 한 틱 빨리 듣는다 (원본은 이중 버퍼라 다음 프레임에 듣는다).
- 발소리는 `HumanController`가 매 틱 `WorldSound.EmitFootstep(사람, 종류)`로 낸다. 지금은 달리는 소리를 다른 팀 AI 에게 알리기만 한다 (원본도 WAV 를 재생하지 않는다). 발소리 WAV 를 넣을 자리는 그 함수 하나다.
- `EventManager`(Autoload, SimOrder 300)가 이벤트 세 줄과 자동 판정을 돌린다. `BeginMission()`을 부른 뒤에만 돌고 맵을 내리면 멈춘다. UI(GDScript)는 시그널 `MessageShown(id, text)`, `MissionEnded(complete)`와 프로퍼티 `Result`, `EndTicks`, `MessageId`, `MessageText`, `MessageAlpha`, `StartCount`를 쓴다.

## 화면 (씬 UI)

- 화면은 `scenes/`의 씬 6개다: `boot` → `opening` → `mainmenu` → `briefing` → `maingame` → `result`. 각 씬은 루트 노드와 `ui/`의 GDScript 하나만 갖고, 화면 요소는 스크립트가 `_ready`에서 코드로 만든다. `maingame`만 `PlayerController`(C#) 노드를 자식으로 둔다.
- **수치는 화면별 GDScript 맨 위 상수 표에 모은다** (위치, 글자 크기, 색, 시간). 값의 출처는 UnityXOPS 0.4의 `unitydata/scene/*.lua`다. 다만 Lua 의 반투명 값은 Unity 가 선형 색 공간에서 섞은 것이라, 아주 옅은 값은 그대로 쓰면 보이지 않는다 (브리핑·결과 배경의 타이틀은 Lua 0.012 → 0.1). 나중에 외부 데이터로 뺄 때 그 표만 옮기면 된다.
- 공용 도우미는 `ui/common/`: `XopsUI`(요소 만들기·배치), `XopsText`(`char.dds` 스프라이트 글자), `XopsLayer`(층과 배율), `XopsLines`(스코프 조준선), `xops_dev.gd`(Autoload `Dev`, 개발용 인자).
- **배치 좌표는 UnityXOPS 화면 좌표 그대로다**: 기준점(화면이나 부모 안의 한 점)에서의 오프셋, +x 오른쪽, +y **위쪽**. `XopsUI`가 Godot 좌표로 바꾼다. 화면 스크립트에서 y 부호를 직접 뒤집지 않는다.
- **층의 배율은 두 가지다** (`XopsUI.layer(parent, order, scaled)`): `scaled = true`는 화면 높이를 480으로 보고 확대하고, `false`는 픽셀 1:1에 설정의 `UIScale`을 곱한다. 어느 요소가 어느 쪽인지는 Lua 를 따른다 (HUD·메뉴는 픽셀, 스코프·중앙 문구·암전은 확대).
- **GDScript 는 창구 Autoload 만 부른다**: `Game`(`GameBridge`), `EventManager`, `ConfigManager`, `InputManager`. 게임플레이 노드를 직접 만지지 않는다. 창구의 좌표·각도 인자는 UnityXOPS 공간이다 (카메라 위치, 무기 표시 자리).
- `Game`이 하는 일: 화면 전환(`ChangeScene`), 맵 로드(`LoadOpening`, `LoadDemo`, `LoadMission`, `BeginMission`, `RestartMission`, `ReloadMission`, `UnloadMap`, `UnloadMission`), 장면 카메라, 벽 블라인드 판정, 미션 목록·브리핑·통계 조회, HUD 가 읽는 플레이어 값, 3D 무기 표시(`HudWeaponView`), 밝기·감마 사각형.
- 오프닝과 메뉴 배경은 `AIController.DrivePlayer = true`로 플레이어까지 AI 가 움직이고 이벤트는 돌지 않는다. 메인게임이 들어올 때 `Game.BeginMission()`이 되돌린다.
- 시점 전환(F1)과 스코프 입력은 `PlayerController`가 처리한다. HUD 는 상태를 읽어 그리기만 한다.
- OS 글꼴 글상자(`XopsUI.label`: 브리핑 본문, 이벤트 메시지, 크레딧)는 자기가 속한 층의 글꼴(`XopsLayer.os_font()`)을 쓴다. 층이 배율만큼 `oversampling`을 맞춰 두므로 확대돼도 흐려지지 않는다. 공용 `XopsUI.os_font()`를 글상자에 직접 넣으면 높은 해상도에서 흐려진다.
- 글꼴은 OS 언어로 고른다 (한국어 맑은 고딕, 일본어 Yu Gothic, 그 밖 Segoe UI. 없는 글자는 시스템의 다른 글꼴로 넘어간다). 텍스트 파일은 BOM → 유효한 UTF-8 → OS 언어의 옛 코드 페이지(한국어 CP949, 일본어 Shift-JIS, 그 밖 1252) 순으로 읽는다 (`EncodingHelper`). UnityXOPS 와 같은 방식이다.
- 크기가 없는 노드에 직접 그리는 요소(`XopsText`)는 그릴 범위를 `RenderingServer.canvas_item_set_custom_rect`로 알려 줘야 한다. 그러지 않으면 기준점이 화면 밖일 때 화면 안에 걸친 부분까지 통째로 그려지지 않는다 (HUD 의 STATE 상자 아랫줄이 그렇게 사라졌었다).
- `PlayerController`는 입력을 넣은 직후 `Controller.ApplyVisual()`을 한 번 더 부른다. 사람 노드의 회전·팔 각도가 `PlayerController`보다 먼저 갱신되므로, 다시 맞추지 않으면 1인칭 팔이 카메라보다 한 프레임 늦게 돈다.
- 3D 무기 표시는 자기만의 3D 공간을 가진 뷰포트다. 무기 모델이 맵과 같은 안개 셰이더를 쓰므로 공간 전체를 1/100로 줄여 카메라 바로 앞에 둔다. 뷰포트는 표시 크기 × UIScale 로 렌더링한다 (표시 크기 그대로면 확대될 때 계단이 진다).
- 메뉴의 OPTION(설정 화면)은 `ui/mainmenu_option.gd`(`MenuOption`)가 만든다. 구성과 수치는 UnityXOPS 0.4의 `mainmenu.lua`다. 탭은 General / Input / Graphic / Sound. 값은 바꾸는 즉시 `ConfigManager`에 들어가 화면에 반영되고(밝기·감마, 음량, 키 바인딩), SAVE 가 파일에 쓰고 `ApplyGraphic`을 부른다. UIScale 은 SAVE 때 적용한다 (바로 적용하면 누르고 있던 화살표가 움직인다. 사용자 결정). BACK 과 ESC 는 `RevertToSaved`로 되돌린다. playerName 은 쓰는 곳이 없어 화면에 넣지 않았다 (사용자 결정).
- 디버그 콘솔(원본의 F11 콘솔)은 `config.json`의 `General` / `AllowConsole`이 true 일 때만 메인게임이 만든다 (사용자 결정. 실행 인자가 아니라 설정이다). 이 설정은 OPTION 화면에 넣지 않고 유저가 파일을 직접 고친다. OPTION 의 RESET 도 이 값은 건드리지 않는다 (`ConfigManager.ResetToDefaults`). 화면과 글자 입력은 `ui/common/xops_console.gd`(`XopsConsole`), 명령 표와 실행은 `src/Scene/DebugConsole.cs`(순수 클래스)이고 `Game.ConsoleExecute`로 잇는다. 명령을 추가할 때는 `DebugConsole` 생성자의 표에 한 줄을 더하고 `docs/development.md`의 명령 표도 고친다. 화면이 해야 하는 일(지우기, 닫기, 재시작, 화면 저장)은 명령이 `Game.ConsoleTakeAction`으로 넘긴다. 글자는 OS 글꼴이다 (사용자 결정. `char.dds`는 글자 크기 문제가 있다). **콘솔의 입력과 출력(사용법, 설명, 결과, 디버그 텍스트)은 영어만 쓴다** (사용자 결정). `ui_check`가 출력에 영어가 아닌 글자가 있는지 본다. 사람은 `MapLoader.Humans`의 인덱스로 가리킨다. 좌표를 받는 명령(`teleport x y z`)은 `info`가 보여 주는 것과 같은 Godot 공간의 미터 값을 받는다 (사용자 결정. 맵을 만드는 사람은 원본 XOPS 좌표를 잘 쓰지 않는다).
- 콘솔의 `flight`(비행 모드)는 `HumanController.SetFlight`로 켠다. 원본에 없는 기능이고, 켜져 있고 살아 있는 동안 `Tick`이 평소의 이동·충돌 대신 `TickFlight`만 돈다 (기존 이동 코드는 건드리지 않는다). 시선 방향으로 움직이고 점프는 무시하며, `HumanCollision`도 그 사람을 건너뛴다. 총알 판정은 그대로다. 날고 있는 동안은 접지한 것으로 친다 (`Grounded` = true, 공중 조준 오차 없음. 사용자 결정). 끄면 그 자리에서 공중 상태가 되고 평소의 이동이 접지 여부를 다시 구한다.
- 콘솔의 `collider`는 `src/Scene/ColliderView.cs`(`Game`의 자식 노드)가 그린다. 판정 코드와 같은 식으로 자리를 구해서 그리므로, 판정 계산(`HumanHitbox.Contains`, `WeaponManager.TickPickup`, `SmallObject.Contains`)을 고치면 이 표시도 같이 고친다.
- 글자를 입력받는 화면은 열려 있는 동안 `InputManager.InputBlocked`를 켠다. 켜져 있으면 `InputManager`의 모든 조회가 "안 눌림"을 돌려준다. 끌 때는 키 이벤트를 받는 도중이 아니라 `_process`에서 끈다 (도중에 끄면 콘솔을 닫은 Esc 가 게임 쪽에서 "메뉴로 나가기"로 읽힌다).
- 화면의 클릭은 `InputManager.WasClickPressed` / `WasClickReleased` / `IsClickPressed`(마우스 왼쪽 버튼 고정)로 읽는다. `"fire"` 액션으로 읽으면 발사 키를 바꿨을 때 메뉴를 누를 수 없게 된다.

## 맵과 렌더링

- `MapLoader`(Autoload)가 블록·스카이·미션 정보를 들고 있고, 씬이 바뀌어도 유지된다. 로드 함수는 이전 것을 먼저 언로드한다.
- 블록 충돌은 `MapLoader.RaycastBlock` / `IsInsideBlock`으로 직접 계산한다 (엔진 물리 미사용). 블록 앞면만 맞는다.
- 추가 충돌(Additional Collision, `MapLoader.AdjustCollision`): 캐릭터-맵 충돌에서 중심축 0.9 m / 1.3 m 높이의 추가 검사 2점은 **이 플래그가 켜진 미션에서만** 돈다 (원본 `human::CollisionMap`의 `AddCollisionFlag`). UnityXOPS `HumanController`는 플래그를 무시하고 항상 검사하는데, 이는 잘못 옮긴 것이므로 따라 하지 않는다.
- 머티리얼은 `MaterialManager`의 `Create*Material`로 만든다. `alpha_clip_blend` 셰이더는 원본처럼 sRGB 값 그대로 곱하고 섞은 뒤 마지막에만 선형으로 바꾼다. 텍스처 유니폼에 `source_color`를 붙이지 않는다.
- 안개는 Godot 환경 안개가 아니라 전역 셰이더 변수(`xops_fog_color`, `xops_fog_range`)로 셰이더가 직접 계산한다 (원본의 선형 안개 재현).
- 어두운 화면(`MapLoader.DarkScreen`) 미션은 블록 면 명도를 낮추고(가산값 0.5 → 0.3), 스카이와 모델(사람·무기·소물·탄환)에 전역 셰이더 변수 `xops_model_brightness` = 0.8 을 곱한다 (원본 `RenderModel`의 darkflag). 0.8 은 코드 상수다 (사용자 결정). 블록은 머티리얼 유니폼 `dark_apply` = 0 으로, HUD 의 무기 표시는 인스턴스 유니폼 `dark_exempt` = 1 로 빠진다.
- 깊이 테스트를 끈 머티리얼(스카이)은 반투명 패스로 들어가므로 `RenderPriority`로 그리는 순서를 정한다.
- 원본 좌표에서 외적으로 법선을 구하는 코드를 옮길 때는 피연산자 순서를 뒤집는다 (원본은 왼손, Godot은 오른손 좌표계).
- 런타임에 만든 노드를 같은 프레임 안에 교체할 때는 `QueueFree` 대신 `RemoveChild` + `Free`를 쓴다. 트리에서 뗀 노드를 `QueueFree`만 해 두고 종료하면 종료 시 치명 오류가 난다.

## 문서

유저가 읽는 문서는 `README.md`(한국어, 원문), `README.en.md`, `README.ja.md`, `ROADMAP.md`, `docs/modding.md`, `docs/development.md`다. **코드나 데이터를 고치면 같은 작업 안에서 해당 문서도 고친다.** 문서가 실제 동작과 어긋난 채로 커밋하지 않는다.

- `docs/modding.md`를 고쳐야 하는 변경: `godotdata/` JSON 의 키 추가·삭제·이름 변경, 값의 뜻이나 단위 변경, 열거형 값, 파일 추가, 지원하는 파일 형식, 에드온 페이지 방식, 풀 크기 같은 제한.
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

- `godotdata/config.json`은 `ConfigManager`가 읽고 쓴다. 파일이 없으면 코드 기본값(`ConfigManagerDefault.cs`)으로 새로 만든다. 새 설정은 기본값 목록에 추가하면 기존 파일에도 자동으로 병합된다.
- `ConfigManager.ApplyGraphic`이 부팅 때 창 모드와 렌더 해상도를 적용한다(기본: 전체화면, 640×480 렌더, 화면비가 다르면 검은 띠). 도구 씬은 `_Ready`에서 창 설정을 되돌린다(`AssetViewer` 참조).
- 입력은 `InputManager`를 거쳐 읽는다: `IsPressed` / `WasPressed` / `WasReleased`(버튼), `ReadVector`(move, look), `IsKeyPressed` / `WasKeyPressed`(치트 키 등 바인딩 밖의 키), `IsClickPressed` / `WasClickPressed` / `WasClickReleased`(화면 클릭). Godot `Input`을 직접 부르지 않는다.
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
