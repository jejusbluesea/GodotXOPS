using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        private static readonly StringName s_fogColorGlobal = "xops_fog_color";
        private static readonly StringName s_fogRangeGlobal = "xops_fog_range";
        private static readonly StringName s_modelBrightnessGlobal = "xops_model_brightness";

        // 어두운 화면 미션에서 스카이와 모델(사람·무기·소물·탄환)에 곱하는 밝기 (원본 D3DGraphics::RenderModel 의 darkflag).
        private const float k_darkModelBrightness = 0.8f;

        // 지금 미션이 속한 맵팩의 하늘 (Manifest 의 skyDataPath). 하늘 번호 10000 에서 19999 가 이 목록의 (번호 - 10000) 번째다. 없으면 null.
        private SkyData m_packSky;

        // 지금 떠 있는 하늘의 번호. 하늘이 없으면 -1. 안개 설정을 바꾼 뒤 다시 적용할 때 쓴다 (RefreshFog).
        private int m_loadedSkyIndex = -1;

        // 지금 떠 있는 하늘의 번호. 하늘이 없으면 -1.
        public static int LoadedSkyIndex => Loaded ? Instance.m_loadedSkyIndex : -1;
        // 안개를 그리고 있는지 (셰이더의 전역 값은 헤드리스에서 읽을 수 없어서 따로 든다).
        private bool m_fogActive;

        // 지금 안개를 그리고 있으면 true.
        public static bool FogActive => Loaded && Instance.m_fogActive;

        /// <summary>
        /// 맵팩의 하늘 파일을 읽어 둔다. sky_data.json 과 같은 키를 쓰는 JSON 이고, 쓰는 것은 skyTexturePath 와 skyColor 두 목록뿐이다
        /// (하늘 모델, 안개 거리, 카메라 거리는 게임 전체의 값이라 맵팩이 바꾸지 못한다). 맵팩 데이터와 함께 미션 정보를 읽을 때 붙고 미션 정보를 내릴 때 떨어진다.
        /// 에디터는 미션 정보를 읽지 않으므로 하늘을 띄울 때 직접 부른다.
        /// </summary>
        /// <param name="relativePath">exe 폴더 기준 경로. 비어 있으면 맵팩의 하늘을 뗀다.</param>
        public static void LoadPackSky(string relativePath)
        {
            Instance.m_packSky = LoadAddonFile<SkyData>(relativePath);
        }

        /// <summary>
        /// 하늘 번호가 가리키는 텍스처와 안개 색을 찾는다. 10000 미만은 sky_data.json, 10000 에서 19999 는 맵팩의 하늘이다. 미션(20000 부터)은 하늘을 들고 오지 않는다.
        /// </summary>
        /// <param name="skyIndex">하늘 번호.</param>
        /// <param name="texturePath">텍스처 경로 (exe 폴더 기준). 그 번호에 텍스처가 없으면 빈 문자열.</param>
        /// <param name="fogColor">안개 색. 그 번호에 색이 없으면 검정 (원본의 default).</param>
        /// <returns>그 번호의 텍스처가 목록에 있으면 true.</returns>
        public static bool TryGetSky(int skyIndex, out string texturePath, out Color fogColor)
        {
            texturePath = string.Empty;
            fogColor = Colors.Black;

            SkyData source = DataManager.Instance.SkyData;
            int index = skyIndex;
            if (skyIndex >= DataList<int>.MissionBase) return false;
            if (skyIndex >= DataList<int>.PackBase)
            {
                source = Instance.m_packSky;
                index = skyIndex - DataList<int>.PackBase;
            }
            if (source == null || index < 0) return false;

            if (index < source.skyColor.Count) fogColor = source.skyColor[index].ToColor();
            if (index >= source.skyTexturePath.Count) return false;
            texturePath = source.skyTexturePath[index] ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 하늘 번호에 겹쳐 그리는 풍경과 해·달을 찾는다. 10000 미만은 sky_data.json, 10000 에서 19999 는 맵팩의 하늘이다.
        /// </summary>
        /// <param name="skyIndex">하늘 번호.</param>
        /// <param name="layer">찾은 것.</param>
        /// <returns>그 번호에 겹쳐 그리는 것이 적혀 있으면 true.</returns>
        public static bool TryGetSkyLayer(int skyIndex, out SkyLayerData layer)
        {
            layer = null;
            SkyData source = DataManager.Instance.SkyData;
            int index = skyIndex;
            if (skyIndex >= DataList<int>.MissionBase) return false;
            if (skyIndex >= DataList<int>.PackBase)
            {
                source = Instance.m_packSky;
                index = skyIndex - DataList<int>.PackBase;
            }
            if (source == null || index < 0 || index >= source.skyLayerData.Count) return false;

            layer = source.skyLayerData[index];
            return layer != null;
        }

        /// <summary>
        /// 하늘 위에 풍경과 해·달을 더한다. 그 하늘 번호에 적힌 것이 없거나 텍스처를 읽지 못하면 그것만 빼고 그린다.
        /// </summary>
        /// <param name="skyIndex">하늘 번호.</param>
        /// <param name="skyMesh">하늘 모델 (풍경도 같은 모델에 입힌다).</param>
        private static void AddSkyLayers(int skyIndex, ArrayMesh skyMesh)
        {
            if (!TryGetSkyLayer(skyIndex, out SkyLayerData layer)) return;

            Texture2D sun = LoadSkyTexture(layer.sunTexturePath);
            if (sun != null)
            {
                Instance.m_skyRoot.AddChild(new MeshInstance3D
                {
                    Name = "Sun",
                    Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
                    MaterialOverride = MaterialManager.Instance.CreateSunMaterial(sun, Coord.FromUnity(layer.sunDirection), layer.sunSize),
                    ExtraCullMargin = 16384f,
                });
            }

            Texture2D billboard = LoadSkyTexture(layer.billboardTexturePath);
            if (billboard != null)
            {
                Instance.m_skyRoot.AddChild(new MeshInstance3D
                {
                    Name = "SkyLayer",
                    Mesh = skyMesh,
                    MaterialOverride = MaterialManager.Instance.CreateSkyLayerMaterial(billboard),
                    ExtraCullMargin = 16384f,
                });
            }
        }

        /// <summary>
        /// 하늘에 쓰는 텍스처 하나를 읽는다.
        /// </summary>
        /// <param name="relativePath">exe 폴더 기준 경로. 비었으면 없다.</param>
        /// <returns>텍스처. 비었거나 읽지 못했으면 null (읽지 못했으면 경고).</returns>
        private static Texture2D LoadSkyTexture(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            string fullPath = GamePath.Resolve(relativePath);
            Texture2D texture = fullPath != null ? ImageLoader.LoadTexture(fullPath) : null;
            if (texture == null) Debugger.LogWarning($"Sky texture open failed: {relativePath}", nameof(MapLoader));
            return texture;
        }

        /// <summary>
        /// 스카이 메시와 텍스처를 로드해 스카이 노드를 생성한다. 이전 스카이는 먼저 제거한다.
        /// 스카이는 셰이더가 카메라 위치에 붙여 그리므로 노드 위치와 무관하게 항상 배경으로 보인다.
        /// 어두운 화면 미션이면 스카이와 모델의 밝기도 여기서 낮춘다 (미션 정보를 먼저 로드해야 한다).
        /// </summary>
        /// <param name="textureIndex">하늘 번호. 10000 에서 19999 는 맵팩의 하늘이다. 텍스처가 없는 번호(0, 범위 밖)면 텍스처 없이 검정.</param>
        public static void LoadSkyData(int textureIndex)
        {
            RemoveSkyNodes();

            SkyData skyData = DataManager.Instance.SkyData;
            if (string.IsNullOrEmpty(skyData.skyMeshPath))
            {
                Debugger.LogError("SkyData mesh path is empty.", nameof(MapLoader));
                return;
            }

            string fullMeshPath = GamePath.Resolve(skyData.skyMeshPath);
            ArrayMesh skyMesh = fullMeshPath != null ? ModelLoader.LoadMesh(fullMeshPath) : null;
            if (skyMesh == null)
            {
                Debugger.LogError($"Failed to load sky mesh: {skyData.skyMeshPath}", nameof(MapLoader));
                return;
            }

            ImageTexture texture = null;
            if (TryGetSky(textureIndex, out string texturePath, out _) && texturePath.Length > 0)
            {
                string fullTexturePath = GamePath.Resolve(texturePath);
                if (fullTexturePath != null)
                {
                    texture = ImageLoader.LoadTexture(fullTexturePath);
                }
            }
            else if (DataList<int>.IsAddon(textureIndex))
            {
                Debugger.LogWarning($"Sky {textureIndex} is not in the map pack's sky data (skyDataPath of the manifest).", nameof(MapLoader));
            }

            var sky = new MeshInstance3D
            {
                Name = "Skybox",
                Mesh = skyMesh,
                MaterialOverride = MaterialManager.Instance.CreateSkyMaterial(texture),
                // 셰이더가 정점을 카메라로 옮기므로 노드 위치 기준 컬링이 맞지 않는다. 항상 그려지게 컬링 범위를 최대로 넓힌다.
                ExtraCullMargin = 16384f,
            };
            Instance.m_skyRoot.AddChild(sky);
            Instance.m_loadedSkyIndex = textureIndex;
            AddSkyLayers(textureIndex, skyMesh);

            ApplySkyFog(textureIndex);
            RenderingServer.GlobalShaderParameterSet(s_modelBrightnessGlobal, Instance.m_darkScreen ? k_darkModelBrightness : 1f);
        }

        /// <summary>
        /// 스카이 노드를 모두 제거하고 안개와 날씨를 끈다 (맵을 내릴 때).
        /// </summary>
        public static void UnloadSkyData()
        {
            RemoveSkyNodes();
            ClearWeather();
        }

        /// <summary>
        /// 스카이 노드를 모두 제거하고 안개를 끈다. 날씨는 남긴다 (하늘을 바꿀 때 날씨가 지워지지 않게).
        /// </summary>
        private static void RemoveSkyNodes()
        {
            Node3D skyRoot = Instance.m_skyRoot;
            foreach (Node child in skyRoot.GetChildren())
            {
                skyRoot.RemoveChild(child);
                child.Free();
            }
            Instance.m_loadedSkyIndex = -1;

            ClearFog();
            RenderingServer.GlobalShaderParameterSet(s_modelBrightnessGlobal, 1f);
        }

        /// <summary>
        /// 원본 SetFog(true, skynumber) 대응. 선형 안개를 SkyData 의 fogStart ~ fogEnd 범위, 하늘 번호별 색으로 적용한다.
        /// SkyData.fog 가 false 이거나(날씨가 안개를 덮어쓰지 않을 때) 설정의 enableFog 가 꺼져 있으면 안개를 끈다. 날씨가 안개를 덮어쓰면 그 색과 거리를 쓴다. 안개는 alpha_clip_blend 셰이더(블록/소물/사람/무기)와 이펙트 셰이더만 받는다.
        /// </summary>
        /// <param name="skyIndex">하늘 번호. 10000 에서 19999 는 맵팩의 하늘이다. 색이 없는 번호면 검정(원본 default).</param>
        public static void ApplySkyFog(int skyIndex)
        {
            SkyData skyData = DataManager.Instance.SkyData;
            WeatherData weather = CurrentWeather;
            bool weatherFog = weather != null && weather.fogOverride;
            if ((!skyData.fog && !weatherFog) || !ConfigManager.Instance.GetBool(ConfigManager.SectionGraphic, ConfigManager.KeyEnableFog, true))
            {
                ClearFog();
                return;
            }

            Color color;
            float start = skyData.fogStart;
            float end = skyData.fogEnd;
            if (weatherFog)
            {
                // 날씨가 안개를 덮어쓰면 하늘의 안개 대신 그것을 쓴다.
                color = weather.fogColor.ToColor();
                start = weather.fogStart;
                end = weather.fogEnd;
            }
            else
            {
                TryGetSky(skyIndex, out _, out color);
            }

            // 셰이더가 sRGB 값 그대로 섞으므로 색을 선형으로 바꾸지 않고 넘긴다. w = 1 은 안개 사용.
            RenderingServer.GlobalShaderParameterSet(s_fogColorGlobal, new Vector4(color.R, color.G, color.B, 1f));
            RenderingServer.GlobalShaderParameterSet(s_fogRangeGlobal, new Vector2(start, end));
            Instance.m_fogActive = true;
        }

        /// <summary>
        /// 설정의 안개 켜기를 지금 떠 있는 하늘에 다시 적용한다 (OPTION 의 SAVE). 하늘이 없으면 아무것도 하지 않는다.
        /// </summary>
        public static void RefreshFog()
        {
            if (!Loaded || Instance.m_loadedSkyIndex < 0) return;

            ApplySkyFog(Instance.m_loadedSkyIndex);
        }

        /// <summary>
        /// 원본 SetFog(false, …) 대응. 안개를 끈다.
        /// </summary>
        public static void ClearFog()
        {
            RenderingServer.GlobalShaderParameterSet(s_fogColorGlobal, Vector4.Zero);
            if (Loaded) Instance.m_fogActive = false;
        }
    }
}
