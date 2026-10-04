# TODO — GodotXOPS 진행 상황과 다음 작업

> 세션을 시작하면 이 파일을 먼저 읽는다. 작업을 끝내면 갱신한다. 설계 규칙과 컨벤션은 `CLAUDE.md`에 있다.

## 현재 상태 (2026-10-04)

브랜치 `main`. 9단계까지 커밋·푸시했다. 버전은 1.0.0, 제작자 표기는 jaytwogames 다 (`godotdata/global.json`). 저장소: https://github.com/jejusbluesea/GodotXOPS

맵을 로드해 플레이어로 걸어 다니며 무기를 쏘고, 버리고, 줍고, 사람과 소물을 맞힐 수 있다. 이펙트와 효과음이 나온다. AI 가 경로를 돌고, 보고 듣고, 싸운다. 미션 이벤트와 클리어·실패 판정이 돈다. 오프닝 → 메뉴 → 브리핑 → 메인게임(HUD) → 결과 화면이 이어진다. 완전 포팅까지 남은 것은 10단계(원본 대조 마무리)다.

| 단계 | 상태 | 주요 파일 |
|---|---|---|
| 1. 뼈대, 파일 로더 | 완료 | `src/Utility/`, `src/IO/` |
| 2. 데이터 계층 | 완료 | `src/Data/`, `godotdata/` |
| 3. 설정과 입력 | 완료 | `src/Data/Config/`, `src/Data/Input/` |
| 4. 맵 표시 (블록, 스카이, 안개, 충돌 계산) | 완료 | `src/Map/Block/`, `src/Map/Sky/`, `src/Map/Mission/`, `shaders/` |
| 5. SimClock, 캐릭터 (이동·충돌, 모델, 플레이어 조작) | 완료 | `src/Map/SimClock.cs`, `src/Map/Human/`, `src/Map/Point/` |
| 6. 무기, 총알, 히트박스 | 완료 | `src/Map/Weapon/`, `src/Map/Bullet/`, `src/Map/Human/HumanWeapon.cs`, `HumanAim.cs`, `HumanHitbox.cs` |
| 7. 이펙트, 사운드, 무기 드롭/줍기, 소물 | 완료 (소리와 화면 연출은 사용자 확인 필요) | `src/Map/Effect/`, `src/Map/Sound/`, `src/Map/Object/`, `src/Map/Weapon/WeaponManager.cs`, `src/Map/MissionStats.cs`, `shaders/effect_blend.gdshader` |
| 8. AI, 이벤트, 미션 판정 | 완료 (사용자가 창에서 동작 확인) | `src/Map/Human/AI/`, `src/Map/Event/`, `src/Map/Human/WorldSound.cs`, `src/Dev/AICheck.cs` |
| 9. 씬 UI (오프닝, 메뉴, 브리핑, HUD, 결과) | 완료 (사용자가 창에서 확인) | `ui/`, `scenes/`, `src/Scene/`, `shaders/screen_color_adjust.gdshader`, `src/Dev/UICheck.cs` |
| 10. 원본 대조 마무리 | **다음** | — |

## 점검 씬 (작업을 마칠 때마다 전부 통과해야 한다)

Godot 콘솔 실행 파일로 `--headless --path . <씬> [-- 인자]` 형식으로 실행한다. 종료 코드 0이 통과다.

| 씬 | 인자 | 확인하는 것 |
|---|---|---|
| `res://scenes/dev/loader_check.tscn` | — | `data/`, `addon/`의 이미지·사운드·모델 전체 로드 |
| `res://scenes/dev/data_check.tscn` | — | `godotdata/` JSON과 로드된 값의 키 단위 대조 |
| `res://scenes/dev/config_input_check.tscn` | — | 설정 읽기/쓰기/되돌리기, 입력 조회 |
| `res://scenes/dev/map_viewer.tscn` | `--selftest` | 59개 미션 블록 로드와 충돌 레이 |
| `res://scenes/dev/play_test.tscn` | `--selftest` | 59개 미션에서 AI 와 이벤트를 켠 채 300틱을 돌려 사람 1,057명이 맵 아래로 빠지거나 좌표가 깨지지 않는지, 맵 배치 무기 449개와 소물 210개 스폰 |
| `res://scenes/dev/weapon_check.tscn` | — | 110항목: 부위 명중, 스침, 관통, 연사 간격, 재장전, 조준 오차, 전체 무기 발사, 폭발, 수류탄 비행, 벽, 사망, 버리기·낙하·줍기, 소물 피격·파괴, 통계, 이펙트·소리 호출, 소리 거리 감쇠 |

| `res://scenes/dev/ai_check.tscn` | — | 78항목: 시야(정면·등 뒤·같은 팀·비전투·벽), 청각(총성·발소리·피격 방향), 경계 시간과 팔 각도, 조준 예측, 재장전·버리기·무기 들기, 좀비 근접 공격, 경로(걷기·대기·랜덤 분기·번호 겹침·우선적 달리기·5초 정지), 이벤트 세 줄, 자동 판정, 복제 |
| `res://scenes/dev/ui_check.tscn` | — | 46항목: 미션 목록·버전·크레딧, 오프닝·메뉴 배경 로드, 장면 카메라, 미션 흐름(로드 → 시작 → 재시작 → 맵 내리기 → 전부 내리기), HUD 가 읽는 플레이어 값, 스코프 정보, 3D 무기 표시 |

화면 스크립트는 헤드리스로 한 번씩 띄워 오류가 없는지 본다 (씬을 지정하지 않고 `--headless --path . -- --scene 이름 [--mission 번호] --ui-quit 초`). 인자는 `CLAUDE.md`의 "빌드와 실행" 참조.

눈으로 확인하는 도구 (`--headless` 없이): `asset_viewer.tscn`, `map_viewer.tscn`, `play_test.tscn`. 뒤의 둘은 `--screenshot 경로.png`로 화면을 저장한다.
`play_test.tscn`은 무기 확인용 인자를 받는다: `--weapon 번호`, `--fire`, `--hitbox`, `--look yaw,pitch`, `--pos x,y,z`, `--drop`. 훈련장(미션 0)의 플레이어는 맨손이므로 `--weapon 1`(MP5)처럼 쥐여 주거나 F7+←/→로 바꾼다.
`play_test.tscn`은 AI 와 이벤트를 켠 채로 돈다 (`--noai` 로 끈다). 화면 왼쪽 위에 AI 상태별 인원, 미션 결과, 메시지, 가까운 사람 6명의 AI 상태가 나온다. F2 AI 정지/재개, F4 전원 비전투, End 전원 경계, F9+↑/↓ 복제, Insert 플레이어 무적, Home 디버그 텍스트 켜기/끄기 (`--invincible`, `--notext` 로 시작 상태를 정한다).

## 지금 구현이 돌아가는 흐름 (새 세션은 여기부터 읽는다)

### 한 프레임

`ProcessPriority` 순서대로 돈다.

1. `InputManager` — 이번 프레임 입력 확정.
2. `SimClock`(-200) — 쌓인 시간만큼 틱을 발행한다 (0~4회). 아래 "한 틱" 참조.
3. 일반 노드(0) — `BulletManager`, `WeaponManager`, `EffectManager`, `SoundManager`의 `_Process`. 탄환·떨어진 무기 모델을 틱 사이로 보간해 놓고, 이펙트를 진행하고, 소리 볼륨을 카메라 거리로 갱신한다.
4. `Human`(50) — `HumanController.ApplyVisual`로 노드 transform 보간, 팔 pitch 반영, 틱에서 표시해 둔 총구 화염·탄피 재생.
5. `PlayerController`(100) — 입력을 읽어 `HumanController.SetInput`(이동·조준)과 `Human.QueueWeaponInput`(무기)에 넣고 카메라를 배치한다. 넣은 입력은 다음 틱이 소비한다.

### 한 틱 (`SimClock.Step`, `SimOrder` 오름차순)

| 순서 | 대상 | 하는 일 |
|---|---|---|
| 10 | `HumanController` (사람마다) | `Human.TickWeapon`(무기 입력 소비 → 카운터 감소 → 조준 오차) → 이동·맵 충돌 → 사망 상태머신 → 다리·팔 동작 |
| 30 | `WeaponManager` | 떨어진 무기 낙하, 줍기 |
| 40 | `BulletManager` | 직선 탄 전부(판정 → 이동), 그다음 수류탄 전부 |
| 100 | `HumanCollision` | 사람끼리 밀어내기 (더한 속도는 다음 틱 이동이 소비) |
| 200 | `AIController` | 플레이어가 아닌 사람마다 `AIBrain.Tick`. AI 가 정한 입력은 다음 틱의 10 이 소비한다 (원본의 1프레임 지연) |
| 300 | `MissionStats`, `EventManager` | 플레이 시간, 자동 판정 → 이벤트 세 줄 → 메시지 시간 |

### 누가 무엇을 갖는가

- `MapLoader`(Autoload, partial 여러 파일): 블록과 충돌 조회(`RaycastBlock`, `IsInsideBlock`), 스카이·안개, 미션 정보, PD1 포인트 조회(`GetPoint`, `GetPathPoint`, `GetEventPoint`), 사람 목록(`Humans`, `Player`), 소물 목록(`SmallObjects`), 통계(`Stats`), 메시지(`GetMessageText`). `LoadPointData`가 사람·무기·소물을 스폰하고 `UnloadPointData`가 모든 풀을 비운다.
- `Human`(Node3D, partial): `Human.cs`(생성, HP, 피격), `HumanWeapon.cs`(슬롯·발사·재장전·전환·버리기·줍기·발사 이펙트), `HumanAim.cs`(조준 오차, 스코프). 논리 상태는 `Human.Controller`(`HumanController`, 순수 클래스)가 갖고 노드 transform 은 시각 전용이다.
- 사람에게 입력을 넣는 창구는 둘뿐이다: `Controller.SetInput(in HumanInput)`(이동 플래그는 OR 누적, 조준각은 덮어씀)과 `Human.QueueWeaponInput(HumanWeaponAction)`. 플레이어도 AI 도 이 창구만 쓴다.
- 순수 클래스(노드 아님): `HumanController`, `HumanCollision`, `Weapon`, `Bullet`, `MissionStats`, `HumanHitbox`(static), `WorldSound`(static).
- 점검 도구(`src/Dev/`): `WeaponCheck`는 사람을 공중(y=500)으로 옮기고 `Human.TickWeapon()` / `BulletManager.Instance.SimTick()` 을 직접 불러 이동 없이 확인한다. 맵 전체를 돌릴 때는 `SimClock.Step()`. 새 시스템의 점검도 같은 방식으로 만든다.

### 코드를 쓸 때 지킬 것 (자세한 것은 `CLAUDE.md`)

- 네임스페이스 `GodotXOPS`, private 필드 `m_`, static `s_`, const `k_`. 주석은 한국어, 클래스와 함수에 `/// <summary>` + `<param>` + `<returns>`. Godot 가상 함수에는 주석을 달지 않는다.
- 주석에는 무엇이고 왜 그런지, 그리고 원본 출처(`원본 object.cpp:1607-1644`)를 적는다. "UnityXOPS 에서 옮겼다"는 적지 않는다.
- 숫자는 `k_` 상수로 빼고 단위와 원본 값을 주석에 적는다. 원본 길이는 ×0.1 해서 미터, 프레임당 값은 `SimClock.FrameRate`로 초 단위로 바꾼다. 시간 카운터는 정수 틱.
- 게임 결과에 영향을 주는 난수는 `GameRandom.Gameplay`(틱에서만), 연출은 `GameRandom.Visual`.
- 좌표·각도 변환은 `Coord`만 쓴다. 각도는 UnityXOPS 규약(도, yaw 오른쪽 +, pitch 아래 +).
- JSON 에 있는 값은 그대로 읽는다. 원본 상수로 하드코딩하지 않는다.
- 조작감에 직결되는 코드는 UnityXOPS 와 원본 C++ 를 함께 대조하고, 계산 방식이 다르면 표로 사용자에게 올려 결정을 받는다. 지금까지의 경향: 원본의 어색한 동작을 UnityXOPS 가 고친 것은 UnityXOPS 를, UnityXOPS 가 엔진 사정으로 근사한 것(물리 레이, 실수 초 타이머, 렌더 프레임 계산)은 원본 방식을 택했다.
- 작업을 마치면 점검 씬 8개를 전부 돌리고, `--headless --path . --import`로 새 `.cs.uid`를 만든 뒤 함께 커밋한다. 빌드 경고 0개.

## 6단계에서 정한 것

- **총알 판정은 원본 방식이다.** 한 틱의 경로를 0.25 m 간격 점으로 나눠, 점마다 사람(수직 원기둥) → 소물(자리만 있음) → 맵(블록 내부) 순으로 검사한다. 엔진 물리도 선분 교차도 쓰지 않는다.
- **무기 처리는 사람 틱(SimOrder 10) 맨 앞에서 한다.** 입력 소비(발사 등) → 카운터 감소 → 조준 오차 갱신 → 이동 순서다. 원본 한 프레임의 순서(입력 → `human::ProcessObject`)와 같고, 총알은 이동 전 위치에서 나간다. SimOrder 30은 7단계의 떨어진 무기용으로 남겨 둔다.
- **카운터는 모두 정수 틱이다** (발사 간격, 재장전, 슬롯 전환, 종류 전환, 탄환 수명). 데이터의 초 단위 값은 `RoundToInt(초 × 33.3333)`으로 바꾼다.
- **무기 구조**: `Weapon`(순수 클래스, 종류와 탄약) + `WeaponVisual`(모델 노드). 사람은 슬롯 2개를 항상 갖고 빈 슬롯은 맨손 무기다. 발사 간격·재장전 카운터는 원본처럼 사람이 갖는다.
- **UnityXOPS가 원본 동작을 고친 네 곳은 UnityXOPS를 따른다** (사용자 결정): 피격 시 조준 흐트러짐은 더 큰 쪽 유지, 가득 찬 탄창 재장전 불가, 폭풍은 항상 멀어지는 쪽, 반동 오차는 조준선과 무관하게 누적.
- **히트박스 회전(`rotationEuler`)을 지원한다.** 회전이 없는 부위는 원본과 같은 수직 원기둥이다.
- **무기 동작은 데이터로만 갈린다.** 코드에 무기 번호를 쓰지 않는다 (`noneWeaponIndex`, `grenadeWeaponIndex`만 참조). `reloadStyle`, `burstMode`, `explosionTrigger`, `useGravity`, `ignoreAimError` 같은 분기 필드를 유지해 JSON만으로 새 무기를 만들 수 있게 한다.

## 7단계에서 정한 것

- **떨어진 무기의 낙하와 줍기는 틱에서 계산한다** (`WeaponManager`, SimOrder 30). 수치는 `drop_physics.json`, 착지는 UnityXOPS 방식(아래로 레이, 바닥 위 `groundCollisionMargin`)이다. 줍기는 매 틱 전부 검사한다 (원본은 2프레임에 한 번).
- **부서진 소물은 판정에서 바로 빠진다** (UnityXOPS). 튀어 오르는 것은 화면 연출이다. 총알-소물 판정은 원본 점 샘플링이고 구·박스·캡슐 형상을 지원한다.
- **이펙트는 렌더 프레임에서 진행한다** (연출, 연출용 난수). 총구 화염·연기·탄피는 틱이 아니라 다음 화면 갱신 때 무기 노드 위치에서 낸다. 탄피는 `shellEjectDelay` 뒤에 나온다.
- **소리는 선형 감쇠(1 m ~ 33.5 m), 패닝 없음.** 일반 재생기 풀 64개의 볼륨을 카메라 거리로 직접 계산한다. 헤드리스에서는 재생하지 않고 호출만 센다.
- **통계**(`MapLoader.Stats`)는 쏜 사람이 플레이어일 때만 기록한다. 플레이 시간은 틱 수로 센다.
- UnityXOPS 값과 다르게 한 것: 사망 시 무기가 흩어지는 속도는 5.0 m/s (원본 프레임당 1.5. UnityXOPS 는 0.15 m/s 로 단위가 어긋나 있었다), 폭발 혈흔 높이는 발 위 1.5 m (원본 hy + 15. UnityXOPS 는 발 위치), 이펙트에도 안개를 적용한다 (원본 고정 파이프라인).

## 8단계에서 정한 것

- **구조**: `AIBrain`(순수 클래스)을 `Human`이 갖고, `AIController`(순수 `ISimTickable`, 200)를 `MapLoader`가 등록한다. `EventManager`는 시그널을 가진 Autoload(300)다.
- **계산은 원본 `ai.cpp` 기준**이고, 사용자 결정으로 다르게 한 것은 아래 다섯 가지와 포인트 검색이다:
  1. 회전·경로 이동 의사는 매 틱 새로 정한다 (UnityXOPS 방식). 유지되는 것은 두리번거림과 전투 중 회피 이동뿐이다.
  2. 아군 시체를 보고 경계하는 조건(`CheckCorpse`)은 넣지 않았다. OpenXOPS 코드에는 있지만 사용자가 원본 게임에서 본 적이 없다고 했다. 넣으려면 `AIBrain.NormalMain`의 경계 진입 조건에 한 줄을 더하면 된다 (원본 ai.cpp:1447-1486, 1738).
  3. 좀비는 적의 이동을 앞질러 겨누지 않는다. 원본 `AItrackability`(사람 종류별 0~3)는 데이터에 필드가 없고 추가하지 않기로 했다.
  4. 맨손인 사람의 팔은 전투 중에만 조준 방향을 따른다 (UnityXOPS 방식).
  5. AI 는 무기 관리 때 스코프를 해제하지 않는다. 지금 AI 가 스코프를 켜는 코드는 없고, 스코프 종류별 AI 값(`aiScopeData`: 발사 허용각, 탐색 거리 가산)만 쓴다. "AI 가 스코프로 조준하는 것을 살리자"는 요청을 이렇게 해석했으므로, AI 가 실제로 스코프를 켜야 한다는 뜻이었다면 추가 작업이 필요하다.
- **원본에서 실행되지 않는 코드**: `MoveTarget`의 "끼었을 때 좌우로 돌기"(ai.cpp:212-220)는 조건(현재 이동 플래그, 누적 이동량)이 그 시점에 늘 0 이어서 원본에서도 돌지 않는다. UnityXOPS 는 이것을 살려 놓았는데, 원본대로 옮기지 않았다.
- **포인트 검색**은 UnityXOPS 처럼 종류별로 한다 (`MapLoader.GetPathPoint`, `GetEventPoint`). 원본은 종류와 무관하게 같은 번호의 첫 포인트를 찾아서 번호가 겹치면 경로·이벤트가 끊기는데, 사용자가 원본의 버그라고 판단했다.
- **발소리**는 AI 청각 신호만 낸다. `WorldSound.EmitFootstep`이 걷기·전진·후진·좌우·점프·착지를 모두 받으므로 WAV 를 넣을 때는 그 함수에서 재생하면 된다 (발밑 블록의 텍스처 번호가 필요하면 사람에서 구한다).
- **소리를 듣는 시점**은 원본보다 한 틱 빠르다 (같은 틱에 듣는다).
- **UnityXOPS 의 무장 좀비**(리치 `zombieMaxMeleeRange`, 무기 데미지 가산, 발사 속도 주기)는 유지했다. 판정 대상은 원본처럼 겨눈 적 한 명이다.
- **`--selftest`의 실패 조건**은 "좌표가 깨짐"과 "플레이어가 아닌 사람이 맵 아래로 빠짐"이다. AI 가 서로 싸우므로 사망 수는 출력만 한다.

## 9단계에서 정한 것

- **구조**: 씬 6개(`boot`, `opening`, `mainmenu`, `briefing`, `maingame`, `result`), 화면 요소는 GDScript 가 코드로 만들고 수치는 스크립트 맨 위 상수 표에 모았다. C# 창구는 Autoload `Game`(`GameBridge`) 하나를 새로 두었다.
- **기준**: 구성은 UnityXOPS 0.2 의 `Runtime/Scene`, 수치와 동작 규칙은 0.4 의 `unitydata/scene/*.lua`.
- **사용자 결정**:
  1. 조준선은 막대 4개다 (0.4 방식). `config.json`의 `aimLength`·`aimThick`·`aimGap`·`aimColor*`·`StaticAim`을 쓰고, 간격 = `aimGap` + 조준 오차.
  2. 메뉴는 0.2 범위(타이틀, 버전, 미션 목록과 스크롤, 공식/어드온 전환, 어드온 페이지 전환, ESC 종료 확인)에 CREDIT·EXIT 버튼을 더했다. **OPTION(설정 화면)은 넣지 않았다** — 포팅 뒤 현대화 단계로 미뤘다.
  3. HUD 에 표시 방식 3단(F2: 일반 → 간이 → 끔), FPS 표시(`ShowFPS`), 벽 블라인드를 넣었다.
- **벽 블라인드**는 UnityXOPS 0.4 방식이다: 가까운 절단면 사각형의 네 변 가운데 점이 블록 안인지 본다. 원본(gamemain.cpp:2973-3003)은 시야 안쪽의 점을 봐서 가장자리로 벽이 파고들면 놓친다.
- **밝기·감마**는 화면 전체(화면 요소 포함)에 적용된다. UnityXOPS 는 3D 화면에만 적용했다. 둘 다 1 이면 아예 그리지 않는다.
- **오프닝 문구**의 "UnityXOPS"는 "GodotXOPS"로 바꿨다. 나머지 이름과 시각은 Lua 그대로다.
- **메인게임의 키**: F1 시점, F2 표시 방식, F12 재시작, ESC 메뉴로. 미션이 (다시) 시작된 뒤 1초 동안은 ESC 와 F12 를 받지 않는다. 치트 키(F5~F9)는 `PlayerController`에 그대로 있다.

## 9단계 뒤에 고친 것 (사용자 확인, 2026-10-05)

- HUD 의 STATE 상자 아랫줄이 그려지지 않던 것 (크기 없는 글자 노드가 화면 밖 기준점에서 통째로 빠짐 → 그릴 범위를 직접 지정).
- 무기 이름을 상자 왼쪽 끝에서 9픽셀로 (원본 값. Lua 는 22픽셀).
- 1인칭 팔이 카메라보다 한 프레임 늦게 돌던 것 (입력 직후 `ApplyVisual` 을 한 번 더).
- 쓰러지는 동작이 33Hz 로 끊겨 보이던 것 (사망 회전 각도를 틱 사이 보간).

## 다음 작업: 10단계 — 원본 대조 마무리

할 일 (위에서부터):

1. **사망 시 쓰러지는 동작**을 원본 `human::CheckAndProcessDead`(object.cpp:1166-1456)와 대조한다. UnityXOPS 구현을 그대로 옮겼고 원본과는 맞춰 보지 않았다.
2. **화면으로 확인하지 못한 HUD 요소**를 하나씩 띄워 본다: 스코프(그림·검은 막대·조준선), 피격 번쩍임, 이벤트 메시지, 미션 종료 연출 → 결과, 벽 블라인드, FPS 표시, 밝기·감마. 상태를 강제로 만드는 개발용 인자(`--ui-state`)를 늘리면 스크린샷으로 볼 수 있다.
3. **스카이 방향**, **떨어진 무기가 눕는 방향**을 원본 화면과 비교한다.
4. **이펙트 모양**(혈흔, 벽 데칼, 착탄 연기, 폭발)을 화면으로 확인한다.
5. **미션 59개 통과 점검**: 각 미션의 이벤트 줄이 끝까지 진행되는지(클리어 조건, 메시지 순서). 자동으로 볼 방법을 정해야 한다 (아래 논의).
6. **TGA, RLE·비트필드 BMP** 로더: 데이터에 없어 시험하지 못했다. 어드온에서 나오면 그때 본다.

## 논의할 것 (다음 세션에서 사용자에게 올린다)

- **10단계의 범위와 끝내는 기준.** "완전 포팅"을 어디서 끝났다고 볼지: 위 목록을 다 확인하면 끝인지, 미션 59개를 직접 클리어해 보는 것까지인지.
- **미션 통과를 자동으로 점검할 방법.** 플레이어를 무적으로 두고 AI 에게 맡겨 돌리는 방식, 이벤트 줄의 포인트 연결만 정적으로 검사하는 방식 등.
- **포팅 뒤 현대화의 순서.** 미뤄 둔 것: 메뉴의 OPTION(설정 화면, UnityXOPS 0.4 `mainmenu.lua`에 구현이 있다), 일시정지 메뉴, 체크포인트, 저해상도 렌더를 확대할 때의 화질, 모딩. 무엇부터 할지와 UnityXOPS 방식을 그대로 따를지.
- **화면 수치를 외부 데이터로 뺄지.** 지금은 화면별 GDScript 맨 위 상수 표에 있다. `godotdata/` JSON 으로 옮기면 유저가 고칠 수 있지만 형식을 정해야 한다.
- **밝기·감마의 적용 범위.** 지금은 화면 요소까지 포함한 전체에 적용된다. UnityXOPS 처럼 3D 화면에만 적용할지.
- **발소리 WAV.** 넣을 자리(`WorldSound.EmitFootstep`)는 있다. 넣을지, 넣는다면 어떤 소리를 쓸지 (원본에는 발소리 파일이 없다).
- **아군 시체를 보고 경계하는 조건**(`CheckCorpse`)과 **AI 가 실제로 스코프를 켜는 동작.** 8단계에서 해석해 넘어간 것이라 다시 확인이 필요하다.
- **익스포트 빌드.** 배포 형태(데이터 폴더 배치, `data/`·`addon/`은 사용자가 따로 넣는 방식), F8 이 에디터 실행 창을 닫는 문제가 익스포트에서도 나는지.

## 아직 옮기지 않은 것 (단계별)

- **메뉴의 OPTION(설정 화면)**: UnityXOPS 0.4 에만 있는 현대화 기능이라 포팅 뒤로 미뤘다. 설정 값 자체(`config.json`)와 읽고 쓰는 API(`ConfigManager`)는 이미 있다.
- **옮기지 않기로 한 것**: `Runtime/Modding/` 전체와 Lua, `LetterboxController`(Godot 창 스케일로 대체), 자체 BMP/DDS/TGA/WAV 파서(Godot 내장 로더로 대체)

## 확인이 덜 된 것

- **화면(9단계)**은 사용자가 창에서 플레이해 보고 이상 없다고 확인했다 (2026-10-05). 아래 요소를 하나하나 본 것은 아니다.
- **스코프 화면**(그림, 검은 막대, 조준선), **피격 번쩍임**, **이벤트 메시지**, **미션 종료 연출**(암전과 문구 → 결과 화면), **벽 블라인드**, **FPS 표시**는 화면으로 확인하지 못했다. 값을 주고받는 창구만 `ui_check`로 확인했다.
- **밝기·감마**는 기본값(1, 1)이라 사각형이 그려지지 않는 상태만 확인했다. 값을 바꿨을 때의 화면은 보지 않았다.
- **브리핑 본문 글꼴**은 한국어 환경(맑은 고딕)에서만 봤다. 일본어 미션의 본문이 제대로 나오는지는 확인하지 않았다.

- **8단계(AI, 이벤트, 좀비, 치트 F9)**는 사용자가 창에서 플레이해 보고 잘 작동한다고 확인했다 (2026-10-04). 미션 59개를 하나하나 끝까지 해 본 것은 아니다.
- **효과음**은 재생 호출과 볼륨 계산만 점검했다. 실제로 들리는 소리(음량 균형, 거리감, 통과음)는 사용자가 창에서 확인해야 한다.
- **이펙트 모양**은 총구 화염·탄피·연기를 스크린샷으로 봤다. 혈흔, 벽 데칼, 착탄 연기, 폭발은 호출 수만 점검했고 화면은 확인하지 않았다.
- **떨어진 무기의 방향**(옆으로 눕는 모양)은 스크린샷 한 장으로만 봤다. 원본과 같은 쪽으로 눕는지는 확인하지 않았다.
- **무기 조작감**(반동, 연사 느낌, 팔 반동 모양, 무기를 쥔 위치)은 수치와 스크린샷으로만 확인했다. 사용자가 창에서 직접 확인해야 한다.
- **스코프**는 켜짐/꺼짐 조건만 점검했다. 시야각이 바뀐 화면은 확인하지 않았다.
- **탄환 표시 시점**(총구를 지난 뒤부터 보이기)은 3인칭 스크린샷 한 장으로만 확인했다. 높은 프레임에서 머리를 뚫고 보이지 않는지는 사용자 확인이 필요하다.
- **사망 시 쓰러지는 동작**은 UnityXOPS 구현을 그대로 옮겼고 원본 `human::CheckAndProcessDead`(object.cpp:1166-1456)와 대조하지 않았다. 사용자가 화면으로 동작은 확인했다.
- **스카이 방향**이 원본과 같은지 확인하지 않았다.
- **저해상도 렌더를 확대할 때의 화질**(게임 기본값은 640×480 렌더)을 정하지 않았다. UI 단계에서 본다.
- **사람 키를 2.0 m로 바꾼 것**(원본 `HUMAN_HEIGHT`)은 RUINS_EXT의 낮은 통로에서 수치로만 확인했다. 사용자가 다른 자리에서 원본과 다르다고 하면 `godotdata/human/controller.json`의 `height`를 다시 본다.
- **F8**(조작 대상 교체 치트)은 Godot 에디터 실행 창에서 창이 닫힌다. 에디터의 실행 중지 단축키로 추정되며 익스포트 빌드에서는 확인하지 않았다.
- **TGA 파일**은 데이터에 없어 로더를 시험하지 못했다. RLE·비트필드 BMP도 마찬가지다.
- `data/map3/suna.bmp`가 없다는 로그는 원본 데이터에 그 파일이 없어서 나는 것이다 (해당 면은 흰색).

## 알려진 주의점

- 한 프레임 안에 노드를 대량으로 만들고 지운 직후 종료하면 종료 과정에서 간헐적으로 죽는다. 점검 씬은 종료 전에 `GC.Collect()`와 `GC.WaitForPendingFinalizers()`를 부른다. 런타임 노드 교체는 `RemoveChild` + `Free`를 쓴다.
- `godotdata/config.json`은 게임이 `Save` 할 때 덮어쓴다. 로컬 테스트로 바뀐 값을 커밋하지 않도록 주의한다.
- `data/`와 `addon/`은 원본 저작물이라 커밋하지 않는다. 새로 클론하면 UnityXOPS의 `Assets/StreamingAssets/data`, `addon`에서 복사해 와야 실행된다.
