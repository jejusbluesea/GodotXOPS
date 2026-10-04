using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        private const int k_textureSlotCount = 10;
        private const int k_texturePathLength = 31;

        // openxops.net/filesystem-bd1.php 기준. 면마다 정점 4개의 인덱스.
        private static readonly int[][] s_faceVertexIndices =
        {
            new[] { 0, 3, 2, 1 },
            new[] { 7, 4, 5, 6 },
            new[] { 4, 0, 1, 5 },
            new[] { 5, 1, 2, 6 },
            new[] { 6, 2, 3, 7 },
            new[] { 7, 3, 0, 4 },
        };

        // 원본 datafile.cpp:181-268 fake lighting 광원 방향.
        // OpenXOPS: L = (cos(190°), sin(120°), sin(190°)) — 정규화되지 않음(‖L‖≈1.312), 원본 그대로 사용.
        private static readonly Vector3 s_lightDirection = Coord.DirectionFromXops(-0.98480775f, 0.86602540f, -0.17364818f);

        // 평평한 블록의 부피 0 AABB 로 인한 프러스텀 컬링 오작동 방지용 최소 두께 (미터, = 1 OpenXOPS 단위).
        private const float k_minBoundsThickness = 0.1f;

        // 충돌 AABB 여유 (원본 COLLISION_ADDSIZE × 0.1). 브로드페이즈 경계 케이스 포함용.
        private const float k_collisionAddSize = 0.001f;

        private readonly List<Block> m_blocks = new List<Block>();
        private readonly List<Block> m_blockColliders = new List<Block>();
        private readonly List<ShaderMaterial> m_blockMaterials = new List<ShaderMaterial>();

        // 로드된 맵의 모든 블록(충돌 없는 판형 블록 포함).
        public static IReadOnlyList<Block> Blocks => Instance.m_blocks;
        // 충돌 판정 대상 블록.
        public static IReadOnlyList<Block> BlockColliders => Instance.m_blockColliders;

        /// <summary>
        /// BD1 파일을 파싱해 블록 메시와 머티리얼을 생성하고 배치한다. 이전에 로드된 블록은 먼저 제거한다.
        /// </summary>
        /// <param name="filepath">BD1 파일 전체 경로.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        public static bool LoadBlockData(string filepath)
        {
            UnloadBlockData();

            if (string.IsNullOrEmpty(filepath))
            {
                Debugger.LogError("BD1 path is empty.", nameof(MapLoader));
                return false;
            }

            if (!File.Exists(filepath))
            {
                Debugger.LogError($"BD1 file not exists: {filepath}", nameof(MapLoader));
                return false;
            }

            if (!LoadBD1File(filepath, out string[] texturePaths, out RawBlockData[] rawBlocks))
            {
                return false;
            }

            MapLoader loader = Instance;

            // 텍스처 슬롯 10개는 항상 머티리얼을 가진다. 경로가 비었거나 로드에 실패한 슬롯은 흰색 머티리얼이다.
            string bd1Directory = Path.GetDirectoryName(filepath);
            for (int i = 0; i < texturePaths.Length; i++)
            {
                ImageTexture texture = null;
                if (!string.IsNullOrEmpty(texturePaths[i]))
                {
                    string fullTexturePath = SafePath.Combine(bd1Directory, texturePaths[i]);
                    texture = fullTexturePath != null ? ImageLoader.LoadTexture(fullTexturePath) : null;
                }

                ShaderMaterial material = MaterialManager.Instance.CreateBlockMaterial(texture);
                material.ResourceName = string.IsNullOrEmpty(texturePaths[i]) ? $"block_slot_{i}" : Path.GetFileName(texturePaths[i]);
                loader.m_blockMaterials.Add(material);
            }

            for (int i = 0; i < rawBlocks.Length; i++)
            {
                Block block = BuildBlock(rawBlocks[i], loader.m_darkScreen, loader.m_blockMaterials.Count);
                loader.m_blocks.Add(block);
                if (block.collider)
                {
                    loader.m_blockColliders.Add(block);
                }

                if (block.mesh == null)
                {
                    continue;
                }

                block.mesh.ResourceName = $"block_{i}";
                var instance = new MeshInstance3D { Name = $"Block_{i}", Mesh = block.mesh, Position = block.position };
                for (int s = 0; s < block.surfaceTextureIndices.Length; s++)
                {
                    instance.SetSurfaceOverrideMaterial(s, loader.m_blockMaterials[block.surfaceTextureIndices[s]]);
                }
                loader.m_blockRoot.AddChild(instance);
            }

            return true;
        }

        /// <summary>
        /// 생성된 모든 블록 노드와 머티리얼, 충돌 데이터를 제거한다.
        /// </summary>
        public static void UnloadBlockData()
        {
            MapLoader loader = Instance;

            // 언로드→로드가 한 프레임에서 이어지므로 지연 삭제(QueueFree) 대신 즉시 삭제한다.
            // 블록 노드는 시그널이나 스크립트가 없는 순수 메시라 즉시 삭제해도 안전하다.
            foreach (Node child in loader.m_blockRoot.GetChildren())
            {
                loader.m_blockRoot.RemoveChild(child);
                child.Free();
            }

            loader.m_blocks.Clear();
            loader.m_blockColliders.Clear();
            loader.m_blockMaterials.Clear();
        }

        /// <summary>
        /// 레이가 처음 만나는 충돌 블록까지의 거리를 구한다 (원본 CheckALLBlockIntersectRay 대응).
        /// 블록의 앞면만 맞으며, 블록 내부에서 쏜 레이는 그 블록을 통과한다.
        /// </summary>
        /// <param name="origin">레이 시작점.</param>
        /// <param name="direction">레이 방향 (정규화).</param>
        /// <param name="maxDist">최대 거리. 0 이하이면 무한.</param>
        /// <param name="dist">맞은 거리. 못 맞으면 0.</param>
        /// <returns>맞았으면 true.</returns>
        public static bool RaycastBlock(Vector3 origin, Vector3 direction, float maxDist, out float dist)
        {
            return RaycastBlock(origin, direction, maxDist, out dist, out _);
        }

        /// <summary>
        /// 레이가 처음 만나는 충돌 블록까지의 거리와 맞은 면의 법선을 구한다.
        /// </summary>
        /// <param name="origin">레이 시작점.</param>
        /// <param name="direction">레이 방향 (정규화).</param>
        /// <param name="maxDist">최대 거리. 0 이하이면 무한.</param>
        /// <param name="dist">맞은 거리. 못 맞으면 0.</param>
        /// <param name="normal">맞은 면의 바깥쪽 법선. 못 맞으면 (0, 0, 0).</param>
        /// <returns>맞았으면 true.</returns>
        public static bool RaycastBlock(Vector3 origin, Vector3 direction, float maxDist, out float dist, out Vector3 normal)
        {
            dist = 0f;
            normal = Vector3.Zero;
            if (!Loaded) return false;

            List<Block> colliders = Instance.m_blockColliders;
            float nearest = maxDist;
            bool found = false;

            for (int i = 0; i < colliders.Count; i++)
            {
                // nearest 를 최대 거리로 넘겨, 이미 찾은 것보다 먼 교차는 블록 쪽에서 걸러지게 한다.
                if (colliders[i].IntersectRay(origin, direction, nearest, out int face, out float hitDist))
                {
                    nearest = hitDist;
                    normal = colliders[i].faceNormals[face];
                    found = true;
                }
            }

            if (found)
            {
                dist = nearest;
            }
            return found;
        }

        /// <summary>
        /// 지정 좌표가 블록 하나라도의 내부에 있는지 검사한다 (원본 CheckALLBlockInside 대응).
        /// 충돌이 없는 판형 블록은 대상이 아니다(원본 BoardBlock 제외와 동일).
        /// </summary>
        /// <param name="point">검사할 월드 좌표.</param>
        /// <returns>내부이면 true. 맵이 로드돼 있지 않으면 false.</returns>
        public static bool IsInsideBlock(Vector3 point)
        {
            if (!Loaded) return false;

            List<Block> colliders = Instance.m_blockColliders;
            for (int i = 0; i < colliders.Count; i++)
            {
                // AABB 로 먼저 걸러 6면 판정 비용을 줄인다 (원본 collision.cpp 의 범위 프리컷 대응).
                if (!colliders[i].OverlapsAABB(point, point)) continue;
                if (colliders[i].Contains(point)) return true;
            }
            return false;
        }

        /// <summary>
        /// BD1 바이너리 파일을 파싱한다.
        /// </summary>
        /// <param name="filepath">BD1 파일 전체 경로.</param>
        /// <param name="texturePaths">텍스처 경로 10개 (BD1 파일 폴더 기준 상대 경로, 빈 문자열 가능).</param>
        /// <param name="rawBlocks">블록 원시 데이터.</param>
        /// <returns>파싱에 성공했으면 true.</returns>
        private static bool LoadBD1File(string filepath, out string[] texturePaths, out RawBlockData[] rawBlocks)
        {
            texturePaths = null;
            rawBlocks = null;

            try
            {
                using var reader = new BinaryReader(File.OpenRead(filepath));

                // 텍스처 경로 (10개 × 31바이트, null 종료)
                texturePaths = new string[k_textureSlotCount];
                for (int i = 0; i < k_textureSlotCount; i++)
                {
                    byte[] pathBytes = reader.ReadBytes(k_texturePathLength);
                    int length = 0;
                    while (length < pathBytes.Length && pathBytes[length] != 0) length++;
                    texturePaths[i] = Encoding.ASCII.GetString(pathBytes, 0, length);
                }

                // 블록 개수 (uint16 리틀 엔디안)
                int blockCount = reader.ReadUInt16();

                rawBlocks = new RawBlockData[blockCount];
                for (int b = 0; b < blockCount; b++)
                {
                    // 정점 좌표: X[8] → Y[8] → Z[8] 순서로 분리 저장됨
                    float[] xs = new float[8];
                    float[] ys = new float[8];
                    float[] zs = new float[8];
                    for (int i = 0; i < 8; i++) xs[i] = reader.ReadSingle();
                    for (int i = 0; i < 8; i++) ys[i] = reader.ReadSingle();
                    for (int i = 0; i < 8; i++) zs[i] = reader.ReadSingle();

                    var vertices = new Vector3[8];
                    for (int i = 0; i < 8; i++)
                    {
                        vertices[i] = Coord.FromXops(xs[i], ys[i], zs[i]);
                    }

                    // UV 좌표: U[24] → V[24] 순서 (6면 × 4개씩 분리 저장됨). 원본과 Godot 모두 V 원점이 위라 뒤집지 않는다.
                    float[] us = new float[24];
                    float[] vs = new float[24];
                    for (int i = 0; i < 24; i++) us[i] = reader.ReadSingle();
                    for (int i = 0; i < 24; i++) vs[i] = reader.ReadSingle();

                    var uvs = new Vector2[24];
                    for (int i = 0; i < 24; i++)
                    {
                        uvs[i] = new Vector2(us[i], vs[i]);
                    }

                    // 텍스처 인덱스 (6면 × int32)
                    int[] textureIndices = new int[6];
                    for (int i = 0; i < 6; i++)
                    {
                        textureIndices[i] = reader.ReadInt32();
                    }

                    rawBlocks[b] = new RawBlockData
                    {
                        vertices = vertices,
                        uvs = uvs,
                        textureIndices = textureIndices,
                        flag = reader.ReadInt32(),
                    };
                }

                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debugger.LogError($"BD1 read failed: {filepath}\n{e.Message}", nameof(MapLoader));
                return false;
            }
        }

        /// <summary>
        /// 블록 하나의 원시 데이터로 메시와 충돌 정보를 만든다.
        /// </summary>
        /// <param name="raw">블록 원시 데이터.</param>
        /// <param name="darkFlag">true면 다크 모드(미션 screenflag bit1) — 면 명도 오프셋 0.3, false면 0.5.</param>
        /// <param name="textureSlotCount">유효한 텍스처 슬롯 수. 이 범위를 벗어난 인덱스의 면은 투명벽으로 보고 그리지 않는다.</param>
        /// <returns>빌드된 블록.</returns>
        private static Block BuildBlock(RawBlockData raw, bool darkFlag, int textureSlotCount)
        {
            Vector3 center = Vector3.Zero;
            for (int i = 0; i < 8; i++) center += raw.vertices[i];
            center /= 8f;

            // --- 면 법선 (셰이딩 + 충돌 공용) ---
            // 원본의 COLLISION_ADDSIZE는 AABB fast-reject에만 쓰이고 면 중심은 실제 값을 그대로 쓴다. 확장 금지.
            var faceNormals = new Vector3[6];
            var faceCenters = new Vector3[6];
            for (int f = 0; f < 6; f++)
            {
                int[] fi = s_faceVertexIndices[f];
                Vector3 v0 = raw.vertices[fi[0]];
                Vector3 v1 = raw.vertices[fi[1]];
                Vector3 v2 = raw.vertices[fi[2]];
                Vector3 v3 = raw.vertices[fi[3]];

                // 원본 datafile.cpp:222-245: 두 삼각형 각각의 법선 계산 후 더 긴 쪽(비퇴화) 선택.
                // 경사 블록의 면이 비평면 quad여도 유효한 법선을 얻을 수 있다.
                // 원본(왼손 좌표계)과 거울상인 Godot(오른손)에서 같은 바깥 방향을 얻으려면 외적의 피연산자 순서가 반대여야 한다.
                Vector3 c1 = (v0 - v2).Cross(v3 - v2);
                Vector3 c2 = (v2 - v0).Cross(v1 - v0);

                // 원본과 동일하게 winding 으로 구한 법선을 그대로 쓴다. 중심 바깥쪽으로 강제 보정하면
                // 함몰/뒤집힌 형태의 통과벽(원본 BoardBlock) 판정이 깨져 통과 불가가 된다.
                faceNormals[f] = (c1.LengthSquared() > c2.LengthSquared() ? c1 : c2).Normalized();
                faceCenters[f] = (v0 + v1 + v2 + v3) * 0.25f;
            }

            // --- 면별 셰이딩 스칼라: shadow = ‖N + L‖ / 6 + offset (원본 datafile.cpp:253-265) ---
            float offset = darkFlag ? 0.3f : 0.5f;

            // --- 메시 빌드: 텍스처별 서피스, 면당 정점 분리 ---
            var surfaceTextures = new List<int>();
            var surfaceVertices = new List<List<Vector3>>();
            var surfaceNormals = new List<List<Vector3>>();
            var surfaceUVs = new List<List<Vector2>>();
            var surfaceColors = new List<List<Color>>();
            var surfaceIndices = new List<List<int>>();
            var meshMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var meshMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int f = 0; f < 6; f++)
            {
                int textureIndex = raw.textureIndices[f];
                if (textureIndex < 0 || textureIndex >= textureSlotCount)
                {
                    continue;
                }

                int surface = surfaceTextures.IndexOf(textureIndex);
                if (surface < 0)
                {
                    surface = surfaceTextures.Count;
                    surfaceTextures.Add(textureIndex);
                    surfaceVertices.Add(new List<Vector3>());
                    surfaceNormals.Add(new List<Vector3>());
                    surfaceUVs.Add(new List<Vector2>());
                    surfaceColors.Add(new List<Color>());
                    surfaceIndices.Add(new List<int>());
                }

                float shadow = (faceNormals[f] + s_lightDirection).Length() / 6f + offset;
                var faceColor = new Color(shadow, shadow, shadow, 1f);

                int vertexBase = surfaceVertices[surface].Count;
                int[] faceVerts = s_faceVertexIndices[f];
                for (int v = 0; v < 4; v++)
                {
                    Vector3 local = raw.vertices[faceVerts[v]] - center;
                    surfaceVertices[surface].Add(local);
                    surfaceNormals[surface].Add(faceNormals[f]);
                    surfaceUVs[surface].Add(raw.uvs[f * 4 + (v + 3) % 4]);
                    surfaceColors[surface].Add(faceColor);
                    meshMin = meshMin.Min(local);
                    meshMax = meshMax.Max(local);
                }

                surfaceIndices[surface].AddRange(new[]
                {
                    vertexBase, vertexBase + 1, vertexBase + 2,
                    vertexBase, vertexBase + 2, vertexBase + 3,
                });
            }

            ArrayMesh mesh = null;
            if (surfaceTextures.Count > 0)
            {
                mesh = new ArrayMesh();
                for (int s = 0; s < surfaceTextures.Count; s++)
                {
                    var arrays = new Godot.Collections.Array();
                    arrays.Resize((int)Mesh.ArrayType.Max);
                    arrays[(int)Mesh.ArrayType.Vertex] = surfaceVertices[s].ToArray();
                    arrays[(int)Mesh.ArrayType.Normal] = surfaceNormals[s].ToArray();
                    arrays[(int)Mesh.ArrayType.TexUV] = surfaceUVs[s].ToArray();
                    arrays[(int)Mesh.ArrayType.Color] = surfaceColors[s].ToArray();
                    arrays[(int)Mesh.ArrayType.Index] = surfaceIndices[s].ToArray();
                    mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                }

                // 평평한 블록(바닥/천장 등 한 축 두께 0)이 특정 카메라 각도에서 컬링돼 사라지지 않도록 최소 두께를 준다.
                Vector3 size = (meshMax - meshMin).Max(Vector3.One * k_minBoundsThickness);
                Vector3 middle = (meshMin + meshMax) * 0.5f;
                mesh.CustomAabb = new Aabb(middle - size * 0.5f, size);
            }

            bool isBoardBlock = HasDuplicateExpandedVertices(ExpandVertices(raw.vertices))
                              || IsCenterVisibleFromAnyFace(center, faceNormals, faceCenters);

            // 8정점 월드 AABB — 충돌 브로드페이즈 fast-reject 용. 원본 COLLISION_ADDSIZE 여유를 반영해 살짝 확장.
            Vector3 boundsMin = raw.vertices[0];
            Vector3 boundsMax = raw.vertices[0];
            for (int i = 1; i < 8; i++)
            {
                boundsMin = boundsMin.Min(raw.vertices[i]);
                boundsMax = boundsMax.Max(raw.vertices[i]);
            }

            return new Block
            {
                mesh = mesh,
                surfaceTextureIndices = surfaceTextures.ToArray(),
                position = center,
                collider = !isBoardBlock,
                faceNormals = faceNormals,
                faceCenters = faceCenters,
                boundsMin = boundsMin - Vector3.One * k_collisionAddSize,
                boundsMax = boundsMax + Vector3.One * k_collisionAddSize,
            };
        }

        /// <summary>
        /// 각 정점을 원점 기준 바깥으로 0.01 만큼 민 사본을 만든다. 판형 블록 판정(원본 조건 1)의 전처리.
        /// </summary>
        /// <param name="vertices">블록 8정점.</param>
        /// <returns>밀어낸 정점 배열.</returns>
        private static Vector3[] ExpandVertices(Vector3[] vertices)
        {
            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float r = vertices[i].Length() + 0.01f;
                result[i] = vertices[i].Normalized() * r;
            }
            return result;
        }

        private static bool HasDuplicateExpandedVertices(Vector3[] expanded)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = i + 1; j < 8; j++)
                {
                    // UnityXOPS 의 Vector3 == 와 같은 허용 오차(거리 1e-5)로 비교한다.
                    if ((expanded[i] - expanded[j]).LengthSquared() < 9.99999944e-11f) return true;
                }
            }
            return false;
        }

        private static bool IsCenterVisibleFromAnyFace(Vector3 center, Vector3[] normals, Vector3[] centers)
        {
            for (int i = 0; i < 6; i++)
            {
                if (normals[i].Dot(centers[i] - center) <= 0f) return true;
            }
            return false;
        }
    }
}
