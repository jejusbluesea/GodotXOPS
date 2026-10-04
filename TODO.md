# TODO — GodotXOPS 진행 상황과 다음 작업

> 세션을 시작하면 이 파일을 먼저 읽는다. 작업을 끝내면 갱신한다. 설계 규칙과 컨벤션은 `CLAUDE.md`에 있다.

## 현재 상태 (2026-10-04)

브랜치 `main`, 마지막 커밋 `29d6c07`. 저장소: https://github.com/jejusbluesea/GodotXOPS

맵을 로드해 플레이어로 걸어 다닐 수 있는 단계다. 무기, AI, 게임 화면(UI)은 아직 없다.

| 단계 | 상태 | 주요 파일 |
|---|---|---|
| 1. 뼈대, 파일 로더 | 완료 | `src/Utility/`, `src/IO/` |
| 2. 데이터 계층 | 완료 | `src/Data/`, `godotdata/` |
| 3. 설정과 입력 | 완료 | `src/Data/Config/`, `src/Data/Input/` |
| 4. 맵 표시 (블록, 스카이, 안개, 충돌 계산) | 완료 | `src/Map/Block/`, `src/Map/Sky/`, `src/Map/Mission/`, `shaders/` |
| 5. SimClock, 캐릭터 (이동·충돌, 모델, 플레이어 조작) | 완료 | `src/Map/SimClock.cs`, `src/Map/Human/`, `src/Map/Point/` |
| 6. 무기, 총알, 히트박스 | **다음** | — |
| 7. 이펙트, 사운드, 무기 드롭/줍기, 소물 | 대기 | — |
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

눈으로 확인하는 도구 (`--headless` 없이): `asset_viewer.tscn`, `map_viewer.tscn`, `play_test.tscn`. 뒤의 둘은 `--screenshot 경로.png`로 화면을 저장한다.

## 다음 작업: 6단계 — 무기, 총알, 히트박스

분량이 많아 둘로 나눈다. 6단계에서 장착·발사·총알·히트박스를, 7단계에서 이펙트·사운드·드롭/줍기를 한다.

### 옮길 대상 (UnityXOPS `Assets/UnityXOPS/Runtime/Map/`)

- `Weapon/Weapon.cs` (465줄), `Weapon/WeaponVisual.cs` (113줄) — 무기 데이터, 발사, 연사 쿨다운, 재장전, 모델
- `Human/HumanWeapon.cs` (375줄) — `Human`의 partial. 슬롯 2개, 장착, 전환, 무기 입력 소비(`QueueWeaponInput` / `ConsumePendingWeapon`)
- `Human/Human.cs`의 무기·조준 부분 — 조준 오차(`GunsightErrorRange`), 반동 누적과 회복, 스코프. 현재 Godot `Human.cs`에는 이 부분이 빠져 있다.
- `Human/HumanVisual.cs`의 팔 반응 — `BeginArmReaction`, `BeginArmReactionHold`, `BeginArmShotReaction`, `TickArmReaction`. 현재 `SetArmPitch`만 있다.
- `Bullet/Bullet.cs` (637줄), `Bullet/BulletManager.cs` (133줄) — 탄도, 명중 판정, 관통, 수류탄, 폭발
- `Human/HumanHitbox.cs` (82줄) — 머리·몸통·다리 판정
- `Human/PlayerController.cs`의 무기 입력 — 발사, 재장전, 슬롯 선택, 무기 ID 전환, 치트 F6/F7

원본 대조 대상: `OpenXOPS/object.cpp`(human의 무기 함수, weapon, bullet, grenade 클래스), `objectmanager.cpp`(총알-사람/블록/소물 충돌, 데미지), `collision.cpp`.

### 설계할 때 정해야 하는 것

- **히트박스 판정 방식.** UnityXOPS는 캡슐 콜라이더와 `Physics.Raycast`를 쓴다. 여기서는 엔진 물리를 쓰지 않으므로 레이-캡슐 교차를 직접 계산해야 한다. 원본 `objectmanager.cpp`의 총알-사람 판정(원기둥·구 기반)을 먼저 읽고, 원본 방식으로 갈지 UnityXOPS의 캡슐 데이터(`godotdata/human/hitbox.json`)를 살릴지 사용자와 정한다.
- **총알과 소물의 충돌.** 소물(SmallObject)은 7단계에 옮기므로, 6단계에서는 소물 판정 자리를 비워 두고 구조만 맞춘다.
- **무기 노드 구조.** UnityXOPS는 장착 슬롯을 프리팹 인스턴스로 두고 드롭 무기는 `WeaponManager` 풀(200개)이 갖는다. Godot에서 노드로 둘지 순수 클래스 + 메시 노드로 둘지 정한다.
- **틱 순서.** 무기 처리는 `SimOrder` 30(`Human`의 무기 타이머), 총알은 40(`BulletManager`)이다. 총알은 논리 위치와 시각 위치를 분리해 틱 사이를 보간한다 (`HumanController`와 같은 방식).

### 6단계에서 연결할 자리 (지금 비어 있는 곳)

- `Human.CreateHuman` — 지금은 맨손 팔 모델만 적용한다(`NoneWeaponModel`). 초기 무기 장착(`EquipInitialWeapons`)으로 바꾼다.
- `HumanController.EnterDeadState` — 사망 시 무기 떨어뜨리기(`DropAllWeaponsOnDeath`), 사망 이펙트가 빠져 있다.
- `HumanController.SimTick` — 발소리 월드 사운드(`EmitFootstep`, AI 경계용)가 빠져 있다. 8단계에서 넣는다.
- `MapLoader.LoadPointData` — 사람만 스폰한다. 무기 포인트(종류 2, 7)와 소물 포인트(종류 5)는 건너뛴다. 추가 사물 초기화(`InitializeAddonObject`)도 아직 호출하지 않는다.
- `PlayerController` — 발사·재장전·무기 전환 입력과 치트 F6/F7/F9가 없다. 스코프(`zoom` 액션)도 없다.

## 아직 옮기지 않은 것 (단계별)

- **7단계**: `Effect/`(셰이더 `EffectBlend` 포함), `Sound/SoundManager.cs`, `Human/WorldSound.cs`, `Weapon/WeaponManager.cs`(드롭 풀), `Object/`(소물), `MissionStats.cs`
- **8단계**: `Human/AIBrain*.cs`, `AIController.cs`, `AIMoveNavi.cs`, `Event/EventManager.cs`
- **9단계**: 씬 UI. 기준은 UnityXOPS `origin/first-release(0.1)`, `origin/refactoring(0.2)` 브랜치의 `Runtime/Scene/`. 화면 로직은 GDScript로 쓰고 C# 창구 Autoload만 호출한다. `Camera/ScreenColorAdjust`(밝기·감마 셰이더)와 `Font/`도 이때 옮긴다.
- **옮기지 않기로 한 것**: `Runtime/Modding/` 전체와 Lua, `LetterboxController`(Godot 창 스케일로 대체), 자체 BMP/DDS/TGA/WAV 파서(Godot 내장 로더로 대체)

## 확인이 덜 된 것

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
