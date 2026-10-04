using System.Collections.Generic;
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

        /// <summary>
        /// 스카이 메시와 텍스처를 로드해 스카이 노드를 생성한다. 이전 스카이는 먼저 제거한다.
        /// 스카이는 셰이더가 카메라 위치에 붙여 그리므로 노드 위치와 무관하게 항상 배경으로 보인다.
        /// 어두운 화면 미션이면 스카이와 모델의 밝기도 여기서 낮춘다 (미션 정보를 먼저 로드해야 한다).
        /// </summary>
        /// <param name="textureIndex">SkyData 텍스처 경로 목록의 인덱스. 0 이거나 범위 밖이면 텍스처 없이 검정.</param>
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
            if (textureIndex > 0 && textureIndex < skyData.skyTexturePath.Count)
            {
                string fullTexturePath = GamePath.Resolve(skyData.skyTexturePath[textureIndex]);
                if (fullTexturePath != null)
                {
                    texture = ImageLoader.LoadTexture(fullTexturePath);
                }
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
        /// <param name="skyIndex">하늘 번호. skyColor 범위를 벗어나면 검정(원본 default).</param>
        public static void ApplySkyFog(int skyIndex)
        {
            SkyData skyData = DataManager.Instance.SkyData;
            if (!skyData.fog)
            {
                ClearFog();
                return;
            }

            List<Color32> colors = skyData.skyColor;
            Color color = (skyIndex >= 0 && skyIndex < colors.Count) ? colors[skyIndex].ToColor() : Colors.Black;

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
