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
