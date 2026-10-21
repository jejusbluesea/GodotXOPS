using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    // SoundManager 의 날씨 소리와 울림 담당 partial: 날씨가 되풀이해 트는 소리 하나(거리와 무관)와,
    // 구역이 정하는 울림(실내 효과)을 거는 버스. 위치가 있는 효과음과 이벤트의 위치 있는 소리가 그 버스를 지난다.
    public partial class SoundManager
    {
        // 위치가 있는 소리가 지나는 버스. 울림(리버브)이 걸려 있고 Master 로 보낸다.
        private const string k_worldBus = "XopsWorld";
        // 울림의 방 크기 (0 에서 1 사이). 세기는 구역이 정하고 크기는 고정이다.
        private const float k_reverbRoomSize = 0.7f;

        private AudioStreamPlayer m_ambientPlayer;
        private float m_ambientVolume;
        // 날씨 소리에 곱하는 값 (실내 구역에서 줄인다).
        private float m_ambientScale = 1f;
        private bool m_ambientActive;
        private int m_worldBus = -1;
        private AudioEffectReverb m_reverb;
        private float m_reverbAmount;

        // 지금 걸려 있는 울림의 세기 (0 이면 없음).
        public float ReverbAmount => m_reverbAmount;
        // 날씨 소리가 나고 있는지 (헤드리스에서는 상태만).
        public bool AmbientActive => m_ambientActive;

        /// <summary>
        /// 날씨 소리의 재생기와 울림 버스를 만든다. _Ready 에서 부른다.
        /// </summary>
        private void ReadyAmbient()
        {
            m_ambientPlayer = new AudioStreamPlayer { Name = "Ambient" };
            AddChild(m_ambientPlayer);

            m_worldBus = AudioServer.GetBusIndex(k_worldBus);
            if (m_worldBus < 0)
            {
                AudioServer.AddBus();
                m_worldBus = AudioServer.BusCount - 1;
                AudioServer.SetBusName(m_worldBus, k_worldBus);
                AudioServer.SetBusSend(m_worldBus, "Master");
                m_reverb = new AudioEffectReverb { RoomSize = k_reverbRoomSize, Wet = 0f, Dry = 1f };
                AudioServer.AddBusEffect(m_worldBus, m_reverb);
                AudioServer.SetBusEffectEnabled(m_worldBus, 0, false);
            }
            foreach (AudioStreamPlayer player in m_players) player.Bus = k_worldBus;
        }

        /// <summary>
        /// 날씨 소리를 되풀이해 튼다. 나던 날씨 소리는 끊긴다.
        /// </summary>
        /// <param name="relativePath">WAV 경로 (exe 폴더 기준). 비었으면 멈춘다.</param>
        /// <param name="volume">볼륨.</param>
        /// <returns>틀었으면 true. 파일을 읽지 못했으면 false.</returns>
        public bool PlayAmbient(string relativePath, float volume)
        {
            StopAmbient();
            if (string.IsNullOrEmpty(relativePath) || volume <= 0f) return false;

            string fullPath = GamePath.Resolve(relativePath);
            AudioStreamWav stream = fullPath != null ? SoundLoader.LoadAudio(fullPath) : null;
            if (stream == null) return false;

            m_ambientVolume = volume;
            m_ambientActive = true;
            if (m_headless) return true;

            m_ambientPlayer.Stream = stream;
            m_ambientPlayer.VolumeLinear = volume * m_ambientScale * ConfigManager.Instance.MasterVolume;
            m_ambientPlayer.Play();
            return true;
        }

        /// <summary>
        /// 날씨 소리를 멈춘다.
        /// </summary>
        public void StopAmbient()
        {
            m_ambientActive = false;
            if (m_ambientPlayer == null) return;
            m_ambientPlayer.Stop();
            m_ambientPlayer.Stream = null;
        }

        /// <summary>
        /// 날씨 소리에 곱할 값을 정한다 (실내 구역에서 줄인다).
        /// </summary>
        /// <param name="scale">0 에서 1 사이.</param>
        public void SetAmbientScale(float scale)
        {
            m_ambientScale = Mathf.Clamp(scale, 0f, 1f);
        }

        /// <summary>
        /// 위치가 있는 소리에 거는 울림의 세기를 정한다. 듣는 자리가 들어 있는 구역이 프레임마다 정한다.
        /// </summary>
        /// <param name="amount">0 에서 1 사이. 0 이면 울림을 끈다.</param>
        public void SetReverb(float amount)
        {
            amount = Mathf.Clamp(amount, 0f, 1f);
            if (Mathf.IsEqualApprox(amount, m_reverbAmount)) return;

            m_reverbAmount = amount;
            if (m_reverb == null || m_worldBus < 0) return;
            m_reverb.Wet = amount;
            AudioServer.SetBusEffectEnabled(m_worldBus, 0, amount > 0f);
        }

        /// <summary>
        /// 날씨 소리의 볼륨과 끝난 것의 되풀이를 맞춘다. _Process 에서 부른다.
        /// </summary>
        /// <param name="master">마스터 볼륨.</param>
        private void ProcessAmbient(float master)
        {
            if (!m_ambientActive || m_headless) return;

            if (!m_ambientPlayer.Playing) m_ambientPlayer.Play();
            m_ambientPlayer.VolumeLinear = m_ambientVolume * m_ambientScale * master;
        }
    }
}
