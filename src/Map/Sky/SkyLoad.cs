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
        /// 스카이 메시와 텍스처를 로드해 스카이 노드를 생성한다. 이전 스카이는 먼저 제거한다.
        /// 스카이는 셰이더가 카메라 위치에 붙여 그리므로 노드 위치와 무관하게 항상 배경으로 보인다.
        /// 어두운 화면 미션이면 스카이와 모델의 밝기도 여기서 낮춘다 (미션 정보를 먼저 로드해야 한다).
        /// </summary>
        /// <param name="textureIndex">하늘 번호. 10000 에서 19999 는 맵팩의 하늘이다. 텍스처가 없는 번호(0, 범위 밖)면 텍스처 없이 검정.</param>
        public static void LoadSkyData(int textureIndex)
        {
            UnloadSkyData();

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

            ApplySkyFog(textureIndex);
            RenderingServer.GlobalShaderParameterSet(s_modelBrightnessGlobal, Instance.m_darkScreen ? k_darkModelBrightness : 1f);
        }

        /// <summary>
        /// 스카이 노드를 모두 제거하고 안개를 끈다.
        /// </summary>
        public static void UnloadSkyData()
        {
            Node3D skyRoot = Instance.m_skyRoot;
            foreach (Node child in skyRoot.GetChildren())
            {
                skyRoot.RemoveChild(child);
                child.Free();
            }

            ClearFog();
            RenderingServer.GlobalShaderParameterSet(s_modelBrightnessGlobal, 1f);
        }

        /// <summary>
        /// 원본 SetFog(true, skynumber) 대응. 선형 안개를 SkyData 의 fogStart ~ fogEnd 범위, 하늘 번호별 색으로 적용한다.
        /// SkyData.fog 가 false 면 안개를 끈다. 안개는 alpha_clip_blend 셰이더(블록/소물/사람/무기)만 받는다.
        /// </summary>
        /// <param name="skyIndex">하늘 번호. 10000 에서 19999 는 맵팩의 하늘이다. 색이 없는 번호면 검정(원본 default).</param>
        public static void ApplySkyFog(int skyIndex)
        {
            SkyData skyData = DataManager.Instance.SkyData;
            if (!skyData.fog)
            {
                ClearFog();
                return;
            }

            TryGetSky(skyIndex, out _, out Color color);

            // 셰이더가 sRGB 값 그대로 섞으므로 색을 선형으로 바꾸지 않고 넘긴다. w = 1 은 안개 사용.
            RenderingServer.GlobalShaderParameterSet(s_fogColorGlobal, new Vector4(color.R, color.G, color.B, 1f));
            RenderingServer.GlobalShaderParameterSet(s_fogRangeGlobal, new Vector2(skyData.fogStart, skyData.fogEnd));
        }

        /// <summary>
        /// 원본 SetFog(false, …) 대응. 안개를 끈다.
        /// </summary>
        public static void ClearFog()
        {
            RenderingServer.GlobalShaderParameterSet(s_fogColorGlobal, Vector4.Zero);
        }
    }
}
