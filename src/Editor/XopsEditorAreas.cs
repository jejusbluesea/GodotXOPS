using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    // XopsEditor 의 구역 담당 partial: 블록 패널 아래의 구역 태그 목록과 성질, 블록을 구역으로 바꾸기와 그 반대, 구역의 덧그림.
    // 구역 블록은 직접 옮기지 않는다: 모양을 고치려면 구역을 블록으로 되돌려 평소의 블록 도구로 고친 뒤 다시 구역으로 만든다.
    public partial class XopsEditor
    {
        private static readonly string[] s_hostilityModeNames = { "always", "never", "provoked" };

        private AreaOverlay m_areaOverlay;
        private ItemList m_areaTagList;
        private int m_areaTagSelected = -1;
        private Control m_areaTagBox;
        private LineEdit m_areaName;
        private SpinBox m_areaDamage;
        private SpinBox m_areaGravity;
        private SpinBox m_areaSight;
        private SpinBox m_areaReverb;
        private CheckBox m_areaBlockWeather;
        private LineEdit m_areaHostility;
        private CheckBox m_areaShow;

        /// <summary>
        /// 블록 패널 아래에 구역의 칸들을 만든다.
        /// </summary>
        /// <param name="box">블록 패널.</param>
        private void BuildAreaInspector(VBoxContainer box)
        {
            box.AddChild(new HSeparator());
            var header = new HBoxContainer();
            box.AddChild(header);
            header.AddChild(new Label { Text = "Areas of this map", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            m_areaShow = new CheckBox { Text = "Show", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "Draw the area blocks (also in point mode)" };
            m_areaShow.Toggled += _ => RedrawAreas();
            header.AddChild(m_areaShow);

            m_areaTagList = new ItemList { CustomMinimumSize = new Vector2(0f, 80f), FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Area tags. All area blocks with the same tag make one area. Events point at an area by this number" };
            m_areaTagList.ItemSelected += index =>
            {
                m_areaTagSelected = (int)index;
                RefreshAreaInspector();
                RedrawAreas();
            };
            box.AddChild(m_areaTagList);

            var tagButtons = new HBoxContainer();
            box.AddChild(tagButtons);
            AddAreaButton(tagButtons, "Add tag", "Add an area tag", AddAreaTag);
            AddAreaButton(tagButtons, "Delete tag", "Delete the selected tag. Only a tag that no area block uses can be deleted; the numbers after it move up by one", DeleteAreaTag);

            var convert = new HBoxContainer();
            box.AddChild(convert);
            AddAreaButton(convert, "Blocks → area", "Turn the selected blocks (block selection) into area blocks of the selected tag. The block numbers after them move up", BlocksToArea);
            AddAreaButton(convert, "Area → blocks", "Turn every area block of the selected tag back into blocks (not drawn, nothing collides) to reshape them with the block tools", AreaToBlocks);

            var tagBox = new GridContainer { Columns = 2 };
            tagBox.AddThemeConstantOverride("h_separation", 8);
            m_areaTagBox = tagBox;
            box.AddChild(tagBox);

            tagBox.AddChild(new Label { Text = "Name" });
            m_areaName = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            m_areaName.TextSubmitted += text => SetAreaTag("Rename area", tag => tag.name = text);
            m_areaName.FocusExited += () =>
            {
                AreaTagData tag = SelectedAreaTag();
                if (tag != null && tag.name != m_areaName.Text) SetAreaTag("Rename area", t => t.name = m_areaName.Text);
            };
            tagBox.AddChild(m_areaName);
            m_areaDamage = AddAreaNumber(tagBox, "Damage/s", "HP per second taken by a human standing in the area (invincible humans take none, armor does not help)",
                0, 10000, value => SetAreaTag("Area damage", tag => tag.damagePerSecond = value));
            m_areaGravity = AddAreaNumber(tagBox, "Gravity", "Gravity scale for humans in the area. Negative: they float up", -10, 10,
                value => SetAreaTag("Area gravity", tag => tag.gravityScale = value));
            m_areaSight = AddAreaNumber(tagBox, "AI sight", "Scale of how far an AI in the area can see", 0, 10,
                value => SetAreaTag("Area AI sight", tag => tag.aiSightScale = value));
            m_areaReverb = AddAreaNumber(tagBox, "Reverb", "Reverb on sounds while the camera is in the area (0 to 1, indoors)", 0, 1,
                value => SetAreaTag("Area reverb", tag => tag.soundReverb = value));
            tagBox.AddChild(new Label { Text = "Weather" });
            m_areaBlockWeather = new CheckBox { Text = "Blocked (indoors)", FocusMode = Control.FocusModeEnum.None, TooltipText = "No rain or snow while the camera is in the area, and the weather sound is quieter" };
            m_areaBlockWeather.Toggled += pressed =>
            {
                if (!m_fillingInspector) SetAreaTag("Area weather", tag => tag.blockWeather = pressed);
            };
            tagBox.AddChild(m_areaBlockWeather);
            tagBox.AddChild(new Label { Text = "Hostility" });
            m_areaHostility = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "1>0 never, 2>0 provoked",
                TooltipText = "Rules for AIs in the area: team>target mode, separated by commas. Modes: always, never, provoked (hostile after seeing one armed or being attacked). Teams without a rule are always hostile",
            };
            m_areaHostility.TextSubmitted += text => SetAreaHostility(text);
            m_areaHostility.FocusExited += () => SetAreaHostility(m_areaHostility.Text);
            tagBox.AddChild(m_areaHostility);
        }

        /// <summary>
        /// 구역 칸에 버튼 하나를 더한다.
        /// </summary>
        /// <param name="row">버튼을 넣을 줄.</param>
        /// <param name="text">글자.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="action">누르면 할 일.</param>
        private static void AddAreaButton(HBoxContainer row, string text, string tooltip, Action action)
        {
            var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip };
            button.Pressed += action;
            row.AddChild(button);
        }

        /// <summary>
        /// 구역 태그의 숫자 칸 하나를 만든다.
        /// </summary>
        /// <param name="grid">칸을 넣을 격자.</param>
        /// <param name="label">이름.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="min">최솟값.</param>
        /// <param name="max">최댓값.</param>
        /// <param name="apply">값이 바뀌면 할 일.</param>
        /// <returns>칸.</returns>
        private SpinBox AddAreaNumber(GridContainer grid, string label, string tooltip, double min, double max, Action<float> apply)
        {
            grid.AddChild(new Label { Text = label, TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Stop });
            SpinBox box = CreateNumberBox(true);
            box.MinValue = min;
            box.MaxValue = max;
            box.TooltipText = tooltip;
            box.ValueChanged += value =>
            {
                if (!m_fillingInspector) apply((float)value);
            };
            grid.AddChild(box);
            return box;
        }

        /// <summary>
        /// 고른 구역 태그. 없으면 null.
        /// </summary>
        /// <returns>태그.</returns>
        private AreaTagData SelectedAreaTag()
        {
            List<AreaTagData> tags = m_document.Textures.areaTagData;
            return m_areaTagSelected >= 0 && m_areaTagSelected < tags.Count ? tags[m_areaTagSelected] : null;
        }

        /// <summary>
        /// 구역의 칸들을 문서에 맞춘다.
        /// </summary>
        private void RefreshAreaInspector()
        {
            if (m_areaTagList == null) return;

            List<AreaTagData> tags = m_document.Textures.areaTagData;
            if (m_areaTagSelected >= tags.Count) m_areaTagSelected = tags.Count - 1;

            m_fillingInspector = true;
            m_areaTagList.Clear();
            for (int i = 0; i < tags.Count; i++)
            {
                int count = 0;
                foreach (BD2Area area in m_document.Blocks.areas)
                {
                    if (area.tag == i) count++;
                }
                m_areaTagList.AddItem($"{i}  {tags[i].name}  ({count} block{(count == 1 ? "" : "s")})");
                m_areaTagList.SetItemCustomFgColor(i, AreaOverlay.TagColor(i, 1f));
            }
            if (m_areaTagSelected >= 0) m_areaTagList.Select(m_areaTagSelected);

            AreaTagData tag = SelectedAreaTag();
            m_areaTagBox.Visible = tag != null;
            if (tag != null)
            {
                if (!m_areaName.HasFocus()) m_areaName.Text = tag.name;
                m_areaDamage.Value = tag.damagePerSecond;
                m_areaGravity.Value = tag.gravityScale;
                m_areaSight.Value = tag.aiSightScale;
                m_areaReverb.Value = tag.soundReverb;
                m_areaBlockWeather.ButtonPressed = tag.blockWeather;
                if (!m_areaHostility.HasFocus()) m_areaHostility.Text = FormatHostility(tag.hostility);
            }
            m_fillingInspector = false;
        }

        /// <summary>
        /// 구역의 덧그림을 문서에 맞춘다. 블록 모드와 포인트 모드에서 Show 가 켜져 있을 때 그린다.
        /// </summary>
        private void RedrawAreas()
        {
            if (m_areaOverlay == null) return;

            m_areaOverlay.Visible = m_areaShow == null || m_areaShow.ButtonPressed;
            if (m_areaOverlay.Visible) m_areaOverlay.Redraw(m_document.Blocks.areas, m_areaTagSelected);
        }

        /// <summary>
        /// 구역을 바꾸는 편집 하나를 한다 (되돌릴 수 있다). 바꾼 뒤 블록과 구역을 다시 만든다.
        /// </summary>
        /// <param name="name">편집의 이름 (영어).</param>
        /// <param name="change">문서를 바꾸는 일. false 를 돌려주면 바꾸지 않은 것으로 친다.</param>
        private void EditAreas(string name, Func<bool> change)
        {
            AreaEditCommand.Snapshot before = AreaEditCommand.Take(m_document);
            if (!change()) return;

            m_history.Push(new AreaEditCommand(name, m_document, before));
            m_blockDirty = true;
            RebuildBlocks();
            RefreshAreaInspector();
            RedrawAreas();
            SetMessage(name);
        }

        /// <summary>
        /// 고른 태그의 값 하나를 바꾼다.
        /// </summary>
        /// <param name="name">편집의 이름.</param>
        /// <param name="change">태그를 바꾸는 일.</param>
        private void SetAreaTag(string name, Action<AreaTagData> change)
        {
            if (SelectedAreaTag() == null) return;
            EditAreas(name, () =>
            {
                change(SelectedAreaTag());
                return true;
            });
        }

        private void AddAreaTag()
        {
            EditAreas("Add area tag", () =>
            {
                List<AreaTagData> tags = m_document.Textures.areaTagData;
                tags.Add(new AreaTagData { name = $"area{tags.Count}" });
                m_areaTagSelected = tags.Count - 1;
                return true;
            });
        }

        private void DeleteAreaTag()
        {
            int tag = m_areaTagSelected;
            if (SelectedAreaTag() == null)
            {
                SetMessage("Select an area tag first");
                return;
            }
            if (m_document.Blocks.areas.Exists(area => area.tag == tag))
            {
                SetMessage("Area blocks use this tag. Turn them back into blocks or delete them first");
                return;
            }
            EditAreas("Delete area tag", () =>
            {
                m_document.Textures.areaTagData.RemoveAt(tag);
                foreach (BD2Area area in m_document.Blocks.areas)
                {
                    if (area.tag > tag) area.tag--;
                }
                m_areaTagSelected = Math.Min(tag, m_document.Textures.areaTagData.Count - 1);
                return true;
            });
        }

        /// <summary>
        /// 고른 블록들을 고른 태그의 구역 블록으로 바꾼다. 블록 단위로 고른 것만 바꾼다.
        /// </summary>
        private void BlocksToArea()
        {
            if (SelectedAreaTag() == null)
            {
                SetMessage("Select an area tag first");
                return;
            }
            if (m_blockElement != BlockElement.Block || m_blockSelection.Count == 0)
            {
                SetMessage("Select whole blocks first (block selection, key 4)");
                return;
            }

            var indices = new List<int>();
            foreach (int key in m_blockSelection) indices.Add(key / ElementStride);
            indices.Sort();
            indices.Reverse();
            int tag = m_areaTagSelected;
            EditAreas($"Blocks to area ({indices.Count})", () =>
            {
                List<BD2Block> blocks = m_document.Blocks.blocks;
                foreach (int index in indices)
                {
                    m_document.Blocks.areas.Add(new BD2Area { vertices = (Vector3[])blocks[index].vertices.Clone(), tag = tag });
                    blocks.RemoveAt(index);
                }
                m_blockSelection.Clear();
                return true;
            });
        }

        /// <summary>
        /// 고른 태그의 구역 블록을 전부 블록으로 되돌린다. 그리지 않고 어느 판정에도 걸리지 않는 블록이고, 블록 목록의 끝에 붙어 선택된다.
        /// </summary>
        private void AreaToBlocks()
        {
            int tag = m_areaTagSelected;
            if (SelectedAreaTag() == null || !m_document.Blocks.areas.Exists(area => area.tag == tag))
            {
                SetMessage("Select an area tag that has area blocks");
                return;
            }

            EditAreas("Area to blocks", () =>
            {
                List<BD2Block> blocks = m_document.Blocks.blocks;
                m_blockSelection.Clear();
                SetBlockElement(BlockElement.Block);
                foreach (BD2Area area in m_document.Blocks.areas.FindAll(a => a.tag == tag))
                {
                    var block = new BD2Block
                    {
                        vertices = (Vector3[])area.vertices.Clone(),
                        flags = BD2File.PassHuman | BD2File.PassBullet | BD2File.PassSight,
                    };
                    for (int f = 0; f < BD2Block.FaceCount; f++) block.textureIndices[f] = k_noTexture;
                    m_blockSelection.Add(blocks.Count * ElementStride);
                    blocks.Add(block);
                }
                m_document.Blocks.areas.RemoveAll(a => a.tag == tag);
                return true;
            });
        }

        /// <summary>
        /// 적대 규칙의 글을 읽어 고른 태그에 넣는다. 읽지 못한 조각이 있으면 바꾸지 않고 알린다.
        /// </summary>
        /// <param name="text">"team>target mode" 를 쉼표로 이은 글.</param>
        private void SetAreaHostility(string text)
        {
            AreaTagData tag = SelectedAreaTag();
            if (tag == null || m_fillingInspector) return;
            if (!TryParseHostility(text, out List<AreaHostilityRule> rules))
            {
                SetMessage("Hostility: write rules like \"1>0 never, 2>0 provoked\"");
                return;
            }
            if (FormatHostility(rules) == FormatHostility(tag.hostility)) return;

            SetAreaTag("Area hostility", t =>
            {
                t.hostility.Clear();
                t.hostility.AddRange(rules);
            });
        }

        /// <summary>
        /// 적대 규칙을 한 줄의 글로 적는다.
        /// </summary>
        /// <param name="rules">규칙들.</param>
        /// <returns>"1>0 never, 2>0 provoked" 꼴의 글.</returns>
        private static string FormatHostility(List<AreaHostilityRule> rules)
        {
            var parts = new List<string>();
            foreach (AreaHostilityRule rule in rules)
            {
                int mode = Math.Clamp((int)rule.mode, 0, s_hostilityModeNames.Length - 1);
                parts.Add($"{rule.team}>{rule.targetTeam} {s_hostilityModeNames[mode]}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// 적대 규칙의 글을 읽는다.
        /// </summary>
        /// <param name="text">"team>target mode" 를 쉼표로 이은 글. 비어 있으면 규칙이 없다.</param>
        /// <param name="rules">읽은 규칙들.</param>
        /// <returns>전부 읽었으면 true.</returns>
        private static bool TryParseHostility(string text, out List<AreaHostilityRule> rules)
        {
            rules = new List<AreaHostilityRule>();
            foreach (string part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string[] teams = words.Length == 2 ? words[0].Split('>') : Array.Empty<string>();
                int mode = words.Length == 2 ? Array.IndexOf(s_hostilityModeNames, words[1].ToLowerInvariant()) : -1;
                if (teams.Length != 2 || mode < 0
                    || !int.TryParse(teams[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int team)
                    || !int.TryParse(teams[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int target))
                {
                    return false;
                }
                rules.Add(new AreaHostilityRule { team = team, targetTeam = target, mode = (AreaHostility)mode });
            }
            return true;
        }
    }
}
