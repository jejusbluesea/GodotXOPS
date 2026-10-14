namespace GodotXOPS
{
    // GameBridge 의 메뉴 맵팩 담당 partial: 메뉴가 맵팩의 페이지를 보고 있는 동안 그 맵팩의 글꼴 그림, 이미지 목록, 메뉴 스크립트(타이틀과 버전 자리)를 쓰게 한다.
    // 미션 정보와 따로 든다. 미션을 고르면 미션 정보에 붙는 맵팩이 먼저다.
    public partial class GameBridge
    {
        // 메뉴가 보고 있는 맵팩 페이지의 맵팩. 공식 미션 탭, addon 폴더의 페이지, 메뉴 밖이면 null.
        private MapPackManifestData m_menuPack;
        // 그 맵팩의 이미지 목록 (Manifest 의 imageDataPath). 메뉴에는 미션 정보가 없어서 게임의 이미지 목록에 붙이지 않고 따로 든다.
        private ImageParameterData m_menuPackImages;

        /// <summary>
        /// 메뉴가 지금 보고 있는 페이지를 알린다. 맵팩의 페이지면 그 맵팩의 글꼴 그림과 이미지 목록과 메뉴 스크립트를 쓰게 되고, 아니면 원래대로다.
        /// 메뉴(기본 화면이나 메뉴 스크립트의 show_page)가 탭이나 페이지를 바꿀 때마다, 메뉴를 떠날 때 부른다.
        /// </summary>
        /// <param name="addon">에드온 탭이면 true. 거짓이면 page 는 무시한다.</param>
        /// <param name="page">에드온 페이지 (0 은 addon 폴더).</param>
        /// <returns>맵팩이 바뀌었으면 true (메뉴가 글자를 다시 그리고 메뉴 스크립트를 바꿔야 한다).</returns>
        public bool SetMenuPage(bool addon, int page)
        {
            var packs = DataManager.Instance.MissionData.addonPagePacks;
            MapPackManifestData pack = addon && page >= 0 && page < packs.Count ? packs[page] : null;
            if (ReferenceEquals(pack, m_menuPack)) return false;

            m_menuPack = pack;
            m_menuPackImages = pack == null ? null : MapLoader.LoadAddonFile<ImageParameterData>(pack.imageDataPath);
            return true;
        }

        /// <summary>
        /// Manifest 하나를 읽어 어드온 페이지의 끝에 더한다 (개발용 인자 --map-pack. addon.json 을 고치지 않고 맵팩을 돌려 본다).
        /// </summary>
        /// <param name="manifestPath">Manifest 경로 (exe 폴더 기준).</param>
        /// <returns>더했으면 true. 읽지 못했으면 false (에러가 남는다).</returns>
        public bool AddMapPack(string manifestPath)
        {
            MapPackManifestData pack = DataManager.ReadMapPack(manifestPath);
            if (pack == null) return false;
            DataManager.Instance.AddMapPackPage(pack);
            return true;
        }

        /// <summary>
        /// 메뉴가 맵팩의 페이지를 보고 있는지.
        /// </summary>
        /// <returns>맵팩의 페이지면 true.</returns>
        public bool MenuPackActive()
        {
            return m_menuPack != null;
        }

        /// <summary>
        /// 메뉴가 보고 있는 맵팩의 이름.
        /// </summary>
        /// <returns>이름. 맵팩의 페이지가 아니면 빈 문자열.</returns>
        public string MenuPackName()
        {
            return m_menuPack?.name ?? string.Empty;
        }

        /// <summary>
        /// 메뉴가 보고 있는 맵팩의 이미지 목록의 항목 수 (10000 부터의 번호가 몇 개인지).
        /// </summary>
        /// <returns>항목 수. 맵팩의 페이지가 아니거나 이미지 목록이 없으면 0.</returns>
        public int MenuPackImageCount()
        {
            return m_menuPackImages?.imageData.Count ?? 0;
        }

        /// <summary>
        /// 메뉴가 보고 있는 맵팩의 이미지 목록에서 번호(10000 에서 19999)의 파일을 찾는다. 미션 정보에 맵팩이 붙어 있으면 그쪽이 먼저이므로 찾지 않는다.
        /// </summary>
        /// <param name="number">이미지 목록의 번호.</param>
        /// <param name="path">파일 경로 (exe 폴더 기준).</param>
        /// <returns>찾았으면 true.</returns>
        private bool MenuPackImagePath(int number, out string path)
        {
            path = string.Empty;
            if (m_menuPackImages == null || MapLoader.MapPack != null) return false;
            if (number < DataList<ImageData>.PackBase || number >= DataList<ImageData>.MissionBase) return false;

            int index = number - DataList<ImageData>.PackBase;
            if (index >= m_menuPackImages.imageData.Count) return false;
            path = m_menuPackImages.imageData[index].imagePath ?? string.Empty;
            return true;
        }
    }
}
