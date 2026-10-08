using System;
using System.Collections.Generic;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 데이터의 정수 값 하나가 다른 목록의 항목을 번호로 가리킨다는 사실. 에디터가 그 번호 옆에 이름을 보여 주고 목록에서 고르게 하는 데 쓴다.
    /// 게임은 이 표를 쓰지 않는다. **데이터 클래스에 다른 목록을 가리키는 번호 필드를 더하면 여기에도 한 줄을 더한다.**
    /// </summary>
    public sealed class AssetReference
    {
        // 가리키는 목록이 든 데이터 클래스. 같은 항목 안의 목록을 가리키면 null.
        public readonly Type Container;
        // 목록의 필드 이름. 안쪽의 목록이면 점으로 잇는다 (예: "humanAIParameterData.aiData"). 같은 항목 안의 목록이면 그 필드 이름.
        public readonly string ListPath;

        public bool Local => Container == null;

        private AssetReference(Type container, string listPath)
        {
            Container = container;
            ListPath = listPath;
        }

        private static readonly Type s_human = typeof(HumanParameterData);
        private static readonly Type s_weapon = typeof(WeaponParameterData);
        private static readonly Type s_object = typeof(ObjectParameterData);
        private static readonly Type s_effect = typeof(EffectParameterData);
        private const string k_effects = nameof(EffectParameterData.effectData);
        private const string k_weapons = nameof(WeaponParameterData.weaponData);
        // 모델의 메시와 팔·다리가 쓰는 텍스처는 그 모델 자신의 textures 목록의 번호다.
        private const string k_localTextures = "textures";

        private static readonly Dictionary<(Type, string), AssetReference> s_table = new Dictionary<(Type, string), AssetReference>
        {
            [(typeof(HumanData), nameof(HumanData.modelIndex))] = new AssetReference(s_human, nameof(HumanParameterData.humanModelData)),
            [(typeof(HumanData), nameof(HumanData.weaponIndex0))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(HumanData), nameof(HumanData.weaponIndex1))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(HumanData), nameof(HumanData.aiIndex))] = new AssetReference(s_human, $"{nameof(HumanParameterData.humanAIParameterData)}.{nameof(HumanAIParameterData.aiData)}"),
            [(typeof(HumanData), nameof(HumanData.typeIndex))] = new AssetReference(s_human, nameof(HumanParameterData.humanTypeData)),
            [(typeof(HumanModelData), nameof(HumanModelData.armIndex))] = new AssetReference(s_human, nameof(HumanParameterData.humanArmModelData)),
            [(typeof(HumanModelData), nameof(HumanModelData.legIndex))] = new AssetReference(s_human, nameof(HumanParameterData.humanLegModelData)),
            [(typeof(HumanModelData), nameof(HumanModelData.armTextureIndex))] = new AssetReference(null, k_localTextures),
            [(typeof(HumanModelData), nameof(HumanModelData.legTextureIndex))] = new AssetReference(null, k_localTextures),
            [(typeof(HumanTypeData), nameof(HumanTypeData.controllerSizeIndex))] = new AssetReference(s_human, nameof(HumanParameterData.controllerSizeData)),
            [(typeof(HumanTypeData), nameof(HumanTypeData.hitboxSizeIndex))] = new AssetReference(s_human, nameof(HumanParameterData.humanHitboxSizeData)),
            [(typeof(HumanTypeData), nameof(HumanTypeData.bloodEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(HumanTypeData), nameof(HumanTypeData.hitEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(HumanTypeData), nameof(HumanTypeData.deathEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(ModelData), nameof(ModelData.textureIndex))] = new AssetReference(null, k_localTextures),
            [(typeof(ObjectData), nameof(ObjectData.modelIndex))] = new AssetReference(s_object, nameof(ObjectParameterData.objectModelData)),
            [(typeof(ObjectData), nameof(ObjectData.colliderIndex))] = new AssetReference(s_object, nameof(ObjectParameterData.objectColliderData)),
            [(typeof(ObjectGeneralData), nameof(ObjectGeneralData.addonObjectIndex))] = new AssetReference(s_object, nameof(ObjectParameterData.objectData)),
            [(typeof(BulletData), nameof(BulletData.explosionEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(BulletData), nameof(BulletData.humanHitEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(BulletData), nameof(BulletData.objectHitEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(WeaponData), nameof(WeaponData.modelIndex))] = new AssetReference(s_weapon, nameof(WeaponParameterData.weaponModelData)),
            [(typeof(WeaponData), nameof(WeaponData.bulletIndex))] = new AssetReference(s_weapon, nameof(WeaponParameterData.bulletData)),
            [(typeof(WeaponData), nameof(WeaponData.scopeIndex))] = new AssetReference(s_weapon, nameof(WeaponParameterData.scopeData)),
            [(typeof(WeaponData), nameof(WeaponData.previousWeaponIndex))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(WeaponData), nameof(WeaponData.nextWeaponIndex))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(WeaponGeneralData), nameof(WeaponGeneralData.noneWeaponIndex))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(WeaponGeneralData), nameof(WeaponGeneralData.grenadeWeaponIndex))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(WeaponGeneralData), nameof(WeaponGeneralData.caseWeaponIndex))] = new AssetReference(s_weapon, k_weapons),
            [(typeof(WeaponModelData), nameof(WeaponModelData.muzzleFlashEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(WeaponModelData), nameof(WeaponModelData.gunfireSmokeEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(WeaponModelData), nameof(WeaponModelData.shellEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(EffectEmitter), nameof(EffectEmitter.textureIndex))] = new AssetReference(s_effect, nameof(EffectParameterData.effectTextureData)),
            [(typeof(EffectGeneralData), nameof(EffectGeneralData.wallBloodEffectIndex))] = new AssetReference(s_effect, k_effects),
            [(typeof(BlockMaterialData), nameof(BlockMaterialData.hitEffect))] = new AssetReference(s_effect, k_effects),
            [(typeof(BlockMaterialData), nameof(BlockMaterialData.bulletHoleEffect))] = new AssetReference(s_effect, k_effects),
        };

        /// <summary>
        /// 데이터 클래스의 필드가 다른 목록을 가리키는 번호인지 찾는다.
        /// </summary>
        /// <param name="owner">필드가 든 데이터 클래스. null 이어도 된다.</param>
        /// <param name="field">필드 이름.</param>
        /// <returns>가리키는 곳. 번호 필드가 아니면 null.</returns>
        public static AssetReference Find(Type owner, string field)
        {
            return owner != null && field != null && s_table.TryGetValue((owner, field), out AssetReference reference) ? reference : null;
        }
    }
}
