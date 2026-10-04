# 개발 문서

GodotXOPS 를 소스에서 빌드하거나 코드를 고치려는 사람을 위한 문서입니다.

- [소스에서 빌드](#소스에서-빌드)
- [폴더 구조](#폴더-구조)
- [구조 한눈에 보기](#구조-한눈에-보기)
- [점검 도구](#점검-도구)
- [개발용 실행 인자](#개발용-실행-인자)
- [원본과 다르게 한 동작](#원본과-다르게-한-동작)
- [코드 규칙](#코드-규칙)

## 소스에서 빌드

### 준비물

- [Godot 4.7.2 .NET 판](https://godotengine.org/download/archive/) (파일 이름에 `mono`가 들어간 것)
- .NET SDK
- 원본 XOPS 의 `data` 폴더 (에드온을 쓰려면 `addon` 폴더도)

`data`와 `addon`은 저작권 때문에 저장소에 없습니다. 클론한 뒤 프로젝트 루트에 직접 복사합니다. 두 폴더는 `.gitignore`에 들어 있어 커밋되지 않습니다.

아래 명령의 `godot`는 Godot 콘솔 실행 파일(`Godot_v4.7.2-stable_mono_win64_console.exe`)의 경로로 바꿔 씁니다.

### 빌드와 실행

```bash
dotnet build GodotXOPS.csproj
```

```bash
godot --path .
```

에디터로 열려면 `project.godot`를 Godot 에서 엽니다.

### Windows 용 익스포트

Godot 에 익스포트 템플릿이 설치되어 있어야 합니다. 프리셋은 `export_presets.cfg`의 "Windows Desktop" 입니다.

```bash
godot --headless --path . --export-release "Windows Desktop" build/windows/GodotXOPS.exe
```

- 결과물은 `GodotXOPS.exe`, `GodotXOPS.pck`, `data_GodotXOPS_windows_x86_64/`(.NET 런타임과 어셈블리. 게임 데이터 `data/`와 다른 폴더) 입니다.
- 실행하려면 실행 파일 옆에 `data/`, `addon/`, `godotdata/`, `addon.json`을 둡니다.
- 익스포트가 끝난 뒤에도 Godot 프로세스가 한동안 종료되지 않을 때가 있습니다. 결과물은 이미 만들어져 있습니다.
- 뜨는지 확인: `GodotXOPS.exe --headless -- --scene mainmenu --ui-quit 2`의 종료 코드가 0 이면 됩니다.

## 폴더 구조

| 폴더 | 내용 |
|---|---|
| `src/` | C# 코드. 게임플레이, 데이터, 로더, 매니저 |
| `src/Utility/` | 공용 도구 (`Coord` 좌표 변환, `Singleton<T>`, `GamePath` 등) |
| `src/IO/` | 파일 로더 (이미지, 모델 `.x`, 소리, BD1, PD1, MIF) |
| `src/Data/` | 데이터 클래스와 `DataManager`, 설정, 입력 |
| `src/Map/` | 맵, 사람, 무기, 총알, 오브젝트, 이펙트, 소리, AI, 이벤트 |
| `src/Scene/` | 화면이 쓰는 창구 `GameBridge`(Autoload `Game`) |
| `src/Dev/` | 점검 도구 |
| `ui/` | GDScript. 화면 5종의 배치·연출·입력, `ui/common/`의 공용 도우미 |
| `scenes/` | `.tscn`. 화면 씬과 `scenes/dev/`의 점검 씬 |
| `shaders/` | `.gdshader` |
| `godotdata/` | 외부 게임 데이터 JSON ([모딩 문서](modding.md)) |
| `data/`, `addon/` | 원본 XOPS 에셋. 커밋하지 않습니다 |

`data/`, `addon/`, `godotdata/`는 Godot 이 임포트하지 않습니다 (`.gdignore`). 런타임에 `GamePath.Resolve()`로 전체 경로를 얻어 파일로 직접 읽습니다.

## 구조 한눈에 보기

### 언어 구분

- 게임플레이, 데이터, 로더, 매니저는 C#.
- 화면(오프닝, 메뉴, 브리핑, HUD, 결과)의 배치·연출·입력은 GDScript.
- GDScript 는 창구 Autoload(`Game`, `EventManager`, `ConfigManager`, `InputManager`)만 부르고, 게임플레이 노드를 직접 만지지 않습니다.

### 엔진 물리를 쓰지 않습니다

원본 조작감을 재현하려고 이동, 충돌, 총알 판정을 직접 계산합니다. `CharacterBody3D`, `RigidBody3D`, 물리 레이캐스트를 쓰지 않습니다.

- 블록 충돌: `MapLoader.RaycastBlock` / `IsInsideBlock`
- 총알: 한 틱의 경로를 0.25 m 간격 점으로 나눠 점마다 사람 → 오브젝트 → 맵 순으로 검사 (원본 `ObjectManager::CollideBullet`)

### 시뮬레이션 틱

게임 진행은 `SimClock`의 33.333Hz 틱에서만 일어납니다. 틱 대상은 `ISimTickable`을 구현해 등록하고, `SimOrder`가 한 틱 안의 순서입니다.

| 순서 | 대상 | 하는 일 |
|---|---|---|
| 10 | 사람 (`HumanController`) | 무기 입력 소비 → 카운터 감소·조준 오차 → 이동·충돌 → 사망 상태 → 다리·팔 동작 |
| 30 | `WeaponManager` | 떨어진 무기 낙하, 줍기 |
| 40 | `BulletManager` | 직선 탄, 수류탄 |
| 100 | `HumanCollision` | 사람끼리 밀어내기 |
| 200 | `AIController` | AI 판단 (정한 입력은 다음 틱에 소비) |
| 300 | `MissionStats`, `EventManager` | 플레이 시간, 미션 판정, 이벤트 |

- 시간 카운터는 정수 틱으로 셉니다. 데이터의 초 단위 값은 `RoundToInt(초 × SimClock.FrameRate)`로 바꿉니다.
- 게임 결과에 영향을 주는 난수는 `GameRandom.Gameplay`(틱에서만), 연출용은 `GameRandom.Visual`.
- 이펙트, 소리 볼륨, 모델 위치 보간 같은 연출은 렌더 프레임에서 합니다.

### 한 프레임의 순서 (`ProcessPriority`)

`InputManager` → `SimClock`(틱 0~4회) → 일반 노드(탄환·떨어진 무기 보간, 이펙트, 소리) → `Human`(시각 보간) → `PlayerController`(입력, 카메라)

### 사람

`Human`(Node3D)은 데이터와 시각만 갖습니다. 논리 위치·이동·충돌·사망 상태는 노드가 아닌 `HumanController`가 갖고, 노드의 transform 은 틱 사이를 보간한 시각 전용 값입니다. 판정에는 `Controller.Position`을 씁니다.

사람에게 입력을 넣는 창구는 `Controller.SetInput`(이동·조준)과 `Human.QueueWeaponInput`(무기) 둘뿐이고, 플레이어와 AI 가 같은 창구를 씁니다.

### 좌표

변환은 반드시 `src/Utility/Coord.cs`를 거칩니다.

- OpenXOPS → Godot: `(-x, y, z) × 0.1`
- UnityXOPS → Godot: `(x, y, -z)`. `godotdata` JSON 의 위치와 `.x` 정점이 여기에 해당합니다.
- 캐릭터 각도는 도 단위, yaw 는 오른쪽이 +, pitch 는 아래가 +.

### Autoload

`ConfigManager` → `DataManager` → `InputManager` → `MaterialManager` → `SimClock` → `MapLoader` → `BulletManager` → `WeaponManager` → `EffectManager` → `SoundManager` → `EventManager` → `Game` → `Dev`

등록 순서가 초기화 순서입니다. 매니저는 `Singleton<T>`를 상속합니다.

## 점검 도구

Godot 콘솔 실행 파일로 헤드리스 실행합니다. 종료 코드 0 이 통과입니다. 코드를 고친 뒤에는 전부 다시 돌립니다.

```bash
godot --headless --path . res://scenes/dev/loader_check.tscn
```

| 씬 | 인자 | 확인하는 것 |
|---|---|---|
| `loader_check.tscn` | — | `data/`, `addon/`의 이미지·소리·모델 전체 로드 |
| `data_check.tscn` | — | `godotdata/` JSON 과 로드된 값의 키 단위 대조 |
| `config_input_check.tscn` | — | 설정 읽기·쓰기·되돌리기, 입력 조회, 키 재지정 |
| `map_viewer.tscn` | `-- --selftest` | 모든 미션의 블록 로드와 충돌 레이 |
| `play_test.tscn` | `-- --selftest` | 모든 미션에서 틱을 돌려 사람이 맵 아래로 빠지지 않는지 |
| `weapon_check.tscn` | — | 무기, 총알, 히트박스, 떨어진 무기, 오브젝트, 통계 |
| `ai_check.tscn` | — | AI(시야, 청각, 경계, 조준, 경로)와 미션 이벤트·판정 |
| `ui_check.tscn` | — | 화면 창구 `Game`의 값과 화면 전환 흐름 |

눈으로 확인하는 도구 (`--headless` 없이 실행):

- `asset_viewer.tscn` — 에셋 뷰어.
- `map_viewer.tscn` — 미션을 골라 자유 카메라로 봅니다. `-- --mission 번호 [--addon] --screenshot 경로.png [--cam x,y,z,yaw,pitch]`로 화면을 저장하고 종료합니다.
- `play_test.tscn` — 미션을 로드해 플레이어를 직접 조작합니다. 인자: `--mission 번호 [--addon] [--third] [--weapon 번호] [--fire] [--hitbox] [--look yaw,pitch] [--pos x,y,z] [--noai] [--invincible] --screenshot 경로.png`. 창에서는 F2(AI 정지/재개), F4(전원 비전투), End(전원 경계), Insert(플레이어 무적), Home(디버그 텍스트).

## 개발용 실행 인자

게임을 씬 지정 없이 실행할 때 `--` 뒤에 줍니다. 익스포트 빌드에서도 동작합니다.

| 인자 | 뜻 |
|---|---|
| `--window 너비x높이` | 설정의 전체화면 대신 그 크기의 창으로 띄웁니다 |
| `--scene 이름 [--mission 번호 [--addon] [--page 번호]]` | 그 화면에서 시작합니다 (`mainmenu`, `briefing`, `maingame`, `result`) |
| `--ui-state 값` | 메뉴는 `credit` / `exit` / `addon` / `option` / `option-input` / `option-graphic` / `option-sound`, 메인게임은 `simple` / `off` 상태로 시작합니다 |
| `--ui-click "목록"` | 가짜 입력을 차례로 넣습니다: `x,y`(클릭), `x,y,초`(누르고 있기), `key:이름`(키 한 번) |
| `--ui-shot 경로.png [--ui-time 초]` | 화면을 PNG 로 저장하고 종료합니다 |
| `--ui-quit 초` | 그 시간 뒤 종료합니다 |

```bash
godot --path . -- --window 640x480 --scene maingame --mission 1 --ui-shot shot.png
```

## 원본과 다르게 한 동작

계산은 OpenXOPS 의 C++ 코드를 기준으로 옮겼습니다. 아래는 일부러 다르게 한 것입니다.

### 무기와 총알

| 동작 | 원본 | GodotXOPS |
|---|---|---|
| 피격 시 조준 흐트러짐 | 새 값으로 대입 | 더 큰 쪽 유지 |
| 가득 찬 탄창의 재장전 | 가능 | 불가 |
| 폭풍 방향 | 폭발이 위에 있으면 끌어당김 | 항상 멀어지는 쪽 |
| 반동 오차 | 조준선이 보일 때만 누적 | 항상 누적 |
| 무기 줍기 검사 | 2프레임에 한 번 | 매 틱 |

### AI 와 이벤트

| 동작 | 원본 | GodotXOPS |
|---|---|---|
| 회전·경로 이동 의사 | 플래그를 유지하다 확률로 해제 | 매 틱 새로 정함 (두리번거림과 전투 중 회피만 유지) |
| 아군 시체를 보고 경계 | 있음 | 없음 |
| 좀비의 이동 앞질러 겨누기 | 사람 종류별 값 | 하지 않음 |
| 수류탄을 든 AI 가 전투 중 무기 바꾸기 | 확률로 바꿈 | 다 던진 뒤에 바꿈 |
| 경로·이벤트의 다음 포인트 찾기 | 종류와 무관하게 같은 번호의 첫 포인트 | 종류별로 찾음 (번호가 겹쳐도 끊기지 않음) |
| 소리를 듣는 시점 | 다음 프레임 | 같은 틱 |

### 화면

| 동작 | 원본 | GodotXOPS |
|---|---|---|
| 조준선 | 고정 모양 | 막대 4개, 길이·굵기·간격·색 설정 가능 |
| 벽 블라인드 판정 | 시야 안쪽의 점 | 가까운 절단면 사각형의 네 변 가운데 |
| 메뉴 | — | OPTION, CREDIT, 에드온 페이지 추가 |

원본에 없던 것: 설정 화면, 키 재지정, 해상도 선택, FPS 표시, 에드온 페이지, JSON 데이터.

## 코드 규칙

- 네임스페이스 `GodotXOPS` (로더는 `GodotXOPS.IO`).
- private 필드 `m_`, private static 필드 `s_`, private const `k_` 접두사. public 멤버는 PascalCase.
- 데이터 클래스(`src/Data/`)의 public 필드는 JSON 키와 같은 camelCase 입니다. 이름을 바꾸면 파일과 어긋납니다.
- 주석은 한국어로 클래스와 함수에 씁니다 (`/// <summary>`, `<param>`, `<returns>`). 원본에서 온 값이나 로직에는 출처를 적습니다 (예: `원본 object.cpp:1607-1644`).
- 숫자 상수는 이름 붙은 `k_` 상수로 빼고 단위와 원본 값을 주석에 적습니다.
- 중괄호는 항상 새 줄에 엽니다.
- 노드일 필요가 없는 것은 순수 C# 클래스로 둡니다.
- JSON 에 있는 값은 그대로 읽습니다. 원본 상수로 하드코딩하지 않습니다.
- 무기 동작은 데이터 필드로만 가릅니다. 코드에 무기 번호를 쓰지 않습니다.
- 빌드 경고 0개를 유지합니다. Godot 이 만드는 `.cs.uid` 파일은 커밋합니다.
