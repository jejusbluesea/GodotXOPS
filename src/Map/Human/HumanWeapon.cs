using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    // Human 의 무기 슬롯(장착·전환·재장전·발사) 담당 partial. 원본 OpenXOPS human 클래스의 무기 함수들과 ObjectManager::ShotWeapon 에 해당한다.
    public partial class Human
    {
        // 들 수 있는 무기 수. 원본 TOTAL_HAVEWEAPON. 슬롯 0 = 보조, 슬롯 1 = 주 무기.
        public const int WeaponSlotCount = 2;

        // 무기를 버리거나 떨어뜨릴 때 놓는 자리: 던지는 방향으로 0.5 m, 위로 1.6 m (원본 weapon::Dropoff, object.cpp:2308-2322 — 5.0, 16.0).
        private const float k_dropForwardOffset = 0.5f;
        private const float k_dropHeight = 1.6f;
        // 사망 시 무기가 흩어지는 수평 속도 (m/s). 원본 Dropoff(..., 1.5f) = 프레임당 1.5.
        private const float k_deathDropSpeed = 1.5f * Coord.Scale * SimClock.FrameRate;
        // 기다리는 탄피 묶음의 상한. 넘으면 새 탄피를 내지 않는다 (지연을 아주 길게 준 데이터가 목록을 끝없이 키우지 않게).
        private const int k_maxPendingShells = 64;

        private readonly Weapon[] m_weapons = new Weapon[WeaponSlotCount];
        private readonly WeaponVisual[] m_weaponVisuals = new WeaponVisual[WeaponSlotCount];
        private int m_selectWeapon;

        // 아래 카운터는 모두 틱 수다. 원본 selectweaponcnt / changeweaponidcnt / weaponreloadcnt / weaponshotcnt.
        private int m_selectWeaponTicks;
        private int m_changeIdTicks;
        private int m_reloadTicks;
        private int m_shotTicks;
        // 발사 입력이 이어지는 동안 쏜 발 수와, 이번 틱에 발사 입력이 있었는지 (원본 weaponburstmodecnt / weaponshotframe).
        private int m_burstShots;
        private bool m_shotRequested;
        // 발사 입력이 한 발씩 장전을 끊었는지. 조작하는 쪽이 확인하면 지워진다.
        private bool m_reloadInterrupted;

        // 다음 틱이 소비할 무기 입력. 렌더 프레임마다 OR 로 쌓인다.
        private HumanWeaponAction m_pendingWeapon;

        // 틱에서 쏜 뒤 화면 갱신 때 낼 발사 이펙트. 무기 모델이 틱 사이를 보간해 움직이므로, 이펙트도 그 프레임의 모델 위치에서 낸다.
        private WeaponModelData m_fireEffectModel;
        // 나오기를 기다리는 탄피들. 지연이 발사 간격보다 길어도 앞의 탄피가 사라지지 않게 전부 들고 있는다.
        private readonly List<PendingShell> m_pendingShells = new List<PendingShell>();

        /// <summary>
        /// 나오기를 기다리는 탄피 한 묶음.
        /// </summary>
        private sealed class PendingShell
        {
            public WeaponModelData model;
            // 남은 시간 (초).
            public float delay;
            public int count;
        }

        public Weapon CurrentWeapon => m_weapons[m_selectWeapon];
        public int SelectWeapon => m_selectWeapon;
        public bool IsSwitchingWeapon => m_selectWeaponTicks > 0 || m_changeIdTicks > 0;
        public bool IsReloading => m_reloadTicks > 0;
        public bool IsChanging => IsSwitchingWeapon || IsReloading;
        // 재장전·무기 종류 전환 중이라 팔을 내린 자세로 잡아 둬야 하는지 (원본 MotionCtrl 에 넘기는 ReloadCnt / ChangeWeaponIDCnt).
        public bool ArmHeld => !CurrentWeapon.IsNone && (m_reloadTicks > 0 || m_changeIdTicks > 0);
        // 떨어진 무기를 주울 수 있는 상태인지. 현재 슬롯이 맨손이고, 전환·재장전 중이 아니고, 무기를 주울 수 있는 종류여야 한다 (원본은 좀비가 줍지 못한다).
        public bool CanPickupWeapon => Alive && m_hp > 0f && !IsChanging && CurrentWeapon.IsNone
            && (m_humanTypeData == null || m_humanTypeData.canPickupWeapon);

        /// <summary>
        /// 슬롯 번호로 무기를 조회한다.
        /// </summary>
        /// <param name="slot">슬롯 번호 (0 또는 1).</param>
        /// <returns>그 슬롯의 무기. 빈 슬롯은 맨손 무기다.</returns>
        public Weapon GetWeapon(int slot)
        {
            return m_weapons[Mathf.Clamp(slot, 0, WeaponSlotCount - 1)];
        }

        /// <summary>
        /// 이번 프레임의 무기 입력을 다음 틱 소비 대기열에 쌓는다. 발사·오차·반동 난수가 틱에서만 뽑히게 하려고 실행은 틱이 한다.
        /// </summary>
        /// <param name="action">무기 입력 플래그.</param>
        public void QueueWeaponInput(HumanWeaponAction action)
        {
            m_pendingWeapon |= action;
        }

        /// <summary>
        /// 대기 중인 무기 입력을 버린다. 조작 대상이 바뀔 때 옛 사람에게 남은 발사 입력이 뒤늦게 나가지 않게 한다.
        /// </summary>
        public void ClearPendingWeaponInput()
        {
            m_pendingWeapon = HumanWeaponAction.None;
        }

        /// <summary>
        /// 한 틱의 무기 처리. HumanController 가 이동 계산 앞에서 부른다.
        /// 원본 한 프레임의 순서와 같다: 입력 처리(발사 등) → human::ProcessObject 앞부분의 카운터 감소·재장전 완료·조준 오차 갱신 (object.cpp:2000-2052).
        /// </summary>
        public void TickWeapon()
        {
            if (!Alive || m_hp <= 0f)
            {
                m_pendingWeapon = HumanWeaponAction.None;
            }
            else if (m_pendingWeapon != HumanWeaponAction.None)
            {
                HumanWeaponAction action = m_pendingWeapon;
                m_pendingWeapon = HumanWeaponAction.None;
                ApplyWeaponAction(action);
            }

            if (m_selectWeaponTicks > 0) m_selectWeaponTicks--;
            if (m_changeIdTicks > 0) m_changeIdTicks--;
            if (m_shotTicks > 0) m_shotTicks--;

            // 이번 틱에 발사 입력이 없었으면 연속 발사 수를 0 으로 되돌린다.
            if (!m_shotRequested) m_burstShots = 0;
            m_shotRequested = false;

            if (m_reloadTicks > 0)
            {
                m_reloadTicks--;
                if (m_reloadTicks == 0 && !CurrentWeapon.IsNone)
                {
                    // 한 발씩 장전하는 무기는 한 발을 넣고, 더 넣을 것이 있으면 다음 한 발의 시간을 다시 센다.
                    if (!CurrentWeapon.ShellByShell) CurrentWeapon.RunReload();
                    else if (CurrentWeapon.LoadShell()) m_reloadTicks = ShellReloadTicks(CurrentWeapon.Data);
                }
            }

            TickGunsightErrorRange();
        }

        /// <summary>
        /// 무기 입력 플래그를 실제 동작으로 옮긴다. 순서는 원본 입력 처리 순서다 (gamemain.cpp:2235-2288):
        /// 발사 → 재장전 → 슬롯 선택 → 무기 종류 전환 → 버리기 → 스코프.
        /// </summary>
        /// <param name="action">무기 입력 플래그.</param>
        public void ApplyWeaponAction(HumanWeaponAction action)
        {
            if ((action & HumanWeaponAction.Fire) != 0) ShotWeapon(true);
            if ((action & HumanWeaponAction.Reload) != 0) ReloadWeapon();
            if ((action & HumanWeaponAction.SelectFirst) != 0) SetSelectWeapon(0);
            if ((action & HumanWeaponAction.SelectSecond) != 0) SetSelectWeapon(1);
            if ((action & HumanWeaponAction.SwitchPrevious) != 0) SwitchWeaponID(CurrentWeapon.Data.previousWeaponIndex);
            if ((action & HumanWeaponAction.SwitchNext) != 0) SwitchWeaponID(CurrentWeapon.Data.nextWeaponIndex);
            if ((action & HumanWeaponAction.Drop) != 0) DropCurrentWeapon();
            if ((action & HumanWeaponAction.Scope) != 0) ToggleScope();
        }

        /// <summary>
        /// 현재 무기를 쏜다. 원본 human::ShotWeapon (object.cpp:658-739) 과 ObjectManager::ShotWeapon (objectmanager.cpp:1926-2060) 을 합친 것이다.
        /// 발사 위치와 방향은 반동이 더해지기 전의 값을 쓴다.
        /// </summary>
        /// <param name="interruptReload">
        /// true 면 한 발씩 장전하는 도중의 발사 입력이 장전을 거기서 끊는다 (그때까지 넣은 탄은 남고, 그 입력으로는 쏘지 않는다). 플레이어의 발사 입력이 켠다.
        /// AI 는 켜지 않는다: 겨눠지면 매 틱 발사를 요청하므로 켜면 한 발 넣을 때마다 장전이 끊긴다.
        /// </param>
        /// <returns>실제로 쐈으면 true.</returns>
        public bool ShotWeapon(bool interruptReload = false)
        {
            Vector3 shotPosition = m_controller.Position + Vector3.Up * m_controller.CameraHeight;
            float yaw = m_controller.Yaw;
            float pitch = m_controller.Pitch;

            if (m_selectWeaponTicks > 0 || m_changeIdTicks > 0) return false;

            Weapon weapon = CurrentWeapon;
            if (weapon.IsNone) return false;

            // 발사 입력 자체는 받아들인 것으로 친다. 간격에 걸려 못 쏴도 연속 발사 수가 초기화되지 않는다.
            m_shotRequested = true;

            // 한 발씩 장전하는 도중의 발사 입력은 장전만 끊는다. 이 입력으로는 쏘지 않고, 발사 버튼을 뗐다가 다시 눌러야 나간다 (PlayerController 가 ConsumeReloadInterrupt 를 보고 막는다).
            // 탄창에 쏠 탄이 있을 때만 끊는다. 비어 있으면 끊어도 쏠 수 없다.
            if (interruptReload && m_reloadTicks > 0 && weapon.ShellByShell && weapon.Magazine > 0)
            {
                m_reloadTicks = 0;
                m_reloadInterrupted = true;
                return false;
            }

            if (m_shotTicks > 0 || m_reloadTicks > 0) return false;

            WeaponData data = weapon.Data;
            if (data.fireRate <= 0f) return false;

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (!parameter.bulletData.Has(data.bulletIndex)) return false;

            int burstLimit = BurstLimit(data);
            if (burstLimit > 0 && m_burstShots >= burstLimit) return false;

            // 무한 탄약: 쏠 때마다 예비 탄에 한 발을 먼저 더한다. 탄창은 그대로 줄어서 재장전은 한다. 먼저 더해야 수류탄 같은 자동 재장전 무기가 마지막 한 발에서 사라지지 않는다.
            if (m_infiniteAmmo && weapon.CanConsumeShot()) weapon.AddReserve(1);
            if (!weapon.ConsumeShot(out bool depleted, out bool autoReloaded)) return false;

            m_burstShots++;
            // 원본 weaponshotcnt = blazings (프레임 수). 데이터는 초당 발사 수라 틱 수로 되돌린다.
            m_shotTicks = Mathf.Max(1, Mathf.RoundToInt(SimClock.FrameRate / data.fireRate));

            // 오차는 이번 발사분 반동을 더하기 전의 값으로 정한다.
            int errorRange = EffectiveErrorRange(data, GunsightErrorRange);
            ScopeData scope = ActiveScope;

            // 무기별 반동값만큼 오차가 쌓인다. 원본은 조준선이 보일 때만 쌓지만(object.cpp:701-705), 그러면 조준선이 없는 무기의 반동값이 쓰이지 않는다.
            m_reactionErrorRange += data.recoil;

            // 시점이 실제로 움직이는 반동. 무기 자체 값은 항상, 스코프 값은 스코프를 쓰는 동안에만 더한다 (원본 object.cpp:707-726).
            ApplyAimRecoil(data.recoilAimHorizontal, data.recoilAimVertical);
            if (scope != null) ApplyAimRecoil(scope.recoilAimHorizontalAdjust, scope.recoilAimVerticalAdjust);

            m_humanVisual.BeginArmShotReaction(data.armReactionAngle);

            Vector3 muzzle = MuzzlePosition(weapon, shotPosition);
            SpawnBullets(data, parameter.bulletData[data.bulletIndex], shotPosition, muzzle, yaw, pitch, errorRange);

            // 발사 통계는 총만 센다 (원본 gamemain.cpp:2240 — 수류탄은 세지 않는다).
            if (weapon.WeaponIndex != parameter.weaponGeneralData.grenadeWeaponIndex) MapLoader.RecordFire(this);

            if (data.soundVolume > 0f)
            {
                // 격발음과, 주변 AI 가 총성을 듣는 처리 (원본 objectmanager.cpp:2051). 소음기 무기는 듣는 거리가 짧다.
                if (SoundManager.Loaded) SoundManager.Instance.PlayAt(data.soundPath, shotPosition, data.soundVolume);

                HumanAIParameterData ai = DataManager.Instance.HumanParameterData.humanAIParameterData;
                WorldSound.EmitPointSound(shotPosition, m_team, data.suppressor ? ai.aiHearGunfireSilencerDist : ai.aiHearGunfireDist, ai.aiHearGunfireAllyDist);
            }

            // 총구 화염·연기·탄피는 다음 화면 갱신 때 낸다. 탄피는 무기별 지연 뒤에 나온다 (원본 yakkyou_delay).
            m_fireEffectModel = weapon.ModelData;
            if (weapon.ModelData != null)
            {
                // 쏠 때 나오는 탄피는 한 발에 하나, 재장전할 때 나오는 탄피는 자동 재장전이 일어난 발사에서 한꺼번에 낸다.
                if (weapon.ModelData.shellEjectMode == ShellEjectMode.OnFire) QueueShells(weapon.ModelData, 1, weapon.ModelData.shellEjectDelay);
                else if (autoReloaded && weapon.ModelData.shellEjectMode == ShellEjectMode.OnReload) QueueShells(weapon.ModelData, weapon.TakeReloadShells(), 0f);
            }

            // 다 쓴 수류탄은 무기째 사라진다 (원본 object.cpp:733-736).
            if (depleted) SetWeapon(m_selectWeapon, parameter.weaponGeneralData.noneWeaponIndex, 0, 0);

            return true;
        }

        /// <summary>
        /// 현재 무기를 재장전하기 시작한다. 원본 human::ReloadWeapon (object.cpp:744-782).
        /// </summary>
        /// <returns>재장전을 시작했으면 true.</returns>
        public bool ReloadWeapon()
        {
            if (m_selectWeaponTicks > 0 || m_changeIdTicks > 0) return false;

            Weapon weapon = CurrentWeapon;
            if (weapon.IsNone) return false;
            if (m_reloadTicks > 0) return false;
            if (!weapon.CanReload()) return false;

            DisableScope();

            // 원본 weaponreloadcnt = reloads + 1. 같은 틱의 카운터 감소로 1 이 바로 빠진다.
            // 한 발씩 장전하는 무기는 한 발을 넣는 시간만 세고, 넣을 때마다 다시 센다 (TickWeapon).
            m_reloadTicks = (weapon.ShellByShell ? ShellReloadTicks(weapon.Data) : Mathf.RoundToInt(weapon.Data.reloadTime * SimClock.FrameRate)) + 1;
            m_burstShots = 0;

            // 재장전할 때 탄피가 나오는 무기(리볼버)는 재장전을 시작하는 순간에 한꺼번에 낸다.
            int shells = weapon.TakeReloadShells();
            if (weapon.ModelData != null && weapon.ModelData.shellEjectMode == ShellEjectMode.OnReload) QueueShells(weapon.ModelData, shells, 0f);
            return true;
        }

        /// <summary>
        /// 발사 입력이 한 발씩 장전을 끊었는지 확인하고 표시를 지운다. 끊은 뒤에는 발사 버튼을 뗐다가 다시 눌러야 쏘게 하려고 PlayerController 가 본다.
        /// </summary>
        /// <returns>지난 확인 뒤에 끊었으면 true.</returns>
        public bool ConsumeReloadInterrupt()
        {
            bool interrupted = m_reloadInterrupted;
            m_reloadInterrupted = false;
            return interrupted;
        }

        /// <summary>
        /// 한 발씩 장전하는 무기가 한 발을 넣는 데 걸리는 틱 수. 재장전 시간(reloadTime)이 빈 탄창을 다 채우는 시간이라서 장탄수로 나눈다.
        /// </summary>
        /// <param name="data">무기 데이터.</param>
        /// <returns>틱 수 (1 이상).</returns>
        private static int ShellReloadTicks(WeaponData data)
        {
            return Mathf.Max(1, Mathf.RoundToInt(data.reloadTime / Mathf.Max(1, data.magazineSize) * SimClock.FrameRate));
        }

        /// <summary>
        /// 탄피를 낼 것을 표시해 둔다. 실제로는 다음 화면 갱신부터 지연이 지난 것을 무기 모델의 자리에서 낸다.
        /// </summary>
        /// <param name="model">무기 모델 데이터.</param>
        /// <param name="count">탄피 수.</param>
        /// <param name="delay">나올 때까지의 시간 (초).</param>
        private void QueueShells(WeaponModelData model, int count, float delay)
        {
            if (count <= 0 || model.shellSize <= 0f || m_pendingShells.Count >= k_maxPendingShells) return;
            m_pendingShells.Add(new PendingShell { model = model, count = count, delay = delay });
        }

        /// <summary>
        /// 활성 슬롯을 바꾼다. 원본 human::ChangeHaveWeapon (object.cpp:454-504).
        /// </summary>
        /// <param name="slot">활성화할 슬롯 (0 또는 1).</param>
        public void SetSelectWeapon(int slot)
        {
            if (m_hp <= 0f) return;
            if (m_reloadTicks > 0) return;
            if (slot < 0 || slot >= WeaponSlotCount || slot == m_selectWeapon) return;
            if (m_selectWeaponTicks > 0 || m_changeIdTicks > 0) return;

            m_selectWeapon = slot;
            DisableScope();
            ApplyActiveWeaponVisual();

            HumanGeneralData general = DataManager.Instance.HumanParameterData.humanGeneralData;
            m_humanVisual.BeginArmSlowReaction(general.armAngleReloading);

            // 원본은 고정 10 프레임. 데이터(slotChangeTime 0.3초)로 빼 둔 값이다.
            m_selectWeaponTicks = Mathf.RoundToInt(CurrentWeapon.Data.slotChangeTime * SimClock.FrameRate);
            m_burstShots = 0;
        }

        /// <summary>
        /// 같은 슬롯 안에서 무기 종류를 바꾼다 (단발 ↔ 연발 등). 탄약은 그대로 넘긴다. 원본 human::ChangeWeaponID (object.cpp:516-580).
        /// </summary>
        /// <param name="targetIndex">바꿀 무기 인덱스. 음수면 무시.</param>
        private void SwitchWeaponID(int targetIndex)
        {
            if (m_changeIdTicks > 0) return;

            Weapon weapon = CurrentWeapon;
            if (weapon.IsNone) return;

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (targetIndex == weapon.WeaponIndex) return;
            if (!parameter.weaponData.Has(targetIndex)) return;

            int switchTicks = Mathf.RoundToInt(weapon.Data.switchTime * SimClock.FrameRate);
            if (switchTicks > 0 && m_reloadTicks > 0) return;

            SetWeapon(m_selectWeapon, targetIndex, weapon.Magazine, weapon.Reserve, false);

            // 전환에 시간이 걸리거나 새 무기에 스코프가 없으면 스코프를 푼다. 즉시 전환이고 스코프가 있으면 새 무기의 스코프로 이어진다.
            if (m_scoping && (switchTicks > 0 || CurrentScopeData == null)) DisableScope();

            m_changeIdTicks = switchTicks;
            m_burstShots = 0;
        }

        /// <summary>
        /// 현재 무기를 앞으로 던져 버린다. 원본 human::DumpWeapon (object.cpp:787-816).
        /// </summary>
        /// <returns>버렸으면 true.</returns>
        public bool DropCurrentWeapon()
        {
            if (IsChanging) return false;

            Weapon weapon = CurrentWeapon;
            if (weapon.IsNone) return false;

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            float yaw = m_controller.Yaw;
            SpawnDroppedWeapon(weapon, yaw, parameter.weaponDropPhysicsData.dropoffHorizontalSpeed);

            SetWeapon(m_selectWeapon, parameter.weaponGeneralData.noneWeaponIndex, 0, 0);
            DisableScope();
            return true;
        }

        /// <summary>
        /// 떨어진 무기를 현재 슬롯(맨손)에 든다. 탄약은 그대로 넘겨받고, 슬롯 전환과 같은 시간 동안 팔을 올린다.
        /// 원본 human::PickupWeapon (object.cpp:422-449).
        /// </summary>
        /// <param name="weaponIndex">주운 무기 번호.</param>
        /// <param name="magazine">장전된 탄.</param>
        /// <param name="reserve">예비 탄.</param>
        public void PickupWeapon(int weaponIndex, int magazine, int reserve)
        {
            SetWeapon(m_selectWeapon, weaponIndex, magazine, reserve, false);

            HumanGeneralData general = DataManager.Instance.HumanParameterData.humanGeneralData;
            m_humanVisual.BeginArmSlowReaction(general.armAngleReloading);
            m_selectWeaponTicks = Mathf.RoundToInt(CurrentWeapon.Data.slotChangeTime * SimClock.FrameRate);
        }

        /// <summary>
        /// 무기 하나를 맵에 떨어뜨린다. 던지는 방향 앞쪽 위에서 시작하고, 모델은 반대쪽을 향한다 (원본 weapon::Dropoff 의 rotation_x = rx + π).
        /// </summary>
        /// <param name="weapon">떨어뜨릴 무기.</param>
        /// <param name="yawDeg">던지는 방향 yaw (도).</param>
        /// <param name="speed">수평 속도 (m/s).</param>
        private void SpawnDroppedWeapon(Weapon weapon, float yawDeg, float speed)
        {
            if (!WeaponManager.Loaded) return;

            Vector3 direction = Coord.YawForward(yawDeg);
            Vector3 position = m_controller.Position + direction * k_dropForwardOffset + Vector3.Up * k_dropHeight;
            WeaponManager.Instance.Spawn(weapon.WeaponIndex, weapon.Magazine, weapon.Reserve, position, yawDeg + 180f, direction * speed);
        }

        /// <summary>
        /// 틱에서 쏜 발사의 총구 화염·연기·탄피를 낸다. 매 렌더 프레임 호출된다.
        /// 원본 ObjectManager::ShotWeaponEffect / ShotWeaponYakkyou (objectmanager.cpp:2065-2160).
        /// </summary>
        /// <param name="dt">프레임 시간.</param>
        private void PlayPendingFireEffects(float dt)
        {
            if (m_fireEffectModel == null && m_pendingShells.Count == 0) return;
            if (!EffectManager.Loaded || !Alive)
            {
                m_fireEffectModel = null;
                m_pendingShells.Clear();
                return;
            }

            if (m_fireEffectModel != null)
            {
                WeaponModelData model = m_fireEffectModel;
                m_fireEffectModel = null;
                if (model.muzzleFlashSize > 0f)
                {
                    Node3D attach = model.fixRightArm ? m_humanVisual.FixedWeaponAttachRoot : m_humanVisual.DynamicWeaponAttachRoot;
                    Vector3 muzzle = attach.GlobalTransform * Coord.FromUnity(model.muzzleFlashOffset);
                    Basis orientation = attach.GlobalBasis.Orthonormalized();
                    EffectManager.Instance.Play(model.muzzleFlashEffectIndex, muzzle, orientation, model.muzzleFlashSize, Vector3.Zero);
                    EffectManager.Instance.Play(model.gunfireSmokeEffectIndex, muzzle, orientation, model.muzzleFlashSize, Vector3.Zero);
                }
            }

            // 기다리는 탄피들. 먼저 쏜 것부터 차례로 보고, 지연이 지난 것을 낸다.
            for (int i = 0; i < m_pendingShells.Count; i++)
            {
                PendingShell shell = m_pendingShells[i];
                shell.delay -= dt;
                if (shell.delay > 0f) continue;

                m_pendingShells.RemoveAt(i--);
                WeaponModelData model = shell.model;
                Node3D attach = model.fixRightArm ? m_humanVisual.FixedWeaponAttachRoot : m_humanVisual.DynamicWeaponAttachRoot;
                Basis orientation = attach.GlobalBasis.Orthonormalized();
                Vector3 position = attach.GlobalTransform * Coord.FromUnity(model.shellEjectOffset);
                Vector3 velocity = orientation * Coord.FromUnity(model.shellEjectDirection.Normalized()) * model.shellEjectSpeed;
                // 한꺼번에 나오는 탄피는 같은 자리에서 여러 번 낸다. 흩어지는 정도는 이펙트 데이터의 난수 범위가 정한다.
                for (int n = 0; n < shell.count; n++)
                {
                    EffectManager.Instance.Play(model.shellEffectIndex, position, orientation, model.shellSize, velocity);
                }
            }
        }

        // 나오기를 기다리는 탄피 수 (점검 도구용).
        public int PendingShellCount
        {
            get
            {
                int total = 0;
                foreach (PendingShell shell in m_pendingShells) total += shell.count;
                return total;
            }
        }

        /// <summary>
        /// 슬롯의 무기를 지정한 종류와 탄약으로 바꾸고 모델을 다시 만든다. 무기 줍기, 종류 전환, 치트, 점검 도구가 쓴다.
        /// </summary>
        /// <param name="slot">슬롯 번호 (0 또는 1).</param>
        /// <param name="weaponIndex">무기 인덱스. 범위 밖이면 맨손.</param>
        /// <param name="magazine">장전된 탄. 음수면 가득.</param>
        /// <param name="reserve">예비 탄. 음수면 사람 종류의 초기 탄약 배수로 정한다.</param>
        /// <param name="clampMagazine">true 면 장전된 탄을 장탄수 이하로 자른다.</param>
        public void SetWeapon(int slot, int weaponIndex, int magazine = -1, int reserve = -1, bool clampMagazine = true)
        {
            if (slot < 0 || slot >= WeaponSlotCount) return;

            if (reserve < 0)
            {
                // 원본 AddVisualWeaponIndex: 전체 탄 = 장탄수 × TOTAL_WEAPON_AUTOBULLET, 그중 장탄수만큼이 장전돼 있다.
                DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
                int magazineSize = list.Has(weaponIndex) ? list[weaponIndex].magazineSize : 0;
                int multiplier = m_humanTypeData != null ? m_humanTypeData.autoBulletMultiplier : Weapon.DefaultAutoBulletMultiplier;
                reserve = magazineSize * Mathf.Max(0, multiplier - 1);
            }
            m_weapons[slot].Configure(weaponIndex, magazine, reserve, clampMagazine);

            RebuildWeaponVisual(slot);
            ApplyActiveWeaponVisual();
        }

        /// <summary>
        /// 치트(F6) — 현재 무기의 예비 탄에 장탄수만큼 더한다.
        /// </summary>
        public void CheatAddMagazine()
        {
            if (!CurrentWeapon.IsNone) CurrentWeapon.CheatAddMagazine();
        }

        /// <summary>
        /// 치트(F7) — 현재 슬롯의 무기를 목록에서 이전/다음 종류로 바꾼다. 탄약은 새 무기의 장탄수에 맞추지 않고 그대로 넘긴다.
        /// 원본 gamemain.cpp:2344-2363 + ObjectManager::CheatNewWeapon (objectmanager.cpp:2320-2395).
        /// </summary>
        /// <param name="direction">+1 = 다음 번호, −1 = 이전 번호.</param>
        public void CheatCycleWeapon(int direction)
        {
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (parameter.weaponData.Count == 0) return;

            // 기본 무기 끝에서 미션의 에드온 무기로 넘어가고, 에드온 끝에서 처음으로 돈다.
            Weapon weapon = CurrentWeapon;
            int next = parameter.weaponData.Neighbor(weapon.WeaponIndex, direction);
            SetWeapon(m_selectWeapon, next, weapon.Magazine, weapon.Reserve, false);

            if (CurrentWeapon.IsNone) DisableScope();
        }

        /// <summary>
        /// 초기 무기를 두 슬롯에 장착하고 주 무기 슬롯을 활성화한다. 원본 ObjectManager::AddHumanIndex (objectmanager.cpp:228-246).
        /// </summary>
        private void EquipInitialWeapons()
        {
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            m_humanVisual.ApplyWeaponAttachScale(parameter.weaponGeneralData.weaponScale);

            int none = parameter.weaponGeneralData.noneWeaponIndex;
            int weaponIndex0 = m_humanData != null ? m_humanData.weaponIndex0 : none;
            int weaponIndex1 = m_humanData != null ? m_humanData.weaponIndex1 : none;

            // HUMAN2 포인트는 주 무기 없이 스폰한다.
            if (m_humanParam.param0 == MapLoader.PointHuman2) weaponIndex1 = none;

            for (int i = 0; i < WeaponSlotCount; i++)
            {
                m_weapons[i] = new Weapon();
                m_weaponVisuals[i] = new WeaponVisual { Name = $"Weapon_{i}" };
                m_humanVisual.DynamicWeaponAttachRoot.AddChild(m_weaponVisuals[i]);
            }

            m_selectWeapon = 1;
            SetWeapon(0, weaponIndex0);
            SetWeapon(1, weaponIndex1);
        }

        /// <summary>
        /// 사망 진입 시 무기 상태를 정리한다. 원본 object.cpp:1228-1247: 든 무기를 모두 무작위 방향(10° 단위)으로 떨어뜨리고 스코프와 카운터를 초기화한다.
        /// 떨어진 무기는 주울 수 있어 게임 결과에 영향을 주므로 방향은 게임플레이 난수로 뽑는다. 사람 종류에 사망 이펙트가 있으면 재생한다.
        /// </summary>
        public void OnDeath()
        {
            int none = DataManager.Instance.WeaponParameterData.weaponGeneralData.noneWeaponIndex;
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (m_weapons[i].IsNone) continue;

                SpawnDroppedWeapon(m_weapons[i], GameRandom.Gameplay.Range(0, 36) * 10f, k_deathDropSpeed);
                SetWeapon(i, none, 0, 0);
            }

            if (m_humanTypeData != null && EffectManager.Loaded)
            {
                EffectManager.Instance.Play(m_humanTypeData.deathEffectIndex, m_controller.Position);
            }

            DisableScope();
            m_selectWeaponTicks = 0;
            m_changeIdTicks = 0;
            m_shotTicks = 0;
            m_reloadTicks = 0;
            m_pendingWeapon = HumanWeaponAction.None;
            ApplyActiveWeaponVisual();
        }

        /// <summary>
        /// 연속으로 쏠 수 있는 발 수. 원본 burstcnt: 0 이면 제한 없음, 1 이면 단발.
        /// </summary>
        /// <param name="data">무기 데이터.</param>
        /// <returns>발 수 제한. 0 이면 제한 없음.</returns>
        private static int BurstLimit(WeaponData data)
        {
            switch (data.burstMode)
            {
                case WeaponBurstMode.SemiAuto: return 1;
                case WeaponBurstMode.Burst: return Mathf.Max(1, data.burstCount);
                default: return 0;
            }
        }

        /// <summary>
        /// 탄환(산탄이면 여러 발)을 만든다. 원본 objectmanager.cpp:1966-2045.
        /// 오차는 yaw, pitch 순으로 한 번 뽑아 모든 탄환이 공유하고, 산탄은 탄환마다 방향(10° 단위)과 반경({5,7,9,11,13})을 뽑아 더 퍼뜨린다.
        /// </summary>
        /// <param name="data">무기 데이터.</param>
        /// <param name="bulletData">탄환 데이터.</param>
        /// <param name="shotPosition">발사 위치 (눈높이).</param>
        /// <param name="muzzle">총구 위치. 탄환 모델을 언제부터 보일지 정하는 기준이다.</param>
        /// <param name="yaw">발사 yaw (도).</param>
        /// <param name="pitch">발사 pitch (도, 아래 +).</param>
        /// <param name="errorRange">조준 오차 (정수 단위, 1 = 0.15°).</param>
        private void SpawnBullets(WeaponData data, BulletData bulletData, Vector3 shotPosition, Vector3 muzzle, float yaw, float pitch, int errorRange)
        {
            if (!BulletManager.Loaded) return;

            DeterministicRandom random = GameRandom.Gameplay;
            const float unit = Weapon.ErrorRangeUnitDegrees;

            if (errorRange > 0)
            {
                int errorYaw = random.Range(0, errorRange * 2 + 1) - errorRange;
                int errorPitch = random.Range(0, errorRange * 2 + 1) - errorRange;
                // 원본은 탄환 각도(왼쪽 +, 위 +)에 더한다. 여기 각도 규약(오른쪽 +, 아래 +)에서는 빼는 것이 된다.
                yaw -= errorYaw * unit;
                pitch -= errorPitch * unit;
            }

            int pellets = data.pelletCount;
            int attacks = pellets > 1 ? (int)(data.damage / (pellets / 2f)) : (int)data.damage;
            float speedPerTick = data.bulletSpeed * SimClock.FrameTime;
            // 산탄은 전탄이 맞았을 때 명중 2 로 센다 (원본 objectmanager.cpp:2002).
            float onTargetWeight = pellets > 1 ? 2f / pellets : 1f;

            for (int i = 0; i < pellets; i++)
            {
                float pelletYaw = yaw;
                float pelletPitch = pitch;
                if (pellets > 1)
                {
                    float angle = Mathf.DegToRad(10f * random.Range(0, 36));
                    int length = random.Range(0, 5) * 2 + 5;
                    pelletYaw -= Mathf.Cos(angle) * length * unit;
                    pelletPitch -= Mathf.Sin(angle) * length * unit;
                }

                if (BulletManager.Instance.Spawn(bulletData, this, m_team, attacks, data.penetration,
                    shotPosition, pelletYaw, pelletPitch, speedPerTick, muzzle, onTargetWeight) == null)
                {
                    // 풀이 가득 차면 남은 탄환은 버린다 (원본 GetNewBulletObject 실패 시 return).
                    return;
                }
            }
        }

        /// <summary>
        /// 시점 반동 한 벌(좌우, 상하)을 조준각에 더한다. 원본은 0.1° 단위로 뽑는다: 최소 + GetRand(범위×10)/10.
        /// 둘 다 0 이면 난수를 뽑지 않는다.
        /// </summary>
        /// <param name="horizontal">좌우 범위 (도).</param>
        /// <param name="vertical">상하 범위 (도, 위 +).</param>
        private void ApplyAimRecoil(FloatRange horizontal, FloatRange vertical)
        {
            if (horizontal.min == 0f && horizontal.max == 0f && vertical.min == 0f && vertical.max == 0f) return;

            float yawKick = RecoilStep(horizontal);
            float pitchKick = RecoilStep(vertical);
            m_controller.AddYawPitch(yawKick, -pitchKick);
        }

        private static float RecoilStep(FloatRange range)
        {
            int steps = Mathf.RoundToInt(range.max * 10f) - Mathf.RoundToInt(range.min * 10f);
            return range.min + GameRandom.Gameplay.Range(0, steps) / 10f;
        }

        /// <summary>
        /// 총구의 월드 위치를 구한다. 시각 노드 기준이라 틱 사이 보간된 위치다 (탄환 표시 시점에만 쓰므로 판정과 무관하다).
        /// </summary>
        /// <param name="weapon">현재 무기.</param>
        /// <param name="fallback">모델 데이터가 없을 때 돌려줄 위치.</param>
        /// <returns>총구 위치.</returns>
        private Vector3 MuzzlePosition(Weapon weapon, Vector3 fallback)
        {
            if (weapon.ModelData == null || !IsInsideTree()) return fallback;

            Node3D attach = weapon.ModelData.fixRightArm ? m_humanVisual.FixedWeaponAttachRoot : m_humanVisual.DynamicWeaponAttachRoot;
            return attach.GlobalTransform * Coord.FromUnity(weapon.ModelData.muzzleFlashOffset);
        }

        /// <summary>
        /// 슬롯 무기의 모델을 다시 만들고, 오른팔 고정 여부에 맞는 부착 루트로 옮긴다. 무기는 오른손에 들리므로 오른팔을 따라간다.
        /// </summary>
        /// <param name="slot">슬롯 번호.</param>
        private void RebuildWeaponVisual(int slot)
        {
            Weapon weapon = m_weapons[slot];
            WeaponVisual visual = m_weaponVisuals[slot];
            visual.Build(weapon.Data, weapon.ModelData);

            bool fixArm = weapon.ModelData != null && weapon.ModelData.fixRightArm;
            Node3D parent = fixArm ? m_humanVisual.FixedWeaponAttachRoot : m_humanVisual.DynamicWeaponAttachRoot;
            if (visual.GetParent() != parent)
            {
                visual.GetParent().RemoveChild(visual);
                parent.AddChild(visual);
            }
        }

        /// <summary>
        /// 활성 슬롯의 무기만 보이게 하고 팔 모델과 자세를 그 무기에 맞춘다.
        /// 맨손인데 AI 가 팔을 조준 방향으로 움직여야 하는 동작(좀비 공격, 항복) 중이면 팔을 조준 쪽에 붙인다.
        /// 죽은 사람도 조준 쪽에 붙인다: 원본은 쓰러진 사람의 팔을 고정 자세가 아니라 팔 각도 그대로 그린다 (object.cpp:2177-2183).
        /// </summary>
        private void ApplyActiveWeaponVisual()
        {
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (m_weaponVisuals[i] != null) m_weaponVisuals[i].Visible = i == m_selectWeapon;
            }

            Weapon weapon = CurrentWeapon;
            if (weapon == null) return;

            bool forceDynamic = weapon.ModelData == null || (weapon.IsNone && (!Alive || m_unarmedArmDynamic));
            m_humanVisual.ApplyArmModel(weapon.ModelData, forceDynamic);
        }
    }
}
