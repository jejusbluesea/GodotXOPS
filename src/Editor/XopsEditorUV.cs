using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        // 변형한 면의 모서리 사이 거리가 이만큼(m)도 바뀌지 않았으면 모양이 그대로인 면으로 본다 (통째로 옮기거나 돌린 면). UV 를 건드리지 않는다.
        private const float k_uvRigidTolerance = 1e-4f;
        // 꼭짓점이 이만큼(m)도 움직이지 않았으면 그 모서리의 UV 는 그대로 둔다.
        private const float k_uvMovedDistance = 1e-5f;
        // 면이 이보다 좁으면(법선의 길이, 위치의 퍼짐) UV 를 따라 고칠 기준을 구할 수 없다.
        private const float k_uvDegenerate = 1e-8f;
        // 면이 거의 뒤집혔으면(법선의 내적) 가장 짧은 회전을 정할 수 없어 처음의 축을 그대로 쓴다.
        private const float k_uvFlippedDot = -0.999f;
        private static readonly Vector2I s_uvWindowSize = new Vector2I(880, 800);
        private static readonly Vector2I s_uvWindowMinSize = new Vector2I(420, 320);

        // 블록을 변형할 때 UV 를 따라 고칠지 (블렌더의 Correct Face Attributes). 꺼져 있으면 UV 는 그대로라 텍스처가 면과 함께 늘어난다.
        private bool m_correctUV = true;
        private CheckBox m_correctUVButton;

        private Window m_uvWindow;
        private UVCanvas m_uvCanvas;
        private Label m_uvInfo;
        private readonly SpinBox[] m_uvValueBoxes = new SpinBox[2];
        private bool m_uvWindowPlaced;
        private bool m_fillingUV;
        // 개발용 인자 --uv-shot: UV 창을 저장할 경로와 저장까지 남은 프레임 수.
        private string m_uvShotPath;
        private int m_uvShotCountdown = -1;
        // UV 화면이 보여 주는 면들의 키 (TargetFaces 의 순서). 그림판의 면 순번이 이 목록의 순번이다.
        private readonly List<int> m_uvFaceKeys = new List<int>();

        /// <summary>
        /// 오른쪽 패널의 블록용 칸들에 "변형할 때 UV 도 따라 고치기" 체크 상자를 만든다 (위쪽 도구 줄에는 자리가 없다). 블록 모드에서만 보인다.
        /// </summary>
        /// <param name="row">체크 상자를 넣을 상자.</param>
        private void BuildCorrectUVControl(Container row)
        {
            m_correctUVButton = new CheckBox
            {
                Text = "Correct UVs when transforming", ButtonPressed = m_correctUV, FocusMode = Control.FocusModeEnum.None,
                TooltipText = "When block vertices are moved, rotated or scaled, adjust the UVs of the faces that changed shape so the texture keeps its size instead of stretching. Faces moved as a whole keep their UVs",
            };
            m_correctUVButton.Toggled += pressed => m_correctUV = pressed;
            row.AddChild(m_correctUVButton);
        }

        /// <summary>
        /// 방금 변형한 블록들에서, 모양이 바뀐 면의 UV 를 꼭짓점이 움직인 만큼 따라 고친다 (변형을 확정할 때).
        /// </summary>
        private void CorrectTransformedUVs()
        {
            List<BD2Block> blocks = m_document.Blocks.blocks;
            for (int i = 0; i < m_transformBlockIndices.Length; i++)
            {
                for (int face = 0; face < BD2Block.FaceCount; face++)
                {
                    CorrectFaceUV(m_transformBlockBefore[i], blocks[m_transformBlockIndices[i]], face);
                }
            }
        }

        /// <summary>
        /// 면 하나의 UV 를 꼭짓점이 움직인 만큼 따라 고친다. 텍스처가 면 위에서 차지하던 크기(1 m 가 UV 얼마인지)를 지킨다: 면을 늘리면 텍스처가 늘어나지 않고 더 반복된다.
        /// 변형 전의 면에서 "면 위의 자리 → UV" 의 기울기를 구하고, 움직인 모서리의 UV 에 "기준에 대한 자리의 변화 × 기울기"를 더한다.
        /// 기준은 움직이지 않은 모서리이고, 전부 움직였으면 면의 가운데다. 새 면의 축은 처음의 축을 법선이 돈 만큼(가장 짧은 회전) 돌린 것이다.
        /// 통째로 옮기거나 돌려서 모양이 그대로인 면과 움직이지 않은 모서리의 UV 는 건드리지 않는다.
        /// </summary>
        /// <param name="before">변형하기 전의 블록.</param>
        /// <param name="block">변형한 블록. 이 블록의 UV 를 고친다.</param>
        /// <param name="face">면 번호 (0 에서 5).</param>
        /// <returns>UV 를 고쳤으면 true.</returns>
        private static bool CorrectFaceUV(BD2Block before, BD2Block block, int face)
        {
            const int count = BD2Block.FaceVertexCount;
            int[] corners = MapLoader.BlockFaceVertices[face];
            var old = new Vector3[count];
            var now = new Vector3[count];
            var moved = new bool[count];
            int anchor = -1;
            int movedCount = 0;
            for (int i = 0; i < count; i++)
            {
                old[i] = before.vertices[corners[i]];
                now[i] = block.vertices[corners[i]];
                moved[i] = old[i].DistanceTo(now[i]) > k_uvMovedDistance;
                if (moved[i]) movedCount++;
                else if (anchor < 0) anchor = i;
            }
            if (movedCount == 0) return false;

            bool rigid = true;
            for (int a = 0; a < count && rigid; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    rigid &= Mathf.Abs(old[a].DistanceTo(old[b]) - now[a].DistanceTo(now[b])) <= k_uvRigidTolerance;
                }
            }
            if (rigid) return false;

            Vector3 normal = (old[2] - old[0]).Cross(old[3] - old[1]);
            Vector3 newNormal = (now[2] - now[0]).Cross(now[3] - now[1]);
            if (normal.LengthSquared() < k_uvDegenerate || newNormal.LengthSquared() < k_uvDegenerate) return false;
            normal = normal.Normalized();
            newNormal = newNormal.Normalized();

            // 처음 면의 축 둘 (면 위에서 서로 수직).
            Vector3 along = old[1] - old[0];
            along -= normal * along.Dot(normal);
            if (along.LengthSquared() < k_uvDegenerate)
            {
                along = old[2] - old[0];
                along -= normal * along.Dot(normal);
            }
            if (along.LengthSquared() < k_uvDegenerate) return false;
            Vector3 axisU = along.Normalized();
            Vector3 axisV = normal.Cross(axisU);

            int first = face * count;
            var flat = new Vector2[count];
            Vector2 flatMean = Vector2.Zero;
            Vector2 uvMean = Vector2.Zero;
            for (int i = 0; i < count; i++)
            {
                flat[i] = new Vector2((old[i] - old[0]).Dot(axisU), (old[i] - old[0]).Dot(axisV));
                flatMean += flat[i] / count;
                uvMean += before.uvs[first + i] / count;
            }

            // 최소 제곱으로 구한 기울기: UV 의 변화 = slope × 면 위의 자리의 변화.
            float sxx = 0f, sxy = 0f, syy = 0f;
            Vector2 sumU = Vector2.Zero;
            Vector2 sumV = Vector2.Zero;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = flat[i] - flatMean;
                Vector2 uvOffset = before.uvs[first + i] - uvMean;
                sxx += offset.X * offset.X;
                sxy += offset.X * offset.Y;
                syy += offset.Y * offset.Y;
                sumU += offset * uvOffset.X;
                sumV += offset * uvOffset.Y;
            }
            float determinant = sxx * syy - sxy * sxy;
            if (Mathf.Abs(determinant) < k_uvDegenerate) return false;
            var slopeU = new Vector2(sumU.X * syy - sumU.Y * sxy, sumU.Y * sxx - sumU.X * sxy) / determinant;
            var slopeV = new Vector2(sumV.X * syy - sumV.Y * sxy, sumV.Y * sxx - sumV.X * sxy) / determinant;

            // 새 면의 축: 처음의 축을 법선이 돈 만큼 돌린다.
            if (normal.Dot(newNormal) > k_uvFlippedDot && !normal.IsEqualApprox(newNormal))
            {
                var turn = new Quaternion(normal, newNormal);
                axisU = turn * axisU;
                axisV = turn * axisV;
            }
            var flatNow = new Vector2[count];
            Vector2 flatNowMean = Vector2.Zero;
            for (int i = 0; i < count; i++)
            {
                flatNow[i] = new Vector2((now[i] - now[0]).Dot(axisU), (now[i] - now[0]).Dot(axisV));
                flatNowMean += flatNow[i] / count;
            }

            Vector2 reference = anchor >= 0 ? flat[anchor] : flatMean;
            Vector2 referenceNow = anchor >= 0 ? flatNow[anchor] : flatNowMean;
            for (int i = 0; i < count; i++)
            {
                if (!moved[i]) continue;
                Vector2 change = (flatNow[i] - referenceNow) - (flat[i] - reference);
                block.uvs[first + i] = before.uvs[first + i] + new Vector2(slopeU.Dot(change), slopeV.Dot(change));
            }
            return true;
        }

        /// <summary>
        /// UV 편집 창을 만든다 (따로 뜨는 창). 위에 도구 줄, 가운데에 그림판, 아래에 안내 글이 있다.
        /// </summary>
        /// <param name="layer">창을 넣을 노드.</param>
        private void BuildUVWindow(Node layer)
        {
            m_uvWindow = new Window { Title = "UV Editor", Size = s_uvWindowSize, MinSize = s_uvWindowMinSize, Visible = false, ForceNative = true, Transient = true };
            m_uvWindow.CloseRequested += () =>
            {
                m_uvCanvas.CancelTransform();
                m_uvWindow.Hide();
            };
            layer.AddChild(m_uvWindow);

            var panel = new PanelContainer();
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            m_uvWindow.AddChild(panel);
            var column = new VBoxContainer();
            panel.AddChild(column);
            var tools = new HFlowContainer();
            column.AddChild(tools);

            m_uvCanvas = new UVCanvas { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            m_uvCanvas.Committed += OnUVCommitted;
            m_uvCanvas.StateChanged += SyncUVControls;
            m_uvCanvas.UndoRequested += Undo;
            m_uvCanvas.RedoRequested += Redo;
            // 마우스가 그림판에 들어오면 키를 받게 한다. 숫자 칸에 글자를 치는 중이면 빼앗지 않는다.
            m_uvCanvas.MouseEntered += () =>
            {
                if (m_uvWindow.GuiGetFocusOwner() is not LineEdit) m_uvCanvas.GrabFocus();
            };

            AddUVToolButton(tools, "All", "Select every corner (A)", () => m_uvCanvas.SelectAll(true));
            AddUVToolButton(tools, "None", "Clear the selection (Alt+A)", () => m_uvCanvas.SelectAll(false));
            AddUVToolButton(tools, "Move", "Move the selected corners (G, or drag a corner). X / Y: only U / only V, click: confirm, right click: cancel", () => m_uvCanvas.BeginTransform(UVCanvas.Mode.Move));
            AddUVToolButton(tools, "Rotate", "Rotate the selected corners around their center (R)", () => m_uvCanvas.BeginTransform(UVCanvas.Mode.Rotate));
            AddUVToolButton(tools, "Scale", "Scale the selected corners around their center (S). X / Y: only U / only V", () => m_uvCanvas.BeginTransform(UVCanvas.Mode.Scale));
            AddUVToolButton(tools, "Frame", "Show every face (F, Home)", () => m_uvCanvas.Frame());
            tools.AddChild(new VSeparator());

            var snap = new CheckBox { Text = "Snap", FocusMode = Control.FocusModeEnum.None, TooltipText = "Snap to the grid while moving (Ctrl turns it on or off for a moment). Rotating snaps to 5 degrees, scaling to 0.1" };
            snap.Toggled += pressed => m_uvCanvas.SnapEnabled = pressed;
            tools.AddChild(snap);
            tools.AddChild(new Label { Text = "Grid" });
            var grid = new SpinBox
            {
                MinValue = UVCanvas.MinGridDivisions, MaxValue = UVCanvas.MaxGridDivisions, Step = 1, Rounded = true, Value = m_uvCanvas.GridDivisions,
                TooltipText = "How many grid cells one texture is divided into (8 means a grid line every 0.125)",
            };
            grid.ValueChanged += value => m_uvCanvas.GridDivisions = (int)value;
            tools.AddChild(grid);
            tools.AddChild(new VSeparator());

            // 선택한 모서리의 값. 고치면 선택한 모서리 전부의 U (또는 V) 가 그 값이 된다.
            string[] names = { "U", "V" };
            for (int axis = 0; axis < m_uvValueBoxes.Length; axis++)
            {
                int index = axis;
                tools.AddChild(new Label { Text = names[axis] });
                SpinBox value = CreateNumberBox(true);
                value.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
                value.TooltipText = $"{names[axis]} of the selected corners. Typing a value gives it to every selected corner";
                value.ValueChanged += changed =>
                {
                    if (!m_fillingUV) m_uvCanvas.SetSelectedAxis(index, (float)changed);
                };
                m_uvValueBoxes[axis] = value;
                tools.AddChild(value);
            }

            column.AddChild(m_uvCanvas);
            m_uvInfo = new Label { ClipText = true };
            column.AddChild(m_uvInfo);
            SyncUVControls();
        }

        /// <summary>
        /// UV 창의 도구 줄에 버튼 하나를 더한다. 누른 뒤에는 그림판이 키를 받게 한다.
        /// </summary>
        /// <param name="row">도구 줄.</param>
        /// <param name="text">버튼의 글자.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="action">누르면 할 일.</param>
        private void AddUVToolButton(Container row, string text, string tooltip, System.Action action)
        {
            var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip };
            button.Pressed += () =>
            {
                m_uvCanvas.GrabFocus();
                action();
            };
            row.AddChild(button);
        }

        /// <summary>
        /// UV 편집 창을 띄운다 (U, Edit 메뉴, 면의 값 칸의 버튼). 면 단위에서는 선택한 면들을, 블록 단위에서는 선택한 블록의 여섯 면을 보여 준다.
        /// </summary>
        private void OpenUVEditor()
        {
            if (m_uvWindow == null) return;
            if (m_editMode != EditMode.Block) SetEditMode(EditMode.Block);

            if (!m_uvWindowPlaced)
            {
                m_uvWindowPlaced = true;
                m_uvWindow.PopupCentered(s_uvWindowSize);
            }
            else
            {
                m_uvWindow.Show();
                m_uvWindow.GrabFocus();
            }
            m_uvFaceKeys.Clear();
            m_uvFaceKeys.Add(-1);
            RefreshUVEditor(false);
            m_uvCanvas.GrabFocus();
            if (m_uvCanvas.FaceCount == 0) SetMessage("UV editor: select faces (3) or blocks (4) to edit their UVs");
        }

        /// <summary>
        /// UV 편집 창을 띄우고, 몇 프레임 뒤에 그 창의 화면을 PNG 로 저장한 뒤 종료하게 한다 (개발용 인자 --uv-shot).
        /// </summary>
        /// <param name="path">저장할 경로.</param>
        private void BeginUVShot(string path)
        {
            OpenUVEditor();
            m_uvCanvas.SelectAll(true);
            m_uvShotPath = path;
            m_uvShotCountdown = k_screenshotWaitFrames;
        }

        /// <summary>
        /// --uv-shot 의 남은 프레임을 세고, 다 되면 UV 창의 화면을 저장하고 종료한다.
        /// </summary>
        private void UpdateUVShot()
        {
            if (m_uvShotCountdown < 0 || m_uvShotCountdown-- > 0) return;

            Error error = m_uvWindow.GetTexture().GetImage().SavePng(m_uvShotPath);
            GD.Print($"UV screenshot {(error == Error.Ok ? "saved" : "failed")}: {m_uvShotPath}");
            GetTree().Quit(error == Error.Ok ? 0 : 1);
        }

        /// <summary>
        /// UV 편집 창을 지금 선택에 맞춘다. 보여 주는 면들이 바뀌었으면 선택을 풀고 시점을 다시 맞추고, 값만 바뀌었으면(편집, 되돌리기) 그대로 둔다.
        /// </summary>
        /// <param name="force">true 면 창이 보이지 않아도 맞춘다.</param>
        private void RefreshUVEditor(bool force)
        {
            if (m_uvCanvas == null || (!force && !m_uvWindow.Visible)) return;

            List<int> keys = m_editMode == EditMode.Block ? TargetFaces() : new List<int>();
            bool same = keys.Count == m_uvFaceKeys.Count;
            for (int i = 0; same && i < keys.Count; i++)
            {
                same = keys[i] == m_uvFaceKeys[i];
            }

            List<BD2Block> blocks = m_document.Blocks.blocks;
            var faces = new List<Vector2[]>(keys.Count);
            foreach (int key in keys)
            {
                var uvs = new Vector2[BD2Block.FaceVertexCount];
                System.Array.Copy(blocks[key / ElementStride].uvs, key % ElementStride * BD2Block.FaceVertexCount, uvs, 0, uvs.Length);
                faces.Add(uvs);
            }

            // 배경은 맨 앞 면의 텍스처다.
            Texture2D texture = null;
            if (keys.Count > 0)
            {
                List<BlockTextureData> textures = m_document.Textures.blockTextureData;
                int index = blocks[keys[0] / ElementStride].textureIndices[keys[0] % ElementStride];
                string path = index >= 0 && index < textures.Count ? GamePath.Resolve(textures[index].diffusePath) : null;
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) texture = ImageLoader.LoadTexture(path);
            }

            m_uvCanvas.SetFaces(faces, texture, same);
            if (!same)
            {
                m_uvFaceKeys.Clear();
                m_uvFaceKeys.AddRange(keys);
                // 창을 막 띄웠으면 그림판의 크기가 아직 정해지지 않았다. 배치가 끝난 뒤에 맞춘다.
                Callable.From(m_uvCanvas.Frame).CallDeferred();
            }
            SyncUVControls();
        }

        /// <summary>
        /// UV 창의 숫자 칸과 안내 글을 그림판의 지금 상태에 맞춘다.
        /// </summary>
        private void SyncUVControls()
        {
            if (m_uvInfo == null) return;

            bool selected = m_uvCanvas.TryGetSelectedUV(out Vector2 uv);
            m_fillingUV = true;
            for (int axis = 0; axis < m_uvValueBoxes.Length; axis++)
            {
                m_uvValueBoxes[axis].Editable = selected && !m_uvCanvas.Transforming;
                m_uvValueBoxes[axis].SetValueNoSignal(selected ? uv[axis] : 0.0);
            }
            m_fillingUV = false;

            if (m_uvCanvas.FaceCount == 0)
            {
                m_uvInfo.Text = "Select faces (3) or blocks (4) in block mode";
            }
            else if (m_uvCanvas.Transforming)
            {
                m_uvInfo.Text = $"{m_uvCanvas.TransformMode}   |   X / Y: only U / only V, Ctrl: snap on/off, click/Enter: confirm, right click/Esc: cancel";
            }
            else
            {
                m_uvInfo.Text = $"{m_uvCanvas.FaceCount} face(s), {m_uvCanvas.SelectedCount} corner(s) selected   |   click / Shift / drag a box: select, drag a corner or G: move, R: rotate, S: scale, middle drag: pan, wheel: zoom";
            }
        }

        /// <summary>
        /// 그림판에서 확정한 UV 를 문서의 블록에 넣는다 (되돌리기 한 번).
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        private void OnUVCommitted(string name)
        {
            var blockIndices = new SortedSet<int>();
            foreach (int key in m_uvFaceKeys)
            {
                blockIndices.Add(key / ElementStride);
            }
            EditBlocks(name, blockIndices, (index, block) =>
            {
                for (int i = 0; i < m_uvFaceKeys.Count && i < m_uvCanvas.FaceCount; i++)
                {
                    int key = m_uvFaceKeys[i];
                    if (key / ElementStride != index) continue;
                    System.Array.Copy(m_uvCanvas.GetFace(i), 0, block.uvs, key % ElementStride * BD2Block.FaceVertexCount, BD2Block.FaceVertexCount);
                }
            });
            // 문서에 넣지 못했으면(3D 화면에서 변형하는 중) 그림판을 문서의 값으로 되돌린다.
            RefreshUVEditor(true);
        }
    }
}
