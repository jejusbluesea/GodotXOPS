[English](README.en.md) | [日本語](README.ja.md) | **한국어**

# GodotXOPS

일본의 인디 FPS **XOPS**(X operations, 2000년)를 Godot 엔진으로 옮긴 프로젝트입니다. 오픈소스 구현인 [OpenXOPS](https://openxops.net/)와 그 Unity 포팅본인 [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS)를 참고해 만들었습니다.

- 엔진: Godot 4.7.2 (.NET)
- 플랫폼: Windows
- 최신 릴리즈: [1.1.0](https://github.com/jejusbluesea/GodotXOPS/releases/tag/v1.1.0)

원본의 조작감을 그대로 재현하는 것이 목표입니다. 이동, 충돌, 총알 판정은 엔진 물리를 쓰지 않고 원본의 계산 방식을 직접 옮겼고, 게임 진행은 원본과 같은 초당 33.33틱으로 돕니다.

## 할 수 있는 것

- 원본 미션과 에드온 미션 플레이 (오프닝 → 메뉴 → 브리핑 → 게임 → 결과)
- 원본의 AI, 미션 이벤트, 클리어·실패 판정
- 메뉴의 OPTION 에서 키 바인딩, 해상도, 밝기·감마, 조준선 모양, 음량 설정
- 사람·무기·오브젝트·이펙트 수치를 `godotdata/`의 JSON 으로 수정
- `addon.json`으로 에드온 폴더를 여러 페이지로 나누어 등록
- 확장 파일 형식 (BD2, PD2, MIF2): 텍스처 개수, 포인트 번호, 이벤트 줄 수의 제한이 없고, 미션이 자기만의 사람·무기·오브젝트·이펙트·재질·소리를 들고 올 수 있습니다. 원본 형식(BD1, PD1, MIF)도 그대로 읽습니다
- 스크립트 이벤트: 원본의 이벤트 열 가지에 더해 변수와 분기, 스폰, 화면 글자, 오브젝트와 블록 움직이기, 소리 재생 등 36종이 들어 있고, 직접 만든 이벤트 묶음을 더할 수 있습니다. 스크립트는 격리된 환경에서만 돕니다
- [에디터](#에디터): 블록, 포인트와 이벤트, 미션, 데이터를 고치고 바로 플레이해 봅니다
- 디버그 콘솔 (`godotdata/config.json`의 `AllowConsole`을 `"true"`로 바꾸면 F11)

## 설치와 실행

원본 XOPS 의 에셋(`data` 폴더)은 저작권 때문에 이 저장소와 릴리즈 파일에 들어 있지 않습니다. 원본 XOPS 에서 직접 가져와야 합니다.

1. [릴리즈 페이지](https://github.com/jejusbluesea/GodotXOPS/releases)에서 `GodotXOPS_x.y.z.7z`를 받아 압축을 풉니다.
2. 원본 XOPS 의 `data` 폴더를 `GodotXOPS.exe`가 있는 폴더에 복사합니다.
3. 에드온 미션을 쓰려면 `addon` 폴더도 같은 자리에 둡니다.
4. `GodotXOPS.exe`를 실행합니다.

폴더는 이렇게 됩니다.

```
GodotXOPS/
├─ GodotXOPS.exe
├─ GodotXOPS_Editor.bat             (에디터 실행)
├─ GodotXOPS.pck
├─ libgodot_riscv.windows.template_release.x86_64.dll   (스크립트 이벤트의 실행기)
├─ data_GodotXOPS_windows_x86_64/   (.NET 런타임. 게임 데이터가 아닙니다)
├─ godotdata/                       (설정과 게임 수치 JSON)
├─ addon.json
├─ LICENSE.txt
├─ THIRD_PARTY_NOTICES.txt
├─ data/                            (원본 XOPS 에서 복사)
└─ addon/                           (선택)
```

## 조작

기본값입니다. 메뉴의 OPTION → Input 에서 바꿀 수 있습니다.

| 동작 | 키 |
|---|---|
| 이동 | W / A / S / D |
| 시점 | 마우스 (방향키로도 가능) |
| 발사 | 마우스 왼쪽 버튼 |
| 점프 | Space |
| 걷기 | Tab |
| 재장전 | R |
| 스코프 | 왼쪽 Shift |
| 무기 슬롯 1 / 2 | 1 / 2 |
| 발사 방식 전환 (단발 / 연발 등, 이전 / 다음) | Z / X |
| 무기 버리기 | G |
| 상호작용 (미션의 이벤트가 쓸 때만) | F |
| 시점 전환 (1인칭 / 3인칭) | F1 |
| HUD 표시 방식 | F2 |
| 미션 재시작 | F12 |
| 메뉴로 나가기 | ESC |

원본의 치트 키(F5 ~ F9)도 그대로 들어 있습니다.

## 에디터

`GodotXOPS_Editor.bat`을 실행합니다 (`GodotXOPS.exe -- --scene editor`와 같습니다). 확장 형식의 블록(BD2), 포인트와 이벤트(PD2), 미션(MIF2), 데이터(JSON)를 만들고 고칩니다. 원본 형식의 맵과 미션은 File 의 Import 로 확장 형식으로 바꿔서 엽니다.

- 조작은 블렌더의 기본 키를 따릅니다: 가운데 버튼으로 시점, 왼쪽 클릭으로 선택, G / R / S 로 옮기기·돌리기·크기 바꾸기. 같은 기능이 메뉴와 버튼에도 있습니다.
- F5 로 지금 내용을 바로 플레이해 보고 Esc 로 돌아옵니다.
- 화면의 글자는 영어입니다. 조작 전체는 [개발 문서의 에디터](docs/development.md#에디터)에 있습니다.

## 에드온 페이지

`addon` 폴더는 기본으로 에드온 목록의 첫 페이지가 됩니다. 폴더를 더 등록하려면 `addon.json`에 경로와 페이지 이름을 같은 개수로 적습니다. 경로는 `GodotXOPS.exe`가 있는 폴더 기준입니다.

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

## 문서

- [모딩 문서](docs/modding.md) — `godotdata/` JSON 으로 무기·사람·오브젝트·이펙트·미션을 고치는 방법, 확장 파일 형식, 스크립트 이벤트, 에드온 페이지
- [개발 문서](docs/development.md) — 소스에서 빌드, 코드 구조, 점검 도구, 에디터, 디버그 콘솔, 원본과 다르게 한 동작

## 원본과의 차이

원본을 최대한 그대로 구현하려 했고 OpenXOPS 를 기준으로 삼았습니다. 다만 OpenXOPS 도 원본을 완전히 똑같이 구현한 것은 아니므로, 원본 XOPS 와 다르게 느껴지는 부분이 있을 수 있습니다. 다른 점을 발견하면 [이슈](https://github.com/jejusbluesea/GodotXOPS/issues)로 알려 주세요.

## 앞으로 할 것

1.0.0 으로 포팅을 마쳤고, 1.1.0 에서 확장 파일 형식과 스크립트 이벤트, 에디터를 넣었습니다. 다음 버전에 넣을 것은 정해지는 대로 [로드맵](ROADMAP.md)에 적습니다.

## 라이선스와 고지

- 이 저장소의 코드는 [MIT License](LICENSE) 입니다.
- XOPS 의 에셋(`data`, `addon`)은 원저작자의 것이며 이 저장소에 포함되어 있지 않습니다.
- [Godot Engine](https://godotengine.org) (MIT) 으로 만들었고, 스크립트 이벤트의 실행에 [Godot Sandbox](https://github.com/libriscv/godot-sandbox) 0.60 (Alf-André Walla, BSD-3-Clause) 을 씁니다. 라이선스 전문은 릴리즈 파일의 `THIRD_PARTY_NOTICES.txt`([저장소의 것](dist/THIRD_PARTY_NOTICES.txt))에 있습니다.
- 코드 작성과 번역에 AI 를 사용했습니다. 2D·3D·사운드 에셋은 AI 생성물이 아닙니다.

## 참고한 프로젝트

- XOPS — nine-two
- [OpenXOPS](https://openxops.net/) — OpenXOPS Project
- [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS)
