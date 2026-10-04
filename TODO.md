# TODO — GodotXOPS 진행 상황과 다음 작업

> 세션을 시작하면 이 파일을 먼저 읽는다. 작업을 끝내면 갱신한다. 설계 규칙과 컨벤션은 `CLAUDE.md`에 있다.

## 현재 상태 (2026-10-04)

브랜치 `main`. 6단계까지 커밋·푸시했다. 저장소: https://github.com/jejusbluesea/GodotXOPS

맵을 로드해 플레이어로 걸어 다니며 무기를 쏘고 사람을 맞힐 수 있는 단계다. 이펙트, 사운드, 무기 드롭/줍기, AI, 게임 화면(UI)은 아직 없다.

| 단계 | 상태 | 주요 파일 |
|---|---|---|
| 1. 뼈대, 파일 로더 | 완료 | `src/Utility/`, `src/IO/` |
| 2. 데이터 계층 | 완료 | `src/Data/`, `godotdata/` |
| 3. 설정과 입력 | 완료 | `src/Data/Config/`, `src/Data/Input/` |
| 4. 맵 표시 (블록, 스카이, 안개, 충돌 계산) | 완료 | `src/Map/Block/`, `src/Map/Sky/`, `src/Map/Mission/`, `shaders/` |
| 5. SimClock, 캐릭터 (이동·충돌, 모델, 플레이어 조작) | 완료 | `src/Map/SimClock.cs`, `src/Map/Human/`, `src/Map/Point/` |
| 6. 무기, 총알, 히트박스 | 완료 (조작감은 사용자 확인 필요) | `src/Map/Weapon/`, `src/Map/Bullet/`, `src/Map/Human/HumanWeapon.cs`, `HumanAim.cs`, `HumanHitbox.cs` |
| 7. 이펙트, 사운드, 무기 드롭/줍기, 소물 | **다음** | — |
| 8. AI, 이벤트, 미션 판정 | 대기 | — |
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
| `res://scenes/dev/play_test.tscn` | `--selftest` | 59개 미션에서 사람 1,057명이 150틱 동안 맵 아래로 빠지지 않는지 |
| `res://scenes/dev/weapon_check.tscn` | — | 무기·총알·히트박스 90항목 (부위 명중, 스침, 관통, 연사 간격, 재장전, 조준 오차, 전체 무기 발사, 폭발, 수류탄 비행, 벽, 사망) |

눈으로 확인하는 도구 (`--headless` 없이): `asset_viewer.tscn`, `map_viewer.tscn`, `play_test.tscn`. 뒤의 둘은 `--screenshot 경로.png`로 화면을 저장한다.
`play_test.tscn`은 무기 확인용 인자를 받는다: `--weapon 번호`, `--fire`, `--hitbox`, `--look yaw,pitch`. 훈련장(미션 0)의 플레이어는 맨손이므로 `--weapon 1`(MP5)처럼 쥐여 주거나 F7+←/→로 바꾼다.

## 6단계에서 정한 것

- **총알 판정은 원본 방식이다.** 한 틱의 경로를 0.25 m 간격 점으로 나눠, 점마다 사람(수직 원기둥) → 소물(자리만 있음) → 맵(블록 내부) 순으로 검사한다. 엔진 물리도 선분 교차도 쓰지 않는다.
- **무기 처리는 사람 틱(SimOrder 10) 맨 앞에서 한다.** 입력 소비(발사 등) → 카운터 감소 → 조준 오차 갱신 → 이동 순서다. 원본 한 프레임의 순서(입력 → `human::ProcessObject`)와 같고, 총알은 이동 전 위치에서 나간다. SimOrder 30은 7단계의 떨어진 무기용으로 남겨 둔다.
- **카운터는 모두 정수 틱이다** (발사 간격, 재장전, 슬롯 전환, 종류 전환, 탄환 수명). 데이터의 초 단위 값은 `RoundToInt(초 × 33.3333)`으로 바꾼다.
- **무기 구조**: `Weapon`(순수 클래스, 종류와 탄약) + `WeaponVisual`(모델 노드). 사람은 슬롯 2개를 항상 갖고 빈 슬롯은 맨손 무기다. 발사 간격·재장전 카운터는 원본처럼 사람이 갖는다.
- **UnityXOPS가 원본 동작을 고친 네 곳은 UnityXOPS를 따른다** (사용자 결정): 피격 시 조준 흐트러짐은 더 큰 쪽 유지, 가득 찬 탄창 재장전 불가, 폭풍은 항상 멀어지는 쪽, 반동 오차는 조준선과 무관하게 누적.
- **히트박스 회전(`rotationEuler`)을 지원한다.** 회전이 없는 부위는 원본과 같은 수직 원기둥이다.
- **무기 동작은 데이터로만 갈린다.** 코드에 무기 번호를 쓰지 않는다 (`noneWeaponIndex`, `grenadeWeaponIndex`만 참조). `reloadStyle`, `burstMode`, `explosionTrigger`, `useGravity`, `ignoreAimError` 같은 분기 필드를 유지해 JSON만으로 새 무기를 만들 수 있게 한다.

## 다음 작업: 7단계 — 이펙트, 사운드, 무기 드롭/줍기, 소물

### 6단계가 비워 둔 자리

- `Bullet.TickStraight` — 벽 착탄 이펙트·소리(`wallEntry` 지점, 원본 `HitBulletMap`), 소물 판정(주석으로 자리 표시), 총알 통과음.
- `Bullet.HitHuman` — 혈흔 이펙트, 피격음, 통계(명중·헤드샷·킬).
- `Bullet.Explode` — 폭발 이펙트·소리, 소물 폭발 데미지(주석으로 자리 표시), 수류탄 킬 통계.
- `Bullet.TickGrenade` — 바운드음 (반사 직전 속력이 틱당 0.34 m 를 넘을 때만, 원본 `speed > 3.4`). 속력은 `GrenadeSpeed`로 읽는다.
- `Human.ShotWeapon` — 격발음, 총구 화염·연기·탄피, 발사 통계.
- `Human.OnDeath` — 든 무기를 맵에 떨어뜨리기 (지금은 슬롯만 비운다. 원본은 무기마다 `GetRand(36)`으로 방향을 뽑는다).
- `Human.ApplyWeaponAction` — `Drop` 입력은 아직 아무것도 하지 않는다 (원본 `human::DumpWeapon`).
- 무기 줍기 (원본 `ObjectManager::PickupWeapon`, 2프레임에 한 번씩 번갈아 검사). 팔 동작은 `HumanVisual.BeginArmSlowReaction`을 쓴다.
- `MapLoader.LoadPointData` — 무기 포인트(종류 2, 7)와 소물 포인트(종류 5)를 스폰하지 않는다. `InitializeAddonObject`도 아직 호출하지 않는다.
- 떨어진 무기의 낙하는 원본처럼 틱에서 계산한다 (UnityXOPS는 렌더 프레임에서 계산한다. `weapon::ProcessObject` object.cpp:2448-).

### 8단계 이후로 넘긴 것

- AI 청각 통지(총성, 총알 통과, 피탄, 폭발), 좀비 근접 공격, 맨손 팔을 조준 방향으로 움직이기(`Human.SetUnarmedArmDynamic` 은 준비돼 있다), 발소리.
- HUD: 조준선(`Human.CurrentErrorRange()`), 스코프 화면, 탄약 표시, 스코프 중 마우스 감도.

## 아직 옮기지 않은 것 (단계별)

- **7단계**: `Effect/`(셰이더 `EffectBlend` 포함), `Sound/SoundManager.cs`, `Human/WorldSound.cs`, `Weapon/WeaponManager.cs`(드롭 풀), `Object/`(소물), `MissionStats.cs`
- **8단계**: `Human/AIBrain*.cs`, `AIController.cs`, `AIMoveNavi.cs`, `Event/EventManager.cs`
- **9단계**: 씬 UI. 기준은 UnityXOPS `origin/first-release(0.1)`, `origin/refactoring(0.2)` 브랜치의 `Runtime/Scene/`. 화면 로직은 GDScript로 쓰고 C# 창구 Autoload만 호출한다. `Camera/ScreenColorAdjust`(밝기·감마 셰이더)와 `Font/`도 이때 옮긴다.
- **옮기지 않기로 한 것**: `Runtime/Modding/` 전체와 Lua, `LetterboxController`(Godot 창 스케일로 대체), 자체 BMP/DDS/TGA/WAV 파서(Godot 내장 로더로 대체)

## 확인이 덜 된 것

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
