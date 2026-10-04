using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. 무기·총알·히트박스·떨어진 무기·소물·통계와 이펙트·소리 호출을 수치로 확인하고 종료한다. 문제가 있으면 종료 코드 1.
    /// 미션 하나를 로드한 뒤 사람들을 블록이 없는 공중으로 옮겨 놓고, 이동 틱 없이 무기 틱과 총알 틱만 직접 돌린다.
    /// 벽 관통과 수류탄 반사는 로드한 맵의 블록을 그대로 쓴다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/weapon_check.tscn
    /// </summary>
    public partial class WeaponCheck : Node
    {
        // 블록이 없는 공중의 기준점.
        private static readonly Vector3 s_arena = new Vector3(0f, 500f, 0f);
        // 원본 BULLET_SPEEDSCALE × 0.1. 판정 점 간격 (m).
        private const float k_substep = 0.25f;

        private readonly List<string> m_problems = new List<string>();
        private int m_checks;

        private Human m_shooter;
        private readonly List<Human> m_targets = new List<Human>();

        public override void _Ready()
        {
            if (!MapLoader.LoadMissionData(0, false, 0) || !MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path))
            {
                GD.Print("문제: 미션 0 을 로드하지 못함");
                GetTree().Quit(1);
                return;
            }

            CheckHitParts();
            CheckHitReactionAndRotation();
            CheckGraze();
            CheckPenetration();
            CheckFireRate();
            CheckReloadAndSwitch();
            CheckAimError();
            CheckAllWeapons();
            CheckExplosion();
            CheckGrenadeFlight();
            CheckWalls();
            CheckDeath();
            CheckDropAndPickup();
            CheckSmallObjects();
            CheckStatsEffectsSounds();

            MapLoader.UnloadPointData();
            GD.Print($"무기 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 조건을 확인하고, 거짓이면 문제 목록에 넣는다.
        /// </summary>
        /// <param name="ok">확인할 조건.</param>
        /// <param name="what">무엇을 확인했는지.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// 사람을 다시 스폰하고 사수 한 명과 다른 팀 표적 셋을 고른다. 사수는 공중 기준점에 정면(−Z)을 보게 놓는다.
        /// </summary>
        /// <returns>준비에 성공했으면 true.</returns>
        private bool Reset()
        {
            MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path);
            GameRandom.Reseed(1u);

            m_shooter = MapLoader.Player;
            m_targets.Clear();
            if (m_shooter == null) return false;

            foreach (Human human in MapLoader.Humans)
            {
                if (human != m_shooter && human.Alive && human.Team != m_shooter.Team && m_targets.Count < 3) m_targets.Add(human);
            }
            if (m_targets.Count < 3) return false;

            m_shooter.Controller.Teleport(s_arena);
            Aim(0f, 0f);
            // 표적은 일단 멀리 치워 둔다.
            for (int i = 0; i < m_targets.Count; i++)
            {
                m_targets[i].Controller.Teleport(s_arena + new Vector3(100f + i * 10f, 0f, 0f));
            }
            return true;
        }

        private void Aim(float yaw, float pitch)
        {
            var input = new HumanInput { yaw = yaw, pitch = pitch };
            m_shooter.Controller.SetInput(in input);
        }

        private Vector3 Eye => s_arena + Vector3.Up * m_shooter.CameraHeight;

        /// <summary>
        /// 사수의 눈높이에서 정면(−Z)으로 일반 탄환을 쏜다.
        /// </summary>
        /// <param name="attacks">위력.</param>
        /// <param name="penetration">관통력.</param>
        /// <returns>발사된 탄환.</returns>
        private Bullet FireStraight(int attacks, int penetration)
        {
            BulletData data = DataManager.Instance.WeaponParameterData.bulletData[0];
            return BulletManager.Instance.Spawn(data, m_shooter, m_shooter.Team, attacks, penetration, Eye, 0f, 0f, 3f, Eye);
        }

        private static void TickBullets(int ticks)
        {
            for (int i = 0; i < ticks; i++) BulletManager.Instance.SimTick();
        }

        /// <summary>
        /// 사수의 무기 틱을 입력 없이 흘려보낸다. 이전 사격의 발사 간격·연속 발사 수·반동 오차가 다음 확인에 섞이지 않게 한다.
        /// </summary>
        /// <param name="ticks">틱 수.</param>
        private void Idle(int ticks)
        {
            m_shooter.ClearPendingWeaponInput();
            for (int i = 0; i < ticks; i++) m_shooter.TickWeapon();
        }

        private static int FindWeapon(string name)
        {
            return DataManager.Instance.WeaponParameterData.weaponData.FindIndex(weapon => weapon.name == name);
        }

        /// <summary>
        /// 부위별 명중: 데미지 = (int)(위력 × 배율) + 난수, 조준 흐트러짐 값, 밀리는 속도, 탄환 소멸.
        /// </summary>
        private void CheckHitParts()
        {
            HumanGeneralData general = DataManager.Instance.HumanParameterData.humanGeneralData;
            var parts = new[]
            {
                (part: HumanHitPart.Head, name: "머리", reaction: general.headHitReaction),
                (part: HumanHitPart.Body, name: "상반신", reaction: general.bodyHitReaction),
                (part: HumanHitPart.Leg, name: "다리", reaction: general.legHitReaction),
            };

            foreach (var entry in parts)
            {
                if (!Reset()) { Expect(false, "부위 명중: 준비 실패"); return; }

                Human target = m_targets[0];
                HumanTypeData type = target.HumanTypeData;
                HitboxPartSizeData size = HumanHitbox.PartOf(target.HitboxSize, entry.part);
                // 탄환 높이(눈높이)에 부위 중심이 오도록 표적의 발 높이를 맞춘다.
                target.Controller.Teleport(new Vector3(0f, Eye.Y - size.position.Y, -5f));

                float hpBefore = target.HP;
                const int attacks = 30;
                Bullet bullet = FireStraight(attacks, 0);
                TickBullets(2);

                float multiplier = entry.part == HumanHitPart.Head ? type.headDamageMultiplier
                    : entry.part == HumanHitPart.Body ? type.bodyDamageMultiplier : type.legDamageMultiplier;
                IntRange add = entry.part == HumanHitPart.Head ? type.headRandomAddDamage
                    : entry.part == HumanHitPart.Body ? type.bodyRandomAddDamage : type.legRandomAddDamage;
                int baseDamage = (int)(attacks * multiplier);
                float dealt = hpBefore - target.HP;

                Expect(dealt >= baseDamage + add.min && dealt <= baseDamage + Mathf.Max(add.min, add.max - 1),
                    $"{entry.name} 명중 데미지 {dealt} (기대 {baseDamage + add.min}~{baseDamage + add.max - 1})");
                Expect(target.GunsightErrorRange == entry.reaction, $"{entry.name} 명중 후 조준 흐트러짐 {target.GunsightErrorRange} (기대 {entry.reaction})");
                Expect(target.Controller.MoveVelocity.IsEqualApprox(new Vector3(0f, 0f, -0.1f * SimClock.FrameRate)),
                    $"{entry.name} 명중 후 밀리는 속도 {target.Controller.MoveVelocity}");
                Expect(!bullet.IsActive, $"{entry.name} 명중 후 관통력 0 탄환이 남아 있음");
            }
        }

        /// <summary>
        /// 피격 시 조준 흐트러짐은 더 큰 쪽을 남기고, 회전을 준 판정 부위는 몸 방향을 따라 도는 기울어진 원기둥이 된다.
        /// </summary>
        private void CheckHitReactionAndRotation()
        {
            if (!Reset()) { Expect(false, "피격 반응: 준비 실패"); return; }

            Human target = m_targets[0];
            target.SetHitReaction(15f);
            target.SetHitReaction(8f);
            Expect(target.GunsightErrorRange == 15, $"큰 흐트러짐 뒤 작은 피격을 받은 값 {target.GunsightErrorRange} (기대 15)");

            // Z축으로 90° 눕힌 원기둥: 길이 1 m, 반지름 0.1 m, 중심 높이 1 m. 몸 방향 0° 에서는 좌우(X)로 눕는다.
            var lying = new HitboxPartSizeData { position = new Vector3(0f, 1f, 0f), rotationEuler = new Vector3(0f, 0f, 90f), height = 1f, radius = 0.1f };
            Vector3 feet = s_arena;
            Expect(HumanHitbox.Contains(lying, feet, 0f, feet + new Vector3(0.4f, 1f, 0f)), "눕힌 원기둥: 축 방향 0.4 m 지점이 밖으로 판정됨");
            Expect(!HumanHitbox.Contains(lying, feet, 0f, feet + new Vector3(0f, 1.4f, 0f)), "눕힌 원기둥: 위쪽 0.4 m 지점이 안으로 판정됨");
            // 몸을 90° 돌리면 원기둥도 앞뒤(Z)로 눕는다.
            Expect(HumanHitbox.Contains(lying, feet, 90f, feet + new Vector3(0f, 1f, 0.4f)) && !HumanHitbox.Contains(lying, feet, 90f, feet + new Vector3(0.4f, 1f, 0f)),
                "눕힌 원기둥이 몸 방향을 따라 돌지 않음");
        }

        /// <summary>
        /// 스침: 원기둥을 점 간격(0.25 m)보다 짧게 스치는 탄환은 판정 점이 원기둥 밖이면 빗나간다 (원본 점 샘플링의 특성).
        /// </summary>
        private void CheckGraze()
        {
            if (!Reset()) { Expect(false, "스침: 준비 실패"); return; }

            Human target = m_targets[0];
            HitboxPartSizeData leg = target.HitboxSize.leg;
            float feetY = Eye.Y - leg.position.Y;
            // 탄환 경로에서 옆으로 반지름보다 0.005 m 안쪽. 걸치는 길이는 약 0.1 m 다.
            float side = leg.radius - 0.005f;

            // 판정 점은 z = −0.25k 에 있다. 표적 중심을 두 점의 한가운데에 두면 어느 점도 원기둥에 들지 않는다.
            target.Controller.Teleport(new Vector3(side, feetY, -5f - k_substep * 0.5f));
            float hp = target.HP;
            Bullet bullet = FireStraight(30, 0);
            TickBullets(3);
            Expect(target.HP == hp && bullet.IsActive, $"점 사이로 스친 탄환이 명중함 (HP {hp} → {target.HP})");

            // 같은 옆 거리라도 중심이 판정 점과 같은 z 에 있으면 맞는다.
            BulletManager.Instance.Clear();
            target.Controller.Teleport(new Vector3(side, feetY, -5f));
            FireStraight(30, 0);
            TickBullets(3);
            Expect(target.HP < hp, "판정 점과 나란한 표적에 탄환이 맞지 않음");
        }

        /// <summary>
        /// 관통: 사람을 지날 때마다 관통력 −1, 위력 × 0.6(상반신) 정수 절단. 관통력이 0 미만이 되면 다음 사람에 닿기 전에 사라진다.
        /// </summary>
        private void CheckPenetration()
        {
            if (!Reset()) { Expect(false, "관통: 준비 실패"); return; }

            float feetY = Eye.Y - m_targets[0].HitboxSize.body.position.Y;
            var hp = new float[3];
            for (int i = 0; i < 3; i++)
            {
                m_targets[i].Controller.Teleport(new Vector3(0f, feetY, -5f - i * 2f));
                hp[i] = m_targets[i].HP;
            }

            IntRange add = m_targets[0].HumanTypeData.bodyRandomAddDamage;
            Bullet bullet = FireStraight(40, 1);
            TickBullets(4);

            float first = hp[0] - m_targets[0].HP;
            float second = hp[1] - m_targets[1].HP;
            Expect(first >= 40 && first <= 40 + add.max - 1, $"관통 1번째 데미지 {first} (기대 40~{40 + add.max - 1})");
            Expect(second >= 24 && second <= 24 + add.max - 1, $"관통 2번째 데미지 {second} (기대 24~{24 + add.max - 1}, 위력 40 × 0.6)");
            Expect(m_targets[2].HP == hp[2], "관통력 1 탄환이 세 번째 사람까지 맞힘");
            Expect(!bullet.IsActive, "관통력을 다 쓴 탄환이 남아 있음");

            // 같은 팀은 맞지 않는다.
            if (!Reset()) return;
            Human ally = null;
            foreach (Human human in MapLoader.Humans)
            {
                if (human != m_shooter && human.Alive && human.Team == m_shooter.Team) { ally = human; break; }
            }
            if (ally != null)
            {
                ally.Controller.Teleport(new Vector3(0f, feetY, -5f));
                float allyHp = ally.HP;
                FireStraight(40, 0);
                TickBullets(3);
                Expect(ally.HP == allyHp, "같은 팀 사람이 총알에 맞음");
            }
        }

        /// <summary>
        /// 연사 간격(틱 수), 단발 무기의 한 번 누름당 한 발, 산탄의 탄환 수와 위력.
        /// </summary>
        private void CheckFireRate()
        {
            if (!Reset()) { Expect(false, "연사: 준비 실패"); return; }

            int mp5 = FindWeapon("MP5");
            m_shooter.SetWeapon(m_shooter.SelectWeapon, mp5);
            WeaponData data = m_shooter.CurrentWeapon.Data;
            int interval = Mathf.RoundToInt(SimClock.FrameRate / data.fireRate);

            var shotTicks = new List<int>();
            for (int tick = 0; tick < 40; tick++)
            {
                int before = BulletManager.SpawnCount;
                m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
                m_shooter.TickWeapon();
                if (BulletManager.SpawnCount > before) shotTicks.Add(tick);
            }

            bool spacing = shotTicks.Count == 40 / interval;
            for (int i = 0; i < shotTicks.Count; i++) spacing &= shotTicks[i] == i * interval;
            Expect(spacing, $"MP5 연사 간격: 40틱에 {shotTicks.Count}발 [{string.Join(",", shotTicks)}] (기대 {interval}틱마다)");
            Expect(m_shooter.CurrentWeapon.Magazine == data.magazineSize - shotTicks.Count, $"MP5 탄창 {m_shooter.CurrentWeapon.Magazine}");

            // 단발 무기: 누르고 있어도 한 발. 한 틱 떼면 다시 쏜다.
            BulletManager.Instance.Clear();
            m_shooter.SetWeapon(m_shooter.SelectWeapon, FindWeapon("M92F"));
            Idle(40);
            int start = BulletManager.SpawnCount;
            for (int tick = 0; tick < 20; tick++)
            {
                m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
                m_shooter.TickWeapon();
            }
            Expect(BulletManager.SpawnCount - start == 1, $"단발 무기를 누르고 있는 동안 {BulletManager.SpawnCount - start}발 (기대 1)");
            Idle(1);
            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            Expect(BulletManager.SpawnCount - start == 2, "단발 무기를 뗐다가 다시 눌러도 쏘지 못함");

            // 산탄: 탄환 수와 탄환당 위력 = (int)(위력 / (탄환 수 / 2)).
            BulletManager.Instance.Clear();
            m_shooter.SetWeapon(m_shooter.SelectWeapon, FindWeapon("M1"));
            WeaponData shotgun = m_shooter.CurrentWeapon.Data;
            Idle(40);
            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            int active = BulletManager.Instance.CountActive();
            int expectedAttacks = (int)(shotgun.damage / (shotgun.pelletCount / 2f));
            bool attacksOk = true;
            for (int i = 0; i < BulletManager.PoolSize; i++)
            {
                Bullet bullet = BulletManager.Instance.GetBullet(i);
                if (bullet.IsActive) attacksOk &= bullet.Attacks == expectedAttacks;
            }
            Expect(active == shotgun.pelletCount && attacksOk, $"산탄 탄환 {active}발 (기대 {shotgun.pelletCount}발, 위력 {expectedAttacks})");
        }

        /// <summary>
        /// 재장전 시간과 탄약 계산, 재장전·슬롯 전환 중 발사 차단.
        /// </summary>
        private void CheckReloadAndSwitch()
        {
            if (!Reset()) { Expect(false, "재장전: 준비 실패"); return; }

            int slot = m_shooter.SelectWeapon;
            m_shooter.SetWeapon(slot, FindWeapon("MP5"), 20, 60);
            WeaponData data = m_shooter.CurrentWeapon.Data;

            m_shooter.QueueWeaponInput(HumanWeaponAction.Reload);
            int ticks = 0;
            int spawnBefore = BulletManager.SpawnCount;
            do
            {
                m_shooter.TickWeapon();
                ticks++;
                m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            }
            while (m_shooter.IsReloading && ticks < 1000);

            int expectedTicks = Mathf.RoundToInt(data.reloadTime * SimClock.FrameRate) + 1;
            Expect(ticks == expectedTicks, $"MP5 재장전 {ticks}틱 (기대 {expectedTicks})");
            Expect(BulletManager.SpawnCount == spawnBefore, "재장전 중에 발사됨");
            // 원본 방식: 탄창에 남은 20발은 버리고 예비 60발에서 30발을 채운다.
            Expect(m_shooter.CurrentWeapon.Magazine == data.magazineSize && m_shooter.CurrentWeapon.Reserve == 60 - data.magazineSize,
                $"재장전 후 탄약 {m_shooter.CurrentWeapon.Magazine}/{m_shooter.CurrentWeapon.Reserve} (기대 {data.magazineSize}/{60 - data.magazineSize})");

            // 탄창이 가득 차 있으면 재장전하지 않는다.
            m_shooter.ClearPendingWeaponInput();
            m_shooter.QueueWeaponInput(HumanWeaponAction.Reload);
            m_shooter.TickWeapon();
            Expect(!m_shooter.IsReloading && m_shooter.CurrentWeapon.Reserve == 60 - data.magazineSize, "가득 찬 탄창에서 재장전이 시작됨");

            // 슬롯 전환: 전환 시간 동안 발사가 막힌다.
            m_shooter.ClearPendingWeaponInput();
            int other = 1 - slot;
            m_shooter.SetWeapon(other, FindWeapon("M92F"));
            m_shooter.QueueWeaponInput(other == 0 ? HumanWeaponAction.SelectFirst : HumanWeaponAction.SelectSecond);
            ticks = 0;
            spawnBefore = BulletManager.SpawnCount;
            do
            {
                m_shooter.TickWeapon();
                ticks++;
                m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            }
            while (m_shooter.IsSwitchingWeapon && ticks < 1000);

            int switchTicks = Mathf.RoundToInt(m_shooter.CurrentWeapon.Data.slotChangeTime * SimClock.FrameRate);
            Expect(m_shooter.SelectWeapon == other && ticks == switchTicks, $"슬롯 전환 {ticks}틱 (기대 {switchTicks})");
            Expect(BulletManager.SpawnCount == spawnBefore, "슬롯 전환 중에 발사됨");

            // 무기 종류 전환(단발 ↔ 연발)과 치트 무기 교체: 종류만 바뀌고 탄약은 그대로다.
            int glock = FindWeapon("GLOCK18 SEMI");
            int target = DataManager.Instance.WeaponParameterData.weaponData[glock].nextWeaponIndex;
            m_shooter.SetWeapon(other, glock, 7, 11);
            Idle(40);
            m_shooter.QueueWeaponInput(HumanWeaponAction.SwitchNext);
            m_shooter.TickWeapon();
            Weapon switched = m_shooter.CurrentWeapon;
            Expect(switched.WeaponIndex == target && switched.Magazine == 7 && switched.Reserve == 11,
                $"무기 종류 전환 후 #{switched.WeaponIndex} {switched.Magazine}/{switched.Reserve} (기대 #{target} 7/11)");

            m_shooter.CheatCycleWeapon(1);
            Weapon cycled = m_shooter.CurrentWeapon;
            Expect(cycled.WeaponIndex == target + 1 && cycled.Magazine == 7 && cycled.Reserve == 11,
                $"치트 무기 교체 후 #{cycled.WeaponIndex} {cycled.Magazine}/{cycled.Reserve} (기대 #{target + 1} 7/11)");
            m_shooter.CheatAddMagazine();
            Expect(m_shooter.CurrentWeapon.Reserve == 11 + cycled.Data.magazineSize, "치트 탄약 추가 후 예비 탄이 장탄수만큼 늘지 않음");
        }

        /// <summary>
        /// 조준 오차: 무기별 하한, 반동 누적과 회복, 조준선이 없는 무기는 반동이 쌓이지 않음, 시점 반동, 탄환 방향이 오차 범위 안.
        /// </summary>
        private void CheckAimError()
        {
            if (!Reset()) { Expect(false, "조준 오차: 준비 실패"); return; }

            int slot = m_shooter.SelectWeapon;
            m_shooter.SetWeapon(slot, FindWeapon("MP5"));
            WeaponData mp5 = m_shooter.CurrentWeapon.Data;
            m_shooter.TickWeapon();
            Expect(m_shooter.CurrentErrorRange() == Mathf.RoundToInt(mp5.errorRange.min),
                $"MP5 정지 상태 오차 {m_shooter.CurrentErrorRange()} (기대 {mp5.errorRange.min})");

            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            Expect(m_shooter.GunsightErrorRange == Mathf.RoundToInt(mp5.recoil) - 1,
                $"MP5 한 발 뒤 반동 오차 {m_shooter.GunsightErrorRange} (기대 {mp5.recoil - 1}: 반동 {mp5.recoil} 에서 한 틱 회복)");

            // 연사하면 오차가 무기 상한을 넘지 않는다.
            for (int tick = 0; tick < 60; tick++)
            {
                m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
                m_shooter.TickWeapon();
            }
            Expect(m_shooter.GunsightErrorRange <= Mathf.RoundToInt(mp5.errorRange.max), $"MP5 연사 중 오차 {m_shooter.GunsightErrorRange} 가 상한 {mp5.errorRange.max} 초과");

            // 탄환 방향은 조준 방향에서 (오차 × 0.15°) 안쪽이다. 축마다 독립이라 대각선 여유를 둔다.
            float limit = Mathf.DegToRad(mp5.errorRange.max * Weapon.ErrorRangeUnitDegrees) * Mathf.Sqrt2 + 1e-4f;
            bool within = true;
            for (int i = 0; i < BulletManager.PoolSize; i++)
            {
                Bullet bullet = BulletManager.Instance.GetBullet(i);
                if (bullet.IsActive) within &= bullet.Direction.AngleTo(Vector3.Forward) <= limit;
            }
            Expect(within, "MP5 탄환 방향이 오차 상한을 벗어남");

            // PSG1: 반동이 오차로 쌓이고 시점도 위로 튄다.
            BulletManager.Instance.Clear();
            m_shooter.SetWeapon(slot, FindWeapon("PSG1"));
            WeaponData psg1 = m_shooter.CurrentWeapon.Data;
            Idle(60);
            Aim(0f, 0f);
            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            Expect(m_shooter.GunsightErrorRange == Mathf.RoundToInt(psg1.recoil) - 1,
                $"PSG1 한 발 뒤 오차 {m_shooter.GunsightErrorRange} (기대 {psg1.recoil - 1}: 반동 {psg1.recoil} 에서 한 틱 회복)");

            float pitchUp = -m_shooter.Controller.Pitch;
            float yaw = m_shooter.Controller.Yaw;
            Expect(pitchUp >= psg1.recoilAimVertical.min - 1e-3f && pitchUp < psg1.recoilAimVertical.max,
                $"PSG1 시점 상하 반동 {pitchUp} (기대 {psg1.recoilAimVertical.min}~{psg1.recoilAimVertical.max})");
            Expect(yaw >= psg1.recoilAimHorizontal.min - 1e-3f && yaw < psg1.recoilAimHorizontal.max,
                $"PSG1 시점 좌우 반동 {yaw} (기대 {psg1.recoilAimHorizontal.min}~{psg1.recoilAimHorizontal.max})");
            Bullet shot = FirstActiveBullet();
            Expect(shot != null && shot.Direction.AngleTo(Vector3.Forward) < 1e-4f, "PSG1 정지 사격이 조준 방향에서 벗어남 (반동 전 방향으로 나가야 함)");

            // 스코프: 스코프 무기에서만 켜지고, 재장전을 시작하면 풀린다.
            m_shooter.ToggleScope();
            Expect(m_shooter.IsScoping && m_shooter.ActiveScope != null, "PSG1 스코프가 켜지지 않음");
            m_shooter.QueueWeaponInput(HumanWeaponAction.Reload);
            m_shooter.TickWeapon();
            Expect(!m_shooter.IsScoping, "재장전을 시작해도 스코프가 풀리지 않음");
            m_shooter.SetWeapon(slot, FindWeapon("MP5"));
            m_shooter.ToggleScope();
            Expect(!m_shooter.IsScoping, "스코프 없는 무기에서 스코프가 켜짐");
        }

        private static Bullet FirstActiveBullet()
        {
            for (int i = 0; i < BulletManager.PoolSize; i++)
            {
                Bullet bullet = BulletManager.Instance.GetBullet(i);
                if (bullet.IsActive) return bullet;
            }
            return null;
        }

        /// <summary>
        /// 모든 무기를 한 번씩 장착해 쏴 본다. 쏠 수 있는 무기는 탄환 수만큼 나가고, 수류탄은 예비 탄이 없으면 쏜 뒤 사라진다.
        /// </summary>
        private void CheckAllWeapons()
        {
            if (!Reset()) { Expect(false, "전체 무기: 준비 실패"); return; }

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            int slot = m_shooter.SelectWeapon;

            for (int index = 0; index < parameter.weaponData.Count; index++)
            {
                WeaponData data = parameter.weaponData[index];
                BulletManager.Instance.Clear();
                m_shooter.SetWeapon(slot, index);
                Idle(40);

                int before = BulletManager.SpawnCount;
                m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
                m_shooter.TickWeapon();
                int spawned = BulletManager.SpawnCount - before;

                bool canFire = data.fireRate > 0f && data.magazineSize > 0 && data.pelletCount > 0 && index != parameter.weaponGeneralData.noneWeaponIndex;
                Expect(spawned == (canFire ? data.pelletCount : 0), $"[{index}] {data.name}: 탄환 {spawned}발 (기대 {(canFire ? data.pelletCount : 0)})");
            }

            int grenade = parameter.weaponGeneralData.grenadeWeaponIndex;
            BulletManager.Instance.Clear();
            m_shooter.SetWeapon(slot, grenade, 1, 1);
            Idle(40);
            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            Expect(m_shooter.CurrentWeapon.WeaponIndex == grenade && m_shooter.CurrentWeapon.Magazine == 1 && m_shooter.CurrentWeapon.Reserve == 0,
                $"수류탄 1+1개에서 하나 던진 뒤 {m_shooter.CurrentWeapon.Magazine}/{m_shooter.CurrentWeapon.Reserve} (기대 1/0)");
            Idle(40);
            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            Expect(m_shooter.CurrentWeapon.IsNone, "마지막 수류탄을 던진 뒤에도 무기가 남아 있음");
        }

        /// <summary>
        /// 폭발: 시한(수명 틱), 발·머리 두 점의 거리 비례 데미지, 폭풍 방향.
        /// </summary>
        private void CheckExplosion()
        {
            if (!Reset()) { Expect(false, "폭발: 준비 실패"); return; }

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            BulletData data = parameter.bulletData[parameter.weaponData[parameter.weaponGeneralData.grenadeWeaponIndex].bulletIndex];
            Human target = m_targets[0];
            float height = target.Controller.Height;

            // 사수는 폭발 범위 밖으로 치운다.
            m_shooter.Controller.Teleport(s_arena + new Vector3(-100f, 0f, 0f));
            target.Controller.Teleport(s_arena);

            // 멈춰 있는 수류탄은 수명만 센다. 수명 틱을 넘긴 다음 틱에 터진다 (원본 cnt > GRENADE_DESTROYFRAME).
            Vector3 origin = s_arena + new Vector3(6f, 0.5f, 0f);
            float hp = target.HP;
            int explosions = BulletManager.ExplosionCount;
            BulletManager.Instance.Spawn(data, m_shooter, m_shooter.Team, 0, 0, origin, 0f, 0f, 0f, origin);
            int ticks = 0;
            while (BulletManager.ExplosionCount == explosions && ticks < 1000)
            {
                BulletManager.Instance.SimTick();
                ticks++;
            }
            int lifeTicks = Mathf.RoundToInt(data.lifetime * SimClock.FrameRate);
            Expect(ticks == lifeTicks + 2, $"수류탄 폭발까지 {ticks}틱 (기대 {lifeTicks + 2})");

            int expected = ExpectedExplosionDamage(origin, s_arena + Vector3.Up * 0.2f, data.explosionRadius, data.humanExplosiveLegDamageMax)
                         + ExpectedExplosionDamage(origin, s_arena + Vector3.Up * (height - 0.2f), data.explosionRadius, data.humanExplosiveHeadDamageMax);
            Expect(hp - target.HP == expected, $"폭발 데미지 {hp - target.HP} (기대 {expected})");
            Expect(target.GunsightErrorRange == DataManager.Instance.HumanParameterData.humanGeneralData.grenadeHitReaction, "폭발 후 조준 흐트러짐 값이 다름");

            // 폭발이 발보다 위에 있으면 멀어지는 쪽(−X)으로 수평으로만 민다.
            Vector3 level = target.Controller.MoveVelocity;
            Expect(level.X < 0f && Mathf.IsZeroApprox(level.Y), $"높은 폭발의 폭풍 방향 {level} (기대: 멀어지는 쪽 수평)");

            // 폭발이 발 높이 아래면 멀어지면서 위로 민다.
            if (!Reset()) return;
            target = m_targets[0];
            m_shooter.Controller.Teleport(s_arena + new Vector3(-100f, 0f, 0f));
            target.Controller.Teleport(s_arena);
            origin = s_arena + new Vector3(5f, -1f, 0f);
            BulletManager.Instance.Spawn(data, m_shooter, m_shooter.Team, 0, 0, origin, 0f, 0f, 0f, origin);
            TickBullets(lifeTicks + 2);
            Vector3 pushed = target.Controller.MoveVelocity;
            Expect(pushed.X < 0f && pushed.Y > 0f, $"낮은 폭발의 폭풍 방향 {pushed} (기대: 멀어지며 위로)");
        }

        private static int ExpectedExplosionDamage(Vector3 origin, Vector3 point, float radius, float max)
        {
            int damage = (int)max - (int)(max / radius * (point - origin).Length());
            return damage > 0 ? damage : 0;
        }

        /// <summary>
        /// 수류탄 비행: 맵 위에서 던져 블록에 박히지 않고 튀다가 수명에 맞춰 터지는지 본다.
        /// </summary>
        private void CheckGrenadeFlight()
        {
            MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path);
            GameRandom.Reseed(1u);
            Human player = MapLoader.Player;
            if (player == null) { Expect(false, "수류탄 비행: 플레이어 없음"); return; }

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            WeaponData weapon = parameter.weaponData[parameter.weaponGeneralData.grenadeWeaponIndex];
            BulletData data = parameter.bulletData[weapon.bulletIndex];

            Vector3 start = player.Controller.Position + Vector3.Up * player.CameraHeight;
            Bullet grenade = BulletManager.Instance.Spawn(data, player, player.Team, 0, 0, start,
                player.Controller.Yaw, 0f, weapon.bulletSpeed * SimClock.FrameTime, start);

            int explosions = BulletManager.ExplosionCount;
            int bounces = 0;
            bool embedded = false;
            float lowest = start.Y;
            float previousY = start.Y;
            bool falling = false;
            int ticks = 0;
            while (grenade.IsActive && ticks < 1000)
            {
                BulletManager.Instance.SimTick();
                ticks++;
                if (!grenade.IsActive) break;

                Vector3 position = grenade.Position;
                if (MapLoader.IsInsideBlock(position)) embedded = true;
                lowest = Mathf.Min(lowest, position.Y);
                if (position.Y < previousY - 1e-5f) falling = true;
                else if (falling && position.Y > previousY + 1e-5f)
                {
                    bounces++;
                    falling = false;
                }
                previousY = position.Y;
            }

            int lifeTicks = Mathf.RoundToInt(data.lifetime * SimClock.FrameRate);
            Expect(BulletManager.ExplosionCount == explosions + 1 && ticks == lifeTicks + 2, $"던진 수류탄이 {ticks}틱에 터짐 (기대 {lifeTicks + 2})");
            Expect(!embedded, "수류탄이 블록 안으로 들어감");
            Expect(bounces >= 1, $"수류탄이 바닥에서 튀지 않음 (최저 높이 {lowest:0.00}, 시작 {start.Y:0.00})");
            GD.Print($"수류탄 비행: {ticks}틱, 튐 {bounces}회, 폭발 위치 {BulletManager.LastExplosionPosition}");
        }

        /// <summary>
        /// 벽: 맵에서 실제 블록을 찾아 두꺼운 벽(내부 점마다 관통력 −1, 위력 × 0.6)과 얇은 벽(관통력 그대로, 위력 × 0.55 / 0.75)을 확인한다.
        /// </summary>
        private void CheckWalls()
        {
            BulletData data = DataManager.Instance.WeaponParameterData.bulletData[0];
            const float speed = 3f;
            int steps = Mathf.RoundToInt(speed / k_substep);
            bool thickDone = false;
            bool thinDone = false;
            int missionCount = DataManager.Instance.MissionData.officialMissions.Count;

            // 얇은 벽(판정 점 사이에 끼는 블록)은 드물어서 여러 미션의 맵을 뒤진다.
            for (int mission = 0; mission < missionCount && !(thickDone && thinDone); mission++)
            {
                if (!MapLoader.LoadMissionData(mission, false, 0)) continue;
                if (!MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path)) continue;
                if (!MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path)) continue;
                Human owner = MapLoader.Player;
                if (owner == null) continue;

                foreach (Human human in MapLoader.Humans)
                {
                    Vector3 origin = human.Controller.Position + Vector3.Up * human.CameraHeight;
                    if (MapLoader.IsInsideBlock(origin)) continue;

                    for (float yaw = 0f; yaw < 360f && !(thickDone && thinDone); yaw += 3f)
                    {
                        for (float pitch = -30f; pitch <= 70f && !(thickDone && thinDone); pitch += 10f)
                        {
                            Vector3 direction = Coord.AimDirection(yaw, pitch);
                            if (!MapLoader.RaycastBlock(origin, direction, speed, out _)) continue;

                            int inside = 0;
                            for (int step = 1; step <= steps; step++)
                            {
                                if (MapLoader.IsInsideBlock(origin + direction * (k_substep * step))) inside++;
                            }

                            if (inside >= 1 && !thickDone)
                            {
                                thickDone = true;

                                // 관통력이 넉넉하면 내부 점 수만큼 관통력이 줄고 위력이 × 0.6 씩 잘린다.
                                int attacks = 100;
                                for (int i = 0; i < inside; i++) attacks = (int)(attacks * 0.6f);
                                Bullet strong = BulletManager.Instance.Spawn(data, owner, -99, 100, inside + 5, origin, yaw, pitch, speed, origin);
                                strong.Tick();
                                Expect(strong.IsActive && strong.Penetration == 5 && strong.Attacks == attacks,
                                    $"두꺼운 벽(내부 점 {inside}개) 통과 후 관통력 {strong.Penetration}, 위력 {strong.Attacks} (기대 5, {attacks})");

                                // 관통력 0 이면 벽에서 사라진다.
                                Bullet weak = BulletManager.Instance.Spawn(data, owner, -99, 100, 0, origin, yaw, pitch, speed, origin);
                                weak.Tick();
                                weak.Tick();
                                Expect(!weak.IsActive, "관통력 0 탄환이 두꺼운 벽을 지나감");
                                GD.Print($"두꺼운 벽 사례: 미션 {mission}, 내부 점 {inside}개");
                            }
                            else if (inside == 0 && !thinDone)
                            {
                                thinDone = true;

                                Bullet stop = BulletManager.Instance.Spawn(data, owner, -99, 100, 0, origin, yaw, pitch, speed, origin);
                                stop.Tick();
                                Expect(stop.IsActive && stop.Penetration == 0 && stop.Attacks == 55, $"얇은 벽 통과(관통력 0) 후 위력 {stop.Attacks} (기대 55)");

                                Bullet pierce = BulletManager.Instance.Spawn(data, owner, -99, 100, 1, origin, yaw, pitch, speed, origin);
                                pierce.Tick();
                                Expect(pierce.IsActive && pierce.Penetration == 1 && pierce.Attacks == 75, $"얇은 벽 통과(관통력 1) 후 위력 {pierce.Attacks} (기대 75)");
                                GD.Print($"얇은 벽 사례: 미션 {mission}, 위치 {origin}, yaw {yaw}, pitch {pitch}");
                            }
                            BulletManager.Instance.Clear();
                        }
                    }
                    if (thickDone && thinDone) break;
                }
            }

            Expect(thickDone, "두꺼운 벽 사례를 맵에서 찾지 못해 확인하지 못함");
            Expect(thinDone, "얇은 벽 사례를 맵에서 찾지 못해 확인하지 못함");
        }

        /// <summary>
        /// 떨어진 무기: 버리면 앞으로 날아가 바닥 바로 위에 멈추고, 맨손인 사람이 범위 안에 들어오면 탄약째로 줍는다. 죽으면 든 무기가 모두 떨어진다.
        /// </summary>
        private void CheckDropAndPickup()
        {
            MapLoader.LoadMissionData(0, false, 0);
            MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path);
            MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path);
            GameRandom.Reseed(1u);

            Human player = MapLoader.Player;
            if (player == null) { Expect(false, "떨어진 무기: 플레이어 없음"); return; }

            int mp5 = FindWeapon("MP5");
            int slot = player.SelectWeapon;
            player.SetWeapon(slot, mp5, 12, 34);
            int before = WeaponManager.Instance.CountActive();
            Vector3 start = player.Controller.Position;

            Expect(player.DropCurrentWeapon() && player.CurrentWeapon.IsNone, "무기를 버렸는데 슬롯이 비지 않음");
            Expect(WeaponManager.Instance.CountActive() == before + 1, "버린 무기가 맵에 생기지 않음");

            // 버린 무기를 찾는다 (탄약 12/34 인 MP5).
            int dropped = -1;
            for (int i = 0; i < WeaponManager.PoolSize; i++)
            {
                if (WeaponManager.Instance.TryGetDropped(i, out Weapon weapon, out _, out _)
                    && weapon.WeaponIndex == mp5 && weapon.Magazine == 12 && weapon.Reserve == 34)
                {
                    dropped = i;
                }
            }
            if (dropped < 0) { Expect(false, "버린 무기를 풀에서 찾지 못함"); return; }

            int ticks = 0;
            bool falling = true;
            Vector3 landed = Vector3.Zero;
            while (falling && ticks < 300)
            {
                WeaponManager.Instance.SimTick();
                ticks++;
                if (!WeaponManager.Instance.TryGetDropped(dropped, out _, out landed, out falling)) break;
            }

            float margin = DataManager.Instance.WeaponParameterData.weaponDropPhysicsData.groundCollisionMargin;
            bool grounded = MapLoader.RaycastBlock(landed, Vector3.Down, 1f, out float groundDist);
            Expect(!falling && grounded && Mathf.Abs(groundDist - margin) < 1e-3f && !MapLoader.IsInsideBlock(landed),
                $"버린 무기가 {ticks}틱 뒤 바닥 위 {groundDist:0.000} m 에 멈춤 (기대 {margin})");
            float thrown = new Vector2(landed.X - start.X, landed.Z - start.Z).Length();
            Expect(thrown > 0.5f, $"버린 무기가 앞으로 날아가지 않음 (수평 {thrown:0.00} m)");
            Expect(player.CurrentWeapon.IsNone, "멀리 있는 무기를 주움");
            GD.Print($"버린 무기: {ticks}틱 뒤 착지, 수평 {thrown:0.00} m");

            // 무기 자리로 가면 줍는다.
            player.Controller.Teleport(landed - Vector3.Up * margin);
            WeaponManager.Instance.SimTick();
            Weapon picked = player.CurrentWeapon;
            Expect(picked.WeaponIndex == mp5 && picked.Magazine == 12 && picked.Reserve == 34 && player.IsSwitchingWeapon,
                $"주운 무기 #{picked.WeaponIndex} {picked.Magazine}/{picked.Reserve} (기대 #{mp5} 12/34, 전환 중)");
            Expect(!WeaponManager.Instance.TryGetDropped(dropped, out _, out _, out _), "주운 무기가 맵에 남아 있음");

            // 사망: 든 무기가 모두 떨어진다.
            Human victim = null;
            foreach (Human human in MapLoader.Humans)
            {
                if (human != player && human.Alive) { victim = human; break; }
            }
            if (victim == null) return;

            victim.SetWeapon(0, FindWeapon("M92F"));
            victim.SetWeapon(1, mp5);
            before = WeaponManager.Instance.CountActive();
            victim.ApplyDamage(victim.HP);
            victim.Controller.SimTick();
            Expect(WeaponManager.Instance.CountActive() == before + 2, $"사망 시 떨어진 무기 {WeaponManager.Instance.CountActive() - before}개 (기대 2)");
        }

        /// <summary>
        /// 소물: 총알이 판정 형상을 지나는 점마다 데미지(위력 × 배율)를 주고 위력이 줄어든다. 내구력이 다하면 부서져 판정에서 빠진다.
        /// </summary>
        private void CheckSmallObjects()
        {
            // 소물이 있는 미션을 찾는다.
            int missionCount = DataManager.Instance.MissionData.officialMissions.Count;
            SmallObject target = null;
            Vector3 origin = Vector3.Zero;
            float yaw = 0f;
            int spawned = 0;

            for (int mission = 0; mission < missionCount && target == null; mission++)
            {
                if (!MapLoader.LoadMissionData(mission, false, 0)) continue;
                if (!MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path)) continue;
                if (!MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path)) continue;
                spawned = MapLoader.SmallObjects.Count;

                foreach (SmallObject candidate in MapLoader.SmallObjects)
                {
                    // 소물 중심에서 0.5 m 떨어진 곳에서 중심을 향해 쏠 수 있는 방향을 찾는다 (사이에 블록이 없어야 한다).
                    for (float tryYaw = 0f; tryYaw < 360f && target == null; tryYaw += 45f)
                    {
                        Vector3 direction = Coord.AimDirection(tryYaw, 0f);
                        Vector3 from = candidate.LogicPosition - direction * 0.5f;
                        if (MapLoader.IsInsideBlock(from) || MapLoader.RaycastBlock(from, direction, 1.5f, out _)) continue;
                        if (!candidate.Contains(candidate.LogicPosition)) continue;

                        target = candidate;
                        origin = from;
                        yaw = tryYaw;
                    }
                    if (target != null) break;
                }
            }

            if (target == null) { Expect(false, "소물: 쏠 수 있는 소물을 찾지 못해 확인하지 못함"); return; }
            GameRandom.Reseed(1u);

            ObjectGeneralData general = DataManager.Instance.ObjectParameterData.objectGeneralData;
            Vector3 aim = Coord.AimDirection(yaw, 0f);

            // 기대값: 한 틱(12점) 동안 판정 형상 안에 드는 점마다 데미지와 감쇠를 적용한다. 경로에 다른 소물이 있으면 그것도 위력을 깎는다.
            const int attacks = 20;
            int expectedAttacks = attacks;
            IReadOnlyList<SmallObject> all = MapLoader.SmallObjects;
            var remaining = new float[all.Count];
            for (int i = 0; i < all.Count; i++) remaining[i] = all[i].HP;
            int targetIndex = 0;
            int hits = 0;
            for (int step = 1; step <= 12; step++)
            {
                Vector3 point = origin + aim * (k_substep * step);
                for (int i = 0; i < all.Count; i++)
                {
                    if (remaining[i] <= 0f || !all[i].Contains(point)) continue;

                    remaining[i] = Mathf.Max(0f, remaining[i] - Mathf.FloorToInt(expectedAttacks * general.bulletDamageMultiplier));
                    expectedAttacks = Mathf.FloorToInt(expectedAttacks * general.bulletPenetrationAttenuation);
                    if (all[i] == target)
                    {
                        targetIndex = i;
                        hits++;
                    }
                }
            }
            float expectedHp = remaining[targetIndex];

            BulletData data = DataManager.Instance.WeaponParameterData.bulletData[0];
            int effects = EffectManager.SpawnCount;
            Bullet bullet = BulletManager.Instance.Spawn(data, null, -99, attacks, 0, origin, yaw, 0f, 3f, origin);
            bullet.Tick();

            Expect(hits >= 1 && target.HP == expectedHp && bullet.Attacks == expectedAttacks && bullet.Penetration == 0,
                $"소물 {target.ObjectData.name}: 명중 {hits}회 뒤 내구력 {target.HP}, 총알 위력 {bullet.Attacks} (기대 {expectedHp}, {expectedAttacks})");
            Expect(EffectManager.SpawnCount > effects, "소물 명중 이펙트가 나오지 않음");
            GD.Print($"소물 점검: {MapLoader.Instance.MissionName} 의 소물 {spawned}개 중 {target.ObjectData.name} at {target.LogicPosition}, 명중 {hits}회");

            target.HitBullet(100000);
            Expect(target.IsDestroyed && !target.Contains(target.LogicPosition), "부서진 소물이 판정에 남아 있음");

            // 폭발: 가려지지 않은 소물은 거리 비례 데미지를 받는다.
            SmallObject other = null;
            foreach (SmallObject candidate in MapLoader.SmallObjects)
            {
                if (!candidate.IsDestroyed) { other = candidate; break; }
            }
            if (other == null) return;

            WeaponParameterData weapons = DataManager.Instance.WeaponParameterData;
            BulletData grenade = weapons.bulletData[weapons.weaponData[weapons.weaponGeneralData.grenadeWeaponIndex].bulletIndex];
            Vector3 blast = other.LogicPosition + Vector3.Up * 0.3f;
            float hp = other.HP;
            int expectedDamage = (int)grenade.objectExplosiveDamageMax - (int)(grenade.objectExplosiveDamageMax / grenade.explosionRadius * 0.3f);
            BulletManager.Instance.Spawn(grenade, null, -99, 0, 0, blast, 0f, 0f, 0f, blast);
            TickBullets(Mathf.RoundToInt(grenade.lifetime * SimClock.FrameRate) + 2);
            Expect(other.HP == Mathf.Max(0f, hp - expectedDamage), $"폭발 뒤 소물 내구력 {other.HP} (기대 {Mathf.Max(0f, hp - expectedDamage)})");
        }

        /// <summary>
        /// 통계(발사·명중·헤드샷·킬)와, 격발·착탄·폭발에서 이펙트와 소리가 호출되는지, 소리의 거리 감쇠가 선형인지 확인한다.
        /// </summary>
        private void CheckStatsEffectsSounds()
        {
            MapLoader.LoadMissionData(0, false, 0);
            MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path);
            if (!Reset()) { Expect(false, "통계: 준비 실패"); return; }

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            int mp5 = FindWeapon("MP5");
            m_shooter.SetWeapon(m_shooter.SelectWeapon, mp5);
            WeaponData data = m_shooter.CurrentWeapon.Data;
            Idle(5);

            // 표적의 머리를 눈높이에 두고 HP 를 1 로 깎아 둔다. 한 발로 명중·헤드샷·킬이 모두 기록돼야 한다.
            Human target = m_targets[0];
            target.Controller.Teleport(new Vector3(0f, Eye.Y - target.HitboxSize.head.position.Y, -5f));
            target.ApplyDamage(target.HP - 1f);

            int sounds = SoundManager.PlayCount;
            Aim(0f, 0f);
            m_shooter.QueueWeaponInput(HumanWeaponAction.Fire);
            m_shooter.TickWeapon();
            Expect(SoundManager.PlayCount == sounds + 1 && SoundManager.LastPlayedPath == data.soundPath, $"격발음 호출: {SoundManager.LastPlayedPath} (기대 {data.soundPath})");

            // 조준 오차로 빗나가지 않도록, 통계용 한 발은 오차 없이 직접 쏜다.
            BulletManager.Instance.Clear();
            int effects = EffectManager.SpawnCount;
            BulletData bulletData = parameter.bulletData[data.bulletIndex];
            BulletManager.Instance.Spawn(bulletData, m_shooter, m_shooter.Team, 30, 0, Eye, 0f, 0f, 3f, Eye);
            TickBullets(2);

            MissionStats stats = MapLoader.Stats;
            Expect(stats.Fire == 1 && stats.OnTarget == 1f && stats.Headshot == 1 && stats.Kill == 1,
                $"통계: 발사 {stats.Fire}, 명중 {stats.OnTarget}, 헤드샷 {stats.Headshot}, 킬 {stats.Kill} (기대 1, 1, 1, 1)");
            Expect(EffectManager.SpawnCount > effects, "사람 명중 혈흔 이펙트가 나오지 않음");
            Expect(bulletData.humanHitSounds.Contains(SoundManager.LastPlayedPath), $"피격음 호출: {SoundManager.LastPlayedPath}");

            // 다른 사람이 쏜 것은 통계에 넣지 않는다.
            BulletManager.Instance.Spawn(bulletData, m_targets[1], m_targets[1].Team, 30, 0, Eye + Vector3.Up * 50f, 0f, 0f, 3f, Eye);
            MapLoader.RecordFire(m_targets[1]);
            Expect(stats.Fire == 1, "플레이어가 아닌 사람의 발사가 통계에 들어감");

            // 폭발 이펙트와 폭발음.
            BulletData grenade = parameter.bulletData[parameter.weaponData[parameter.weaponGeneralData.grenadeWeaponIndex].bulletIndex];
            Vector3 blast = s_arena + new Vector3(200f, 0f, 0f);
            effects = EffectManager.SpawnCount;
            BulletManager.Instance.Clear();
            BulletManager.Instance.Spawn(grenade, m_shooter, m_shooter.Team, 0, 0, blast, 0f, 0f, 0f, blast);
            TickBullets(Mathf.RoundToInt(grenade.lifetime * SimClock.FrameRate) + 2);
            Expect(SoundManager.LastPlayedPath == grenade.explosionSound, $"폭발음 호출: {SoundManager.LastPlayedPath}");
            int expectedEffects = DataManager.Instance.EffectParameterData.effectData[grenade.explosionEffectIndex].emitters.Count;
            Expect(EffectManager.SpawnCount - effects == expectedEffects, $"폭발 이펙트 {EffectManager.SpawnCount - effects}개 (기대 {expectedEffects})");

            // 소리의 거리 감쇠: 1 m 까지 최대, 33.5 m 에서 0, 그 사이 선형.
            Expect(Mathf.IsEqualApprox(SoundManager.Attenuation(new Vector3(0.5f, 0f, 0f), Vector3.Zero), 1f)
                && Mathf.IsEqualApprox(SoundManager.Attenuation(new Vector3(17.25f, 0f, 0f), Vector3.Zero), 0.5f)
                && SoundManager.Attenuation(new Vector3(40f, 0f, 0f), Vector3.Zero) == 0f,
                "소리의 거리 감쇠가 선형(1 m ~ 33.5 m)이 아님");
        }

        /// <summary>
        /// 사망: HP 가 0 이 된 다음 틱에 쓰러지기 시작하고 든 무기가 사라진다.
        /// </summary>
        private void CheckDeath()
        {
            MapLoader.LoadMissionData(0, false, 0);
            MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path);
            if (!Reset()) { Expect(false, "사망: 준비 실패"); return; }

            Human target = m_targets[0];
            target.SetWeapon(target.SelectWeapon, FindWeapon("MP5"));
            target.ApplyDamage(target.HP);
            target.Controller.SimTick();

            Expect(target.DeadState == HumanDeadState.Falling, $"HP 0 다음 틱의 상태 {target.DeadState}");
            Expect(target.GetWeapon(0).IsNone && target.GetWeapon(1).IsNone, "사망 후에도 무기를 들고 있음");

            int before = BulletManager.SpawnCount;
            target.QueueWeaponInput(HumanWeaponAction.Fire);
            target.Controller.SimTick();
            Expect(BulletManager.SpawnCount == before, "죽은 사람이 발사함");
        }
    }
}
