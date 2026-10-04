using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// PD1 파일에서 파싱된 포인트 하나. position 은 Godot 좌표, look 은 UnityXOPS 규약 yaw(도)다.
    /// param0~3 은 원본의 P1~P4 에 해당한다 (param0 = 종류, param3 = 식별번호).
    /// </summary>
    public class RawPointData
    {
        public Vector3 position;
        public float look;
        public int param0;
        public int param1;
        public int param2;
        public int param3;
    }

    public partial class MapLoader
    {
        // 포인트 종류(param0).
        public const int PointHuman = 1;
        public const int PointWeapon = 2;
        public const int PointAIPath = 3;
        public const int PointHumanInfo = 4;
        public const int PointSmallObject = 5;
        public const int PointHuman2 = 6;
        public const int PointRandomWeapon = 7;
        public const int PointRandomAIPath = 8;
        public const int PointEventFirst = 10;
        public const int PointEventLast = 19;

        private const int k_maxParameterCount = 20;
        // 한 맵에 둘 수 있는 사람 수 (원본 MAX_HUMAN). 치트로 사람을 추가할 때의 상한이다.
        private const int k_maxHumans = 96;
        // 복제한 사람을 놓는 자리: 원본 사람의 정면 1 m, 위로 0.5 m (원본 gamemain.cpp:2436-2438 — 10.0, 5.0).
        private const float k_cloneForwardOffset = 1.0f;
        private const float k_cloneHeightOffset = 0.5f;

        private Node3D m_humanRoot;
        private Node3D m_objectRoot;
        private readonly List<Human> m_humans = new List<Human>();
        private readonly List<SmallObject> m_smallObjects = new List<SmallObject>();
        private readonly MissionStats m_stats = new MissionStats();
        private readonly List<string> m_messages = new List<string>();
        private readonly Dictionary<string, ShaderMaterial> m_entityMaterialCache = new Dictionary<string, ShaderMaterial>();
        private readonly HumanCollision m_humanCollision = new HumanCollision();
        private readonly AIController m_aiController = new AIController();
        private ShaderMaterial m_untexturedMaterial;
        private Human m_player;

        // 종류(param0)별 → 식별번호(param3)별 포인트 목록. 파일 순서를 유지한다.
        private List<Dictionary<int, List<RawPointData>>> m_sortedRawPointData;

        public static Human Player => Instance.m_player;
        // 스폰된 전체 Human 목록 (스폰 순서).
        public static IReadOnlyList<Human> Humans => Instance.m_humans;
        public static int HumanCount => Instance.m_humans.Count;
        // 스폰된 전체 소물 목록 (스폰 순서). 부서진 것도 남아 있다.
        public static IReadOnlyList<SmallObject> SmallObjects => Instance.m_smallObjects;
        // 플레이어의 미션 통계.
        public static MissionStats Stats => Instance.m_stats;
        public static int MessageCount => Instance.m_messages.Count;

        // m_humans 내 플레이어 인덱스. 플레이어가 없으면 -1.
        public static int PlayerIndex => Instance.m_player != null ? Instance.m_humans.IndexOf(Instance.m_player) : -1;

        /// <summary>
        /// PD1 파일을 읽어 포인트를 정리하고 사람·무기·소물을 스폰한다. 이전에 로드된 것은 먼저 제거한다.
        /// 같은 이름의 .msg 파일이 있으면 메시지도 읽는다.
        /// </summary>
        /// <param name="filepath">PD1 파일 전체 경로.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        public static bool LoadPointData(string filepath)
        {
            UnloadPointData();

            if (string.IsNullOrEmpty(filepath))
            {
                Debugger.LogError("PD1 path is empty.", nameof(MapLoader));
                return false;
            }

            if (!File.Exists(filepath))
            {
                Debugger.LogError($"PD1 file not exists: {filepath}", nameof(MapLoader));
                return false;
            }

            if (!LoadPD1File(filepath, out RawPointData[] points))
            {
                return false;
            }

            MapLoader loader = Instance;

            loader.m_sortedRawPointData = new List<Dictionary<int, List<RawPointData>>>();
            for (int i = 0; i < k_maxParameterCount; i++)
            {
                loader.m_sortedRawPointData.Add(new Dictionary<int, List<RawPointData>>());
            }
            foreach (RawPointData raw in points)
            {
                if (raw.param0 < 0 || raw.param0 >= k_maxParameterCount) continue;

                Dictionary<int, List<RawPointData>> byId = loader.m_sortedRawPointData[raw.param0];
                if (!byId.TryGetValue(raw.param3, out List<RawPointData> list))
                {
                    list = new List<RawPointData>();
                    byId[raw.param3] = list;
                }
                list.Add(raw);
            }

            // 원본 LoadPointData 는 파일 순서대로 순회하며 HUMAN/HUMAN2 를 스폰한다. 인간 정보(HUMANINFO)는 param1 로 찾고 첫 매치를 쓴다.
            foreach (RawPointData raw in points)
            {
                if (raw.param0 != PointHuman && raw.param0 != PointHuman2) continue;

                RawPointData info = GetPoint(PointHumanInfo, raw.param1);
                if (info == null) continue;

                var human = new Human { Name = $"Human_{loader.m_humans.Count}" };
                loader.m_humanRoot.AddChild(human);
                human.CreateHuman(raw, info);
                loader.m_humans.Add(human);

                // 식별번호 0 인 사람이 플레이어다. 여럿이면 파일에서 마지막 것이 이긴다 (원본 AddHumanIndex).
                if (raw.param3 == 0) loader.m_player = human;
            }

            // 식별번호 0 이 하나도 없으면 첫 번째로 스폰된 사람이 플레이어다 (원본 Player_HumanID 초기값 0).
            if (loader.m_player == null && loader.m_humans.Count > 0)
            {
                loader.m_player = loader.m_humans[0];
            }

            string msgPath = Path.ChangeExtension(filepath, ".msg");
            if (File.Exists(msgPath))
            {
                loader.m_messages.AddRange(EncodingHelper.ReadAllLines(msgPath));
            }

            SpawnWeapons(points);
            SpawnSmallObjects(points);

            loader.m_stats.Reset();
            SimClock.Register(loader.m_humanCollision);
            SimClock.Register(loader.m_aiController);
            SimClock.Register(loader.m_stats);
            return true;
        }

        /// <summary>
        /// 무기 포인트에 떨어진 무기를 놓는다. 원본 ObjectManager::AddWeaponIndex (objectmanager.cpp:367-414).
        /// 일반 무기(종류 2): param1 = 무기 번호, param2 = 전체 탄 수.
        /// 랜덤 무기(종류 7): param1 과 param2 중 하나를 반반 확률로 고르고, 탄 수는 장탄수 × 초기 탄약 배수다.
        /// </summary>
        /// <param name="points">파일 순서대로의 포인트.</param>
        private static void SpawnWeapons(RawPointData[] points)
        {
            if (!WeaponManager.Loaded) return;

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            foreach (RawPointData raw in points)
            {
                if (raw.param0 != PointWeapon && raw.param0 != PointRandomWeapon) continue;

                int weaponIndex = raw.param1;
                int totalBullets = raw.param2;
                if (raw.param0 == PointRandomWeapon)
                {
                    weaponIndex = GameRandom.Gameplay.Range(0, 2) == 0 ? raw.param1 : raw.param2;
                    if (weaponIndex < 0 || weaponIndex >= parameter.weaponData.Count) continue;
                    totalBullets = parameter.weaponData[weaponIndex].magazineSize * Weapon.DefaultAutoBulletMultiplier;
                }

                if (weaponIndex < 0 || weaponIndex >= parameter.weaponData.Count) continue;
                if (weaponIndex == parameter.weaponGeneralData.noneWeaponIndex) continue;

                // 전체 탄 수를 탄창과 예비로 나눈다 (원본은 탄창 0 으로 놓고 RunReload 를 한 번 부른다).
                int magazineSize = parameter.weaponData[weaponIndex].magazineSize;
                int magazine = Mathf.Min(totalBullets, magazineSize);
                int reserve = Mathf.Max(0, totalBullets - magazineSize);

                WeaponManager.Instance.Spawn(weaponIndex, magazine, reserve, raw.position, raw.look, Vector3.Zero);
            }
        }

        /// <summary>
        /// 소물 포인트(종류 5)에 소물을 놓는다. 원본 ObjectManager::AddSmallObjectIndex (objectmanager.cpp:459-484).
        /// param1 = 소물 번호, param2 가 0 이 아니면 바닥에 붙인다, param3 = 식별번호.
        /// </summary>
        /// <param name="points">파일 순서대로의 포인트.</param>
        private static void SpawnSmallObjects(RawPointData[] points)
        {
            MapLoader loader = Instance;

            // 어드온 미션 전용 추가 사물의 자리를 먼저 채운다. 추가 사물이 없는 미션이면 자리를 비운다.
            InitializeAddonObject();

            ObjectParameterData parameter = DataManager.Instance.ObjectParameterData;
            foreach (RawPointData raw in points)
            {
                if (raw.param0 != PointSmallObject) continue;
                if (raw.param1 < 0 || raw.param1 >= parameter.objectData.Count) continue;

                var smallObject = new SmallObject { Name = $"Object_{loader.m_smallObjects.Count}" };
                loader.m_objectRoot.AddChild(smallObject);
                // look 은 사람 기준 yaw 다 (원본 방향 + 180°). 원본은 사람만 방향에 π 를 더해 그리고 (object.cpp:2158) 소물은 그대로 그리므로 (object.cpp:2765) 도로 뺀다.
                smallObject.CreateObject(raw.param1, raw.param3, raw.position, raw.look - 180f);
                if (raw.param2 != 0) smallObject.SnapToGround();
                loader.m_smallObjects.Add(smallObject);
            }
        }

        /// <summary>
        /// 식별번호가 일치하는 첫 번째 소물을 찾는다. 원본 ObjectManager::SearchSmallobject 대응.
        /// </summary>
        /// <param name="identifier">식별번호.</param>
        /// <returns>첫 매치. 없으면 null.</returns>
        public static SmallObject SearchSmallObject(int identifier)
        {
            return Instance.m_smallObjects.Find(smallObject => smallObject.Identifier == identifier);
        }

        /// <summary>
        /// 발사 통계를 기록한다. 쏜 사람이 플레이어일 때만 센다.
        /// </summary>
        /// <param name="shooter">쏜 사람.</param>
        public static void RecordFire(Human shooter)
        {
            if (shooter != null && shooter == Instance.m_player) Instance.m_stats.Fire++;
        }

        /// <summary>
        /// 명중 통계를 기록한다. 쏜 사람이 플레이어일 때만 센다.
        /// </summary>
        /// <param name="shooter">쏜 사람.</param>
        /// <param name="headshot">머리에 맞았으면 true.</param>
        /// <param name="weight">명중 가중치 (단발 1, 산탄은 2 / 탄환 수).</param>
        public static void RecordHit(Human shooter, bool headshot, float weight)
        {
            if (shooter == null || shooter != Instance.m_player) return;

            Instance.m_stats.OnTarget += weight;
            if (headshot) Instance.m_stats.Headshot++;
        }

        /// <summary>
        /// 킬 통계를 기록한다. 쏜 사람이 플레이어일 때만 센다.
        /// </summary>
        /// <param name="shooter">쏜 사람.</param>
        public static void RecordKill(Human shooter)
        {
            if (shooter != null && shooter == Instance.m_player) Instance.m_stats.Kill++;
        }

        /// <summary>
        /// 로드된 포인트, 스폰된 사람·무기·소물, 날아가는 탄환, 이펙트, 소리, 메시지를 모두 제거한다.
        /// </summary>
        public static void UnloadPointData()
        {
            MapLoader loader = Instance;

            SimClock.Unregister(loader.m_humanCollision);
            SimClock.Unregister(loader.m_aiController);
            SimClock.Unregister(loader.m_stats);
            if (EventManager.Loaded) EventManager.Instance.StopMission();
            if (BulletManager.Loaded) BulletManager.Instance.Clear();
            if (WeaponManager.Loaded) WeaponManager.Instance.Clear();
            if (EffectManager.Loaded) EffectManager.Instance.Clear();
            if (SoundManager.Loaded) SoundManager.Instance.Clear();

            loader.m_smallObjects.Clear();
            foreach (Node child in loader.m_objectRoot.GetChildren())
            {
                loader.m_objectRoot.RemoveChild(child);
                child.Free();
            }

            loader.m_player = null;
            loader.m_humans.Clear();
            foreach (Node child in loader.m_humanRoot.GetChildren())
            {
                loader.m_humanRoot.RemoveChild(child);
                child.Free();
            }

            loader.m_messages.Clear();
            loader.m_entityMaterialCache.Clear();
            loader.m_sortedRawPointData = null;
        }

        /// <summary>
        /// 종류(param0)와 식별번호(param3)로 포인트의 첫 매치를 조회한다.
        /// </summary>
        /// <param name="category">포인트 종류.</param>
        /// <param name="id">식별번호.</param>
        /// <returns>첫 매치. 없거나 맵이 로드돼 있지 않으면 null.</returns>
        public static RawPointData GetPoint(int category, int id)
        {
            List<Dictionary<int, List<RawPointData>>> sorted = Instance.m_sortedRawPointData;
            if (sorted == null || category < 0 || category >= sorted.Count) return null;

            return sorted[category].TryGetValue(id, out List<RawPointData> list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// AI 경로 포인트를 식별번호로 조회한다. 경로(AIPATH)에서 먼저 찾고, 없으면 랜덤 분기(RAND_AIPATH)에서 찾는다.
        /// 원본 SearchPointdata 는 종류를 가리지 않고 같은 번호의 첫 포인트를 찾아서, 번호가 같은 다른 종류의 포인트가 파일 앞쪽에 있으면 경로가 끊긴다.
        /// 그 동작은 따르지 않고 종류별로 찾는다.
        /// </summary>
        /// <param name="id">식별번호.</param>
        /// <returns>첫 매치. 없으면 null (경로 끝).</returns>
        public static RawPointData GetPathPoint(int id)
        {
            return GetPoint(PointAIPath, id) ?? GetPoint(PointRandomAIPath, id);
        }

        /// <summary>
        /// 이벤트 포인트를 식별번호로 조회한다. 이벤트 종류(10~19) 전체에서 찾는다. 경로와 마찬가지로 다른 종류의 포인트는 보지 않는다.
        /// </summary>
        /// <param name="id">식별번호.</param>
        /// <returns>첫 매치. 없으면 null (이벤트 줄 끝).</returns>
        public static RawPointData GetEventPoint(int id)
        {
            for (int type = PointEventFirst; type <= PointEventLast; type++)
            {
                RawPointData point = GetPoint(type, id);
                if (point != null) return point;
            }
            return null;
        }

        /// <summary>
        /// 스폰 순서 인덱스로 Human 을 조회한다.
        /// </summary>
        /// <param name="index">스폰 순서 인덱스.</param>
        /// <returns>해당 Human. 범위 밖이면 null.</returns>
        public static Human GetHuman(int index)
        {
            List<Human> humans = Instance.m_humans;
            return index >= 0 && index < humans.Count ? humans[index] : null;
        }

        /// <summary>
        /// 식별번호가 일치하는 첫 번째 Human 을 찾는다. 원본 ObjectManager::SearchHuman 대응.
        /// </summary>
        /// <param name="identifier">식별번호.</param>
        /// <returns>첫 매치. 없으면 null.</returns>
        public static Human SearchHuman(int identifier)
        {
            return Instance.m_humans.Find(human => human.Identifier == identifier);
        }

        /// <summary>
        /// 조작 대상(플레이어)을 바꾼다. 이벤트/경로/스폰 데이터는 건드리지 않는다 (치트 F8).
        /// </summary>
        /// <param name="human">새 플레이어. null 이면 무시.</param>
        public static void SetPlayer(Human human)
        {
            if (human != null) Instance.m_player = human;
        }

        /// <summary>
        /// 치트(F9) — 사람 하나를 복제해 그 앞에 세운다. 원본 gamemain.cpp:2411-2455: 종류·팀·무기 종류를 그대로 쓰고 탄약은 새로 채운다.
        /// 복제된 사람의 식별번호는 0 이고 경로가 없다. AI 는 호출한 쪽이 AIBrain.SetHoldTracking / SetHoldWait 로 정한다.
        /// </summary>
        /// <param name="source">복제할 사람.</param>
        /// <returns>새로 만든 사람. 사람 수가 상한이거나 맵이 로드돼 있지 않으면 null.</returns>
        public static Human SpawnHumanClone(Human source)
        {
            MapLoader loader = Instance;
            if (source == null || loader.m_sortedRawPointData == null || loader.m_humans.Count >= k_maxHumans) return null;

            float yaw = source.Controller.Yaw;
            var point = new RawPointData
            {
                position = source.Controller.Position + Coord.YawForward(yaw) * k_cloneForwardOffset + Vector3.Up * k_cloneHeightOffset,
                look = yaw,
                param0 = PointHuman,
                param1 = source.HumanParam.param1,
                // 어떤 포인트의 식별번호도 아닌 값이라 경로가 없는 사람이 된다.
                param2 = -1,
                param3 = 0,
            };
            var info = new RawPointData
            {
                param0 = PointHumanInfo,
                param1 = source.HumanDataParam.param1,
                param2 = source.Team,
                param3 = source.HumanParam.param1,
            };

            var human = new Human { Name = $"Human_{loader.m_humans.Count}" };
            loader.m_humanRoot.AddChild(human);
            human.CreateHuman(point, info);
            for (int slot = 0; slot < Human.WeaponSlotCount; slot++)
            {
                human.SetWeapon(slot, source.GetWeapon(slot).WeaponIndex);
            }
            human.SetSelectWeapon(source.SelectWeapon);
            loader.m_humans.Add(human);
            return human;
        }

        /// <summary>
        /// 메시지 번호(.msg 파일의 0-기반 줄 번호)로 텍스트를 조회한다.
        /// </summary>
        /// <param name="id">메시지 번호.</param>
        /// <returns>메시지. 범위 밖이면 빈 문자열.</returns>
        public static string GetMessageText(int id)
        {
            List<string> messages = Instance.m_messages;
            return id >= 0 && id < messages.Count ? messages[id] : string.Empty;
        }

        /// <summary>
        /// 사람·무기·소물이 쓰는 머티리얼을 텍스처 경로로 얻는다. 같은 텍스처는 맵이 로드돼 있는 동안 머티리얼을 공유한다.
        /// </summary>
        /// <param name="relativeTexturePath">데이터 루트 기준 텍스처 경로. null 이거나 로드에 실패하면 텍스처 없는 흰색 머티리얼.</param>
        /// <returns>머티리얼.</returns>
        public static ShaderMaterial GetEntityMaterial(string relativeTexturePath)
        {
            MapLoader loader = Instance;

            string fullPath = string.IsNullOrEmpty(relativeTexturePath) ? null : GamePath.Resolve(relativeTexturePath);
            if (fullPath != null)
            {
                if (loader.m_entityMaterialCache.TryGetValue(fullPath, out ShaderMaterial cached))
                {
                    return cached;
                }

                ImageTexture texture = ImageLoader.LoadTexture(fullPath);
                if (texture != null)
                {
                    ShaderMaterial material = MaterialManager.Instance.CreateMainMaterial(texture);
                    material.ResourceName = Path.GetFileName(fullPath);
                    loader.m_entityMaterialCache[fullPath] = material;
                    return material;
                }
            }

            return loader.m_untexturedMaterial ??= MaterialManager.Instance.CreateMainMaterial(null);
        }

        /// <summary>
        /// PD1 바이너리 파일을 파싱한다.
        /// </summary>
        /// <param name="filepath">PD1 파일 전체 경로.</param>
        /// <param name="points">파일 순서대로의 포인트 배열.</param>
        /// <returns>파싱에 성공했으면 true.</returns>
        private static bool LoadPD1File(string filepath, out RawPointData[] points)
        {
            points = null;

            try
            {
                using var reader = new BinaryReader(File.OpenRead(filepath));

                int pointCount = reader.ReadInt16();
                points = new RawPointData[pointCount];
                for (int i = 0; i < pointCount; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    points[i] = new RawPointData
                    {
                        position = Coord.FromXops(x, y, z),
                        // 원본 방향(라디안)을 UnityXOPS 규약 yaw(도)로 바꾼다. UnityXOPS 와 같은 식이다.
                        look = Mathf.RadToDeg(reader.ReadSingle()) + 180f,
                        param0 = reader.ReadByte(),
                        param1 = reader.ReadByte(),
                        param2 = reader.ReadByte(),
                        param3 = reader.ReadByte(),
                    };
                }

                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debugger.LogError($"PD1 read failed: {filepath}\n{e.Message}", nameof(MapLoader));
                return false;
            }
        }
    }
}
