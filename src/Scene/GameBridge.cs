using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 화면(GDScript)이 게임을 다루는 창구. Autoload 이름은 Game 이다.
    /// 화면 스크립트는 게임플레이 노드를 직접 만지지 않고 이 클래스와 EventManager·ConfigManager·InputManager 만 부른다.
    /// GDScript 가 볼 수 있도록 public 멤버는 Godot Variant 호환 타입만 쓴다. 좌표와 각도는 UnityXOPS 공간으로 주고받는다 (화면 수치가 그 공간 기준이다).
    /// 관심사별 partial: 이 파일(화면 전환, 맵 로드, 카메라, 밝기) / GameBridgeData(미션 목록·브리핑·통계) / GameBridgePlayer(플레이어 값, 무기 표시) / GameBridgeConsole(디버그 콘솔).
    /// </summary>
    public partial class GameBridge : Singleton<GameBridge>
    {
        private const string k_scenePathFormat = "res://scenes/{0}.tscn";
        private const string k_colorAdjustShaderPath = "res://shaders/screen_color_adjust.gdshader";
        // 밝기·감마 사각형은 모든 화면 요소보다 위에 그린다.
        private const int k_colorAdjustLayer = 100;

        // 마지막으로 시도한 미션 로드가 실패한 이유. 성공했으면 빈 문자열.
        private string m_lastLoadError = string.Empty;
        // 마지막에 로드한 배경 맵 (오프닝, 메뉴 데모). 디버그 콘솔의 restart 가 같은 맵을 다시 올린다.
        private DemoData m_lastBackground;

        // 벽 블라인드 결과의 비트.
        public const int BlindTop = 1;
        public const int BlindBottom = 2;
        public const int BlindLeft = 4;
        public const int BlindRight = 8;

        private Camera3D m_sceneCamera;
        private ColorRect m_colorAdjustRect;
        private ShaderMaterial m_colorAdjustMaterial;

        public override void _Ready()
        {
            // 오프닝과 메뉴 배경을 비추는 카메라. 메인게임에서는 PlayerController 의 카메라가 대신한다.
            m_sceneCamera = new Camera3D { Name = "SceneCamera", TopLevel = true };
            AddChild(m_sceneCamera);
            MapLoader.ApplyCameraSettings(m_sceneCamera);

            m_colliderView = new ColliderView { Name = "ColliderView" };
            AddChild(m_colliderView);

            if (DisplayServer.GetName() != "headless")
            {
                var layer = new CanvasLayer { Name = "ColorAdjust", Layer = k_colorAdjustLayer };
                AddChild(layer);

                m_colorAdjustMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(k_colorAdjustShaderPath) };
                m_colorAdjustRect = new ColorRect
                {
                    Name = "Rect",
                    Material = m_colorAdjustMaterial,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false,
                };
                m_colorAdjustRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                layer.AddChild(m_colorAdjustRect);
            }
        }

        public override void _Process(double delta)
        {
            if (m_colorAdjustRect == null) return;

            // 설정의 밝기·감마를 매 프레임 읽어 바로 반영한다. 둘 다 1 이면 사각형을 아예 그리지 않는다.
            float brightness = ConfigManager.Instance.Brightness;
            float gamma = ConfigManager.Instance.Gamma;
            bool neutral = Mathf.IsEqualApprox(brightness, 1f) && Mathf.IsEqualApprox(gamma, 1f);
            m_colorAdjustRect.Visible = !neutral;
            if (!neutral)
            {
                m_colorAdjustMaterial.SetShaderParameter("brightness", brightness);
                m_colorAdjustMaterial.SetShaderParameter("gamma", Mathf.Max(0.001f, gamma));
            }
        }

        /// <summary>
        /// 화면을 바꾼다. 실제 전환은 이번 프레임이 끝난 뒤에 일어난다.
        /// </summary>
        /// <param name="sceneName">scenes 폴더의 씬 이름 (확장자 없이). 예: "mainmenu".</param>
        public void ChangeScene(string sceneName)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, string.Format(k_scenePathFormat, sceneName));
        }

        /// <summary>
        /// 게임을 끝낸다.
        /// </summary>
        public void Quit()
        {
            GetTree().Quit();
        }

        /// <summary>
        /// 개발용 — 설정 파일의 화면 설정 대신 지정한 크기의 창으로 띄운다. 렌더 해상도도 그 크기가 된다.
        /// </summary>
        /// <param name="width">창 너비.</param>
        /// <param name="height">창 높이.</param>
        public void SetDevWindow(int width, int height)
        {
            var size = new Vector2I(width, height);
            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            root.ContentScaleSize = size;
            root.Mode = Window.ModeEnum.Windowed;
            root.Size = size;
            root.MoveToCenter();
        }

        /// <summary>
        /// 오프닝 배경 맵을 로드하고 시뮬레이션을 켠다. 플레이어까지 AI 가 움직이고 이벤트는 돌지 않는다 (원본 데모).
        /// </summary>
        /// <returns>로드에 성공했으면 true.</returns>
        public bool LoadOpening()
        {
            return LoadBackgroundMap(DataManager.Instance.MissionData.openingData);
        }

        /// <summary>
        /// 메뉴 배경 맵을 데모 목록에서 하나 골라 로드하고 시뮬레이션을 켠다.
        /// </summary>
        /// <returns>로드에 성공했으면 true. 데모가 하나도 없으면 false.</returns>
        public bool LoadDemo()
        {
            var demos = DataManager.Instance.MissionData.demoData;
            if (demos.Count == 0) return false;

            return LoadBackgroundMap(demos[GameRandom.Visual.Range(0, demos.Count)]);
        }

        /// <summary>
        /// 미션을 로드한다 (미션 정보 → 블록 → 하늘 → 사람·무기·소물). 시뮬레이션은 멈춘 채로 둔다. 브리핑 화면이 그 위에 뜬다.
        /// </summary>
        /// <param name="index">미션 목록의 인덱스.</param>
        /// <param name="addon">true 면 어드온 미션.</param>
        /// <param name="page">어드온 페이지 (공식 미션이면 무시).</param>
        /// <returns>로드에 성공했으면 true.</returns>
        public bool LoadMission(int index, bool addon, int page)
        {
            UnloadMission();
            int errors = Debugger.ErrorCount;
            bool loaded = MapLoader.LoadMissionData(index, addon, page) && LoadCurrentMissionMap();
            RecordLoadResult(loaded, errors);
            return loaded;
        }

        /// <summary>
        /// 미션 목록에 없는 미션 파일(.mif) 하나를 로드한다 (디버그 콘솔의 loadmission). 시뮬레이션은 멈춘 채로 둔다.
        /// </summary>
        /// <param name="mifPath">.mif 파일 전체 경로.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        public bool LoadMissionFile(string mifPath)
        {
            UnloadMission();
            int errors = Debugger.ErrorCount;
            bool loaded = MapLoader.LoadMissionFile(mifPath) && LoadCurrentMissionMap();
            RecordLoadResult(loaded, errors);
            return loaded;
        }

        /// <summary>
        /// 미션 파일 없이 블록 파일과 포인트 파일을 직접 로드한다 (디버그 콘솔의 loadmap). 시뮬레이션은 멈춘 채로 둔다.
        /// </summary>
        /// <param name="blockPath">블록 데이터 파일 전체 경로.</param>
        /// <param name="pointPath">포인트 데이터 파일 전체 경로.</param>
        /// <param name="skyIndex">하늘 번호.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        public bool LoadMapFiles(string blockPath, string pointPath, int skyIndex)
        {
            UnloadMission();
            int errors = Debugger.ErrorCount;
            MapLoader.SetDirectMission(blockPath, pointPath, skyIndex);
            bool loaded = LoadCurrentMissionMap();
            RecordLoadResult(loaded, errors);
            return loaded;
        }

        /// <summary>
        /// 마지막으로 시도한 미션 로드가 실패한 이유 (영어 한 줄). 성공했으면 빈 문자열이다. 메뉴가 화면에 보여 준다.
        /// </summary>
        /// <returns>실패 이유.</returns>
        public string LastLoadError()
        {
            return m_lastLoadError;
        }

        /// <summary>
        /// 미션 로드의 결과를 남긴다. 실패했으면 로드하는 동안 처음 남은 에러 로그를 이유로 삼는다 (뒤의 에러는 대개 그 여파다).
        /// </summary>
        /// <param name="loaded">로드에 성공했는지.</param>
        /// <param name="errorsBefore">로드를 시작하기 전의 Debugger.ErrorCount.</param>
        private void RecordLoadResult(bool loaded, int errorsBefore)
        {
            if (loaded)
            {
                m_lastLoadError = string.Empty;
                return;
            }

            string reason = Debugger.FirstErrorSince(errorsBefore).Split('\n')[0];
            m_lastLoadError = reason.Length > 0 ? reason : "Mission load failed";
        }

        /// <summary>
        /// 오프닝이나 메뉴의 배경 맵을 처음부터 다시 로드한다 (디버그 콘솔의 restart). 마지막에 로드한 배경 맵과 같은 맵이다.
        /// </summary>
        /// <returns>다시 로드하는 데 성공했으면 true. 배경 맵을 로드한 적이 없으면 false.</returns>
        public bool ReloadBackground()
        {
            return m_lastBackground != null && LoadBackgroundMap(m_lastBackground);
        }

        /// <summary>
        /// 로드된 미션을 시작한다. 시뮬레이션과 이벤트를 켜고 플레이어를 사람이 조작하게 한다. 메인게임 화면이 들어올 때 부른다.
        /// </summary>
        public void BeginMission()
        {
            ReleaseSceneCamera();
            AIController.Enabled = true;
            AIController.DrivePlayer = false;
            SimClock.TickEnabled = true;
            EventManager.Instance.BeginMission();
            m_console.Reset();
        }

        /// <summary>
        /// 같은 미션을 처음부터 다시 시작한다. 맵을 다시 로드하고 이벤트를 처음으로 되돌린다.
        /// </summary>
        /// <returns>다시 로드하는 데 성공했으면 true.</returns>
        public bool RestartMission()
        {
            if (!ReloadMission()) return false;

            BeginMission();
            return true;
        }

        /// <summary>
        /// 같은 미션의 맵을 다시 로드한다. 시작은 하지 않는다. 결과 화면에서 다시 시작할 때 쓰고, 메인게임 화면이 들어오면서 BeginMission 을 부른다.
        /// </summary>
        /// <returns>다시 로드하는 데 성공했으면 true.</returns>
        public bool ReloadMission()
        {
            UnloadMap();
            return LoadCurrentMissionMap();
        }

        /// <summary>
        /// 맵(블록, 하늘, 사람·무기·소물)을 내리고 시뮬레이션을 멈춘다. 미션 정보와 통계, 미션 결과는 남는다 (결과 화면이 읽는다).
        /// </summary>
        public void UnloadMap()
        {
            SimClock.TickEnabled = false;
            MapLoader.UnloadPointData();
            MapLoader.UnloadBlockData();
            MapLoader.UnloadSkyData();
        }

        /// <summary>
        /// 맵과 미션 정보를 모두 내린다.
        /// </summary>
        public void UnloadMission()
        {
            UnloadMap();
            MapLoader.UnloadMissionData();
        }

        /// <summary>
        /// 장면 카메라(오프닝, 메뉴 배경)를 놓고 현재 카메라로 삼는다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="euler">회전 (UnityXOPS 오일러 각, 도).</param>
        /// <param name="fov">세로 시야각 (도).</param>
        public void SetSceneCamera(Vector3 position, Vector3 euler, float fov)
        {
            m_sceneCamera.Position = Coord.FromUnity(position);
            m_sceneCamera.Rotation = Coord.FromUnityEuler(euler);
            m_sceneCamera.Fov = fov;
            if (!m_sceneCamera.Current) m_sceneCamera.MakeCurrent();
        }

        /// <summary>
        /// 장면 카메라를 현재 카메라에서 내린다.
        /// </summary>
        public void ReleaseSceneCamera()
        {
            m_sceneCamera.Current = false;
        }

        /// <summary>
        /// 지금 화면을 그리는 카메라가 벽에 묻혔는지 위·아래·왼쪽·오른쪽으로 본다.
        /// 가까운 절단면 사각형의 네 변 가운데 점이 블록 안인지 검사한다. 화면이 뚫려 보이기 시작하는 경계가 그 면이기 때문이다.
        /// 원본(gamemain.cpp:2973-3003)은 시야 안쪽의 점을 봐서 가장자리로 벽이 파고들면 놓친다.
        /// </summary>
        /// <returns>BlindTop / BlindBottom / BlindLeft / BlindRight 비트의 합. 플레이어가 없거나 죽었으면 0.</returns>
        public int GetWallBlind()
        {
            Human player = MapLoader.Player;
            Camera3D camera = GetViewport().GetCamera3D();
            if (camera == null || player == null || !player.Alive) return 0;

            Basis basis = camera.GlobalBasis;
            Vector3 right = basis.X;
            Vector3 up = basis.Y;
            Vector3 forward = -basis.Z;

            Vector2 viewSize = GetViewport().GetVisibleRect().Size;
            float aspect = viewSize.Y > 0f ? viewSize.X / viewSize.Y : 1f;
            float halfHeight = camera.Near * Mathf.Tan(Mathf.DegToRad(camera.Fov * 0.5f));
            float halfWidth = halfHeight * aspect;
            Vector3 center = camera.GlobalPosition + forward * camera.Near;

            int result = 0;
            if (MapLoader.IsInsideBlock(BlockLayer.Human, center + up * halfHeight)) result |= BlindTop;
            if (MapLoader.IsInsideBlock(BlockLayer.Human, center - up * halfHeight)) result |= BlindBottom;
            if (MapLoader.IsInsideBlock(BlockLayer.Human, center - right * halfWidth)) result |= BlindLeft;
            if (MapLoader.IsInsideBlock(BlockLayer.Human, center + right * halfWidth)) result |= BlindRight;
            return result;
        }

        /// <summary>
        /// 배경용 맵(오프닝, 메뉴 데모)을 로드한다.
        /// </summary>
        /// <param name="data">맵 경로와 하늘 번호.</param>
        /// <returns>블록과 포인트 로드에 성공했으면 true.</returns>
        private bool LoadBackgroundMap(DemoData data)
        {
            m_lastBackground = data;
            UnloadMission();
            GameRandom.ReseedEntropy();

            bool blocks = MapLoader.LoadBlockData(GamePath.Resolve(data.bd1Path));
            MapLoader.LoadSkyData(data.skyIndex);
            bool points = MapLoader.LoadPointData(GamePath.Resolve(data.pd1Path));

            AIController.Enabled = true;
            AIController.DrivePlayer = true;
            SimClock.TickEnabled = blocks && points;
            return blocks && points;
        }

        /// <summary>
        /// MapLoader 에 들어 있는 미션 정보대로 맵을 로드한다. 난수는 로드 전에 새로 시드한다 (랜덤 무기 스폰이 로드 중에 난수를 쓴다).
        /// </summary>
        /// <returns>블록과 포인트 로드에 성공했으면 true.</returns>
        private static bool LoadCurrentMissionMap()
        {
            MapLoader loader = MapLoader.Instance;
            GameRandom.ReseedEntropy();

            bool blocks = MapLoader.LoadBlockData(loader.MissionBD1Path);
            MapLoader.LoadSkyData(loader.SkyIndex);
            bool points = MapLoader.LoadPointData(loader.MissionPD1Path);
            return blocks && points;
        }
    }
}
