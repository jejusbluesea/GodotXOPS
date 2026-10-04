using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 텍스트 인코딩 판별 및 변환을 제공하는 유틸리티 클래스.
    /// </summary>
    public static class EncodingHelper
    {
        // 잘못된 바이트 시퀀스를 만나면 예외를 던지는 UTF-8 디코더. 유효성 검증 전용.
        private static readonly UTF8Encoding s_strictUtf8 = new UTF8Encoding(false, true);

        static EncodingHelper()
        {
            // .NET 은 CP949/Shift-JIS 같은 레거시 코드 페이지를 기본으로 제공하지 않으므로 공급자를 등록해야 한다.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 현재 시스템 언어에 적합한 레거시(ANSI) 인코딩을 반환한다.
        /// </summary>
        /// <returns>한국어: CP949, 일본어: Shift-JIS, 기타: Windows-1252 인코딩.</returns>
        public static Encoding GetEncoding()
        {
            switch (OS.GetLocaleLanguage())
            {
                case "ko":
                    return Encoding.GetEncoding(949);
                case "ja":
                    return Encoding.GetEncoding(932);
                default:
                    return Encoding.GetEncoding(1252);
            }
        }

        /// <summary>
        /// 바이트 배열 선두의 BOM을 검사해 유니코드 인코딩을 식별한다.
        /// </summary>
        /// <param name="bytes">검사할 바이트 배열.</param>
        /// <param name="bomLength">검출된 BOM의 바이트 길이. BOM이 없으면 0.</param>
        /// <returns>BOM에 해당하는 유니코드 인코딩. BOM이 없으면 null.</returns>
        public static Encoding DetectBomEncoding(byte[] bytes, out int bomLength)
        {
            bomLength = 0;
            if (bytes == null)
            {
                return null;
            }

            // UTF-32 LE BOM(FF FE 00 00)은 UTF-16 LE BOM(FF FE)을 포함하므로 먼저 검사한다.
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                bomLength = 4;
                return new UTF32Encoding(false, true);
            }
            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                bomLength = 4;
                return new UTF32Encoding(true, true);
            }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                bomLength = 3;
                return Encoding.UTF8;
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                bomLength = 2;
                return Encoding.Unicode;
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                bomLength = 2;
                return Encoding.BigEndianUnicode;
            }
            return null;
        }

        /// <summary>
        /// 바이트 배열을 인코딩 자동 판별 후 문자열로 변환한다. 선두의 BOM은 결과에서 제거된다.
        /// </summary>
        /// <param name="bytes">변환할 바이트 배열.</param>
        /// <returns>디코딩된 문자열. 입력이 null이거나 비어 있으면 빈 문자열.</returns>
        public static string Decode(byte[] bytes)
        {
            return Decode(bytes, GetEncoding());
        }

        /// <summary>
        /// 바이트 배열을 지정한 레거시 인코딩으로 변환한다. 단, BOM이나 유효한 UTF-8이 검출되면 유니코드를 우선한다.
        /// </summary>
        /// <param name="bytes">변환할 바이트 배열.</param>
        /// <param name="fallback">유니코드가 아닐 때 사용할 인코딩.</param>
        /// <returns>디코딩된 문자열. 입력이 null이거나 비어 있으면 빈 문자열.</returns>
        public static string Decode(byte[] bytes, Encoding fallback)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var bom = DetectBomEncoding(bytes, out int bomLength);
            if (bom != null)
            {
                return bom.GetString(bytes, bomLength, bytes.Length - bomLength);
            }

            if (IsValidUtf8(bytes))
            {
                return Encoding.UTF8.GetString(bytes);
            }

            return fallback.GetString(bytes);
        }

        /// <summary>
        /// 파일을 읽어 인코딩 자동 판별 후 문자열로 반환한다.
        /// </summary>
        /// <param name="path">읽을 파일 경로.</param>
        /// <returns>디코딩된 파일 내용.</returns>
        public static string ReadAllText(string path)
        {
            return Decode(File.ReadAllBytes(path));
        }

        /// <summary>
        /// 파일을 읽어 인코딩 자동 판별 후 줄 단위로 분리한 문자열 배열을 반환한다. CR, LF, CRLF 줄바꿈을 모두 처리한다.
        /// </summary>
        /// <param name="path">읽을 파일 경로.</param>
        /// <returns>줄바꿈을 제외한 각 줄의 문자열 배열.</returns>
        public static string[] ReadAllLines(string path)
        {
            var lines = new List<string>();
            using (var reader = new StringReader(ReadAllText(path)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return lines.ToArray();
        }

        /// <summary>
        /// 바이트 배열이 엄격한 UTF-8 규칙에 맞는지 검사한다.
        /// </summary>
        /// <param name="bytes">검사할 바이트 배열.</param>
        /// <returns>유효한 UTF-8이면 true, 잘못된 시퀀스가 있으면 false.</returns>
        private static bool IsValidUtf8(byte[] bytes)
        {
            try
            {
                s_strictUtf8.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }
    }
}
