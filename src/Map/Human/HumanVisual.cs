using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// Human 캐릭터의 몸통, 팔, 다리 메시를 조립하고 다리 애니메이션과 팔 각도를 제어하는 시각 표현 노드.
    /// 노드 구조는 UnityXOPS 의 Human 프리팹과 같다: 이 노드(Y 180° 회전) 아래에 Body / DynamicArm / FixedArm / Leg 가 있고,
    /// 팔은 무기 모델 데이터에 따라 DynamicArm(조준 pitch 추종) 또는 FixedArm(고정 각도) 아래로 옮겨 붙는다.
    /// 회전 값은 UnityXOPS 규약(도)으로 계산해 Coord 로 변환한다.
    /// </summary>
    public partial class HumanVisual : Node3D
    {
        private const string k_idleAnimation = "Idle";
        private const string k_walkAnimation = "Walk";
        private const string k_runAnimation = "Run";

        // 원본: 다리 방향 = 이전 방향 × 0.85 + 목표 × 0.15 (프레임당).
        private const float k_legTurnBlend = 0.15f;

        // 팔 반동 복원 (원본 HumanMotionControl::ProcessObject, object.cpp:3400-3419). 단위는 도.
        private const float k_armReactionDecay = 0.5f; // 발사 반동: 틱마다 절반
        private const float k_armReactionSnap = 0.01f; // 이보다 작으면 0 으로
        private const float k_armSlowStep = 2f; // 무기 전환·줍기: 틱마다 2° 씩 복원

        private Human m_human;

        private Node3D m_bodyRoot;
        private Node3D m_dynamicArmRoot;
        private Node3D m_fixedArmRoot;
        private Node3D m_dynamicWeaponAttachRoot;
        private Node3D m_fixedWeaponAttachRoot;
        private MeshInstance3D m_leftArm;
        private MeshInstance3D m_rightArm;
        private MeshInstance3D m_leg;

        private readonly List<ShaderMaterial> m_humanMaterials = new List<ShaderMaterial>();
        private readonly List<ArrayMesh> m_leftArmMeshes = new List<ArrayMesh>();
        private readonly List<ArrayMesh> m_rightArmMeshes = new List<ArrayMesh>();
        private readonly List<ArrayMesh> m_legMeshes = new List<ArrayMesh>();

        private HumanModelData m_humanModelData;
        private HumanArmModelData m_humanArmModelData;
        private HumanAnimation m_idleAnimation;
        private HumanAnimation m_walkAnimation;
        private HumanAnimation m_runAnimation;

        private float m_legAnimationTime;
        private string m_legAnimationName;
        private float m_legRotationX;
        private bool m_legRotationInitialized;
        private float m_armPitchDeg;
        // 팔 반동 각도 (도, 위 +). 틱에서 갱신하고 화면에는 직전 틱 값과 보간해 반영한다. 원본 reaction_y / slowarm.
        private float m_armReactionDeg;
        private float m_armReactionPrevDeg;
        private bool m_armSlow;

        public Node3D DynamicWeaponAttachRoot => m_dynamicWeaponAttachRoot;
        public Node3D FixedWeaponAttachRoot => m_fixedWeaponAttachRoot;

        /// <summary>
        /// 인간 데이터로부터 몸통, 팔, 다리 메시와 머티리얼을 로드해 초기화한다.
        /// </summary>
        /// <param name="human">소유 Human.</param>
        /// <param name="data">인간 파라미터 데이터. null 이면 빈 뼈대만 만든다.</param>
        public void CreateHumanVisual(Human human, HumanData data)
        {
            m_human = human;
            BuildNodes();

            HumanParameterData parameter = DataManager.Instance.HumanParameterData;
            HumanGeneralData general = parameter.humanGeneralData;

            m_bodyRoot.Position = new Vector3(0f, general.humanBodyHeight, 0f);
            m_bodyRoot.Scale = Vector3.One * general.humanBodyScale;
            m_dynamicArmRoot.Position = new Vector3(0f, general.humanArmHeight, 0f);
            m_dynamicArmRoot.Scale = Vector3.One * general.humanArmScale;
            // FixedArm 은 각도를 갖지 않는 앵커다(위치/스케일만). 고정 각도는 팔과 고정 무기가 각자 갖는다.
            m_fixedArmRoot.Position = new Vector3(0f, general.humanArmHeight, 0f);
            m_fixedArmRoot.Scale = Vector3.One * general.humanArmScale;
            m_leg.Position = new Vector3(0f, general.humanLegHeight, 0f);
            m_leg.Scale = Vector3.One * general.humanLegScale;

            if (data == null || data.modelIndex < 0 || data.modelIndex >= parameter.humanModelData.Count)
            {
                return;
            }
            m_humanModelData = parameter.humanModelData[data.modelIndex];

            foreach (string texturePath in m_humanModelData.textures)
            {
                m_humanMaterials.Add(MapLoader.GetEntityMaterial(texturePath));
            }

            // 몸통
            for (int i = 0; i < m_humanModelData.modelData.Count; i++)
            {
                ModelData model = m_humanModelData.modelData[i];
                var body = new MeshInstance3D
                {
                    Name = $"Body_{i}",
                    Mesh = LoadMesh(model.modelPath),
                    MaterialOverride = MaterialAt(model.textureIndex),
                    Position = Coord.FromUnity(model.position),
                    Rotation = Coord.FromUnityEuler(model.rotation),
                    Scale = model.scale,
                };
                m_bodyRoot.AddChild(body);
            }

            // 팔 — 어떤 메시를 쓸지와 고정 여부는 ApplyArmModel 이 무기 모델 데이터로 정한다.
            int armIndex = m_humanModelData.armIndex;
            if (armIndex >= 0 && armIndex < parameter.humanArmModelData.Count)
            {
                m_humanArmModelData = parameter.humanArmModelData[armIndex];
                foreach (string path in m_humanArmModelData.leftArms) m_leftArmMeshes.Add(LoadMesh(path));
                foreach (string path in m_humanArmModelData.rightArms) m_rightArmMeshes.Add(LoadMesh(path));

                ShaderMaterial armMaterial = MaterialAt(m_humanModelData.armTextureIndex);
                m_leftArm.MaterialOverride = armMaterial;
                m_rightArm.MaterialOverride = armMaterial;
            }

            // 다리
            int legIndex = m_humanModelData.legIndex;
            if (legIndex >= 0 && legIndex < parameter.humanLegModelData.Count)
            {
                List<HumanAnimation> animations = parameter.humanAnimationData.humanAnimation;
                m_idleAnimation = animations.Find(animation => animation.name == k_idleAnimation);
                m_walkAnimation = animations.Find(animation => animation.name == k_walkAnimation);
                m_runAnimation = animations.Find(animation => animation.name == k_runAnimation);

                foreach (string path in parameter.humanLegModelData[legIndex].legs) m_legMeshes.Add(LoadMesh(path));
                m_leg.MaterialOverride = MaterialAt(m_humanModelData.legTextureIndex);
            }

            SetLegModel(m_idleAnimation != null && m_idleAnimation.index.Count > 0 ? m_idleAnimation.index[0] : 0);
        }

        /// <summary>
        /// 무기 모델 데이터대로 좌/우 팔 메시를 교체하고, 고정 여부에 따라 팔을 FixedArm 또는 DynamicArm 아래에 붙인다.
        /// 고정 시에는 팔마다 각자의 고정 각도를 갖는다. 좌/우가 독립이라 한쪽만 고정할 수 있다.
        /// 고정 무기 부착 루트의 각도도 오른팔 기준으로 맞춘다(무기는 오른손에 들린다).
        /// </summary>
        /// <param name="model">적용할 무기 모델 데이터. null 이면 팔 인덱스 0 + 동적 부착으로 폴백한다.</param>
        /// <param name="forceDynamic">true 면 고정 지정을 무시하고 양팔을 DynamicArm 에 붙인다(비무장 공격 자세 등).</param>
        public void ApplyArmModel(WeaponModelData model, bool forceDynamic)
        {
            if (model != null && model.fixRightArm)
            {
                // 부착 루트는 원래 Y 180° 회전을 갖는다. 그 위에 고정 각도를 합성한다. 회전만 바꿔 부착 루트의 스케일은 유지한다.
                Basis rotation = Basis.FromEuler(Coord.FromUnityEuler(new Vector3(model.fixedRightArmAngle, 0f, 0f)))
                    * Basis.FromEuler(new Vector3(0f, Mathf.Pi, 0f));
                m_fixedWeaponAttachRoot.Quaternion = rotation.GetRotationQuaternion();
            }

            if (m_humanArmModelData == null)
            {
                m_leftArm.Mesh = null;
                m_rightArm.Mesh = null;
                return;
            }

            bool fixLeft = model != null && model.fixLeftArm && !forceDynamic;
            bool fixRight = model != null && model.fixRightArm && !forceDynamic;
            AttachArm(m_leftArm, fixLeft, model != null ? model.fixedLeftArmAngle : 0f);
            AttachArm(m_rightArm, fixRight, model != null ? model.fixedRightArmAngle : 0f);

            int leftIndex = model != null ? model.leftArmIndex : 0;
            int rightIndex = model != null ? model.rightArmIndex : 0;
            m_leftArm.Mesh = leftIndex >= 0 && leftIndex < m_leftArmMeshes.Count ? m_leftArmMeshes[leftIndex] : null;
            m_rightArm.Mesh = rightIndex >= 0 && rightIndex < m_rightArmMeshes.Count ? m_rightArmMeshes[rightIndex] : null;
        }

        /// <summary>
        /// 몸통과 다리 표시를 토글한다. 1인칭은 false, 3인칭/사망 카메라는 true.
        /// </summary>
        /// <param name="visible">true 면 표시.</param>
        public void SetBodyVisible(bool visible)
        {
            m_bodyRoot.Visible = visible;
            m_leg.Visible = visible;
        }

        /// <summary>
        /// 두 무기 부착 루트의 월드 스케일을 무기 스케일로 맞춘다. 부모(팔 루트)의 팔 스케일을 상쇄해 무기가 원본 크기로 보이게 한다.
        /// </summary>
        /// <param name="weaponScale">WeaponGeneralData.weaponScale (원본 길이 단위 → 미터).</param>
        public void ApplyWeaponAttachScale(float weaponScale)
        {
            float armScale = DataManager.Instance.HumanParameterData.humanGeneralData.humanArmScale;
            Vector3 scale = Vector3.One * (armScale > 0f ? weaponScale / armScale : weaponScale);
            m_dynamicWeaponAttachRoot.Scale = scale;
            m_fixedWeaponAttachRoot.Scale = scale;
        }

        /// <summary>
        /// 시선 pitch(상하 조준각)와 팔 반동을 DynamicArm 의 회전으로 반영한다. 매 렌더 프레임 호출된다.
        /// 원본 OpenXOPS: armmodel_rotation_y = armrotation_y + reaction_y (object.cpp:3450-3455).
        /// </summary>
        /// <param name="pitchDeg">pitch (도, 아래를 볼수록 +).</param>
        public void SetArmPitch(float pitchDeg)
        {
            m_armPitchDeg = pitchDeg;
            float reaction = Mathf.Lerp(m_armReactionPrevDeg, m_armReactionDeg, SimClock.InterpolationAlpha);
            // 이 노드가 Y 180° 회전돼 있어 자식의 X축 회전은 월드에서 방향이 뒤집힌다. 그래서 pitch 부호를 반대로 넣는다.
            m_dynamicArmRoot.Rotation = Coord.FromUnityEuler(new Vector3(-m_armPitchDeg + reaction, 0f, 0f));
        }

        /// <summary>
        /// 발사 반동 — 팔을 순간적으로 들어 올린 뒤 틱마다 절반씩 되돌린다. 원본 HumanMotionControl::ShotWeapon (object.cpp:3341-3362).
        /// </summary>
        /// <param name="angleDeg">들어 올릴 각도 (도, 위 +). 원본은 0.5° × 무기 반동값, 수류탄은 20°.</param>
        public void BeginArmShotReaction(float angleDeg)
        {
            m_armReactionDeg = angleDeg;
            m_armReactionPrevDeg = angleDeg;
            m_armSlow = false;
        }

        /// <summary>
        /// 무기 전환·줍기 — 팔을 내린 각도에서 시작해 틱마다 2° 씩 천천히 되돌린다. 원본 ChangeHaveWeapon / PickupWeapon (object.cpp:3311-3329).
        /// </summary>
        /// <param name="angleDeg">시작 각도 (도, 아래는 음수). 원본 −20°.</param>
        public void BeginArmSlowReaction(float angleDeg)
        {
            m_armReactionDeg = angleDeg;
            m_armReactionPrevDeg = angleDeg;
            m_armSlow = true;
        }

        /// <summary>
        /// 매 틱 HumanController 가 호출한다. 팔 반동을 되돌리고, 재장전·무기 종류 전환 중이면 팔을 내린 각도로 잡아 둔다.
        /// 원본 HumanMotionControl::ProcessObject (object.cpp:3400-3429).
        /// </summary>
        /// <param name="held">재장전 또는 무기 종류 전환 중이면 true.</param>
        public void TickArmReaction(bool held)
        {
            m_armReactionPrevDeg = m_armReactionDeg;

            if (!m_armSlow)
            {
                if (Mathf.Abs(m_armReactionDeg) > k_armReactionSnap) m_armReactionDeg *= k_armReactionDecay;
                else m_armReactionDeg = 0f;
            }
            else
            {
                if (Mathf.Abs(m_armReactionDeg) < k_armSlowStep)
                {
                    m_armReactionDeg = 0f;
                    m_armSlow = false;
                }
                if (m_armReactionDeg > 0f) m_armReactionDeg -= k_armSlowStep;
                if (m_armReactionDeg < 0f) m_armReactionDeg += k_armSlowStep;
            }

            if (held)
            {
                m_armReactionDeg = DataManager.Instance.HumanParameterData.humanGeneralData.armAngleReloading;
            }
        }

        /// <summary>
        /// 매 틱 HumanController 가 호출한다. 이동 플래그와 몸통 yaw 에 따라 다리 메시 프레임과 다리 방향을 갱신한다.
        /// 원본 HumanMotionControl::ProcessObject (object.cpp:3396-3540) 포팅.
        /// </summary>
        /// <param name="dt">틱 시간 (초).</param>
        /// <param name="moveFlag">이번 틱 이동 플래그 (원본 MoveFlag_lt).</param>
        /// <param name="bodyYaw">몸통 yaw (도).</param>
        /// <param name="alive">생존 여부.</param>
        public void TickLeg(float dt, HumanMoveFlag moveFlag, float bodyYaw, bool alive)
        {
            const HumanMoveFlag directions = HumanMoveFlag.Forward | HumanMoveFlag.Back | HumanMoveFlag.Left | HumanMoveFlag.Right;
            bool walk = (moveFlag & HumanMoveFlag.Walk) != 0;

            // 1. 애니메이션 선택. Walk 플래그는 방향 플래그와 무관하게 전진 걷기다.
            HumanAnimation animation;
            if (!alive) animation = m_idleAnimation;
            else if (walk) animation = m_walkAnimation;
            else if ((moveFlag & directions) != 0) animation = m_runAnimation;
            else animation = m_idleAnimation;
            animation ??= m_idleAnimation;
            if (animation == null || animation.index.Count == 0) return;

            if (animation.name != m_legAnimationName)
            {
                m_legAnimationTime = 0f;
                m_legAnimationName = animation.name;
            }

            // 2. 방향별 한 사이클 길이(초). 걷기는 항상 전진 값을 쓴다.
            float cycle;
            if (walk) cycle = animation.forwardSpeed;
            else if ((moveFlag & HumanMoveFlag.Back) != 0) cycle = animation.backwardSpeed;
            else if ((moveFlag & (HumanMoveFlag.Left | HumanMoveFlag.Right)) != 0) cycle = animation.strafeSpeed;
            else cycle = animation.forwardSpeed;

            int frameCount = animation.index.Count;
            int frame = 0;
            if (cycle > 1e-6f && frameCount > 1)
            {
                m_legAnimationTime = (m_legAnimationTime + dt) % cycle;
                frame = Mathf.FloorToInt(m_legAnimationTime / cycle * frameCount) % frameCount;
            }
            else
            {
                m_legAnimationTime = 0f;
            }
            SetLegModel(animation.index[frame]);

            // 3. 다리 방향 (원본 object.cpp:3463-3513). 이동 방향으로 다리를 돌리되, 후진 성분이면 180° 뒤집어 정면을 향한 채 뒷걸음치게 한다.
            float moveYaw = 0f;
            if (alive && !walk)
            {
                switch (moveFlag & directions)
                {
                    case HumanMoveFlag.Back: moveYaw = 180f; break;
                    case HumanMoveFlag.Left: moveYaw = 90f; break;
                    case HumanMoveFlag.Right: moveYaw = -90f; break;
                    case HumanMoveFlag.Forward | HumanMoveFlag.Left: moveYaw = 45f; break;
                    case HumanMoveFlag.Back | HumanMoveFlag.Left: moveYaw = 135f; break;
                    case HumanMoveFlag.Back | HumanMoveFlag.Right: moveYaw = -135f; break;
                    case HumanMoveFlag.Forward | HumanMoveFlag.Right: moveYaw = -45f; break;
                }
            }
            if (Mathf.Abs(moveYaw) > 90f) moveYaw += 180f;

            float target = bodyYaw - moveYaw;
            if (!alive || !m_legRotationInitialized)
            {
                m_legRotationX = alive ? target : bodyYaw;
                m_legRotationInitialized = true;
            }
            else
            {
                m_legRotationX = Coord.LerpAngle(m_legRotationX, target, k_legTurnBlend);
            }

            m_leg.Rotation = Coord.FromUnityEuler(new Vector3(0f, m_legRotationX - bodyYaw, 0f));
        }

        /// <summary>
        /// 노드 뼈대를 만든다. UnityXOPS Human 프리팹의 Visual 하위 구조와 같다.
        /// </summary>
        private void BuildNodes()
        {
            // 원본 모델은 정면이 반대라 전체를 Y 180° 돌려 놓는다.
            Rotation = new Vector3(0f, Mathf.Pi, 0f);

            m_bodyRoot = new Node3D { Name = "Body" };
            AddChild(m_bodyRoot);

            m_dynamicArmRoot = new Node3D { Name = "DynamicArm" };
            AddChild(m_dynamicArmRoot);
            m_dynamicWeaponAttachRoot = new Node3D { Name = "DynamicWeaponAttachRoot", Rotation = new Vector3(0f, Mathf.Pi, 0f) };
            m_dynamicArmRoot.AddChild(m_dynamicWeaponAttachRoot);

            m_fixedArmRoot = new Node3D { Name = "FixedArm" };
            AddChild(m_fixedArmRoot);
            m_fixedWeaponAttachRoot = new Node3D { Name = "FixedWeaponAttachRoot", Rotation = new Vector3(0f, Mathf.Pi, 0f) };
            m_fixedArmRoot.AddChild(m_fixedWeaponAttachRoot);

            m_leftArm = new MeshInstance3D { Name = "Left" };
            m_dynamicArmRoot.AddChild(m_leftArm);
            m_rightArm = new MeshInstance3D { Name = "Right" };
            m_dynamicArmRoot.AddChild(m_rightArm);

            m_leg = new MeshInstance3D { Name = "Leg" };
            AddChild(m_leg);
        }

        /// <summary>
        /// 팔 하나를 고정/동적 루트에 붙인다. 고정이면 자기 각도를 갖고, 동적이면 부모(DynamicArm)가 조준 pitch 를 담당한다.
        /// </summary>
        /// <param name="arm">붙일 팔.</param>
        /// <param name="fix">true 면 FixedArm 에, false 면 DynamicArm 에 붙인다.</param>
        /// <param name="fixedAngleDeg">고정 시 적용할 각도 (도).</param>
        private void AttachArm(MeshInstance3D arm, bool fix, float fixedAngleDeg)
        {
            Node3D parent = fix ? m_fixedArmRoot : m_dynamicArmRoot;
            if (arm.GetParent() != parent)
            {
                arm.GetParent().RemoveChild(arm);
                parent.AddChild(arm);
            }
            arm.Rotation = fix ? Coord.FromUnityEuler(new Vector3(fixedAngleDeg, 0f, 0f)) : Vector3.Zero;
        }

        /// <summary>
        /// 다리 메시를 인덱스로 교체한다.
        /// </summary>
        /// <param name="legIndex">다리 메시 인덱스. 범위 밖이면 메시를 없앤다.</param>
        private void SetLegModel(int legIndex)
        {
            m_leg.Mesh = legIndex >= 0 && legIndex < m_legMeshes.Count ? m_legMeshes[legIndex] : null;
        }

        private ShaderMaterial MaterialAt(int textureIndex)
        {
            return textureIndex >= 0 && textureIndex < m_humanMaterials.Count
                ? m_humanMaterials[textureIndex]
                : MapLoader.GetEntityMaterial(null);
        }

        private static ArrayMesh LoadMesh(string relativePath)
        {
            string fullPath = GamePath.Resolve(relativePath);
            return fullPath != null ? ModelLoader.LoadMesh(fullPath) : null;
        }
    }
}
