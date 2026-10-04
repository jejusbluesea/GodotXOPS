using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 맵 로드/언로드 싱글톤의 공유 코어. 블록·스카이·미션 각 관심사는 동명 partial 파일
    /// (Block/BlockData, Sky/SkyLoad, Mission/MissionLoad)에서 처리하고, 이 파일은 공용 노드만 보유한다.
    /// Autoload 라 씬이 바뀌어도 로드된 맵이 유지된다.
    /// </summary>
    public partial class MapLoader : Singleton<MapLoader>
    {
        private Node3D m_blockRoot;
        private Node3D m_skyRoot;

        public override void _Ready()
        {
            m_blockRoot = new Node3D { Name = "BlockRoot" };
            AddChild(m_blockRoot);

            m_skyRoot = new Node3D { Name = "SkyRoot" };
            AddChild(m_skyRoot);

            // 스카이 메시가 없을 때의 배경은 검정이다(원본 기본값). 조명은 쓰지 않으므로 환경광도 끈다.
            var environment = new Environment
            {
                BackgroundMode = Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Environment.AmbientSource.Disabled,
            };
            AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = environment });

            ClearFog();
        }

        /// <summary>
        /// 카메라에 Graphic 설정(config.json)의 near/far 클리핑 면과 시야각을 적용한다.
        /// far 는 안개가 완전히 가리는 거리보다 넉넉히 크게 두면 된다(그 너머는 안개가 가려 클리핑이 안 보인다).
        /// </summary>
        /// <param name="camera">설정을 적용할 카메라.</param>
        public static void ApplyCameraSettings(Camera3D camera)
        {
            ConfigManager config = ConfigManager.Instance;
            camera.Near = config.GetFloat(ConfigManager.SectionGraphic, "nearClippingPlane", camera.Near);
            camera.Far = config.GetFloat(ConfigManager.SectionGraphic, "farClippingPlane", camera.Far);
            camera.Fov = config.GetInt(ConfigManager.SectionGraphic, "fov", Mathf.RoundToInt(camera.Fov));
        }
    }
}
