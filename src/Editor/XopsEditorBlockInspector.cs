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
        private const string k_textureListSuffix = "_textures.json";
        private const string k_textureListExtension = ".json";
        private const float k_textureListHeight = 110f;

        // 블록 모드의 값 칸들.
        private Control m_blockBox;
        private readonly CheckBox[] m_flagBoxes = new CheckBox[3];
        private Control m_faceBox;
        private Label m_faceLabel;
        private OptionButton m_faceTexture;
        private OptionButton m_faceMaterial;
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
        /// 오른쪽 패널의 블록용 값 칸들을 만든다: 블록의 통과 플래그, 면의 텍스처·재질·UV, 텍스처 목록.
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
            m_faceMaterial = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, ClipText = true };
            m_faceMaterial.ItemSelected += index =>
            {
                if (!m_fillingInspector) SetFaceMaterial(m_materialOptionIndices[(int)index]);
            };
            grid.AddChild(m_faceMaterial);

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

            box.AddChild(new HSeparator());
            box.AddChild(new Label { Text = "Textures of this map" });
            m_textureList = new ItemList { CustomMinimumSize = new Vector2(0f, k_textureListHeight), FocusMode = Control.FocusModeEnum.None };
            box.AddChild(m_textureList);
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
            if (!m_blockBox.Visible) return;

            List<BD2Block> blocks = m_document.Blocks.blocks;
            List<BlockTextureData> textures = m_document.Textures.blockTextureData;
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

            m_textureList.Clear();
            m_faceTexture.Clear();
            m_faceTexture.AddItem("(not drawn)");
            for (int i = 0; i < textures.Count; i++)
            {
                string name = string.IsNullOrEmpty(textures[i].diffusePath) ? "(white)" : textures[i].diffusePath;
                m_textureList.AddItem($"[{i}] {name}");
                m_faceTexture.AddItem($"[{i}] {Path.GetFileName(name)}");
            }

            DataList<BlockMaterialData> materials = DataManager.Instance.BlockMaterialParameterData.blockMaterialData;
            m_faceMaterial.Clear();
            m_materialOptionIndices.Clear();
            m_faceMaterial.AddItem("(mission default)");
            m_materialOptionIndices.Add(k_defaultMaterial);
            for (int i = 0; i < materials.Count; i++)
            {
                m_faceMaterial.AddItem($"[{i}] {materials[i].name}");
                m_materialOptionIndices.Add(i);
            }

            List<int> faces = TargetFaces();
            m_faceBox.Visible = faces.Count > 0;
            if (faces.Count > 0)
            {
                // 여러 면을 고르면 맨 앞 면의 값을 보여 주고, 고치면 전부에 들어간다.
                BD2Block block = blocks[faces[0] / ElementStride];
                int face = faces[0] % ElementStride;
                m_faceLabel.Text = faces.Count == 1 ? $"Face {face} of block #{faces[0] / ElementStride}" : $"{faces.Count} faces (showing the first)";

                int texture = block.textureIndices[face];
                m_faceTexture.Select(texture >= 0 && texture < textures.Count ? texture + 1 : 0);

                int material = block.materialIndices[face];
                if (!m_materialOptionIndices.Contains(material))
                {
                    m_faceMaterial.AddItem($"[{material}]");
                    m_materialOptionIndices.Add(material);
                }
                m_faceMaterial.Select(m_materialOptionIndices.IndexOf(material));

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
        /// 고칠 면들의 재질을 정한다.
        /// </summary>
        /// <param name="materialIndex">재질 번호. −1 이면 미션의 기본 재질이다.</param>
        private void SetFaceMaterial(int materialIndex)
        {
            EditFaces("Set material", (block, face) => block.materialIndices[face] = materialIndex);
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
            List<BlockTextureData> textures = m_document.Textures.blockTextureData;
            if (replaceIndex >= textures.Count) return Fail($"There is no texture {replaceIndex}");
            if (!string.IsNullOrEmpty(relativePath))
            {
                string fullPath = GamePath.Resolve(relativePath);
                if (fullPath == null || !File.Exists(fullPath)) return Fail($"Texture file open failed: {relativePath}");
            }

            string[] before = TextureListCommand.Snapshot(textures);
            if (replaceIndex < 0) textures.Add(new BlockTextureData { diffusePath = relativePath ?? string.Empty });
            else textures[replaceIndex].diffusePath = relativePath ?? string.Empty;

            m_history.Push(new TextureListCommand(replaceIndex < 0 ? "Add texture" : "Replace texture", textures, before));
            m_blockDirty = true;
            RebuildBlocks();
            SetMessage(replaceIndex < 0 ? $"Added texture [{textures.Count - 1}] {relativePath}" : $"Texture [{replaceIndex}] is now {relativePath}");
            return true;
        }

        /// <summary>
        /// 빈 맵으로 새로 시작한다: 블록도 포인트도 텍스처도 없다. 텍스처를 더하고 블록을 놓은 뒤 이름을 정해 저장한다.
        /// </summary>
        private void NewMap()
        {
            if (Transforming) CancelTransform();
            m_document.Blocks = new BD2File();
            m_document.Textures = new BlockTextureListData();
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
        /// 블록 파일 옆에 두는 텍스처 목록 파일의 경로를 만든다 (같은 이름 + _textures.json. 변환 도구가 쓰는 이름과 같다).
        /// </summary>
        /// <param name="blockPath">BD2 경로 (exe 폴더 기준).</param>
        /// <returns>텍스처 목록 경로 (exe 폴더 기준).</returns>
        private static string TextureListPathFor(string blockPath)
        {
            return blockPath.Substring(0, blockPath.Length - BD2File.Extension.Length) + k_textureListSuffix;
        }
    }
}
