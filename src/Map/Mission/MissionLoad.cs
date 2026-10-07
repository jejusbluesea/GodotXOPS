using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        // 추가 사물 콜라이더 변환: OpenXOPS decide × SMALLOBJECT_COLLISIONSCALE(0.13) × 0.1(미터) = decide × 0.013 = 구 반지름.
        private const float k_addonDecideToRadius = 0.013f;

        private const string k_briefingFolder = "data/briefing";

        private string m_missionName = string.Empty;
        private string m_missionFullname = string.Empty;
        private string m_missionBD1Path = string.Empty;
        private string m_missionPD1Path = string.Empty;
        private string m_missionAddonObjectPath = string.Empty;
        private string m_missionImage0 = string.Empty;
        private string m_missionImage1 = string.Empty;
        private string m_missionBriefing = string.Empty;
        private int m_skyIndex;
        private bool m_adjustCollision;
        private bool m_darkScreen;

        // 경로는 모두 전체 경로다. 해당 항목이 없으면 빈 문자열.
        public string MissionName => m_missionName;
        public string MissionFullname => m_missionFullname;
        public string MissionBD1Path => m_missionBD1Path;
        public string MissionPD1Path => m_missionPD1Path;
        public string MissionAddonObjectPath => m_missionAddonObjectPath;
        public string MissionImage0 => m_missionImage0;
        public string MissionImage1 => m_missionImage1;
        public string MissionBriefing => m_missionBriefing;
        public int SkyIndex => m_skyIndex;
        public bool AdjustCollision => m_adjustCollision;
        public bool DarkScreen => m_darkScreen;

        /// <summary>
        /// 지정된 미션의 정보(이름, 맵 경로, 하늘 번호, 브리핑 등)를 읽어 MapLoader 에 세팅한다. 맵 자체는 로드하지 않는다.
        /// </summary>
        /// <param name="index">미션 목록의 인덱스.</param>
        /// <param name="mif">true면 어드온 .mif 파일, false면 공식 미션 데이터를 읽는다.</param>
        /// <param name="page">어드온 페이지(0-기반). 공식 미션(mif=false)이면 무시된다.</param>
        /// <returns>읽기에 성공했으면 true. 인덱스가 범위를 벗어났거나 .mif 형식이 잘못됐으면 false.</returns>
        public static bool LoadMissionData(int index, bool mif, int page)
        {
            UnloadMissionData();

            MapLoader loader = Instance;
            MissionData missionData = DataManager.Instance.MissionData;

            if (mif)
            {
                if (page < 0 || page >= missionData.addonMissions.Count
                    || index < 0 || index >= missionData.addonMissions[page].Count)
                {
                    return false;
                }

                return LoadMissionFile(missionData.addonMissions[page][index].mifPath);
            }

            if (index < 0 || index >= missionData.officialMissions.Count)
            {
                return false;
            }

            OfficialMissionData official = missionData.officialMissions[index];
            loader.m_missionName = official.name;
            loader.m_missionFullname = official.fullname;
            loader.m_missionBD1Path = GamePath.Resolve(official.bd1Path) ?? string.Empty;
            loader.m_missionPD1Path = GamePath.Resolve(official.pd1Path) ?? string.Empty;
            loader.m_adjustCollision = official.adjustCollision;
            loader.m_darkScreen = official.darkScreen;

            // 공식 미션 txt: 0 이미지1 / 1 이미지2 / 2 하늘 번호 / 3~ 브리핑. 이미지는 data/briefing 의 bmp 이름(확장자 없음).
            string txtPath = GamePath.Resolve(official.txtPath);
            if (txtPath != null && File.Exists(txtPath))
            {
                string[] txt = EncodingHelper.ReadAllLines(txtPath);
                if (txt.Length > 2)
                {
                    loader.m_missionImage0 = ResolveBriefingImage(txt[0]);
                    loader.m_missionImage1 = ResolveBriefingImage(txt[1]);
                    if (int.TryParse(txt[2].Trim(), out int skyIndex))
                    {
                        loader.m_skyIndex = skyIndex;
                    }
                    loader.m_missionBriefing = string.Join("\n", txt, 3, txt.Length - 3);
                }
            }
            return true;
        }

        /// <summary>
        /// 미션 파일(.mif) 하나를 읽어 미션 정보를 MapLoader 에 세팅한다. 맵 자체는 로드하지 않는다.
        /// 미션 목록에 없는 파일도 읽을 수 있다 (디버그 콘솔의 loadmission).
        /// </summary>
        /// <param name="mifPath">.mif 파일 전체 경로.</param>
        /// <returns>읽기에 성공했으면 true. 파일이 없거나 형식이 잘못됐으면 false.</returns>
        public static bool LoadMissionFile(string mifPath)
        {
            UnloadMissionData();

            if (string.IsNullOrEmpty(mifPath) || !File.Exists(mifPath))
            {
                Debugger.LogError($"Mission file open failed: {mifPath}", nameof(MapLoader));
                return false;
            }

            // .mif: 0 이름 / 1 정식 이름 / 2 BD1 / 3 PD1 / 4 하늘 번호 / 5 화면 플래그 / 6 추가 사물 / 7 이미지1 / 8 이미지2 / 9~ 브리핑
            string[] lines = EncodingHelper.ReadAllLines(mifPath);
            if (lines.Length < 9)
            {
                Debugger.LogError($"Mission file has too few lines: {mifPath}", nameof(MapLoader));
                return false;
            }

            MapLoader loader = Instance;
            loader.m_missionName = lines[0];
            loader.m_missionFullname = lines[1];
            loader.m_missionBD1Path = ResolveMissionPath(lines[2]);
            loader.m_missionPD1Path = ResolveMissionPath(lines[3]);
            loader.m_skyIndex = int.TryParse(lines[4].Trim(), out int skyIndex) ? skyIndex : 0;
            if (int.TryParse(lines[5].Trim(), out int screenFlag))
            {
                loader.m_adjustCollision = (screenFlag & 1) != 0;
                loader.m_darkScreen = (screenFlag & 2) != 0;
            }
            loader.m_missionAddonObjectPath = ResolveMissionPath(lines[6]);
            loader.m_missionImage0 = ResolveMissionPath(lines[7]);
            loader.m_missionImage1 = ResolveMissionPath(lines[8]);
            loader.m_missionBriefing = string.Join("\n", lines, 9, lines.Length - 9);
            return true;
        }

        /// <summary>
        /// 미션 파일 없이 블록 파일과 포인트 파일을 직접 지정해 미션 정보를 세팅한다 (디버그 콘솔의 loadmap). 맵 자체는 로드하지 않는다.
        /// 이름은 블록 파일 이름이고, 브리핑과 이미지는 없으며, 추가 충돌과 어두운 화면은 꺼져 있다.
        /// </summary>
        /// <param name="blockPath">블록 데이터 파일 전체 경로.</param>
        /// <param name="pointPath">포인트 데이터 파일 전체 경로.</param>
        /// <param name="skyIndex">하늘 번호.</param>
        public static void SetDirectMission(string blockPath, string pointPath, int skyIndex)
        {
            UnloadMissionData();

            MapLoader loader = Instance;
            loader.m_missionName = Path.GetFileName(blockPath ?? string.Empty);
            loader.m_missionFullname = loader.m_missionName;
            loader.m_missionBD1Path = blockPath ?? string.Empty;
            loader.m_missionPD1Path = pointPath ?? string.Empty;
            loader.m_skyIndex = skyIndex;
        }

        /// <summary>
        /// MapLoader 에 저장된 모든 미션 정보를 초기화한다.
        /// </summary>
        public static void UnloadMissionData()
        {
            MapLoader loader = Instance;
            loader.m_missionName = string.Empty;
            loader.m_missionFullname = string.Empty;
            loader.m_missionBD1Path = string.Empty;
            loader.m_missionPD1Path = string.Empty;
            loader.m_missionAddonObjectPath = string.Empty;
            loader.m_missionImage0 = string.Empty;
            loader.m_missionImage1 = string.Empty;
            loader.m_missionBriefing = string.Empty;
            loader.m_skyIndex = 0;
            loader.m_adjustCollision = false;
            loader.m_darkScreen = false;
        }

        /// <summary>
        /// .mif 미션 전용 추가 사물(ADDON-OBJECT)을 초기화한다. MissionAddonObjectPath 가 가리키는 txt를 파싱해
        /// ObjectParameterData 의 예약 슬롯(objectGeneralData.addonObjectIndex)에 모델/콜라이더/내구력/소리/점프를 채운다.
        /// 추가 사물이 없는(경로 비었거나 파싱 실패) 미션이면 슬롯을 빈 상태로 되돌린다 → 미션마다 안전하게 호출 가능.
        /// </summary>
        public static void InitializeAddonObject()
        {
            ObjectParameterData op = DataManager.Instance.ObjectParameterData;
            int index = op.objectGeneralData.addonObjectIndex;
            if (index < 0 || index >= op.objectData.Count) return;

            // 아래에서 모델/콜라이더 슬롯에 인덱스로 대입하므로 두 리스트 범위도 함께 확인한다.
            ObjectData data = op.objectData[index];
            if (data.modelIndex < 0 || data.modelIndex >= op.objectModelData.Count) return;
            if (data.colliderIndex < 0 || data.colliderIndex >= op.objectColliderData.Count) return;

            if (!ParseAddonObjectFile(Instance.m_missionAddonObjectPath, out AddonObjectFileData addon))
            {
                // 추가 사물 없음 — 예약 슬롯을 비워 이전 미션 데이터 잔존을 막는다.
                op.objectModelData[data.modelIndex] = new ObjectModelData();
                op.objectColliderData[data.colliderIndex] = new ObjectColliderData();
                data.hp = 0f;
                data.jump = 0;
                data.soundPath = string.Empty;
                return;
            }

            // 모델: 텍스처 1장 + 단일 메시. 변환은 기본값(원점/무회전/스케일 1).
            op.objectModelData[data.modelIndex] = new ObjectModelData
            {
                textures = new List<string> { addon.texturePath },
                modelData = new List<ModelData>
                {
                    new ModelData
                    {
                        modelPath = addon.modelPath,
                        position = Vector3.Zero,
                        rotation = Vector3.Zero,
                        scale = Vector3.One,
                        textureIndex = 0,
                    },
                },
            };

            // 콜라이더: decide → 구 반지름. 기존 소물과 동일하게 단일 구체.
            op.objectColliderData[data.colliderIndex] = new ObjectColliderData
            {
                shapes = new List<ColliderShape>
                {
                    new ColliderShape
                    {
                        type = ColliderShapeType.Sphere,
                        center = Vector3.Zero,
                        size = new Vector3(addon.decide * k_addonDecideToRadius, 0f, 0f),
                    },
                },
            };

            // jump 는 raw 정수 그대로 저장한다 — 파괴 점프 변환은 소물 쪽 상수가 처리한다.
            data.hp = addon.hp;
            data.jump = addon.jump;
            data.soundPath = addon.soundPath;
        }

        /// <summary>
        /// 추가 사물 txt 1개 분량의 파싱 결과. 경로는 데이터 루트 기준 상대 경로다.
        /// </summary>
        private struct AddonObjectFileData
        {
            public string modelPath;
            public string texturePath;
            public int decide;
            public float hp;
            public string soundPath;
            public int jump;
        }

        /// <summary>
        /// 추가 사물 정보 txt(6줄)를 파싱한다.
        /// line0 모델경로 / line1 텍스처경로 / line2 decide(콜라이더 크기) / line3 hp / line4 피격음 경로 / line5 jump(파괴 점프력).
        /// </summary>
        /// <param name="txtPath">txt 전체 경로.</param>
        /// <param name="data">파싱 결과.</param>
        /// <returns>파싱 성공 시 true, 경로가 없거나 6줄 미만이면 false.</returns>
        private static bool ParseAddonObjectFile(string txtPath, out AddonObjectFileData data)
        {
            data = default;
            if (string.IsNullOrEmpty(txtPath) || !File.Exists(txtPath)) return false;

            string[] lines = EncodingHelper.ReadAllLines(txtPath);
            if (lines.Length < 6) return false;

            // 원본 ENABLE_ADDOBJ_PARAM8BIT(main.h:73, 기본 ON) — 원조 XOPS 호환을 위해 char 8비트 마스킹.
            // 클램프가 아닌 비트 AND 라 범위 초과값은 wrap 된다 (decide/hp 0~127, jump 0~255).
            data = new AddonObjectFileData
            {
                modelPath = NormalizeXopsPath(lines[0]),
                texturePath = NormalizeXopsPath(lines[1]),
                decide = (int.TryParse(lines[2].Trim(), out int d) ? d : 0) & 0x7F,
                hp = (int.TryParse(lines[3].Trim(), out int h) ? h : 0) & 0x7F,
                soundPath = NormalizeXopsPath(lines[4]),
                jump = (int.TryParse(lines[5].Trim(), out int j) ? j : 0) & 0xFF,
            };
            return true;
        }

        /// <summary>
        /// OpenXOPS 경로(".\data\.." 또는 "./data/..")를 데이터 루트 기준 상대 경로("data/..")로 정규화한다.
        /// </summary>
        /// <param name="raw">파일에 적힌 경로.</param>
        /// <returns>정규화된 상대 경로. "!"(없음)나 빈 줄이면 빈 문자열.</returns>
        private static string NormalizeXopsPath(string raw)
        {
            string s = raw.Trim();
            if (string.IsNullOrEmpty(s) || s == "!") return string.Empty;
            return s.TrimStart('.', '\\', '/').Replace('\\', '/');
        }

        /// <summary>
        /// 미션 파일에 적힌 OpenXOPS 경로를 전체 경로로 바꾼다.
        /// </summary>
        /// <param name="raw">파일에 적힌 경로.</param>
        /// <returns>전체 경로. 없음("!")이거나 데이터 루트를 벗어나면 빈 문자열.</returns>
        private static string ResolveMissionPath(string raw)
        {
            return GamePath.Resolve(NormalizeXopsPath(raw)) ?? string.Empty;
        }

        /// <summary>
        /// 공식 미션 txt 에 적힌 브리핑 이미지 이름을 data/briefing 의 bmp 전체 경로로 바꾼다.
        /// </summary>
        /// <param name="name">확장자 없는 이미지 이름.</param>
        /// <returns>전체 경로. 없음("!")이면 빈 문자열.</returns>
        private static string ResolveBriefingImage(string name)
        {
            string trimmed = name.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == "!") return string.Empty;
            return GamePath.Resolve($"{k_briefingFolder}/{trimmed}.bmp") ?? string.Empty;
        }
    }
}
