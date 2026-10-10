using Godot;
using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 무기 모델의 메시, 텍스처, 총구 섬광, 탄피 배출, 팔 파지 자세 파라미터를 담는 컨테이너 클래스.
    /// fix*Arm 이 true 인 팔은 조준 pitch 를 따르지 않고 fixed*ArmAngle 각도로 고정된다(무기 없음/서류가방 등).
    /// 무기 모델도 오른팔을 따라가므로 fixRightArm / fixedRightArmAngle 이 무기 자세까지 결정한다.
    /// </summary>
    public class WeaponModelData
    {
        public string name;
        // 섹션 파일이 깨지거나 항목에 키가 없어도 소비자(WeaponVisual)가 null 역참조하지 않도록 미리 초기화한다.
        public List<string> textures = new List<string>();
        public List<ModelData> modelData = new List<ModelData>();
        public int muzzleFlashEffectIndex;
        public Vector3 muzzleFlashOffset;
        public float muzzleFlashSize;
        public int gunfireSmokeEffectIndex;
        public int shellEffectIndex;
        public Vector3 shellEjectOffset;
        public Vector3 shellEjectDirection;
        public float shellEjectSpeed;
        public float shellEjectDelay;
        public ShellEjectMode shellEjectMode;
        public float shellSize;
        public int leftArmIndex;
        public bool fixLeftArm;
        public float fixedLeftArmAngle;
        public int rightArmIndex;
        public bool fixRightArm;
        public float fixedRightArmAngle;
    }

    /// <summary>
    /// 탄피 배출 시점을 정의하는 열거형.
    /// OnFire 는 쏠 때마다 shellEjectDelay 뒤에 하나, OnReload 는 재장전을 시작할 때 한꺼번에(리볼버), None 은 나오지 않는다.
    /// </summary>
    public enum ShellEjectMode
    {
        OnFire,
        OnReload,
        None
    }
}
