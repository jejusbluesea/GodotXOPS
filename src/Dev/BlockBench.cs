using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 측정 씬 스크립트. 블록 수가 늘 때 시뮬레이션 틱과 충돌 조회가 얼마나 느려지는지 잰다.
    /// 사람이 가장 많은 공식 미션의 블록에 상자 블록을 더 얹은 BD2 를 만들어 로드하고, AI 와 이벤트를 켠 채 틱을 돌려 한 틱에 걸리는 시간을 잰다.
    /// 더 얹는 블록은 원래 맵 주변에 넓게 깔린다 (큰 맵에서 대부분의 블록이 조회 지점에서 먼 상황).
    /// 점검이 아니라 측정이라 항상 종료 코드 0 이다. 파일은 build/block_bench/ 에 쓰고 끝나면 지운다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/block_bench.tscn
    /// 명령행 인자("--" 뒤): --ticks 수 (기본 200), --mission 번호 (기본: 사람이 가장 많은 미션).
    /// </summary>
    public partial class BlockBench : Node
    {
        private const string k_workFolder = "build/block_bench";
        private const float k_tickBudgetMs = 30f;
        // 더 얹는 블록의 간격과 크기 (m).
        private const float k_extraSpacing = 6f;
        private const int k_rayCount = 20000;
        private const int k_pointCount = 200000;
        private const float k_rayLength = 40f;

        private static readonly int[] s_extraCounts = { 0, 500, 2000, 8000, 30000 };

        public override void _Ready()
        {
            string[] args = OS.GetCmdlineUserArgs();
            int ticks = ReadInt(args, "--ticks", 200);
            int mission = ReadInt(args, "--mission", -1);
            if (mission < 0) mission = FindBusiestMission();

            Directory.CreateDirectory(GamePath.Resolve(k_workFolder));
            MapLoader.LoadMissionData(mission, false, 0);
            MapLoader loader = MapLoader.Instance;
            string name = loader.MissionName;
            string bd1Path = loader.MissionBD1Path;
            string pd1Path = loader.MissionPD1Path;

            GD.Print($"미션 {mission} {name}, 틱 {ticks}회. 한 틱의 예산은 {k_tickBudgetMs:0} ms (33.3 Hz).");
            GD.Print("블록 수 | 사람 | 틱 평균 ms | 틱 최대 ms | 레이 1회 us | 내부 판정 1회 us");

            foreach (int extra in s_extraCounts)
            {
                string bd2Relative = $"{k_workFolder}/bench.bd2";
                if (!MapLoader.ConvertBD1(bd1Path, $"{k_workFolder}/bench_textures.json", out BD2File file, out BlockTextureListData textures)) break;
                AddExtraBlocks(file, extra);
                File.WriteAllText(GamePath.Resolve(file.textureListPath), JsonData.ToJson(textures));
                file.Write(GamePath.Resolve(bd2Relative), out _);

                GameRandom.Reseed(1u);
                if (!MapLoader.LoadBlockData(GamePath.Resolve(bd2Relative)) || !MapLoader.LoadPointData(pd1Path)) break;

                AIController.Enabled = true;
                AIController.DrivePlayer = true;
                EventManager.Instance.BeginMission();

                var watch = new Stopwatch();
                double total = 0.0;
                double worst = 0.0;
                for (int tick = 0; tick < ticks; tick++)
                {
                    watch.Restart();
                    SimClock.Step();
                    double ms = watch.Elapsed.TotalMilliseconds;
                    total += ms;
                    worst = Math.Max(worst, ms);
                }

                (double rayMicro, double pointMicro) = MeasureQueries();
                GD.Print($"{MapLoader.Blocks.Count,7} | {MapLoader.HumanCount,4} | {total / ticks,10:0.000} | {worst,10:0.000} | {rayMicro,11:0.00} | {pointMicro,16:0.000}");

                MapLoader.UnloadPointData();
                MapLoader.UnloadBlockData();
            }

            MapLoader.UnloadMissionData();
            Directory.Delete(GamePath.Resolve(k_workFolder), true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(0);
        }

        private static int ReadInt(string[] args, string name, int fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int value) ? value : fallback;
        }

        /// <summary>
        /// 사람이 가장 많은 공식 미션을 찾는다.
        /// </summary>
        /// <returns>미션 번호.</returns>
        private static int FindBusiestMission()
        {
            int best = 0;
            int most = -1;
            int count = DataManager.Instance.MissionData.officialMissions.Count;
            for (int index = 0; index < count; index++)
            {
                if (!MapLoader.LoadMissionData(index, false, 0) || !MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path)) continue;
                if (MapLoader.HumanCount > most)
                {
                    most = MapLoader.HumanCount;
                    best = index;
                }
                MapLoader.UnloadPointData();
            }
            return best;
        }

        /// <summary>
        /// 상자 블록을 원래 맵 둘레에 격자로 더 얹는다. 원래 맵의 범위 밖 한쪽에 정사각형으로 깔아서 원래 맵의 진행을 바꾸지 않는다.
        /// </summary>
        /// <param name="file">블록을 더할 BD2.</param>
        /// <param name="count">더할 블록 수.</param>
        private static void AddExtraBlocks(BD2File file, int count)
        {
            if (count <= 0) return;

            float maxX = float.MinValue;
            foreach (BD2Block block in file.blocks)
            {
                foreach (Vector3 vertex in block.vertices) maxX = Mathf.Max(maxX, vertex.X);
            }

            int side = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                var center = new Vector3(maxX + 20f + (i % side) * k_extraSpacing, 0f, (i / side - side * 0.5f) * k_extraSpacing);
                file.blocks.Add(BD2Check.MakeBox(center, new Vector3(2f, 1.5f, 2f), 0));
            }
        }

        /// <summary>
        /// 레이와 내부 판정 한 번에 걸리는 시간을 잰다. 조회 지점은 원래 맵의 사람들 주변이다 (실제 게임의 조회와 같은 자리).
        /// </summary>
        /// <returns>레이 1회와 내부 판정 1회의 시간 (마이크로초).</returns>
        private static (double ray, double point) MeasureQueries()
        {
            IReadOnlyList<Human> humans = MapLoader.Humans;
            if (humans.Count == 0) return (0.0, 0.0);

            var random = new Random(1);
            var origins = new Vector3[256];
            var directions = new Vector3[256];
            for (int i = 0; i < origins.Length; i++)
            {
                origins[i] = humans[random.Next(humans.Count)].Controller.Position + Vector3.Up * 1.5f;
                var direction = new Vector3((float)random.NextDouble() - 0.5f, ((float)random.NextDouble() - 0.5f) * 0.3f, (float)random.NextDouble() - 0.5f);
                directions[i] = direction.LengthSquared() > 1e-6f ? direction.Normalized() : Vector3.Forward;
            }

            int hits = 0;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < k_rayCount; i++)
            {
                int index = i & 255;
                if (MapLoader.RaycastBlock(BlockLayer.Sight, origins[index], directions[index], k_rayLength, out _)) hits++;
            }
            double rayMicro = watch.Elapsed.TotalMilliseconds * 1000.0 / k_rayCount;

            watch.Restart();
            for (int i = 0; i < k_pointCount; i++)
            {
                int index = i & 255;
                if (MapLoader.IsInsideBlock(BlockLayer.Bullet, origins[index] + directions[index] * (i % 40))) hits++;
            }
            double pointMicro = watch.Elapsed.TotalMilliseconds * 1000.0 / k_pointCount;

            GC.KeepAlive(hits);
            return (rayMicro, pointMicro);
        }
    }
}
