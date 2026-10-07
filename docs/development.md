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

아래 명령의 `godot`는 Godot 콘솔 실행 파일(`Godot_v4.7.2-stable_mono_win64_console.exe`)의 경로로 바꿔 씁니다. PowerShell 에서는 경로를 따옴표로 감싸므로 앞에 호출 연산자 `&`를 붙이고(`& "C:/.../Godot_v4.7.2-stable_mono_win64_console.exe" ...`), 게임에 넘기는 인자 구분자는 `'--'`로 감쌉니다.

### 빌드와 실행

```powershell
dotnet build GodotXOPS.csproj
```

```powershell
godot --path .
```

에디터로 열려면 `project.godot`를 Godot 에서 엽니다.

### Windows 용 익스포트

Godot 에 익스포트 템플릿이 설치되어 있어야 합니다. 프리셋은 `export_presets.cfg`의 "Windows Desktop" 입니다.

```powershell
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
| `src/IO/` | 파일 로더 (이미지, 모델 `.x`, 소리)와 확장 형식의 읽기·쓰기 (`BD2File`, `PD2File`). 게임 싱글톤과 무관해서 다른 도구에서도 쓸 수 있습니다 |
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

- 블록 충돌: `MapLoader.RaycastBlock` / `IsInsideBlock`. 첫 인자로 판정 종류(`BlockLayer`)를 받습니다: `Human`(이동, 발밑, 카메라, 떨어진 무기와 오브젝트의 바닥), `Bullet`(총알, 수류탄, 혈흔 입자), `Sight`(AI 시야·사선, 폭발 가림). BD2 의 블록 플래그로 판정별 충돌 여부가 갈리고, BD1 블록은 세 판정이 항상 같습니다. 기본값이 없으므로 새 호출 지점은 어느 판정인지 정해서 넘깁니다.
- 블록 면의 재질은 `MapLoader.GetFaceMaterial(블록, 면)`으로 얻습니다. BD1 블록은 면 재질 번호가 없고 모든 면이 0번 재질입니다 (원본의 착탄 연기와 착탄음이 0번에 들어 있습니다). 블록에 맞은 탄의 이펙트와 소리는 탄환이 아니라 재질이 정합니다.
- 포인트 데이터도 PD1 과 PD2 를 확장자로 가려 읽고 같은 구조(`RawPointData`)가 됩니다. 이벤트 줄 수는 포인트 데이터가 정합니다 (`MapLoader.EventEntryIds`. PD1 은 세 줄).
- 블록 데이터는 BD1 과 BD2 를 확장자로 가려 읽고, 읽은 뒤에는 같은 구조(`RawBlockData` → `Block`)가 됩니다. BD2 의 구조는 [모딩 문서](modding.md)에 있습니다.
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

```powershell
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
| `bd2_check.tscn` | — | 모든 미션의 BD1 을 BD2 로 바꿔 쓰고 읽어 블록·메시·판정 결과가 같은지, 블록 플래그(판정별 통과), 그리지 않는 면, 재질(착탄 이펙트와 소리, 발소리와 박자), 깨진 파일 |
| `pd2_check.tscn` | — | 모든 미션의 PD1 을 PD2 로 바꿔 쓰고 읽어 포인트와 스폰된 사람·무기·오브젝트가 같은지, 255 를 넘는 번호, 추가 파라미터, 방향, 이벤트 줄 수, 깨진 파일 |
| `effect_viewer.tscn` | `-- --selftest` | 이펙트 재생 수, 풀 증가, 블렌드 모드별 머티리얼, 발광 감쇠, 면 위 재생(데칼의 방향과 띄우는 거리) |

눈으로 확인하는 도구 (`--headless` 없이 실행):

- `asset_viewer.tscn` — 에셋 뷰어.
- `map_viewer.tscn` — 미션을 골라 자유 카메라로 봅니다. `-- --mission 번호 [--addon] --screenshot 경로.png [--cam x,y,z,yaw,pitch]`로 화면을 저장하고 종료합니다. `-- --file 경로`는 미션 대신 블록 데이터 파일(BD1, BD2) 하나를 띄웁니다 (게임 폴더 기준 경로).
- `pd2_check.tscn` — `-- --convert 입력.pd1 출력.pd2`로 PD1 하나를 PD2 로 바꿉니다. 같은 이름의 `.msg`도 복사합니다 (헤드리스 가능, 게임 폴더 기준 경로).
- `bd2_check.tscn` — `-- --convert 입력.bd1 출력.bd2`로 BD1 하나를 BD2 와 텍스처 목록(`출력_textures.json`)으로 바꿉니다 (헤드리스 가능, 게임 폴더 기준 경로).
- `effect_viewer.tscn` — 이펙트 프리셋을 골라 봅니다. 인자: `-- [--effect 번호] [--additive] [--screenshot 경로.png]`. 창에서는 ← →(프리셋), Space(다시 재생), B(가산 미리보기), ↑ ↓(카메라 거리).
- `play_test.tscn` — 미션을 로드해 플레이어를 직접 조작합니다. 인자: `--mission 번호 [--addon] [--third] [--weapon 번호] [--fire] [--hitbox] [--look yaw,pitch] [--pos x,y,z] [--noai] [--invincible] --screenshot 경로.png`. 창에서는 F2(AI 정지/재개), F4(전원 비전투), End(전원 경계), Insert(플레이어 무적), Home(디버그 텍스트).

## 개발용 실행 인자

게임을 씬 지정 없이 실행할 때 `--` 뒤에 줍니다. 익스포트 빌드에서도 동작합니다.

| 인자 | 뜻 |
|---|---|
| `--window 너비x높이` | 설정의 전체화면 대신 그 크기의 창으로 띄웁니다 |
| `--scene 이름 [--mission 번호 [--addon] [--page 번호]]` | 그 화면에서 시작합니다 (`mainmenu`, `briefing`, `maingame`, `result`) |
| `--ui-state 값` | 메뉴는 `credit` / `exit` / `addon` / `option` / `option-input` / `option-graphic` / `option-sound`, 메인게임은 `simple` / `off` / `console`(설정과 무관하게 디버그 콘솔 허용) 상태로 시작합니다 |
| `--ui-click "목록"` | 가짜 입력을 차례로 넣습니다: `x,y`(클릭), `x,y,초`(누르고 있기), `key:이름`(키 한 번), `text:글자`(글자를 차례로 침) |
| `--ui-shot 경로.png [--ui-time 초]` | 화면을 PNG 로 저장하고 종료합니다 |
| `--ui-quit 초` | 그 시간 뒤 종료합니다 |

```powershell
godot --path . -- --window 640x480 --scene maingame --mission 1 --ui-shot shot.png
```

## 디버그 콘솔

원본 OpenXOPS 의 F11 콘솔에 해당합니다. `godotdata/config.json`의 `General` 섹션에 있는 `AllowConsole`의 `value`를 `"true"`로 바꾸면 메인게임에서 F11 로 열고 닫습니다. 기본값은 `"false"`이고, 그때 F11 은 아무 일도 하지 않습니다.

```json
{
  "name": "AllowConsole",
  "type": "bool",
  "value": "true",
  "min": 0,
  "max": 0
}
```

이 설정은 메뉴의 OPTION 에 나오지 않습니다. 파일을 직접 고쳐야 하고, OPTION 의 RESET 도 이 값은 바꾸지 않습니다.

- 입력과 출력은 영어만 씁니다.
- 명령을 치고 Enter 로 실행합니다. 대소문자는 가리지 않습니다. 위·아래 화살표로 전에 친 명령을 불러오고, Esc 나 F11 로 닫습니다.
- 콘솔이 열려 있는 동안 게임 조작은 막히고 게임은 계속 진행됩니다.
- 사람 번호는 사람 목록의 순서입니다 (0 부터). `info`의 디버그 텍스트에 플레이어의 번호가 `#번호`로 나옵니다.
- 켜고 끄는 명령은 한 번 더 치면 되돌아갑니다. 미션을 다시 시작하면 AI 정지, 비전투, 이벤트 멈춤, 안개, 하늘이 처음 상태로 돌아갑니다.

| 명령 | 하는 일 |
|---|---|
| `help [명령]` | 명령 목록. 명령 이름을 주면 그 명령의 사용법과 설명 (영어) |
| `ver` | 게임 버전 |
| `clear` | 콘솔의 글자를 지웁니다 |
| `exit` | 콘솔을 닫습니다 |
| `info` | 디버그 텍스트(플레이어 위치·체력·무기, AI 상태별 인원, 미션 결과)를 켜고 끕니다. 화면 왼쪽 위에 나오고, 콘솔이 열려 있으면 콘솔 상자 뒤로 비쳐 보입니다 |
| `human` | 사람 수와 팀별 생존자 수 |
| `result` | 지금까지의 통계 (발사, 명중, 헤드샷, 킬, 시간) |
| `event` | 이벤트 세 줄이 기다리는 포인트와 미션 결과 |
| `nodamage [번호]` | 무적을 켜고 끕니다. 번호가 없으면 플레이어 |
| `treat [번호]` | 체력을 처음 값으로 되돌립니다. 번호가 없으면 플레이어 |
| `teleport 번호` | 플레이어를 그 사람의 자리로 옮깁니다 |
| `teleport x y z` | 플레이어를 그 좌표로 옮깁니다. `info`가 보여 주는 위치와 같은 좌표(미터, 발 기준)입니다. 블록 안이나 허공이어도 그대로 옮깁니다 |
| `player 번호` | 조작 대상을 그 사람으로 바꿉니다 |
| `weapon 번호 [탄 수]` | 플레이어가 든 무기를 그 번호의 무기로 바꿉니다. 탄 수는 전체 탄 수이고, 장탄수만큼 장전한 나머지가 예비 탄이 됩니다. 탄 수가 없으면 사람 종류의 초기 탄약 배수(`autoBulletMultiplier`)를 씁니다 |
| `kill 번호` | 그 사람을 죽입니다 (무적이면 죽지 않습니다) |
| `flight` | 플레이어의 비행 모드를 켜고 끕니다. 전진·후진은 시선 방향(위아래 포함)으로, 좌우는 수평 옆으로 움직입니다. 중력, 블록 충돌, 사람끼리 밀어내기, 낙하 데미지가 없고 점프는 무시됩니다. 날고 있는 동안은 공중에 뜬 상태로 치지 않아서 공중 조준 오차가 붙지 않고, 공중에서 끄면 떨어지는 동안 공중 상태가 됩니다. 총알에는 그대로 맞습니다. 걷기 키를 누르면 느려집니다. 조작 대상을 바꾸거나 미션을 다시 시작하면 꺼집니다 |
| `bot` | 플레이어를 AI 가 움직이게 합니다 |
| `nofight` | 모든 AI 를 비전투로 만듭니다 |
| `caution` | 모든 AI 를 경계시킵니다 |
| `stop` | 모든 AI 를 멈춥니다 |
| `comp` / `fail` | 미션을 클리어 / 실패로 끝냅니다 |
| `estop` | 이벤트 진행을 멈춥니다 (자동 판정은 계속 돕니다) |
| `f12` | 미션을 처음부터 다시 시작합니다 |
| `collider human` / `weapon` / `object` | 판정 범위를 선으로 그립니다. `human`은 사람의 총알 판정 원기둥(초록), `weapon`은 떨어진 무기의 줍기 범위(빨강. 사람의 발이 이 원기둥 안에 들어오면 줍습니다), `object`는 오브젝트의 판정 형상(파랑). 종류마다 따로 켜고 끄고, 벽에 가려지지 않습니다. 1인칭에서는 자기 원기둥을 그리지 않습니다 |
| `fog` | 안개를 켜고 끕니다 |
| `sky 번호` | 하늘을 그 번호로 바꿉니다 (0 은 없음). 번호의 범위는 `sky_data.json`의 `skyTexturePath` 개수입니다 |
| `ss` | 콘솔을 뺀 화면을 게임 폴더의 `screenshot/`에 PNG 로 저장합니다 |

명령은 `src/Scene/DebugConsole.cs`의 표에 한 줄씩 등록합니다. 화면과 글자 입력은 `ui/common/xops_console.gd`가 맡습니다.

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
