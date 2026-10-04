using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace GodotXOPS.IO
{
    /// <summary>
    /// .x 파일에서 파싱된 단일 메시의 정점, 인덱스, UV 데이터를 담는 클래스. 값은 파일에 적힌 그대로다(좌표 변환 전).
    /// </summary>
    public class XMeshData
    {
        public List<Vector3> Vertices = new List<Vector3>();
        public List<int> Indices = new List<int>();
        public List<Vector2> UVs = new List<Vector2>();
    }

    /// <summary>
    /// 텍스트 형식 DirectX .x 파일 파서. 정점·면·텍스처 좌표만 읽고 나머지 블록(법선, 머티리얼, 애니메이션)은 건너뛴다.
    /// </summary>
    public class XFile
    {
        public List<XMeshData> Meshes = new List<XMeshData>();

        /// <summary>
        /// .x 파일 텍스트를 토큰 단위로 파싱하는 내부 클래스.
        /// </summary>
        private class XTokenizer
        {
            private readonly string m_text;
            private int m_pos;

            public XTokenizer(string text)
            {
                m_text = text;
                m_pos = 0;
            }

            /// <summary>
            /// 공백, 구분자, 주석을 건너뛰어 다음 토큰의 시작 위치로 이동한다.
            /// </summary>
            private void SkipSeparators()
            {
                while (m_pos < m_text.Length)
                {
                    char c = m_text[m_pos];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == ';' || c == ',')
                    {
                        m_pos++;
                    }
                    else if (c == '#' || (c == '/' && m_pos + 1 < m_text.Length && m_text[m_pos + 1] == '/'))
                    {
                        while (m_pos < m_text.Length && m_text[m_pos] != '\n') m_pos++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            /// <summary>
            /// 현재 위치를 변경하지 않고 다음 토큰을 미리 읽어 반환한다.
            /// </summary>
            /// <returns>다음 토큰. 끝이면 null.</returns>
            public string Peek()
            {
                int saved = m_pos;
                string token = Read();
                m_pos = saved;
                return token;
            }

            /// <summary>
            /// 현재 위치에서 다음 토큰을 읽고 위치를 전진시켜 반환한다.
            /// </summary>
            /// <returns>읽은 토큰. 끝이면 null.</returns>
            public string Read()
            {
                SkipSeparators();
                if (m_pos >= m_text.Length) return null;

                char c = m_text[m_pos];

                if (c == '{') { m_pos++; return "{"; }
                if (c == '}') { m_pos++; return "}"; }

                // GUID 건너뜀: <xxxxxxxx-...>
                if (c == '<')
                {
                    while (m_pos < m_text.Length && m_text[m_pos] != '>') m_pos++;
                    if (m_pos < m_text.Length) m_pos++;
                    return Read();
                }

                // 문자열 리터럴
                if (c == '"')
                {
                    m_pos++;
                    int stringStart = m_pos;
                    while (m_pos < m_text.Length && m_text[m_pos] != '"') m_pos++;
                    string str = m_text.Substring(stringStart, m_pos - stringStart);
                    if (m_pos < m_text.Length) m_pos++;
                    return "\"" + str + "\"";
                }

                // 식별자 또는 숫자
                int start = m_pos;
                while (m_pos < m_text.Length)
                {
                    char ch = m_text[m_pos];
                    if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' ||
                        ch == ';' || ch == ',' || ch == '{' || ch == '}' ||
                        ch == '<' || ch == '>')
                    {
                        break;
                    }
                    m_pos++;
                }
                return m_text.Substring(start, m_pos - start);
            }

            /// <summary>
            /// 다음 토큰을 float으로 파싱해 반환한다.
            /// </summary>
            /// <returns>파싱된 값.</returns>
            public float ReadFloat()
            {
                return float.Parse(Read(), CultureInfo.InvariantCulture);
            }

            /// <summary>
            /// 다음 토큰을 int로 파싱해 반환한다.
            /// </summary>
            /// <returns>파싱된 값.</returns>
            public int ReadInt()
            {
                return int.Parse(Read(), CultureInfo.InvariantCulture);
            }

            /// <summary>
            /// 현재 블록의 닫는 중괄호까지 모든 토큰을 건너뛴다. '{' 는 이미 소비한 상태여야 한다.
            /// </summary>
            public void SkipBlock()
            {
                int depth = 1;
                while (m_pos < m_text.Length && depth > 0)
                {
                    string t = Read();
                    if (t == null) break;
                    if (t == "{") depth++;
                    else if (t == "}") depth--;
                }
            }

            /// <summary>
            /// 블록 앞에 선택적으로 붙는 이름 토큰이 있으면 건너뛰고 여는 중괄호까지 소비한다. (예: "Mesh {" vs "Mesh obj11 {")
            /// </summary>
            public void EnterBlock()
            {
                if (Peek() != "{")
                {
                    Read();
                }
                Read();
            }

            /// <summary>
            /// 다음 토큰이 여는 중괄호면 그 블록 전체를 건너뛴다. 알 수 없는 블록 무시용.
            /// </summary>
            public void SkipBlockIfPresent()
            {
                if (Peek() == "{")
                {
                    Read();
                    SkipBlock();
                }
            }
        }

        /// <summary>
        /// 지정 경로의 .x 파일을 파싱한다.
        /// </summary>
        /// <param name="filepath">.x 파일 전체 경로.</param>
        /// <returns>파싱된 XFile. 실패 시 null.</returns>
        public static XFile Load(string filepath)
        {
            if (!File.Exists(filepath))
            {
                Debugger.LogError($"File not found: {filepath}", nameof(XFile));
                return null;
            }

            string text = EncodingHelper.ReadAllText(filepath);

            if (text.Length < 16 || !text.StartsWith("xof"))
            {
                Debugger.LogError($"File is not a valid .x file: {filepath}", nameof(XFile));
                return null;
            }

            // 헤더는 "xof " + 버전(4) + 형식(4) + 실수 크기(4) = 16바이트. 텍스트 형식("txt ")만 지원한다.
            if (text.Substring(8, 4) != "txt ")
            {
                Debugger.LogError($"Only text format .x files are supported ({text.Substring(8, 4).Trim()}): {filepath}", nameof(XFile));
                return null;
            }

            try
            {
                // 줄바꿈 없이 헤더 뒤에 바로 본문이 오는 파일도 있어 16바이트 이후부터 파싱한다.
                var tokenizer = new XTokenizer(text.Substring(16));
                var xFile = new XFile();
                ParseBlocks(tokenizer, xFile, false);
                return xFile;
            }
            catch (System.Exception e)
            {
                Debugger.LogError($"Failed to parse .x file: {filepath}\n{e.Message}", nameof(XFile));
                return null;
            }
        }

        /// <summary>
        /// 블록들을 순회하며 Frame(재귀)과 Mesh를 파싱한다. 최상위와 Frame 내부가 같은 규칙을 쓴다.
        /// </summary>
        /// <param name="tokenizer">토크나이저.</param>
        /// <param name="xFile">메시를 추가할 대상.</param>
        /// <param name="insideFrame">Frame 내부면 true. 닫는 중괄호에서 멈추고 소비한다.</param>
        private static void ParseBlocks(XTokenizer tokenizer, XFile xFile, bool insideFrame)
        {
            string token;
            while ((token = tokenizer.Peek()) != null)
            {
                tokenizer.Read();
                if (token == "}")
                {
                    if (insideFrame) return;
                    continue;
                }

                switch (token)
                {
                    case "Frame":
                        tokenizer.EnterBlock();
                        ParseBlocks(tokenizer, xFile, true);
                        break;

                    case "Mesh":
                        tokenizer.EnterBlock();
                        xFile.Meshes.Add(ParseMesh(tokenizer));
                        break;

                    case "template":
                    case "Header":
                    case "FrameTransformMatrix":
                        tokenizer.EnterBlock();
                        tokenizer.SkipBlock();
                        break;

                    default:
                        tokenizer.SkipBlockIfPresent();
                        break;
                }
            }
        }

        /// <summary>
        /// Mesh 블록을 파싱해 정점, 면, UV를 포함하는 XMeshData를 반환한다.
        /// </summary>
        /// <param name="tokenizer">Mesh 블록의 여는 중괄호 직후에 위치한 토크나이저.</param>
        /// <returns>파싱된 메시 데이터.</returns>
        private static XMeshData ParseMesh(XTokenizer tokenizer)
        {
            var meshData = new XMeshData();

            int vertexCount = tokenizer.ReadInt();
            for (int i = 0; i < vertexCount; i++)
            {
                float x = tokenizer.ReadFloat();
                float y = tokenizer.ReadFloat();
                float z = tokenizer.ReadFloat();
                meshData.Vertices.Add(new Vector3(x, y, z));
                meshData.UVs.Add(Vector2.Zero); // MeshTextureCoords 가 없을 때의 기본값
            }

            // 면 — 삼각형/사각형 혼합을 팬으로 삼각화한다.
            int faceCount = tokenizer.ReadInt();
            for (int i = 0; i < faceCount; i++)
            {
                int cornerCount = tokenizer.ReadInt();
                int[] corners = new int[cornerCount];
                for (int j = 0; j < cornerCount; j++)
                {
                    corners[j] = tokenizer.ReadInt();
                }

                for (int j = 1; j < cornerCount - 1; j++)
                {
                    meshData.Indices.Add(corners[0]);
                    meshData.Indices.Add(corners[j]);
                    meshData.Indices.Add(corners[j + 1]);
                }
            }

            string token;
            while ((token = tokenizer.Peek()) != null && token != "}")
            {
                tokenizer.Read();
                if (token == "MeshTextureCoords")
                {
                    tokenizer.EnterBlock();
                    ParseMeshTextureCoords(tokenizer, meshData);
                }
                else
                {
                    // MeshNormals, MeshMaterialList 등은 쓰지 않는다.
                    tokenizer.SkipBlockIfPresent();
                }
            }

            if (tokenizer.Peek() == "}") tokenizer.Read();

            return meshData;
        }

        /// <summary>
        /// MeshTextureCoords 블록을 파싱해 meshData의 UV 목록을 갱신한다.
        /// </summary>
        /// <param name="tokenizer">블록의 여는 중괄호 직후에 위치한 토크나이저.</param>
        /// <param name="meshData">UV를 채울 메시 데이터.</param>
        private static void ParseMeshTextureCoords(XTokenizer tokenizer, XMeshData meshData)
        {
            int coordCount = tokenizer.ReadInt();
            for (int i = 0; i < coordCount; i++)
            {
                float u = tokenizer.ReadFloat();
                float v = tokenizer.ReadFloat();
                if (i < meshData.UVs.Count)
                {
                    meshData.UVs[i] = new Vector2(u, v);
                }
            }

            if (tokenizer.Peek() == "}") tokenizer.Read();
        }
    }
}
