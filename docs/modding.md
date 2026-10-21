# 모딩 문서

GodotXOPS 는 원본 XOPS 에서 코드에 박혀 있던 수치를 `godotdata/` 폴더의 JSON 파일로 빼 두었습니다. 메모장으로 고치고 게임을 다시 켜면 반영됩니다. 프로그램을 다시 빌드할 필요는 없습니다.

- [기본 규칙](#기본-규칙)
- [파일 목록](#파일-목록)
- [무기](#무기)
- [사람](#사람)
- [오브젝트](#오브젝트)
- [이펙트](#이펙트)
- [미션과 스카이](#미션과-스카이)
- [확장 블록 데이터 (BD2)](#확장-블록-데이터-bd2)
- [확장 포인트 데이터 (PD2)](#확장-포인트-데이터-pd2)
- [확장 미션 데이터 (MIF2)](#확장-미션-데이터-mif2)
- [스크립트 이벤트](#스크립트-이벤트)
- [이벤트의 소리](#이벤트의-소리)
- [이미지 목록](#이미지-목록)
- [화면 스크립트](#화면-스크립트)
- [블록 재질](#블록-재질)
- [에드온 페이지와 맵팩](#에드온-페이지)
- [알려진 제한](#알려진-제한)

## 기본 규칙

- **고치기 전에 `godotdata/` 폴더를 통째로 복사해 두세요.** 되돌릴 때는 복사본을 다시 덮어쓰면 됩니다.
- JSON 은 게임을 시작할 때 한 번 읽습니다. 고친 뒤에는 게임을 껐다 켭니다.
- 파일에 없는 키는 기본값으로 채워집니다. 파일이 깨져 읽지 못하면 그 파일 전체가 기본값이 되므로, 고친 것이 전혀 반영되지 않으면 쉼표나 괄호를 빠뜨리지 않았는지 봅니다.
- 키 이름은 대소문자를 구분합니다.
- **목록에서의 순서가 곧 번호입니다** (0부터 셉니다). 다른 파일은 이 번호로 항목을 가리킵니다 (`modelIndex`, `bulletIndex`, `weaponIndex0` 등). 미션 파일(PD1)도 사람·무기·오브젝트을 이 번호로 가리키므로, **기존 항목의 순서를 바꾸거나 중간에 끼워 넣지 말고 맨 끝에 추가하세요.**
- 값을 가리키지 않을 때는 `-1`을 씁니다 (예: `scopeIndex`, `previousWeaponIndex`).
- 파일 경로는 `GodotXOPS.exe`가 있는 폴더 기준입니다 (`data/model/weapon/mp5.x`).

### 단위

| 종류 | 단위 |
|---|---|
| 길이, 위치 | 미터. 원본 XOPS 길이의 1/10 입니다 (원본 10 = 1 m) |
| 시간 | 초. 게임은 초당 33.333틱으로 돌고, 초 단위 값은 가장 가까운 틱 수로 반올림해 사용합니다 |
| 속도 | 미터/초 |
| 각도 | 도 |
| 색 | `sky_data.json`의 `skyColor`는 0 에서 255 사이의 정수, 그 밖(스코프 조준선, 설정의 조준선 색)은 0 에서 1 사이의 실수 |

위치와 회전 값의 축 방향은 UnityXOPS 와 같습니다 (+x 오른쪽, +y 위, +z 앞).

### 쓸 수 있는 파일 형식

- 모델: DirectX `.x` 파일만 됩니다.
- 텍스처: `.bmp`, `.dds`, `.tga`, `.png`, `.jpg` 등 Godot 이 읽을 수 있는 이미지.
- 소리: `.wav`.

## 파일 목록

| 파일 | 내용 |
|---|---|
| `config.json` | 게임 설정과 키 바인딩. 메뉴의 OPTION 에서 SAVE 하면 덮어씁니다. `General`의 `AllowConsole`은 OPTION 에 없는 설정이고, `"true"`로 바꾸면 어느 화면에서든 F11 로 디버그 콘솔이 열립니다 ([개발 문서](development.md#디버그-콘솔)) `AllowEventScript` 도 OPTION 에 없는 설정이고, `"false"` 로 바꾸면 [스크립트 이벤트](#스크립트-이벤트)를 쓰는 미션을 로드하지 않습니다. `AllowUiScript` 도 OPTION 에 없는 설정이고, `"false"` 로 바꾸면 [화면 스크립트](#화면-스크립트)를 쓰지 않습니다. `customBindings` 는 유저가 바꾼 [커스텀 바인딩](#커스텀-바인딩)의 키입니다 |
| `input/*.json` | 설치형 [커스텀 바인딩](#커스텀-바인딩). 기본은 없습니다 (폴더도 없습니다) |
| `global.json` | 제품 이름, 버전, 크레딧에 나오는 라이선스 문구 |
| `mission_data.json` | 오프닝·메뉴 배경 맵, 공식 미션 목록 |
| `sky_data.json` | 스카이 모델과 텍스처, 안개 |
| `effect_data.json` | 이펙트 텍스처와 프리셋 |
| `block_material_data.json` | 블록 면의 재질 (발소리, 총알이 맞았을 때의 이펙트와 소리). BD2 맵에서만 쓰입니다 |
| `sound_data.json` | [이벤트가 번호로 재생하는 소리](#이벤트의-소리)의 목록 |
| `image_data.json` | [이벤트와 화면 스크립트가 번호로 그리는 이미지](#이미지-목록)의 목록. 기본은 비어 있습니다 |
| `weather_data.json` | [날씨](#날씨)의 목록 (비, 눈, 안개, 번개). 0 번이 맑음입니다 |
| `weapon/list.json` | 무기 목록 (데미지, 발사 속도, 탄창, 반동 등) |
| `weapon/model.json` | 무기 모델, 총구 화염, 탄피, 팔 모양 |
| `weapon/bullet.json` | 탄환 종류 (직선 탄, 수류탄. 폭발, 착탄 소리와 이펙트) |
| `weapon/scope.json` | 스코프 종류 (시야각, 그림, 조준선) |
| `weapon/accuracy.json` | 이동·점프·부상에 따른 조준 오차 |
| `weapon/drop_physics.json` | 떨어진 무기의 낙하 |
| `weapon/general.json` | 맨손·수류탄·가방 무기 번호, 무기 모델 배율 |
| `human/list.json` | 사람 목록 (HP, 모델, AI 수준, 들고 나오는 무기, 종류) |
| `human/model.json` | 사람 몸통 모델과 텍스처, 팔·다리 모델 번호 |
| `human/arm.json`, `human/leg.json` | 팔, 다리 모델 |
| `human/animation.json` | 다리 동작 |
| `human/type.json` | 사람 종류 (이동 속도, 부위별 데미지 배수, 좀비 여부) |
| `human/ai.json` | AI 수준별 값과 시야·청각·회전 등 AI 공통 값 |
| `human/controller.json` | 중력, 낙하, 경사, 몸 크기 |
| `human/hitbox.json` | 머리·몸·다리 피격 판정 |
| `human/general.json` | 모델 배율과 높이, 팔 각도, 피격 시 조준 흐트러짐 |
| `human/interaction.json` | 무기 줍기 범위 |
| `object/list.json` | 오브젝트 목록 (체력, 부서지는 소리, 튀는 세기) |
| `object/model.json` | 오브젝트 모델 |
| `object/collider.json` | 오브젝트 판정 모양 |
| `event/base.json`, `event/base.sgd` | 기본 제공 [스크립트 이벤트](#스크립트-이벤트) 묶음의 등록 파일과 스크립트. 이 폴더에 다른 묶음의 `.json` 을 넣으면 함께 읽습니다 |
| `ui/*.json` | [화면 스크립트](#화면-스크립트)의 등록 파일. 기본으로는 비어 있고 `ui/samples/` 에 예제가 있습니다 |
| `object/general.json` | 오브젝트 배율, 총알 데미지 배수, 에드온 오브젝트 번호 |

## 무기

무기 하나는 `weapon/list.json`의 `weaponData` 항목 하나입니다. 모델은 `weapon/model.json`, 탄환은 `weapon/bullet.json`, 스코프는 `weapon/scope.json`의 항목을 번호로 가리킵니다.

무기의 동작은 전부 데이터로 정해집니다. 코드에 "몇 번 무기는 이렇게 한다"는 분기가 없으므로, 아래 값들을 조합해 새 무기를 만들 수 있습니다.

### `weapon/list.json`

| 키 | 뜻 |
|---|---|
| `name` | 화면에 나오는 이름 |
| `modelIndex` | `weapon/model.json`의 번호 |
| `bulletIndex` | `weapon/bullet.json`의 번호 |
| `damage` | 한 발의 데미지 (부위별 배수는 사람 종류에서 곱합니다) |
| `penetration` | 관통력. 사람을 맞힐 때마다 1 씩 줄고(벽은 두께에 따라 더 줄어듭니다), 0 보다 작아지면 탄이 사라집니다 (0 이면 첫 사람에서 멈추고, 1 이면 한 명을 뚫고 나갑니다). 방어구나 헬멧이 있는 부위를 맞히면 1 을 더 씁니다 |
| `armorPointDecay` | 맞은 사람의 방어구 포인트를 데미지의 몇 배만큼 깎는지 (0 에서 1 사이. 1 이면 데미지만큼, 0 이면 깎지 못합니다). 적지 않으면 1 입니다 |
| `helmetPointDecay` | 맞은 사람의 헬멧 포인트를 데미지의 몇 배만큼 깎는지. 뜻은 `armorPointDecay` 와 같습니다 |
| `armorIgnore` | 맞은 사람의 방어구가 데미지를 줄이는 것을 얼마나 무시하는지 (0 에서 1 사이. 0 이면 무시하지 않고, 1 이면 방어구가 없는 것처럼 전부 들어갑니다). 적지 않으면 0 입니다 |
| `helmetIgnore` | 맞은 사람의 헬멧이 데미지를 줄이는 것을 얼마나 무시하는지. 뜻은 `armorIgnore` 와 같습니다 |
| `consumeAmmo` | 쏠 때 탄을 쓰는지. `false` 면 탄창이 줄지 않고, 탄이 없어도 쏘며, 재장전하지 않습니다. AI 도 탄이 없다고 버리거나 바꾸지 않습니다. 적지 않으면 `true` 입니다 |
| `ammoGroup` | 탄의 묶음 번호. 종류 전환(`previousWeaponIndex` / `nextWeaponIndex`)으로 이어진 두 무기의 번호가 같으면 탄을 그대로 넘기고, 다르면 넘기지 않고 묶음마다 따로 둡니다 (아래 "탄을 따로 쓰는 종류 전환"). 적지 않으면 0 입니다 |
| `initialReserve` | 이 무기를 새로 채울 때의 예비 탄 수 (탄창은 늘 장탄수만큼 가득입니다). -1 이면 원본대로 장탄수 × (사람 종류의 `autoBulletMultiplier` − 1) 입니다. 0 이상이면 누가 들어도 그 값입니다. 듣는 곳은 사람이 처음부터 들고 나올 때, 종류 전환으로 다른 탄 묶음을 처음 꺼낼 때, 이벤트나 치트가 탄 수 없이 무기를 줄 때입니다. 맵에 놓인 무기(포인트)는 포인트에 적은 탄 수를 따릅니다. 적지 않으면 -1 입니다 |
| `aiMaxRange` | AI 가 이 무기를 쓰는 가장 먼 거리 (m). 적이 이보다 멀면 쏘지 않고 달려서 다가갑니다. 0 이면 제한이 없습니다 |
| `aiMinRange` | AI 가 이 무기를 쓰는 가장 가까운 거리 (m). 적이 이보다 가까우면 쏘지 않고 물러납니다 (뒤가 막혔거나 낭떠러지면 서 있습니다). 0 이면 제한이 없습니다 |
| `aiPriority` | AI 가 종류 전환으로 이어진 종류들 가운데 무엇을 들지 고르는 순서 (정수, 음수도 됩니다). 아래 "AI 가 종류를 고르는 순서". 적지 않으면 0 입니다 |
| `aiBallisticAim` | 중력을 받는 탄을 AI 가 궤적을 계산해서 겨누는지. 아래 "AI 의 궤적 조준". 적지 않으면 `false` 입니다 |
| `fireRate` | 초당 발사 수. 0 이면 쏠 수 없습니다 |
| `bulletSpeed` | 탄속 (m/s) |
| `magazineSize` | 탄창 크기 |
| `pelletCount` | 한 번에 나가는 탄 수 (산탄총) |
| `burstMode` | 0 연발, 1 단발, 2 점사 |
| `burstCount` | `burstMode`가 2 일 때, 발사 키를 한 번 누르고 있는 동안 나가는 최대 발 수 |
| `reloadStyle` | 0 남은 탄을 버리고 재장전, 1 남은 탄을 유지하고 재장전, 2 한 발씩 장전, 3 자동 재장전. 아래 설명 |
| `reloadTime` | 재장전 시간 (초). 한 발씩 장전에서는 빈 탄창을 다 채우는 시간입니다 |
| `recoil` | 쏠 때마다 늘어나는 조준 오차 |
| `armReactionAngle` | 쏠 때 팔이 들리는 각도 |
| `recoilAimVertical`, `recoilAimHorizontal` | 쏠 때 시점이 튀는 범위 (`min` ~ `max`) |
| `errorRange` | 조준 오차의 최소·최대 |
| `ignoreAimError` | true 면 조준 오차를 무시하고 정확히 나갑니다 |
| `crosshair` | 조준선 표시 |
| `scope`, `scopeIndex` | 스코프 사용 여부와 `weapon/scope.json`의 번호 |
| `position`, `size` | 손에 쥔 무기 모델의 기준 위치와 크기 |
| `soundPath`, `soundVolume` | 발사음 경로, 발사음 볼륨 |
| `suppressor` | 소음기. AI 가 총성을 듣는 거리가 짧아집니다 |
| `previousWeaponIndex`, `nextWeaponIndex` | Z / X 키로 바뀌는 무기 번호 (단발 ↔ 연발처럼 다른 항목으로 바꿉니다). 없으면 -1 |
| `switchTime` | Z / X 로 바꾸는 데 걸리는 시간 |
| `slotChangeTime` | 1 / 2 슬롯을 바꾸는 데 걸리는 시간 |
| `discardAfterAutoReloadIfNoAmmo` | 탄을 다 쓰면 무기를 버립니다 (수류탄) |

#### 탄을 따로 쓰는 종류 전환

원본의 종류 전환(기본 키 Z / X)은 단발 ↔ 연발처럼 같은 탄을 쓰는 무기 사이를 오가는 것이라 탄을 그대로 넘깁니다. 총에 달린 유탄 발사기처럼 **탄이 다른** 두 종류를 오가게 하려면 `ammoGroup` 을 다르게 적습니다.

- 소총은 `ammoGroup` 0, 유탄 발사기는 1 로 두고 서로를 `nextWeaponIndex` / `previousWeaponIndex` 로 가리키게 합니다.
- 전환하면 지금 탄(예: 30/60)을 접어 두고 그 묶음의 탄(예: 1/2)을 꺼냅니다. 돌아오면 접어 둔 탄이 그대로 나옵니다.
- 어떤 묶음을 처음 꺼낼 때는 그 종류의 무기를 새로 받은 것처럼 채웁니다 (장탄수, 예비 탄은 장탄수 × (사람 종류의 `autoBulletMultiplier` − 1)). 처음 꺼내는 사람의 배수를 따르므로, 유탄처럼 수를 정해 두고 싶으면 그 종류에 `initialReserve` 를 적습니다 (예: 유탄 발사기에 `magazineSize` 1, `initialReserve` 2 면 누가 들어도 1 + 2 발).
- 맵에 놓인 무기의 탄 수(포인트의 값)는 포인트에 적은 종류의 탄입니다. 다른 묶음의 탄은 위의 규칙으로 처음 꺼낼 때 채워집니다.
- 묶음은 셋 이상이어도 되고, 같은 번호끼리는 지금처럼 탄을 넘깁니다 (소총의 단발 ↔ 연발은 둘 다 0).
- 접어 둔 탄은 그 무기 한 자루의 것입니다. 버렸다 주워도, 죽은 사람이 떨어뜨린 것을 주워도 그대로입니다 (다시 차지 않습니다).
- HUD 는 지금 든 종류의 탄만 보여 줍니다.
- AI 는 지금 든 종류의 탄이 다 떨어지면, 전환으로 갈 수 있는 다른 종류에 탄이 남아 있는지 보고 그쪽으로 전환합니다. 어느 묶음에도 탄이 없을 때만 무기를 버립니다.

#### AI 가 종류를 고르는 순서

종류 전환으로 이어진 무기들(소총과 거기 달린 유탄 발사기 등)에서 AI 가 무엇을 들지는 `aiPriority` 와 쓰는 거리(`aiMinRange`, `aiMaxRange`)로 정합니다. 이 값들을 하나도 적지 않은 무기(기본 데이터 전부)는 원본의 규칙(근거리에서 연발, 원거리에서 단발) 그대로입니다.

- 전투 중에 AI 는 **탄이 있고 적까지의 거리에서 쓸 수 있는 종류 가운데 `aiPriority` 가 가장 큰 것**을 듭니다. 값이 같으면 지금 든 종류를 그대로 듭니다.
- **"다른 슬롯의 무기로 바꾸기"가 0 입니다.** 0 이상인 종류에 탄이 남아 있는 동안은 이 무기를 들고, 음수인 종류의 탄만 남았으면 다른 슬롯에 쓸 무기가 있는 한 그 슬롯으로 넘어갑니다. 음수인 종류는 다른 슬롯에도 쓸 무기가 없을 때에야 씁니다 (AI 가 아껴 쓰게 하는 값입니다).
- 예: 유탄 발사기에 `aiPriority` 1, `aiMinRange` 6, `aiMaxRange` 40 을 적으면, AI 는 6 에서 40 m 의 적에게 유탄부터 쏘고, 유탄이 떨어지거나 적이 6 m 안으로 들어오면 소총으로 돌아옵니다. −1 을 적으면 소총과 다른 슬롯의 무기를 다 쓴 뒤에야 유탄을 씁니다.
- 0 이상인 종류에 탄은 있는데 전부 거리 밖이면 바꾸지 않고, 다가가거나 물러납니다.
- 전투 중이 아닐 때는 고르지 않습니다. 탄이 떨어졌을 때 탄이 남은 종류로 넘어가는 것만 합니다.
- 다른 슬롯의 무기로 넘어간 뒤에는 그 무기를 다 쓸 때까지 돌아오지 않습니다.

#### AI 의 궤적 조준

원본의 AI 는 기본 수류탄 하나만 "거리에 비례해 조금 위로" 겨누고, 그 밖의 무기는 중력을 받는 탄이어도 직선으로 겨눕니다. `aiBallisticAim` 을 `true` 로 적은 무기는 AI 가 탄이 날아가는 궤적(탄속, 탄환의 `gravityScale`, 감쇠, `lifetime`)을 계산해서 적에게 떨어지는 각도로 겨눕니다.

- 탄환의 `useGravity` 가 켜진 무기에만 듣습니다.
- 낮은 궤적과 높은 궤적 가운데 낮은 쪽을 씁니다. 블록과 튕김은 계산하지 않습니다 (천장이나 벽에 걸릴 수 있습니다).
- 닿지 않는 거리면 가장 멀리 가는 각도로 겨눕니다. 쏘지 않게 하려면 `aiMaxRange` 를 함께 적습니다.
- 겨누는 곳만 정확해질 뿐, 쏘는 확률과 조준 오차는 AI 수준(`human/ai.json`)대로입니다. 적의 움직임은 앞질러 겨누지 않습니다.
- 기본 수류탄은 꺼져 있어 원본처럼 던집니다.

#### 근접 무기 만들기

원본에 없는 기능이고, 위 값들을 적지 않은 무기(기본 데이터 전부)는 전과 같습니다. 근접 무기는 따로 있는 종류가 아니라 **아주 짧게 나가는 탄**을 쏘는 무기입니다.

- **닿는 거리**는 탄환의 `lifetime` 과 무기의 `bulletSpeed` 가 정합니다. 한 틱에 가는 거리를 d = `bulletSpeed` × 0.03 이라 하면, 닿는 거리 = (`lifetime` 을 틱으로 바꾼 수 + 1) × d + (d 를 0.25 m 단위로 반올림한 값) 입니다. `lifetime` 0 일 때 `bulletSpeed` 30 이면 1.9 m, 20 이면 1.1 m, 10 이면 0.55 m 입니다. 여기에 맞는 사람의 히트박스 반지름(몸통 0.24 m)만큼 더 닿습니다. **`bulletSpeed` 가 4.2 보다 작으면 판정 점이 하나도 없어서 아무것도 맞지 않습니다.** 가장 짧게 만들 수 있는 거리는 약 0.4 m 입니다.
- `consumeAmmo` 를 `false` 로 두면 탄이 필요 없습니다. `magazineSize` 는 0 이어도 됩니다.
- `aiMaxRange` 를 닿는 거리보다 조금 짧게 적으면 AI 가 그 거리까지 다가와서 씁니다. 적지 않으면 AI 는 총처럼 멀리서 휘두릅니다.
- `penetration` 을 0 으로 두면 맨 앞의 한 사람만 맞고, `ignoreAimError` 를 켜면 늘 정면으로 나갑니다.
- 방어구를 깎지 않으면서 그대로 뚫게 하려면 `armorPointDecay` 0, `armorIgnore` 1 (헬멧도 같은 식)로 둡니다.
- 탄환 모델을 감추려면 탄환의 `bulletBoundAdjust` 를 닿는 거리보다 크게 적습니다 (총구에서 그만큼 멀어져야 보입니다).
- 총알 판정을 그대로 쓰므로 조준선 위의 사람만 맞습니다 (휘두르는 범위 판정이 아닙니다). 한 틱의 경로보다 얇은 벽은 총알처럼 지나갑니다. HUD 의 탄 수는 0 으로 나옵니다.

AI 가 쓰는 거리(`aiMaxRange`, `aiMinRange`)는 근접 무기가 아니어도 됩니다. 가까이에서만 쏘는 산탄총이나, 가까우면 쏘지 않는 폭발물에 쓸 수 있습니다. 좀비 종류의 사람은 전처럼 자기 방식으로 다가가 치고 이 값을 보지 않습니다: 탄을 쏘지 않고 데미지를 직접 주며(방어구를 거치지 않습니다), 멈춰 서는 거리는 그 무기의 탄이 닿는 거리입니다. 같은 근접 무기는 좀비가 들어도 같은 거리에서 닿습니다.

### `weapon/model.json`

| 키 | 뜻 |
|---|---|
| `textures` | 텍스처 경로 목록 |
| `modelData` | 모델 조각 목록. 조각마다 `modelPath`, `textureIndex`(위 목록의 번호), `position`, `rotation`, `scale` |
| `muzzleFlashEffectIndex`, `muzzleFlashOffset`, `muzzleFlashSize` | 총구 화염 이펙트 번호, 위치, 크기 |
| `gunfireSmokeEffectIndex` | 발사 연기 이펙트 번호 |
| `shellEffectIndex`, `shellEjectOffset`, `shellEjectDirection`, `shellEjectSpeed`, `shellSize` | 탄피 이펙트 번호, 나오는 위치·방향·속도·크기. 위치와 방향은 무기 기준입니다 (아래로 떨어뜨리려면 방향의 `y` 를 음수로). 크기가 0 이면 탄피가 나오지 않습니다 |
| `shellEjectMode` | 탄피가 나오는 때 (1.2.1). 0 쏠 때마다 하나, 1 재장전할 때 한꺼번에, 2 나오지 않음. 아래 설명 |
| `shellEjectDelay` | 쏜 뒤 탄피가 나올 때까지의 시간 (초). `shellEjectMode` 가 0 일 때만 씁니다. 펌프액션이나 볼트액션처럼 쏘고 조금 뒤에 탄피가 나오는 무기에 씁니다. 발사 간격보다 길어도 쏜 만큼 전부 나옵니다 |
| `leftArmIndex`, `rightArmIndex` | 이 무기를 들었을 때의 왼팔·오른팔 모양 번호 (`human/arm.json`의 `leftArms`, `rightArms` 안에서의 순서). -1 이면 그 팔을 그리지 않습니다 |
| `fixLeftArm`, `fixedLeftArmAngle`, `fixRightArm`, `fixedRightArmAngle` | 팔을 조준 방향과 무관하게 고정할지와 그 각도 |

모델 조각을 여러 개 넣으면 `.x` 파일 여러 개를 조립해 무기 하나로 만들 수 있습니다.

**재장전 방식 (`reloadStyle`)**

| 값 | 방식 | 동작 |
|---|---|---|
| 0 | 버리고 재장전 | 재장전 시간이 지나면 탄창에 남은 탄을 버리고 예비 탄에서 새로 채웁니다 (원본의 방식) |
| 1 | 남기고 재장전 | 재장전 시간이 지나면 모자란 만큼만 채웁니다 |
| 2 | 한 발씩 장전 | `reloadTime` ÷ `magazineSize` 초마다 한 발씩 들어갑니다. 탄창이 차거나 예비 탄이 떨어지면 끝납니다. **장전하는 도중에 발사 키를 누르면 장전이 거기서 끊깁니다** (그때까지 넣은 탄은 남습니다). 그 입력으로는 쏘지 않고, 발사 키를 뗐다가 다시 눌러야 나갑니다 (연발 무기를 누른 채로 있어도 장전만 끊깁니다). 탄창이 비어 있는 동안에는 끊기지 않습니다. AI 는 끊지 않고 끝까지 장전합니다 |
| 3 | 자동 재장전 | 탄창의 마지막 발을 쏘는 순간 예비 탄에서 바로 채웁니다 (수류탄). **재장전 키로는 재장전할 수 없습니다.** 예비 탄까지 다 쓰면 `discardAfterAutoReloadIfNoAmmo` 가 참일 때 무기가 사라지고, 거짓이면 빈 채로 남습니다 |

**탄피가 나오는 때 (`shellEjectMode`)**

| 값 | 때 | 동작 |
|---|---|---|
| 0 | 쏠 때 | 한 발 쏠 때마다 `shellEjectDelay` 초 뒤에 하나 나옵니다 (기본 데이터의 모든 무기) |
| 1 | 재장전할 때 | 쏠 때는 나오지 않고, 재장전을 시작하는 순간에 한꺼번에 나옵니다 (리볼버). 개수는 재장전 방식에 따라 다릅니다: 남은 탄을 두는 방식(1, 2)은 지난 재장전 뒤에 쏜 만큼, 그 밖의 방식(0, 3)은 장탄수만큼. 자동 재장전(3)은 자동으로 채워지는 발사에서 나옵니다 |
| 2 | 없음 | 탄피가 나오지 않습니다 |

탄피의 속도는 무기 모델의 `shellEjectDirection` × `shellEjectSpeed` 에 탄피 이펙트(`shellEffectIndex` 가 가리키는 [이펙트](#이펙트))의 `velocity` 와 `velocityRandomRange` 를 더한 것입니다. 무기 모델이 "어느 쪽으로 얼마나 빨리"를, 이펙트가 "얼마나 흩어지는지"를 정합니다. 기본 데이터의 탄피 이펙트는 위아래로만 조금 흩어지므로(`velocityRandomRange` 의 `y` 0.36), 한꺼번에 나오는 탄피를 옆으로도 퍼지게 하려면 `x` 와 `z` 에도 값을 줍니다. 다른 무기의 탄피까지 바뀌지 않게 하려면 탄피 이펙트를 하나 더 만들어 그 무기 모델만 가리키게 합니다.

### `weapon/bullet.json`

| 키 | 뜻 |
|---|---|
| `modelPath`, `texturePath`, `modelPosition`, `modelRotation`, `modelScale` | 탄환 모델 |
| `bulletBoundAdjust` | 탄환 모델이 총구에서 이 거리만큼 멀어진 뒤부터 보입니다 (판정과 무관한 연출) |
| `useGravity`, `gravityScale` | 중력을 받는 탄 (수류탄) |
| `explosionTrigger` | 폭발 조건. 아래 값을 더해서 씁니다: 1 탄환 사라짐, 2 맵에 충돌, 4 사람에 충돌, 8 오브젝트에 충돌. 0 이면 폭발하지 않습니다 |
| `armingDelay` | 발사 뒤 이 시간이 지나야 폭발할 수 있습니다 |
| `explosionRadius` | 폭발 반경 (m) |
| `humanExplosiveHeadDamageMax`, `humanExplosiveLegDamageMax`, `objectExplosiveDamageMax` | 폭발 중심에서의 최대 데미지 |
| `explosionknockbackMax` | 폭풍에 밀리는 최대 세기 |
| `explosionSound`, `explosionEffectIndex` | 폭발 소리와 이펙트 |
| `humanHitEffectIndex`, `objectHitEffectIndex` | 사람, 오브젝트에 맞았을 때 내는 이펙트 번호 |
| `humanHitSounds`, `bulletPassingSounds` | 피격음, 스쳐 지나가는 소리. 여러 개를 넣으면 그중 하나를 무작위로 냅니다 |
| `bounceSounds` | 중력을 받는 탄(수류탄)이 벽이나 바닥에 튕길 때의 소리. 여러 개를 넣으면 그중 하나를 무작위로 냅니다. 직선 탄에서는 쓰이지 않습니다. AI 는 이 소리도, 날아가는 수류탄도 듣지 못합니다 (폭발만 듣습니다) |
| `bulletHoleSize` | 재질이 남기는 탄흔(`bulletHoleEffect`)의 크기 배율. 1 이 프리셋 그대로입니다 |
| `lifetime` | 탄환이 사라질 때까지의 시간 (초) |

예: 로켓은 `explosionTrigger`를 15(1+2+4+8)로, 수류탄은 1 로 둡니다.

탄환이 벽에 맞았을 때의 이펙트와 소리는 탄환이 아니라 맞은 면의 [재질](#블록-재질)이 정합니다. 1.0 의 `wallHitEffectIndex`와 `wallHitSounds`는 없어졌습니다: 착탄 연기와 착탄음은 0번 재질로 옮겨졌고, 수류탄이 튕기는 소리는 `bounceSounds`가 됐습니다.

### `weapon/general.json`

| 키 | 뜻 |
|---|---|
| `noneWeaponIndex` | 맨손으로 취급하는 무기 번호 |
| `grenadeWeaponIndex` | 수류탄으로 취급하는 무기 번호 (AI 가 던지는 방식이 달라집니다) |
| `caseWeaponIndex` | 가방으로 취급하는 무기 번호 목록 (미션 이벤트의 "가방 소지" 조건, AI 가 버리지 않음) |
| `weaponScale` | 무기 모델 배율 |

### `weapon/scope.json`

`scopeData` 항목 하나가 스코프 한 종류입니다. 무기의 `scopeIndex` 가 이 목록의 번호를 가리킵니다.

| 키 | 뜻 |
|---|---|
| `fovDegrees` | 스코프를 쓰는 동안의 시야각 (도). 0 이면 평소의 시야각 그대로입니다 |
| `recoilAimVerticalAdjust`, `recoilAimHorizontalAdjust` | 스코프를 쓰는 동안 한 발 쏠 때마다 시점이 튀는 범위 (`min`, `max`, 도. 위와 오른쪽이 +). 그 사이에서 0.1 도 단위로 무작위로 뽑습니다. 무기 자체의 반동에 더해집니다 |
| `texturePath` | 스코프를 쓰는 동안 화면을 덮는 그림. 비워 두면 그림이 없습니다 |
| `textureAspect` | 그 그림의 가로 ÷ 세로. 이 비율을 지킨 채 화면에 가장 크게 넣고, 남는 바깥은 검게 가립니다. 0 이면 4:3 으로 칩니다 |
| `hideCrosshair` | `true` 면 스코프를 쓰는 동안 무기의 조준선을 그리지 않습니다 |
| `lines` | 스코프의 조준선. 선마다 `start`, `end`(화면 가운데에서의 위치), `color`(0 에서 1 사이), `width` |
| `errorRangeAdjust` | 쓰이지 않습니다 |

AI 가 스코프 무기를 다룰 때의 값은 `human/ai.json` 의 `aiScopeData` 에 따로 있습니다 ([AI](#humanaijson--ai)).

### `weapon/accuracy.json`

모든 무기에 함께 적용되는 조준 오차입니다. 값은 무기의 `errorRange` 와 같은 눈금이고, 클수록 탄이 퍼집니다.

| 키 | 뜻 |
|---|---|
| `walkAccuracyPenalty`, `forwardAccuracyPenalty`, `backAccuracyPenalty`, `strafeAccuracyPenalty` | 걷는 중, 앞으로·뒤로·옆으로 달리는 중의 오차. 여러 개가 겹치면 하나만 적용됩니다 (옆 > 뒤 > 앞 > 걷기 순으로 우선) |
| `airborneAccuracyPenalty` | 공중에 있을 때의 오차. 위의 것들을 대신합니다 |
| `injuryHpThreshold`, `injuryAccuracyPenalty` | 체력이 이 값보다 낮으면 위의 오차에 더하는 값 |
| `reactionRecoveryPerSecond` | 쏘거나 맞아서 생긴 흐트러짐이 초당 줄어드는 양 |

### `weapon/drop_physics.json`

떨어진 무기(버린 것, 죽은 사람이 놓친 것)의 움직임입니다. 들고 있는 무기와는 무관합니다.

| 키 | 뜻 |
|---|---|
| `gravity` | 아래로 붙는 가속도 (m/s²) |
| `terminalVelocityY` | 떨어지는 속도의 한계 (음수, m/s) |
| `dropoffHorizontalSpeed` | 버릴 때 앞으로 던지는 속도 (m/s) |
| `horizontalDampingPerSec` | 1초 뒤에 남는 수평 속도의 비율 (0 에서 1 사이. 작을수록 빨리 멈춥니다) |
| `horizontalStopThreshold` | 수평 속도가 이 값보다 작아지면 멈춥니다 (m/s) |
| `groundCollisionMargin` | 바닥이나 벽에서 띄워 두는 거리 (m) |
| `deadlineY` | 이 높이 아래로는 떨어지지 않고 멈춥니다 (맵 밖으로 떨어진 무기) |

## 사람

사람 하나는 `human/list.json`의 `humanData` 항목 하나입니다.

| 키 | 뜻 |
|---|---|
| `name` | 이름 (구분용) |
| `hp` | 체력 |
| `armor` | 방어구 포인트. 0 이면 방어구가 없습니다 (기본 데이터는 전부 0 입니다) |
| `armorDamageDecrease` | 방어구가 남아 있는 동안 몸통과 다리에 받는 총알 데미지를 줄이는 비율 (0 에서 1 사이. 0 이면 줄이지 않고 1 이면 받지 않습니다) |
| `helmet` | 헬멧 포인트. 0 이면 헬멧이 없습니다 |
| `helmetDamageDecrease` | 헬멧이 남아 있는 동안 머리에 받는 총알 데미지를 줄이는 비율. 뜻은 `armorDamageDecrease` 와 같습니다 |
| `modelIndex` | `human/model.json`의 번호 |
| `aiIndex` | `human/ai.json`의 `aiData` 번호 (AI 수준) |
| `weaponIndex0`, `weaponIndex1` | 처음 들고 있는 무기 번호 (슬롯 1, 2) |
| `typeIndex` | `human/type.json`의 번호 (사람 종류) |

#### 방어구와 헬멧

원본에 없는 기능이고, 포인트가 0 인 사람(기본 데이터 전부)은 전과 똑같이 맞습니다.

- 방어구는 몸통과 다리를, 헬멧은 머리를 맡습니다. 총알에만 듣고 폭발, 좀비의 공격, 낙하, 이벤트가 주는 데미지는 그대로 받습니다.
- 맞은 부위의 포인트가 0 보다 크면 그 한 발의 데미지(부위 배수와 난수를 더한 값)에 (1 − 줄이는 비율)을 곱해서 받습니다. 소수점 아래는 버립니다.
- 쏜 무기가 방어구를 무시하면 그만큼 덜 줄입니다: 실제로 줄이는 비율 = `armorDamageDecrease` × (1 − 무기의 `armorIgnore`). 머리는 `helmetDamageDecrease` 와 `helmetIgnore` 입니다. 방어구를 깎지 않으면서 그대로 뚫는 무기(근접 무기 등)는 `armorPointDecay` 0, `armorIgnore` 1 로 만듭니다. 무시하는 무기에도 아래의 관통력 규칙은 그대로입니다.
- 포인트는 **줄이기 전의 데미지** × 쏜 무기의 `armorPointDecay`(머리는 `helmetPointDecay`)만큼 깎입니다. 0 이하가 되면 부서진 것이고 다음 발부터는 그대로 맞습니다. 포인트가 조금만 남아 있어도 부서지는 그 한 발까지는 줄여서 받습니다.
- 방어구나 헬멧은 총알이 사람을 **뚫고 나가는 것**만 막습니다. 사람은 늘 맞고, 그 뒤로 나가는 데 관통력이 하나 더 듭니다: 방어구 입은 사람을 뚫으려면 관통력이 2 이상이어야 합니다.
- 무적인 사람은 포인트도 깎이지 않습니다. 미션을 다시 시작하면 처음 값으로 돌아옵니다.
- 기본 HUD 는 방어구와 헬멧을 그리지 않습니다. 화면 스크립트가 `armor`, `helmet` 값으로 그릴 수 있습니다 ([화면 스크립트](#화면-스크립트)).

### `human/type.json` — 사람 종류

기본으로 세 종류가 들어 있고, 마지막이 좀비입니다.

| 키 | 뜻 |
|---|---|
| `progressRunAcceleration`, `sidewaysRunAcceleration`, `regressRunAcceleration`, `progressWalkAcceleration` | 전진·옆·후진 달리기와 걷기의 가속도 |
| `attenuation` | 속도 감쇠 (클수록 빨리 멈춥니다) |
| `jumpSpeed` | 점프 속도 |
| `headDamageMultiplier`, `bodyDamageMultiplier`, `legDamageMultiplier` | 부위별 데미지 배수 |
| `headRandomAddDamage`, `bodyRandomAddDamage`, `legRandomAddDamage` | 부위별로 무작위로 더해지는 데미지 범위 |
| `maxFallDamage` | 낙하 데미지 최댓값 |
| `bloodEffectIndex`, `bloodEffectThreshold`, `hitEffectIndex`, `deathEffectIndex`, `bloodAttachesToMap` | 피격·사망 이펙트 |
| `canPickupWeapon` | 무기를 주울 수 있는지 |
| `zombie` | 좀비 (근접 공격만 합니다) |
| `zombieMeleeDamageRange`, `zombieMaxMeleeRange`, `zombieAttackSound` | 좀비의 근접 데미지, 무기를 든 좀비가 닿는 거리의 상한, 공격음. 무기를 든 좀비는 그 무기의 탄이 닿는 거리([근접 무기 만들기](#근접-무기-만들기)의 식)와 이 상한 중 짧은 쪽에서 멈춰 서서 칩니다 |
| `autoBulletMultiplier` | 처음 갖는 탄의 총량 (탄창 크기 × 이 값. 장전된 한 탄창을 포함합니다) |
| `regeneration` | 체력 재생을 하는지 (기본 데이터는 전부 `false`) |
| `regenerationDelay` | 체력이 마지막으로 줄어든 뒤 재생이 시작되기까지의 시간 (초) |
| `regenerationRate` | 재생 속도 (초당 체력) |
| `regenerationMaxRatio` | 재생으로 채워지는 상한. 그 사람의 처음 체력(`human/list.json` 의 `hp`)에 대한 비율 (0 에서 1 사이. 0.5 면 절반까지) |
| `controllerSizeIndex`, `hitboxSizeIndex` | 몸 크기(`controller.json`)와 피격 판정(`hitbox.json`)의 번호 |

#### 체력 재생

원본에 없는 기능이고, `regeneration` 이 `false` 인 종류(기본 데이터 전부)는 전과 같습니다.

- 체력이 줄어들면 `regenerationDelay` 초를 처음부터 다시 셉니다. 무엇으로 줄었는지는 가리지 않습니다 (총알, 폭발, 좀비의 공격, 낙하, 이벤트의 Damage Human / Set HP / Add HP 의 빼기). 무적이라 체력이 줄지 않았으면 다시 세지 않습니다.
- 그 시간이 지나면 초당 `regenerationRate` 씩 채웁니다. 체력은 1 단위로 늘어납니다.
- 상한은 처음 체력 × `regenerationMaxRatio` 이고 소수점 아래는 버립니다. 이미 상한 이상인 체력(이벤트로 더 채운 경우)은 건드리지 않습니다.
- 죽은 사람과 체력이 0 인 사람은 재생하지 않습니다. 방어구와 헬멧은 재생하지 않습니다.
- 이벤트의 게임 정지(Pause World) 중에는 멈춥니다.

### `human/ai.json` — AI

`aiData`는 AI 수준 목록입니다.

| 키 | 뜻 |
|---|---|
| `aiming` | 조준 보정 빈도. 클수록 자주 고쳐 겨눕니다 |
| `attack` | 발사 확률. **작을수록 자주 쏩니다** |
| `search` | 탐색 능력. 클수록 멀리, 꼼꼼히 봅니다 |
| `limitsError` | 발사를 허용하는 각도 보정. 음수면 더 정확히 겨눈 뒤에 쏩니다 |

`aiScopeData` 는 스코프 종류마다의 AI 값입니다. **0번은 스코프가 없는 무기와 맨손**이고, 스코프 무기는 `scopeIndex` + 1 번을 씁니다 (에드온 스코프는 같은 번호의 에드온 항목을 씁니다).

| 키 | 뜻 |
|---|---|
| `aiShotAngle`, `aiShotAngleLong` | 근거리와 원거리에서, 겨눈 방향이 적에게서 이 각도(도) 안에 들어와야 쏩니다. AI 수준의 `limitsError` 가 여기에 더해집니다 |
| `aiAddSearchDistNormal`, `aiAddSearchDistCaution` | 평소와 경계 중의 발견 거리에 더하는 값 (m) |

그 밖의 `ai*` 값은 모든 AI 가 함께 쓰는 값입니다.

| 키 | 뜻 |
|---|---|
| `aiSearchFovHNear`, `aiSearchFovVNear` | 근거리의 시야각, 가로와 세로 (도. 전체 각이고 좌우·상하로 절반씩입니다) |
| `aiSearchFovHLong`, `aiSearchFovVLong` | 원거리의 시야각 |
| `aiSearchFovHNearCaution`, `aiSearchFovVNearCaution`, `aiSearchFovHLongCaution`, `aiSearchFovVLongCaution` | 경계 중일 때의 같은 값 |
| `aiSearchDistBaseNormal`, `aiSearchDistCoeffNormal` | 평소의 발견 거리 = Base + Coeff × (AI 수준의 `search` − 2) + 스코프의 가산 (m) |
| `aiSearchDistBaseCaution`, `aiSearchDistCoeffCaution` | 경계 중의 발견 거리 |
| `aiShortAttackDist` | 근거리와 원거리를 가르는 거리 (m) |
| `aiSearchLoopScale` | 한 틱에 살펴보는 사람 수 = AI 수준의 `search` × 이 값 |
| `aiSearchPoolSize` | 살펴볼 사람을 뽑는 범위의 크기. 맵의 사람 수보다 크면 빈 자리를 뽑는 일이 생겨 발견이 그만큼 늦어집니다 (원본의 반응 지연이 여기서 나옵니다) |
| `aiTurnRateDeg`, `aiTurnDamping`, `aiTurnDeadzoneDeg` | 조준을 돌리는 가속(틱당 도), 틱마다 남는 회전 속도의 비율, 이보다 느린 회전은 멈춤 |
| `aiTurnMaxPitchDeg` | 위아래로 겨눌 수 있는 한계 (도) |
| `aiActionCancelDist` | 적이 이보다 멀어지면 전투를 그만둡니다 (m) |
| `aiCombatRetreatDist` | 전투 중 적이 이보다 가까우면 앞으로 움직이지 않습니다 (m) |
| `aiCautionFrames` | 경계를 유지하는 시간 (틱) |
| `aiZombieArmAngle` | 좀비가 공격할 때 드는 팔의 각도 (도. 0 이 수평, 음수가 아래) |
| `aiHearGunfireDist`, `aiHearGunfireSilencerDist`, `aiHearGunfireAllyDist` | 적의 총소리, 소음기를 단 적의 총소리, 아군의 총소리를 듣는 거리 (m) |
| `aiHearBulletDist` | 머리 옆을 스쳐 가는 총알을 듣는 거리. 수류탄처럼 중력을 받는 탄은 듣지 못합니다 |
| `aiHearBulletWallHitDist`, `aiHearExplosionDist` | 총알이 벽에 맞는 소리, 폭발을 듣는 거리 |
| `aiHearHitHumanHead`, `aiHearHitHumanBody`, `aiHearHitHumanLeg`, `aiHearHitHumanZombie`, `aiHearHitSmallObject` | 총알이 사람의 머리·몸·다리에 맞는 소리, 좀비의 공격이 맞는 소리, 총알이 오브젝트에 맞는 소리를 듣는 거리 |
| `aiHearFootstepForward`, `aiHearFootstepSide`, `aiHearFootstepBack` | 다른 팀이 앞으로·옆으로·뒤로 달리는 발소리를 듣는 거리. 걷는 소리와 같은 팀의 발소리는 듣지 못합니다 |
| `aiArrivalDistPath` | 경로의 포인트에 도착했다고 치는 거리 (m) |
| `aiArrivalDistTracking`, `aiArrivalDistWalkTracking` | 사람을 따라갈 때 멈추는 거리와 걷기로 바꾸는 거리 |
| `aiStop5SecFrames` | 경로의 "5초 대기" 포인트에서 기다리는 시간 (틱) |
| `aiJumpCheckDist` | 앞의 이 거리 안에 걸리는 것이 있으면 점프를 시도합니다 (m) |

소리를 들은 AI 는 경계 상태가 됩니다.

### `human/controller.json` — 이동과 몸 크기

`humanControllerData` 는 모든 사람이 함께 쓰는 이동 값입니다.

| 키 | 뜻 |
|---|---|
| `gravityAcceleration` | 아래로 붙는 가속도 (m/s²) |
| `fallMaxSpeed` | 떨어지는 속도의 한계 (음수, m/s) |
| `fallMinSpeed` | 이보다 빠르게 떨어져 착지하면 낙하 데미지를 받습니다 (음수, m/s) |
| `fallDamageMax` | 한계 속도로 착지했을 때의 낙하 데미지. 그 사이의 속도는 비례합니다 |
| `fallDamageRandomMax` | 낙하 데미지에 더하는 무작위 값의 상한 (0 이상 이 값 미만의 정수) |
| `controllerSlopeLimit` | 이보다 가파른 면은 올라가지 못하고 미끄러집니다 (도) |
| `controllerStepClimbSpeed` | 턱을 올라설 때 몸이 올라가는 속도 (m/s) |
| `deadlineY` | 이 높이 아래로 떨어지면 죽습니다 |
| `controllerStepOffset`, `controllerGroundProbeRadius`, `deadBodyFallAngularSpeed`, `deadBodyCollision` | 쓰이지 않습니다 |

`controllerSizeData` 는 몸 크기의 목록이고, 사람 종류의 `controllerSizeIndex` 가 번호를 가리킵니다.

| 키 | 뜻 |
|---|---|
| `height` | 키 (m). 맵과 사람끼리의 충돌에서 세로 범위입니다 |
| `cameraHeight` | 눈높이 (m). 1인칭 카메라, AI 의 시야, 총알이 나가는 높이입니다 |
| `mapRadius` | 맵의 블록과 부딪치는 반지름 |
| `humanRadius` | 다른 사람과 부딪치는 반지름 |

### `human/general.json` — 모습과 피격 반응

| 키 | 뜻 |
|---|---|
| `humanBodyScale`, `humanArmScale`, `humanLegScale` | 몸통, 팔, 다리 모델의 배율 |
| `humanBodyHeight`, `humanArmHeight`, `humanLegHeight` | 몸통, 팔, 다리 모델을 놓는 높이 (발에서, m) |
| `armAngleInitial` | 무기를 든 사람이 처음에 겨누는 각도이자 AI 가 평소에 팔을 두는 각도 (도. 음수가 아래) |
| `armAngleReloading` | 재장전하거나 무기를 바꾸는 동안의 팔 각도 |
| `headHitReaction`, `bodyHitReaction`, `legHitReaction` | 총알이 머리·몸·다리에 맞았을 때 조준이 흐트러지는 양 (무기의 `errorRange` 와 같은 눈금) |
| `grenadeHitReaction`, `zombieHitReaction` | 폭발에 휩쓸렸을 때와 좀비의 공격에 맞았을 때의 같은 값 |

흐트러짐은 지금 값보다 클 때만 바뀌고, `weapon/accuracy.json` 의 `reactionRecoveryPerSecond` 만큼씩 줄어듭니다.

### `human/interaction.json` — 무기 줍기

맨손인 슬롯이 있는 사람은 떨어진 무기에 가까이 가면 줍습니다.

| 키 | 뜻 |
|---|---|
| `weaponPickupRadius` | 주울 수 있는 수평 거리 (m) |
| `weaponPickupVerticalRange` | 주울 수 있는 높이 차 (`min`, `max`. 무기의 높이 − 사람의 발 높이, m) |

### `human/hitbox.json` — 피격 판정

머리(`head`), 몸(`body`), 다리(`leg`)마다 원기둥 하나입니다: `position`(발에서의 위치), `height`, `radius`, `rotationEuler`(기울이기). 종류가 다른 판정을 쓰려면 목록 끝에 추가하고 `human/type.json`의 `hitboxSizeIndex`로 가리킵니다.

### `human/model.json` — 모습

`textures`와 `modelData`는 무기 모델과 같은 형식입니다. `armIndex`·`legIndex`는 팔·다리 모델 번호(`human/arm.json`, `human/leg.json`), `armTextureIndex`·`legTextureIndex`는 그 모델에 입힐 텍스처 번호(이 항목의 `textures` 안에서)입니다.

### `human/arm.json` — 팔 모델

`humanArmModelData` 항목 하나가 팔 모델 한 벌입니다.

| 키 | 뜻 |
|---|---|
| `name` | 이름 (구분용) |
| `leftArms`, `rightArms` | 왼팔·오른팔 모양의 `.x` 경로 목록. 무기 모델의 `leftArmIndex`·`rightArmIndex`가 이 목록 안의 순서를 가리킵니다 |

### `human/leg.json` — 다리 모델

`humanLegModelData` 항목 하나가 다리 모델 한 벌입니다.

| 키 | 뜻 |
|---|---|
| `name` | 이름 (구분용) |
| `legs` | 다리 동작 한 장면씩의 `.x` 경로 목록. `human/animation.json`의 `index`가 이 목록 안의 순서를 가리킵니다 |

`human/animation.json`의 `humanAnimation`은 서 있기·걷기·달리기 동작입니다. `index`는 그 동작에서 차례로 보여 줄 다리 모양 번호, `forwardSpeed`·`strafeSpeed`·`backwardSpeed`는 방향별로 동작 한 바퀴에 걸리는 시간(초)입니다. `footstepPhase`는 한 바퀴 안에서 발이 땅에 닿는 순간들이고(0 이상 1 미만의 비율, 기본 0 과 0.5), 그때 [재질](#블록-재질)의 발소리가 납니다. 비워 두면 그 동작에서는 발소리가 나지 않습니다.

## 오브젝트

`object/list.json`의 `objectData` 항목 하나가 오브젝트 하나입니다.

| 키 | 뜻 |
|---|---|
| `name` | 이름 |
| `modelIndex` | `object/model.json`의 번호 |
| `colliderIndex` | `object/collider.json`의 번호 |
| `hp` | 체력 |
| `soundPath`, `soundVolume` | 부서질 때의 소리 |
| `jump` | 부서질 때 튀어 오르는 세기 |

`object/collider.json`의 판정 모양(`shapes`)은 여러 개를 겹쳐 쓸 수 있습니다.

| `type` | 모양 | `size` |
|---|---|---|
| 0 | 구 | `x` = 반지름 |
| 1 | 상자 | `x`, `y`, `z` = 전체 크기 |
| 2 | 캡슐 | `x` = 반지름, `y` = 높이, `z` = 방향 (0 X축, 1 Y축, 2 Z축) |

`object/general.json` 은 모든 오브젝트가 함께 쓰는 값입니다.

| 키 | 뜻 |
|---|---|
| `modelScale` | 오브젝트 모델의 배율 |
| `bulletDamageMultiplier` | 총알이 오브젝트에 주는 데미지 = 총알의 위력 × 이 값 (소수점 아래 버림) |
| `bulletPenetrationAttenuation` | 오브젝트를 맞힌 뒤 총알에 남는 위력의 비율. 총알은 판정 모양을 지나는 동안 0.25 m 마다 다시 맞으므로, 두꺼운 오브젝트일수록 여러 번 깎입니다 |
| `addonObjectIndex` | 에드온 미션(`.mif`)이 직접 지정하는 오브젝트가 들어가는 자리. 이 번호의 항목은 미션을 로드할 때마다 덮어쓰이므로 다른 용도로 쓰지 마세요 |

## 이펙트

`effect_data.json`에 있습니다. 총구 화염, 탄피, 연기, 혈흔, 폭발이 모두 이 파일의 값으로 그려집니다.

### `effectTextureData`

이펙트가 쓰는 텍스처 목록입니다. 개수 제한은 없습니다. `emitters`의 `textureIndex`가 이 목록의 번호입니다.

| 키 | 뜻 |
|---|---|
| `texturePath` | 텍스처 파일 경로 (게임 폴더 기준) |

### `effectGeneralData`

| 키 | 뜻 |
|---|---|
| `wallBloodEffectIndex` | 혈흔 입자가 벽에 닿았을 때 그 자리에 남길 프리셋 번호 |
| `decalSurfaceOffset` | 벽에 붙는 자국(`flags` 1)을 면에서 띄우는 거리 (미터). 0 이면 면과 겹쳐 떨려 보입니다 |
| `poolInitialSize` | 시작할 때 만들어 두는 이펙트 자리 수 (원본은 256 고정) |
| `poolGrowStep` | 자리가 다 찼을 때 한 번에 늘리는 수. 0 이면 늘리지 않고 원본처럼 새 이펙트를 버립니다 |
| `poolMaxSize` | 늘릴 수 있는 한계. 0 이면 한계 없음 |

자리를 늘리는 데는 비용이 들므로 `poolGrowStep`은 한 번에 넉넉히(수십 개) 잡는 편이 좋습니다. 한 번 늘린 자리는 줄지 않습니다.

### `effectData`

이펙트 프리셋 목록입니다. 무기 모델·탄환·사람 종류의 `...EffectIndex`가 이 목록의 번호를 가리킵니다. 프리셋 하나는 `name`과 `emitters`(한 번에 내는 입자 묶음) 여러 개로 이루어집니다. 예를 들어 폭발은 섬광 하나와 연기 넷입니다.

`emitters`의 항목 하나가 입자 한 종류입니다.

| 키 | 뜻 |
|---|---|
| `textureIndex` | `effectTextureData`의 번호 |
| `flags` | 동작 플래그. 0 없음, 1 빌보드를 끄고 방향을 고정(벽에 붙는 자국. 아래 설명), 2 블록에 닿는지 검사(닿으면 `wallBloodEffectIndex` 자국을 남기고 사라집니다). 더해서 씁니다 |
| `blendMode` | 색을 섞는 방식. 0 알파(원본과 같습니다), 1 가산(뒤에 있는 색에 더합니다. 불꽃·섬광처럼 발광하는 것) |
| `spawnCount` | 한 번에 내는 개수 |
| `countPerTrigger` | 0 보다 크면 개수 = 내림(트리거값 × 이 값)이고 `spawnCount`를 무시합니다. 트리거값은 피격 데미지입니다 (혈흔이 데미지에 비례해 튀는 것) |
| `positionOffset`, `positionRandomRange` | 내는 자리와 그 ± 흔들림 |
| `velocity`, `velocityRandomRange` | 처음 속도와 그 ± 흔들림 (초당 미터) |
| `gravityY` | 초당 속도에 더하는 세로 가속도. 떨어뜨리려면 음수 |
| `rotationDeg`, `rotationRandomRange` | 텍스처를 돌리는 각도와 그 ± 흔들림 (도) |
| `rotationRateDeg`, `rotationRateRandomRange` | 초당 회전 속도와 그 ± 흔들림 (도) |
| `size`, `sizeRandomRange`, `sizeRate` | 크기(미터), 그 ± 흔들림, 초당 크기 변화 |
| `alpha`, `alphaRate` | 불투명도(0 에서 1 사이)와 초당 변화. 사라지게 하려면 음수 |
| `brightness`, `brightnessRate` | 가산일 때의 발광 세기(0 에서 1 사이)와 초당 변화. **알파일 때는 쓰이지 않습니다** |
| `lifetime` | 수명 (초) |

크기가 0 이하, 불투명도가 0 이하가 되면 수명이 남아도 사라집니다. 가산인 경우 발광 세기가 0 이하일 때도 같습니다.

`blendMode`를 1 로 바꿀 때는 `brightness`도 함께 올리세요. 기본 데이터의 `brightness`는 대부분 0 에 가깝고, 가산에서 세기가 0 이면 아무것도 더해지지 않아 보이지 않습니다.

### 벽에 붙는 자국

`flags`에 1 을 넣은 입자는 카메라를 따라 돌지 않고 한 방향으로 고정됩니다. 블록 면 위에서 나오는 이펙트에서는 그 면에 눕고, 면에서 `decalSurfaceOffset`만큼 떠서 놓입니다. 면 위에서 나오는 이펙트는 둘입니다.

- [재질](#블록-재질)의 `hitEffect`와 `bulletHoleEffect` — 탄환이 블록에 맞은 자리.
- `wallBloodEffectIndex` — 혈흔 입자(`flags` 2)가 블록에 닿은 자리.

그래서 `flags` 1 인 입자로 만든 프리셋을 재질의 `bulletHoleEffect`에 넣으면 탄흔이 됩니다 (`hitEffect`의 프리셋에 그런 입자를 더해도 됩니다. 같은 프리셋의 연기 등은 전과 똑같이 나옵니다). 기본 데이터에는 탄흔이 없습니다 (원본에 없습니다).

- 자국의 `positionOffset`과 `velocity`는 면 기준입니다 (x, y 가 면을 따라가는 쪽이고, z 는 음수가 면에서 나오는 쪽).
- 자국은 `lifetime`이 다하거나 불투명도가 0 이 될 때까지 이펙트 자리를 하나씩 차지합니다. 오래 남기면 풀이 그만큼 늘어납니다.
- 관통하는 탄환이 한 틱 안에 블록을 여러 번 뚫으면, 뚫은 횟수만큼의 자국이 전부 처음 들어간 면의 같은 자리에 찍힙니다.
- 그 밖의 이펙트(총구 화염, 폭발, 오브젝트 피격 등)에서는 `flags` 1 인 입자가 면이 아니라 이펙트를 낸 쪽의 방향(총구 화염은 무기)으로 고정됩니다.

## 미션과 스카이

### `mission_data.json`

- `officialMissions` — 메뉴의 공식 미션 목록. 항목마다 `name`(목록에 나오는 이름), `fullname`(브리핑 제목), `bd1Path`(블록), `pd1Path`(포인트), `txtPath`(브리핑 글), `adjustCollision`(추가 충돌 검사), `darkScreen`(어두운 화면).
- `openingData` — 오프닝에 쓰는 맵.
- `demoData` — 메뉴 배경으로 도는 맵 목록.

미션을 목록에 추가하려면 `officialMissions` 끝에 항목을 더하면 됩니다. BD1 / PD1 / MIF 파일은 맵 에디터를 이용해 수정합니다.

### `sky_data.json`

| 키 | 뜻 |
|---|---|
| `skyMeshPath` | 하늘을 그리는 모델 (`.x`). 비워 두면 하늘을 그리지 않습니다 |
| `skyTexturePath` | 스카이 번호별 텍스처 |
| `skyColor` | 스카이 번호별 안개의 색 (0 에서 255 사이의 정수) |
| `fog` | `false` 면 안개를 쓰지 않습니다. `true` 여도 유저가 OPTION 의 Graphic 에서 Fog 를 끄면(`config.json` 의 `enableFog`) 안개가 없습니다 |
| `fogStart`, `fogEnd` | 안개가 시작하는 거리와 완전히 가리는 거리 (m) |
| `skyLayerData` | 스카이 번호별로 하늘에 겹쳐 그리는 풍경과 해·달 (아래). 목록에 없는 번호는 하늘만 그립니다 |

**하늘에 겹쳐 그리는 것** (`skyLayerData`, 1.3.0): 항목의 순서가 스카이 번호이고, 항목마다 다음 키를 씁니다. 원본에는 없고, 기본 데이터는 비어 있습니다.

```json
"skyLayerData": [
  {},
  { "billboardTexturePath": "addon/mysky/city.png", "sunTexturePath": "addon/mysky/sun.png", "sunDirection": { "x": 0.3, "y": 0.4, "z": 1.0 }, "sunSize": 10 }
]
```

| 키 | 뜻 |
|---|---|
| `billboardTexturePath` | 하늘 위에 겹치는 풍경(도시, 산). 하늘과 같은 모델(`skyMeshPath`)에 입히므로 **텍스처의 배치가 하늘 텍스처와 같아야 합니다** (`sky.x` 는 한 장을 2 × 2 칸으로 나눠 씁니다). 알파로 하늘과 섞습니다 (PNG 를 쓰세요). 비우면 없습니다 |
| `sunTexturePath` | 해나 달. 늘 카메라를 보는 사각형 하나로 그리고 알파로 섞습니다. 비우면 없습니다 |
| `sunDirection` | 해가 있는 방향 (+x 오른쪽, +y 위, +z 앞). 길이는 쓰지 않습니다 |
| `sunSize` | 해의 겉보기 크기 (도) |

그리는 순서는 하늘 → 해·달 → 풍경입니다 (산이 해를 가립니다). 셋 다 안개를 받지 않고, 그림자는 없습니다. 맵팩의 하늘 파일(Manifest 의 `skyDataPath`)에도 같은 키로 적을 수 있습니다.

카메라가 그리는 거리는 여기가 아니라 설정(`config.json` 의 `Graphic` 에 있는 `nearClippingPlane`, `farClippingPlane`. OPTION 의 Graphic 에서 Near, Far)에 있습니다. 안개를 끄고 Far 가 짧으면(기본 80 m) 그 거리에서 맵이 잘려 보입니다. 안개가 그 경계를 가리고 있었기 때문입니다. 1.2.2 까지 이 파일에 있던 같은 이름의 두 키는 쓰이지 않던 것이라 1.2.3 에서 뺐습니다 (파일에 남아 있으면 경고를 남기고 무시합니다).

### `global.json`

메뉴의 버전 표시와 CREDIT 화면에 나오는 글입니다. 게임의 동작에는 영향이 없습니다.

| 키 | 뜻 |
|---|---|
| `productName` | 제품 이름 |
| `versionMajor`, `versionMinor`, `versionPatch` | 버전 (점으로 이어 붙여 보여 줍니다) |
| `companyName` | 만든 이 |
| `licenseType`, `licenseName`, `licenseLines` | 라이선스의 이름과 본문 |

## 확장 블록 데이터 (BD2)

BD1 의 제한(텍스처 10개, 면마다의 정보 없음)을 없앤 GodotXOPS 전용 블록 형식입니다. 확장자는 `.bd2`입니다. BD1 은 그대로 읽습니다.

미션에서는 [확장 미션 데이터](#확장-미션-데이터-mif2)로 불러옵니다. [개발 문서](development.md)의 `map_viewer`로 파일 하나를 띄워 볼 수 있고, `bd2_check`의 `--convert`로 BD1 을 BD2 로 바꿀 수 있습니다.

### 파일 구조

바이너리이고 모든 수는 리틀 엔디안입니다.

| 위치 | 크기 | 내용 |
|---|---|---|
| 0 | 8 | 글자 `GDXOPSB3` |
| 8 | 4 | 맵 데이터 파일 경로의 길이 (바이트, 부호 없는 정수) |
| 12 | N | 맵 데이터 파일 경로 (UTF-8, 게임 폴더 기준) |
| 12 + N | 4 | 블록 개수 (부호 없는 정수) |
| 16 + N | 316 × 개수 | 블록 |
| 뒤 | 4 | 구역 블록 개수 (부호 없는 정수) |
| 뒤 | 100 × 개수 | 구역 블록: 정점 8개(블록과 같은 순서, 실수 4바이트씩 96바이트)와 [구역](#구역) 태그 번호 (정수 4바이트) |

블록 하나는 316바이트입니다.

| 위치 | 크기 | 내용 |
|---|---|---|
| 0 | 96 | 정점 8개. 정점마다 x, y, z (실수 4바이트) |
| 96 | 192 | UV. 면 6개 × 정점 4개, 정점마다 u, v (실수 4바이트) |
| 288 | 24 | 면 6개의 텍스처 번호 (정수 4바이트). 텍스처 목록의 번호이고, 음수이거나 목록에 없는 번호이면 그 면을 그리지 않습니다 |
| 312 | 4 | 블록 플래그 (정수 4바이트) |

- 좌표는 미터이고 게임 안의 축 그대로입니다 (+x 오른쪽, +y 위, -z 앞). BD1 의 좌표 (x, y, z) 는 BD2 에서 (-x × 0.1, y × 0.1, z × 0.1) 입니다. 정점 8개의 순서는 BD1 과 같습니다.
- 면이 쓰는 정점은 BD1 과 같습니다: 면 0 은 정점 0, 3, 2, 1 / 면 1 은 7, 4, 5, 6 / 면 2 는 4, 0, 1, 5 / 면 3 은 5, 1, 2, 6 / 면 4 는 6, 2, 3, 7 / 면 5 는 7, 3, 0, 4.
- UV 는 면의 정점 순서대로 적습니다. 면 2 의 첫 UV 는 정점 4 의 것입니다. BD1 처럼 한 칸 밀려 있지 않습니다.
- 텍스처 개수에는 제한이 없습니다.
- 면의 [재질](#블록-재질)(착탄 이펙트와 소리, 발소리)은 블록 파일에 없습니다. 그 면의 텍스처가 정합니다 (아래 맵 데이터 파일의 `materialIndex`).

**1.2.5 까지의 BD2** 는 글자가 `GDXOPSB2` 이고 구역 부분이 없습니다 (블록에서 끝납니다). 지금도 읽을 수 있고 구역은 0 개입니다.

**1.2.2 까지의 BD2** 는 글자가 `GDXOPSBD` 이고 블록이 340바이트였습니다 (텍스처 번호와 플래그 사이에 면 6개의 재질 번호가 정수 4바이트씩 있었습니다). 지금도 읽을 수 있지만 그 재질 번호는 버립니다. 버린 번호 가운데 -1 이 아닌 것이 있으면 경고를 남깁니다. 에디터에서 열어 저장하면 지금의 형식으로 다시 쓰입니다.

### 블록 플래그

통과 여부만 담습니다. 판정을 끄는 비트이고, 0 이면 모든 판정에서 충돌합니다. 더해서 씁니다. 아래 세 값 밖의 비트는 나중을 위해 비워 둔 것이니 0 으로 둡니다.

| 값 | 켜면 통과하는 것 |
|---|---|
| 1 | 사람 (이동, 떨어진 무기와 오브젝트의 바닥, 3인칭 카메라, 화면 가장자리의 벽 가림) |
| 2 | 총알과 수류탄 |
| 4 | AI 의 시야와 사선, 폭발 가림 |

통과하는 블록에서는 아무 일도 일어나지 않습니다 (총알이 지나가도 이펙트나 소리가 없습니다). 예를 들어 5 (1 + 4) 는 걸어 들어갈 수 있고 AI 가 건너편을 볼 수 있지만 총알은 막는 블록입니다. 7 은 보이기만 하고 아무것도 막지 않는 블록입니다. **모양이 유효한 입체가 아닌 블록은 플래그와 관계없이 아무것도 막지 않습니다** (원본의 규칙이고 BD1 과 BD2 가 같습니다): 꼭짓점 둘 이상이 같은 자리에 겹친 블록, 그리고 블록의 가운데가 어느 한 면의 바깥쪽에 놓이는 블록(납작하게 눌리거나 뒤집히거나 심하게 뒤틀린 것)입니다. 이런 블록은 그려지기만 합니다. 에디터의 블록 모드에서 그런 블록을 선택하면 오른쪽 칸에 빨간 글로 알려 줍니다.

### 맵 데이터 파일

BD2 가 경로를 들고 있는 JSON 파일입니다. 텍스처 목록과 [구역](#구역) 태그가 들어 있습니다. 파일 이름은 자유이고 (에디터는 `블록 파일 이름_map.json` 으로 씁니다), 여러 맵이 한 파일을 같이 쓸 수 있습니다. 1.2.5 까지는 "텍스처 목록 파일"이었고 키가 `blockTextureData` 였습니다. 1.3.0 에서 `textureData` 로 바뀌었으니 옛 파일은 키 이름을 고쳐야 합니다.

```json
{
  "textureData": [
    { "diffusePath": "addon/mymap/wall.png", "materialIndex": -1 },
    { "diffusePath": "addon/mymap/floor.png", "materialIndex": 10000 }
  ],
  "areaTagData": [
    { "name": "indoor", "damagePerSecond": 0, "gravityScale": 1, "aiSightScale": 1, "soundReverb": 0.4, "blockWeather": true, "hostility": [] }
  ]
}
```

텍스처 목록(`textureData`)의 키:

| 키 | 뜻 |
|---|---|
| `diffusePath` | 텍스처 파일 경로 (게임 폴더 기준). 비워 두면 흰 면이 됩니다 |
| `materialIndex` | 이 텍스처를 쓰는 면의 [재질](#블록-재질) 번호 (10000 에서 19999 는 미션이 속한 맵팩의 재질). -1 이면 미션의 기본 재질(MIF2 의 `defaultBlockMaterial`)입니다. 적지 않으면 -1 입니다 |

구역 태그(`areaTagData`)의 키는 [구역](#구역)에 있습니다.

재질은 면마다가 아니라 **텍스처마다** 정합니다: 콘크리트 텍스처에 콘크리트 재질을 한 번 적으면 그 텍스처를 쓰는 면 전부에서 콘크리트의 착탄과 발소리가 납니다. 같은 그림인데 재질이 달라야 하면 같은 파일을 목록에 두 번 넣고 재질만 다르게 적습니다. 그리지 않는 면(텍스처 번호가 음수이거나 목록에 없는 면)은 미션의 기본 재질입니다.

## 확장 포인트 데이터 (PD2)

PD1 의 제한(값이 0 에서 255 사이, 파라미터 4개, 이벤트 세 줄)을 없앤 GodotXOPS 전용 포인트 형식입니다. 확장자는 `.pd2`입니다. PD1 은 그대로 읽습니다.

미션에서는 [확장 미션 데이터](#확장-미션-데이터-mif2)로 불러옵니다. `pd2_check`의 `--convert`로 PD1 을 PD2 로 바꿀 수 있습니다 ([개발 문서](development.md)). 에디터(`GodotXOPS_Editor.bat`)로 BD2, PD2, MIF2 와 데이터 파일(JSON)을 만들고 고칠 수 있고, 이벤트는 등록 파일의 이름과 `kind` 대로 입력 칸이 나옵니다 ([개발 문서의 에디터](development.md#에디터)).

### 파일 구조

바이너리이고 모든 수는 리틀 엔디안입니다.

| 크기 | 내용 |
|---|---|
| 8 | 글자 `GDXOPSPD` |
| 4 | 이벤트 시작 번호의 개수 (부호 없는 정수) |
| 4 × 개수 | 이벤트 시작 번호들 (정수 4바이트) |
| 4 | 포인트 개수 (부호 없는 정수) |
| 포인트마다 다름 | 포인트 |

포인트 하나는 36바이트에 추가 파라미터가 붙습니다.

| 크기 | 내용 |
|---|---|
| 12 | 위치 x, y, z (실수 4바이트) |
| 4 | 방향 (실수 4바이트, 도) |
| 16 | 파라미터 4개 (정수 4바이트): 종류, 두 번째, 세 번째, 식별번호. PD1 의 P1 에서 P4 와 같은 뜻입니다 |
| 4 | 추가 파라미터의 개수 (부호 없는 정수) |
| 4 × 개수 | 추가 파라미터 (4바이트 칸) |

- 좌표는 BD2 와 같습니다: 미터, 게임 안의 축 그대로. PD1 의 (x, y, z) 는 PD2 에서 (-x × 0.1, y × 0.1, z × 0.1) 입니다.
- 파라미터는 정수 4바이트라서 255 를 넘는 번호를 쓸 수 있습니다. 사람·무기·오브젝트 번호에 10000 이상을 적으면 맵팩(10000 부터)이나 미션(20000 부터)의 [에드온 데이터](#맵팩과-미션의-추가-데이터-에드온-데이터)를 가리킵니다. "없음"을 뜻하는 값은 -1 처럼 음수로 적을 수 있습니다.
- 메시지는 PD1 과 같이 포인트 파일 옆의 같은 이름 `.msg` 파일에서 읽습니다.

### 방향

그 자리에 놓이는 것을 위에서 볼 때 오른쪽(시계 방향)으로 몇 도 돌려 놓는지입니다. 사람은 0 일 때 -z 쪽을 봅니다. 무기와 오브젝트는 모델 파일을 그 각도만큼 돌려 놓습니다.

PD1 의 방향 값 r (라디안)과의 관계는 사람과 무기가 r 을 도로 바꾼 값 + 180, 오브젝트가 r 을 도로 바꾼 값입니다. 경로와 이벤트 포인트는 방향을 쓰지 않습니다.

### 이벤트 시작 번호

이벤트 줄마다 처음 처리할 이벤트 포인트의 식별번호입니다. 개수가 곧 이벤트 줄 수라서 PD1 의 세 줄보다 많이 둘 수 있습니다. PD1 은 항상 156, 146, 136 세 줄입니다 (원본의 -100, -110, -120).

### 원본 형식에만 있는 제한

PD2 에는 아래 제한이 없습니다. PD1 미션은 원본과 똑같이 진행되도록 그대로 둡니다.

- 메시지 개수: PD1 은 16개까지입니다. PD2 는 `.msg` 파일의 줄 수만큼 씁니다.
- 한 번(1/33초)에 한 이벤트 줄이 처리하는 이벤트 수: PD1 은 6개까지이고 나머지는 다음으로 넘어갑니다. PD2 는 기다리는 이벤트(도착, 사망, 시간 등)를 만날 때까지 한 번에 다 처리합니다. 바로 넘어가는 이벤트끼리 서로를 가리켜 고리를 이루면, 한 바퀴 돌 때마다 다음으로 넘어갑니다.
- 포인트 종류 번호: 20 이상은 [스크립트 이벤트](#스크립트-이벤트)입니다. 어느 이벤트 묶음에도 등록되지 않은 번호가 있으면 미션을 로드하지 않습니다. PD1 은 20 이상의 종류를 무시합니다.

### 추가 파라미터

포인트마다 개수가 다를 수 있는 4바이트 칸입니다. 포인트 종류에 따라 게임이 칸을 정수, 실수, 참·거짓(0 이면 거짓)으로 읽습니다. [스크립트 이벤트](#스크립트-이벤트)가 이 칸을 파라미터와 출구로 씁니다 (`e0` 이 첫 칸). 원본의 포인트 종류(1 에서 19 사이)는 추가 파라미터를 쓰지 않고, 넣어도 무시합니다. 없는 칸은 0 으로 읽습니다.

## 확장 미션 데이터 (MIF2)

원본 MIF 를 대체하는 GodotXOPS 전용 미션 파일입니다. 확장자는 `.mif2`이고 내용은 JSON 입니다. MIF 는 그대로 읽습니다.

- **MIF2 는 BD2 와 PD2 만 불러옵니다.** `blockPath`에 BD1, `pointPath`에 PD1 을 적으면 로드하지 않습니다. 원본 형식의 맵은 원본 MIF 로 씁니다.
- 경로는 전부 게임 폴더(`GodotXOPS.exe`가 있는 폴더) 기준입니다. 미션 파일이 있는 폴더 기준이 아닙니다.
- `addon` 폴더에 넣으면 `.mif`와 함께 미션 목록에 나옵니다. [맵팩](#맵팩)의 미션으로 쓰려면 맵팩의 Manifest 에 적습니다.
- 원본 미션을 확장 형식 한 벌로 바꾸는 도구가 있습니다 ([개발 문서](development.md)의 `mif2_check`).

```json
{
  "name": "MY MISSION",
  "fullname": "My first extended mission",
  "blockPath": "addon/mypack/map.bd2",
  "pointPath": "addon/mypack/map.pd2",
  "skyIndex": 1,
  "weatherIndex": 0,
  "adjustCollision": false,
  "darkScreen": false,
  "image0": "addon/mypack/brief.png",
  "image1": "",
  "briefing": [
    "첫 줄",
    "둘째 줄"
  ],
  "defaultBlockMaterial": 0,
  "addonHumanDataPath": "",
  "addonWeaponDataPath": "addon/mypack/weapons.json",
  "addonObjectDataPath": "",
  "addonSoundDataPath": "",
  "addonImageDataPath": "",
  "addonInputDataPath": "",
  "addonWeatherDataPath": "",
  "briefingScriptPath": "",
  "hudScriptPath": "",
  "resultScriptPath": "",
  "fontImageOverridePath": ""
}
```

| 키 | 뜻 |
|---|---|
| `name` | 미션 목록에 나오는 이름 |
| `fullname` | 브리핑 제목 |
| `blockPath` | 블록 데이터 (BD2) |
| `pointPath` | 포인트 데이터 (PD2). 메시지는 같은 이름의 `.msg` 파일에서 읽습니다 |
| `skyIndex` | 스카이 번호 (`sky_data.json`). 10000 에서 19999 는 이 미션이 속한 [맵팩](#맵팩)의 하늘입니다 |
| `weatherIndex` | 미션을 시작할 때의 [날씨](#날씨) 번호. 0 이 맑음입니다 |
| `adjustCollision` | 추가 충돌 검사 (MIF 화면 플래그의 1) |
| `darkScreen` | 어두운 화면 (MIF 화면 플래그의 2) |
| `image0`, `image1` | 브리핑 이미지. 비워 두면 없습니다 |
| `briefing` | 브리핑 본문. 항목 하나가 한 줄입니다 |
| `defaultBlockMaterial` | 재질을 적지 않은(`materialIndex` -1) 텍스처의 면과 그리지 않는 면이 쓰는 [재질](#블록-재질) 번호. 0 이 원본의 벽 착탄입니다. **-1 을 적으면 안 됩니다** (없는 재질이 되어 이펙트도 소리도 나지 않습니다) |
| `addon...DataPath` | 이 미션이 들고 오는 추가 데이터 파일 다섯 가지 (아래). 비워 두면 없습니다 |
| `addonInputDataPath` | 이 미션에서만 쓰는 [커스텀 바인딩](#커스텀-바인딩)의 등록 파일. 비워 두면 없습니다 |
| `addonWeatherDataPath` | 이 미션이 들고 오는 [날씨](#날씨) 목록. 첫 항목이 20000 번입니다. 비워 두면 없습니다 |
| `briefingScriptPath`, `hudScriptPath`, `resultScriptPath` | 이 미션에서만 브리핑, HUD, 결과 화면을 대신 그리는 [화면 스크립트](#미션이-들고-오는-화면)(`.sgd`). 비워 두면 평소의 화면입니다 |
| `fontImageOverridePath` | 이 미션에서만 스프라이트 글꼴(`data/char.dds`) 대신 쓰는 그림 (아래). 비워 두면 평소의 글꼴입니다 |

파일에 없는 키는 위 예시의 빈 값이나 0, `false`로 읽습니다. 모르는 키는 무시하고 경고를 남깁니다 (디버그 콘솔에서 볼 수 있습니다).

1.2.3 까지의 MIF2 에 있던 `addonSkyDataPath`, `addonEventDataPath`, `addonBlockMaterialDataPath`, `addonEffectDataPath` 는 1.2.4 에서 [맵팩](#맵팩)으로 옮겼습니다. MIF2 에 남아 있으면 모르는 키로 무시하고 경고를 남깁니다. 미션의 데이터 번호도 10000 에서 20000 으로 바뀌었으므로 손으로 고쳐야 합니다.

**미션의 글꼴 그림** (`fontImageOverridePath`): 그 미션을 하는 동안 `char.dds` 로 그리는 글자가 전부 이 그림으로 그려집니다.

- 바뀌는 것: 브리핑, 메인게임의 HUD, 결과 화면의 스프라이트 글자 (기본 화면이든 화면 스크립트든), 이벤트가 놓는 글자 가운데 글꼴이 1(`char.dds`)인 것.
- 바뀌지 않는 것: OS 글꼴로 그리는 글자(브리핑 본문, 이벤트 메시지, 글꼴 0 의 글자), 메뉴와 오프닝, 디버그 콘솔. 메뉴는 [맵팩](#맵팩이-들고-오는-화면)의 글꼴 그림만 그 맵팩의 페이지에서 바꿉니다.
- 그림의 배치는 `char.dds` 와 같아야 합니다: 가로세로 16칸씩이고 글자 코드가 칸 번호입니다 (왼쪽 위가 0, 오른쪽으로 가며 늘고 한 줄이 16칸). 그림의 크기는 달라도 되고 칸은 그림을 16 으로 나눈 크기입니다. HUD 의 테두리도 이 그림의 글리프로 그리므로 `char.dds` 를 복사해 고치는 편이 안전합니다.
- 글꼴은 미션에 하나입니다. 글자마다 다른 글꼴을 고를 수는 없습니다.
- 미션이 적지 않았으면 미션이 속한 맵팩의 Manifest 에 적힌 글꼴 그림을 씁니다.
- 고른 그림을 읽지 못하면 경고를 남기고 평소의 글꼴로 그립니다 (맵팩의 것으로 넘어가지 않습니다). 미션이 끝나 메뉴로 돌아가면 원래 글꼴입니다 (맵팩의 페이지에 있으면 그 맵팩의 글꼴).

### 맵팩과 미션의 추가 데이터 (에드온 데이터)

맵팩과 미션이 자기만의 사람, 무기, 오브젝트, 소리, 이미지를 들고 올 수 있고, 맵팩은 하늘, 이벤트, 재질, 이펙트도 들고 옵니다. 기본 데이터(`godotdata/`)를 고치지 않아도 되고, 그 미션을 하는 동안에만 적용됩니다.

**번호는 세 구간입니다.**

| 번호 | 가리키는 것 |
|---|---|
| 0 에서 9999 | 기본 데이터 (`godotdata/`) |
| 10000 에서 19999 | 미션이 속한 [맵팩](#맵팩)의 데이터. 10000 이 맵팩 파일의 첫 항목입니다 |
| 20000 부터 | 미션(MIF2)이 들고 온 데이터. 20000 이 미션 파일의 첫 항목입니다 |

PD2 의 포인트도, 데이터 안에서 다른 항목을 가리키는 번호(`modelIndex`, `bulletIndex`, `typeIndex`, 이펙트 번호 등)도 같은 규칙입니다. 그래서 미션의 무기가 기본 탄환(예: 0)을 쓸 수도, 맵팩의 탄환(10000)이나 자기 탄환(20000)을 쓸 수도 있습니다. 맵팩의 데이터는 맵팩의 미션 전부가 함께 쓰므로, 맵팩의 데이터에서 20000 이상의 번호를 쓰지 마세요 (미션마다 다른 항목을 가리킵니다).

에드온 파일은 종류마다 하나이고, 기본 데이터에서는 여러 파일로 나뉘어 있는 목록들을 한 파일에 모아 적습니다. 키 이름과 항목의 모양은 기본 데이터와 같습니다.

| MIF2 의 키 | Manifest 의 키 | 그 파일에 적는 목록 |
|---|---|---|
| `addonHumanDataPath` | `humanDataPath` | `humanData`, `humanModelData`, `humanArmModelData`, `humanLegModelData`, `humanTypeData`, `controllerSizeData`, `humanHitboxSizeData`, `humanAIParameterData` 안의 `aiData`와 `aiScopeData` |
| `addonWeaponDataPath` | `weaponDataPath` | `weaponData`, `bulletData`, `scopeData`, `weaponModelData` |
| `addonObjectDataPath` | `objectDataPath` | `objectData`, `objectModelData`, `objectColliderData` |
| `addonSoundDataPath` | `soundDataPath` | `soundData` ([이벤트의 소리](#이벤트의-소리)) |
| `addonImageDataPath` | `imageDataPath` | `imageData` ([이미지 목록](#이미지-목록)) |
| `addonWeatherDataPath` | `weatherDataPath` | `weatherData` ([날씨](#날씨)). 미션의 것은 이미지처럼 미션 정보와 함께 붙습니다 |
| (없음) | `effectDataPath` | `effectData`, `effectTextureData` |
| (없음) | `blockMaterialDataPath` | `blockMaterialData` |
| (없음) | `skyDataPath` | `skyTexturePath`, `skyColor` (아래 "맵팩의 하늘") |
| (없음) | `eventDataPath` | [스크립트 이벤트](#스크립트-이벤트) 묶음의 등록 파일 |

- 맵팩의 데이터는 미션 정보를 읽을 때(메뉴에서 미션을 고를 때) 붙고, 미션을 내릴 때 떨어집니다. 브리핑과 결과 화면에서도 쓸 수 있습니다. 미션을 다시 시작해도 다시 읽지 않습니다.
- 미션의 데이터 가운데 사람, 무기, 오브젝트, 소리는 맵을 로드할 때 붙고 맵을 내릴 때 떨어집니다 (미션을 다시 시작하면 파일을 다시 읽습니다). 이미지는 맵팩의 것처럼 미션 정보에 붙습니다.

**맵팩의 하늘** (`skyDataPath`): `sky_data.json` 과 같은 키를 쓰는 파일이고, 읽는 것은 텍스처 목록 `skyTexturePath` 와 안개 색 목록 `skyColor` 뿐입니다. 하늘 모델과 안개 거리, 카메라 거리는 게임 전체의 값이라 맵팩이 바꾸지 못합니다. MIF2 의 `skyIndex` 를 10000 으로 적으면 이 파일의 첫 항목입니다. 미션은 하늘을 들고 오지 않으므로 20000 이상은 없는 번호입니다.

```json
{
  "skyTexturePath": [ "addon/mypack/sky_dusk.png" ],
  "skyColor": [ { "r": 72, "g": 48, "b": 40, "a": 255 } ]
}
```

- 안개 색은 같은 순번의 `skyColor` 입니다. 적지 않으면 검정입니다 (먼 곳의 블록이 검게 사라집니다). 하늘의 아래쪽 색에 맞추면 자연스럽습니다.
- 목록에 없는 번호를 적으면 경고를 남기고 텍스처 없는 검은 하늘이 됩니다.

미션에 무기 하나를 더하는 파일(MIF2 의 `addonWeaponDataPath`)의 예입니다. 탄환은 기본 데이터의 0번을 쓰고 모델만 자기 것을 씁니다.

```json
{
  "weaponData": [
    { "name": "MY RIFLE", "modelIndex": 20000, "bulletIndex": 0, "magazineSize": 20 }
  ],
  "weaponModelData": [
    { "name": "MY RIFLE MODEL" }
  ]
}
```

(예시는 키를 줄여 적었습니다. 실제로는 기본 데이터의 항목 하나를 통째로 복사해서 고치는 편이 안전합니다. 빠진 키는 0 이나 빈 값이 됩니다.)

- 전역 설정(`weaponGeneralData`, `effectGeneralData`, `humanControllerData` 같은 것)은 에드온 파일에 적어도 쓰이지 않습니다. 미션 하나가 게임 전체의 값을 바꾸지 못합니다.
- 포인트가 없는 에드온 번호를 가리키면 그 무기나 오브젝트는 놓이지 않고 경고가 남습니다.
- 에드온 스코프의 AI 값은 같은 번호의 에드온 `aiScopeData`를 씁니다 (기본 데이터처럼 1 을 더하지 않습니다).
- 기본 데이터의 목록은 각각 10000 개 미만, 맵팩의 목록은 각각 10000 개 미만이어야 합니다.
- 원본 MIF 의 추가 오브젝트(addon-object)는 MIF2 에 없습니다. 미션의 에드온 오브젝트 데이터(20000)로 적습니다. 변환 도구가 자동으로 옮겨 줍니다.

## 스크립트 이벤트

원본의 이벤트는 열 가지(포인트 종류 10 에서 19 사이)뿐입니다. GodotXOPS 는 **종류 번호 20 이상을 스크립트로 만든 이벤트**로 씁니다. 기본으로 들어 있는 이벤트 묶음이 있고, 직접 만든 묶음을 설치하거나 미션에 넣어 올 수도 있습니다.

- 스크립트 이벤트는 **PD2 맵에서만** 동작합니다. PD1 맵은 원본과 똑같이 진행됩니다.
- PD2 에 20 이상의 종류가 있는데 그 번호를 등록한 묶음이 없으면 미션을 로드하지 않습니다.
- 스크립트는 [Godot Sandbox](https://github.com/libriscv/godot-sandbox) 의 SafeGDScript(`.sgd`)로 쓰고, 게임과 격리된 채로 실행됩니다. 파일을 읽고 쓰거나 다른 프로그램을 실행하거나 네트워크를 쓸 수 없습니다. 게임이 내준 함수(아래 [API](#api))로만 게임을 조작합니다.
- `config.json` 의 `General` 에 있는 `AllowEventScript` 를 `"false"` 로 바꾸면 스크립트 이벤트를 쓰는 미션은 로드되지 않습니다. 기본값은 `"true"` 입니다. 이 설정은 OPTION 화면에 없습니다.

### 이벤트가 진행되는 방식

이벤트는 **여러 줄이 동시에 따로 진행됩니다.** 줄마다 "지금 어느 이벤트 포인트에 있는지"를 하나씩 갖고, 서로 기다려 주지 않습니다. 줄 수와 줄마다의 시작 번호는 PD2 가 정합니다 ([이벤트 시작 번호](#이벤트-시작-번호)). 줄 번호는 0 부터입니다.

이벤트 포인트는 `[종류][P2][P3][식별번호]` 와 추가 파라미터로 이루어집니다. 원본 이벤트와 마찬가지로 P3 이 다음 이벤트 포인트의 식별번호입니다. 스크립트 이벤트는 여기에 두 가지가 더해집니다.

- **출구가 여럿일 수 있습니다.** 분기 이벤트는 조건에 따라 서로 다른 번호로 넘어갑니다. 첫째 출구는 P3 이고 나머지는 추가 파라미터 칸에 적습니다.
- **미션 변수**가 있습니다. 번호로 가리키는 정수이고 미션을 시작할 때 전부 0 입니다. 한 줄이 변수를 바꾸면 다른 줄이 그 값을 기다리거나 그 값으로 분기할 수 있습니다. "A 와 B 가 둘 다 끝나면"이나 "둘 중 하나라도 끝나면" 같은 조건을 이것으로 만듭니다.

반복은 이벤트끼리 고리를 만들면 됩니다 (마지막 이벤트의 다음 번호를 앞의 이벤트로). 기다림 없이 도는 고리는 1/33초에 한 바퀴씩 돕니다.

### 기본 제공 이벤트

`godotdata/event/base.json` 과 `base.sgd` 에 들어 있습니다. 사람은 원본 이벤트처럼 **식별번호**로 가리킵니다. 없는 사람을 가리키면 기다리는 이벤트는 계속 기다리고, 동작 이벤트는 아무 일도 하지 않습니다. "칸"은 그 값이 포인트의 어디에 들어가는지입니다 (`e0` 부터가 추가 파라미터).

**기다리는 이벤트** — 조건이 될 때까지 그 줄이 머뭅니다.

| 번호 | 이름 | 파라미터 (칸) | 넘어가는 조건 |
|---|---|---|---|
| 20 | Wait Variable | 변수(P2), 비교(e0), 값(e1) | 변수가 비교를 만족할 때 |
| 21 | Wait Team Alive | 팀(P2), 인원(e0) | 그 팀의 살아 있는 사람이 인원 이하일 때. 인원을 0 으로 두면 전멸 |
| 22 | Wait Area | 사람(P2), 반경(e0, 실수, 미터) | 사람이 이 포인트의 반경 안에 있을 때. 원본 13번은 반경이 2.5 m 로 고정입니다 |
| 23 | Wait HP | 사람(P2), 체력(e0, 실수) | 사람의 체력이 값 이하일 때 |
| 24 | Wait Weapon | 사람(P2), 무기(e0) | 사람이 그 무기를 손에 들고 있을 때 |
| 25 | Wait Ticks | 틱 수(P2) | 그만큼 지났을 때. 1초가 약 33틱입니다. 원본 17번은 초 단위입니다 |
| 26 | Wait Interact | 사람(P2), 반경(e0, 실수), 각도(e1, 실수), 안내 메시지(e2), 누르는 시간(e3, 실수, 초) | 아래 설명 |

비교는 숫자로 적습니다: 0 같다, 1 다르다, 2 작다, 3 작거나 같다, 4 크다, 5 크거나 같다.

**26 Wait Interact** 는 플레이어가 Interact 키(기본 F)를 눌러야 넘어가는 이벤트입니다.

- 사람이 이 포인트의 반경 안에 있고, 시선이 이 포인트에서 각도 이내로 벗어나 있을 때만 조건이 됩니다. 각도는 눈에서 조준 방향과 포인트 사이의 각도(도)입니다. 180 이상을 적으면 방향을 보지 않습니다.
- 조건이 되는 동안 조준선 아래에 안내가 `char.dds` 글자로 뜹니다. 안내 메시지에 `.msg` 의 줄 번호를 적으면 그 문구를, -1 을 적으면 `PRESS F` 를 씁니다.
- 문구 안의 `{interact}` 는 지금 설정된 키 이름으로 바뀝니다 (`OPEN: {interact}` → `OPEN: F`). `char.dds` 로 그리므로 영문, 숫자, 기호만 쓸 수 있고 그 밖의 글자는 `?` 로 나옵니다.
- 누르는 시간(초)을 적으면 그만큼 키를 **누르고 있어야** 넘어갑니다. 0 이면 누르는 순간 넘어갑니다 (적지 않으면 0). 도중에 키를 떼거나 반경을 벗어나거나 시선을 돌리면 처음부터 다시 셉니다. 기본 안내는 `HOLD F` 이고, 누르고 있는 동안 문구 뒤에 진행이 `50%` 처럼 붙습니다 (안내 메시지를 적었어도 붙습니다).
- 플레이어가 조작하는 사람만 됩니다. AI 는 이 키를 누르지 않습니다.
- Wait Interact 둘이 동시에 조건이 되면 안내가 서로 덮어씁니다. 반경이 겹치지 않게 놓습니다.

**사람의 상태를 고치는 이벤트** — 바로 실행하고 다음으로 넘어갑니다. 치료, 방탄복, 헬멧 같은 재정비를 만드는 데 씁니다 (Wait Interact 나 Wait Area 뒤에 잇습니다).

| 번호 | 이름 | 파라미터 (칸) | 하는 일 |
|---|---|---|---|
| 30 | Set HP | 사람(P2), 값(e0, 실수) | 체력을 그 값으로 정합니다. 0 이하면 죽습니다 |
| 31 | Add HP | 사람(P2), 양(e0, 실수), 상한(e1, 실수) | 체력에 더합니다 (음수면 뺍니다) |
| 32 | Set Armor | 사람(P2), 값(e0, 실수) | 방어구 포인트를 정합니다. 0 이하면 방어구가 없어집니다 |
| 33 | Add Armor | 사람(P2), 양(e0, 실수), 상한(e1, 실수) | 방어구 포인트에 더합니다 |
| 34 | Set Helmet | 사람(P2), 값(e0, 실수) | 헬멧 포인트를 정합니다 |
| 35 | Add Helmet | 사람(P2), 양(e0, 실수), 상한(e1, 실수) | 헬멧 포인트에 더합니다 |

- 사람이 -1 이면 그 순간에 있는 전원입니다. 죽은 사람은 바뀌지 않습니다 (되살리지 않습니다).
- **상한**은 더할 때 넘지 않을 값입니다: 0 보다 크면 그 값, 0 이면 그 사람이 처음에 가진 값(`human/list.json` 의 `hp` / `armor` / `helmet`), 0 보다 작으면 상한이 없습니다. 이미 상한을 넘어 있는 값은 끌어내리지 않습니다. 뺄 때는 상한을 보지 않고 0 에서 멈춥니다.
- 방어구가 없는 사람(`armor` 가 0)에게 방탄복을 주려면 상한을 직접 적습니다. 상한을 0 으로 두면 처음 값이 0 이라 더해지지 않습니다.
- 포인트만으로는 데미지가 줄지 않습니다. 줄이는 비율은 그 사람의 `armorDamageDecrease` / `helmetDamageDecrease` 입니다 ([방어구와 헬멧](#방어구와-헬멧)). 비율이 0 인 사람도 포인트가 있으면 총알이 뚫고 나가는 데 관통력을 하나 더 씁니다.
- 무적과 방어구를 거치지 않고 값을 바로 고칩니다. 46 Damage Human 은 무적인 사람에게 듣지 않습니다.

**동작 이벤트** — 바로 실행하고 다음으로 넘어갑니다. 스폰·이동·이펙트는 **그 이벤트 포인트가 놓인 자리와 방향**을 씁니다.

| 번호 | 이름 | 파라미터 (칸) | 하는 일 |
|---|---|---|---|
| 40 | Set Variable | 변수(P2), 값(e0) | 변수에 값을 넣습니다 |
| 41 | Add Variable | 변수(P2), 값(e0) | 변수에 값을 더합니다 (음수면 뺍니다) |
| 42 | Spawn Human | 사람 정보(P2), 새 식별번호(e0), 경로(e1) | 사람을 새로 세웁니다. 아래 설명 |
| 43 | Spawn Weapon | 무기(P2), 탄 수(e0) | 떨어진 무기를 놓습니다 |
| 44 | Spawn Object | 오브젝트(P2), 새 식별번호(e0), 바닥에 붙이기(e1, 참·거짓) | 오브젝트를 놓습니다 |
| 45 | Kill Human | 사람(P2) | 사람을 죽입니다 |
| 46 | Damage Human | 사람(P2), 피해량(e0, 실수) | 체력을 깎습니다 |
| 47 | Move Human | 사람(P2) | 사람을 이 포인트의 자리로 옮깁니다 |
| 48 | Set Team | 사람(P2), 팀(e0) | 팀을 바꿉니다. 원본 19번은 팀 0 으로만 바꿉니다 |
| 49 | Set Path Mode | 경로 포인트(P2), 모드(e0) | 경로 포인트의 이동 모드를 바꿉니다. 아래 설명 |
| 50 | Destroy Object | 오브젝트(P2) | 오브젝트를 부숩니다 |
| 51 | Play Effect | 이펙트(P2) | 이펙트를 냅니다 (`effect_data.json` 의 `effectData` 번호) |
| 52 | Set Auto Judge | 0 또는 1 (P2) | 자동 판정을 끄거나(0) 켭니다(1). 아래 설명 |
| 53 | Give Weapon Primary | 사람(P2), 무기(e0), 탄 수(e1) | 주 무기 슬롯(미션을 시작할 때 들고 있는 슬롯)의 무기를 바꿉니다 |
| 54 | Give Weapon Secondary | 사람(P2), 무기(e0), 탄 수(e1) | 보조 무기 슬롯의 무기를 바꿉니다 |
| 55 | Tween Object | 오브젝트(P2), 경로 포인트(e0), 방향(e1, 실수), 시간(e2, 실수, 초), 부드럽게(e3, 참·거짓) | 오브젝트를 그 방향으로 바로 돌린 뒤, 시간에 걸쳐 경로 포인트(종류 3)의 자리로 움직입니다. 아래 설명 |
| 56 | Play Sound | 칸(P2), 소리(e0), 볼륨(e1, 실수), 되풀이(e2, 참·거짓), 어디서나(e3, 참·거짓) | [소리 목록](#이벤트의-소리)의 소리를 칸에 재생합니다. 아래 설명 |
| 57 | Stop Sound | 칸(P2) | 그 칸의 소리를 멈춥니다. -1 이면 모든 칸 |
| 58 | Move Block | 블록(P2), dx·dy·dz(e0 에서 e2, 실수, m), pitch·roll·turn(e3 에서 e5, 실수, 도), 시간(e6, 실수, 초), 부드럽게(e7, 참·거짓) | 블록을 시간에 걸쳐 옮기고 돌립니다. 아래 설명 |
| 59 | Toggle Block | 블록(P2), 켜기(e0, 참·거짓) | 거짓이면 블록을 끕니다: 그려지지 않고 사람·총알·시야 어느 판정에도 걸리지 않습니다. 참이면 원래대로 돌아옵니다 |

**Tween Object** 는 순간이동이 아니라 시간에 걸쳐 움직입니다.

- 방향은 오브젝트 포인트의 방향과 같은 값(도)이고, 움직이기 시작할 때 바로 바뀝니다. 0 보다 작으면 경로 포인트의 방향을 씁니다.
- 지금 자리에서 도착점까지 직선으로 갑니다. 블록과 사람을 무시하고 지나갑니다. "부드럽게"를 켜면 천천히 출발해 천천히 멈추고, 끄면 같은 속도로 갑니다.
- 이벤트 줄은 기다리지 않고 바로 다음으로 넘어갑니다. 도착할 때까지 기다리려면 뒤에 Wait Ticks 를 둡니다 (1초가 약 33틱).
- 움직이는 동안에도 총알에 맞고 부서집니다. 부서지면 그 자리에서 멈춥니다. 움직이는 중에 다시 걸면 그 순간의 자리에서 새로 시작합니다.
- 오브젝트 위에 선 사람은 같이 움직이지 않습니다 (사람과 오브젝트는 서로 부딪치지 않습니다). 문이나 장애물의 연출용입니다.

**Move Block** 과 **Toggle Block** 은 블록을 번호로 가리킵니다. 번호는 BD2 파일 안의 순번(0 부터)이고, 에디터의 블록 모드에서 블록을 선택하면 오른쪽 칸에 나옵니다. 에디터에서 블록을 지우면 그 뒤의 번호가 하나씩 당겨지므로, 이벤트를 건 뒤에는 블록을 지우지 않는 것이 안전합니다.

- 이동량과 각도는 **파일에 적힌 처음 모양 기준**입니다. 같은 블록에 다시 걸어도 쌓이지 않고, 전부 0 을 주면 처음 자리로 돌아옵니다. 블록은 자기 가운데 둘레로 돕니다. turn 이 세로축 둘레의 회전(yaw)입니다.
- 지금 모양에서 새 모양까지 시간에 걸쳐 갑니다. 시간이 0 이하면 바로 옮깁니다. 각도는 적은 그대로 돌므로 180° 를 넘는 회전이나 여러 바퀴(720 등)도 됩니다.
- 이벤트 줄은 기다리지 않고 바로 넘어갑니다. 한 번에 블록 하나이고, 블록 여러 개로 된 문은 이벤트를 이어서 겁니다 (같은 틱에 전부 시작합니다).
- 판정(사람, 총알, 시야)이 블록을 따라갑니다. 다른 블록은 무시하고 지나갑니다.
- **사람은 블록과 함께 움직이지 않습니다.** 올라가는 블록 위의 사람이나 블록에 덮인 사람은 게임의 평소 충돌 처리가 다룹니다 (따로 밀거나 싣지 않습니다). 옆으로 가는 블록은 위의 사람을 싣고 가지 못합니다.
- AI 는 경로 포인트로만 다니므로 블록이 길을 막아도 돌아가지 않습니다. 이미 남은 탄흔은 블록을 따라가지 않습니다.
- 미션을 다시 시작하면 옮기거나 끈 블록이 전부 처음으로 돌아갑니다.

**Play Sound** 는 칸에 소리 하나를 재생합니다. 칸은 0 에서 15 까지이고, 같은 칸에 다시 재생하면 앞의 소리가 끊깁니다.

- 볼륨은 0 에서 1 사이이고, 0 이하로 두면 1 입니다.
- "되풀이"를 켜면 Stop Sound 로 멈추거나 미션이 끝날 때까지 되풀이합니다.
- "어디서나"를 끄면 이 포인트의 자리에서 나고 멀어질수록 작아집니다 (다른 효과음과 같이 33.5 m 에서 들리지 않습니다). 켜면 어디서나 같은 크기로 들립니다 (배경음, 방송).
- 목록에 없는 번호나 읽지 못한 파일은 소리 없이 넘어가고 디버그 콘솔에 경고를 남깁니다. 이벤트 줄은 멈추지 않습니다.
- 이 소리는 AI 가 듣지 않습니다.

- **42 Spawn Human**: 맵을 로드할 때와 같은 방식으로 만듭니다. 맵에 사람 정보 포인트 `[4][사람 데이터][팀][식별번호]` 를 놓아 두고, 이 이벤트의 P2 에 그 식별번호를 적습니다 (사람 포인트의 P2 에 적는 그 번호입니다). 맵에 세우지 않을 사람의 정보 포인트만 따로 놓아 둘 수 있습니다. 경로에는 처음 향할 경로 포인트의 식별번호를 적고, 없는 번호를 적으면 그 자리에 서 있습니다. 한 맵의 사람은 96명까지입니다.
- **49 Set Path Mode**: AI 경로 포인트 `[3][모드][다음][식별번호]` 의 모드 칸을 미션 도중에 바꿉니다. 그 포인트에 서 있거나 앞으로 도착할 AI 가 새 모드로 움직입니다. 원본 14번이 모드를 걷기로만 바꾸는 것의 확장입니다. 대기 포인트에 세워 둔 적을 달리기로 바꿔 뛰쳐나오게 하거나, 순찰 경로의 한 포인트를 대기로 바꿔 멈추게 할 수 있습니다. 랜덤 분기 포인트(종류 8)는 바꾸지 않습니다.

  | 모드 | 뜻 |
  |---|---|
  | 0 | 걷기 |
  | 1 | 달리기 |
  | 2 | 대기 |
  | 3 | 추적 |
  | 4 | 경계 대기 (경계가 끝나면 다음 포인트로) |
  | 5 | 5초 정지 |
  | 6 | 수류탄 투척 |
  | 7 | 우선적 달리기 (전투 중에도 경로를 따름) |

- **52 Set Auto Judge**: 원본은 적이 전멸하면 클리어, 플레이어가 죽으면 실패로 자동 판정합니다. 끄면 이벤트(10번, 11번)만이 미션을 끝냅니다. 적을 도중에 증원하는 미션은 처음 적이 전멸하는 순간 끝나 버리므로 꺼 두어야 합니다. 미션을 시작할 때는 늘 켜져 있습니다.
- **53, 54**: 지금 어느 슬롯을 들고 있는지는 바꾸지 않습니다. 탄 수는 전체 탄 수이고, -1 을 적으면 사람 종류의 기본값입니다.

**흐름 이벤트**

| 번호 | 이름 | 파라미터 (칸) | 출구 (칸) |
|---|---|---|---|
| 60 | Branch Variable | 변수(P2), 비교(e0), 값(e1) | 참(P3), 거짓(e2) |
| 61 | Branch Random | 확률 %(P2) | 적중(P3), 빗나감(e0) |
| 62 | Random Exit | 갈래 수(P2, 2 에서 4 사이) | 갈래마다 하나 (P3, e0, e1, e2) |
| 63 | Start Line | 줄 번호(P2), 시작할 이벤트(e0) | 다음(P3) |
| 64 | Stop Line | 줄 번호(P2) | 다음(P3) |

- **62 Random Exit**: 다음 이벤트를 여러 갈래 중에서 같은 확률로 고릅니다. 갈래 수가 3 이면 P3, e0, e1 에 적은 번호 중 하나로 넘어갑니다. 증원이 세 방향 중 한 곳에서 나오게 할 때 씁니다.
- **64 Stop Line**: 다른 줄을 멈춥니다. 멈춘 줄은 그 자리에서 더 진행하지 않습니다. 예: 줄 1 이 "60초 뒤 미션 실패"이고, 줄 0 이 폭탄 해체에 성공하면 줄 1 을 멈춥니다.
- **63 Start Line**: 다른 줄을 지정한 이벤트에서 진행시킵니다. 멈춰 있던 줄도 풀립니다. 예: 줄 2 를 없는 번호에서 출발시켜 쉬게 두었다가, 경보가 울리는 시점에 줄 2 를 증원 이벤트에서 시작시킵니다. 이 이벤트를 실행한 줄은 자기 흐름을 계속 갑니다.

**화면 글자 이벤트**

화면에는 글자 칸이 32개(0 에서 31 사이) 있고, 이벤트가 칸마다 글자를 놓거나 지웁니다. 나중에 놓은 글자가 위에 그려집니다. 미션이 끝나거나 다시 시작되면 전부 지워집니다. 31번 칸은 Wait Interact 의 안내가 씁니다.

| 번호 | 이름 | 파라미터 (칸) | 하는 일 |
|---|---|---|---|
| 70 | Show Text | 칸(P2), 메시지(e0), 배치(e1 에서 e6), 시간(e7, 실수, 초) | `.msg` 의 문구를 화면의 원하는 자리에 놓습니다. 원본 18번의 확장입니다 |
| 71 | Clear Text | 칸(P2). -1 이면 전부 | 글자를 지웁니다 |
| 72 | Countdown | 칸(P2), 초(e0), 형식(e1), 변수(e2), 배치(e3 에서 e8) | 그 줄이 머무는 동안 남은 시간을 표시하며 세어 내려가고, 0 이 되면 넘어갑니다. 남은 초를 변수에 넣어 줍니다 (-1 이면 넣지 않음) |
| 73 | Show Variable | 칸(P2), 변수(e0), 형식(e1), 배치(e2 에서 e7), 시간(e8, 실수, 초) | 변수의 지금 값을 놓습니다 |

"배치"는 연속한 여섯 칸입니다.

| 순서 | 값 | 뜻 |
|---|---|---|
| 1 | 글꼴 | 0 은 OS 글꼴(한글 등 어느 글자든), 1 은 `char.dds` (영문, 숫자, 기호만) |
| 2 | 기준점 | 화면을 3×3 으로 나눈 점. 0 왼쪽 위, 1 위 가운데, 2 오른쪽 위, 3 왼쪽 가운데, 4 화면 가운데, 5 오른쪽 가운데, 6 왼쪽 아래, 7 아래 가운데, 8 오른쪽 아래 |
| 3 | x (실수) | 기준점에서 오른쪽으로 얼마나 |
| 4 | y (실수) | 기준점에서 위쪽으로 얼마나 (아래로 내리려면 음수) |
| 5 | 크기 (실수) | 글자 높이 |
| 6 | 색 | 0xRRGGBB 를 정수로 (흰색 16777215, 빨강 16711680) |

- x, y, 크기는 화면 높이를 480 으로 본 값입니다. 해상도가 달라도 같은 자리에 같은 비율로 나옵니다.
- 글자는 기준점 쪽으로 정렬됩니다. 기준점이 오른쪽 위(2)면 글자의 오른쪽 위 모서리가 그 점에 놓입니다.
- 시간이 0 이면 지울 때까지 남습니다.
- 형식은 0 이 숫자 그대로, 1 이 `분:초` 입니다 (65 → `1:05`).
- Countdown 을 다른 줄에서 Stop Line 으로 멈추면 글자는 저절로 사라집니다.
- 디버그 콘솔의 `hudtext` 명령으로 자리를 미리 맞춰 볼 수 있습니다 ([개발 문서](development.md#디버그-콘솔)).

**화면 이미지 이벤트**

화면에는 이미지 칸이 16개(0 에서 15 사이) 있고, 이벤트가 칸마다 [이미지 목록](#이미지-목록)의 이미지를 놓거나 지웁니다. 글자 칸과 따로이고, **이미지는 글자와 이벤트 메시지 아래에** 그려집니다. 이미지끼리는 나중에 놓은 것이 위입니다. HUD 를 꺼도(Show HUD) 남고, 미션이 끝나거나 다시 시작되면 전부 지워집니다.

| 번호 | 이름 | 파라미터 (칸) | 하는 일 |
|---|---|---|---|
| 74 | Show Image | 칸(P2), 이미지(e0), 기준점(e1), x(e2, 실수), y(e3, 실수), 너비(e4, 실수), 높이(e5, 실수), 진하기(e6, 실수), 시간(e7, 실수, 초), 페이드(e8, 실수, 초) | 이미지를 화면의 원하는 자리에 놓습니다 |
| 75 | Clear Image | 칸(P2). -1 이면 전부, 페이드(e0, 실수, 초) | 이미지를 지웁니다. 페이드를 주면 그 시간에 걸쳐 사라집니다 |

- 기준점과 x, y 는 글자의 배치와 같습니다 (기준점 0 에서 8, 화면 높이 480 기준, +x 오른쪽, +y 위).
- 너비와 높이도 화면 높이 480 기준입니다. 둘 다 0 이면 이미지의 픽셀 크기 그대로, 하나만 0 이면 이미지의 비율에 맞춥니다. 화면을 세로로 꽉 채우려면 기준점 4, 높이 480, 너비 0 으로 둡니다.
- 진하기는 0 에서 1 이고, 0 으로 두면 1(다 보임)로 칩니다.
- 시간이 0 이면 지울 때까지 남습니다.
- 페이드는 서서히 나타나는 시간이고, 시간을 정한 이미지는 끝날 때도 같은 시간에 걸쳐 사라집니다. 0 이면 바로 나타납니다.
- 같은 칸에 다시 놓으면 그림과 자리만 바뀌고 그리는 순서는 그대로입니다.

**연출 이벤트** (1.2.1)

컷신을 만드는 이벤트입니다. 전부 그 줄이 기다리지 않고 바로 다음으로 넘어갑니다. 끝날 때까지 기다리려면 뒤에 Wait Ticks 를 둡니다 (1초가 약 33틱). 미션을 다시 시작하면 전부 처음 상태로 돌아갑니다.

| 번호 | 이름 | 파라미터 (칸) | 하는 일 |
|---|---|---|---|
| 80 | Pause World | 켜기(e0, 참·거짓) | 게임을 멈추거나 다시 돌립니다. 아래 설명 |
| 81 | Pause AI | 켜기(e0, 참·거짓) | 모든 AI 를 멈추거나 다시 돌립니다. 멈춘 사람은 그 자리에 서 있습니다 |
| 82 | Lock Player | 이동(e0), 점프(e1. 앉기도 함께 막습니다), 시점(e2), 발사(e3), 무기(e4), 시점 전환(e5). 전부 참·거짓 | 켠 칸의 조작을 막습니다. 전부 끄면 다 풀립니다. 아래 설명 |
| 83 | Set Invincible | 사람(P2), 켜기(e0, 참·거짓) | 체력이 깎이지 않게 합니다. 사람이 -1 이면 그 순간에 있는 전원 |
| 84 | Set Infinite Ammo | 사람(P2), 켜기(e0, 참·거짓) | 한 발 쏠 때마다 예비 탄이 한 발 늘어서 탄이 떨어지지 않습니다. 재장전은 그대로 합니다. 사람이 -1 이면 그 순간에 있는 전원 |
| 85 | Letterbox | 켜기(e0, 참·거짓), 높이(e1, 실수), 시간(e2, 실수, 초) | 화면 위아래에 검은 띠를 넣거나 뺍니다. 높이는 띠 하나의 높이(화면 높이 480 기준)이고 0 이하면 60 입니다 |
| 86 | Show HUD | 켜기(e0, 참·거짓) | HUD 를 켜거나 끕니다. 아래 설명 |
| 87 | Fade Screen | 색(e0), 진하기(e1, 실수), 시간(e2, 실수, 초) | 화면을 한 색으로 덮거나 걷습니다. 진하기 1 이 완전히 덮인 것, 0 이 걷힌 것입니다. 지금의 진하기에서 새 진하기로 갑니다 |
| 88 | Detach Camera | pitch(e0, 실수), roll(e1, 실수), 시야각(e2, 실수) | 카메라를 플레이어에게서 떼어 **이 포인트의 자리**에, 포인트의 방향을 보게 놓습니다. 아래 설명 |
| 89 | Attach Camera | 없음 | 카메라를 플레이어에게 다시 붙입니다 |
| 90 | Tween Camera | pitch(e0), roll(e1), 시야각(e2), 시간(e3, 초). 전부 실수. 부드럽게(e4, 참·거짓) | 뗀 카메라를 시간에 걸쳐 **이 포인트의 자리와 방향**으로 옮깁니다 |
| 91 | AI Look At | 사람(P2) | 사람이 **이 포인트의 자리**를 바라봅니다 (좌우와 위아래 모두). 아래 설명 |
| 92 | AI Fire At | 사람(P2), 발 수(e0) | 사람이 이 포인트의 자리를 겨누고 그 발 수만큼 쏩니다 (0 이하면 한 발) |
| 93 | AI Release | 사람(P2) | 91, 92 의 지시를 풉니다. -1 이면 전원 |
| 94 | Tween Block | 블록(P2), 경로 포인트(e0), pitch·roll·turn(e1 에서 e3, 실수, 도), 시간(e4, 실수, 초), 부드럽게(e5, 참·거짓) | 블록의 가운데가 경로 포인트(종류 3)의 자리에 오도록 옮깁니다. 회전은 Move Block 처럼 처음 모양 기준입니다 |
| 95 | Set Player | 사람(P2) | 플레이어가 조작하는 사람을 그 사람으로 바꿉니다. 아래 설명 |

**구역, 날씨, 하늘** (1.3.0) — 100 과 101 은 조건이 될 때까지 기다리고, 나머지는 바로 넘어갑니다.

| 번호 | 이름 | 파라미터 (칸) | 하는 일 |
|---|---|---|---|
| 100 | Wait Area Enter | 사람(P2), 구역(e0) | 그 사람(없는 식별번호면 플레이어)의 발이 [구역](#구역) 안에 들어오기를 기다립니다 |
| 101 | Wait Area Leave | 사람(P2), 구역(e0) | 그 사람(없는 식별번호면 플레이어)이 구역 밖으로 나가기를 기다립니다 |
| 102 | Set Area | 구역(P2), 성질(e0), 값(e1, 실수) | 구역의 성질 하나를 바꿉니다: 0 데미지(초당 HP), 1 중력(배율), 2 AI 시야(배율), 3 울림(0 에서 1 사이), 4 날씨 막기(0 이 아니면 켬). 미션을 다시 시작하면 처음 값입니다 |
| 103 | Set Area Hostility | 구역(P2), 팀(e0), 상대 팀(e1), 방식(e2) | 구역 안에서 그 팀의 AI 가 상대 팀을 대하는 방식: 0 늘 적대, 1 적대 안 함, 2 무기를 든 상대를 보거나 공격받으면 적대 |
| 120 | Set Weather | 날씨(P2) | [날씨](#날씨)를 바로 바꿉니다. 바뀌는 순간을 감추려면 앞에 Fade Screen(87)을 둡니다 |
| 121 | Set Sky | 하늘(P2) | 하늘을 바로 바꿉니다. 안개 색도 새 하늘의 것이 됩니다 (날씨가 안개를 덮어쓰면 그대로) |

- **95 Set Player**: 지금까지 조작하던 사람은 AI 가 이어받고, 새로 조작하게 된 사람의 AI 는 멈춥니다. 카메라와 HUD 는 새 사람을 따라갑니다. 팀, 무기, 체력, 경로는 두 사람 모두 그대로입니다. 없는 사람이나 죽은 사람을 가리키면 아무 일도 없습니다. **자동 판정은 새 사람을 기준으로 합니다**: 새 사람과 다른 팀이 전멸하면 클리어, 새 사람이 죽으면 실패입니다. 원본 이벤트의 사람 식별번호(예: 12번 "사람이 죽기를 기다림")는 바뀌지 않으므로, 식별번호 0 을 "플레이어"로 쓰던 이벤트는 여전히 처음의 그 사람을 가리킵니다.
- **80 Pause World**: 사람, 총알, 떨어진 무기, 오브젝트, 움직이는 블록, AI, 이펙트가 그 자리에 멈춥니다. 플레이어도 아무 조작을 할 수 없습니다 (나가기와 Interact 는 됩니다). **이벤트는 계속 진행됩니다**: Wait Ticks 가 시간을 세고, 화면 글자·암전·레터박스·뗀 카메라가 움직입니다. 이미 재생된 소리는 끝까지 납니다.
- **82 Lock Player**: "무기"는 재장전, 무기 바꾸기, 버리기, 단발·연발 전환, 스코프입니다. "시점 전환"은 1인칭·3인칭 키(F1)입니다. 나가기(ESC)와 Interact 는 어느 칸으로도 막히지 않습니다.
- **86 Show HUD**: 체력·탄약·무기 표시, 조준선, 스코프, 재장전 문구, 피격 번쩍임이 사라집니다. **이벤트 메시지(원본 18번)와 이벤트가 놓은 글자(70번대)는 남습니다.** 컷신의 자막으로 쓸 수 있습니다. 글자까지 지우려면 Clear Text 를 씁니다. 미션이 끝나면 HUD 는 다시 보입니다.
- **85, 87 의 그리는 순서**: 아래에서부터 게임 화면 → 암전(87) → 레터박스(85) → HUD → 이벤트 메시지와 글자 → 미션을 시작하고 끝낼 때의 암전입니다. 암전이 HUD 아래이므로, 화면을 완전히 가리려면 Show HUD 로 HUD 도 꺼야 합니다.
- **88 Detach Camera**: pitch 는 아래를 볼수록 양수이고, 시야각이 0 이하면 설정의 시야각입니다. 카메라가 떨어져 있는 동안에는 플레이어의 몸이 보이고 조준선과 스코프가 그려지지 않습니다. **조작, HUD, 무적은 건드리지 않습니다.** 컷신 동안 플레이어를 세워 두려면 Lock Player 를, HUD 를 감추려면 Show HUD 를 함께 씁니다. 카메라는 블록을 무시합니다.
- **90 Tween Camera**: 카메라가 붙어 있으면 아무 일도 하지 않습니다 (먼저 88 로 뗍니다). 방향은 가까운 쪽으로 돕니다. 시야각을 설정값(0)에서 다른 값으로 바꾸거나 그 반대로 바꾸면 그 사이는 보간하지 않고 바로 바뀝니다.
- **91 AI Look At, 92 AI Fire At**: 걸려 있는 동안 그 사람은 평소의 AI(적 찾기, 경로 이동, 무기 관리)를 하지 않고 제자리에서 그 점만 봅니다. 도는 속도는 AI 의 평소 회전 속도입니다. 사격은 평소처럼 조준 오차와 반동이 들어가고, 탄창이 비면 재장전하며, 다 쏜 뒤에는 계속 바라봅니다. Pause AI 가 켜져 있어도 이 지시는 듣습니다. **플레이어에게도 걸 수 있고**, 그동안 플레이어는 조작할 수 없습니다. 죽은 사람에게는 걸리지 않습니다.
- **94 Tween Block**: 블록의 가운데는 꼭짓점 8개의 평균입니다. 그 밖의 것(충돌 무시, 사람은 같이 움직이지 않음)은 아래의 Move Block 과 같습니다.

시작 장면의 예입니다: `[87 검은색, 진하기 1, 0초] → [82 전부 잠금] → [86 HUD 끄기] → [88 카메라 떼기] → [85 레터박스] → [87 진하기 0, 1초] → [90 카메라 옮기기 3초] → [25 Wait Ticks 100] → [85 레터박스 끄기] → [89 카메라 붙이기] → [86 HUD 켜기] → [82 전부 풀기]`.

폭탄 타이머의 예입니다. 줄 1: `[20 폭탄을 발견했다는 변수를 기다림] → [72 Countdown 60초, 오른쪽 위] → [11 미션 실패]`. 줄 0: `[26 폭탄 앞에서 Interact] → [64 줄 1 멈추기] → [10 미션 클리어]`.

### 묶음 만들기

묶음 하나는 등록 파일(JSON) 하나와 스크립트(`.sgd`) 하나입니다.

- **설치형 묶음**: 등록 파일을 `godotdata/event/` 에 넣습니다. 그 폴더의 `.json` 을 파일 이름 순서로 전부 읽습니다. 종류 번호는 **20 에서 9999 사이**입니다. 20 에서 139 사이는 기본 제공 묶음이 쓰므로 (앞으로 더할 자리를 남겨) 200 부터 쓰는 것을 권합니다.
- **맵팩의 묶음**: [맵팩](#맵팩) Manifest 의 `eventDataPath` 에 등록 파일의 경로를 적습니다. 종류 번호는 **10000 에서 19999 사이**입니다. 그 맵팩의 미션을 하는 동안에만 적용됩니다. 미션(MIF2)은 이벤트 묶음을 들고 오지 않습니다.
- 같은 번호를 두 군데서 등록하면 그 번호를 쓰는 미션은 로드되지 않습니다.
- 미션이 실제로 쓰는 종류가 들어 있는 묶음의 스크립트만 읽습니다.

```json
{
  "scriptPath": "addon/mypack/events.sgd",
  "events": [
    {
      "type": 10000,
      "name": "Wait Enemies Near",
      "function": "wait_enemies_near",
      "parameters": [
        { "name": "team", "slot": "p2", "kind": "team" },
        { "name": "radius", "slot": "e0", "kind": "float" }
      ],
      "exits": [
        { "name": "next", "slot": "p3", "kind": "event" }
      ]
    }
  ]
}
```

| 키 | 뜻 |
|---|---|
| `scriptPath` | 스크립트 파일. 게임 폴더 기준 경로이고 확장자는 `.sgd` 여야 합니다. 256 KB 까지 |
| `type` | 포인트 종류 번호 |
| `name` | 이벤트의 이름 (디버그 콘솔의 `event` 와 맵 에디터에 나옵니다) |
| `function` | 스크립트 안의 함수 이름 |
| `parameters` | 스크립트가 받을 값들. `name` 이 스크립트에서 쓰는 이름, `slot` 이 포인트의 칸, `kind` 가 형식입니다 |
| `exits` | 출구들. 함수가 돌려준 번호(0 부터)의 출구가 가리키는 칸의 값이 다음 이벤트의 식별번호입니다. 비워 두면 P3 하나입니다 |

- `slot`: `p2`(원본의 P2), `p3`(원본의 P3), `e0` 부터는 PD2 의 추가 파라미터 칸입니다.
- `kind`: `int`(정수), `float`(실수), `bool`(참·거짓)만 게임이 구분합니다. 실수와 참·거짓은 추가 파라미터 칸에서만 됩니다. 그 밖의 값(`human`, `weapon`, `message` 등)은 맵 에디터가 입력 칸을 고르는 데 쓰는 표시이고 게임은 정수로 읽습니다.
- 파라미터 이름으로 `id`, `x`, `y`, `z`, `yaw` 는 쓸 수 없습니다 (게임이 채워 넣는 이름입니다).

### 스크립트

SafeGDScript 는 GDScript 와 거의 같은 문법입니다. 묶음의 스크립트는 함수 두 종류를 갖습니다.

```gdscript
var api

# 미션을 로드할 때 한 번 불립니다. 게임이 내주는 함수 표를 받아 둡니다.
func init(a):
	api = a

# 이벤트마다 하나. 줄이 그 포인트에 있는 동안 1/33초마다 불립니다.
func wait_enemies_near(p, state):
	var count = 0
	for i in range(api["human_count"].call()):
		var h = api["human"].call(i)
		if h["alive"] and h["team"] == p["team"]:
			var dx = h["x"] - p["x"]
			var dz = h["z"] - p["z"]
			if dx * dx + dz * dz <= p["radius"] * p["radius"]:
				count += 1
	if count >= 3:
		return 0
	return -1
```

- **`p`** 는 그 포인트의 값입니다. 등록 파일의 `parameters` 에 적은 이름으로 꺼내고(`p["radius"]`), 포인트의 식별번호 `p["id"]`, 위치 `p["x"]` `p["y"]` `p["z"]`, 방향 `p["yaw"]` 가 늘 들어 있습니다.
- **`state`** 는 그 줄이 이 포인트에 머무는 동안 쓰는 저장 칸입니다. 줄이 다른 포인트로 넘어가면 비워집니다. 몇 틱째인지 세는 것 같은 일에 씁니다.
- **돌려주는 값**: `-1` 은 기다림(다음 틱에 다시 불립니다), `0` 이상은 그 번호의 출구로 나갑니다. 값을 돌려주지 않거나 정수가 아닌 값을 돌려주면 실패입니다.
- 스크립트 맨 위의 변수(`var`)는 모든 줄과 모든 포인트가 함께 씁니다. 줄마다 따로 가져야 하는 값은 `state` 에, 줄끼리 주고받을 값은 미션 변수에 둡니다.
- 좌표는 미터이고 디버그 콘솔의 `info` 와 `teleport` 가 쓰는 것과 같은 좌표입니다. 각도는 도이고, yaw 는 오른쪽으로 돌수록, pitch 는 아래를 볼수록 커집니다.
- 사람은 **인덱스**(사람 목록의 순서, 0 부터)로 가리킵니다. 포인트에 적는 식별번호와 다릅니다. `find_human(식별번호)` 로 인덱스를 얻습니다.

### API

`api["이름"].call(인자들)` 로 부릅니다.

**조회**

| 함수 | 돌려주는 것 |
|---|---|
| `human_count()` | 사람 수 |
| `human(인덱스)` | 사전: `alive`, `hp`, `armor`, `helmet`(남은 방어구 / 헬멧 포인트), `hp_max`, `armor_max`, `helmet_max`(사람 데이터에 적힌 처음 값), `team`, `x`, `y`, `z`, `yaw`, `pitch`, `weapon`(손에 든 무기 번호), `id`(식별번호), `invincible`, `infinite_ammo`, `directed`(`ai_look` / `ai_fire` 의 지시를 받는 중인지), `shots_left`(지시받은 사격에서 남은 발 수). 없는 인덱스면 빈 사전 |
| `find_human(식별번호)` | 그 식별번호의 첫 사람의 인덱스. 없으면 -1 |
| `player()` | 플레이어의 인덱스. 없으면 -1 |
| `team_alive(팀)` | 그 팀의 살아 있는 사람 수 |
| `look_angle(인덱스, x, y, z)` | 사람의 눈에서 조준 방향과 그 점 사이의 각도 (도) |
| `object(식별번호)` | 사전: `exists`, `destroyed`, `hp`, `x`, `y`, `z`, `moving`(움직이는 중인지) |
| `tick()` | 미션을 시작한 뒤 지난 틱 수 |
| `stage()` | 연출 상태의 사전: `paused`(게임 정지), `ai_paused`, `lock`(조작 잠금의 합), `hud`, `fade`(암전이 도착할 진하기), `fading`(암전이 바뀌는 중인지), `letterbox`(띠가 도착할 높이), `camera_detached`, `camera_moving` |
| `line_count()` | 이벤트 줄 수 |
| `get_var(번호)` | 미션 변수의 값 |
| `random(n)` | 0 이상 n 미만의 난수 |
| `interact_pressed()` | 이번 틱에 플레이어가 Interact 키를 눌렀는지 |
| `interact_held()` | 이번 틱에 플레이어가 Interact 키를 누르고 있는지 (꾹 눌러야 하는 이벤트를 만들 때 틱마다 셉니다) |
| `input_pressed(이름)` | 이번 틱에 플레이어가 그 [커스텀 바인딩](#커스텀-바인딩)을 눌렀는지. 없는 이름이면 거짓 |
| `input_held(이름)` | 이번 틱에 플레이어가 그 커스텀 바인딩을 누르고 있는지. 없는 이름이면 거짓 |
| `message_text(번호)` | `.msg` 의 문구. `{액션 이름}` 은 키 이름으로 바뀐 채로 옵니다 |
| `key_name(액션)` | 그 액션에 설정된 키의 이름 (예: `key_name("interact")` → `F`). 커스텀 바인딩의 이름도 됩니다 |
| `in_area(인덱스, 태그)` | 그 사람의 발이 그 [구역](#구역) 안에 있는지 (마지막 틱 기준) |
| `area_of(인덱스)` | 그 사람이 들어 있는 구역 태그 (겹치면 번호가 가장 큰 것). 없으면 -1 |
| `area_count(태그, 팀)` | 그 구역 안의 살아 있는 사람 수. 팀이 음수면 모든 팀 |
| `area_find(이름)` | 이름으로 찾은 구역 태그 번호 (대소문자 무시). 없으면 -1 |
| `area_info(태그)` | 구역의 지금 성질: `exists`, `name`, `damage`, `gravity`, `ai_sight`, `reverb`, `block_weather` |
| `weather()` | 지금 [날씨](#날씨)의 번호. 날씨가 없으면 -1 |
| `sky()` | 지금 하늘의 번호. 하늘이 없으면 -1 |

**조작**

| 함수 | 하는 일 |
|---|---|
| `set_var(번호, 값)` | 미션 변수를 씁니다. 변수는 4096개까지 |
| `set_team(인덱스, 팀)` | 팀을 바꿉니다 |
| `damage(인덱스, 피해량)` | 체력을 깎습니다 |
| `set_hp(인덱스, 값)` | 살아 있는 사람의 체력을 정합니다. 무적과 방어구를 거치지 않습니다. 0 이하면 죽습니다 |
| `set_armor(인덱스, 값)`, `set_helmet(인덱스, 값)` | 방어구 / 헬멧 포인트를 정합니다. 0 이하면 없어집니다 |
| `kill(인덱스)` | 죽입니다 |
| `teleport(인덱스, x, y, z)` | 옮깁니다 |
| `give_weapon(인덱스, 슬롯, 무기, 탄 수)` | 슬롯의 무기를 바꿉니다. 슬롯 1 이 주 무기, 0 이 보조 무기 |
| `spawn_human(사람 정보, x, y, z, yaw, 식별번호, 경로)` | 사람 정보 포인트로 사람을 세웁니다. 새 사람의 인덱스를 돌려주고, 실패하면 -1 |
| `spawn_weapon(무기, 탄 수, x, y, z, yaw)` | 떨어진 무기를 놓습니다. 놓았으면 참 |
| `spawn_object(오브젝트, 식별번호, x, y, z, yaw, 바닥에 붙이기)` | 오브젝트를 놓습니다. 놓았으면 참 |
| `destroy_object(식별번호)` | 오브젝트를 부숩니다 |
| `tween_object(식별번호, x, y, z, yaw, 초, 부드럽게)` | 오브젝트를 yaw(오브젝트 포인트의 방향과 같은 값)로 바로 돌리고, 그 시간에 걸쳐 자리로 움직입니다. 0초 이하면 바로 옮깁니다. 시작했으면 참 |
| `path_point(경로 포인트)` | 경로 포인트의 `exists`, `x`, `y`, `z`, `yaw` 를 담은 사전 |
| `block(번호)` | 사전: `exists`, `enabled`, `moving`, `x`, `y`, `z`(블록의 가운데) |
| `move_block(번호, dx, dy, dz, pitch, roll, yaw, 초, 부드럽게)` | 블록을 처음 모양 기준의 이동량(m)과 각도(도)로, 그 시간에 걸쳐 옮기고 돌립니다. 시작했으면 참 |
| `tween_block(번호, x, y, z, pitch, roll, yaw, 초, 부드럽게)` | 블록의 가운데가 그 자리에 오도록 옮기고, 처음 모양 기준의 각도(도)로 돌립니다. 시작했으면 참 |
| `toggle_block(번호, 참·거짓)` | 블록을 켜거나 끕니다. 없는 블록이면 거짓 |
| `set_path_mode(경로 포인트, 모드)` | 경로 포인트의 이동 모드를 바꿉니다 |
| `effect(번호, x, y, z)` | 이펙트를 냅니다 |
| `play_sound(칸, 소리, 볼륨, 되풀이, 어디서나, x, y, z)` | [소리 목록](#이벤트의-소리)의 소리를 칸(0 에서 15)에 재생합니다. 재생했으면 참 |
| `stop_sound(칸)` | 칸의 소리를 멈춥니다. -1 이면 전부 |
| `set_area(태그, 이름, 값)` | [구역](#구역)의 성질 하나를 바꿉니다. 이름은 `damage`(초당 HP, 0 이상), `gravity`(배율), `ai_sight`(배율, 0 이상), `reverb`(0 에서 1 사이), `block_weather`(0 이 아니면 켬). 바꿨으면 참 |
| `set_area_hostility(태그, 팀, 상대 팀, 방식)` | 구역의 적대 규칙 하나를 정합니다 (같은 짝이 있으면 바꿉니다). 방식은 0 늘 적대, 1 적대 안 함, 2 무기를 든 상대를 보거나 공격받으면 적대. 정했으면 참 |
| `set_weather(번호)` | [날씨](#날씨)를 바로 바꿉니다. 없는 번호면 거짓이고 날씨는 그대로입니다 |
| `set_sky(번호)` | 하늘을 바로 바꿉니다 |
| `pause_world(참·거짓)` | 게임을 멈추거나 다시 돌립니다. 이벤트는 계속 진행됩니다 (기본 제공 이벤트 80번의 설명) |
| `pause_ai(참·거짓)` | 모든 AI 를 멈추거나 다시 돌립니다 |
| `lock_player(잠금)` | 플레이어의 조작을 막습니다. 값은 막을 것의 합입니다: 1 이동, 2 점프와 앉기, 4 시점, 8 발사, 16 무기, 32 시점 전환. 0 이면 전부 풉니다 |
| `set_invincible(인덱스, 참·거짓)` | 체력이 깎이지 않게 합니다 |
| `set_infinite_ammo(인덱스, 참·거짓)` | 쏠 때마다 예비 탄이 한 발 늘게 합니다 |
| `show_hud(참·거짓)` | HUD 를 켜거나 끕니다. 이벤트 메시지와 `hud_text` 의 글자는 남습니다 |
| `fade(색, 진하기, 초)` | 화면을 그 색(0xRRGGBB)으로 덮습니다. 지금의 진하기에서 새 진하기(0 에서 1 사이)로 갑니다. HUD 아래에 그려집니다 |
| `letterbox(높이, 초)` | 화면 위아래의 검은 띠. 높이는 띠 하나의 높이(화면 높이 480 기준, 240 까지)이고 0 이면 없앱니다 |
| `camera_detach(x, y, z, yaw, pitch, roll, 시야각)` | 카메라를 플레이어에게서 떼어 그 자리에 놓습니다. 시야각이 0 이하면 설정값. 놓았으면 참 |
| `camera_tween(x, y, z, yaw, pitch, roll, 시야각, 초, 부드럽게)` | 뗀 카메라를 그 시간에 걸쳐 옮깁니다. 카메라가 붙어 있으면 거짓 |
| `camera_attach()` | 카메라를 플레이어에게 다시 붙입니다 |
| `ai_look(인덱스, x, y, z)` | 사람이 그 점을 계속 바라봅니다. 걸었으면 참 |
| `ai_fire(인덱스, x, y, z, 발 수)` | 사람이 그 점을 겨누고 그 발 수만큼 쏜 뒤 계속 바라봅니다. 걸었으면 참 |
| `ai_release(인덱스)` | `ai_look` / `ai_fire` 의 지시를 풉니다 |
| `set_player(인덱스)` | 플레이어가 조작하는 사람을 그 사람으로 바꿉니다. 지금까지 조작하던 사람은 AI 가 이어받습니다. 바뀌었으면(이미 그 사람이었어도) 참, 없는 사람이나 죽은 사람이면 거짓 |
| `message(번호)` | 원본 18번처럼 메시지를 화면 아래에 5초 동안 띄웁니다 |
| `hud_text(칸, 글자, 선택값)` | 화면의 칸에 글자를 놓습니다 (아래) |
| `hud_clear(칸)` | 칸을 지웁니다. -1 이면 전부 |
| `hud_image(칸, 이미지, 선택값)` | 화면의 이미지 칸(0 에서 15)에 [이미지 목록](#이미지-목록)의 이미지를 놓습니다. 놓았으면 참, 없는 칸이나 목록에 없는 번호면 거짓. 선택값의 키: `anchor`(0 에서 8, 기본 4), `x`, `y`, `width`, `height`(0 이면 이미지의 크기나 비율), `color`(0xRRGGBB, 이미지에 곱하는 색), `alpha`(0 에서 1, 기본 1), `seconds`(0 이면 지울 때까지), `fade`(나타나고 사라지는 초) |
| `hud_image_clear(칸, 페이드)` | 이미지 칸을 지웁니다. -1 이면 전부. 페이드(초)가 0 보다 크면 그 시간에 걸쳐 사라집니다 |
| `set_auto_judge(참·거짓)` | 자동 판정을 켜거나 끕니다 |
| `end_mission(참·거짓)` | 미션을 클리어(참) 또는 실패(거짓)로 끝냅니다 |
| `start_line(줄, 식별번호)` / `stop_line(줄)` | 이벤트 줄을 그 포인트에서 진행시키거나 멈춥니다 |
| `log(글자)` | 디버그 콘솔에 한 줄을 남깁니다 (200자까지) |

`hud_text` 의 선택값은 사전이고, 적지 않은 키는 기본값입니다.

```gdscript
api["hud_text"].call(0, "02:35", {"font": 1, "anchor": 2, "x": -10.0, "y": -10.0, "size": 20.0, "color": 16711680, "seconds": 0.0})
```

| 키 | 뜻 | 기본값 |
|---|---|---|
| `font` | 0 OS 글꼴, 1 `char.dds` | 0 |
| `anchor` | 기준점 (0 에서 8 사이) | 4 (화면 가운데) |
| `x`, `y` | 기준점에서의 오프셋. 화면 높이 480 기준, y 는 위쪽이 양수 | 0 |
| `size` | 글자 높이 | 16 |
| `width` | `char.dds` 글자 한 칸의 너비 | `size` 의 0.8배 |
| `color` | 색 (0xRRGGBB) | 흰색 |
| `alpha` | 진하기 (0 에서 1 사이) | 1 |
| `seconds` | 보이는 시간. 0 이면 지울 때까지 | 0 |

글자를 스크립트가 직접 만들어 넘기므로 어떤 형식으로든 보여 줄 수 있습니다. 글자는 256자까지입니다.

### 스크립트가 실패했을 때

- **미션을 로드할 때 잡히는 것**은 미션을 로드하지 않습니다: 등록되지 않은 번호, 겹친 번호, 문법 오류, 등록 파일에 적은 함수가 스크립트에 없음, 틀린 칸 표기, `.sgd` 가 아닌 스크립트, `init` 안의 오류. 이유는 디버그 콘솔에 나오고, 메뉴에서는 타이틀 아래에 잠깐 표시됩니다.
- **실행 중의 실패**는 그 이벤트 줄만 멈춥니다. 다른 줄과 미션은 계속 진행되고, 디버그 콘솔에 에러가 한 번 남습니다. 스크립트 안의 오류, 허용되지 않는 기능을 쓰려 한 것, 없는 출구 번호, 값을 돌려주지 않은 것, 실행 한도를 넘은 것이 여기에 해당합니다.
- 디버그 콘솔의 `event` 가 줄마다 어느 이벤트에 있는지와 멈췄는지를 보여 줍니다.

실행 한도는 이벤트 함수를 한 번 부를 때마다 적용됩니다.

- 게임 함수(API) 호출은 1000번까지입니다. 사전을 돌려주는 `human` 같은 함수는 약 300번에서 실행 한도에 먼저 걸립니다. 스크립트 안의 계산(반복문, 사칙연산)은 거의 제한에 걸리지 않습니다.
- 끝나지 않는 반복문은 한도에 닿을 때까지 게임을 약 0.5초 멈춘 뒤 끊기고, 그 줄은 멈춥니다 (한 번뿐입니다).

## 이벤트의 소리

스크립트 이벤트는 파일 경로를 받지 않습니다. 그래서 이벤트가 재생하는 소리도 다른 데이터처럼 번호로 가리킵니다. 목록은 `sound_data.json` 의 `soundData` 입니다.

```json
{
  "soundData": [
    { "name": "bang", "soundPath": "data/sound/bang.wav" }
  ]
}
```

| 키 | 뜻 |
|---|---|
| `name` | 에디터에서 알아보는 이름 |
| `soundPath` | WAV 파일 경로 (게임 폴더 기준) |

기본 목록에는 원본의 효과음 14개가 파일 이름 순서로 들어 있습니다 (0번 `bang` 부터). 자기 소리를 쓰려면 같은 모양의 파일을 만들어 맵팩 Manifest 의 `soundDataPath`(첫 항목이 10000번)나 MIF2 의 `addonSoundDataPath`(첫 항목이 20000번)에 적습니다. 재생은 [Play Sound](#기본-제공-이벤트) 이벤트나 API 의 `play_sound` 로 합니다.

## 이미지 목록

스크립트는 파일 경로를 받지 않습니다. 그래서 이벤트와 화면 스크립트가 그리는 이미지도 소리처럼 번호로 가리킵니다. 목록은 `image_data.json` 의 `imageData` 입니다.

```json
{
  "imageData": [
    { "name": "portrait", "imagePath": "addon/mypack/portrait.png" }
  ]
}
```

| 키 | 뜻 |
|---|---|
| `name` | 에디터에서 알아보는 이름 |
| `imagePath` | 이미지 파일 경로 (게임 폴더 기준. [쓸 수 있는 형식](#쓸-수-있는-파일-형식)) |

기본 목록은 비어 있습니다. 같은 모양의 파일을 만들어 맵팩 Manifest 의 `imageDataPath`(첫 항목이 10000번)나 MIF2 의 `addonImageDataPath`(첫 항목이 20000번)에 적습니다.

**미션의 이미지 목록 하나를 그 미션의 이벤트와 화면 스크립트가 함께 씁니다.**

- 이벤트: [Show Image](#기본-제공-이벤트) 나 API 의 `hud_image` 에 번호를 줍니다.
- [미션이 들고 오는 화면 스크립트](#미션이-들고-오는-화면)(브리핑, HUD, 결과): 이미지의 `source` 에 번호를 줍니다.
- 설치한 화면 스크립트(`godotdata/ui/`)에서는 10000 이상의 번호가 이미지 목록(맵팩 10000 부터, 미션 20000 부터)이고, 10000 미만은 그 등록 파일의 `images` 입니다.

다른 에드온 데이터와 달리 미션의 이미지 목록은 미션 정보와 함께 읽습니다. 그래서 맵이 뜨기 전의 브리핑과 맵이 내려간 뒤의 결과 화면에서도 쓸 수 있습니다. 없는 번호나 읽지 못한 파일은 그려지지 않고 경고만 남습니다.

## 날씨

하늘과 따로 정하는 연출입니다: 비나 눈, 안개 덮어쓰기, 하늘 바꾸기, 되풀이하는 소리, 번개. **연출만 하고 게임 결과에는 영향이 없습니다** (AI 의 시야, 소리, 총알은 그대로입니다. AI 의 시야를 줄이려면 [구역](#구역)을 씁니다). 목록은 `weather_data.json` 의 `weatherData` 이고 0 번이 맑음(XOPS 그대로)입니다. 기본 목록에는 맑음, 비, 눈, 폭풍우가 들어 있고 원본 미션은 전부 맑음입니다.

- 미션을 시작할 때의 날씨는 MIF2 의 `weatherIndex` 입니다. 미션 중에는 이벤트 Set Weather(120)나 API `set_weather` 로 **바로** 바꿉니다 (서서히 바뀌지 않습니다).
- 맵팩은 Manifest 의 `weatherDataPath`(첫 항목이 10000 번), 미션은 MIF2 의 `addonWeatherDataPath`(첫 항목이 20000 번)로 날씨를 더할 수 있습니다.
- 카메라가 날씨를 막는 [구역](#구역)(실내) 안에 있으면 비와 눈이 보이지 않고 날씨 소리가 작아집니다.
- 게임을 멈추면(Pause World) 비와 눈, 번개도 멈춥니다.

| 키 | 뜻 |
|---|---|
| `name` | 에디터에서 알아보는 이름 |
| `skyIndex` | 이 날씨가 되면 바꿀 하늘 번호. -1 이면 하늘을 그대로 둡니다 |
| `precipitation` | 내리는 것: 0 없음, 1 비(세로로 선 줄), 2 눈(늘 카메라를 보는 점) |
| `particleTexturePath` | 내리는 것의 그림 (알파로 섞습니다). 비우면 흰 사각형입니다 |
| `particleColor` | 내리는 것의 색과 진하기 (0 에서 255 사이) |
| `particleAmount` | 카메라 둘레에 동시에 떠 있는 수. 0 이면 내리지 않습니다 |
| `particleWidth`, `particleLength` | 하나의 너비와 길이 (m) |
| `fallSpeed` | 떨어지는 속도 (m/s) |
| `wind` | 바람 (m/s, +x 오른쪽, +y 위, +z 앞) |
| `areaRadius`, `areaHeight` | 카메라 둘레에서 내리는 범위의 가로 반지름과 높이 (m) |
| `fogOverride` | 참이면 하늘의 안개 대신 아래의 안개를 씁니다. 유저가 OPTION 에서 안개를 끄면 이것도 그리지 않습니다 |
| `fogColor`, `fogStart`, `fogEnd` | 덮어쓰는 안개의 색(0 에서 255 사이)과 시작·끝 거리 (m) |
| `soundIndex`, `soundVolume` | 되풀이해 트는 소리([소리 목록](#이벤트의-소리)의 번호, -1 이면 없음)와 크기. 거리와 무관하게 들립니다 |
| `lightningIntervalMin`, `lightningIntervalMax` | 번개가 치는 간격 (초, 그 사이의 난수). 최대가 0 이면 번개가 없습니다 |
| `lightningColor`, `lightningDuration` | 번쩍이는 색(a 가 가장 밝을 때의 진하기)과 사라지는 데 걸리는 시간 (초) |
| `lightningSoundIndex`, `lightningSoundDelay` | 번개 소리(소리 목록의 번호, -1 이면 없음)와 번쩍인 뒤 소리가 나기까지의 시간 (초) |

## 구역

맵의 한 부분에 성질을 주는 것입니다: 그 안에서 받는 데미지, 중력, AI 의 시야, 소리의 울림, 날씨 막기, 팀끼리의 적대. 이벤트가 사람이 들어오고 나가는 것을 기다릴 수도 있습니다. BD2 맵에만 있습니다 (BD1 에는 없습니다).

- **모양은 블록입니다.** BD2 의 구역 블록은 블록과 같은 꼭짓점 8개짜리 모양이고, 그리지도 않고 어느 판정에도 걸리지 않습니다. 구역 블록마다 **태그** 번호가 있고, **같은 태그의 구역 블록들이 구역 하나**입니다 (L 자 방처럼 상자 하나로 안 되는 모양을 여러 개로 만듭니다).
- 태그는 [맵 데이터 파일](#맵-데이터-파일)의 `areaTagData` 에 미리 적어 두고, 이벤트와 스크립트는 그 번호로 가리킵니다.
- **사람이 구역 안에 있는지는 발의 위치로 봅니다.** 매 틱 정합니다.
- 구역이 겹치면 **번호가 가장 큰 태그**의 성질을 씁니다. 데미지만은 들어 있는 구역들의 것을 더합니다.
- 성질은 이벤트 Set Area(102), Set Area Hostility(103)나 API `set_area`, `set_area_hostility` 로 미션 중에 바꿀 수 있고, 미션을 다시 시작하면 파일의 처음 값으로 돌아갑니다.
- 에디터의 블록 모드에서 블록을 구역으로 바꾸고 태그와 성질을 고칩니다 ([개발 문서의 에디터](development.md#에디터)).

| 키 | 뜻 |
|---|---|
| `name` | 이름. API `area_find` 와 에디터가 씁니다 |
| `damagePerSecond` | 구역 안의 사람이 받는 데미지 (초당 HP). 무적은 막고 방어구와 헬멧은 거치지 않습니다. 소수는 쌓였다가 1 이 될 때 빠집니다 |
| `gravityScale` | 구역 안의 사람에게 거는 중력의 배율. 음수면 위로 떠오르고 천장에 머리가 닿으면 멈춥니다 (바닥에 서지 못하므로 점프도 못 합니다). 사람에게만 듣습니다 (수류탄, 떨어진 무기는 그대로) |
| `aiSightScale` | 구역 안의 AI 가 볼 수 있는 거리의 배율. 보는 AI 가 구역 안에 있을 때 듣습니다 |
| `soundReverb` | 카메라가 구역 안에 있을 때 위치가 있는 소리에 거는 울림 (0 에서 1 사이, 실내 효과). 거리와 무관한 소리(배경음)에는 걸지 않습니다 |
| `blockWeather` | 참이면 카메라가 구역 안에 있는 동안 비와 눈을 그리지 않고 날씨 소리를 줄입니다 (실내) |
| `hostility` | 구역 안의 AI 가 팀끼리 적대하는 규칙의 목록. 항목마다 `team`(보는 팀), `targetTeam`(상대 팀), `mode`(0 늘 적대, 1 적대 안 함, 2 무기를 든 상대를 보거나 그 팀에게 공격받으면 적대). 규칙이 없는 팀끼리는 늘 적대합니다 |

**적대 규칙은 AI 의 판단만 바꿉니다.** 총알과 폭발의 데미지는 그대로 들어갑니다. 규칙은 **보는 AI 가 그 구역 안에 있을 때만** 듣고, "공격받으면 적대"로 기억한 것은 그 AI 가 구역을 옮기면 잊습니다.

## 커스텀 바인딩

기본 조작(점프, 발사, Interact 등) 말고도 에드온이 쓸 버튼을 더할 수 있습니다. 더한 버튼은 OPTION 의 **Custom** 탭에서 유저가 키를 바꿀 수 있고, [스크립트 이벤트](#api)와 [화면 스크립트](#frame-이-받는-값)가 이름으로 읽습니다. 버튼을 눌렀을 때 무엇을 할지는 스크립트가 정합니다 (게임이 그 버튼으로 하는 일은 없습니다).

```json
{
  "bindings": [
    { "name": "flashlight", "label": "Light", "baseValue": "<Keyboard>/v" },
    { "name": "radio", "label": "Radio", "baseValue": "<Mouse>/middleButton" }
  ]
}
```

| 키 | 뜻 |
|---|---|
| `name` | 액션 이름. 영문자, 숫자, 밑줄만 씁니다. 스크립트가 이 이름으로 읽습니다 |
| `label` | OPTION 에 보이는 이름. 스프라이트 글꼴이라 영문이고 8자까지 보입니다. 비우면 `name` 입니다 |
| `baseValue` | 처음 키. `config.json` 의 키 바인딩과 같은 형식입니다 (`<Keyboard>/키`, `<Mouse>/leftButton` 등). 비우면 묶인 키가 없습니다 |

**등록하는 곳은 세 군데입니다.**

| 곳 | 쓰이는 때 |
|---|---|
| `godotdata/input/*.json` (설치형) | 늘. 파일 이름 순서로 읽습니다 |
| 맵팩 Manifest 의 `inputDataPath` | 그 맵팩의 미션을 하는 동안과, 메뉴에서 그 맵팩의 페이지를 보는 동안 |
| MIF2 의 `addonInputDataPath` | 그 미션을 하는 동안 (미션 정보를 읽을 때 붙고 메뉴로 돌아가면 떨어집니다) |

- OPTION 의 Custom 탭에는 지금 쓰이는 것만 나옵니다. 설치형 → 맵팩 → 미션 순서이고, 하나도 없으면 탭이 없습니다. 한 페이지에 14개이고 넘치면 페이지를 넘깁니다.
- **미션의 버튼은 지금은 OPTION 에서 키를 바꿀 수 없습니다.** OPTION 은 메뉴에서만 열리고 메뉴에는 미션 정보가 없기 때문입니다. 그 미션 동안 처음 키(또는 전에 같은 이름으로 바꿔 둔 키)로 동작합니다. 여러 미션이 쓰는 버튼은 맵팩에 등록하세요.
- 유저가 바꾼 키는 `config.json` 의 `customBindings` 에 이름으로 남습니다 (처음 키와 같으면 남기지 않습니다). **이름이 같으면 맵팩이 달라도 같은 키 설정을 씁니다.** 같은 이름이 여러 곳에 있으면 앞의 곳(설치형 → 맵팩 → 미션)의 `label` 과 `baseValue` 를 씁니다.
- OPTION 의 RESET 은 커스텀 바인딩도 전부(지금 보이지 않는 맵팩의 것도) 처음 키로 되돌립니다.
- 기본 조작과 같은 이름(`jump`, `fire`, `interact` 등), 쓸 수 없는 글자가 든 이름은 경고를 남기고 뺍니다. 처음 키가 잘못됐거나 예약 키(Esc, F1 에서 F12)면 경고를 남기고 묶지 않습니다.
- 키가 기본 조작과 겹쳐도 막지 않습니다 (기본 조작끼리도 그렇습니다). 두 동작이 함께 일어납니다.
- 이벤트에서는 Interact 처럼 살아 있는 플레이어의 입력만 받고, 눌림은 다음 틱 한 번, 누르고 있음은 다음 틱부터입니다. 조작 잠금 이벤트는 이 버튼을 막지 않습니다.
- `.msg` 의 문구와 `message_text` 에서 `{이름}` 은 그 버튼의 키 이름으로 바뀝니다 (예: `{flashlight}` → `V`).

## 화면 스크립트

화면을 스크립트로 바꿔 그릴 수 있습니다. 바꿀 수 있는 화면은 여섯입니다: 오프닝(`opening`), 메뉴(`mainmenu`), 브리핑(`briefing`), 메인게임의 HUD(`maingame`), 결과(`result`), 설정 화면(`option`). 디버그 콘솔은 바꿀 수 없습니다.

- 스크립트는 [스크립트 이벤트](#스크립트-이벤트)와 같은 SafeGDScript(`.sgd`)이고 게임과 격리된 채로 실행됩니다. 게임이 내준 함수로만 화면 요소를 만듭니다.
- `godotdata/ui/` 에 등록 파일(`.json`)을 넣으면 그 화면을 기본 화면 대신 스크립트가 그립니다. 그 폴더 바로 아래의 `.json` 만 읽습니다.
- 스크립트가 실패하면(오류, 실행 한도 초과) 그 자리에서 기본 화면으로 돌아가고, 디버그 콘솔에 에러가 한 번 남습니다.
- 이벤트가 화면에 놓는 글자(Show Text, Countdown, Wait Interact 의 안내)와 이미지(Show Image)는 화면 스크립트와 무관하게 게임이 늘 그립니다.
- 미션과 맵팩은 브리핑, HUD, 결과 화면의 스크립트를 들고 올 수 있고, 맵팩은 메뉴의 타이틀 자리도 바꿀 수 있습니다 ([미션이 들고 오는 화면](#미션이-들고-오는-화면), [맵팩이 들고 오는 화면](#맵팩이-들고-오는-화면)).
- `config.json` 의 `General` 에 있는 `AllowUiScript` 를 `"false"` 로 바꾸면 화면 스크립트를 쓰지 않습니다. 기본값은 `"true"` 이고 OPTION 화면에 없는 설정입니다.
- 예제: `godotdata/ui/samples/` 에 기본 화면 여섯 개를 그대로 스크립트로 옮긴 것이 있습니다 (`opening`, `mainmenu`, `briefing`, `hud`, `result`, `option` 의 `.json` 과 `.sgd`). 등록 파일(`.json`)을 `godotdata/ui/` 로 복사하면 그 화면을 스크립트가 그립니다. 고쳐 쓰려면 `.sgd` 도 복사하고 등록 파일의 `scriptPath` 를 바꿉니다.
- 화면을 떠났다 돌아오면 스크립트는 처음부터 다시 시작합니다. 남겨야 하는 값은 `save` 로 저장합니다.

### 등록 파일

```json
{
  "screen": "maingame",
  "scriptPath": "godotdata/ui/samples/hud.sgd",
  "images": ["addon/myhud/panel.png"]
}
```

| 키 | 뜻 |
|---|---|
| `screen` | 맡을 화면: `opening`, `mainmenu`, `briefing`, `maingame`, `result`, `option` |
| `scriptPath` | 스크립트 경로 (게임 폴더 기준, `.sgd`) |
| `images` | 스크립트가 쓰는 이미지 파일들 (게임 폴더 기준). 스크립트는 파일 경로를 받지 않고 이 목록의 번호(0 부터)로 가리킵니다. 256개까지입니다 |

같은 화면을 여러 파일이 등록하면 파일 이름 순서로 처음 것을 쓰고 경고를 남깁니다.

### 미션이 들고 오는 화면

미션(MIF2)이 자기만의 브리핑, HUD, 결과 화면을 들고 올 수 있습니다. 그 미션을 하는 동안에만 적용됩니다.

| MIF2 의 키 | 바꾸는 화면 |
|---|---|
| `briefingScriptPath` | 브리핑 (`briefing`) |
| `hudScriptPath` | 메인게임의 HUD (`maingame`) |
| `resultScriptPath` | 결과 (`result`) |

- 값은 스크립트(`.sgd`)의 경로입니다 (게임 폴더 기준). 등록 파일은 쓰지 않습니다. 비워 둔 화면은 평소대로입니다.
- 스크립트가 쓰는 함수, 받는 값, 한도, 격리는 설치한 화면 스크립트와 똑같습니다. 예제(`godotdata/ui/samples/`)의 `.sgd` 를 복사해 고쳐 쓰면 됩니다.
- **이미지는 [이미지 목록](#이미지-목록)의 번호로 가리킵니다.** 등록 파일이 없으므로 `images` 도 없습니다. 맵팩의 이미지는 10000 부터, 미션의 이미지는 20000 부터이고, 10000 미만은 `image_data.json` 의 번호입니다. 예제는 0번을 등록 파일의 타이틀 그림으로 쓰므로, 복사한 뒤 그 번호를 미션의 목록에 넣은 그림의 번호로 바꿉니다.
- 찾는 순서는 **미션(MIF2) → 미션이 속한 [맵팩](#맵팩이-들고-오는-화면) → 유저가 `godotdata/ui/` 에 설치한 것 → 기본 화면**입니다. 고른 스크립트를 읽지 못하거나 실행 중에 실패하면 다음 것으로 넘어가지 않고 기본 화면으로 돌아갑니다.
- `AllowUiScript` 가 꺼져 있으면 미션과 맵팩의 화면 스크립트도 쓰지 않습니다.
- HUD 스크립트는 화면 전체를 맡습니다. 미션을 시작할 때의 페이드, 종료 문구(objective complete, mission failure), 벽 블라인드도 스크립트가 그립니다 (예제 `hud.sgd` 에 들어 있으니 필요 없는 부분만 지우면 됩니다). 이벤트의 메시지와 글자, 이미지, 암전, 레터박스는 스크립트와 무관하게 게임이 그립니다.
- 오프닝과 설정 화면은 미션도 맵팩도 바꿀 수 없습니다. 메뉴는 맵팩만 타이틀과 버전 자리를 바꿀 수 있습니다 (아래).

### 맵팩이 들고 오는 화면

[맵팩](#맵팩)의 Manifest 에도 MIF2 와 같은 키(`briefingScriptPath`, `hudScriptPath`, `resultScriptPath`, `fontImageOverridePath`)를 적을 수 있습니다. 맵팩의 미션 전부에 적용되고, 미션(MIF2)이 같은 키를 적었으면 미션의 것이 먼저입니다. 순서와 규칙은 위와 같습니다.

**메뉴 스크립트** (Manifest 의 `mainMenuScriptPath`): 메뉴에서 그 맵팩의 페이지를 보고 있는 동안 타이틀과 버전 자리를 그리는 스크립트입니다. 맵팩에만 있습니다 (MIF2 에는 없습니다).

- 그 페이지에 들어가는 순간부터 맵팩의 메뉴 스크립트와 글꼴 그림(`fontImageOverridePath`)이 적용되고, 공식 미션 탭이나 다른 페이지로 가면 원래대로 돌아옵니다. 맵팩의 미션을 하고 메뉴로 돌아오면 그 페이지에 있으므로 바로 적용된 상태입니다.
- 글꼴 그림은 그 페이지에 있는 동안 메뉴 화면 전체(미션 목록, OPTION, CREDIT, EXIT 포함)의 스프라이트 글자에 적용됩니다.
- 게임이 기본 타이틀 그림과 버전 글자를 감추고 스크립트가 그 자리를 그립니다. 미션 목록, 페이지 넘기기, OPTION / CREDIT / EXIT 는 그대로이고 스크립트가 바꿀 수 없습니다 (화면 전용 함수가 없습니다). 층의 순서는 1(타이틀), 2(버전)를 권합니다 (미션 목록은 3 에서 5).
- 계약은 다른 화면 스크립트와 같습니다 (`init`, `build`, `frame`). `ctx`: `version`(버전 글자), `pack`(맵팩의 이름), `page`(에드온 페이지 번호), `pack_images`(맵팩의 이미지 목록의 항목 수). `v`: `option_open`.
- 이미지는 [이미지 목록](#이미지-목록)의 번호입니다. 메뉴에는 미션이 없으므로 10000 부터는 그 맵팩의 이미지 목록(Manifest 의 `imageDataPath`)이고, 10000 미만은 `image_data.json` 입니다.
- 스크립트가 실패하면 그 스크립트만 내리고 기본 타이틀로 돌아갑니다. `AllowUiScript` 가 꺼져 있으면 스크립트는 돌지 않지만 글꼴 그림은 적용됩니다.
- 유저가 메뉴 전체를 바꾸는 스크립트(`godotdata/ui/` 의 `mainmenu`)를 설치했으면 맵팩의 메뉴 스크립트는 그 위에 겹쳐 그려집니다. 그 스크립트가 [`show_page`](#화면마다-다른-것)로 페이지를 알리고 `pack_title` 이 참일 때 자기 타이틀을 감춰야 자연스럽게 보입니다 (예제 `mainmenu.sgd` 가 그렇게 합니다).
- 예제: `godotdata/ui/samples/mainmenu_pack.sgd`. 맵팩의 이미지 10000 을 로고로 그리고, 이미지가 없으면 맵팩의 이름을 글자로 그립니다. 버전은 기본 메뉴와 같은 자리에 그립니다.

### 스크립트의 함수

| 함수 | 불리는 때 |
|---|---|
| `init(api)` | 올릴 때 한 번. 게임이 내주는 함수 표를 받아 둡니다 |
| `build(ctx)` | 화면에 들어올 때 한 번. 요소를 만듭니다. `ctx` 에는 화면마다 다른 값([화면마다 다른 것](#화면마다-다른-것))과 `saved`(전에 `save` 로 남긴 값), 화면 크기 값이 들어 있습니다 |
| `frame(v, delta)` | 프레임마다. `v` 는 게임의 상태, `delta` 는 지난 시간(초)입니다. 이 함수는 꼭 있어야 합니다 |
| `restart()` | 디버그 콘솔의 `restart` 가 화면을 처음으로 되돌릴 때 (오프닝, 메뉴). 없어도 됩니다 |

다음 화면으로 넘어가기, 나가기, 재시작 키도 스크립트가 처리합니다. 예제 스크립트를 참고하세요.

### 좌표와 층

- 요소는 **층** 안에 놓습니다. 층은 번호(`order`)가 클수록 위에 그려집니다.
- 층은 두 가지입니다. 확대 층(`scaled` 참)은 화면 높이를 480 으로 보고 확대하고, 픽셀 층(`scaled` 거짓)은 화면 픽셀에 설정의 UIScale 을 곱합니다. 기본 HUD 는 체력·탄약 같은 것을 픽셀 층에, 스코프·가운데 문구·암전을 확대 층에 둡니다.
- 위치는 **기준점(`pivot`)에서의 오프셋**입니다. `x` 는 오른쪽이 +, `y` 는 **위쪽이 +** 입니다.
- 기준점과 정렬의 번호: 0 왼쪽 위, 1 위 가운데, 2 오른쪽 위, 3 왼쪽 가운데, 4 가운데, 5 오른쪽 가운데, 6 왼쪽 아래, 7 아래 가운데, 8 오른쪽 아래.

### 요소를 만드는 함수

함수는 `api["이름"].call(인자)` 로 부릅니다. 만드는 함수는 요소의 번호를 돌려주고 (실패하면 0), 그 번호로 나중에 고칩니다.

| 함수 | 내용 |
|---|---|
| `layer(order, scaled, overlay)` | 층을 만듭니다. `order` 는 −100 에서 100 사이입니다. `overlay` 는 적지 않아도 되고(기본 거짓), 메인게임에서만 뜻이 있습니다 (아래 "화면마다 다른 것"의 메인게임) |
| `text(layer, props)` | `char.dds` 스프라이트 글자 한 줄 |
| `label(layer, props)` | OS 글꼴 글자 (한글 등) |
| `rect(layer, props)` | 단색 사각형 |
| `image(layer, props)` | 이미지 |
| `lines(layer, props)` | 선 묶음 (스코프의 조준선) |
| `set(id, props)` | 요소 하나의 값을 고칩니다. 준 키만 바뀝니다 |
| `set_many(changes)` | 여러 요소를 한 번에 고칩니다. `changes` 는 요소 번호 → 값 사전입니다. 함수 호출이 비싸므로 프레임마다 고칠 것은 이것으로 모아 보내는 편이 좋습니다 |
| `remove(id)` | 요소나 층을 지웁니다. 층을 지우면 그 안의 요소도 지워집니다 |

`props` 의 키입니다. 요소에 맞지 않는 키는 무시합니다.

| 키 | 쓰는 요소 | 뜻 |
|---|---|---|
| `visible` | 전부 | 보일지 |
| `pivot` | 전부 | 기준점 번호 (기본 0) |
| `x`, `y` | 전부 | 기준점에서의 오프셋 |
| `w`, `h` | 사각형, 이미지, OS 글자 | 크기 |
| `stretch` | 사각형, 이미지, OS 글자 | 층에 맞춰 펼칩니다: `"top"`, `"middle"`, `"bottom"`(가로로 꽉 채움), `"left"`, `"center"`, `"right"`(세로로 꽉 채움), `"full"`. 펼쳐지는 축의 크기는 "층의 크기 + `w` 또는 `h`" 입니다 (0 이면 꽉 채움). `"none"` 이면 펼치지 않습니다 |
| `color` | 글자, 사각형, 이미지 | 색 (`0xRRGGBB`) |
| `alpha` | 글자, 사각형, 이미지 | 진하기 (0 에서 1 사이) |
| `text` | 글자 | 글 (스프라이트 글자는 512자, OS 글자는 16384자까지) |
| `glyphs` | 스프라이트 글자 | 글자 코드의 배열 (0 에서 255 사이). `char.dds` 의 테두리 같은 칸을 그릴 때 `text` 대신 씁니다 |
| `font_w`, `font_h` | 스프라이트 글자 | 글자 한 칸의 크기 |
| `align` | 스프라이트 글자 | 기준점에 대한 정렬 번호. 6 이면 글자의 왼쪽 아래가 기준점에 옵니다 |
| `size` | OS 글자 | 글자 크기 |
| `halign` | OS 글자 | 가로 정렬 (0 왼쪽, 1 가운데, 2 오른쪽) |
| `valign` | OS 글자 | 세로 정렬 (0 위, 1 가운데, 2 아래) |
| `wrap` | OS 글자 | 상자 너비에서 줄을 바꿀지 |
| `clip` | OS 글자 | 상자를 넘는 글을 자를지 |
| `line_spacing` | OS 글자 | 줄 사이 간격 (음수면 좁아집니다) |
| `fit`, `fit_min`, `fit_max`, `fit_margin` | OS 글자 | `fit` 을 켜면 글자 크기를 상자 안에 들어가는 가장 큰 값으로 맞춥니다 (`fit_min` 에서 `fit_max` 사이, `fit_margin` 은 상자 안쪽 여백) |
| `hit` | 사각형, 이미지 | 마우스 판정을 가질지. 켜야 `hovered` 에 잡힙니다 |
| `hit_w`, `hit_h`, `hit_pivot` | 스프라이트 글자 | 마우스 판정 상자의 크기와, 그 상자가 글자의 자리에 놓이는 기준점 번호. 크기를 주면 `hovered` 에 잡힙니다 |
| `source` | 이미지, 선 | 이미지: 등록 파일의 이미지 번호(10000 이상은 [이미지 목록](#이미지-목록). 맵팩 10000 부터, 미션 20000 부터. 미션이 들고 온 스크립트에서는 모든 번호가 이미지 목록의 번호), 화면이 내주는 이미지의 이름([화면마다 다른 것](#화면마다-다른-것)), `"none"`. 선: `"scope"`(메인게임에서, 쓰고 있는 스코프의 조준선), `"none"` |

### 그 밖의 함수

| 함수 | 내용 |
|---|---|
| `chr(code)` | `char.dds` 의 그 칸을 가리키는 글자. 글 사이에 탄창 그림 같은 칸을 섞을 때 씁니다 |
| `config_number(section, key)` | `config.json` 의 숫자 값 |
| `config_bool(section, key)` | `config.json` 의 참·거짓 값 |
| `pressed(action)` | 그 입력(키 바인딩의 이름. 예: `"escape"`)이 이번 프레임에 눌렸는지 |
| `key_pressed(name)` | 그 키(예: `"F2"`)가 이번 프레임에 눌렸는지 |
| `log(message)` | 디버그 콘솔에 글을 남깁니다 |
| `mouse(layer)` | 마우스의 자리를 그 층의 좌표로 돌려줍니다: `x`, `y`. 층의 왼쪽 위(기준점 0)에서의 오프셋이라 아래로 갈수록 `y` 가 작아집니다 |
| `hovered()` | 마우스가 올라가 있는 요소들의 번호 (배열). 판정이 있는 요소만 봅니다 (`hit`, `hit_w` / `hit_h`) |
| `save(values)` | 화면을 떠났다 돌아와도 남길 값을 저장합니다 (다음 `build` 의 `ctx["saved"]` 로 돌아옵니다). 수, 참·거짓, 글자만 되고 키는 32개까지입니다. 게임을 끄면 사라집니다 |

### `frame` 이 받는 값

어느 화면에서나 들어 있는 값입니다.

| 키 | 뜻 |
|---|---|
| `click_pressed`, `click_released`, `click_held` | 마우스 왼쪽 버튼을 이번 프레임에 눌렀는지, 뗐는지, 누르고 있는지 |
| `pixel_w`, `pixel_h` | 픽셀 층의 크기 |
| `scaled_w`, `scaled_h` | 확대 층의 크기 (높이는 늘 480) |
| `ui_scale` | 설정의 UIScale |

### 화면마다 다른 것

화면마다 `build` 의 `ctx`, `frame` 의 `v`, 쓸 수 있는 함수가 더 있습니다. 화면 전용 함수의 인자는 사전 하나입니다 (없으면 생략).

**오프닝 (`opening`)**

| 함수 | 내용 |
|---|---|
| `camera({"position": [x, y, z], "euler": [x, y, z], "fov": 도})` | 배경을 비추는 카메라를 놓습니다. 준 키만 바뀝니다. 자리와 각도는 UnityXOPS 공간입니다 |
| `finish()` | 오프닝을 끝내고 메뉴로 갑니다 |

**메뉴 (`mainmenu`)**

- `ctx`: `version`(버전 글자), `credit`(크레딧 본문), `official_count`(공식 미션 수), `addon_pages`(에드온 페이지 수), `addon_exists`(에드온 미션이 있는지), `state`(개발용 시작 상태).
- `v`: `option_open`(게임의 OPTION 화면이 열려 있는지), `player_exists`, `player`(배경 맵의 플레이어 자리 `[x, y, z]`), `pack_page`(보여 주는 페이지가 [맵팩](#맵팩)의 것인지), `pack_title`(맵팩의 [메뉴 스크립트](#맵팩이-들고-오는-화면)가 타이틀과 버전을 그리고 있는지. 참이면 자기 타이틀과 버전을 감춥니다).

| 함수 | 내용 |
|---|---|
| `camera(...)` | 오프닝과 같습니다 |
| `show_page({"addon": 참·거짓, "page": 페이지})` | 지금 보여 주는 탭과 페이지를 게임에 알립니다. 탭이나 페이지를 바꿀 때마다 부릅니다. 맵팩의 페이지면 그 맵팩의 글꼴과 메뉴 스크립트가 적용됩니다 (부르지 않으면 적용되지 않습니다) |
| `mission_count({"addon": 참·거짓, "page": 페이지})` | 미션 수. `addon` 이 거짓이면 공식 미션입니다 |
| `missions({"addon", "page", "from", "count"})` | 미션 이름의 배열. `from` 번째부터 `count` 개 (한 번에 64개까지) |
| `page_name({"page": 페이지})` | 에드온 페이지의 이름 |
| `load_mission({"index", "addon", "page"})` | 미션을 로드하고 브리핑으로 넘어갑니다. `ok`(넘어갔는지)와 `error`(실패한 이유)를 돌려줍니다 |
| `open_option()` | 설정 화면을 엽니다. 열려 있는 동안 `option_open` 이 참이고, 그 화면의 입력은 설정 화면이 처리합니다. 설정 화면은 따로 등록하는 화면(`option`)이고, 등록이 없으면 게임의 기본 화면이 픽셀 층의 순서 5 에 그려집니다 |
| `quit()` | 게임을 끕니다 |

**브리핑 (`briefing`)**

- `ctx`: `fullname`(미션 이름), `briefing`(본문), `images`(미션 이미지 수: 0, 1, 2).
- 이미지의 `source`: `"mission_image_0"`, `"mission_image_1"`.

| 함수 | 내용 |
|---|---|
| `start()` | 미션을 시작합니다 |
| `back()` | 메뉴로 돌아갑니다 |

**메인게임 (`maingame`)**

- `ctx`: `mode`(시작할 표시 방식: `normal` / `simple` / `off`).
- 이미지의 `source`: `"scope"`(쓰고 있는 스코프의 그림), `"weapon_view"`(3D 무기 표시).

| 함수 | 내용 |
|---|---|
| `scope()` | 쓰고 있는 스코프: `index`(없으면 −1), `aspect`(그림의 가로세로비), `hide_crosshair` |
| `weapon_view(props)` | 3D 무기 표시의 자리. 준 키만 바뀝니다: `main` / `sub`(든 무기와 다른 슬롯의 무기 자리, `[x, y, z]`), `main_scale`, `main_yaw`, `sub_scale`, `sub_yaw`, `camera`, `camera_euler`, `fov` |
| `restart_mission()` | 미션을 처음부터 다시 시작합니다 |
| `leave({"scene": 이름})` | 화면을 떠납니다. `"result"` 면 결과 화면으로, 그 밖에는 메뉴로 갑니다 |

`v` 에 더 들어 있는 값입니다.

| 키 | 뜻 |
|---|---|
| `exists`, `alive`, `index` | 조작하는 사람이 있는지, 살아 있는지, 그 사람의 번호 |
| `hp` | 체력 |
| `armor`, `armor_max` | 남은 방어구 포인트와 처음 값 (사람 데이터의 `armor`). 방어구가 없는 사람은 둘 다 0 |
| `helmet`, `helmet_max` | 남은 헬멧 포인트와 처음 값 |
| `magazine`, `reserve` | 탄창의 탄 수, 예비 탄 수 |
| `weapon` | 든 무기의 이름 |
| `reloading`, `switching` | 재장전 중인지, 무기를 바꾸는 중인지 |
| `first_person` | 1인칭 시점인지 |
| `crosshair` | 든 무기가 조준선을 보여 주는지 |
| `error_range` | 지금의 조준 오차 (조준선이 벌어지는 정도) |
| `scoping`, `scope_index` | 스코프를 보고 있는지, 스코프의 번호 |
| `blind` | 카메라가 벽에 묻힌 방향 (1 위, 2 아래, 4 왼쪽, 8 오른쪽의 합) |
| `hit` | 이번 프레임에 맞았는지 |
| `result` | 미션 결과 (0 진행 중, 1 성공, 2 실패) |
| `start_count` | 미션이 시작된 횟수. 값이 바뀌면 미션이 다시 시작된 것입니다 |
| `message_id`, `message_text`, `message_alpha` | 이벤트 메시지의 번호(없으면 −1), 글, 진하기 |
| `hud_visible` | 이벤트가 HUD 를 켜 두었는지 (Show HUD). 감추는 일은 게임이 하므로 스크립트는 읽기만 합니다 |
| `custom_held`, `custom_pressed` | [커스텀 바인딩](#커스텀-바인딩)의 이름 → 지금 누르고 있는지 / 이번 프레임에 눌렸는지. 지금 쓰이는 것만 들어 있습니다 |

**이벤트가 HUD 를 끌 때** (1.2.1): 이벤트 Show HUD 가 HUD 를 끄면 게임이 스크립트의 층을 통째로 감춥니다. 꺼도 남아야 하는 것(이벤트 메시지, 미션을 시작하고 끝낼 때의 암전과 종료 문구, 벽 블라인드)은 `layer` 의 셋째 인자 `overlay` 를 참으로 준 층에 둡니다. `overlay` 를 주지 않은 스크립트는 HUD 가 꺼져 있는 동안 이벤트 메시지도 보이지 않습니다. 미션이 끝나면 HUD 는 다시 보입니다. 이벤트의 암전과 레터박스, 이벤트가 놓은 글자는 스크립트와 무관하게 게임이 그립니다.

**설정 화면 (`option`)**

다른 화면 위에 덮여 뜨는 화면입니다 (지금은 메뉴가 엽니다). 메뉴가 기본 화면이든 스크립트든 상관없이 따로 등록합니다. 값은 바꾸는 즉시 게임에 들어가고, `config_save` 가 파일에 씁니다. `close` 는 저장하지 않은 변경을 되돌립니다.

- `ctx`: `tab`(시작할 탭의 이름. 없으면 빈 글자), `sections`(설정 섹션 이름의 배열), `custom`(지금 쓰이는 [커스텀 바인딩](#커스텀-바인딩)의 배열. 항목마다 `name`, `label`). 키는 기본 조작처럼 `binding` 과 `bind_listen` 에 `name` 을 줘서 다룹니다. 예제 `option.sgd` 는 `custom` 이 있으면 Custom 탭을 더합니다.
- `v`: `bind_listening`(키를 기다리는 액션의 이름. 없으면 빈 글자), `bind_captured`(이번 프레임에 키를 받았는지).

| 함수 | 내용 |
|---|---|
| `config_set({"section", "key", "value"})` | 설정 값을 바꿉니다. 형이 맞아야 하고 범위는 게임이 자릅니다. `AllowConsole`, `AllowEventScript`, `AllowUiScript` 와 해상도는 여기서 바꿀 수 없습니다. 바꿨으면 참을 돌려줍니다 |
| `config_range({"section", "key"})` | 설정의 범위: `min`, `max`. UIScale 의 상한은 해상도를 따라 바뀝니다 |
| `config_save()` | 설정을 파일에 쓰고 창 모드·해상도·UIScale 을 적용합니다 |
| `config_reset()` | 설정과 키 바인딩(커스텀 바인딩 포함)을 처음 값으로 되돌립니다 (저장은 따로) |
| `resolution()` | 해상도: `position`(고를 수 있는 목록에서의 자리), `count`(목록의 수), `label`(이름) |
| `resolution_set({"position"})` | 해상도를 목록의 그 자리의 것으로 바꿉니다 (저장할 때 적용됩니다) |
| `actions()` | 입력 액션 이름의 배열 (지금 쓰이는 커스텀 바인딩 포함) |
| `binding({"action"})` | 그 액션에 묶인 키의 경로 (예: `<Keyboard>/leftShift`) |
| `bind_listen({"action"})` | 그 액션의 키를 다음에 눌리는 키나 마우스 버튼으로 바꾸기 시작합니다. 키는 게임이 받아서 넣습니다. 빈 이름을 주면 그만둡니다 |
| `close()` | 저장하지 않은 변경을 되돌리고 화면을 닫습니다 |

**결과 (`result`)**

- `ctx`: `fullname`(미션 이름), `complete`(성공했는지), `stats`(`playTime` 초, `fire`, `onTarget`, `accuracy` 퍼센트, `kill`, `headshot`).

| 함수 | 내용 |
|---|---|
| `restart()` | 같은 미션을 다시 시작합니다 |
| `back()` | 메뉴로 돌아갑니다 |

### 한도

- 요소는 층을 포함해 512개까지입니다. 넘으면 스크립트가 실패한 것으로 칩니다.
- 함수 한 번(`init`, `build`, `frame`)이 부를 수 있는 게임 함수는 2000번까지입니다.
- 함수 한 번에는 실행 한도가 있습니다. 게임 함수를 부르는 것뿐 아니라 사전과 배열을 다루는 것도 한도를 씁니다. `build` 는 요소 250개쯤을 만들 수 있고, `frame` 은 요소 250개쯤의 값을 한 번에 고칠 수 있습니다 (예제 설정 화면이 탭을 바꿀 때가 그 정도입니다).
- 끝나지 않는 반복문은 약 2초(`build` 에서는 약 4초) 게임을 멈춘 뒤 끊기고, 기본 화면으로 돌아갑니다.

### 스크립트를 쓸 때 조심할 것

SafeGDScript 의 컴파일러에는 일반 GDScript 와 다르게 도는 곳이 있습니다. 예제를 만들면서 걸린 것들입니다.

- **변수에 든 글자끼리의 비교(`a == b`)는 믿을 수 없습니다.** 같은 글자인데도 거짓이 나올 때가 있습니다. 예제 설정 화면에서 "고른 탭의 이름"을 글자로 들고 `name == selected` 로 견줬더니, 어떻게 써도 고른 탭을 알아보지 못했습니다. 글자를 코드에 적은 값과 견주는 것(`mode == "simple"`)은 됩니다.
- **그래서 상태는 수로 듭니다.** "몇 번째 탭을 골랐는지", "몇 번째 액션이 키를 기다리는지"처럼 목록에서의 자리를 수로 들고 수끼리 견줍니다. 수와 참·거짓의 비교에는 이런 일이 없습니다. 글자가 목록의 몇 번째인지는 `목록.find(글자)` 로 알 수 있습니다.
- **전역 변수의 글자를 지역 변수에 옮겨 두고 전역 변수를 바꾸면 지역 변수도 같이 바뀔 수 있습니다.** `var was = listening` 다음에 `listening = ""` 을 하면 `was` 도 빈 글자가 됩니다. 이것도 상태를 수로 들면 생기지 않습니다.
- `%` 로 글자를 만드는 서식(`"%d" % 값`)은 쓰지 않고 `str()` 과 `+` 로 잇습니다.
- 기본 HUD 를 옮긴 예제 스크립트는 프레임마다 약 0.2 에서 0.4 ms 가 듭니다.
- 스크립트는 파일을 읽거나 쓰지 못합니다. 설정(`config.json`)을 고치는 함수는 설정 화면(`option`)의 스크립트에만 있습니다.

## 블록 재질

`block_material_data.json`에 있습니다. 맵의 [맵 데이터 파일](#맵-데이터-파일)에서 텍스처마다 적는 재질 번호(`materialIndex`)가 `blockMaterialData` 목록의 번호입니다. 재질은 그 텍스처를 쓰는 면을 밟았을 때의 발소리와 총알이 맞았을 때의 이펙트·소리를 정합니다. (1.2.2 까지는 BD2 의 면마다 재질 번호를 적었습니다.)

기본 데이터에는 0번 `Default` 하나만 있고, 원본의 벽 착탄 연기와 착탄음이 들어 있습니다 (발소리와 탄흔은 없습니다). **BD1 맵은 모든 면이 0번 재질입니다.** 그래서 0번을 고치면 원본 맵 전체의 착탄이 바뀌고, 0번에 발소리를 넣으면 원본 맵에서도 발소리가 납니다. BD2 맵에서 재질을 적지 않은 텍스처(-1)는 미션의 기본 재질이고, MIF2 가 `defaultBlockMaterial` 을 바꾸지 않았으면 그것도 0번입니다.

재질을 더하려면 목록 끝에 항목을 추가하거나, [맵팩](#맵팩)의 데이터로 들고 옵니다 (번호 10000 부터). 미션(MIF2)은 재질을 들고 오지 않습니다. 소리도 이펙트도 없는 면이 필요하면 내용을 비운 항목을 하나 더해 그 번호를 쓰면 됩니다. 목록에 없는 번호를 가리키는 텍스처의 면도 소리와 이펙트가 없습니다.

| 키 | 뜻 |
|---|---|
| `name` | 구분용 이름 |
| `footstepWalk`, `footstepRun`, `footstepLanding` | 걷기, 달리기, 착지 발소리의 WAV 경로 목록. 여러 개를 넣으면 그중 하나를 무작위로 냅니다. 비워 두면 소리가 나지 않습니다 |
| `hitSounds` | 총알이 맞았을 때의 WAV 경로 목록 |
| `hitEffect` | 총알이 맞았을 때의 이펙트 프리셋 번호 (`effect_data.json`). 0 이면 내지 않습니다 |
| `bulletHoleEffect` | 맞은 자리에 남기는 탄흔의 이펙트 프리셋 번호. 크기에 탄환의 `bulletHoleSize`가 곱해집니다. 0 이면 남기지 않습니다. 탄흔으로 쓸 프리셋은 `flags` 1 인 입자로 만듭니다 ([벽에 붙는 자국](#벽에-붙는-자국)) |

`blockMaterialGeneralData`의 `footstepWalkVolume`, `footstepRunVolume`, `footstepLandingVolume`은 발소리 볼륨(0 에서 1 사이)입니다. 소리가 거리에 따라 줄어드는 방식은 다른 효과음과 같아서, 볼륨이 작을수록 가까이에서만 들립니다.

발소리가 나는 박자는 `human/animation.json`의 `footstepPhase`가 정합니다. AI 가 발소리를 듣는 것은 이 소리와 무관합니다 (`human/ai.json`의 `aiHearFootstep...`).

## 에드온 페이지

`addon` 폴더 안의 미션 파일(`.mif`와 `.mif2`)은 자동으로 에드온 목록의 첫 페이지에 나옵니다. 두 형식을 섞어 파일 이름 순서(숫자는 숫자 크기대로)로 정렬됩니다. 읽지 못한 `.mif2`는 목록에 나오지 않고 에러가 남습니다.

그 뒤의 페이지는 [맵팩](#맵팩)입니다. 맵팩 하나가 페이지 하나가 되고, 메뉴에서 페이지를 넘겨 가며 고를 수 있습니다.

### 맵팩

맵팩은 미션 여러 개와 그 미션들이 함께 쓰는 데이터(사람, 무기, 오브젝트, 소리, 이미지, 하늘, 이벤트, 재질, 이펙트)를 묶은 것입니다. **Manifest 파일이 있어야 맵팩입니다.** Manifest 는 맵팩의 이름, 미션의 순서, 데이터 파일의 경로를 적은 JSON 입니다.

`GodotXOPS.exe` 옆의 `addon.json`에 Manifest 의 경로를 적어 등록합니다.

```json
{
    "manifests" : [
        "mappacks/vol0/manifest.json"
    ]
}
```

Manifest 의 예입니다.

```json
{
    "name": "Vol.0",
    "missions": [
        "mappacks/vol0/m01.mif2",
        "mappacks/vol0/m02.mif2"
    ],
    "humanDataPath": "mappacks/vol0/human.json",
    "weaponDataPath": "",
    "objectDataPath": "",
    "soundDataPath": "",
    "imageDataPath": "mappacks/vol0/images.json",
    "skyDataPath": "",
    "eventDataPath": "mappacks/vol0/events.json",
    "blockMaterialDataPath": "mappacks/vol0/materials.json",
    "effectDataPath": "mappacks/vol0/effects.json",
    "inputDataPath": "mappacks/vol0/input.json",
    "weatherDataPath": "mappacks/vol0/weather.json",
    "briefingScriptPath": "",
    "hudScriptPath": "mappacks/vol0/hud.sgd",
    "resultScriptPath": "",
    "fontImageOverridePath": "mappacks/vol0/font.png",
    "mainMenuScriptPath": "mappacks/vol0/menu_title.sgd"
}
```

| 키 | 뜻 |
|---|---|
| `name` | 메뉴의 페이지 이름 |
| `missions` | 맵팩의 미션(MIF2). 적은 순서대로 메뉴에 나옵니다 |
| `...DataPath` | 맵팩의 데이터 파일. 번호 10000 부터입니다 ([맵팩과 미션의 추가 데이터](#맵팩과-미션의-추가-데이터-에드온-데이터)). 비워 두면 없습니다 |
| `inputDataPath` | 맵팩의 [커스텀 바인딩](#커스텀-바인딩)의 등록 파일. 번호가 아니라 이름으로 가리킵니다. 비워 두면 없습니다 |
| `briefingScriptPath`, `hudScriptPath`, `resultScriptPath` | 맵팩의 미션에서 브리핑, HUD, 결과 화면을 대신 그리는 화면 스크립트 ([맵팩이 들고 오는 화면](#맵팩이-들고-오는-화면)). 미션(MIF2)이 적은 것이 먼저입니다 |
| `fontImageOverridePath` | 맵팩의 미션과, 메뉴에서 맵팩의 페이지를 보는 동안 쓰는 스프라이트 글꼴 그림. 미션(MIF2)이 적은 것이 먼저입니다 |
| `mainMenuScriptPath` | 메뉴에서 맵팩의 페이지를 보는 동안 타이틀과 버전 자리를 그리는 화면 스크립트 ([메뉴 스크립트](#맵팩이-들고-오는-화면)) |

- 경로는 전부 `GodotXOPS.exe`가 있는 폴더 기준입니다. Manifest 가 있는 폴더 기준이 아닙니다. 그 폴더 밖을 가리키는 경로는 무시합니다.
- `missions` 에는 MIF2 만 적습니다. 없는 파일, `.mif`, 읽지 못한 MIF2 는 에러를 남기고 목록에서 뺍니다. 맵팩 폴더에 있어도 `missions` 에 적지 않은 미션은 나오지 않습니다.
- 미션이 어느 맵팩의 것인지는 등록된 Manifest 들의 `missions` 로 찾습니다. 그래서 디버그 콘솔의 `loadmissionmif` 로 연 미션도 Manifest 에 적혀 있으면 맵팩의 데이터를 씁니다. 어느 Manifest 에도 없는 MIF2 는 맵팩 없이 돕니다 (하늘, 이벤트, 재질, 이펙트를 추가할 수 없습니다. 미션 하나짜리 맵팩을 만들면 됩니다).
- 읽지 못한 Manifest 는 페이지가 되지 않고 에러가 남습니다.
- 1.2.3 까지의 `addon.json`(`addonPath`, `addonName`)은 읽지 않습니다. 모르는 키로 경고만 남깁니다.

### 글자가 깨질 때

브리핑 같은 텍스트 파일은 UTF-8 이면 그대로 읽고, 아니면 Windows 표시 언어의 옛 인코딩으로 읽습니다 (한국어 CP949, 일본어 Shift-JIS, 그 밖 Windows-1252). 그래서 한국어 Windows 에서 Shift-JIS 로 저장된 일본어 에드온은 글자가 깨집니다. 텍스트 파일을 UTF-8 로 다시 저장하면 어느 언어에서든 제대로 나옵니다.

## 알려진 제한

원본 파일 형식에서 오는 제한입니다.

- 맵(BD1) 하나가 쓸 수 있는 텍스처는 10개입니다. BD2 에는 이 제한이 없습니다.
- 미션 파일(PD1)의 포인트 값은 0 에서 255 사이입니다. 사람·무기·오브젝트 번호도 이 범위 안에서만 가리킬 수 있습니다. PD2 에는 이 제한이 없습니다.
- 동시에 존재할 수 있는 수: 떨어진 무기 200개, 탄환 160개. 넘으면 새로 생기지 않습니다.
- 이펙트는 기본 256개이고 모자라면 늘어납니다 (`effect_data.json`의 `poolInitialSize`, `poolGrowStep`, `poolMaxSize`).
- 원본 포인트 형식(PD1)의 미션은 이벤트가 세 줄, 메시지가 16개, 한 줄이 한 번(1/33초)에 처리하는 이벤트가 6개입니다. PD2 에는 이 제한이 없습니다.
- 블록, 포인트, 사람의 수에는 제한이 없습니다. 다만 수가 많아질수록 느려집니다.
- 이미지·모델·소리는 한 번 읽으면 게임을 끌 때까지 기억합니다. 실행 중에 파일을 바꿨다면 게임을 다시 켭니다.
- 유저가 만든 코드는 [스크립트 이벤트](#스크립트-이벤트)와 [화면 스크립트](#화면-스크립트)로만 넣을 수 있습니다. 디버그 콘솔은 스크립트로 바꿀 수 없습니다.
- 스크립트(이벤트, 화면)는 격리된 채로 실행되지만, 아주 큰 배열이나 문자열을 만들어 메모리를 바닥내는 것까지는 막지 못합니다. 그런 스크립트는 게임을 멈추게 할 수 있습니다 (파일이나 시스템에 손대지는 못합니다).
- 스크립트의 실행 환경(Godot Sandbox)은 지금 Windows 용만 들어 있습니다.
