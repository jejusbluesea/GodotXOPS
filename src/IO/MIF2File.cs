using System;
using System.IO;

namespace GodotXOPS.IO
{
    /// <summary>
    /// 확장 미션 파일(MIF2)의 읽기와 쓰기. 내용은 JSON 이고 키는 ExtendedMissionData 의 필드 이름이다.
    /// 파일에 없는 키는 기본값으로 남고, 모르는 키는 경고 로그를 남기고 무시한다.
    /// </summary>
    public static class MIF2File
    {
        public const string Extension = ".mif2";

        /// <summary>
        /// MIF2 파일을 읽는다.
        /// </summary>
        /// <param name="filepath">MIF2 파일 전체 경로.</param>
        /// <param name="data">읽은 내용. 실패하면 null.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>읽기에 성공했으면 true.</returns>
        public static bool Read(string filepath, out ExtendedMissionData data, out string error)
        {
            data = null;
            error = null;

            string text;
            try
            {
                text = EncodingHelper.ReadAllText(filepath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }

            var result = new ExtendedMissionData();
            if (string.IsNullOrWhiteSpace(text) || !JsonData.Overwrite(text, result, Path.GetFileName(filepath)))
            {
                error = "not a valid JSON object";
                return false;
            }

            data = result;
            return true;
        }

        /// <summary>
        /// MIF2 파일로 쓴다 (UTF-8). 같은 이름의 파일이 있으면 덮어쓴다.
        /// </summary>
        /// <param name="filepath">MIF2 파일 전체 경로.</param>
        /// <param name="data">쓸 내용.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>쓰기에 성공했으면 true.</returns>
        public static bool Write(string filepath, ExtendedMissionData data, out string error)
        {
            error = null;
            try
            {
                File.WriteAllText(filepath, JsonData.ToJson(data));
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
