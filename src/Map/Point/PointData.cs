using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// PD1 이나 PD2 파일에서 파싱된 포인트 하나. 어느 형식에서 읽었든 같은 모양이다. position 은 Godot 좌표, look 은 UnityXOPS 규약 yaw(도)다.
    /// param0~3 은 원본의 P1~P4 에 해당한다 (param0 = 종류, param3 = 식별번호). PD1 은 0 에서 255 사이이고 PD2 는 int32 전체다.
    /// look 은 사람 기준 yaw 다 (원본 방향 + 180°). 사람과 무기는 look 을 그대로 쓰고, 소물에는 look − 180 을 쓴다.
    /// </summary>
    public class RawPointData
    {
        public Vector3 position;
        public float look;
        public int param0;
        public int param1;
        public int param2;
        public int param3;
        // 추가 파라미터 (PD2). PD1 은 빈 배열이다. 4바이트 칸이고 포인트 종류에 따라 정수, 실수, 불로 읽는다.
        public int[] extra = Array.Empty<int>();

        /// <summary>
        /// 추가 파라미터 한 칸을 정수로 읽는다.
        /// </summary>
        /// <param name="index">칸 번호 (0 부터).</param>
        /// <param name="fallback">칸이 없을 때의 값. 옛 파일에는 나중에 더해진 칸이 없다.</param>
        /// <returns>칸의 값.</returns>
        public int GetExtraInt(int index, int fallback = 0)
        {
            return index >= 0 && index < extra.Length ? extra[index] : fallback;
        }

        /// <summary>
        /// 추가 파라미터 한 칸을 실수(float32)로 읽는다.
        /// </summary>
        /// <param name="index">칸 번호 (0 부터).</param>
        /// <param name="fallback">칸이 없을 때의 값.</param>
        /// <returns>칸의 값.</returns>
        public float GetExtraFloat(int index, float fallback = 0f)
        {
            return index >= 0 && index < extra.Length ? BitConverter.Int32BitsToSingle(extra[index]) : fallback;
        }

        /// <summary>
        /// 추가 파라미터 한 칸을 불로 읽는다. 0 이면 거짓, 그 밖은 참이다.
        /// </summary>
        /// <param name="index">칸 번호 (0 부터).</param>
        /// <param name="fallback">칸이 없을 때의 값.</param>
        /// <returns>칸의 값.</returns>
        public bool GetExtraBool(int index, bool fallback = false)
        {
            return index >= 0 && index < extra.Length ? extra[index] != 0 : fallback;
        }
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

        // 소물은 원본이 방향을 그대로 그린다. 사람 기준 yaw 인 look 과 180° 차이가 난다 (원본 object.cpp:2158 사람은 +π, :2765 소물은 그대로).
        private const float k_modelYawOffset = 180f;
        // PD1 의 이벤트 세 줄의 시작 식별번호. 원본은 −100, −110, −120 인데 PD1 의 파라미터를 부호 없는 바이트로 읽으므로 156, 146, 136 이다.
        private static readonly int[] s_legacyEventEntryIds = { 156, 146, 136 };
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
        private int[] m_eventEntryIds = s_legacyEventEntryIds;
        // 이벤트 포인트로 조회할 종류 번호. 원본의 10~19 에, 확장 형식이면 이 맵이 쓰는 스크립트 이벤트의 종류가 더해진다.
        private readonly List<int> m_eventPointTypes = new List<int>();
        // 스크립트가 놓은 것까지 합친 소물 수의 상한. 스크립트가 소물을 끝없이 만드는 것을 막는다.
        private const int k_maxSmallObjects = 256;

        // 종류(param0)별 → 식별번호(param3)별 포인트 목록. 파일 순서를 유지한다.
        private Dictionary<int, Dictionary<int, List<RawPointData>>> m_sortedRawPointData;
        private bool m_pointDataExtended;

        public static Human Player => Instance.m_player;
        // 스폰된 전체 Human 목록 (스폰 순서).
        public static IReadOnlyList<Human> Humans => Instance.m_humans;
        public static int HumanCount => Instance.m_humans.Count;
        // 스폰된 전체 소물 목록 (스폰 순서). 부서진 것도 남아 있다.
        public static IReadOnlyList<SmallObject> SmallObjects => Instance.m_smallObjects;
        // 플레이어의 미션 통계.
        public static MissionStats Stats => Instance.m_stats;
        public static int MessageCount => Instance.m_messages.Count;
        // 이벤트 줄마다의 시작 식별번호. 개수가 이벤트 줄 수다. PD1 은 항상 세 줄이고, PD2 는 파일이 정한다.
        public static IReadOnlyList<int> EventEntryIds => Instance.m_eventEntryIds;
        // 로드된 포인트 데이터가 확장 형식(PD2)인지. 원본 형식(PD1)에만 걸리는 원본의 제한(메시지 16개, 한 틱의 이벤트 6개)을 가르는 데 쓴다.
        public static bool PointDataExtended => Instance.m_pointDataExtended;

        // m_humans 내 플레이어 인덱스. 플레이어가 없으면 -1.
        public static int PlayerIndex => Instance.m_player != null ? Instance.m_humans.IndexOf(Instance.m_player) : -1;

        /// <summary>
        /// 포인트 데이터 파일을 읽어 포인트를 정리하고 사람·무기·소물을 스폰한다. 이전에 로드된 것은 먼저 제거한다.
        /// 확장자가 .pd2 이면 PD2 로, 그 밖에는 PD1 로 읽는다. 읽은 뒤의 구조는 같다. 같은 이름의 .msg 파일이 있으면 메시지도 읽는다.
        /// </summary>
        /// <param name="filepath">PD1 또는 PD2 파일 전체 경로.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        public static bool LoadPointData(string filepath)
        {
            UnloadPointData();

            if (string.IsNullOrEmpty(filepath))
            {
                Debugger.LogError("Point data path is empty.", nameof(MapLoader));
                return false;
            }

            if (!File.Exists(filepath))
            {
                Debugger.LogError($"Point data open failed: {Path.GetRelativePath(GamePath.Root, filepath)}", nameof(MapLoader));
                return false;
            }

            bool pd2 = string.Equals(Path.GetExtension(filepath), PD2File.Extension, StringComparison.OrdinalIgnoreCase);
            RawPointData[] points;
            int[] eventEntryIds = s_legacyEventEntryIds;
            if (pd2 ? !LoadPD2File(filepath, out points, out eventEntryIds) : !LoadPD1File(filepath, out points))
            {
                return false;
            }

            MapLoader loader = Instance;
            loader.m_eventEntryIds = eventEntryIds;
            loader.m_pointDataExtended = pd2;

            // 사람·무기·소물을 만들기 전에 미션의 에드온 데이터를 붙인다 (MIF2 가 아닌 미션이면 에드온 없음).
            LoadAddonData();

            // 종류 번호에 제한을 두지 않는다 (원본의 종류는 1 에서 19 사이지만, 나중에 종류를 더할 수 있다).
            loader.m_sortedRawPointData = new Dictionary<int, Dictionary<int, List<RawPointData>>>();
            foreach (RawPointData raw in points)
            {
                if (!loader.m_sortedRawPointData.TryGetValue(raw.param0, out Dictionary<int, List<RawPointData>> byId))
                {
                    byId = new Dictionary<int, List<RawPointData>>();
                    loader.m_sortedRawPointData[raw.param0] = byId;
                }
                if (!byId.TryGetValue(raw.param3, out List<RawPointData> list))
                {
                    list = new List<RawPointData>();
                    byId[raw.param3] = list;
                }
                list.Add(raw);
            }

            // 확장 형식에서 20 이상의 종류는 스크립트 이벤트다. 등록되지 않았거나 스크립트를 올리지 못하면 미션을 로드하지 않는다.
            // 원본 형식(PD1)은 원본대로만 돈다.
            loader.m_eventPointTypes.Clear();
            for (int type = PointEventFirst; type <= PointEventLast; type++)
            {
                loader.m_eventPointTypes.Add(type);
            }
            if (pd2 && EventManager.Loaded)
            {
                var scriptTypes = new List<int>();
                foreach (int type in loader.m_sortedRawPointData.Keys)
                {
                    if (type >= EventManager.ScriptEventFirst) scriptTypes.Add(type);
                }
                scriptTypes.Sort();
                if (!EventManager.Instance.LoadScripts(scriptTypes, loader.m_addonEventDataPath))
                {
                    UnloadPointData();
                    return false;
                }
                loader.m_eventPointTypes.AddRange(scriptTypes);
            }

            // 원본 LoadPointData 는 파일 순서대로 순회하며 HUMAN/HUMAN2 를 스폰한다. 인간 정보(HUMANINFO)는 param1 로 찾고 첫 매치를 쓴다.
            foreach (RawPointData raw in points)
            {
                if (raw.param0 != PointHuman && raw.param0 != PointHuman2) continue;

                RawPointData info = GetPoint(PointHumanInfo, raw.param1);
                if (info == null) continue;
                WarnMissingAddon(DataManager.Instance.HumanParameterData.humanData, info.param1, "human");

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
                    if (!parameter.weaponData.Has(weaponIndex))
                    {
                        WarnMissingAddon(parameter.weaponData, weaponIndex, "weapon");
                        continue;
                    }
                    totalBullets = parameter.weaponData[weaponIndex].magazineSize * Weapon.DefaultAutoBulletMultiplier;
                }

                if (!parameter.weaponData.Has(weaponIndex))
                {
                    WarnMissingAddon(parameter.weaponData, weaponIndex, "weapon");
                    continue;
                }
                if (weaponIndex == parameter.weaponGeneralData.noneWeaponIndex) continue;

                // 전체 탄 수를 탄창과 예비로 나눈다 (원본은 탄창 0 으로 놓고 RunReload 를 한 번 부른다).
                int magazineSize = parameter.weaponData[weaponIndex].magazineSize;
                int magazine = Mathf.Min(totalBullets, magazineSize);
                int reserve = Mathf.Max(0, totalBullets - magazineSize);

                // 무기는 사람 기준 yaw(look)를 그대로 쓴다. 소물처럼 180° 를 빼면 원본과 반대로 놓인다 (화면으로 원본과 대조해 확인했다).
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
                if (!parameter.objectData.Has(raw.param1))
                {
                    WarnMissingAddon(parameter.objectData, raw.param1, "object");
                    continue;
                }

                var smallObject = new SmallObject { Name = $"Object_{loader.m_smallObjects.Count}" };
                loader.m_objectRoot.AddChild(smallObject);
                // look 은 사람 기준 yaw 다 (원본 방향 + 180°). 원본은 사람만 방향에 π 를 더해 그리고 (object.cpp:2158) 소물은 그대로 그리므로 (object.cpp:2765) 도로 뺀다.
                smallObject.CreateObject(raw.param1, raw.param3, raw.position, raw.look - k_modelYawOffset);
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
            if (EventManager.Loaded)
            {
                EventManager.Instance.StopMission();
                EventManager.Instance.UnloadScripts();
            }
            loader.m_eventPointTypes.Clear();
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
            loader.m_eventEntryIds = s_legacyEventEntryIds;
            loader.m_pointDataExtended = false;

            // 에드온 데이터를 쓰던 것(사람, 무기, 이펙트)을 다 지운 뒤에 뗀다.
            UnloadAddonData();
        }

        /// <summary>
        /// 포인트가 에드온 번호(10000 이상)를 가리키는데 미션이 그 항목을 들고 오지 않았으면 경고를 남긴다. 디버그 콘솔에서 바로 보인다.
        /// 10000 미만의 없는 번호는 원본 맵에도 있을 수 있어 조용히 건너뛴다.
        /// </summary>
        /// <typeparam name="T">항목의 형식.</typeparam>
        /// <param name="list">데이터 목록.</param>
        /// <param name="index">포인트가 가리키는 번호.</param>
        /// <param name="what">무엇의 번호인지 (영어, 로그용).</param>
        private static void WarnMissingAddon<T>(DataList<T> list, int index, string what)
        {
            if (index < DataList<T>.AddonBase || list.Has(index)) return;

            Debugger.LogWarning($"A point refers to add-on {what} {index}, but the mission provides {list.AddonCount} add-on {what} entries", nameof(MapLoader));
        }

        /// <summary>
        /// 종류(param0)와 식별번호(param3)로 포인트의 첫 매치를 조회한다.
        /// </summary>
        /// <param name="category">포인트 종류.</param>
        /// <param name="id">식별번호.</param>
        /// <returns>첫 매치. 없거나 맵이 로드돼 있지 않으면 null.</returns>
        public static RawPointData GetPoint(int category, int id)
        {
            Dictionary<int, Dictionary<int, List<RawPointData>>> sorted = Instance.m_sortedRawPointData;
            if (sorted == null || !sorted.TryGetValue(category, out Dictionary<int, List<RawPointData>> byId)) return null;

            return byId.TryGetValue(id, out List<RawPointData> list) && list.Count > 0 ? list[0] : null;
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
        /// 이벤트 포인트를 식별번호로 조회한다. 이벤트 종류(10~19 와 이 맵이 쓰는 스크립트 이벤트) 전체에서 찾는다. 경로와 마찬가지로 다른 종류의 포인트는 보지 않는다.
        /// </summary>
        /// <param name="id">식별번호.</param>
        /// <returns>첫 매치. 없으면 null (이벤트 줄 끝).</returns>
        public static RawPointData GetEventPoint(int id)
        {
            foreach (int type in Instance.m_eventPointTypes)
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
        /// 이벤트가 미션 도중에 사람을 새로 세운다. 맵을 로드할 때와 같이 사람 정보 포인트(종류 4: 사람 데이터 번호와 팀)를 바탕으로 만든다.
        /// </summary>
        /// <param name="infoId">사람 정보 포인트의 식별번호 (사람 포인트의 P2 에 적는 번호).</param>
        /// <param name="position">위치 (발밑).</param>
        /// <param name="yaw">방향 (사람 기준 yaw, 도).</param>
        /// <param name="identifier">식별번호. 이벤트가 이 사람을 가리킬 때 쓴다. 0 이어도 플레이어가 되지는 않는다.</param>
        /// <param name="pathId">처음 향할 경로 포인트의 식별번호. 없는 번호면 경로가 없는 사람이 된다.</param>
        /// <returns>새 사람의 인덱스. 맵이 로드돼 있지 않거나, 사람 수가 상한이거나, 그 번호의 사람 정보 포인트가 없으면 −1.</returns>
        public static int SpawnHuman(int infoId, Vector3 position, float yaw, int identifier, int pathId)
        {
            MapLoader loader = Instance;
            if (loader.m_sortedRawPointData == null || loader.m_humans.Count >= k_maxHumans || !position.IsFinite()) return -1;

            RawPointData info = GetPoint(PointHumanInfo, infoId);
            if (info == null) return -1;

            var point = new RawPointData { position = position, look = yaw, param0 = PointHuman, param1 = infoId, param2 = pathId, param3 = identifier };

            var human = new Human { Name = $"Human_{loader.m_humans.Count}" };
            loader.m_humanRoot.AddChild(human);
            human.CreateHuman(point, info);
            loader.m_humans.Add(human);
            return loader.m_humans.Count - 1;
        }

        /// <summary>
        /// 이벤트가 미션 도중에 소물을 새로 놓는다.
        /// </summary>
        /// <param name="objectIndex">소물 데이터 번호.</param>
        /// <param name="identifier">식별번호.</param>
        /// <param name="position">위치.</param>
        /// <param name="yaw">방향 (사람 기준 yaw, 도. 소물 포인트의 look 과 같다).</param>
        /// <param name="snap">true 면 바닥에 붙인다.</param>
        /// <returns>놓았으면 true. 맵이 로드돼 있지 않거나, 소물 수가 상한이거나, 없는 소물 데이터면 false.</returns>
        public static bool SpawnSmallObject(int objectIndex, int identifier, Vector3 position, float yaw, bool snap)
        {
            MapLoader loader = Instance;
            if (loader.m_sortedRawPointData == null || loader.m_smallObjects.Count >= k_maxSmallObjects) return false;
            if (!DataManager.Instance.ObjectParameterData.objectData.Has(objectIndex) || !position.IsFinite()) return false;

            var smallObject = new SmallObject { Name = $"Object_{loader.m_smallObjects.Count}" };
            loader.m_objectRoot.AddChild(smallObject);
            smallObject.CreateObject(objectIndex, identifier, position, yaw - k_modelYawOffset);
            if (snap) smallObject.SnapToGround();
            loader.m_smallObjects.Add(smallObject);
            return true;
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
