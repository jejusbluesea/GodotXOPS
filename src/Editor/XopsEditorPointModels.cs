using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        // 다리 메시는 게임이 서 있을 때 쓰는 동작의 첫 프레임을 쓴다 (HumanVisual 의 k_idleAnimation).
        private const string k_idleAnimation = "Idle";
        // 떨어진 무기는 옆으로 눕혀 놓는다 (WeaponManager.Spawn 과 같은 각도, 도).
        private const float k_droppedWeaponRoll = 90f;

        private CheckBox m_modelsButton;

        /// <summary>
        /// 포인트의 모델을 보여 줄지 바꾼다. 끄면 전부 상자로 그린다.
        /// </summary>
        /// <param name="enabled">보여 줄지.</param>
        private void SetShowModels(bool enabled)
        {
            if (m_markers.ShowModels == enabled) return;

            m_markers.ShowModels = enabled;
            if (m_modelsButton != null) m_modelsButton.SetPressedNoSignal(enabled);
            RebuildPoints();
            SetMessage($"Models {(enabled ? "on: humans, weapons and objects are shown as their models" : "off: points are shown as boxes")}");
        }

        /// <summary>
        /// 포인트 하나의 모델을 만든다 (표식이 부른다). 사람, 무기, 소물 포인트만 모델이 있다. 방향 yaw 0 기준으로 조립하고 방향은 표식이 돌린다.
        /// 번호가 가리키는 데이터는 에셋 모드의 미리 보기와 같이 찾는다 (열려 있는 파일의 편집 → 게임의 데이터, 10000 이상은 미션의 에드온 파일).
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <returns>모델 노드. 모델이 없는 종류거나 데이터를 찾지 못하면 null.</returns>
        private Node3D BuildPointModel(PD2Point point)
        {
            switch (point.type)
            {
                case MapLoader.PointHuman:
                case MapLoader.PointHuman2:
                    return BuildHumanPointModel(point);
                case MapLoader.PointWeapon:
                case MapLoader.PointRandomWeapon:
                    return BuildWeaponPointModel(point);
                case MapLoader.PointSmallObject:
                    return BuildObjectPointModel(point);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 사람 포인트의 모델: 사람 정보 포인트(종류 4, 식별번호 = 이 포인트의 P2)가 가리키는 사람 데이터의 모델. 게임의 로드와 같이 첫 매치를 쓴다.
        /// 게임이 시작할 때처럼 주 무기(사람 데이터의 weaponIndex1)를 들고 팔도 그 무기의 자세다. 주 무기가 없으면(종류 6 이거나 데이터에 없으면) 맨손이고 팔도 맨손의 자세다 (사용자 결정: 보조 무기로 바꿔 보여 주지 않는다).
        /// 에드온 데이터(10000 이상)의 모델을 쓰는 사람은 무기 없이 팔의 첫 메시로 그린다 (에디터는 에드온 데이터를 게임에 붙이지 않는다).
        /// </summary>
        /// <param name="point">사람 포인트.</param>
        /// <returns>모델 노드. 사람 정보나 데이터가 없으면 null.</returns>
        private Node3D BuildHumanPointModel(PD2Point point)
        {
            PD2Point info = m_document.Points.points.Find(other => other.type == MapLoader.PointHumanInfo && other.id == point.param1);
            if (info == null) return null;

            HumanData human = LookupData<HumanData>(typeof(HumanParameterData), nameof(HumanParameterData.humanData), info.param1);
            HumanModelData model = human == null ? null : LookupData<HumanModelData>(typeof(HumanParameterData), nameof(HumanParameterData.humanModelData), human.modelIndex);
            if (model == null) return null;
            if (DataManager.Instance.HumanParameterData.humanModelData.Has(human.modelIndex)) return BuildArmedHumanModel(human, point.type == MapLoader.PointHuman2);

            List<HumanAnimation> animations = DataManager.Instance.HumanParameterData.humanAnimationData.humanAnimation;
            HumanAnimation idle = animations.Find(animation => animation.name == k_idleAnimation);
            var root = new Node3D { Name = "Model" };
            BuildHumanModel(root, model, 0, idle != null && idle.index.Count > 0 ? idle.index[0] : 0, out _, out _);
            return root;
        }

        /// <summary>
        /// 사람을 게임의 HumanVisual 로 조립하고 무기를 쥐여 준다. 슬롯은 Human.EquipInitialWeapons 와 같다 (주 무기는 weaponIndex1, 종류 6 은 주 무기가 없다).
        /// 주 무기가 없으면 맨손(None 무기)의 팔 자세 그대로다. 보조 무기는 보여 주지 않는다.
        /// Human 노드는 만들지 않는다. 모델과 무기는 게임이 읽어 둔 데이터에서 찾는다.
        /// </summary>
        /// <param name="human">사람 데이터.</param>
        /// <param name="noPrimary">true 면 주 무기 없이 (포인트 종류 6).</param>
        /// <returns>모델 노드.</returns>
        private static Node3D BuildArmedHumanModel(HumanData human, bool noPrimary)
        {
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            int none = parameter.weaponGeneralData.noneWeaponIndex;
            int weaponIndex = noPrimary || !parameter.weaponData.Has(human.weaponIndex1) ? none : human.weaponIndex1;
            WeaponData weapon = parameter.weaponData.Has(weaponIndex) ? parameter.weaponData[weaponIndex] : null;
            WeaponModelData weaponModel = weapon != null && parameter.weaponModelData.Has(weapon.modelIndex) ? parameter.weaponModelData[weapon.modelIndex] : null;

            var root = new Node3D { Name = "Model" };
            var visual = new HumanVisual { Name = "Human" };
            root.AddChild(visual);
            visual.CreateHumanVisual(null, human);
            visual.ApplyWeaponAttachScale(parameter.weaponGeneralData.weaponScale);
            visual.ApplyArmModel(weaponModel, weaponModel == null);
            if (weaponModel != null && weaponIndex != none)
            {
                var weaponVisual = new WeaponVisual { Name = "Weapon" };
                (weaponModel.fixRightArm ? visual.FixedWeaponAttachRoot : visual.DynamicWeaponAttachRoot).AddChild(weaponVisual);
                weaponVisual.Build(weapon, weaponModel);
            }
            return root;
        }

        /// <summary>
        /// 무기 포인트의 모델: P2 가 가리키는 무기를 떨어진 무기와 같은 크기와 자세로 놓는다 (WeaponManager.Spawn). 랜덤 무기는 첫 번째 후보(P2)를 보여 준다.
        /// </summary>
        /// <param name="point">무기 포인트.</param>
        /// <returns>모델 노드. 무기나 모델이 없거나 맨손 무기면 null.</returns>
        private Node3D BuildWeaponPointModel(PD2Point point)
        {
            WeaponGeneralData general = DataManager.Instance.WeaponParameterData.weaponGeneralData;
            if (point.param1 == general.noneWeaponIndex) return null;

            WeaponData weapon = LookupData<WeaponData>(typeof(WeaponParameterData), nameof(WeaponParameterData.weaponData), point.param1);
            WeaponModelData model = weapon == null ? null : LookupData<WeaponModelData>(typeof(WeaponParameterData), nameof(WeaponParameterData.weaponModelData), weapon.modelIndex);
            if (model == null) return null;

            var root = new Node3D { Name = "Model" };
            var visual = new Node3D
            {
                Name = "Visual",
                Rotation = Coord.FromUnityEuler(new Vector3(0f, 0f, k_droppedWeaponRoll)),
                Scale = Vector3.One * Mathf.Max(1e-4f, weapon.size * general.weaponScale),
            };
            root.AddChild(visual);
            WeaponVisual.BuildModelParts(visual, model.textures, model.modelData);
            return root;
        }

        /// <summary>
        /// 소물 포인트의 모델: P2 가 가리키는 오브젝트 데이터의 모델 (SmallObject.CreateObject 와 같은 크기와 방향). 바닥에 붙이기(P3)는 반영하지 않고 포인트의 자리에 그린다.
        /// </summary>
        /// <param name="point">소물 포인트.</param>
        /// <returns>모델 노드. 데이터나 모델이 없으면 null.</returns>
        private Node3D BuildObjectPointModel(PD2Point point)
        {
            ObjectData data = LookupData<ObjectData>(typeof(ObjectParameterData), nameof(ObjectParameterData.objectData), point.param1);
            ObjectModelData model = data == null ? null : LookupData<ObjectModelData>(typeof(ObjectParameterData), nameof(ObjectParameterData.objectModelData), data.modelIndex);
            if (model == null) return null;

            var root = new Node3D { Name = "Model" };
            AddObjectModel(root, model);
            return root;
        }
    }
}
