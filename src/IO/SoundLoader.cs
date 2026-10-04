using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS.IO
{
    /// <summary>
    /// WAV 오디오 파일을 런타임에 읽어 캐시하는 로더. 디코딩은 Godot 내장 로더에 맡긴다.
    /// </summary>
    public static class SoundLoader
    {
        private static readonly Dictionary<string, AudioStreamWav> s_audioCache = new Dictionary<string, AudioStreamWav>();

        /// <summary>
        /// 지정된 경로의 오디오 파일을 로드해 반환한다. 이미 로드된 오디오는 캐시에서 반환한다.
        /// </summary>
        /// <param name="filepath">오디오 파일 전체 경로.</param>
        /// <returns>로드된 오디오 스트림. 실패 시 null.</returns>
        public static AudioStreamWav LoadAudio(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                Debugger.LogError("Audio path is empty.", nameof(SoundLoader));
                return null;
            }

            if (s_audioCache.TryGetValue(filepath, out AudioStreamWav cached))
            {
                return cached;
            }

            if (!File.Exists(filepath))
            {
                Debugger.LogError($"Audio file not found: {filepath}", nameof(SoundLoader));
                return null;
            }

            if (Path.GetExtension(filepath).ToLowerInvariant() != ".wav")
            {
                Debugger.LogError($"Unsupported audio extension: {filepath}", nameof(SoundLoader));
                return null;
            }

            AudioStreamWav stream = AudioStreamWav.LoadFromBuffer(File.ReadAllBytes(filepath));
            if (stream == null || stream.Data == null || stream.Data.Length == 0)
            {
                Debugger.LogError($"Failed to decode audio: {filepath}", nameof(SoundLoader));
                return null;
            }

            stream.ResourceName = Path.GetFileNameWithoutExtension(filepath);
            s_audioCache.Add(filepath, stream);
            return stream;
        }

        /// <summary>
        /// 오디오 캐시를 비운다.
        /// </summary>
        public static void ClearCache()
        {
            s_audioCache.Clear();
        }
    }
}
