using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. 확장 미션 파일(MIF2)과 에드온 데이터를 확인한다.
    /// 1) 모든 공식 미션을 확장 형식 한 벌(BD2, PD2, MIF2)로 바꿔, 원본과 미션 정보가 같고 같은 난수 씨앗으로 돌린 시뮬레이션 결과가 같은지 본다.
    /// 2) 직접 만든 MIF2 로 에드온 데이터(사람, 무기, 오브젝트, 이펙트, 재질)와 10000 번호 규칙, 없는 번호의 처리를 본다.
    /// 3) MIF2 의 형식 오류, 미션 목록 스캔, 원본 MIF 의 추가 사물 변환을 본다.
    /// 파일은 build/mif2_check/ 에 쓰고 끝나면 지운다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/mif2_check.tscn
    /// 명령행 인자("--" 뒤): --convert-official 번호|all 출력폴더 는 공식 미션을, --convert 입력.mif 출력폴더 는 MIF 미션을 확장 형식으로 바꾸고 종료한다 (경로는 exe 폴더 기준).
    /// </summary>
    public partial class MIF2Check : Node
    {
        private const string k_workFolder = "build/mif2_check";
        private const int k_simulationTicks = 100;
        private const float k_positionTolerance = 1e-3f;
        private const int k_addon = DataList<int>.AddonBase;

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();

        /// <summary>
        /// 미션 하나를 로드해 일정 틱을 돌린 뒤의 상태.
        /// </summary>
        private class Snapshot
        {
            public string info;
            public int blocks;
            public int result;
            public string[] humans;
            public Vector3[] positions;
        }

        public override void _Ready()
        {
            string[] args = OS.GetCmdlineUserArgs();
            int officialArg = Array.IndexOf(args, "--convert-official");
            int fileArg = Array.IndexOf(args, "--convert");
            if (officialArg >= 0 || fileArg >= 0)
            {
                bool ok = officialArg >= 0
                    ? officialArg + 2 < args.Length && ConvertOfficial(args[officialArg + 1], args[officialArg + 2])
                    : fileArg + 2 < args.Length && ConvertFile(args[fileArg + 1], args[fileArg + 2]);
                if (!ok) GD.Print("사용법: --convert-official 번호|all 출력폴더, 또는 --convert 입력.mif 출력폴더 (경로는 exe 폴더 기준)");
                Finish(ok);
                return;
            }

            string workFolder = GamePath.Resolve(k_workFolder);
            Directory.CreateDirectory(workFolder);

            int missions = CheckOfficialMissions();
            CheckAddonData();
            CheckFormat();
            CheckLegacyAddonObject();

            MapLoader.UnloadPointData();
            MapLoader.UnloadBlockData();
            MapLoader.UnloadMissionData();
            Directory.Delete(workFolder, true);

            GD.Print($"MIF2 점검 {m_checks}항목 (미션 {missions}개 대조) — 문제 {m_problems.Count}건");
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
        /// 공식 미션을 확장 형식으로 바꿔 쓴다.
        /// </summary>
        /// <param name="which">미션 번호, 또는 "all".</param>
        /// <param name="outputFolder">출력 폴더 (exe 폴더 기준).</param>
        /// <returns>전부 성공했으면 true.</returns>
        private static bool ConvertOfficial(string which, string outputFolder)
        {
            int count = DataManager.Instance.MissionData.officialMissions.Count;
            int first = 0;
            int last = count - 1;
            if (which != "all")
            {
                if (!int.TryParse(which, out first) || first < 0 || first >= count) return false;
                last = first;
            }

            bool ok = true;
            for (int index = first; index <= last; index++)
            {
                if (!MapLoader.LoadMissionData(index, false, 0))
                {
                    ok = false;
                    continue;
                }
                ok &= ConvertLoaded(outputFolder, $"mission{index:00}");
            }
            return ok;
        }

        /// <summary>
        /// MIF 미션 하나를 확장 형식으로 바꿔 쓴다.
        /// </summary>
        /// <param name="input">MIF 경로 (exe 폴더 기준).</param>
        /// <param name="outputFolder">출력 폴더 (exe 폴더 기준).</param>
        /// <returns>성공했으면 true.</returns>
        private static bool ConvertFile(string input, string outputFolder)
        {
            string path = GamePath.Resolve(input);
            if (path == null || !MapLoader.LoadMissionFile(path))
            {
                GD.Print($"미션 파일을 읽지 못했습니다: {input}");
                return false;
            }
            return ConvertLoaded(outputFolder, Path.GetFileNameWithoutExtension(path));
        }

        private static bool ConvertLoaded(string outputFolder, string baseName)
        {
            string name = MapLoader.Instance.MissionName;
            string result = MapLoader.ConvertMissionToExtended(outputFolder, baseName, out string error);
            GD.Print(result != null ? $"변환: {name} → {result}" : $"변환 실패: {name} ({error})");
            return result != null;
        }

        /// <summary>
        /// 모든 공식 미션을 확장 형식으로 바꿔, 미션 정보와 시뮬레이션 결과가 원본과 같은지 본다.
        /// 두 번 다 같은 난수 씨앗으로 로드하고 AI 와 이벤트를 켠 채 같은 수의 틱을 돌린다.
        /// </summary>
        /// <returns>대조한 미션 수.</returns>
        private int CheckOfficialMissions()
        {
            int count = DataManager.Instance.MissionData.officialMissions.Count;
            int compared = 0;

            for (int index = 0; index < count; index++)
            {
                string label = DataManager.Instance.MissionData.officialMissions[index].name;
                if (!MapLoader.LoadMissionData(index, false, 0)) continue;

                Snapshot before = LoadAndRun();
                if (before == null)
                {
                    Expect(false, $"{label}: 원본 미션 로드 실패");
                    continue;
                }

                // 변환은 미션 정보만 있으면 된다. 맵을 내린 뒤에 해도 미션 정보는 남아 있다.
                string converted = MapLoader.ConvertMissionToExtended(k_workFolder, "map", out string error);
                if (converted == null || !MapLoader.LoadMissionFile(GamePath.Resolve(converted)))
                {
                    Expect(false, $"{label}: 확장 형식 변환이나 읽기 실패 ({error})");
                    continue;
                }
                Expect(MapLoader.Instance.ExtendedMission, $"{label}: 변환한 미션이 확장 미션으로 읽히지 않음");

                Snapshot after = LoadAndRun();
                string difference = after == null ? "확장 미션 로드 실패" : Compare(before, after);
                Expect(difference == null, $"{label}: 원본과 확장 형식이 다름 — {difference}");
                compared++;
            }

            return compared;
        }

        /// <summary>
        /// MapLoader 에 들어 있는 미션을 로드하고 정해진 틱을 돌린 뒤 상태를 뜬다. 끝나면 맵을 내린다 (미션 정보는 남긴다).
        /// </summary>
        /// <returns>뜬 상태. 로드에 실패하면 null.</returns>
        private static Snapshot LoadAndRun()
        {
            MapLoader loader = MapLoader.Instance;
            GameRandom.Reseed(1u);
            bool loaded = MapLoader.LoadBlockData(loader.MissionBD1Path);
            MapLoader.LoadSkyData(loader.SkyIndex);
            loaded &= MapLoader.LoadPointData(loader.MissionPD1Path);
            if (!loaded)
            {
                MapLoader.UnloadPointData();
                MapLoader.UnloadBlockData();
                return null;
            }

            AIController.Enabled = true;
            AIController.DrivePlayer = false;
            EventManager.Instance.BeginMission();
            for (int tick = 0; tick < k_simulationTicks; tick++) SimClock.Step();

            IReadOnlyList<Human> humans = MapLoader.Humans;
            var snapshot = new Snapshot
            {
                info = $"{loader.MissionName}|{loader.MissionFullname}|{loader.SkyIndex}|{loader.AdjustCollision}|{loader.DarkScreen}|"
                    + $"{loader.MissionImage0}|{loader.MissionImage1}|{loader.MissionBriefing}|{MapLoader.MessageCount}",
                blocks = MapLoader.Blocks.Count,
                result = EventManager.Instance.Result,
                humans = new string[humans.Count],
                positions = new Vector3[humans.Count],
            };
            for (int i = 0; i < humans.Count; i++)
            {
                Human human = humans[i];
                snapshot.humans[i] = string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}",
                    human.HP, human.Alive, human.Team, human.CurrentWeapon.WeaponIndex, human.CurrentWeapon.Magazine);
                snapshot.positions[i] = human.Controller.Position;
            }

            MapLoader.UnloadPointData();
            MapLoader.UnloadBlockData();
            return snapshot;
        }

        /// <summary>
        /// 두 상태를 대조한다.
        /// </summary>
        /// <param name="a">원본에서 뜬 상태.</param>
        /// <param name="b">확장 형식에서 뜬 상태.</param>
        /// <returns>처음 발견한 차이의 설명. 같으면 null.</returns>
        private static string Compare(Snapshot a, Snapshot b)
        {
            if (a.info != b.info) return $"미션 정보\n  {a.info}\n  {b.info}";
            if (a.blocks != b.blocks) return $"블록 수 {a.blocks} / {b.blocks}";
            if (a.result != b.result) return $"미션 결과 {a.result} / {b.result}";
            if (a.humans.Length != b.humans.Length) return $"사람 수 {a.humans.Length} / {b.humans.Length}";
            for (int i = 0; i < a.humans.Length; i++)
            {
                if (a.humans[i] != b.humans[i]) return $"사람 {i} 의 상태 {a.humans[i]} / {b.humans[i]}";
                if (a.positions[i].DistanceTo(b.positions[i]) > k_positionTolerance) return $"사람 {i} 의 위치 {a.positions[i]} / {b.positions[i]}";
            }
            return null;
        }

        /// <summary>
        /// 데이터 객체를 JSON 을 거쳐 복사한다.
        /// </summary>
        /// <typeparam name="T">데이터 클래스.</typeparam>
        /// <param name="value">복사할 객체.</param>
        /// <returns>내용이 같은 새 객체.</returns>
        private static T Clone<T>(T value)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonData.Options), JsonData.Options);
        }

        private static void WriteJson(string relativePath, object value)
        {
            File.WriteAllText(GamePath.Resolve(relativePath), JsonData.ToJson(value));
        }

        /// <summary>
        /// 직접 만든 확장 미션으로 에드온 데이터를 확인한다. 에드온 파일은 기본 데이터의 항목을 복사해 값 하나씩만 바꾼 것이라,
        /// 번호 10000 이 그 항목을 가리키는지를 바뀐 값으로 알 수 있다. 에드온 파일의 전역 설정은 무시돼야 한다.
        /// </summary>
        private void CheckAddonData()
        {
            const float addonHumanHp = 777f;
            const int addonMagazine = 77;
            const float addonObjectHp = 55f;
            const string addonHitSound = "data/sound/hit3.wav";
            const int objectId = 300000;

            DataManager data = DataManager.Instance;
            HumanParameterData baseHuman = data.HumanParameterData;
            WeaponParameterData baseWeapon = data.WeaponParameterData;
            ObjectParameterData baseObject = data.ObjectParameterData;
            EffectParameterData baseEffect = data.EffectParameterData;
            int noneWeapon = baseWeapon.weaponGeneralData.noneWeaponIndex;
            int poolSize = baseEffect.effectGeneralData.poolInitialSize;
            int sourceWeapon = baseWeapon.weaponData.FindIndex(weapon => weapon.magazineSize > 0 && baseWeapon.bulletData.Has(weapon.bulletIndex) && !baseWeapon.bulletData[weapon.bulletIndex].useGravity);
            int smoke = baseEffect.effectData.FindIndex(effect => effect.name == "WallHitSmoke");
            if (sourceWeapon < 0 || smoke < 0)
            {
                Expect(false, "에드온 점검의 전제(총알을 쏘는 무기, WallHitSmoke 프리셋)가 맞지 않음");
                return;
            }

            // --- 에드온 데이터 파일 다섯 개 ---
            var human = new HumanParameterData();
            HumanData humanEntry = Clone(baseHuman.humanData[0]);
            human.humanTypeData.Add(Clone(baseHuman.humanTypeData[humanEntry.typeIndex]));
            humanEntry.hp = addonHumanHp;
            humanEntry.typeIndex = k_addon;
            human.humanData.Add(humanEntry);
            WriteJson($"{k_workFolder}/addon_human.json", human);

            var weapon = new WeaponParameterData();
            WeaponData weaponEntry = Clone(baseWeapon.weaponData[sourceWeapon]);
            weapon.bulletData.Add(Clone(baseWeapon.bulletData[weaponEntry.bulletIndex]));
            weapon.weaponModelData.Add(Clone(baseWeapon.weaponModelData[weaponEntry.modelIndex]));
            weaponEntry.magazineSize = addonMagazine;
            weaponEntry.bulletIndex = k_addon;
            weaponEntry.modelIndex = k_addon;
            weapon.weaponData.Add(weaponEntry);
            // 전역 설정은 에드온 파일에 있어도 쓰이지 않아야 한다.
            weapon.weaponGeneralData.noneWeaponIndex = noneWeapon + 1;
            WriteJson($"{k_workFolder}/addon_weapon.json", weapon);

            var smallObject = new ObjectParameterData();
            ObjectData objectEntry = Clone(baseObject.objectData[0]);
            smallObject.objectModelData.Add(Clone(baseObject.objectModelData[objectEntry.modelIndex]));
            smallObject.objectColliderData.Add(Clone(baseObject.objectColliderData[objectEntry.colliderIndex]));
            objectEntry.hp = addonObjectHp;
            objectEntry.modelIndex = k_addon;
            objectEntry.colliderIndex = k_addon;
            smallObject.objectData.Add(objectEntry);
            WriteJson($"{k_workFolder}/addon_object.json", smallObject);

            var effect = new EffectParameterData();
            EffectData effectEntry = Clone(baseEffect.effectData[smoke]);
            effect.effectTextureData.Add(Clone(baseEffect.effectTextureData[effectEntry.emitters[0].textureIndex]));
            foreach (EffectEmitter emitter in effectEntry.emitters) emitter.textureIndex = k_addon;
            effect.effectData.Add(effectEntry);
            effect.effectGeneralData.poolInitialSize = poolSize + 1;
            WriteJson($"{k_workFolder}/addon_effect.json", effect);

            var material = new BlockMaterialParameterData();
            material.blockMaterialData.Add(new BlockMaterialData { name = "AddonFloor", hitEffect = k_addon, hitSounds = new List<string> { addonHitSound } });
            WriteJson($"{k_workFolder}/addon_material.json", material);

            // --- 블록: 넓은 바닥. 재질은 전부 -1 이고 미션의 기본 재질이 에드온 재질이다 ---
            var blocks = new BD2File { textureListPath = $"{k_workFolder}/addon_textures.json" };
            blocks.blocks.Add(BD2Check.MakeBox(new Vector3(0f, -1f, 0f), new Vector3(80f, 1f, 80f), 0));
            for (int f = 0; f < BD2Block.FaceCount; f++) blocks.blocks[0].materialIndices[f] = -1;
            var textures = new BlockTextureListData();
            textures.blockTextureData.Add(new BlockTextureData());
            WriteJson(blocks.textureListPath, textures);
            blocks.Write(GamePath.Resolve($"{k_workFolder}/addon.bd2"), out _);

            // --- 포인트: 에드온 사람(플레이어), 기본 사람(다른 팀), 에드온 무기와 오브젝트, 그리고 없는 에드온 번호 셋 ---
            var points = new PD2File();
            points.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = k_addon, param2 = 0, id = 100 });
            points.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = 100, param2 = -1, id = 0, position = new Vector3(0f, 0.01f, 0f) });
            points.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 1, id = 101 });
            points.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = 101, param2 = -1, id = 5, position = new Vector3(40f, 0.01f, 0f) });
            points.points.Add(new PD2Point { type = MapLoader.PointWeapon, param1 = k_addon, param2 = 200, position = new Vector3(5f, 1f, 5f) });
            points.points.Add(new PD2Point { type = MapLoader.PointWeapon, param1 = k_addon + 5, param2 = 200, position = new Vector3(6f, 1f, 5f) });
            points.points.Add(new PD2Point { type = MapLoader.PointSmallObject, param1 = k_addon, param2 = 0, id = objectId, position = new Vector3(10f, 1f, 5f) });
            points.points.Add(new PD2Point { type = MapLoader.PointSmallObject, param1 = k_addon + 9, param2 = 0, id = objectId + 1, position = new Vector3(11f, 1f, 5f) });
            points.Write(GamePath.Resolve($"{k_workFolder}/addon.pd2"), out _);

            var mission = new ExtendedMissionData
            {
                name = "ADDON CHECK",
                fullname = "Add-on data check",
                blockPath = $"{k_workFolder}/addon.bd2",
                pointPath = $"{k_workFolder}/addon.pd2",
                skyIndex = 1,
                darkScreen = true,
                briefing = new List<string> { "line one", "line two" },
                defaultBlockMaterial = k_addon,
                addonHumanDataPath = $"{k_workFolder}/addon_human.json",
                addonWeaponDataPath = $"{k_workFolder}/addon_weapon.json",
                addonObjectDataPath = $"{k_workFolder}/addon_object.json",
                addonEffectDataPath = $"{k_workFolder}/addon_effect.json",
                addonBlockMaterialDataPath = $"{k_workFolder}/addon_material.json",
            };
            string missionPath = GamePath.Resolve($"{k_workFolder}/addon.mif2");
            MIF2File.Write(missionPath, mission, out _);

            // --- 로드 ---
            AIController.Enabled = false;
            int logsBefore = Debugger.TotalCount;
            MapLoader loader = MapLoader.Instance;
            bool loaded = MapLoader.LoadMissionFile(missionPath) && MapLoader.LoadBlockData(loader.MissionBD1Path) && MapLoader.LoadPointData(loader.MissionPD1Path);
            if (!loaded)
            {
                Expect(false, "에드온 점검용 미션 로드 실패");
                AIController.Enabled = true;
                return;
            }

            Expect(loader.ExtendedMission && loader.MissionName == "ADDON CHECK" && loader.SkyIndex == 1 && loader.DarkScreen && !loader.AdjustCollision
                && loader.MissionBriefing == "line one\nline two" && MapLoader.DefaultBlockMaterial == k_addon, "MIF2 의 미션 정보가 다름");

            // 목록마다 에드온이 붙었고 번호 10000 이 그 항목이다.
            Expect(baseHuman.humanData.AddonCount == 1 && baseHuman.humanTypeData.AddonCount == 1 && baseWeapon.weaponData.AddonCount == 1
                && baseWeapon.bulletData.AddonCount == 1 && baseWeapon.weaponModelData.AddonCount == 1 && baseObject.objectData.AddonCount == 1
                && baseObject.objectModelData.AddonCount == 1 && baseObject.objectColliderData.AddonCount == 1 && baseEffect.effectData.AddonCount == 1
                && baseEffect.effectTextureData.AddonCount == 1 && data.BlockMaterialParameterData.blockMaterialData.AddonCount == 1,
                "에드온 데이터가 목록에 붙지 않음");
            Expect(baseWeapon.weaponData.Has(k_addon) && !baseWeapon.weaponData.Has(k_addon + 1) && !baseWeapon.weaponData.Has(baseWeapon.weaponData.Count)
                && !baseWeapon.weaponData.Has(-1) && baseWeapon.weaponData.Has(0), "번호가 있는지 묻는 결과가 다름 (에드온, 에드온 끝, 빈 구간, 음수, 기본)");
            Expect(baseWeapon.weaponData.Neighbor(baseWeapon.weaponData.Count - 1, 1) == k_addon && baseWeapon.weaponData.Neighbor(k_addon, 1) == 0
                && baseWeapon.weaponData.Neighbor(0, -1) == k_addon, "기본 목록과 에드온을 잇는 다음·이전 번호가 다름");
            Expect(baseWeapon.weaponGeneralData.noneWeaponIndex == noneWeapon && baseEffect.effectGeneralData.poolInitialSize == poolSize,
                "에드온 파일의 전역 설정이 기본 데이터를 바꿈");

            // 사람: 에드온 사람 종류로 스폰된다.
            Human player = MapLoader.Player;
            Expect(player != null && Mathf.IsEqualApprox(player.HP, addonHumanHp) && ReferenceEquals(player.HumanData, baseHuman.humanData[k_addon])
                && ReferenceEquals(player.HumanTypeData, baseHuman.humanTypeData[k_addon]), "에드온 사람(10000)으로 스폰되지 않음");
            Expect(MapLoader.HumanCount == 2 && ReferenceEquals(MapLoader.SearchHuman(5)?.HumanData, baseHuman.humanData[0]), "기본 사람(0)이 그대로 스폰되지 않음");

            // 무기: 에드온 무기 하나만 놓이고(없는 번호는 건너뜀), 쥐고 쏠 수 있다 (에드온 탄환과 모델을 쓴다).
            Expect(WeaponManager.Instance.CountActive() == 1, "에드온 무기가 하나 놓이지 않음 (없는 번호는 건너뛰어야 한다)");
            if (player != null)
            {
                player.SetWeapon(player.SelectWeapon, k_addon);
                Weapon held = player.CurrentWeapon;
                Expect(held.WeaponIndex == k_addon && held.Data.magazineSize == addonMagazine && held.Magazine == addonMagazine
                    && ReferenceEquals(held.ModelData, baseWeapon.weaponModelData[k_addon]), "에드온 무기를 쥐지 못함");
                // 스폰 직후의 무기 전환 대기가 끝날 때까지 무기 카운터만 돌린다.
                int bullets = BulletManager.SpawnCount;
                bool fired = false;
                for (int tick = 0; tick < 100 && !fired; tick++)
                {
                    fired = player.ShotWeapon();
                    player.TickWeapon();
                }
                Expect(fired && BulletManager.SpawnCount > bullets, "에드온 무기가 에드온 탄환을 쏘지 못함");
                BulletManager.Instance.Clear();
            }

            // 오브젝트: 에드온 오브젝트 하나만 놓인다.
            SmallObject placed = MapLoader.SearchSmallObject(objectId);
            Expect(MapLoader.SmallObjects.Count == 1 && placed != null && placed.ObjectIndex == k_addon && Mathf.IsEqualApprox(placed.HP, addonObjectHp),
                "에드온 오브젝트(10000)가 놓이지 않음");

            // 재질과 이펙트: 기본 재질(-1)이 에드온 재질이고, 그 재질의 에드온 이펙트와 소리가 난다.
            BlockMaterialData floor = MapLoader.GetFaceMaterial(MapLoader.Blocks[0], 0);
            Expect(ReferenceEquals(floor, data.BlockMaterialParameterData.blockMaterialData[k_addon]), "기본 재질이 에드온 재질(10000)이 아님");
            int sounds = SoundManager.PlayCount;
            int particles = EffectManager.SpawnCount;
            var origin = new Vector3(-30f, 4.1f, -30f);
            Bullet shot = BulletManager.Instance.Spawn(baseWeapon.bulletData[0], null, 0, 100, 0, origin, 0f, 90f, 3f, origin);
            for (int tick = 0; tick < 20 && shot != null && shot.IsActive; tick++) BulletManager.Instance.SimTick();
            int expectedParticles = 0;
            foreach (EffectEmitter emitter in effectEntry.emitters) expectedParticles += emitter.spawnCount;
            Expect(shot != null && !shot.IsActive && SoundManager.PlayCount == sounds + 1 && SoundManager.LastPlayedPath == addonHitSound
                && EffectManager.SpawnCount - particles == expectedParticles, "에드온 재질의 이펙트와 소리가 나지 않음");

            // 없는 에드온 번호 둘(무기, 오브젝트)에 경고가 남는다.
            var levels = new List<LogLevel>();
            var texts = new List<string>();
            Debugger.GetSince(logsBefore, levels, texts);
            int warnings = texts.FindAll(text => text.Contains("add-on weapon") || text.Contains("add-on object")).Count;
            Expect(warnings == 2, $"없는 에드온 번호의 경고가 {warnings}건 (기대 2건)");

            // 맵을 내리면 에드온이 떨어진다. 그 뒤에는 10000 이 없는 번호다.
            MapLoader.UnloadPointData();
            Expect(baseHuman.humanData.AddonCount == 0 && baseWeapon.weaponData.AddonCount == 0 && baseObject.objectData.AddonCount == 0
                && baseEffect.effectData.AddonCount == 0 && data.BlockMaterialParameterData.blockMaterialData.AddonCount == 0 && !baseWeapon.weaponData.Has(k_addon)
                && baseWeapon.weaponData.Neighbor(baseWeapon.weaponData.Count - 1, 1) == 0, "맵을 내린 뒤에도 에드온 데이터가 남아 있음");

            // 에드온 파일을 적지 않은 MIF2: 같은 포인트를 로드하면 에드온 번호는 전부 건너뛴다. 사람 종류가 없는 사람은 죽은 채로 스폰된다.
            mission.addonHumanDataPath = string.Empty;
            mission.addonWeaponDataPath = string.Empty;
            mission.addonObjectDataPath = string.Empty;
            mission.addonEffectDataPath = string.Empty;
            mission.addonBlockMaterialDataPath = string.Empty;
            MIF2File.Write(missionPath, mission, out _);
            loaded = MapLoader.LoadMissionFile(missionPath) && MapLoader.LoadBlockData(loader.MissionBD1Path) && MapLoader.LoadPointData(loader.MissionPD1Path);
            Expect(loaded && WeaponManager.Instance.CountActive() == 0 && MapLoader.SmallObjects.Count == 0 && MapLoader.HumanCount == 2
                && MapLoader.Player != null && !MapLoader.Player.Alive && MapLoader.SearchHuman(5).Alive, "에드온 파일이 없는 MIF2 에서 에드온 번호가 건너뛰어지지 않음");
            Expect(MapLoader.GetFaceMaterial(MapLoader.Blocks[0], 0).hitEffect == 0, "에드온 재질이 없는데 기본 재질(10000)이 빈 재질이 아님");

            // 없는 에드온 파일을 가리키면 에러 로그가 남고 로드는 계속된다.
            mission.addonWeaponDataPath = $"{k_workFolder}/no_such.json";
            MIF2File.Write(missionPath, mission, out _);
            int errors = Debugger.ErrorCount;
            loaded = MapLoader.LoadMissionFile(missionPath) && MapLoader.LoadBlockData(loader.MissionBD1Path) && MapLoader.LoadPointData(loader.MissionPD1Path);
            Expect(loaded && Debugger.FirstErrorSince(errors).StartsWith("Add-on data open failed"), "없는 에드온 파일의 에러가 남지 않음");

            MapLoader.UnloadPointData();
            MapLoader.UnloadBlockData();
            AIController.Enabled = true;
        }

        /// <summary>
        /// MIF2 의 형식 오류와 기본값, 미션 목록 스캔을 확인한다.
        /// </summary>
        private void CheckFormat()
        {
            string path = GamePath.Resolve($"{k_workFolder}/format.mif2");

            // 블록이나 포인트가 원본 형식이면 읽지 않는다.
            File.WriteAllText(path, "{ \"name\": \"X\", \"blockPath\": \"data/map0/temp.bd1\", \"pointPath\": \"a.pd2\" }");
            int errors = Debugger.ErrorCount;
            Expect(!MapLoader.LoadMissionFile(path) && Debugger.FirstErrorSince(errors).StartsWith("MIF2 needs a BD2 block file"), "BD1 을 적은 MIF2 를 읽음");
            File.WriteAllText(path, "{ \"name\": \"X\", \"blockPath\": \"a.bd2\", \"pointPath\": \"data/map0/op.pd1\" }");
            errors = Debugger.ErrorCount;
            Expect(!MapLoader.LoadMissionFile(path) && Debugger.FirstErrorSince(errors).StartsWith("MIF2 needs a PD2 point file"), "PD1 을 적은 MIF2 를 읽음");

            // 없는 키는 기본값이고, 모르는 키는 경고를 남기고 무시한다.
            File.WriteAllText(path, "{ \"name\": \"X\", \"blockPath\": \"a.bd2\", \"pointPath\": \"a.pd2\", \"noSuchKey\": 1 }");
            int logs = Debugger.TotalCount;
            MapLoader loader = MapLoader.Instance;
            Expect(MapLoader.LoadMissionFile(path) && loader.MissionName == "X" && loader.MissionFullname == string.Empty && loader.SkyIndex == 0
                && !loader.DarkScreen && loader.MissionBriefing == string.Empty && loader.MissionImage0 == string.Empty && MapLoader.DefaultBlockMaterial == 0,
                "키가 빠진 MIF2 의 기본값이 다름");
            var levels = new List<LogLevel>();
            var texts = new List<string>();
            Debugger.GetSince(logs, levels, texts);
            Expect(texts.Exists(text => text.Contains("noSuchKey")) && levels.Contains(LogLevel.Warning), "모르는 키의 경고가 남지 않음");

            // JSON 이 아닌 파일.
            File.WriteAllText(path, "this is not json");
            Expect(!MapLoader.LoadMissionFile(path), "JSON 이 아닌 MIF2 를 읽음");

            // 미션 정보를 내리면 기본 재질이 0 으로 돌아간다.
            MapLoader.DefaultBlockMaterial = 7;
            MapLoader.UnloadMissionData();
            Expect(MapLoader.DefaultBlockMaterial == 0 && !loader.ExtendedMission, "미션 정보를 내린 뒤 기본 재질이나 확장 표시가 남아 있음");

            // 미션 목록: 한 폴더의 .mif 와 .mif2 를 파일 이름 순서로 함께 모으고, 읽지 못한 MIF2 는 뺀다.
            string scanFolder = GamePath.Resolve($"{k_workFolder}/scan");
            Directory.CreateDirectory(scanFolder);
            File.WriteAllLines(Path.Combine(scanFolder, "a.mif"), new[] { "LEGACY", "Legacy mission", "./a.bd1", "./a.pd1", "0", "0", "!", "!", "!" });
            File.WriteAllText(Path.Combine(scanFolder, "b.mif2"), "{ \"name\": \"EXTENDED\", \"blockPath\": \"a.bd2\", \"pointPath\": \"a.pd2\" }");
            File.WriteAllText(Path.Combine(scanFolder, "c.mif2"), "{ broken");
            File.WriteAllText(Path.Combine(scanFolder, "d.txt"), "not a mission");
            List<AddonMissionData> page = DataManager.ScanAddonMifs(scanFolder);
            Expect(page.Count == 2 && page[0].name == "LEGACY" && page[0].mifPath.EndsWith("a.mif") && page[1].name == "EXTENDED" && page[1].mifPath.EndsWith("b.mif2"),
                "미션 목록 스캔이 .mif 와 .mif2 를 함께 모으지 않음");
        }

        /// <summary>
        /// 원본 MIF 의 추가 사물(addon-object)이 확장 형식에서 에드온 오브젝트(10000)로 옮겨지는지 확인한다.
        /// 훈련장의 블록에, 추가 사물 포인트 하나가 있는 PD1 과 MIF 를 직접 만들어 쓴다.
        /// </summary>
        private void CheckLegacyAddonObject()
        {
            const int objectId = 77;
            const int addonHp = 33;

            ObjectParameterData parameter = DataManager.Instance.ObjectParameterData;
            int legacyIndex = parameter.objectGeneralData.addonObjectIndex;
            OfficialMissionData training = DataManager.Instance.MissionData.officialMissions[0];
            ObjectModelData sourceModel = parameter.objectModelData[parameter.objectData[0].modelIndex];
            if (!parameter.objectData.Has(legacyIndex) || sourceModel.modelData.Count == 0 || sourceModel.textures.Count == 0)
            {
                Expect(false, "추가 사물 점검의 전제(예약 자리, 첫 오브젝트의 모델)가 맞지 않음");
                return;
            }

            // 추가 사물 txt: 모델, 텍스처, 판정 크기, 내구력, 소리, 튀는 세기.
            File.WriteAllLines(GamePath.Resolve($"{k_workFolder}/legacy_object.txt"), new[]
            {
                "./" + sourceModel.modelData[0].modelPath, "./" + sourceModel.textures[0], "10", addonHp.ToString(), "./" + parameter.objectData[0].soundPath, "5",
            });

            // PD1: 플레이어, 다른 팀 한 명, 추가 사물 하나. 좌표와 방향은 원본 단위다.
            using (var writer = new BinaryWriter(File.Create(GamePath.Resolve($"{k_workFolder}/legacy.pd1"))))
            {
                (int p0, int p1, int p2, int p3, float x)[] rows =
                {
                    (MapLoader.PointHumanInfo, 0, 0, 1, 0f),
                    (MapLoader.PointHuman, 1, 255, 0, 0f),
                    (MapLoader.PointHumanInfo, 0, 1, 2, 0f),
                    (MapLoader.PointHuman, 2, 255, 5, 50f),
                    (MapLoader.PointSmallObject, legacyIndex, 0, objectId, 20f),
                };
                writer.Write((short)rows.Length);
                foreach ((int p0, int p1, int p2, int p3, float x) in rows)
                {
                    writer.Write(x);
                    writer.Write(5000f);
                    writer.Write(0f);
                    writer.Write(0f);
                    writer.Write((byte)p0);
                    writer.Write((byte)p1);
                    writer.Write((byte)p2);
                    writer.Write((byte)p3);
                }
            }

            string mifPath = GamePath.Resolve($"{k_workFolder}/legacy.mif");
            File.WriteAllLines(mifPath, new[]
            {
                "LEGACY OBJECT", "Legacy add-on object", "./" + training.bd1Path, $"./{k_workFolder}/legacy.pd1", "2", "3",
                $"./{k_workFolder}/legacy_object.txt", "!", "!", "briefing a", "briefing b",
            });

            AIController.Enabled = false;
            MapLoader loader = MapLoader.Instance;
            bool loaded = MapLoader.LoadMissionFile(mifPath) && MapLoader.LoadPointData(loader.MissionPD1Path);
            SmallObject original = MapLoader.SearchSmallObject(objectId);
            Expect(loaded && original != null && original.ObjectIndex == legacyIndex && Mathf.IsEqualApprox(original.HP, addonHp), "원본 MIF 의 추가 사물이 놓이지 않음");
            MapLoader.UnloadPointData();

            string converted = MapLoader.ConvertMissionToExtended(k_workFolder, "legacy", out string error);
            Expect(converted != null, $"추가 사물이 있는 MIF 의 변환 실패 ({error})");
            if (converted != null)
            {
                loaded = MapLoader.LoadMissionFile(GamePath.Resolve(converted)) && MapLoader.LoadPointData(loader.MissionPD1Path);
                SmallObject moved = MapLoader.SearchSmallObject(objectId);
                Expect(loaded && loader.ExtendedMission && loader.SkyIndex == 2 && loader.AdjustCollision && loader.DarkScreen
                    && loader.MissionBriefing == "briefing a\nbriefing b" && loader.MissionAddonObjectPath == string.Empty, "변환한 MIF2 의 미션 정보가 다름");
                Expect(moved != null && moved.ObjectIndex == k_addon && Mathf.IsEqualApprox(moved.HP, addonHp) && parameter.objectData.AddonCount == 1
                    && parameter.objectModelData[k_addon].modelData[0].modelPath == sourceModel.modelData[0].modelPath
                    && parameter.objectColliderData[k_addon].shapes.Count == 1, "추가 사물이 에드온 오브젝트(10000)로 옮겨지지 않음");
                Expect(Mathf.IsZeroApprox(parameter.objectData[legacyIndex].hp), "확장 미션에서 원본의 예약 자리가 비워지지 않음");
                MapLoader.UnloadPointData();
            }

            AIController.Enabled = true;
        }
    }
}
