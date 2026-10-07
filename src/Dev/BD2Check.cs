using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. 모든 미션의 BD1 을 BD2 로 바꿔 저장하고 다시 읽어, 두 형식의 블록·판정 결과가 같은지 대조한다.
    /// 이어서 블록 플래그(판정별 통과), 그리지 않는 면, 재질(착탄 이펙트·소리, 발소리), 깨진 파일을 확인한다. 변환한 파일은 build/bd2_check/ 에 쓰고 끝나면 지운다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/bd2_check.tscn
    /// 명령행 인자("--" 뒤): --convert 입력.bd1 출력.bd2 는 점검 대신 파일 하나를 변환하고 종료한다 (경로는 exe 폴더 기준. 텍스처 목록은 출력 옆에 _textures.json 으로 쓴다).
    /// </summary>
    public partial class BD2Check : Node
    {
        private const string k_workFolder = "build/bd2_check";
        private const int k_raysPerMap = 200;
        private const int k_pointsPerMap = 200;
        private const float k_rayStartHeight = 1000f;

        private static readonly BlockLayer[] s_layers = { BlockLayer.Human, BlockLayer.Bullet, BlockLayer.Sight };

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();

        /// <summary>
        /// 로드된 블록 상태를 대조용으로 떠 둔 것.
        /// </summary>
        private class Snapshot
        {
            public int blockCount;
            public int[] layerCounts;
            public int[] layerMasks;
            public int[] surfaceCounts;
            public Vector3[] normals;
            public Vector3[] boundsMin;
            public Vector2[] firstUVs;
            public string[] rays;
            public bool[] inside;
        }

        public override void _Ready()
        {
            string[] args = OS.GetCmdlineUserArgs();
            int convertArg = Array.IndexOf(args, "--convert");
            if (convertArg >= 0)
            {
                bool ok = convertArg + 2 < args.Length && Convert(args[convertArg + 1], args[convertArg + 2]);
                if (!ok) GD.Print("사용법: --convert 입력.bd1 출력.bd2 (경로는 exe 폴더 기준)");
                Finish(ok);
                return;
            }

            string workFolder = GamePath.Resolve(k_workFolder);
            Directory.CreateDirectory(workFolder);

            int maps = CheckAllMissions(workFolder);
            CheckFlags(workFolder);
            CheckMaterials(workFolder);
            CheckBrokenFiles(workFolder);

            Directory.Delete(workFolder, true);

            GD.Print($"BD2 점검 {m_checks}항목 (맵 {maps}개 대조) — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            Finish(m_problems.Count == 0);
        }

        /// <summary>
        /// 관리 객체를 정리하고 종료한다. 노드를 대량으로 만들고 지운 직후 종료하면 간헐적으로 죽는 것을 막는다.
        /// </summary>
        /// <param name="ok">true 면 종료 코드 0.</param>
        private void Finish(bool ok)
        {
            MapLoader.UnloadBlockData();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(ok ? 0 : 1);
        }

        /// <summary>
        /// 점검 한 건을 센다.
        /// </summary>
        /// <param name="ok">통과했으면 true.</param>
        /// <param name="what">실패했을 때 남길 설명.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// BD1 파일 하나를 BD2 와 텍스처 목록으로 바꿔 쓴다.
        /// </summary>
        /// <param name="input">BD1 경로 (exe 폴더 기준).</param>
        /// <param name="output">BD2 경로 (exe 폴더 기준).</param>
        /// <returns>성공했으면 true.</returns>
        private static bool Convert(string input, string output)
        {
            string inputPath = GamePath.Resolve(input);
            string outputPath = GamePath.Resolve(output);
            if (inputPath == null || outputPath == null) return false;

            string listRelative = Path.ChangeExtension(output, null).Replace('\\', '/') + "_textures.json";
            if (!MapLoader.ConvertBD1(inputPath, listRelative, out BD2File file, out BlockTextureListData textures))
            {
                GD.Print($"BD1 을 읽지 못했습니다: {input}");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            if (!file.Write(outputPath, out string error))
            {
                GD.Print($"BD2 를 쓰지 못했습니다: {error}");
                return false;
            }
            File.WriteAllText(GamePath.Resolve(listRelative), JsonData.ToJson(textures));

            GD.Print($"변환: {input} → {output} (블록 {file.blocks.Count}개), 텍스처 목록 {listRelative}");
            return true;
        }

        /// <summary>
        /// 모든 미션의 BD1 을 BD2 로 바꿔, 파일을 쓰고 읽은 결과와 로드한 뒤의 블록·판정이 같은지 본다.
        /// </summary>
        /// <param name="workFolder">변환한 파일을 쓸 폴더 전체 경로.</param>
        /// <returns>대조한 맵 수.</returns>
        private int CheckAllMissions(string workFolder)
        {
            MissionData missionData = DataManager.Instance.MissionData;
            var entries = new List<(bool mif, int page, int index, string label)>();
            for (int i = 0; i < missionData.officialMissions.Count; i++)
            {
                entries.Add((false, 0, i, missionData.officialMissions[i].name));
            }
            for (int page = 0; page < missionData.addonMissions.Count; page++)
            {
                for (int i = 0; i < missionData.addonMissions[page].Count; i++)
                {
                    entries.Add((true, page, i, $"addon {page}/{missionData.addonMissions[page][i].name}"));
                }
            }

            string bd2Path = Path.Combine(workFolder, "map.bd2");
            string copyPath = Path.Combine(workFolder, "copy.bd2");
            string listRelative = $"{k_workFolder}/map_textures.json";
            int maps = 0;

            foreach ((bool mif, int page, int index, string label) in entries)
            {
                if (!MapLoader.LoadMissionData(index, mif, page)) continue;

                string bd1Path = MapLoader.Instance.MissionBD1Path;
                if (!MapLoader.LoadBlockData(bd1Path))
                {
                    Expect(false, $"{label}: BD1 로드 실패");
                    continue;
                }
                Snapshot before = TakeSnapshot(index);

                if (!MapLoader.ConvertBD1(bd1Path, listRelative, out BD2File file, out BlockTextureListData textures)
                    || !file.Write(bd2Path, out _))
                {
                    Expect(false, $"{label}: BD2 변환 실패");
                    continue;
                }
                File.WriteAllText(GamePath.Resolve(listRelative), JsonData.ToJson(textures));

                // 쓴 파일을 읽어 다시 쓰면 바이트가 같아야 한다.
                bool reread = BD2File.Read(bd2Path, out BD2File copy, out _) && copy.Write(copyPath, out _);
                Expect(reread && copy.textureListPath == listRelative && copy.blocks.Count == file.blocks.Count
                    && File.ReadAllBytes(bd2Path).AsSpan().SequenceEqual(File.ReadAllBytes(copyPath)),
                    $"{label}: BD2 를 읽어 다시 쓴 파일이 다름");
                Expect(new FileInfo(bd2Path).Length == 8 + 4 + System.Text.Encoding.UTF8.GetByteCount(listRelative) + 4 + 340L * file.blocks.Count,
                    $"{label}: BD2 파일 크기가 레이아웃과 다름");

                if (!MapLoader.LoadBlockData(bd2Path))
                {
                    Expect(false, $"{label}: BD2 로드 실패");
                    continue;
                }
                Snapshot after = TakeSnapshot(index);

                string difference = Compare(before, after);
                Expect(difference == null, $"{label}: BD1 과 BD2 가 다름 — {difference}");
                maps++;
            }

            return maps;
        }

        /// <summary>
        /// 지금 로드된 블록의 모양과 판정 결과를 떠 둔다. 같은 씨앗으로 뽑은 레이와 점을 쓰므로 같은 맵이면 결과가 같아야 한다.
        /// </summary>
        /// <param name="seed">난수 씨앗.</param>
        /// <returns>떠 둔 상태.</returns>
        private static Snapshot TakeSnapshot(int seed)
        {
            IReadOnlyList<Block> blocks = MapLoader.Blocks;
            var snapshot = new Snapshot
            {
                blockCount = blocks.Count,
                layerCounts = new int[s_layers.Length],
                layerMasks = new int[blocks.Count],
                surfaceCounts = new int[blocks.Count],
                normals = new Vector3[blocks.Count * 6],
                boundsMin = new Vector3[blocks.Count],
                firstUVs = new Vector2[blocks.Count],
                rays = new string[k_raysPerMap * s_layers.Length],
                inside = new bool[k_pointsPerMap * s_layers.Length],
            };

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks[i];
                snapshot.layerMasks[i] = block.layerMask;
                snapshot.surfaceCounts[i] = block.mesh?.GetSurfaceCount() ?? 0;
                snapshot.boundsMin[i] = block.boundsMin;
                for (int f = 0; f < 6; f++) snapshot.normals[i * 6 + f] = block.faceNormals[f];
                if (block.mesh != null)
                {
                    var uvs = (Vector2[])block.mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.TexUV];
                    snapshot.firstUVs[i] = uvs[1];
                }
                min = min.Min(block.boundsMin);
                max = max.Max(block.boundsMax);
            }
            for (int l = 0; l < s_layers.Length; l++)
            {
                snapshot.layerCounts[l] = MapLoader.GetBlockColliders(s_layers[l]).Count;
            }

            var random = new Random(seed);
            for (int r = 0; r < k_raysPerMap; r++)
            {
                var origin = new Vector3(Lerp(min.X, max.X, random), Lerp(min.Y, max.Y, random), Lerp(min.Z, max.Z, random));
                var direction = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f);
                if (direction.LengthSquared() < 1e-6f) direction = Vector3.Down;
                direction = direction.Normalized();

                for (int l = 0; l < s_layers.Length; l++)
                {
                    bool hit = MapLoader.RaycastBlock(s_layers[l], origin, direction, 0f, out float dist, out Block block, out int face);
                    snapshot.rays[r * s_layers.Length + l] = hit ? $"{block.index}/{face}/{dist:R}" : "-";
                }
            }
            for (int p = 0; p < k_pointsPerMap; p++)
            {
                var point = new Vector3(Lerp(min.X, max.X, random), Lerp(min.Y, max.Y, random), Lerp(min.Z, max.Z, random));
                for (int l = 0; l < s_layers.Length; l++)
                {
                    snapshot.inside[p * s_layers.Length + l] = MapLoader.IsInsideBlock(s_layers[l], point);
                }
            }

            return snapshot;
        }

        private static float Lerp(float from, float to, Random random)
        {
            return from + (to - from) * (float)random.NextDouble();
        }

        /// <summary>
        /// 두 상태를 대조한다.
        /// </summary>
        /// <param name="a">BD1 에서 뜬 상태.</param>
        /// <param name="b">BD2 에서 뜬 상태.</param>
        /// <returns>처음 발견한 차이의 설명. 같으면 null.</returns>
        private static string Compare(Snapshot a, Snapshot b)
        {
            if (a.blockCount != b.blockCount) return $"블록 수 {a.blockCount} / {b.blockCount}";
            for (int l = 0; l < a.layerCounts.Length; l++)
            {
                if (a.layerCounts[l] != b.layerCounts[l]) return $"{s_layers[l]} 충돌 블록 수 {a.layerCounts[l]} / {b.layerCounts[l]}";
            }
            for (int i = 0; i < a.blockCount; i++)
            {
                if (a.layerMasks[i] != b.layerMasks[i]) return $"블록 {i} 의 판정 비트";
                if (a.surfaceCounts[i] != b.surfaceCounts[i]) return $"블록 {i} 의 서피스 수 {a.surfaceCounts[i]} / {b.surfaceCounts[i]}";
                if (a.boundsMin[i] != b.boundsMin[i]) return $"블록 {i} 의 범위";
                if (a.firstUVs[i] != b.firstUVs[i]) return $"블록 {i} 의 UV";
            }
            for (int i = 0; i < a.normals.Length; i++)
            {
                if (a.normals[i] != b.normals[i]) return $"블록 {i / 6} 면 {i % 6} 의 법선";
            }
            for (int i = 0; i < a.rays.Length; i++)
            {
                if (a.rays[i] != b.rays[i]) return $"레이 {i / s_layers.Length} ({s_layers[i % s_layers.Length]}): {a.rays[i]} / {b.rays[i]}";
            }
            for (int i = 0; i < a.inside.Length; i++)
            {
                if (a.inside[i] != b.inside[i]) return $"내부 판정 {i / s_layers.Length} ({s_layers[i % s_layers.Length]})";
            }
            return null;
        }

        /// <summary>
        /// 상자 블록 하나를 만든다. 정점과 면의 순서는 BD1 과 같다 (윗면 0-1-2-3, 아랫면 4-5-6-7).
        /// </summary>
        /// <param name="center">중심.</param>
        /// <param name="halfSize">각 축의 절반 크기.</param>
        /// <param name="flags">블록 플래그.</param>
        /// <returns>만든 블록. 텍스처와 재질 번호는 전부 0 이다.</returns>
        internal static BD2Block MakeBox(Vector3 center, Vector3 halfSize, int flags)
        {
            // 면 법선이 바깥을 향하는 정점 순서다 (점검이 위와 옆에서 쏜 레이로 확인한다).
            float x = halfSize.X, y = halfSize.Y, z = halfSize.Z;
            var block = new BD2Block { flags = flags };
            block.vertices[0] = center + new Vector3(-x, y, -z);
            block.vertices[1] = center + new Vector3(-x, y, z);
            block.vertices[2] = center + new Vector3(x, y, z);
            block.vertices[3] = center + new Vector3(x, y, -z);
            block.vertices[4] = center + new Vector3(-x, -y, -z);
            block.vertices[5] = center + new Vector3(-x, -y, z);
            block.vertices[6] = center + new Vector3(x, -y, z);
            block.vertices[7] = center + new Vector3(x, -y, -z);
            for (int f = 0; f < BD2Block.FaceCount; f++)
            {
                block.uvs[f * 4 + 1] = new Vector2(1f, 0f);
                block.uvs[f * 4 + 2] = new Vector2(1f, 1f);
                block.uvs[f * 4 + 3] = new Vector2(0f, 1f);
            }
            return block;
        }

        /// <summary>
        /// 직접 만든 BD2 로 블록 플래그(판정별 통과), 그리지 않는 면, 재질 번호, 텍스처 목록이 없을 때의 동작을 확인한다.
        /// 상자 넷을 X 축으로 늘어놓는다: 전부 충돌 / 사람만 통과 / 총알만 통과 / 시야만 통과.
        /// </summary>
        /// <param name="workFolder">파일을 쓸 폴더 전체 경로.</param>
        private void CheckFlags(string workFolder)
        {
            string bd2Path = Path.Combine(workFolder, "flags.bd2");
            string listRelative = $"{k_workFolder}/flags_textures.json";
            // 마지막 블록은 예약 비트가 켜져 있다. 판정은 아래 세 비트만 보므로 전부 충돌해야 한다.
            int[] flags = { 0, BD2File.PassHuman, BD2File.PassBullet, BD2File.PassSight, 0x7FFFFFF8 };

            var file = new BD2File { textureListPath = listRelative };
            for (int i = 0; i < flags.Length; i++)
            {
                file.blocks.Add(MakeBox(new Vector3(i * 10f, 0f, 0f), Vector3.One, flags[i]));
            }
            // 첫 블록: 면 하나는 그리지 않고, 면 하나는 목록에 없는 번호이고, 재질 번호를 면마다 다르게 준다.
            file.blocks[0].textureIndices[0] = -1;
            file.blocks[0].textureIndices[1] = 99;
            file.blocks[0].textureIndices[2] = 1;
            for (int f = 0; f < BD2Block.FaceCount; f++) file.blocks[0].materialIndices[f] = f == 0 ? -1 : 10000 + f;

            var textures = new BlockTextureListData();
            textures.blockTextureData.Add(new BlockTextureData());
            textures.blockTextureData.Add(new BlockTextureData());
            File.WriteAllText(GamePath.Resolve(listRelative), JsonData.ToJson(textures));

            if (!file.Write(bd2Path, out _) || !MapLoader.LoadBlockData(bd2Path))
            {
                Expect(false, "플래그 점검용 BD2 로드 실패");
                return;
            }

            IReadOnlyList<Block> blocks = MapLoader.Blocks;
            Expect(blocks.Count == flags.Length, "플래그 점검용 블록 수가 다름");

            for (int i = 0; i < flags.Length; i++)
            {
                var center = new Vector3(i * 10f, 0f, 0f);
                var above = new Vector3(i * 10f, k_rayStartHeight, 0f);
                for (int l = 0; l < s_layers.Length; l++)
                {
                    bool shouldCollide = (flags[i] & (1 << l)) == 0;
                    string name = $"플래그 0x{flags[i]:X}";
                    bool hit = MapLoader.RaycastBlock(s_layers[l], above, Vector3.Down, 0f, out float dist, out Block block, out int face);
                    bool hitOk = shouldCollide
                        ? hit && block.index == i && Mathf.Abs(dist - (k_rayStartHeight - 1f)) < 1e-3f && block.faceNormals[face].Y > 0.99f
                        : !hit;
                    Expect(hitOk, $"{name} 블록의 {s_layers[l]} 레이 결과가 다름");
                    Expect(MapLoader.IsInsideBlock(s_layers[l], center) == shouldCollide, $"{name} 블록의 {s_layers[l]} 내부 판정이 다름");
                }
            }

            // 상자가 바깥을 향한 법선을 갖는지 (감는 방향이 맞는지) 본다. 옆에서 쏜 레이도 맞아야 한다.
            Expect(MapLoader.RaycastBlock(BlockLayer.Human, new Vector3(-5f, 0f, 0f), Vector3.Right, 0f, out float sideDist)
                && Mathf.Abs(sideDist - 4f) < 1e-3f, "옆에서 쏜 레이가 상자에 맞지 않음");

            // 그리지 않는 면 둘(음수, 목록 밖)을 뺀 네 면이 텍스처 0 과 1 의 서피스 둘로 나뉜다.
            ArrayMesh mesh = blocks[0].mesh;
            int vertexCount = 0;
            for (int s = 0; mesh != null && s < mesh.GetSurfaceCount(); s++)
            {
                vertexCount += ((Vector3[])mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex]).Length;
            }
            Expect(mesh != null && mesh.GetSurfaceCount() == 2 && vertexCount == 16, "그리지 않는 면이 메시에서 빠지지 않음");
            Expect(blocks[1].mesh != null && blocks[1].mesh.GetSurfaceCount() == 1, "통과 블록이 그려지지 않음");

            int[] materials = blocks[0].faceMaterials;
            Expect(materials != null && materials[0] == -1 && materials[5] == 10005, "면 재질 번호가 블록에 전달되지 않음");

            // 텍스처 목록 파일이 없어도 블록과 판정은 로드된다. 면은 그릴 텍스처가 없어 전부 빠진다.
            file.textureListPath = $"{k_workFolder}/missing.json";
            bool loaded = file.Write(bd2Path, out _) && MapLoader.LoadBlockData(bd2Path);
            Expect(loaded && MapLoader.Blocks.Count == flags.Length && MapLoader.Blocks[0].mesh == null
                && MapLoader.GetBlockColliders(BlockLayer.Human).Count == 4, "텍스처 목록이 없을 때의 로드 결과가 다름");

            // BD1 에는 면 재질 번호가 없고, 모든 면이 0번 재질을 쓴다.
            Expect(MapLoader.LoadMissionData(0, false, 0) && MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path)
                && MapLoader.Blocks[0].faceMaterials == null
                && MapLoader.GetFaceMaterial(MapLoader.Blocks[0], 0) == DataManager.Instance.BlockMaterialParameterData.blockMaterialData[0],
                "BD1 블록의 재질이 0번이 아님");
        }

        /// <summary>
        /// 면 재질이 총알 착탄과 발소리에 쓰이는지, 총알이 통과하는 블록에서는 아무것도 나지 않는지 확인한다. 점검용 재질 둘을 재질 목록에 잠시 더했다가 뺀다.
        /// 블록 넷: 넓은 바닥(재질 1) / 총알 통과 상자(재질 2) / 기본 재질 상자(-1, 0번) / 소리가 들리지 않을 만큼 먼 상자(재질 1).
        /// </summary>
        /// <param name="workFolder">파일을 쓸 폴더 전체 경로.</param>
        private void CheckMaterials(string workFolder)
        {
            const string hitSound = "data/sound/hit1.wav";
            const string passSound = "data/sound/hit3.wav";
            const float bulletSpeed = 3f;

            List<BlockMaterialData> materials = DataManager.Instance.BlockMaterialParameterData.blockMaterialData;
            List<EffectData> effects = DataManager.Instance.EffectParameterData.effectData;
            int smoke = effects.FindIndex(effect => effect.name == "WallHitSmoke");
            int decal = effects.FindIndex(effect => effect.name == "WallBlood");
            int originalCount = materials.Count;
            if (originalCount != 1 || smoke < 0 || decal < 0)
            {
                Expect(false, "재질 점검의 전제(기본 재질 1개, WallHitSmoke·WallBlood 프리셋)가 맞지 않음");
                return;
            }

            materials.Add(new BlockMaterialData
            {
                name = "CheckSolid",
                hitEffect = smoke,
                bulletHoleEffect = decal,
                hitSounds = new List<string> { hitSound },
                footstepRun = new List<string> { hitSound },
                footstepLanding = new List<string> { passSound },
            });
            materials.Add(new BlockMaterialData { name = "CheckPass", hitEffect = smoke, hitSounds = new List<string> { passSound } });

            string bd2Path = Path.Combine(workFolder, "material.bd2");
            string listRelative = $"{k_workFolder}/material_textures.json";
            var file = new BD2File { textureListPath = listRelative };
            file.blocks.Add(MakeBox(new Vector3(0f, -1f, 0f), new Vector3(50f, 1f, 50f), 0));
            file.blocks.Add(MakeBox(new Vector3(0f, 10f, 0f), Vector3.One, BD2File.PassBullet));
            file.blocks.Add(MakeBox(new Vector3(80f, 0f, 0f), Vector3.One, 0));
            file.blocks.Add(MakeBox(new Vector3(200f, 0f, 0f), Vector3.One, 0));
            int[] blockMaterials = { 1, 2, -1, 1 };
            for (int b = 0; b < blockMaterials.Length; b++)
            {
                for (int f = 0; f < BD2Block.FaceCount; f++) file.blocks[b].materialIndices[f] = blockMaterials[b];
            }
            file.blocks[2].materialIndices[1] = 10005;

            var textures = new BlockTextureListData();
            textures.blockTextureData.Add(new BlockTextureData());
            File.WriteAllText(GamePath.Resolve(listRelative), JsonData.ToJson(textures));

            bool savedAI = AIController.Enabled;
            AIController.Enabled = false;

            if (!file.Write(bd2Path, out _) || !MapLoader.LoadMissionData(0, false, 0)
                || !MapLoader.LoadBlockData(bd2Path) || !MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path))
            {
                Expect(false, "재질 점검용 맵 로드 실패");
            }
            else
            {
                IReadOnlyList<Block> blocks = MapLoader.Blocks;
                BulletData bullet = DataManager.Instance.WeaponParameterData.bulletData[0];

                // 재질 번호가 재질로 바뀌는 규칙.
                Expect(MapLoader.GetFaceMaterial(blocks[0], 0) == materials[1], "면 재질 번호 1 이 재질 목록의 1번이 아님");
                Expect(MapLoader.GetFaceMaterial(blocks[2], 0) == materials[0], "면 재질 번호 -1 이 기본 재질(0번)이 아님");
                BlockMaterialData unknown = MapLoader.GetFaceMaterial(blocks[2], 1);
                Expect(unknown != null && !materials.Contains(unknown) && unknown.hitEffect == 0, "목록에 없는 재질 번호가 빈 재질이 아님");

                // 총알이 통과하는 블록을 지나 바닥에 맞는 탄: 통과 블록에서는 아무것도 나지 않고, 바닥에서 재질 1 의 소리와 연기·탄흔이 한 번 난다.
                int sounds = SoundManager.PlayCount;
                int particles = EffectManager.SpawnCount;
                var paths = new List<string>();
                var origin = new Vector3(0f, 14f, 0f);
                Bullet shot = BulletManager.Instance.Spawn(bullet, null, 0, 100, 0, origin, 0f, 90f, bulletSpeed, origin);
                for (int tick = 0; tick < 20 && shot != null && shot.IsActive; tick++)
                {
                    int before = SoundManager.PlayCount;
                    BulletManager.Instance.SimTick();
                    if (SoundManager.PlayCount != before) paths.Add(SoundManager.LastPlayedPath);
                }
                Expect(shot != null && !shot.IsActive, "통과 블록을 지난 탄이 바닥에서 사라지지 않음");
                Expect(SoundManager.PlayCount - sounds == 1 && paths.Count == 1 && paths[0] == hitSound,
                    $"통과 블록을 지난 탄의 소리가 다름 ({string.Join(", ", paths)})");
                int expectedParticles = CountParticles(effects[smoke]) + CountParticles(effects[decal]);
                Expect(EffectManager.SpawnCount - particles == expectedParticles,
                    $"재질 이펙트의 입자 수 {EffectManager.SpawnCount - particles} (기대 {expectedParticles}: 연기 하나와 탄흔 하나)");

                // 기본 재질(0번) 면에 맞은 탄: 원본의 착탄 연기와 착탄음이 난다. 탄흔은 없다.
                sounds = SoundManager.PlayCount;
                particles = EffectManager.SpawnCount;
                origin = new Vector3(80f, 4.1f, 0f);
                shot = BulletManager.Instance.Spawn(bullet, null, 0, 100, 0, origin, 0f, 90f, bulletSpeed, origin);
                for (int tick = 0; tick < 20 && shot != null && shot.IsActive; tick++) BulletManager.Instance.SimTick();
                Expect(shot != null && !shot.IsActive && SoundManager.PlayCount == sounds + 1 && materials[0].hitSounds.Contains(SoundManager.LastPlayedPath)
                    && EffectManager.SpawnCount - particles == CountParticles(effects[materials[0].hitEffect]),
                    $"기본 재질(0번) 면의 착탄이 다름 (소리 {SoundManager.PlayCount - sounds} {SoundManager.LastPlayedPath}, 입자 {EffectManager.SpawnCount - particles})");

                Human runner = MapLoader.Player;
                if (runner == null)
                {
                    Expect(false, "발소리 점검용 사람이 없음");
                }
                else
                {
                    // 직접 호출: 달리기와 착지는 목록의 소리가 나고, 목록이 빈 걷기와 점프는 나지 않는다.
                    runner.Controller.Teleport(new Vector3(0f, 0f, 0f));
                    sounds = SoundManager.PlayCount;
                    WorldSound.PlayFootstep(runner, FootstepKind.Forward);
                    Expect(SoundManager.PlayCount == sounds + 1 && SoundManager.LastPlayedPath == hitSound, "달리기 발소리가 재질의 소리가 아님");
                    WorldSound.PlayFootstep(runner, FootstepKind.Landing);
                    Expect(SoundManager.PlayCount == sounds + 2 && SoundManager.LastPlayedPath == passSound, "착지 발소리가 재질의 소리가 아님");
                    WorldSound.PlayFootstep(runner, FootstepKind.Walk);
                    WorldSound.PlayFootstep(runner, FootstepKind.Jump);
                    Expect(SoundManager.PlayCount == sounds + 2, "목록이 빈 걷기나 점프에서 발소리가 남");

                    // 기본 재질 위, 들리지 않는 거리, 공중에서는 나지 않는다.
                    runner.Controller.Teleport(new Vector3(80f, 1f, 0f));
                    WorldSound.PlayFootstep(runner, FootstepKind.Forward);
                    runner.Controller.Teleport(new Vector3(200f, 1f, 0f));
                    WorldSound.PlayFootstep(runner, FootstepKind.Forward);
                    runner.Controller.Teleport(new Vector3(0f, 5f, 0f));
                    WorldSound.PlayFootstep(runner, FootstepKind.Forward);
                    Expect(SoundManager.PlayCount == sounds + 2, "발소리가 없는 기본 재질 위, 먼 거리, 공중 중 하나에서 발소리가 남");

                    // 박자: 달리기 전진은 한 사이클 0.72 초에 두 걸음 = 12틱에 한 번. 120틱을 달리면 10번이다.
                    // 다른 사람들은 바닥에 떨어지며 착지 소리를 내지 않게 들리지 않는 곳으로 보낸다.
                    foreach (Human other in MapLoader.Humans)
                    {
                        if (other != runner) other.Controller.Teleport(new Vector3(1000f, 500f, 1000f));
                    }
                    runner.Controller.Teleport(new Vector3(0f, 0.01f, 0f));
                    for (int tick = 0; tick < 10; tick++) SimClock.Step();
                    sounds = SoundManager.PlayCount;
                    for (int tick = 0; tick < 120; tick++)
                    {
                        runner.Controller.SetInput(new HumanInput { moveFlag = HumanMoveFlag.Forward, yaw = 0f, pitch = 0f });
                        SimClock.Step();
                    }
                    int steps = SoundManager.PlayCount - sounds;
                    Expect(steps == 10 && SoundManager.LastPlayedPath == hitSound, $"120틱 달리기의 발소리 {steps}번 (기대 10번)");
                }
            }

            MapLoader.UnloadPointData();
            AIController.Enabled = savedAI;
            materials.RemoveRange(originalCount, materials.Count - originalCount);
        }

        /// <summary>
        /// 이펙트 프리셋 하나가 한 번에 내는 입자 수를 센다. 개수가 고정인 프리셋에만 쓴다.
        /// </summary>
        /// <param name="effect">프리셋.</param>
        /// <returns>입자 수.</returns>
        private static int CountParticles(EffectData effect)
        {
            int count = 0;
            foreach (EffectEmitter emitter in effect.emitters) count += emitter.spawnCount;
            return count;
        }

        /// <summary>
        /// 깨진 파일을 읽지 않는지 확인한다.
        /// </summary>
        /// <param name="workFolder">파일을 쓸 폴더 전체 경로.</param>
        private void CheckBrokenFiles(string workFolder)
        {
            string goodPath = Path.Combine(workFolder, "good.bd2");
            string badPath = Path.Combine(workFolder, "bad.bd2");

            var file = new BD2File { textureListPath = "a.json" };
            file.blocks.Add(MakeBox(Vector3.Zero, Vector3.One, 0));
            file.Write(goodPath, out _);
            byte[] bytes = File.ReadAllBytes(goodPath);

            byte[] wrongMagic = (byte[])bytes.Clone();
            wrongMagic[0] = (byte)'X';
            File.WriteAllBytes(badPath, wrongMagic);
            Expect(!BD2File.Read(badPath, out _, out string magicError) && magicError != null, "매직이 다른 파일을 읽음");

            File.WriteAllBytes(badPath, bytes.AsSpan(0, bytes.Length - 10).ToArray());
            Expect(!BD2File.Read(badPath, out _, out _), "블록이 잘린 파일을 읽음");

            File.WriteAllBytes(badPath, bytes.AsSpan(0, 10).ToArray());
            Expect(!BD2File.Read(badPath, out _, out _), "머리말이 잘린 파일을 읽음");

            Expect(!MapLoader.LoadBlockData(badPath) && MapLoader.Blocks.Count == 0, "깨진 BD2 를 로드함");
            Expect(BD2File.Read(goodPath, out BD2File good, out _) && good.blocks.Count == 1 && good.textureListPath == "a.json", "정상 파일을 읽지 못함");
        }
    }
}
