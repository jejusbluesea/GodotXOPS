using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 면의 UV 를 한 번에 바꾸는 방법.
        /// </summary>
        private enum UVOperation
        {
            // 면 하나에 텍스처 한 장이 꼭 맞게.
            Fit,
            // 네 모서리의 UV 를 한 칸씩 돌린다 (텍스처가 90° 돈다).
            Rotate,
            FlipU,
            FlipV,
        }

        // 면이 그려지지 않을 때의 텍스처 번호와, 미션의 기본 재질을 쓰는 재질 번호.
        private const int k_noTexture = -1;
        private const int k_defaultMaterial = -1;
        private const string k_imageFilter = "*.bmp, *.dds, *.png, *.jpg, *.jpeg ; Images";
        private const string k_textureListSuffix = "_map.json";
        private const string k_textureListExtension = ".json";
        private const float k_textureListHeight = 110f;
        // 텍스처 목록에서 지울 수 없는 앞쪽 자리의 수. 원본(BD1)의 텍스처 슬롯 10개에 해당하는 자리이고, 그 뒤에 더한 것만 지울 수 있다.
        private const int k_fixedTextureSlots = 10;

        // 블록 모드의 값 칸들.
        private Control m_blockBox;
        private readonly CheckBox[] m_flagBoxes = new CheckBox[3];
        private Control m_faceBox;
        private Label m_faceLabel;
        private OptionButton m_faceTexture;
        // 선택한 면의 재질을 알려 주는 글 (재질은 그 면의 텍스처가 정한다).
        private Label m_faceMaterialLabel;
        // 텍스처 목록에서 고른 텍스처의 재질.
        private OptionButton m_textureMaterial;
        // 텍스처 목록에서 고른 자리. 목록을 다시 채워도 남긴다. 없으면 −1.
        private int m_textureSelected = -1;
        // 값 칸이 마지막으로 보여 준 맨 앞 면의 키와 그 면의 텍스처. 다른 면을 고르거나 그 면의 텍스처가 바뀌면 텍스처 목록의 선택을 그 텍스처로 옮긴다.
        private int m_inspectedFace = -1;
        private int m_inspectedTexture = -1;
        private readonly SpinBox[] m_uvBoxes = new SpinBox[BD2Block.FaceVertexCount * 2];
        private ItemList m_textureList;
        // 재질 드롭다운의 줄 번호 → 재질 번호.
        private readonly List<int> m_materialOptionIndices = new List<int>();
        // 텍스처 파일을 고른 뒤 바꿀 목록의 자리. 더하기면 −1.
        private int m_textureReplaceIndex = -1;

        private Label m_boardLabel;
        private Label m_blockIndexLabel;
        // 블록 번호를 한 줄에 이만큼까지 적는다.
        private const int k_shownBlockIndices = 8;
        private static readonly int[] s_flagBits = { BD2File.PassHuman, BD2File.PassBullet, BD2File.PassSight };
        private static readonly string[] s_flagNames = { "Humans pass through", "Bullets pass through", "Sight passes through" };

        /// <summary>
        /// 오른쪽 패널의 블록용 값 칸들을 만든다: 블록의 통과 플래그, 면의 텍스처·UV, 텍스처 목록과 텍스처마다의 재질.
        /// </summary>
        /// <param name="column">칸을 넣을 세로 상자.</param>
        private void BuildBlockInspector(VBoxContainer column)
        {
            var box = new VBoxContainer { Visible = false };
            m_blockBox = box;
            column.AddChild(box);

            // 블록의 번호(파일 안의 순번)를 보여 준다. 이벤트(Move Block, Toggle Block)가 블록을 이 번호로 가리킨다.
            m_blockIndexLabel = new Label { Text = "Block", AutowrapMode = TextServer.AutowrapMode.WordSmart, TooltipText = "The block's number in the file. Events (Move Block, Toggle Block) point at a block by this number. Deleting a block lowers the numbers after it" };
            box.AddChild(m_blockIndexLabel);
            for (int i = 0; i < s_flagBits.Length; i++)
            {
                int bit = s_flagBits[i];
                var flag = new CheckBox { Text = s_flagNames[i], FocusMode = Control.FocusModeEnum.None };
                flag.Toggled += pressed =>
                {
                    if (!m_fillingInspector) SetBlockFlag(bit, pressed);
                };
                m_flagBoxes[i] = flag;
                box.AddChild(flag);
            }

            BuildCorrectUVControl(box);

            m_boardLabel = new Label
            {
                Text = "Not a valid solid shape: nothing collides with this block, whatever the boxes above say (the original game's rule)",
                AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = s_errorColor, Visible = false,
            };
            box.AddChild(m_boardLabel);

            var face = new VBoxContainer();
            m_faceBox = face;
            box.AddChild(face);
            face.AddChild(new HSeparator());
            m_faceLabel = new Label();
            face.AddChild(m_faceLabel);

            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 8);
            face.AddChild(grid);
            grid.AddChild(new Label { Text = "Texture" });
            m_faceTexture = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, ClipText = true };
            m_faceTexture.ItemSelected += index =>
            {
                if (!m_fillingInspector) SetFaceTexture((int)index - 1);
            };
            grid.AddChild(m_faceTexture);
            grid.AddChild(new Label { Text = "Material" });
            m_faceMaterialLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = "The material comes from the face's texture. Change it under Textures of this map" };
            grid.AddChild(m_faceMaterialLabel);

            for (int corner = 0; corner < BD2Block.FaceVertexCount; corner++)
            {
                grid.AddChild(new Label { Text = $"UV {corner}" });
                var pair = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                grid.AddChild(pair);
                for (int axis = 0; axis < 2; axis++)
                {
                    int slot = corner * 2 + axis;
                    SpinBox value = CreateNumberBox(true);
                    value.ValueChanged += _ =>
                    {
                        if (!m_fillingInspector) SetFaceUV(slot / 2, new Vector2((float)m_uvBoxes[slot / 2 * 2].Value, (float)m_uvBoxes[slot / 2 * 2 + 1].Value));
                    };
                    m_uvBoxes[slot] = value;
                    pair.AddChild(value);
                }
            }

            var uvButtons = new HBoxContainer();
            face.AddChild(uvButtons);
            AddUVButton(uvButtons, "Fit", "One whole texture on the face", UVOperation.Fit);
            AddUVButton(uvButtons, "Rotate", "Turn the texture by 90 degrees", UVOperation.Rotate);
            AddUVButton(uvButtons, "Flip U", "Mirror the texture horizontally", UVOperation.FlipU);
            AddUVButton(uvButtons, "Flip V", "Mirror the texture vertically", UVOperation.FlipV);
            var editUV = new Button { Text = "Edit UV...", FocusMode = Control.FocusModeEnum.None, TooltipText = "Open the UV editor: the faces drawn over the texture, to drag their corners (U)" };
            editUV.Pressed += OpenUVEditor;
            face.AddChild(editUV);

            box.AddChild(new HSeparator());
            box.AddChild(new Label { Text = "Textures of this map" });
            m_textureList = new ItemList { CustomMinimumSize = new Vector2(0f, k_textureListHeight), FocusMode = Control.FocusModeEnum.None };
            m_textureList.ItemSelected += index =>
            {
                m_textureSelected = (int)index;
                SyncTextureMaterial();
            };
            box.AddChild(m_textureList);
            // 재질(착탄 이펙트·소리, 발소리)은 텍스처마다 정한다. 그 텍스처를 쓰는 면 전부에 듣는다.
            var materialRow = new HBoxContainer();
            box.AddChild(materialRow);
            materialRow.AddChild(new Label { Text = "Material" });
            m_textureMaterial = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, ClipText = true,
                TooltipText = "Material of the texture selected in the list: bullet hit effect and sound, footsteps. Every face using the texture gets it",
            };
            m_textureMaterial.ItemSelected += index =>
            {
                if (!m_fillingInspector) SetTextureMaterial(m_textureSelected, m_materialOptionIndices[(int)index]);
            };
            materialRow.AddChild(m_textureMaterial);
            var textureButtons = new HBoxContainer();
            box.AddChild(textureButtons);
            var add = new Button { Text = "Add...", FocusMode = Control.FocusModeEnum.None, TooltipText = "Add an image file to the texture list" };
            add.Pressed += () => PickTextureFile(-1);
            textureButtons.AddChild(add);
            var replace = new Button { Text = "Replace...", FocusMode = Control.FocusModeEnum.None, TooltipText = "Replace the texture selected in the list with another image file" };
            replace.Pressed += () =>
            {
                int[] chosen = m_textureList.GetSelectedItems();
                if (chosen.Length == 0) SetMessage("Select a texture in the list first");
                else PickTextureFile(chosen[0]);
            };
            textureButtons.AddChild(replace);
            var delete = new Button { Text = "Delete", FocusMode = Control.FocusModeEnum.None, TooltipText = "Delete the texture selected in the list. Only textures after the first 10 can be deleted. Faces using it are no longer drawn, and the numbers after it move up by one" };
            delete.Pressed += () => DeleteTexture(m_textureSelected);
            textureButtons.AddChild(delete);

            BuildAreaInspector(box);
        }

        /// <summary>
        /// UV 를 한 번에 바꾸는 버튼 하나를 더한다.
        /// </summary>
        /// <param name="row">버튼을 넣을 줄.</param>
        /// <param name="text">버튼의 글자.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="operation">누르면 할 일.</param>
        private void AddUVButton(HBoxContainer row, string text, string tooltip, UVOperation operation)
        {
            var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip };
            button.Pressed += () => TransformFaceUV(operation);
            row.AddChild(button);
        }

        /// <summary>
        /// 값 칸이 고칠 면들을 모은다. 면 단위에서는 선택한 면들, 블록 단위에서는 선택한 블록의 여섯 면 전부, 그 밖의 단위에서는 없다.
        /// </summary>
        /// <returns>면의 키들 (오름차순).</returns>
        private List<int> TargetFaces()
        {
            var result = new List<int>();
            if (m_blockElement == BlockElement.Face)
            {
                result.AddRange(m_blockSelection);
            }
            else if (m_blockElement == BlockElement.Block)
            {
                foreach (int key in m_blockSelection)
                {
                    for (int f = 0; f < BD2Block.FaceCount; f++)
                    {
                        result.Add(key + f);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 블록용 값 칸들을 지금 선택에 맞춘다. 포인트 모드에서는 감춘다.
        /// </summary>
        private void RefreshBlockInspector()
        {
            if (m_blockBox == null) return;

            m_blockBox.Visible = m_editMode == EditMode.Block;
            RefreshUVEditor(false);
            if (!m_blockBox.Visible) return;
            RefreshAreaInspector();

            List<BD2Block> blocks = m_document.Blocks.blocks;
            List<BlockTextureData> textures = m_document.Textures.textureData;
            m_fillingInspector = true;

            // 플래그: 선택한 블록이 전부 켜져 있을 때만 체크로 보인다.
            SortedSet<int> selectedBlocks = SelectedBlocks();
            for (int i = 0; i < s_flagBits.Length; i++)
            {
                bool all = selectedBlocks.Count > 0;
                foreach (int index in selectedBlocks)
                {
                    all &= (blocks[index].flags & s_flagBits[i]) != 0;
                }
                m_flagBoxes[i].Disabled = selectedBlocks.Count == 0;
                m_flagBoxes[i].SetPressedNoSignal(all);
            }

            var shown = new List<string>();
            foreach (int index in selectedBlocks)
            {
                if (shown.Count == k_shownBlockIndices) break;
                shown.Add($"#{index}");
            }
            m_blockIndexLabel.Text = selectedBlocks.Count == 0 ? "Block"
                : $"{(selectedBlocks.Count == 1 ? "Block" : "Blocks")} {string.Join(", ", shown)}{(selectedBlocks.Count > shown.Count ? $" (+{selectedBlocks.Count - shown.Count} more)" : string.Empty)}";

            // 모양이 판형이라 플래그와 관계없이 충돌하지 않는 블록이 선택에 있으면 알린다. 화면의 블록(MapLoader)은 옮기기를 확정한 뒤의 모양이다.
            bool board = false;
            foreach (int index in selectedBlocks)
            {
                board |= index < MapLoader.Blocks.Count && MapLoader.Blocks[index].boardShape;
            }
            m_boardLabel.Visible = board;

            List<int> faces = TargetFaces();
            // 다른 면을 골랐거나 그 면의 텍스처가 바뀌었으면 텍스처 목록의 선택을 그 면의 텍스처로 옮긴다 (그 재질을 바로 고칠 수 있게).
            int firstFace = faces.Count > 0 ? faces[0] : -1;
            int firstTexture = firstFace >= 0 ? blocks[firstFace / ElementStride].textureIndices[firstFace % ElementStride] : -1;
            if (firstFace != m_inspectedFace || firstTexture != m_inspectedTexture)
            {
                m_inspectedFace = firstFace;
                m_inspectedTexture = firstTexture;
                if (firstTexture >= 0 && firstTexture < textures.Count) m_textureSelected = firstTexture;
            }
            if (m_textureSelected >= textures.Count) m_textureSelected = -1;

            m_textureList.Clear();
            m_faceTexture.Clear();
            m_faceTexture.AddItem("(not drawn)");
            for (int i = 0; i < textures.Count; i++)
            {
                string name = string.IsNullOrEmpty(textures[i].diffusePath) ? "(white)" : textures[i].diffusePath;
                string material = textures[i].materialIndex == k_defaultMaterial ? string.Empty : $"  ({MaterialName(textures[i].materialIndex)})";
                m_textureList.AddItem($"[{i}] {name}{material}");
                m_faceTexture.AddItem($"[{i}] {Path.GetFileName(name)}");
            }
            if (m_textureSelected >= 0) m_textureList.Select(m_textureSelected);
            SyncTextureMaterial();

            m_faceBox.Visible = faces.Count > 0;
            if (faces.Count > 0)
            {
                // 여러 면을 고르면 맨 앞 면의 값을 보여 주고, 고치면 전부에 들어간다.
                BD2Block block = blocks[faces[0] / ElementStride];
                int face = faces[0] % ElementStride;
                m_faceLabel.Text = faces.Count == 1 ? $"Face {face} of block #{faces[0] / ElementStride}" : $"{faces.Count} faces (showing the first)";

                int texture = block.textureIndices[face];
                m_faceTexture.Select(texture >= 0 && texture < textures.Count ? texture + 1 : 0);

                int material = texture >= 0 && texture < textures.Count ? textures[texture].materialIndex : k_defaultMaterial;
                m_faceMaterialLabel.Text = material == k_defaultMaterial ? "(mission default)" : MaterialName(material);

                for (int corner = 0; corner < BD2Block.FaceVertexCount; corner++)
                {
                    Vector2 uv = block.uvs[face * BD2Block.FaceVertexCount + corner];
                    m_uvBoxes[corner * 2].SetValueNoSignal(uv.X);
                    m_uvBoxes[corner * 2 + 1].SetValueNoSignal(uv.Y);
                }
            }

            m_fillingInspector = false;
        }

        /// <summary>
        /// 블록 몇 개를 고치고 되돌리기 기록에 남긴 뒤 화면의 블록을 다시 만든다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="blockIndices">고칠 블록의 번호들.</param>
        /// <param name="edit">블록 하나를 고치는 함수 (블록 번호와 블록을 받는다).</param>
        private void EditBlocks(string name, ICollection<int> blockIndices, Action<int, BD2Block> edit)
        {
            if (blockIndices.Count == 0 || Transforming) return;

            List<BD2Block> blocks = m_document.Blocks.blocks;
            var indices = new int[blockIndices.Count];
            blockIndices.CopyTo(indices, 0);
            var before = new BD2Block[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                before[i] = BlockChangeCommand.Clone(blocks[indices[i]]);
                edit(indices[i], blocks[indices[i]]);
            }
            m_history.Push(new BlockChangeCommand(name, blocks, indices, before));
            m_blockDirty = true;
            RebuildBlocks();
            SetMessage(name);
        }

        /// <summary>
        /// 고칠 면들을 블록별로 묶어 고친다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="edit">면 하나를 고치는 함수 (블록과 면 번호를 받는다).</param>
        private void EditFaces(string name, Action<BD2Block, int> edit)
        {
            List<int> faces = TargetFaces();
            var blockIndices = new SortedSet<int>();
            foreach (int key in faces)
            {
                blockIndices.Add(key / ElementStride);
            }
            EditBlocks(name, blockIndices, (index, block) =>
            {
                foreach (int key in faces)
                {
                    if (key / ElementStride == index) edit(block, key % ElementStride);
                }
            });
        }

        /// <summary>
        /// 고칠 면들의 텍스처를 정한다.
        /// </summary>
        /// <param name="textureIndex">텍스처 목록의 번호. 음수면 그 면을 그리지 않는다.</param>
        private void SetFaceTexture(int textureIndex)
        {
            EditFaces("Set texture", (block, face) => block.textureIndices[face] = textureIndex < 0 ? k_noTexture : textureIndex);
        }

        /// <summary>
        /// 재질 번호를 사람이 알아볼 이름으로 적는다. 10000 이상은 지금 미션이 속한 맵팩의 재질 파일에서 찾는다.
        /// </summary>
        /// <param name="materialIndex">재질 번호.</param>
        /// <returns>"[번호] 이름". 목록에 없는 번호면 번호만.</returns>
        private string MaterialName(int materialIndex)
        {
            BlockMaterialData material = LookupData<BlockMaterialData>(typeof(BlockMaterialParameterData), nameof(BlockMaterialParameterData.blockMaterialData), materialIndex);
            return material == null ? $"[{materialIndex}]" : $"[{materialIndex}] {material.name}";
        }

        /// <summary>
        /// 재질 드롭다운을 채우고 텍스처 목록에서 고른 텍스처의 재질을 가리키게 한다: 미션의 기본 재질, 기본 데이터의 재질들, 지금 미션이 속한 맵팩의 재질들(10000 부터).
        /// </summary>
        private void SyncTextureMaterial()
        {
            List<BlockTextureData> textures = m_document.Textures.textureData;
            bool filling = m_fillingInspector;
            m_fillingInspector = true;

            m_textureMaterial.Clear();
            m_materialOptionIndices.Clear();
            m_textureMaterial.AddItem("(mission default)");
            m_materialOptionIndices.Add(k_defaultMaterial);
            System.Collections.IList materials = ListOf(typeof(BlockMaterialParameterData), nameof(BlockMaterialParameterData.blockMaterialData));
            System.Collections.IList pack = AddonListOf(typeof(BlockMaterialParameterData), nameof(BlockMaterialParameterData.blockMaterialData), true);
            for (int i = 0; materials != null && i < materials.Count; i++)
            {
                m_textureMaterial.AddItem($"[{i}] {ItemName(materials[i])}");
                m_materialOptionIndices.Add(i);
            }
            for (int i = 0; pack != null && i < pack.Count; i++)
            {
                m_textureMaterial.AddItem($"[{DataList<BlockMaterialData>.PackBase + i}] {ItemName(pack[i])}  (map pack)");
                m_materialOptionIndices.Add(DataList<BlockMaterialData>.PackBase + i);
            }

            bool selected = m_textureSelected >= 0 && m_textureSelected < textures.Count;
            m_textureMaterial.Disabled = !selected;
            if (selected)
            {
                int material = textures[m_textureSelected].materialIndex;
                if (!m_materialOptionIndices.Contains(material))
                {
                    m_textureMaterial.AddItem($"[{material}]  (not in the list)");
                    m_materialOptionIndices.Add(material);
                }
                m_textureMaterial.Select(m_materialOptionIndices.IndexOf(material));
            }
            m_fillingInspector = filling;
        }

        /// <summary>
        /// 텍스처 목록의 한 텍스처의 재질을 정한다. 그 텍스처를 쓰는 면 전부의 착탄 이펙트·소리와 발소리가 바뀐다.
        /// </summary>
        /// <param name="textureIndex">텍스처 목록의 번호.</param>
        /// <param name="materialIndex">재질 번호. −1 이면 미션의 기본 재질이다.</param>
        /// <returns>바꿨으면 true.</returns>
        private bool SetTextureMaterial(int textureIndex, int materialIndex)
        {
            if (Transforming) CancelTransform();
            List<BlockTextureData> textures = m_document.Textures.textureData;
            if (textureIndex < 0 || textureIndex >= textures.Count) return Fail("Select a texture in the list first");
            if (textures[textureIndex].materialIndex == materialIndex) return false;

            BlockTextureData[] before = TextureListCommand.Snapshot(textures);
            textures[textureIndex].materialIndex = materialIndex;
            m_history.Push(new TextureListCommand("Set texture material", textures, before));
            m_blockDirty = true;
            RebuildBlocks();
            SetMessage(materialIndex == k_defaultMaterial ? $"Texture [{textureIndex}] uses the mission's default material" : $"Texture [{textureIndex}] uses material {MaterialName(materialIndex)}");
            return true;
        }

        /// <summary>
        /// 고칠 면들의 한 모서리의 UV 를 정한다.
        /// </summary>
        /// <param name="corner">모서리 번호 (0 에서 3).</param>
        /// <param name="uv">UV.</param>
        private void SetFaceUV(int corner, Vector2 uv)
        {
            if (corner < 0 || corner >= BD2Block.FaceVertexCount || !uv.IsFinite()) return;
            EditFaces("Set UV", (block, face) => block.uvs[face * BD2Block.FaceVertexCount + corner] = uv);
        }

        /// <summary>
        /// 고칠 면들의 UV 를 한 번에 바꾼다 (꼭 맞추기, 돌리기, 뒤집기). 뒤집기는 그 면의 UV 범위 안에서 뒤집으므로 타일 수는 그대로다.
        /// </summary>
        /// <param name="operation">바꾸는 방법.</param>
        private void TransformFaceUV(UVOperation operation)
        {
            EditFaces($"UV {operation}", (block, face) =>
            {
                int first = face * BD2Block.FaceVertexCount;
                var corners = new Vector2[BD2Block.FaceVertexCount];
                Array.Copy(block.uvs, first, corners, 0, corners.Length);
                Vector2 low = corners[0];
                Vector2 high = corners[0];
                foreach (Vector2 uv in corners)
                {
                    low = low.Min(uv);
                    high = high.Max(uv);
                }

                for (int corner = 0; corner < corners.Length; corner++)
                {
                    Vector2 uv = corners[corner];
                    switch (operation)
                    {
                        case UVOperation.Fit: uv = s_faceUVs[corner]; break;
                        case UVOperation.Rotate: uv = corners[(corner + 1) % corners.Length]; break;
                        case UVOperation.FlipU: uv.X = low.X + high.X - uv.X; break;
                        case UVOperation.FlipV: uv.Y = low.Y + high.Y - uv.Y; break;
                    }
                    block.uvs[first + corner] = uv;
                }
            });
        }

        /// <summary>
        /// 선택한 요소들이 속한 블록의 통과 플래그 하나를 켜거나 끈다.
        /// </summary>
        /// <param name="bit">플래그 (BD2File.PassHuman / PassBullet / PassSight).</param>
        /// <param name="enabled">켤지. 켜면 그 판정이 블록을 통과한다.</param>
        private void SetBlockFlag(int bit, bool enabled)
        {
            EditBlocks("Set block flag", SelectedBlocks(), (_, block) => block.flags = enabled ? block.flags | bit : block.flags & ~bit);
        }

        /// <summary>
        /// 텍스처로 쓸 이미지 파일을 고르는 창을 띄운다.
        /// </summary>
        /// <param name="replaceIndex">바꿀 텍스처의 번호. 목록에 더하려면 −1.</param>
        private void PickTextureFile(int replaceIndex)
        {
            m_textureReplaceIndex = replaceIndex;
            ShowFileDialog(k_menuPickTexture, replaceIndex < 0 ? "Add texture" : "Replace texture", k_imageFilter, false);
        }

        /// <summary>
        /// 텍스처 목록에 이미지 파일을 더하거나, 있는 자리의 파일을 바꾼다. 자리를 지우는 기능은 없다 (지우면 뒤의 번호가 전부 밀린다).
        /// </summary>
        /// <param name="relativePath">이미지 경로 (exe 폴더 기준). 비어 있으면 흰 면이 되는 빈 자리다.</param>
        /// <param name="replaceIndex">바꿀 자리. 더하려면 −1.</param>
        /// <returns>바꿨으면 true.</returns>
        private bool SetTexture(string relativePath, int replaceIndex)
        {
            if (Transforming) CancelTransform();
            List<BlockTextureData> textures = m_document.Textures.textureData;
            if (replaceIndex >= textures.Count) return Fail($"There is no texture {replaceIndex}");
            if (!string.IsNullOrEmpty(relativePath))
            {
                string fullPath = GamePath.Resolve(relativePath);
                if (fullPath == null || !File.Exists(fullPath)) return Fail($"Texture file open failed: {relativePath}");
            }

            BlockTextureData[] before = TextureListCommand.Snapshot(textures);
            if (replaceIndex < 0) textures.Add(new BlockTextureData { diffusePath = relativePath ?? string.Empty });
            else textures[replaceIndex].diffusePath = relativePath ?? string.Empty;

            m_history.Push(new TextureListCommand(replaceIndex < 0 ? "Add texture" : "Replace texture", textures, before));
            m_blockDirty = true;
            RebuildBlocks();
            SetMessage(replaceIndex < 0 ? $"Added texture [{textures.Count - 1}] {relativePath}" : $"Texture [{replaceIndex}] is now {relativePath}");
            return true;
        }

        /// <summary>
        /// 텍스처 목록에서 한 자리를 지운다. 원본의 슬롯에 해당하는 앞의 10개는 지우지 못한다.
        /// 뒤의 번호가 하나씩 당겨지므로 블록의 면이 가리키는 번호도 함께 고친다: 지운 텍스처를 쓰던 면은 그리지 않는 면이 되고, 더 뒤의 번호는 하나씩 줄인다.
        /// </summary>
        /// <param name="index">지울 자리.</param>
        /// <returns>지웠으면 true.</returns>
        private bool DeleteTexture(int index)
        {
            if (Transforming) CancelTransform();
            List<BlockTextureData> textures = m_document.Textures.textureData;
            if (index < 0 || index >= textures.Count) return Fail("Select a texture in the list first");
            if (index < k_fixedTextureSlots) return Fail($"Textures [0] to [{k_fixedTextureSlots - 1}] cannot be deleted (use Replace). Only textures added after them can");

            List<BD2Block> blocks = m_document.Blocks.blocks;
            BlockTextureData[] texturesBefore = TextureListCommand.Snapshot(textures);
            BD2Block[] blocksBefore = BlockListCommand.Snapshot(blocks);
            string path = textures[index].diffusePath;
            textures.RemoveAt(index);
            int cleared = 0;
            foreach (BD2Block block in blocks)
            {
                for (int f = 0; f < BD2Block.FaceCount; f++)
                {
                    if (block.textureIndices[f] == index)
                    {
                        block.textureIndices[f] = k_noTexture;
                        cleared++;
                    }
                    else if (block.textureIndices[f] > index)
                    {
                        block.textureIndices[f]--;
                    }
                }
            }

            m_history.Push(new TextureDeleteCommand("Delete texture", new TextureListCommand("Delete texture", textures, texturesBefore), new BlockListCommand("Delete texture", blocks, blocksBefore)));
            m_textureSelected = -1;
            m_blockDirty = true;
            RebuildBlocks();
            SetMessage($"Deleted texture [{index}] {path}{(cleared > 0 ? $": {cleared} face(s) used it and are no longer drawn" : string.Empty)}");
            return true;
        }

        /// <summary>
        /// 빈 맵으로 새로 시작한다: 블록도 포인트도 텍스처도 없다. 텍스처를 더하고 블록을 놓은 뒤 이름을 정해 저장한다.
        /// </summary>
        private void NewMap()
        {
            if (Transforming) CancelTransform();
            m_document.Blocks = new BD2File();
            m_document.Textures = new BlockMapData();
            m_document.Points = new PD2File();
            m_document.Messages.Clear();
            m_document.BlockPath = string.Empty;
            m_document.PointPath = string.Empty;
            m_document.SkyIndex = 0;
            m_document.MissionPath = string.Empty;
            m_document.Mission = new ExtendedMissionData();
            m_missionDirty = false;
            ApplyMission();

            m_selection.Clear();
            m_blockSelection.Clear();
            m_history.Clear();
            m_dirty = false;
            m_blockDirty = false;
            MapLoader.LoadSkyData(0);
            RebuildBlocks();
            RebuildPoints();
            SetMessage("New map: add textures in block mode, then add blocks (Shift+A)");
        }

        /// <summary>
        /// 블록 파일 옆에 두는 텍스처 목록 파일의 경로를 만든다 (같은 이름 + _map.json. 변환 도구가 쓰는 이름과 같다).
        /// </summary>
        /// <param name="blockPath">BD2 경로 (exe 폴더 기준).</param>
        /// <returns>텍스처 목록 경로 (exe 폴더 기준).</returns>
        private static string TextureListPathFor(string blockPath)
        {
            return blockPath.Substring(0, blockPath.Length - BD2File.Extension.Length) + k_textureListSuffix;
        }
    }
}
