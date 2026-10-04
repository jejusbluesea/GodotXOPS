using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS.IO
{
    /// <summary>
    /// 3D 모델 파일(.x)을 런타임에 읽어 메시로 만들고 캐시하는 로더.
    /// </summary>
    public static class ModelLoader
    {
        private static readonly Dictionary<string, ArrayMesh> s_meshCache = new Dictionary<string, ArrayMesh>();

        /// <summary>
        /// 지정 경로의 모델 파일을 로드해 메시를 반환한다. 캐시된 메시는 재사용한다.
        /// </summary>
        /// <param name="filepath">모델 파일 전체 경로.</param>
        /// <returns>로드된 메시. 실패 시 null.</returns>
        public static ArrayMesh LoadMesh(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                Debugger.LogError("Mesh path is empty.", nameof(ModelLoader));
                return null;
            }

            if (s_meshCache.TryGetValue(filepath, out ArrayMesh cached))
            {
                return cached;
            }

            if (Path.GetExtension(filepath).ToLowerInvariant() != ".x")
            {
                Debugger.LogError($"Unsupported mesh extension: {filepath}", nameof(ModelLoader));
                return null;
            }

            XFile xFile = XFile.Load(filepath);
            if (xFile == null)
            {
                return null;
            }

            string meshName = Path.GetFileNameWithoutExtension(filepath);
            ArrayMesh mesh = BuildMesh(xFile, meshName);
            if (mesh == null)
            {
                return null;
            }

            s_meshCache.Add(filepath, mesh);
            return mesh;
        }

        /// <summary>
        /// 메시 캐시를 비운다.
        /// </summary>
        public static void ClearCache()
        {
            s_meshCache.Clear();
        }

        /// <summary>
        /// XFile의 모든 메시 데이터를 서피스 하나짜리 메시로 합친다. 머티리얼은 붙이지 않는다(사용처가 텍스처와 함께 지정).
        /// </summary>
        /// <param name="xFile">파싱된 .x 데이터.</param>
        /// <param name="meshName">리소스 이름.</param>
        /// <returns>만들어진 메시. 정점이나 면이 없으면 null.</returns>
        private static ArrayMesh BuildMesh(XFile xFile, string meshName)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();

            foreach (XMeshData meshData in xFile.Meshes)
            {
                int offset = vertices.Count;
                foreach (Vector3 vertex in meshData.Vertices)
                {
                    // .x 정점은 UnityXOPS 가 변환 없이 쓰던 값이므로 UnityXOPS 공간으로 취급한다.
                    vertices.Add(Coord.FromUnity(vertex));
                }
                // DirectX 와 Godot 모두 UV 원점이 좌상단이라 V 를 뒤집지 않는다.
                uvs.AddRange(meshData.UVs);
                foreach (int index in meshData.Indices)
                {
                    indices.Add(index + offset);
                }
            }

            if (vertices.Count == 0 || indices.Count == 0)
            {
                Debugger.LogError($"No mesh data found in .x file: {meshName}", nameof(ModelLoader));
                return null;
            }

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = ComputeSmoothNormals(vertices, indices);
            arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

            var mesh = new ArrayMesh();
            mesh.ResourceName = meshName;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        /// <summary>
        /// 정점을 공유하는 면들의 법선을 면적 가중으로 평균해 정점 법선을 만든다.
        /// </summary>
        /// <param name="vertices">정점 목록.</param>
        /// <param name="indices">삼각형 인덱스 목록.</param>
        /// <returns>정점 수와 같은 길이의 법선 배열.</returns>
        private static Vector3[] ComputeSmoothNormals(List<Vector3> vertices, List<int> indices)
        {
            var normals = new Vector3[vertices.Count];

            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                int a = indices[i];
                int b = indices[i + 1];
                int c = indices[i + 2];

                // Godot 은 시계 방향이 앞면이므로 앞면 법선은 (c - a) × (b - a) 다. 정규화 전 길이가 면적에 비례한다.
                Vector3 faceNormal = (vertices[c] - vertices[a]).Cross(vertices[b] - vertices[a]);
                normals[a] += faceNormal;
                normals[b] += faceNormal;
                normals[c] += faceNormal;
            }

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normals[i].LengthSquared() > 0f ? normals[i].Normalized() : Vector3.Up;
            }

            return normals;
        }
    }
}
