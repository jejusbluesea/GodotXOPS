using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 게임 데이터를 JSON에서 로드하고 전역 접근을 제공하는 싱글톤 매니저.
    /// </summary>
    public partial class DataManager : Singleton<DataManager>
    {
        public HumanParameterData HumanParameterData { get; private set; } = new HumanParameterData();
        public WeaponParameterData WeaponParameterData { get; private set; } = new WeaponParameterData();
        public ObjectParameterData ObjectParameterData { get; private set; } = new ObjectParameterData();
        public EffectParameterData EffectParameterData { get; private set; } = new EffectParameterData();
        public BlockMaterialParameterData BlockMaterialParameterData { get; private set; } = new BlockMaterialParameterData();
        public SkyData SkyData { get; private set; } = new SkyData();
        public MissionData MissionData { get; private set; } = new MissionData();
        public GlobalData GlobalData { get; private set; } = new GlobalData();

        public override void _Ready()
        {
            LoadHumanParameterData();
            LoadWeaponParameterData();
            LoadObjectParameterData();
            LoadEffectParameterData();
            LoadBlockMaterialParameterData();
            LoadSkyData();
            LoadMissionData();
            LoadGlobalData();
        }

        /// <summary>
        /// 데이터 루트 기준 상대 경로의 JSON 텍스트를 읽는다. 파일이 없거나 읽을 수 없거나 내용이 비어 있으면
        /// 에디터에서만 로그를 남기고 실패를 알린다.
        /// </summary>
        /// <param name="relativePath">데이터 루트 기준 JSON 파일 경로.</param>
        /// <param name="json">읽어낸 JSON 텍스트. 실패 시 null.</param>
        /// <returns>읽기에 성공했으면 true, 파일이 없거나 읽기에 실패했거나 내용이 비어 있으면 false.</returns>
        private static bool TryReadJson(string relativePath, out string json)
        {
            json = null;

            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                Debugger.LogError($"데이터 파일을 찾을 수 없습니다(기본값 사용): {relativePath}", nameof(DataManager));
                return false;
            }

            string text;
            try
            {
                text = EncodingHelper.ReadAllText(fullPath);
            }
            catch (IOException e)
            {
                // 파일 잠김·네트워크 드라이브 등 읽기 자체의 실패. 여기서 막지 않으면 예외가 _Ready로 올라가 뒤따르는 도메인 로드가 전부 중단된다.
                Debugger.LogError($"데이터 파일을 읽을 수 없습니다(기본값 사용): {relativePath}\n{e.Message}", nameof(DataManager));
                return false;
            }
            catch (System.UnauthorizedAccessException e)
            {
                Debugger.LogError($"데이터 파일에 접근할 수 없습니다(기본값 사용): {relativePath}\n{e.Message}", nameof(DataManager));
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debugger.LogError($"데이터 파일이 비어 있습니다(기본값 사용): {relativePath}", nameof(DataManager));
                return false;
            }

            json = text;
            return true;
        }

        /// <summary>
        /// 데이터 루트 기준 상대 경로의 JSON을 읽어 대상 객체에서 그 파일에 존재하는 필드만 덮어쓴다.
        /// 여러 섹션 파일을 하나의 컨테이너로 병합할 때 사용한다. 파일이 없거나 비어 있거나 JSON 형식이 깨졌으면
        /// 대상을 건드리지 않고(=컨테이너 필드는 미리 초기화된 기본값 유지) 에디터에서만 로그를 남긴 뒤 반환한다.
        /// 한 파일이 잘못돼도 예외가 밖으로 나가지 않으므로 다른 로드는 계속 진행된다.
        /// </summary>
        /// <param name="relativePath">데이터 루트 기준 JSON 파일 경로.</param>
        /// <param name="target">병합 대상 객체. 파일에 있는 필드만 이 객체에 덮어쓴다.</param>
        private static void OverwriteFromJson(string relativePath, object target)
        {
            if (!TryReadJson(relativePath, out string json))
            {
                return;
            }

            JsonData.Overwrite(json, target, relativePath);
        }

        /// <summary>
        /// 루트 오브젝트 하나로 이루어진 JSON 파일을 통째로 읽어 새 인스턴스를 만든다.
        /// 섹션 분리를 하지 않은 단일 파일 도메인용으로, 어느 실패 경우든 필드가 기본값인 인스턴스를 돌려준다.
        /// </summary>
        /// <typeparam name="T">역직렬화할 데이터 클래스. 필드는 기본값으로 초기화돼 있어야 한다.</typeparam>
        /// <param name="relativePath">데이터 루트 기준 JSON 파일 경로.</param>
        /// <returns>로드된 데이터. 실패하면 필드가 기본값인 빈 인스턴스(null 아님).</returns>
        private static T LoadOrDefault<T>(string relativePath) where T : new()
        {
            T result = new T();
            OverwriteFromJson(relativePath, result);
            return result;
        }

        private void LoadEffectParameterData()
        {
            EffectParameterData = LoadOrDefault<EffectParameterData>(k_effectDataPath);
        }

        private void LoadBlockMaterialParameterData()
        {
            BlockMaterialParameterData = LoadOrDefault<BlockMaterialParameterData>(k_blockMaterialDataPath);
        }

        private void LoadSkyData()
        {
            SkyData = LoadOrDefault<SkyData>(k_skyDataPath);
        }

        private void LoadMissionData()
        {
            MissionData = LoadOrDefault<MissionData>(k_missionDataPath);

            LoadAddonMissions();
        }

        /// <summary>
        /// 어드온 미션을 페이지 단위로 로드한다. 페이지 0은 항상 기본 OpenXOPS 어드온 폴더("addon") 폴백(이름 없음).
        /// 페이지 1~은 addon.json의 addonPath 목록(유저 추가 경로)으로, 파일이 없거나 목록이 없으면 폴백만 남는다.
        /// 경로는 항상 데이터 루트 기준이며 루트 밖으로 나가는 경로는 무시한다.
        /// 경로가 감지되면 폴더가 없거나 .mif가 없어도 빈 페이지로 추가한다. 페이지 이름은 같은 인덱스의 addonName(없으면 빈 문자열).
        /// </summary>
        private void LoadAddonMissions()
        {
            MissionData.addonMissions = new List<List<AddonMissionData>>();
            MissionData.addonPageNames = new List<string>();

            // 페이지 0: 항상 존재하는 폴백 = 기본 OpenXOPS 어드온 폴더. 페이지 이름 없음.
            MissionData.addonMissions.Add(ScanAddonMifs(GamePath.Resolve(GamePath.AddonFolder)));
            MissionData.addonPageNames.Add(string.Empty);

            // addon.json은 선택 파일이므로 부재를 먼저 걸러 TryReadJson의 "찾을 수 없습니다" 에러 로그가 상시 뜨는 것을 막는다.
            if (!File.Exists(GamePath.Resolve(k_addonPathDataPath)))
            {
                return;
            }

            AddonPathData addonPathData = LoadOrDefault<AddonPathData>(k_addonPathDataPath);
            if (addonPathData.addonPath == null)
            {
                return;
            }

            for (int i = 0; i < addonPathData.addonPath.Count; i++)
            {
                string packDirectory = GamePath.Resolve(addonPathData.addonPath[i]);
                if (packDirectory == null)
                {
                    continue;
                }

                MissionData.addonMissions.Add(ScanAddonMifs(packDirectory));
                string pageName = (addonPathData.addonName != null && i < addonPathData.addonName.Count)
                    ? addonPathData.addonName[i]
                    : string.Empty;
                MissionData.addonPageNames.Add(pageName ?? string.Empty);
            }
        }

        /// <summary>
        /// 지정한 디렉터리에서 .mif 파일을 스캔해 어드온 미션 목록을 만든다. 파일 순서는 파일명 자연 정렬(숫자 값 비교).
        /// </summary>
        /// <param name="directory">스캔할 맵 팩 디렉터리(전체 경로).</param>
        /// <returns>해당 디렉터리의 어드온 미션 목록. 디렉터리가 없으면 빈 목록.</returns>
        private static List<AddonMissionData> ScanAddonMifs(string directory)
        {
            var page = new List<AddonMissionData>();
            if (directory == null || !Directory.Exists(directory))
            {
                return page;
            }

            // Directory.GetFiles는 순서를 보장하지 않으므로 파일명 기준으로 자연 정렬한다(gates2 < gates10처럼 숫자를 값으로 비교).
            string[] mifPaths = Directory.GetFiles(directory, "*.mif");
            System.Array.Sort(mifPaths, (a, b) => CompareNatural(Path.GetFileName(a), Path.GetFileName(b)));
            foreach (string path in mifPaths)
            {
                string[] lines = EncodingHelper.ReadAllLines(path);
                page.Add(new AddonMissionData
                {
                    mifPath = path,
                    name = lines.Length > 0 ? lines[0] : string.Empty
                });
            }
            return page;
        }

        /// <summary>
        /// 두 문자열을 자연 정렬 순서로 비교한다. 숫자 구간은 값으로 비교해 gates2가 gates10보다 앞서게 하고,
        /// 그 외 문자는 대소문자 무시 비교한다. 선행 0(zero-padding)은 값에 영향을 주지 않는다.
        /// </summary>
        /// <param name="a">비교 대상 문자열 A.</param>
        /// <param name="b">비교 대상 문자열 B.</param>
        /// <returns>A가 앞서면 음수, 같으면 0, 뒤면 양수.</returns>
        private static int CompareNatural(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int startA = i, startB = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;

                    // 선행 0 제거 후 자릿수 → 값 비교. 값이 같으면 자릿수 적은(패딩 없는) 쪽을 앞에 둔다.
                    string na = a.Substring(startA, i - startA).TrimStart('0');
                    string nb = b.Substring(startB, j - startB).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length - nb.Length;
                    int digitCompare = string.CompareOrdinal(na, nb);
                    if (digitCompare != 0) return digitCompare;
                    if ((i - startA) != (j - startB)) return (i - startA) - (j - startB);
                }
                else
                {
                    int charCompare = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (charCompare != 0) return charCompare;
                    i++;
                    j++;
                }
            }
            return (a.Length - i) - (b.Length - j);
        }

        /// <summary>
        /// 전역 데이터를 JSON에서 로드한다. 파일이 없으면 프로젝트 설정 값으로 새 JSON 파일을 만들어 두고 그 값을 그대로 쓴다.
        /// </summary>
        private void LoadGlobalData()
        {
            string fullPath = GamePath.Resolve(k_globalDataPath);
            if (File.Exists(fullPath))
            {
                GlobalData = LoadOrDefault<GlobalData>(k_globalDataPath);
                return;
            }

            GlobalData = BuildGlobalDataFromProjectSettings();

            // 데이터 폴더가 읽기 전용인 배포 환경에서는 생성에 실패할 수 있다. 메모리상 GlobalData 는 이미 유효하므로 로그만 남기고 진행한다.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, JsonData.ToJson(GlobalData));
            }
            catch (IOException e)
            {
                Debugger.LogError($"전역 데이터 파일을 만들 수 없습니다: {k_globalDataPath}\n{e.Message}", nameof(DataManager));
            }
            catch (System.UnauthorizedAccessException e)
            {
                Debugger.LogError($"전역 데이터 파일을 만들 권한이 없습니다: {k_globalDataPath}\n{e.Message}", nameof(DataManager));
            }
        }

        /// <summary>
        /// 프로젝트 설정(제품명·버전)으로 기본 전역 데이터를 만든다. 회사명과 라이선스 항목은 비워 둔다.
        /// </summary>
        /// <returns>프로젝트 설정 값이 채워진 GlobalData.</returns>
        private static GlobalData BuildGlobalDataFromProjectSettings()
        {
            var data = new GlobalData();
            data.productName = ProjectSettings.GetSetting("application/config/name", string.Empty).AsString();

            // 버전은 '.'로 나눠 앞의 둘을 major/minor로 쓰고, 남는 조각은 다시 '.'로 이어 patch로 둔다(예: "1.2.3.4" → patch "3.4").
            string version = ProjectSettings.GetSetting("application/config/version", string.Empty).AsString();
            string[] parts = string.IsNullOrEmpty(version) ? new string[0] : version.Split('.');
            data.versionMajor = parts.Length > 0 ? parts[0] : "0";
            data.versionMinor = parts.Length > 1 ? parts[1] : "0";
            data.versionPatch = parts.Length > 2 ? string.Join(".", parts, 2, parts.Length - 2) : "0";
            return data;
        }
    }
}
