using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// 날씨의 연출을 그리는 노드: 카메라 둘레에 내리는 것(비, 눈), 되풀이하는 소리, 번개(화면이 번쩍이고 소리가 난다).
    /// 전부 렌더 프레임의 연출이고 게임 결과에 영향을 주지 않는다 (난수는 GameRandom.Visual). 게임 정지 중에는 멈춘다.
    /// 카메라가 날씨를 막는 구역(실내) 안에 있으면 내리는 것을 감추고 소리를 줄인다.
    /// 기준 날씨(MapLoader.SetWeather)와 따로, 카메라가 날씨를 정한 구역 안에 있으면 그 구역의 날씨를 그린다 (나오면 기준 날씨로 돌아온다).
    /// </summary>
    public partial class WeatherView : Node3D
    {
        // 실내에서 날씨 소리에 곱하는 값.
        private const float k_indoorSoundScale = 0.35f;
        // 번쩍임을 그리는 캔버스 층. 3D 바로 위, 이벤트의 암전(0)과 HUD 보다 아래.
        private const int k_flashCanvasLayer = -1;
        // 내리는 것이 화면에서 잘리지 않게 넉넉히 잡는 표시 범위 (m).
        private const float k_visibilityExtent = 1000f;

        private GpuParticles3D m_particles;
        private CanvasLayer m_flashLayer;
        private ColorRect m_flash;
        private WeatherData m_data;
        // 기준 날씨 (이벤트가 정한 것). 구역의 날씨가 없을 때 그린다.
        private WeatherData m_base;
        private Vector3 m_lastCamera;
        private float m_lightningTimer;
        private float m_flashTime = -1f;
        private float m_thunderTimer = -1f;
        private bool m_indoor;

        // 지금 그리는 날씨 (구역의 날씨 포함). null 이면 없다.
        public WeatherData Data => m_data;
        // 카메라가 날씨를 막는 구역 안에 있는지 (마지막 프레임 기준).
        public bool Indoor => m_indoor;

        public override void _Ready()
        {
            m_flashLayer = new CanvasLayer { Name = "LightningFlash", Layer = k_flashCanvasLayer };
            AddChild(m_flashLayer);
            m_flash = new ColorRect { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            m_flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            m_flashLayer.AddChild(m_flash);
        }

        public override void _Process(double delta)
        {
            Camera3D camera = GetViewport().GetCamera3D();
            Vector3 cameraPosition = camera != null ? camera.GlobalPosition : Vector3.Zero;
            m_lastCamera = cameraPosition;

            // 카메라가 날씨를 정한 구역에 들어가거나 나오면 그리는 날씨를 바꾸고 안개·하늘 가리기를 다시 맞춘다.
            WeatherData shown = ShownWeather(cameraPosition);
            if (!ReferenceEquals(shown, m_data))
            {
                Show(shown);
                MapLoader.RefreshFog();
            }
            if (m_data == null) return;

            m_indoor = MapLoader.WeatherBlockedAt(cameraPosition);
            SoundManager.Instance.SetAmbientScale(m_indoor ? k_indoorSoundScale : 1f);

            bool paused = SimClock.WorldPaused;
            if (m_particles != null)
            {
                m_particles.GlobalPosition = cameraPosition + Vector3.Up * (m_data.areaHeight * 0.5f);
                m_particles.Visible = !m_indoor;
                m_particles.SpeedScale = paused ? 0f : 1f;
            }

            if (!paused) TickLightning((float)delta, cameraPosition);
        }

        /// <summary>
        /// 기준 날씨를 바꾼다. 바로 바뀐다 (서서히 바뀌지 않는다). 카메라가 날씨를 정한 구역 안이면 그 구역의 날씨를 계속 그린다.
        /// </summary>
        /// <param name="data">새 기준 날씨. null 이면 기준 날씨가 없다 (맑음과 같다).</param>
        public void SetBase(WeatherData data)
        {
            m_base = data;
            Show(ShownWeather(m_lastCamera));
        }

        /// <summary>
        /// 그 자리에서 그릴 날씨: 날씨를 정한 구역(번호가 가장 큰 태그) 안이면 그 날씨, 아니면 기준 날씨. 구역의 날씨 번호가 목록에 없으면 기준 날씨다.
        /// </summary>
        /// <param name="cameraPosition">카메라 자리.</param>
        /// <returns>날씨. 없으면 null.</returns>
        private WeatherData ShownWeather(Vector3 cameraPosition)
        {
            int index = MapLoader.AreaWeatherAt(cameraPosition);
            var list = DataManager.Instance.WeatherParameterData.weatherData;
            return index >= 0 && list.Has(index) ? list[index] : m_base;
        }

        /// <summary>
        /// 그리는 날씨를 바꾼다 (내리는 것, 소리, 번개). 안개와 하늘 가리기는 MapLoader 가 맞춘다.
        /// </summary>
        /// <param name="data">그릴 날씨. null 이면 아무것도 그리지 않는다.</param>
        private void Show(WeatherData data)
        {
            ClearVisuals();
            m_data = data;
            if (data == null) return;

            if (data.precipitation != WeatherPrecipitation.None && data.particleAmount > 0) BuildParticles(data);

            if (data.soundIndex >= 0) SoundManager.Instance.PlayAmbient(SoundPath(data.soundIndex), data.soundVolume);
            ResetLightningTimer();
        }

        /// <summary>
        /// 기준 날씨와 그리던 날씨를 전부 지운다 (맵을 내릴 때).
        /// </summary>
        public void Clear()
        {
            m_base = null;
            ClearVisuals();
        }

        /// <summary>
        /// 그리던 날씨를 지운다 (기준 날씨는 남긴다).
        /// </summary>
        private void ClearVisuals()
        {
            m_data = null;
            m_indoor = false;
            m_flashTime = -1f;
            m_thunderTimer = -1f;
            if (m_flash != null) m_flash.Visible = false;
            if (m_particles != null)
            {
                RemoveChild(m_particles);
                m_particles.Free();
                m_particles = null;
            }
            if (SoundManager.Loaded)
            {
                SoundManager.Instance.StopAmbient();
                SoundManager.Instance.SetAmbientScale(1f);
            }
        }

        /// <summary>
        /// 카메라 둘레의 상자에서 내리는 입자를 만든다. 입자는 월드 공간에 남고 상자만 카메라를 따라간다.
        /// </summary>
        /// <param name="data">날씨.</param>
        private void BuildParticles(WeatherData data)
        {
            float fallSpeed = Mathf.Max(0.1f, data.fallSpeed);
            Vector3 velocity = Vector3.Down * fallSpeed + Coord.FromUnity(data.wind);
            float lifetime = Mathf.Max(0.1f, data.areaHeight / fallSpeed);
            float radius = Mathf.Max(0.1f, data.areaRadius);

            var process = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(radius, data.areaHeight * 0.5f, radius),
                Direction = velocity.Normalized(),
                Spread = 0f,
                InitialVelocityMin = velocity.Length(),
                InitialVelocityMax = velocity.Length(),
                Gravity = Vector3.Zero,
            };

            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = data.particleColor.ToColor(),
                // 비는 세로로 선 채 카메라 쪽을 보고, 눈은 늘 카메라를 본다.
                BillboardMode = data.precipitation == WeatherPrecipitation.Rain
                    ? BaseMaterial3D.BillboardModeEnum.FixedY : BaseMaterial3D.BillboardModeEnum.Enabled,
                BillboardKeepScale = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            Texture2D texture = LoadTexture(data.particleTexturePath);
            if (texture != null) material.AlbedoTexture = texture;

            m_particles = new GpuParticles3D
            {
                Name = "Precipitation",
                Amount = data.particleAmount,
                Lifetime = lifetime,
                Preprocess = lifetime,
                LocalCoords = false,
                ProcessMaterial = process,
                DrawPass1 = new QuadMesh { Size = new Vector2(Mathf.Max(0.001f, data.particleWidth), Mathf.Max(0.001f, data.particleLength)), Material = material },
                VisibilityAabb = new Aabb(-Vector3.One * k_visibilityExtent, Vector3.One * k_visibilityExtent * 2f),
            };
            AddChild(m_particles);
        }

        /// <summary>
        /// 번개: 시간이 되면 화면을 번쩍이고 조금 뒤에 소리를 낸다. 번쩍임은 정한 시간에 걸쳐 사라진다.
        /// </summary>
        /// <param name="delta">프레임 시간 (초).</param>
        /// <param name="cameraPosition">소리를 낼 자리 (듣는 자리).</param>
        private void TickLightning(float delta, Vector3 cameraPosition)
        {
            if (m_data.lightningIntervalMax <= 0f) return;

            m_lightningTimer -= delta;
            if (m_lightningTimer <= 0f)
            {
                m_flashTime = 0f;
                m_thunderTimer = Mathf.Max(0f, m_data.lightningSoundDelay);
                ResetLightningTimer();
            }

            if (m_flashTime >= 0f)
            {
                float duration = Mathf.Max(0.01f, m_data.lightningDuration);
                Color color = m_data.lightningColor.ToColor();
                color.A *= 1f - Mathf.Clamp(m_flashTime / duration, 0f, 1f);
                m_flash.Color = color;
                m_flash.Visible = color.A > 0f;
                m_flashTime += delta;
                if (m_flashTime > duration)
                {
                    m_flashTime = -1f;
                    m_flash.Visible = false;
                }
            }

            if (m_thunderTimer >= 0f)
            {
                m_thunderTimer -= delta;
                if (m_thunderTimer < 0f && m_data.lightningSoundIndex >= 0)
                {
                    SoundManager.Instance.PlayAt(SoundPath(m_data.lightningSoundIndex), cameraPosition, m_indoor ? k_indoorSoundScale : 1f);
                }
            }
        }

        /// <summary>
        /// 다음 번개까지의 시간을 정한다 (간격의 최소와 최대 사이의 난수).
        /// </summary>
        private void ResetLightningTimer()
        {
            if (m_data == null || m_data.lightningIntervalMax <= 0f) return;

            float min = Mathf.Max(0.1f, Mathf.Min(m_data.lightningIntervalMin, m_data.lightningIntervalMax));
            m_lightningTimer = (float)GameRandom.Visual.Range(min, Mathf.Max(min, m_data.lightningIntervalMax));
        }

        /// <summary>
        /// 소리 목록의 번호를 파일 경로로 바꾼다. 없는 번호면 경고하고 빈 경로다.
        /// </summary>
        /// <param name="index">소리 목록의 번호.</param>
        /// <returns>WAV 경로.</returns>
        private static string SoundPath(int index)
        {
            var list = DataManager.Instance.SoundParameterData.soundData;
            if (list.Has(index)) return list[index].soundPath;

            Debugger.LogWarning($"Weather sound {index} is not in the sound list.", nameof(WeatherView));
            return string.Empty;
        }

        /// <summary>
        /// 내리는 것의 그림을 읽는다.
        /// </summary>
        /// <param name="relativePath">exe 폴더 기준 경로. 비었으면 없다.</param>
        /// <returns>텍스처. 비었거나 읽지 못했으면 null (읽지 못했으면 경고).</returns>
        private static Texture2D LoadTexture(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            string fullPath = GamePath.Resolve(relativePath);
            Texture2D texture = fullPath != null ? ImageLoader.LoadTexture(fullPath) : null;
            if (texture == null) Debugger.LogWarning($"Weather texture open failed: {relativePath}", nameof(WeatherView));
            return texture;
        }
    }
}
