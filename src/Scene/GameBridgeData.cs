using System.Text;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    // GameBridge 의 조회 담당 partial: 미션 목록, 브리핑, 통계, 버전·크레딧, 텍스처.
    public partial class GameBridge
    {
        /// <summary>
        /// 공식 미션 수.
        /// </summary>
        /// <returns>미션 수.</returns>
        public int OfficialMissionCount()
        {
            return DataManager.Instance.MissionData.officialMissions.Count;
        }

        /// <summary>
        /// 공식 미션의 목록 표시 이름.
        /// </summary>
        /// <param name="index">미션 인덱스.</param>
        /// <returns>이름. 범위 밖이면 빈 문자열.</returns>
        public string OfficialMissionName(int index)
        {
            var missions = DataManager.Instance.MissionData.officialMissions;
            return index >= 0 && index < missions.Count ? missions[index].Name ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// 어드온 페이지 수. 페이지 0 은 addon 폴더이고 그 뒤는 addon.json 으로 추가한 폴더다.
        /// </summary>
        /// <returns>페이지 수.</returns>
        public int AddonPageCount()
        {
            return DataManager.Instance.MissionData.addonMissions.Count;
        }

        /// <summary>
        /// 어드온 페이지의 이름.
        /// </summary>
        /// <param name="page">페이지.</param>
        /// <returns>이름. 없으면 빈 문자열.</returns>
        public string AddonPageName(int page)
        {
            var names = DataManager.Instance.MissionData.addonPageNames;
            return page >= 0 && page < names.Count ? names[page] ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// 어드온 페이지 하나의 미션 수.
        /// </summary>
        /// <param name="page">페이지.</param>
        /// <returns>미션 수. 페이지가 범위 밖이면 0.</returns>
        public int AddonMissionCount(int page)
        {
            var pages = DataManager.Instance.MissionData.addonMissions;
            return page >= 0 && page < pages.Count ? pages[page].Count : 0;
        }

        /// <summary>
        /// 어드온 미션의 목록 표시 이름.
        /// </summary>
        /// <param name="page">페이지.</param>
        /// <param name="index">미션 인덱스.</param>
        /// <returns>이름. 범위 밖이면 빈 문자열.</returns>
        public string AddonMissionName(int page, int index)
        {
            var pages = DataManager.Instance.MissionData.addonMissions;
            if (page < 0 || page >= pages.Count || index < 0 || index >= pages[page].Count) return string.Empty;

            return pages[page][index].Name ?? string.Empty;
        }

        /// <summary>
        /// 로드된 미션의 정식 이름.
        /// </summary>
        /// <returns>정식 이름.</returns>
        public string MissionFullname()
        {
            return MapLoader.Instance.MissionFullname ?? string.Empty;
        }

        /// <summary>
        /// 로드된 미션의 브리핑 본문.
        /// </summary>
        /// <returns>브리핑 본문. 줄바꿈이 들어 있다.</returns>
        public string MissionBriefing()
        {
            return MapLoader.Instance.MissionBriefing ?? string.Empty;
        }

        /// <summary>
        /// 로드된 미션의 브리핑 이미지.
        /// </summary>
        /// <param name="slot">0 = 첫째 이미지, 1 = 둘째 이미지.</param>
        /// <returns>텍스처. 이미지가 없으면 null.</returns>
        public Texture2D MissionImage(int slot)
        {
            string path = slot == 0 ? MapLoader.Instance.MissionImage0 : MapLoader.Instance.MissionImage1;
            return string.IsNullOrEmpty(path) ? null : ImageLoader.LoadTexture(path);
        }

        /// <summary>
        /// 데이터 폴더의 이미지 파일을 텍스처로 읽는다.
        /// </summary>
        /// <param name="relativePath">데이터 루트 기준 경로. 예: "data/title.dds".</param>
        /// <returns>텍스처. 경로가 비었거나 읽지 못하면 null.</returns>
        public Texture2D LoadTexture(string relativePath)
        {
            string path = string.IsNullOrEmpty(relativePath) ? null : GamePath.Resolve(relativePath);
            return path != null ? ImageLoader.LoadTexture(path) : null;
        }

        /// <summary>
        /// 플레이어의 미션 통계.
        /// </summary>
        /// <returns>playTime(초), fire, onTarget, accuracy(%), kill, headshot 을 담은 사전.</returns>
        public Godot.Collections.Dictionary GetStats()
        {
            MissionStats stats = MapLoader.Stats;
            return new Godot.Collections.Dictionary
            {
                { "playTime", stats.PlayTime },
                { "fire", stats.Fire },
                { "onTarget", stats.OnTargetInt },
                { "accuracy", stats.AccuracyPercent },
                { "kill", stats.Kill },
                { "headshot", stats.Headshot },
            };
        }

        /// <summary>
        /// 게임 버전 문자열 (major.minor.patch).
        /// </summary>
        /// <returns>버전.</returns>
        public string Version()
        {
            return DataManager.Instance.GlobalData.Version;
        }

        /// <summary>
        /// 크레딧 창에 띄울 본문. 제품명·버전, 제작자, 라이선스를 여러 줄로 잇는다.
        /// </summary>
        /// <returns>크레딧 본문.</returns>
        public string CreditText()
        {
            GlobalData global = DataManager.Instance.GlobalData;
            var text = new StringBuilder();
            text.Append(global.productName).Append("  v").Append(global.Version).Append('\n');
            text.Append(global.companyName).Append("\n\n");
            text.Append(global.licenseType).Append('\n');
            text.Append(global.licenseName).Append(' ').Append(global.companyName).Append("\n\n");
            text.Append(string.Join("\n", global.licenseLines));
            return text.ToString();
        }
    }
}
