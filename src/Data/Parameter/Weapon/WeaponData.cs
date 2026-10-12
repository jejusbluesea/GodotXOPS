using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 무기의 데미지, 발사율, 탄약, 반동, 정확도, 음향, 재장전 파라미터를 담는 컨테이너 클래스.
    /// </summary>
    public class WeaponData
    {
        public string name;
        public int modelIndex;
        public float damage;
        public int penetration;
        // 맞은 사람의 방어구 / 헬멧 포인트를 데미지의 몇 배만큼 깎는지 (0 에서 1. 1 이면 데미지만큼).
        public float armorPointDecay = 1f;
        public float helmetPointDecay = 1f;
        // 맞은 사람의 방어구 / 헬멧이 데미지를 줄이는 것을 얼마나 무시하는지 (0 에서 1. 0 이면 무시하지 않고, 1 이면 방어구가 없는 것처럼 데미지가 전부 들어간다).
        public float armorIgnore;
        public float helmetIgnore;
        // 쏠 때 탄을 쓰는지. false 면 탄창이 줄지 않고 재장전도 하지 않으며, 탄이 없어도 쏜다 (근접 무기). 원본의 무기는 전부 true 다.
        public bool consumeAmmo = true;
        // 탄의 묶음 번호. 종류 전환(Z / X)으로 넘어가는 두 무기의 번호가 같으면 탄을 그대로 넘기고(원본: 단발 ↔ 연발),
        // 다르면 넘기지 않고 그 무기 한 자루가 묶음마다 탄을 따로 갖는다 (소총 ↔ 총에 달린 유탄 발사기). 원본의 무기는 전부 0 이다.
        public int ammoGroup;
        // 이 무기를 새로 채울 때의 예비 탄 (탄창은 늘 장탄수만큼 가득이다). 음수면 원본대로 장탄수 × (사람 종류의 autoBulletMultiplier − 1) 이다.
        // 듣는 곳: 사람이 처음부터 들고 나올 때, 종류 전환으로 다른 탄 묶음을 처음 꺼낼 때, 탄 수를 적지 않고 무기를 줄 때. 맵에 놓인 무기는 포인트에 적은 탄 수를 따른다.
        public int initialReserve = -1;
        // AI 가 이 무기를 쓰는 거리 (m). 적이 aiMaxRange 보다 멀면 쏘지 않고 다가가고, aiMinRange 보다 가까우면 쏘지 않고 물러난다. 0 이면 그 제한이 없다 (원본과 같다).
        // 무기 하나하나가 이 값을 처음 값으로 갖고, 스크립트가 그 무기의 값을 바꿀 수 있다.
        public float aiMaxRange;
        public float aiMinRange;
        // AI 가 종류 전환(Z / X)으로 이어진 종류들 가운데 무엇을 들지 정하는 순서. 탄이 있고 지금 거리에서 쓸 수 있는 것 중 값이 큰 것을 든다.
        // "다른 슬롯의 무기로 바꾸기"가 0 이다: 음수인 종류는 다른 슬롯에 쓸 무기가 없을 때에야 쓴다. 원본의 무기는 전부 0 이다.
        public int aiPriority;
        // 중력을 받는 탄(수류탄 등)을 AI 가 궤적을 계산해서 겨누는지. false 면 원본처럼 겨눈다 (기본 수류탄은 거리에 비례해 조금 위, 그 밖에는 직선).
        public bool aiBallisticAim;
        public int bulletIndex;
        public float fireRate;
        public float bulletSpeed;
        public int magazineSize;
        public int pelletCount;
        public WeaponBurstMode burstMode;
        public int burstCount;
        public WeaponReloadStyle reloadStyle;
        public float reloadTime;
        public float recoil;
        public float armReactionAngle;
        public FloatRange recoilAimVertical;
        public FloatRange recoilAimHorizontal;
        public FloatRange errorRange;
        public bool ignoreAimError;
        public bool crosshair;
        public bool scope;
        public int scopeIndex;
        public Vector3 position;
        public float size;
        public string soundPath;
        public float soundVolume;
        public bool suppressor;
        public int previousWeaponIndex;
        public int nextWeaponIndex;
        public float switchTime;
        public float slotChangeTime;
        public bool discardAfterAutoReloadIfNoAmmo;
    }

    /// <summary>
    /// 무기의 재장전 방식을 정의하는 열거형.
    /// </summary>
    public enum WeaponReloadStyle
    {
        DiscardAndReload,
        RetainAndReload,
        ShellByShellReload,
        AutoReload
    }

    /// <summary>
    /// 무기의 발사 모드를 정의하는 열거형.
    /// </summary>
    public enum WeaponBurstMode
    {
        FullAuto,
        SemiAuto,
        Burst
    }
}
