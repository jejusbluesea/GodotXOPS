# 모딩 문서

GodotXOPS 는 원본 XOPS 에서 코드에 박혀 있던 수치를 `godotdata/` 폴더의 JSON 파일로 빼 두었습니다. 메모장으로 고치고 게임을 다시 켜면 반영됩니다. 프로그램을 다시 빌드할 필요는 없습니다.

- [기본 규칙](#기본-규칙)
- [파일 목록](#파일-목록)
- [무기](#무기)
- [사람](#사람)
- [오브젝트](#오브젝트)
- [이펙트](#이펙트)
- [미션과 스카이](#미션과-스카이)
- [에드온 페이지](#에드온-페이지)
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
| `config.json` | 게임 설정과 키 바인딩. 메뉴의 OPTION 에서 SAVE 하면 덮어씁니다. `General`의 `AllowConsole`은 OPTION 에 없는 설정이고, `"true"`로 바꾸면 메인게임에서 F11 로 디버그 콘솔이 열립니다 ([개발 문서](development.md#디버그-콘솔)) |
| `global.json` | 제품 이름, 버전, 크레딧에 나오는 라이선스 문구 |
| `mission_data.json` | 오프닝·메뉴 배경 맵, 공식 미션 목록 |
| `sky_data.json` | 스카이 텍스처, 배경색, 안개, 시야 거리 |
| `effect_parameter_data.json` | 이펙트 텍스처와 프리셋 |
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
| `human/controller.json` | 중력, 낙하, 계단 높이, 몸 크기 |
| `human/hitbox.json` | 머리·몸·다리 피격 판정 |
| `human/general.json` | 모델 배율과 높이, 팔 각도, 피격 시 조준 흐트러짐 |
| `human/interaction.json` | 무기 줍기 범위 |
| `object/list.json` | 오브젝트 목록 (체력, 부서지는 소리, 튀는 세기) |
| `object/model.json` | 오브젝트 모델 |
| `object/collider.json` | 오브젝트 판정 모양 |
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
| `penetration` | 관통력 |
| `fireRate` | 초당 발사 수. 0 이면 쏠 수 없습니다 |
| `bulletSpeed` | 탄속 (m/s) |
| `magazineSize` | 탄창 크기 |
| `pelletCount` | 한 번에 나가는 탄 수 (산탄총) |
| `burstMode` | 0 연발, 1 단발, 2 점사 |
| `burstCount` | `burstMode`가 2 일 때, 발사 키를 한 번 누르고 있는 동안 나가는 최대 발 수 |
| `reloadStyle` | 0 남은 탄을 버리고 재장전, 1 남은 탄을 유지하고 재장전, 2 한 발씩 장전, 3 자동 재장전 |
| `reloadTime` | 재장전 시간 (초) |
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

### `weapon/model.json`

| 키 | 뜻 |
|---|---|
| `textures` | 텍스처 경로 목록 |
| `modelData` | 모델 조각 목록. 조각마다 `modelPath`, `textureIndex`(위 목록의 번호), `position`, `rotation`, `scale` |
| `muzzleFlashEffectIndex`, `muzzleFlashOffset`, `muzzleFlashSize` | 총구 화염 이펙트 번호, 위치, 크기 |
| `gunfireSmokeEffectIndex` | 발사 연기 이펙트 번호 |
| `shellEffectIndex`, `shellEjectOffset`, `shellEjectDirection`, `shellEjectSpeed`, `shellEjectDelay`, `shellSize` | 탄피 이펙트 번호, 나오는 위치·방향·속도·지연·크기 |
| `leftArmIndex`, `rightArmIndex` | 이 무기를 들었을 때의 왼팔·오른팔 모양 번호 (`human/arm.json`의 `leftArms`, `rightArms` 안에서의 순서). -1 이면 그 팔을 그리지 않습니다 |
| `fixLeftArm`, `fixedLeftArmAngle`, `fixRightArm`, `fixedRightArmAngle` | 팔을 조준 방향과 무관하게 고정할지와 그 각도 |

모델 조각을 여러 개 넣으면 `.x` 파일 여러 개를 조립해 무기 하나로 만들 수 있습니다.

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
| `wallHitEffectIndex`, `humanHitEffectIndex`, `objectHitEffectIndex` | 맞은 곳에 따라 내는 이펙트 번호 |
| `wallHitSounds`, `humanHitSounds`, `bulletPassingSounds` | 착탄음, 피격음, 스쳐 지나가는 소리. 여러 개를 넣으면 그중 하나를 무작위로 냅니다 |
| `lifetime` | 탄환이 사라질 때까지의 시간 (초) |

예: 로켓은 `explosionTrigger`를 15(1+2+4+8)로, 수류탄은 1 로 둡니다.

### `weapon/general.json`

| 키 | 뜻 |
|---|---|
| `noneWeaponIndex` | 맨손으로 취급하는 무기 번호 |
| `grenadeWeaponIndex` | 수류탄으로 취급하는 무기 번호 (AI 가 던지는 방식이 달라집니다) |
| `caseWeaponIndex` | 가방으로 취급하는 무기 번호 목록 (미션 이벤트의 "가방 소지" 조건, AI 가 버리지 않음) |
| `weaponScale` | 무기 모델 배율 |

## 사람

사람 하나는 `human/list.json`의 `humanData` 항목 하나입니다.

| 키 | 뜻 |
|---|---|
| `name` | 이름 (구분용) |
| `hp` | 체력 |
| `modelIndex` | `human/model.json`의 번호 |
| `aiIndex` | `human/ai.json`의 `aiData` 번호 (AI 수준) |
| `weaponIndex0`, `weaponIndex1` | 처음 들고 있는 무기 번호 (슬롯 1, 2) |
| `typeIndex` | `human/type.json`의 번호 (사람 종류) |

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
| `zombieMeleeDamageRange`, `zombieMaxMeleeRange`, `zombieAttackSound` | 좀비의 근접 데미지, 닿는 거리, 공격음 |
| `autoBulletMultiplier` | 처음 갖는 탄의 총량 (탄창 크기 × 이 값. 장전된 한 탄창을 포함합니다) |
| `controllerSizeIndex`, `hitboxSizeIndex` | 몸 크기(`controller.json`)와 피격 판정(`hitbox.json`)의 번호 |

### `human/ai.json` — AI

`aiData`는 AI 수준 목록입니다.

| 키 | 뜻 |
|---|---|
| `aiming` | 조준 보정 빈도. 클수록 자주 고쳐 겨눕니다 |
| `attack` | 발사 확률. **작을수록 자주 쏩니다** |
| `search` | 탐색 능력. 클수록 멀리, 꼼꼼히 봅니다 |
| `limitsError` | 발사를 허용하는 각도 보정. 음수면 더 정확히 겨눈 뒤에 쏩니다 |

그 밖의 `ai*` 값은 모든 AI 가 함께 쓰는 값입니다: 시야각(`aiSearchFov*`), 발견 거리(`aiSearchDist*`), 회전 속도(`aiTurn*`), 경계 유지 시간(`aiCautionFrames`, 틱 단위), 소리를 듣는 거리(`aiHear*`), 경로 도착 판정 거리(`aiArrivalDist*`).

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

`human/animation.json`의 `humanAnimation`은 서 있기·걷기·달리기 동작입니다. `index`는 그 동작에서 차례로 보여 줄 다리 모양 번호, `forwardSpeed`·`strafeSpeed`·`backwardSpeed`는 방향별 재생 속도입니다.

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

`object/general.json`의 `addonObjectIndex`는 에드온 미션(.mif)이 직접 지정하는 오브젝트이 들어가는 자리입니다. 이 번호의 항목은 미션을 로드할 때마다 덮어쓰이므로 다른 용도로 쓰지 마세요.

## 이펙트

`effect_parameter_data.json`에 있습니다. 총구 화염, 탄피, 연기, 혈흔, 폭발이 모두 이 파일의 값으로 그려집니다.

### `effectGeneralData`

| 키 | 뜻 |
|---|---|
| `texturePaths` | 이펙트가 쓰는 텍스처 목록. 개수 제한은 없습니다. `emitters`의 `textureIndex`가 이 목록의 번호입니다 |
| `wallBloodEffectIndex` | 혈흔 입자가 벽에 닿았을 때 그 자리에 남길 프리셋 번호 |
| `poolInitialSize` | 시작할 때 만들어 두는 이펙트 자리 수 (원본은 256 고정) |
| `poolGrowStep` | 자리가 다 찼을 때 한 번에 늘리는 수. 0 이면 늘리지 않고 원본처럼 새 이펙트를 버립니다 |
| `poolMaxSize` | 늘릴 수 있는 한계. 0 이면 한계 없음 |

자리를 늘리는 데는 비용이 들므로 `poolGrowStep`은 한 번에 넉넉히(수십 개) 잡는 편이 좋습니다. 한 번 늘린 자리는 줄지 않습니다.

### `effectData`

이펙트 프리셋 목록입니다. 무기 모델·탄환·사람 종류의 `...EffectIndex`가 이 목록의 번호를 가리킵니다. 프리셋 하나는 `name`과 `emitters`(한 번에 내는 입자 묶음) 여러 개로 이루어집니다. 예를 들어 폭발은 섬광 하나와 연기 넷입니다.

`emitters`의 항목 하나가 입자 한 종류입니다.

| 키 | 뜻 |
|---|---|
| `textureIndex` | `texturePaths`의 번호 |
| `flags` | 동작 플래그. 0 없음, 1 빌보드를 끄고 `orientation` 방향으로 고정(벽에 붙는 자국), 2 블록에 닿는지 검사(닿으면 `wallBloodEffectIndex` 자국을 남기고 사라집니다). 더해서 씁니다 |
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

## 미션과 스카이

### `mission_data.json`

- `officialMissions` — 메뉴의 공식 미션 목록. 항목마다 `name`(목록에 나오는 이름), `fullname`(브리핑 제목), `bd1Path`(블록), `pd1Path`(포인트), `txtPath`(브리핑 글), `adjustCollision`(추가 충돌 검사), `darkScreen`(어두운 화면).
- `openingData` — 오프닝에 쓰는 맵.
- `demoData` — 메뉴 배경으로 도는 맵 목록.

미션을 목록에 추가하려면 `officialMissions` 끝에 항목을 더하면 됩니다. BD1 / PD1 / MIF 파일은 맵 에디터를 이용해 수정합니다.

### `sky_data.json`

스카이 번호별 텍스처(`skyTexturePath`)와 안개 색(`skyColor`), 안개가 시작하고 끝나는 거리(`fogStart`, `fogEnd`), 보이는 거리(`farClippingPlane`)입니다.

## 에드온 페이지

`addon` 폴더 안의 `.mif` 파일은 자동으로 에드온 목록의 첫 페이지에 나옵니다. 파일 이름 순서(숫자는 숫자 크기대로)로 정렬됩니다.

폴더를 더 등록하려면 `GodotXOPS.exe` 옆의 `addon.json`에 적습니다. 폴더 하나가 페이지 하나가 되고, 메뉴에서 페이지를 넘겨 가며 고를 수 있습니다.

```json
{
    "addonPath" : [
        "addon_pack1",
        "addon_pack2"
    ],
    "addonName" : [
        "Pack 1",
        "Pack 2"
    ]
}
```

- `addonPath`는 `GodotXOPS.exe`가 있는 폴더 기준 경로입니다. 그 폴더 밖을 가리키는 경로는 무시합니다.
- `addonName`은 같은 순서의 페이지 이름입니다. 두 목록의 개수를 맞춥니다.
- 폴더가 없거나 `.mif`가 없어도 빈 페이지로 나옵니다.

### 글자가 깨질 때

브리핑 같은 텍스트 파일은 UTF-8 이면 그대로 읽고, 아니면 Windows 표시 언어의 옛 인코딩으로 읽습니다 (한국어 CP949, 일본어 Shift-JIS, 그 밖 Windows-1252). 그래서 한국어 Windows 에서 Shift-JIS 로 저장된 일본어 에드온은 글자가 깨집니다. 텍스트 파일을 UTF-8 로 다시 저장하면 어느 언어에서든 제대로 나옵니다.

## 알려진 제한

원본 파일 형식에서 오는 제한입니다.

- 맵(BD1) 하나가 쓸 수 있는 텍스처는 10개입니다.
- 미션 파일(PD1)의 포인트 값은 0~255 범위입니다. 사람·무기·오브젝트 번호도 이 범위 안에서만 가리킬 수 있습니다.
- 동시에 존재할 수 있는 수: 떨어진 무기 200개, 탄환 160개. 넘으면 새로 생기지 않습니다.
- 이펙트는 기본 256개이고 모자라면 늘어납니다 (`effect_parameter_data.json`의 `poolInitialSize`, `poolGrowStep`, `poolMaxSize`).
- 이벤트는 세 줄, 메시지는 미션당 16개입니다.
- 이미지·모델·소리는 한 번 읽으면 게임을 끌 때까지 기억합니다. 실행 중에 파일을 바꿨다면 게임을 다시 켭니다.
- 유저가 만든 코드(스크립트)를 넣는 방법은 아직 없습니다.
