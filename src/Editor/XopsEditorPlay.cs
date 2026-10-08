using System;
using System.IO;
using System.Text.Json;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        // 플레이 테스트용 파일을 쓰는 폴더 (exe 폴더 기준). 플레이할 때마다 덮어쓴다.
        private const string k_playFolder = "editor_temp";
        private const string k_playName = "play";
        private const string k_playScene = "maingame";
        private const string k_ignoreFile = ".gdignore";

        // 플레이 테스트로 트리에서 떨어져 있는지. 그동안에는 트리를 나가도 블록을 내리지 않는다 (게임이 로드한 맵이다).
        private bool m_playing;
        // 가져올 원본 미션: 미션 파일(.mif)의 경로, 또는 공식 미션의 번호 (경로가 null 일 때).
        private string m_importSource;
        private int m_importOfficial;
        private AcceptDialog m_officialDialog;
        private ItemList m_officialList;

        /// <summary>
        /// 창을 에디터용으로 맞춘다: 게임의 저해상도 렌더를 끄고, 닫기 버튼을 직접 받고, 커서를 보인다.
        /// </summary>
        private void SetupWindow()
        {
            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            root.Title = "GodotXOPS Editor";
            Engine.MaxFps = 0;
            GetTree().AutoAcceptQuit = false;
            InputManager.Instance.MouseCursorMode(false, false, false);
        }

        /// <summary>
        /// 지금 편집 중인 내용(저장하지 않은 것까지)으로 미션을 플레이한다 (F5). 내용을 임시 파일로 쓰고 메인게임 화면으로 넘어간다.
        /// 에디터는 지워지지 않고 맡겨 뒀다가, 메인게임에서 나가거나(ESC) 미션이 끝나면 그대로 돌아온다. 편집 중인 파일은 건드리지 않는다.
        /// 미션 파일에서 열었으면 그 미션의 설정(하늘, 추가 충돌, 어두운 화면, 에드온 데이터)을 쓰고, 아니면 하늘만 정한 기본 설정이다.
        /// </summary>
        /// <returns>플레이를 시작했으면 true.</returns>
        private bool PlayTest()
        {
            if (Transforming) ConfirmTransform();
            string missionPath = WritePlayFiles(out string error);
            if (missionPath == null) return Fail($"Play test files could not be written ({error})");

            GameBridge game = GameBridge.Instance;
            bool loaded = game.LoadMissionFile(GamePath.Resolve(missionPath));
            if (!loaded || MapLoader.Player == null)
            {
                string reason = loaded ? "the map has no human to play (add a Human point and its Human info point)" : game.LastLoadError();
                game.UnloadMission();
                RestoreView();
                return Fail($"Play test failed: {reason}");
            }

            // 게임처럼 창 크기에 맞춘 한 장의 화면으로 그린다 (개발용 인자 --window 와 같다). 창의 크기와 모드는 그대로 둔다.
            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            root.ContentScaleSize = root.Size;
            root.Title = "GodotXOPS Editor - play test (Esc: back to the editor)";

            m_playing = true;
            game.HoldSceneAndChange(this, k_playScene);
            return true;
        }

        /// <summary>
        /// 플레이 테스트에서 돌아왔을 때 화면을 에디터의 것으로 되돌린다.
        /// </summary>
        private void ResumeFromPlay()
        {
            m_playing = false;
            m_navigating = false;
            m_pressing = false;
            SetupWindow();
            RestoreView();
            m_view.Camera.MakeCurrent();
            SetMessage("Back from the play test");
        }

        /// <summary>
        /// 3D 화면을 문서의 내용으로 다시 만든다: 블록, 하늘, 안개 끄기. 게임이 맵을 로드했다가 내린 뒤에 부른다.
        /// </summary>
        private void RestoreView()
        {
            MapLoader.LoadSkyData(m_document.SkyIndex);
            RebuildBlocks();
        }

        /// <summary>
        /// 지금 내용을 플레이 테스트용 파일 한 벌(BD2, 텍스처 목록, PD2, .msg, MIF2)로 쓴다. 문서의 경로와 "바뀜" 표시는 건드리지 않는다.
        /// </summary>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>쓴 MIF2 의 경로 (exe 폴더 기준). 실패하면 null.</returns>
        private string WritePlayFiles(out string error)
        {
            string prefix = $"{k_playFolder}/{k_playName}";
            string blockPath = prefix + BD2File.Extension;
            string texturePath = prefix + k_textureListSuffix;
            string pointPath = prefix + PD2File.Extension;
            string missionPath = prefix + MIF2File.Extension;

            ExtendedMissionData mission = m_document.Mission != null
                ? JsonSerializer.Deserialize<ExtendedMissionData>(JsonSerializer.Serialize(m_document.Mission, JsonData.Options), JsonData.Options)
                : new ExtendedMissionData { name = "Play test", fullname = "Play test", skyIndex = m_document.SkyIndex };
            mission.blockPath = blockPath;
            mission.pointPath = pointPath;

            // BD2 는 자기 텍스처 목록의 경로를 담는다. 임시 파일에만 임시 경로를 적고 문서의 값은 되돌린다.
            string keptTextureList = m_document.Blocks.textureListPath;
            try
            {
                string folder = GamePath.Resolve(k_playFolder);
                Directory.CreateDirectory(folder);
                // 개발 환경에서는 이 폴더가 프로젝트 안이다. Godot 이 임포트하지 않게 한다.
                string ignore = Path.Combine(folder, k_ignoreFile);
                if (!File.Exists(ignore)) File.WriteAllText(ignore, string.Empty);

                m_document.Blocks.textureListPath = texturePath;
                if (!m_document.Blocks.Write(GamePath.Resolve(blockPath), out error)) return null;
                File.WriteAllText(GamePath.Resolve(texturePath), JsonData.ToJson(m_document.Textures));
                if (!m_document.Points.Write(GamePath.Resolve(pointPath), out error)) return null;

                string messagePath = Path.ChangeExtension(GamePath.Resolve(pointPath), k_messageExtension);
                if (m_document.Messages.Count > 0) File.WriteAllLines(messagePath, m_document.Messages);
                else File.Delete(messagePath);

                if (!MIF2File.Write(GamePath.Resolve(missionPath), mission, out error)) return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return null;
            }
            finally
            {
                m_document.Blocks.textureListPath = keptTextureList;
            }

            error = null;
            return missionPath;
        }

        /// <summary>
        /// 원본 블록 데이터(BD1)를 확장 형식으로 바꿔 연다. 파일로 쓰지 않는다: 이름이 없는 새 블록 데이터가 되고, 저장할 때 이름을 정한다.
        /// </summary>
        /// <param name="relativePath">BD1 경로 (exe 폴더 기준).</param>
        /// <returns>가져왔으면 true.</returns>
        private bool ImportBlock(string relativePath)
        {
            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !MapLoader.ConvertBD1(fullPath, string.Empty, out BD2File file, out BlockTextureListData textures))
            {
                return Fail($"Block file import failed: {relativePath}");
            }

            m_document.Blocks = file;
            m_document.Textures = textures;
            m_document.BlockPath = string.Empty;
            ShowBlocks();
            m_blockDirty = true;
            SetMessage($"Imported {relativePath} ({file.blocks.Count} blocks). Save blocks as... to name the new file");
            return true;
        }

        /// <summary>
        /// 원본 포인트 데이터(PD1)를 확장 형식으로 바꿔 연다. 같은 이름의 .msg 가 있으면 메시지도 가져온다. 파일로 쓰지 않는다.
        /// </summary>
        /// <param name="relativePath">PD1 경로 (exe 폴더 기준).</param>
        /// <returns>가져왔으면 true.</returns>
        private bool ImportPoints(string relativePath)
        {
            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !MapLoader.ConvertPD1(fullPath, out PD2File file))
            {
                return Fail($"Point file import failed: {relativePath}");
            }

            m_document.Points = file;
            m_document.PointPath = string.Empty;
            m_document.Messages.Clear();
            string messagePath = Path.ChangeExtension(fullPath, k_messageExtension);
            if (File.Exists(messagePath)) m_document.Messages.AddRange(EncodingHelper.ReadAllLines(messagePath));
            ShowPoints();
            m_dirty = true;
            SetMessage($"Imported {relativePath} ({file.points.Count} points). Save points as... to name the new file");
            return true;
        }

        /// <summary>
        /// 고른 원본 미션(미션 파일이나 공식 미션)을 확장 형식 한 벌로 바꿔 쓰고 그것을 연다.
        /// 미션은 블록·포인트 말고도 미션 파일과 추가 사물의 데이터가 함께 있어야 하므로, 블록이나 포인트 하나를 가져올 때와 달리 바로 파일로 쓴다.
        /// 같은 이름의 파일이 있으면 덮어쓴다.
        /// </summary>
        /// <param name="targetPath">만들 MIF2 의 경로 (exe 폴더 기준). 나머지 파일은 그 옆에 같은 이름으로 쓰인다.</param>
        /// <returns>가져왔으면 true.</returns>
        private bool ImportMission(string targetPath)
        {
            bool read = m_importSource != null
                ? MapLoader.LoadMissionFile(GamePath.Resolve(m_importSource) ?? string.Empty)
                : MapLoader.LoadMissionData(m_importOfficial, false, 0);
            string source = m_importSource ?? $"official mission {m_importOfficial}";
            if (!read) return Fail($"Mission import failed: {source} could not be read");
            if (MapLoader.Instance.ExtendedMission)
            {
                MapLoader.UnloadMissionData();
                return Fail($"Mission import failed: {source} is already in the extended format");
            }

            string folder = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) folder = ".";
            string converted = MapLoader.ConvertMissionToExtended(folder, Path.GetFileNameWithoutExtension(targetPath), out string error);
            MapLoader.UnloadMissionData();
            if (converted == null) return Fail($"Mission import failed: {error}");

            bool opened = OpenMission(converted);
            if (opened) SetMessage($"Imported {source} as {converted}");
            return opened;
        }

        /// <summary>
        /// 가져올 원본 미션을 정하고, 변환한 파일을 쓸 자리를 묻는다.
        /// </summary>
        /// <param name="missionPath">미션 파일(.mif)의 경로 (exe 폴더 기준). 공식 미션이면 null.</param>
        /// <param name="official">공식 미션의 번호 (missionPath 가 null 일 때).</param>
        /// <param name="suggestedName">저장 창에 미리 넣을 파일 이름 (확장자 없이).</param>
        private void AskImportTarget(string missionPath, int official, string suggestedName)
        {
            m_importSource = missionPath;
            m_importOfficial = official;
            // 파일 창이 고른 파일을 알리는 도중에 같은 창을 다시 띄우지 않게 한 박자 미룬다.
            Callable.From(() =>
            {
                ShowFileDialog(k_menuImportTarget, "Save the converted mission as", "*" + MIF2File.Extension, true);
                m_fileDialog.CurrentFile = suggestedName + MIF2File.Extension;
            }).CallDeferred();
        }

        /// <summary>
        /// 공식 미션을 고르는 창을 만든다.
        /// </summary>
        /// <param name="parent">창을 넣을 노드.</param>
        private void BuildOfficialDialog(Node parent)
        {
            m_officialDialog = new AcceptDialog { Title = "Import official mission", OkButtonText = "Import", Size = new Vector2I(420, 480) };
            m_officialList = new ItemList();
            m_officialDialog.AddChild(m_officialList);
            m_officialDialog.Confirmed += ImportSelectedOfficial;
            m_officialList.ItemActivated += _ =>
            {
                m_officialDialog.Hide();
                ImportSelectedOfficial();
            };
            parent.AddChild(m_officialDialog);
        }

        /// <summary>
        /// 공식 미션을 고르는 창을 띄운다.
        /// </summary>
        private void ShowOfficialDialog()
        {
            m_officialList.Clear();
            var missions = DataManager.Instance.MissionData.officialMissions;
            for (int i = 0; i < missions.Count; i++)
            {
                m_officialList.AddItem($"{i}  {missions[i].name}");
            }
            m_officialDialog.PopupCentered();
        }

        /// <summary>
        /// 창에서 고른 공식 미션을 가져오기 시작한다 (쓸 자리를 묻는다).
        /// </summary>
        private void ImportSelectedOfficial()
        {
            int[] selected = m_officialList.GetSelectedItems();
            if (selected.Length == 0) return;

            AskImportTarget(null, selected[0], $"mission{selected[0]:00}");
        }
    }
}
