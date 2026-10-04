# TODO — GodotXOPS 진행 상황과 다음 작업

> 세션을 시작하면 이 파일을 먼저 읽는다. 작업을 끝내면 갱신한다. 설계 규칙과 컨벤션은 `CLAUDE.md`에 있다.

## 현재 상태 (2026-10-04)

브랜치 `main`. 7단계까지 커밋·푸시했다. 저장소: https://github.com/jejusbluesea/GodotXOPS

맵을 로드해 플레이어로 걸어 다니며 무기를 쏘고, 버리고, 줍고, 사람과 소물을 맞힐 수 있다. 이펙트와 효과음이 나온다. AI, 이벤트, 게임 화면(UI)은 아직 없다.

| 단계 | 상태 | 주요 파일 |
|---|---|---|
| 1. 뼈대, 파일 로더 | 완료 | `src/Utility/`, `src/IO/` |
| 2. 데이터 계층 | 완료 | `src/Data/`, `godotdata/` |
| 3. 설정과 입력 | 완료 | `src/Data/Config/`, `src/Data/Input/` |
| 4. 맵 표시 (블록, 스카이, 안개, 충돌 계산) | 완료 | `src/Map/Block/`, `src/Map/Sky/`, `src/Map/Mission/`, `shaders/` |
| 5. SimClock, 캐릭터 (이동·충돌, 모델, 플레이어 조작) | 완료 | `src/Map/SimClock.cs`, `src/Map/Human/`, `src/Map/Point/` |
| 6. 무기, 총알, 히트박스 | 완료 | `src/Map/Weapon/`, `src/Map/Bullet/`, `src/Map/Human/HumanWeapon.cs`, `HumanAim.cs`, `HumanHitbox.cs` |
| 7. 이펙트, 사운드, 무기 드롭/줍기, 소물 | 완료 (소리와 화면 연출은 사용자 확인 필요) | `src/Map/Effect/`, `src/Map/Sound/`, `src/Map/Object/`, `src/Map/Weapon/WeaponManager.cs`, `src/Map/MissionStats.cs`, `shaders/effect_blend.gdshader` |
| 8. AI, 이벤트, 미션 판정 | **다음** | — |
| 9. 씬 UI (오프닝, 메뉴, 브리핑, HUD, 결과) | 대기 | — |
| 10. 원본 대조 마무리 | 대기 | — |

## 점검 씬 (작업을 마칠 때마다 전부 통과해야 한다)

Godot 콘솔 실행 파일로 `--headless --path . <씬> [-- 인자]` 형식으로 실행한다. 종료 코드 0이 통과다.

| 씬 | 인자 | 확인하는 것 |
|---|---|---|
| `res://scenes/dev/loader_check.tscn` | — | `data/`, `addon/`의 이미지·사운드·모델 전체 로드 |
| `res://scenes/dev/data_check.tscn` | — | `godotdata/` JSON과 로드된 값의 키 단위 대조 |
| `res://scenes/dev/config_input_check.tscn` | — | 설정 읽기/쓰기/되돌리기, 입력 조회 |
| `res://scenes/dev/map_viewer.tscn` | `--selftest` | 59개 미션 블록 로드와 충돌 레이 |
| `res://scenes/dev/play_test.tscn` | `--selftest` | 59개 미션에서 사람 1,057명이 150틱 동안 맵 아래로 빠지지 않는지, 맵 배치 무기 449개와 소물 210개 스폰 |
| `res://scenes/dev/weapon_check.tscn` | — | 110항목: 부위 명중, 스침, 관통, 연사 간격, 재장전, 조준 오차, 전체 무기 발사, 폭발, 수류탄 비행, 벽, 사망, 버리기·낙하·줍기, 소물 피격·파괴, 통계, 이펙트·소리 호출, 소리 거리 감쇠 |

눈으로 확인하는 도구 (`--headless` 없이): `asset_viewer.tscn`, `map_viewer.tscn`, `play_test.tscn`. 뒤의 둘은 `--screenshot 경로.png`로 화면을 저장한다.
`play_test.tscn`은 무기 확인용 인자를 받는다: `--weapon 번호`, `--fire`, `--hitbox`, `--look yaw,pitch`, `--pos x,y,z`, `--drop`. 훈련장(미션 0)의 플레이어는 맨손이므로 `--weapon 1`(MP5)처럼 쥐여 주거나 F7+←/→로 바꾼다.

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
| 200 | (비어 있음) | **8단계 AI 자리.** AI 가 정한 입력은 다음 틱의 10 이 소비한다 (원본의 1프레임 지연) |
| 300 | `MissionStats` | 플레이 시간. **8단계 이벤트·미션 판정 자리** |

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
- 작업을 마치면 점검 씬 6개를 전부 돌리고, `--headless --path . --import`로 새 `.cs.uid`를 만든 뒤 함께 커밋한다. 빌드 경고 0개.

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

## 다음 작업: 8단계 — AI, 이벤트, 미션 판정

**설계 논의부터 한다.** 아래를 읽고 UnityXOPS 코드와 원본을 대조한 뒤, 결정이 필요한 것을 권고안과 함께 사용자에게 올린다. 구현은 결정을 받은 다음이다.

### 옮길 대상

UnityXOPS `C:\Users\twoj2\Desktop\Project\UnityXOPS\Assets\UnityXOPS\Runtime\Map\` (브랜치 `QoL-road-to-multiplay(0.4)`):

| 파일 | 줄 수 | 내용 |
|---|---|---|
| `Human/AIController.cs` | 84 | AI 틱 진입점 (`SimOrder` 200). 사람마다 `AIBrain`을 돌리고 이동 의사를 컨트롤러에 넣는다 |
| `Human/AIBrain.cs` | 329 | 상태(통상·경계·전투 등)와 전이, 피격·소리 반응 |
| `Human/AIBrainAim.cs` | 191 | 조준, 시야 판정 |
| `Human/AIBrainCombat.cs` | 210 | 적 탐색, 사격 판단 |
| `Human/AIBrainNavigation.cs` | 204 | 경로 따라가기, 장애물 대응 |
| `Human/AIBrainWeapon.cs` | 76 | 무기 선택, 재장전 |
| `Human/AIBrainZombie.cs` | 222 | 좀비 근접 공격 |
| `Human/AIMoveNavi.cs` | 138 | PD1 경로 포인트 순회 (치트 F9 용 고정 목표 포함) |
| `Event/EventManager.cs`, `EventType.cs` | 274, 21 | 미션 이벤트 줄 처리, 클리어·실패 판정 (`SimOrder` 300) |

원본 대조 대상: `OpenXOPS/ai.cpp`(2,371줄), `event.cpp`(352줄), `gamemain.cpp`의 미션 판정·치트 부분, `objectmanager.cpp`의 `CheckZombieAttack` / `HitZombieAttack`(2416-2530), 발소리(`SetFootsteps`, 2760-2805).
데이터: `godotdata/human/ai.json`(`HumanAIParameterData`, AI 레벨별 값과 `aiHear*` 청취 거리), `type.json`(좀비 여부, 근접 데미지).

### 설계할 때 정해야 하는 것

- **AI 클래스 구조.** UnityXOPS 는 `AIBrain` partial 5개다. Godot 에서도 순수 클래스(노드 아님)로 두고 `Human`이 하나씩 갖게 할지, 플레이어가 된 사람(F8 로 교체)의 AI 를 어떻게 멈출지 정한다. 원본은 플레이어 번호만 건너뛴다.
- **시야·사선 판정.** UnityXOPS 가 `Physics.Raycast`를 쓴 곳은 `MapLoader.RaycastBlock`으로 바꾼다. 원본 `ai.cpp`의 판정(블록만 보는지, 사람·소물도 보는지)을 확인해 그대로 따른다.
- **난수.** AI 판단의 `GetRand`는 전부 `GameRandom.Gameplay`. 호출 순서가 원본과 같아야 하는 곳이 있는지 확인한다.
- **AI 의 틱 주기.** 원본은 매 프레임 돌지만 일부 판단은 카운터로 쉰다. UnityXOPS 가 실수 초 타이머로 바꾼 곳은 정수 틱으로 되돌린다.
- **이벤트.** PD1 이벤트 포인트(종류 10~19)를 `MapLoader.GetEventPoint`로 따라간다. 메시지 표시와 클리어·실패 결과를 9단계 UI 가 읽을 수 있게 어디에 둘지(시그널을 가진 창구 Autoload) 정한다. GDScript 는 C# static 을 못 본다.
- **발소리.** 원본은 소리 재생과 AI 청각을 한 함수로 한다. 데이터에 `aiHearFootstep*`가 있다. `HumanController.SimTick`에 넣을 자리와, 발소리 WAV 를 실제로 낼지(UnityXOPS 가 냈는지)를 확인한다.
- **맨손 팔.** 원본은 플레이어가 아닌 맨손 사람의 팔이 조준 방향을 따른다 (`object.cpp:3431-3438`). UnityXOPS 는 좀비 공격·항복 동작 때만 따르게 했다. `Human.SetUnarmedArmDynamic`이 준비돼 있다. 어느 쪽으로 할지 사용자에게 묻는다.
- **검증 방법.** AI 는 눈으로 봐야 하는 부분이 많다. `play_test`에 AI 켜기/끄기, AI 상태 표시를 넣고, `--selftest`는 AI 를 켠 채 59개 미션을 일정 틱 돌려 예외·맵 이탈이 없는지 본다. 지금 `--selftest`는 "서 있기만 한 사람이 죽으면 실패"로 보는데, AI 가 켜지면 서로 쏘므로 이 조건을 바꿔야 한다.

### 이미 준비돼 있는 것

- AI 청각 신호: `WorldSound.EmitPointSound`가 총성·착탄·피격·폭발·소물 피격에서 호출되고, 총알 통과는 `Bullet.NotifyBulletPass`가 알린다. AI 는 `Human.ConsumeThreatHeard()` / `ConsumeHit(out 공격자 방향)` 으로 소비한다. `SimClock.TickEnabled` 가 false 면 신호를 내지 않는다.
- 무기 조작: `Human.QueueWeaponInput` 또는 `ShotWeapon` / `ReloadWeapon` / `SetSelectWeapon` / `DropCurrentWeapon`. 연속 발사 수 제한(단발 무기)이 사람 쪽에 있어 AI 가 매 틱 발사를 요청해도 원본처럼 동작한다.
- 사람 정보: `Human.AILevel`, `PathStartId`, `Team`, `Identifier`, `HumanTypeData.zombie`, `CurrentErrorRange()`, `IsReloading`, `Controller.Position` / `Yaw` / `Pitch` / `Grounded` / `MoveVelocity`.
- 찾기: `MapLoader.SearchHuman`, `SearchSmallObject`, `GetPathPoint`, `GetEventPoint`, `GetMessageText`, `Human.SetTeam`(팀 변경 이벤트).
- 밀기: `Controller.AddKnockback` / `AddKnockbackVector`, 순간 이동: `Controller.Teleport`.

### 아직 없는 것

- 발소리, 좀비 근접 공격과 그 혈흔·소리, 치트 F9(복제).
- HUD(9단계): 조준선(`Human.CurrentErrorRange()`), 스코프 화면, 탄약 표시, 스코프 중 마우스 감도, 피격 화면 번쩍임(`Human.ConsumeHit`), 통계 화면(`MapLoader.Stats`).

## 아직 옮기지 않은 것 (단계별)

- **9단계**: 씬 UI. 기준은 UnityXOPS `origin/first-release(0.1)`, `origin/refactoring(0.2)` 브랜치의 `Runtime/Scene/`. 화면 로직은 GDScript로 쓰고 C# 창구 Autoload만 호출한다. `Camera/ScreenColorAdjust`(밝기·감마 셰이더)와 `Font/`도 이때 옮긴다.
- **옮기지 않기로 한 것**: `Runtime/Modding/` 전체와 Lua, `LetterboxController`(Godot 창 스케일로 대체), 자체 BMP/DDS/TGA/WAV 파서(Godot 내장 로더로 대체)

## 확인이 덜 된 것

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
