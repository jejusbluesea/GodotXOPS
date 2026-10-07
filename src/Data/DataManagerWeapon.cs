using System.Diagnostics;
using Godot;

namespace GodotXOPS
{
    public partial class DataManager
    {
        /// <summary>
        /// 무기 파라미터 데이터를 weapon/ 폴더의 섹션별 JSON에서 각각 읽어 하나의 컨테이너로 병합한다.
        /// 각 파일은 컨테이너 필드명 하나로 감싼 오브젝트이며, OverwriteFromJson으로 그 필드만 채운다.
        /// </summary>
        private void LoadWeaponParameterData()
        {
            WeaponParameterData = new WeaponParameterData();
            OverwriteFromJson(k_weaponGeneralDataPath, WeaponParameterData);
            OverwriteFromJson(k_weaponAccuracyDataPath, WeaponParameterData);
            OverwriteFromJson(k_weaponDropPhysicsDataPath, WeaponParameterData);
            OverwriteFromJson(k_weaponListDataPath, WeaponParameterData);
            OverwriteFromJson(k_weaponBulletDataPath, WeaponParameterData);
            OverwriteFromJson(k_weaponScopeDataPath, WeaponParameterData);
            OverwriteFromJson(k_weaponModelDataPath, WeaponParameterData);

            ValidateArmAngles();
        }

        /// <summary>
        /// 팔을 고정으로 지정해 놓고 고정 각도를 빠뜨린 무기 모델을 에디터에서 경고한다.
        /// 각도 키를 누락하면 조용히 0(팔이 정면)이 되므로, 모더가 원인을 바로 알 수 있도록 모델 이름과 함께 알린다.
        /// 익스포트 빌드에서는 호출 자체가 제거된다.
        /// </summary>
        [Conditional("TOOLS")]
        private void ValidateArmAngles()
        {
            var models = WeaponParameterData.weaponModelData;
            for (int i = 0; i < models.Count; i++)
            {
                WeaponModelData m = models[i];
                if (m == null) continue;

                if (m.fixLeftArm && Mathf.IsZeroApprox(m.fixedLeftArmAngle))
                {
                    Debugger.LogError($"Weapon model [{i}] {m.name}: fixLeftArm is true but fixedLeftArmAngle is 0 (possibly missing). The usual value is -70.", nameof(DataManager));
                }
                if (m.fixRightArm && Mathf.IsZeroApprox(m.fixedRightArmAngle))
                {
                    Debugger.LogError($"Weapon model [{i}] {m.name}: fixRightArm is true but fixedRightArmAngle is 0 (possibly missing). The usual value is -70.", nameof(DataManager));
                }
            }
        }
    }
}
