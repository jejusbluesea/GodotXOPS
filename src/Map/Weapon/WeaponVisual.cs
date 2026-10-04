using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    /// <summary>
    /// 무기 모델을 표시하는 노드. 무기 모델 데이터의 메시들을 자식으로 조립한다.
    /// 이 노드의 위치는 손에 쥔 자리(WeaponData.position)이고, 그 아래 Visual 노드가 정면 보정(Y 180°)과 크기를 갖는다.
    /// 사람의 무기 부착 루트 아래에 붙여 쓴다.
    /// </summary>
    public partial class WeaponVisual : Node3D
    {
        private Node3D m_visualRoot;

        /// <summary>
        /// 무기 종류에 맞게 모델을 다시 조립한다. 이전 모델은 지운다. 모델 데이터가 없는 무기(맨손 등)면 빈 상태가 된다.
        /// </summary>
        /// <param name="data">무기 데이터 (쥐는 위치, 크기).</param>
        /// <param name="modelData">무기 모델 데이터. null 이면 모델 없음.</param>
        public void Build(WeaponData data, WeaponModelData modelData)
        {
            if (m_visualRoot == null)
            {
                // 원본 모델은 정면이 반대라 Y 180° 돌려 놓는다. 부착 루트의 Y 180° 와 상쇄된다.
                m_visualRoot = new Node3D { Name = "Visual", Rotation = new Vector3(0f, Mathf.Pi, 0f) };
                AddChild(m_visualRoot);
            }

            foreach (Node child in m_visualRoot.GetChildren())
            {
                m_visualRoot.RemoveChild(child);
                child.Free();
            }

            Position = Coord.FromUnity(data.position);
            Rotation = Vector3.Zero;
            m_visualRoot.Scale = Vector3.One * data.size;

            if (modelData != null)
            {
                BuildModelParts(m_visualRoot, modelData);
            }
        }

        /// <summary>
        /// 무기 모델 데이터의 메시들을 parent 아래에 만든다.
        /// </summary>
        /// <param name="parent">메시 노드를 붙일 부모.</param>
        /// <param name="modelData">무기 모델 데이터.</param>
        public static void BuildModelParts(Node3D parent, WeaponModelData modelData)
        {
            for (int i = 0; i < modelData.modelData.Count; i++)
            {
                ModelData model = modelData.modelData[i];
                string texturePath = model.textureIndex >= 0 && model.textureIndex < modelData.textures.Count
                    ? modelData.textures[model.textureIndex]
                    : null;
                string meshPath = GamePath.Resolve(model.modelPath);

                var part = new MeshInstance3D
                {
                    Name = $"Part_{i}",
                    Mesh = meshPath != null ? ModelLoader.LoadMesh(meshPath) : null,
                    MaterialOverride = MapLoader.GetEntityMaterial(texturePath),
                    Position = Coord.FromUnity(model.position),
                    Rotation = Coord.FromUnityEuler(model.rotation),
                    Scale = model.scale,
                };
                parent.AddChild(part);
            }
        }
    }
}
