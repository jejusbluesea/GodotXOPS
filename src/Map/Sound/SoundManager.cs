using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// 위치가 있는 효과음을 재생하는 싱글톤. 격발, 착탄, 피격, 폭발 등 모든 효과음이 여기를 거친다.
    /// 원본 OpenXOPS 의 소리는 거리에 따라 선형으로 줄고(MAX_SOUNDDIST 335 에서 0) 좌우 패닝이 없다.
    /// Godot 의 3D 재생기에는 선형 감쇠가 없으므로, 일반 재생기를 풀로 두고 카메라와의 거리로 볼륨을 매 프레임 직접 계산한다.
    /// </summary>
    public partial class SoundManager : Singleton<SoundManager>
    {
        // 이 거리부터 소리가 들리지 않는다 (m). 원본 MAX_SOUNDDIST 335.
        private const float k_maxDistance = 33.5f;
        // 이 거리 안에서는 최대 볼륨이다 (m). 플레이어 자신의 무기 소리가 줄지 않게 한다.
        private const float k_minDistance = 1f;
        // 동시에 재생할 수 있는 소리 수. 다 쓰면 가장 오래된 것부터 끊고 다시 쓴다 (원본 MAX_SOUNDLISTS 100 참고).
        private const int k_poolSize = 64;

        private readonly AudioStreamPlayer[] m_players = new AudioStreamPlayer[k_poolSize];
        private readonly Vector3[] m_positions = new Vector3[k_poolSize];
        private readonly float[] m_volumes = new float[k_poolSize];
        private int m_next;
        private bool m_headless;

        // 점검 도구용 누계와 마지막 재생 정보.
        public static int PlayCount { get; private set; }
        public static string LastPlayedPath { get; private set; } = string.Empty;

        // 소리를 듣는 위치. 현재 카메라가 없으면 원점이다.
        public Vector3 ListenerPosition
        {
            get
            {
                Camera3D camera = GetViewport().GetCamera3D();
                return camera != null ? camera.GlobalPosition : Vector3.Zero;
            }
        }

        public override void _Ready()
        {
            m_headless = DisplayServer.GetName() == "headless";

            for (int i = 0; i < k_poolSize; i++)
            {
                m_players[i] = new AudioStreamPlayer { Name = $"Sfx_{i}" };
                AddChild(m_players[i]);
            }
        }

        public override void _Process(double delta)
        {
            Vector3 listener = ListenerPosition;
            float master = ConfigManager.Instance.MasterVolume;
            for (int i = 0; i < k_poolSize; i++)
            {
                if (m_players[i].Playing) m_players[i].VolumeLinear = m_volumes[i] * master * Attenuation(m_positions[i], listener);
            }
        }

        /// <summary>
        /// 지정한 위치에서 효과음을 재생한다.
        /// </summary>
        /// <param name="relativePath">데이터 루트 기준 WAV 경로. 비어 있으면 무시.</param>
        /// <param name="position">소리가 나는 위치.</param>
        /// <param name="volume">볼륨. 0 이하면 무시.</param>
        public void PlayAt(string relativePath, Vector3 position, float volume)
        {
            if (string.IsNullOrEmpty(relativePath) || volume <= 0f) return;

            string fullPath = GamePath.Resolve(relativePath);
            AudioStreamWav stream = fullPath != null ? SoundLoader.LoadAudio(fullPath) : null;
            if (stream == null) return;

            PlayCount++;
            LastPlayedPath = relativePath;

            // 헤드리스(점검 씬)에서는 실제로 재생하지 않는다. 재생 중에 바로 종료하면 재생 객체가 정리되지 못해 종료 경고가 난다.
            if (m_headless) return;

            int index = AcquirePlayer();
            m_positions[index] = position;
            m_volumes[index] = volume;

            AudioStreamPlayer player = m_players[index];
            player.Stream = stream;
            player.VolumeLinear = volume * ConfigManager.Instance.MasterVolume * Attenuation(position, ListenerPosition);
            player.Play();
        }

        /// <summary>
        /// 그 위치에서 난 소리가 지금 듣는 위치에서 들리는지 알려 준다.
        /// </summary>
        /// <param name="position">소리가 나는 위치.</param>
        /// <returns>거리 감쇠가 0 보다 크면 true.</returns>
        public bool IsAudible(Vector3 position)
        {
            return Attenuation(position, ListenerPosition) > 0f;
        }

        /// <summary>
        /// 목록에서 하나를 무작위로 골라 재생한다. 같은 경로를 여러 번 넣어 확률에 가중치를 줄 수 있다.
        /// </summary>
        /// <param name="relativePaths">WAV 경로 목록. 비어 있으면 무시.</param>
        /// <param name="position">소리가 나는 위치.</param>
        /// <param name="volume">볼륨.</param>
        public void PlayRandomAt(List<string> relativePaths, Vector3 position, float volume)
        {
            if (relativePaths == null || relativePaths.Count == 0) return;
            PlayAt(relativePaths[GameRandom.Visual.Range(0, relativePaths.Count)], position, volume);
        }

        /// <summary>
        /// 재생 중인 소리를 모두 멈춘다. 맵을 내릴 때 호출한다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < k_poolSize; i++)
            {
                m_players[i].Stop();
                m_players[i].Stream = null;
            }
            m_next = 0;
        }

        /// <summary>
        /// 거리에 따른 볼륨 배율을 구한다. 최소 거리까지 1, 최대 거리에서 0, 그 사이는 선형이다.
        /// </summary>
        /// <param name="position">소리 위치.</param>
        /// <param name="listener">듣는 위치.</param>
        /// <returns>0~1 의 배율.</returns>
        public static float Attenuation(Vector3 position, Vector3 listener)
        {
            float distance = position.DistanceTo(listener);
            return Mathf.Clamp(1f - (distance - k_minDistance) / (k_maxDistance - k_minDistance), 0f, 1f);
        }

        /// <summary>
        /// 쓸 재생기를 고른다. 쉬는 것이 있으면 그것을, 없으면 가장 오래된 것부터 돌아가며 쓴다.
        /// </summary>
        /// <returns>재생기 인덱스.</returns>
        private int AcquirePlayer()
        {
            for (int i = 0; i < k_poolSize; i++)
            {
                if (!m_players[i].Playing) return i;
            }

            int index = m_next;
            m_next = (m_next + 1) % k_poolSize;
            return index;
        }
    }
}
