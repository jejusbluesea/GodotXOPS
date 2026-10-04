using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 미션에서 사용되는 셰이더를 보관하고 용도별 머티리얼을 만들어 주는 싱글톤 클래스.
    /// </summary>
    public partial class MaterialManager : Singleton<MaterialManager>
    {
        private const string k_alphaClipBlendPath = "res://shaders/alpha_clip_blend.gdshader";
        private const string k_skyMeshPath = "res://shaders/sky_mesh.gdshader";
        private const string k_effectBlendPath = "res://shaders/effect_blend.gdshader";

        // 원본은 맵 블록을 먼저, 소물/사람/무기를 나중에 그린다. 같은 평면에서 겹칠 때 나중 것이 위에 오도록 우선순위로 순서를 고정한다.
        private const int k_blockRenderPriority = -1;
        private const int k_mainRenderPriority = 0;
        // 이펙트는 깊이를 쓰지 않으므로 블록·사람보다 나중에 그려야 그 위에 겹쳐 보인다.
        private const int k_effectRenderPriority = 1;

        private static readonly StringName s_mainTexture = "main_texture";
        private static readonly StringName s_darkApply = "dark_apply";

        private Shader m_alphaClipBlend;
        private Shader m_skyMesh;
        private Shader m_effectBlend;

        public override void _Ready()
        {
            m_alphaClipBlend = GD.Load<Shader>(k_alphaClipBlendPath);
            m_skyMesh = GD.Load<Shader>(k_skyMeshPath);
            m_effectBlend = GD.Load<Shader>(k_effectBlendPath);
        }

        /// <summary>
        /// 이펙트 빌보드용 머티리얼을 만든다. 투명도는 노드마다 인스턴스 유니폼(effect_alpha)으로 준다.
        /// </summary>
        /// <param name="texture">입힐 텍스처.</param>
        /// <returns>새 머티리얼.</returns>
        public ShaderMaterial CreateEffectMaterial(Texture2D texture)
        {
            return Create(m_effectBlend, texture, k_effectRenderPriority);
        }

        /// <summary>
        /// 소물·사람·무기용 머티리얼을 만든다. 어두운 화면 미션에서 모델 밝기(xops_model_brightness)를 받는다.
        /// </summary>
        /// <param name="texture">입힐 텍스처. null 이면 흰색.</param>
        /// <returns>새 머티리얼.</returns>
        public ShaderMaterial CreateMainMaterial(Texture2D texture)
        {
            ShaderMaterial material = Create(m_alphaClipBlend, texture, k_mainRenderPriority);
            material.SetShaderParameter(s_darkApply, 1f);
            return material;
        }

        /// <summary>
        /// 맵 블록용 머티리얼을 만든다. 소물·사람보다 먼저 그려진다.
        /// </summary>
        /// <param name="texture">입힐 텍스처. null 이면 흰색.</param>
        /// <returns>새 머티리얼.</returns>
        public ShaderMaterial CreateBlockMaterial(Texture2D texture)
        {
            return Create(m_alphaClipBlend, texture, k_blockRenderPriority);
        }

        /// <summary>
        /// 스카이 메시용 머티리얼을 만든다.
        /// </summary>
        /// <param name="texture">입힐 텍스처. null 이면 검정.</param>
        /// <returns>새 머티리얼.</returns>
        public ShaderMaterial CreateSkyMaterial(Texture2D texture)
        {
            // 깊이 테스트를 끈 머티리얼은 반투명 패스에서 그려지므로, 가장 낮은 우선순위로 다른 모든 것보다 먼저 그리게 한다.
            return Create(m_skyMesh, texture, (int)Material.RenderPriorityMin);
        }

        private static ShaderMaterial Create(Shader shader, Texture2D texture, int renderPriority)
        {
            var material = new ShaderMaterial { Shader = shader, RenderPriority = renderPriority };
            if (texture != null)
            {
                material.SetShaderParameter(s_mainTexture, texture);
            }
            return material;
        }
    }
}
