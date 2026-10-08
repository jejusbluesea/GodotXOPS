using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        private const float k_previewWidth = 360f;
        private const float k_previewHeight = 320f;
        // 미리 보기의 바닥 격자: 한 칸의 크기 (m)와 가운데에서 뻗는 칸 수.
        private const float k_previewGridStep = 0.5f;
        private const int k_previewGridCells = 4;
        // 미리 보기를 처음 띄울 때 보는 각도 (도).
        private const float k_previewYaw = 150f;
        private const float k_previewPitch = 20f;
        // 보여 줄 것이 아주 작아도 이만큼(m)은 떨어져서 본다.
        private const float k_previewMinRadius = 0.2f;
        // 충돌 형상의 원을 이만큼의 선분으로 그린다.
        private const int k_circleSegments = 24;
        private static readonly Color s_previewGridColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color s_previewColliderColor = new Color(0.3f, 1f, 0.4f, 0.9f);
        private static readonly string[] s_imageExtensions = { ".bmp", ".png", ".jpg", ".jpeg", ".dds", ".tga" };

        private Control m_previewBox;
        private SubViewport m_previewViewport;
        private EditorCamera m_previewView;
        private Node3D m_previewRoot;
        private TextureRect m_previewImage;
        private SubViewportContainer m_previewContainer;
        private Label m_previewCaption;
        private bool m_previewDragging;
        // 지금 미리 보는 데이터 객체. 같은 것을 다시 보여 줄 때는 시점을 그대로 둔다.
        private object m_previewSubject;
        // 팔과 다리는 메시가 여럿이다 (무기마다의 팔 자세, 다리 애니메이션의 프레임). 그 가운데 몇 번째를 보여 줄지와, 고르는 칸.
        private int m_previewArm;
        private int m_previewLeg;
        private int m_previewArmCount;
        private int m_previewLegCount;
        private Control m_previewArmBox;
        private Control m_previewLegBox;
        private Label m_previewArmLabel;
        private Label m_previewLegLabel;
        // 이펙트를 미리 볼 때의 시점: 보는 자리의 높이와 거리 (m).
        private const float k_effectFocusHeight = 0.3f;
        private const float k_effectFocusRadius = 0.8f;

        /// <summary>
        /// 에셋 모드의 미리 보기 칸을 만든다: 자기만의 3D 공간을 가진 화면(모델, 충돌 형상)과 이미지 칸. 3D 화면은 끌어서 돌리고 휠로 확대한다.
        /// </summary>
        /// <param name="parent">칸을 넣을 가로 상자.</param>
        private void BuildAssetPreview(HBoxContainer parent)
        {
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(k_previewWidth, 0f) };
            m_previewBox = box;
            parent.AddChild(box);
            box.AddChild(new Label { Text = "Preview" });

            m_previewContainer = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(k_previewWidth, k_previewHeight) };
            m_previewContainer.GuiInput += OnPreviewInput;
            box.AddChild(m_previewContainer);
            m_previewViewport = new SubViewport { OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            m_previewContainer.AddChild(m_previewViewport);

            var camera = new Camera3D { Name = "PreviewCamera" };
            m_previewViewport.AddChild(camera);
            MapLoader.ApplyCameraSettings(camera);
            camera.Current = true;
            m_previewView = new EditorCamera(camera);
            m_previewView.SetAngles(k_previewYaw, k_previewPitch);
            m_previewView.Focus(new Vector3(0f, k_effectFocusHeight, 0f), k_effectFocusRadius);

            m_previewViewport.AddChild(BuildPreviewGrid());
            m_previewRoot = new Node3D { Name = "Subject" };
            m_previewViewport.AddChild(m_previewRoot);

            m_previewImage = new TextureRect
            {
                CustomMinimumSize = new Vector2(k_previewWidth, k_previewHeight),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Visible = false,
            };
            box.AddChild(m_previewImage);

            var variants = new HBoxContainer();
            box.AddChild(variants);
            m_previewArmBox = BuildVariantBox(variants, "Arm", step => m_previewArm += step, out m_previewArmLabel);
            m_previewLegBox = BuildVariantBox(variants, "Leg", step => m_previewLeg += step, out m_previewLegLabel);

            m_previewCaption = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = s_hintColor };
            box.AddChild(m_previewCaption);
            ShowPreviewNothing();
        }

        /// <summary>
        /// 팔이나 다리의 메시를 넘겨 보는 칸을 만든다: 앞으로, 몇 번째인지, 뒤로.
        /// </summary>
        /// <param name="parent">칸을 넣을 가로 상자.</param>
        /// <param name="title">이름.</param>
        /// <param name="move">번호를 그만큼 옮기는 함수.</param>
        /// <param name="label">몇 번째인지를 적는 라벨.</param>
        /// <returns>만든 칸 (감춰진 채로 만든다).</returns>
        private Control BuildVariantBox(HBoxContainer parent, string title, Action<int> move, out Label label)
        {
            var box = new HBoxContainer { Visible = false };
            parent.AddChild(box);
            box.AddChild(new Label { Text = title });
            AddDialogButton(box, "<", () =>
            {
                move(-1);
                RefreshAssetPreview();
            });
            label = new Label();
            box.AddChild(label);
            AddDialogButton(box, ">", () =>
            {
                move(1);
                RefreshAssetPreview();
            });
            return box;
        }

        /// <summary>
        /// 번호를 목록의 범위 안으로 돌려 맞춘다 (끝을 넘으면 처음으로).
        /// </summary>
        /// <param name="index">번호.</param>
        /// <param name="count">목록의 길이.</param>
        /// <returns>0 이상 count 미만의 번호. 목록이 비었으면 0.</returns>
        private static int WrapIndex(int index, int count)
        {
            return count <= 0 ? 0 : ((index % count) + count) % count;
        }

        /// <summary>
        /// 미리 보기의 바닥 격자를 만든다 (크기를 가늠하는 기준).
        /// </summary>
        /// <returns>격자 노드.</returns>
        private static MeshInstance3D BuildPreviewGrid()
        {
            var mesh = new ImmediateMesh();
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            float reach = k_previewGridStep * k_previewGridCells;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
            for (int i = -k_previewGridCells; i <= k_previewGridCells; i++)
            {
                float offset = i * k_previewGridStep;
                foreach (Vector3 point in new[] { new Vector3(offset, 0f, -reach), new Vector3(offset, 0f, reach), new Vector3(-reach, 0f, offset), new Vector3(reach, 0f, offset) })
                {
                    mesh.SurfaceSetColor(s_previewGridColor);
                    mesh.SurfaceAddVertex(point);
                }
            }
            mesh.SurfaceEnd();
            return new MeshInstance3D { Name = "Grid", Mesh = mesh };
        }

        /// <summary>
        /// 미리 보기 화면에서의 마우스: 왼쪽이나 가운데 버튼으로 끌면 돌리고, 휠로 확대·축소한다.
        /// </summary>
        /// <param name="inputEvent">입력 이벤트.</param>
        private void OnPreviewInput(InputEvent inputEvent)
        {
            switch (inputEvent)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle } button:
                    m_previewDragging = button.Pressed;
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                    m_previewView.Zoom(1f);
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                    m_previewView.Zoom(-1f);
                    break;
                case InputEventMouseMotion motion when m_previewDragging:
                    m_previewView.Orbit(motion.Relative);
                    break;
            }
        }

        /// <summary>
        /// 트리에서 선택한 줄에 맞는 미리 보기를 띄운다. 선택한 값이 이미지 파일이면 그 그림을, 아니면 선택한 줄이 속한 항목(무기, 오브젝트, 사람과 그 모델·충돌 형상)의 모델을 보여 준다.
        /// </summary>
        private void RefreshAssetPreview()
        {
            if (m_previewRoot == null) return;

            AssetNode selected = m_assetTree == null ? null : AssetNodeOf(m_assetTree.GetSelected());
            if (selected == null)
            {
                ShowPreviewNothing();
                return;
            }

            if (selected.Type == typeof(string) && TryShowPreviewImage(selected.Get() as string)) return;

            // 선택한 줄에서 위로 올라가며 미리 볼 수 있는 항목을 찾는다 (모델의 메시 한 줄을 골라도 그 모델 전체가 나온다).
            for (AssetNode node = selected; node != null; node = node.Parent)
            {
                object value = node.Type.IsValueType || node.Type == typeof(string) ? null : node.Get();
                if (value != null && ShowPreviewModel(value)) return;
            }
            ShowPreviewNothing();
        }

        /// <summary>
        /// 미리 볼 것이 없을 때의 화면.
        /// </summary>
        private void ShowPreviewNothing()
        {
            ClearPreviewSubject();
            m_previewSubject = null;
            m_previewContainer.Visible = true;
            m_previewImage.Visible = false;
            m_previewArmBox.Visible = false;
            m_previewLegBox.Visible = false;
            m_previewCaption.Text = "Select a weapon, object, human or effect (or one of their models, colliders or image paths) to see it here.\nDrag to turn, wheel to zoom. One grid square is 0.5 m.";
        }

        /// <summary>
        /// 미리 보기 공간의 모델을 지운다.
        /// </summary>
        private void ClearPreviewSubject()
        {
            foreach (Node child in m_previewRoot.GetChildren())
            {
                m_previewRoot.RemoveChild(child);
                child.Free();
            }
        }

        /// <summary>
        /// 글이 이미지 파일의 경로면 그 그림을 보여 준다.
        /// </summary>
        /// <param name="path">경로 (exe 폴더 기준).</param>
        /// <returns>그림을 띄웠으면 true.</returns>
        private bool TryShowPreviewImage(string path)
        {
            if (string.IsNullOrEmpty(path) || Array.IndexOf(s_imageExtensions, Path.GetExtension(path).ToLowerInvariant()) < 0) return false;

            string fullPath = GamePath.Resolve(path);
            ImageTexture texture = fullPath != null && File.Exists(fullPath) ? ImageLoader.LoadTexture(fullPath) : null;
            ClearPreviewSubject();
            m_previewSubject = null;
            m_previewContainer.Visible = false;
            m_previewImage.Visible = true;
            m_previewArmBox.Visible = false;
            m_previewLegBox.Visible = false;
            m_previewImage.Texture = texture;
            m_previewCaption.Text = texture != null ? $"{path}\n{texture.GetWidth()} x {texture.GetHeight()}" : $"{path}\n(the file could not be read)";
            return true;
        }

        /// <summary>
        /// 데이터 객체 하나의 모델을 미리 보기 공간에 만든다. 미리 볼 수 있는 종류가 아니면 아무것도 하지 않는다.
        /// 다른 목록을 번호로 가리키는 값(무기의 모델 번호 등)은 열려 있는 파일의 고친 내용을 먼저, 없으면 게임이 읽어 둔 데이터를 따라간다.
        /// </summary>
        /// <param name="value">데이터 객체.</param>
        /// <returns>모델을 띄웠으면 true.</returns>
        private bool ShowPreviewModel(object value)
        {
            var subject = new Node3D { Name = "Model" };
            string caption;
            // 다른 것을 보기 시작하면 팔과 다리는 첫 메시부터다.
            if (!ReferenceEquals(m_previewSubject, value))
            {
                m_previewArm = 0;
                m_previewLeg = 0;
            }
            m_previewArmCount = 0;
            m_previewLegCount = 0;
            switch (value)
            {
                case EffectData effect:
                {
                    var player = new EffectPreview { Name = "Effect" };
                    subject.AddChild(player);
                    player.Play(effect, index => LookupData<EffectTextureData>(typeof(EffectParameterData), nameof(EffectParameterData.effectTextureData), index)?.texturePath);
                    caption = $"Effect: {effect.name}\n{effect.emitters.Count} emitter(s), played again and again. Emitters that count by damage use {EffectPreview.TriggerValue}. Hitting the map is not shown";
                    break;
                }

                case WeaponModelData model:
                    WeaponVisual.BuildModelParts(subject, model.textures, model.modelData);
                    caption = $"Weapon model: {model.name}";
                    break;

                case WeaponData weapon:
                {
                    WeaponModelData model = LookupData<WeaponModelData>(typeof(WeaponParameterData), nameof(WeaponParameterData.weaponModelData), weapon.modelIndex);
                    if (model != null) WeaponVisual.BuildModelParts(subject, model.textures, model.modelData);
                    // 떨어진 무기와 같은 크기로 보여 준다.
                    subject.Scale = Vector3.One * Mathf.Max(1e-4f, weapon.size * DataManager.Instance.WeaponParameterData.weaponGeneralData.weaponScale);
                    caption = model != null ? $"Weapon: {weapon.name}\nmodel {weapon.modelIndex} ({model.name}), at its size on the ground" : $"Weapon: {weapon.name}\nno model {weapon.modelIndex}";
                    break;
                }

                case ObjectModelData model:
                    AddObjectModel(subject, model);
                    caption = "Object model, at its size in the game";
                    break;

                case ObjectColliderData collider:
                    subject.AddChild(BuildColliderWire(collider));
                    caption = $"Object collider: {collider.shapes.Count} shape(s)";
                    break;

                case ObjectData data:
                {
                    ObjectModelData model = LookupData<ObjectModelData>(typeof(ObjectParameterData), nameof(ObjectParameterData.objectModelData), data.modelIndex);
                    ObjectColliderData collider = LookupData<ObjectColliderData>(typeof(ObjectParameterData), nameof(ObjectParameterData.objectColliderData), data.colliderIndex);
                    if (model != null) AddObjectModel(subject, model);
                    if (collider != null) subject.AddChild(BuildColliderWire(collider));
                    caption = $"Object: {data.name}\nmodel {data.modelIndex}{(model == null ? " (missing)" : string.Empty)}, collider {data.colliderIndex}{(collider == null ? " (missing)" : string.Empty)} in green";
                    break;
                }

                case HumanModelData model:
                    AddHumanModel(subject, model);
                    caption = $"Human model: {model.name}\narms {model.armIndex}, legs {model.legIndex}";
                    break;

                case HumanData human:
                {
                    HumanModelData model = LookupData<HumanModelData>(typeof(HumanParameterData), nameof(HumanParameterData.humanModelData), human.modelIndex);
                    if (model != null) AddHumanModel(subject, model);
                    caption = model != null ? $"Human: {human.name}\nmodel {human.modelIndex} ({model.name})" : $"Human: {human.name}\nno model {human.modelIndex}";
                    break;
                }

                case HumanArmModelData arms:
                    m_previewArmCount = Mathf.Max(arms.leftArms.Count, arms.rightArms.Count);
                    m_previewArm = WrapIndex(m_previewArm, m_previewArmCount);
                    AddPlainMesh(subject, m_previewArm < arms.leftArms.Count ? arms.leftArms[m_previewArm] : null);
                    AddPlainMesh(subject, m_previewArm < arms.rightArms.Count ? arms.rightArms[m_previewArm] : null);
                    caption = $"Arms: {arms.name}\nleft and right mesh {m_previewArm}, no texture. Weapon models choose the mesh with leftArmIndex / rightArmIndex";
                    break;

                case HumanLegModelData legs:
                    m_previewLegCount = legs.legs.Count;
                    m_previewLeg = WrapIndex(m_previewLeg, m_previewLegCount);
                    AddPlainMesh(subject, m_previewLegCount > 0 ? legs.legs[m_previewLeg] : null);
                    caption = $"Legs: {legs.name}\nmesh {m_previewLeg}, no texture. Leg animations go through these meshes";
                    break;

                default:
                    subject.Free();
                    return false;
            }

            ClearPreviewSubject();
            m_previewRoot.AddChild(subject);
            m_previewContainer.Visible = true;
            m_previewImage.Visible = false;
            m_previewCaption.Text = caption;
            m_previewArmBox.Visible = m_previewArmCount > 1;
            m_previewLegBox.Visible = m_previewLegCount > 1;
            m_previewArmLabel.Text = $"{m_previewArm + 1} / {m_previewArmCount}";
            m_previewLegLabel.Text = $"{m_previewLeg + 1} / {m_previewLegCount}";

            // 다른 것을 보기 시작할 때만 시점을 맞춘다. 같은 것의 값을 고치는 동안에는 보던 각도와 거리를 그대로 둔다.
            if (!ReferenceEquals(m_previewSubject, value))
            {
                m_previewSubject = value;
                if (value is EffectData) m_previewView.Focus(new Vector3(0f, k_effectFocusHeight, 0f), k_effectFocusRadius);
                else FocusPreview(subject);
            }
            return true;
        }

        /// <summary>
        /// 오브젝트 모델을 게임에서와 같은 크기와 방향으로 더한다 (SmallObject 의 Visual 노드와 같다).
        /// </summary>
        /// <param name="parent">더할 부모.</param>
        /// <param name="model">오브젝트 모델 데이터.</param>
        private static void AddObjectModel(Node3D parent, ObjectModelData model)
        {
            var visual = new Node3D
            {
                Name = "Visual",
                Rotation = new Vector3(0f, Mathf.Pi, 0f),
                Scale = Vector3.One * Mathf.Max(1e-4f, DataManager.Instance.ObjectParameterData.objectGeneralData.modelScale),
            };
            parent.AddChild(visual);
            WeaponVisual.BuildModelParts(visual, model.textures, model.modelData);
        }

        /// <summary>
        /// 사람 모델을 더한다: 몸통, 팔, 다리. 팔과 다리는 메시 목록에서 고른 번호의 것이다 (m_previewArm, m_previewLeg). 높이와 크기는 게임과 같이 사람의 공통 설정을 따른다 (HumanVisual.CreateHumanVisual).
        /// </summary>
        /// <param name="parent">더할 부모.</param>
        /// <param name="model">사람 모델 데이터.</param>
        private void AddHumanModel(Node3D parent, HumanModelData model)
        {
            HumanGeneralData general = DataManager.Instance.HumanParameterData.humanGeneralData;
            ShaderMaterial MaterialAt(int index) => MapLoader.GetEntityMaterial(index >= 0 && index < model.textures.Count ? model.textures[index] : null);

            var body = new Node3D { Name = "Body", Position = new Vector3(0f, general.humanBodyHeight, 0f), Scale = Vector3.One * general.humanBodyScale };
            parent.AddChild(body);
            WeaponVisual.BuildModelParts(body, model.textures, model.modelData);

            HumanArmModelData arms = LookupData<HumanArmModelData>(typeof(HumanParameterData), nameof(HumanParameterData.humanArmModelData), model.armIndex);
            if (arms != null)
            {
                var armRoot = new Node3D { Name = "Arms", Position = new Vector3(0f, general.humanArmHeight, 0f), Scale = Vector3.One * general.humanArmScale };
                parent.AddChild(armRoot);
                ShaderMaterial material = MaterialAt(model.armTextureIndex);
                m_previewArmCount = Mathf.Max(arms.leftArms.Count, arms.rightArms.Count);
                m_previewArm = WrapIndex(m_previewArm, m_previewArmCount);
                if (m_previewArm < arms.leftArms.Count) armRoot.AddChild(new MeshInstance3D { Mesh = LoadPreviewMesh(arms.leftArms[m_previewArm]), MaterialOverride = material });
                if (m_previewArm < arms.rightArms.Count) armRoot.AddChild(new MeshInstance3D { Mesh = LoadPreviewMesh(arms.rightArms[m_previewArm]), MaterialOverride = material });
            }

            HumanLegModelData legs = LookupData<HumanLegModelData>(typeof(HumanParameterData), nameof(HumanParameterData.humanLegModelData), model.legIndex);
            if (legs != null && legs.legs.Count > 0)
            {
                m_previewLegCount = legs.legs.Count;
                m_previewLeg = WrapIndex(m_previewLeg, m_previewLegCount);
                parent.AddChild(new MeshInstance3D
                {
                    Name = "Legs",
                    Mesh = LoadPreviewMesh(legs.legs[m_previewLeg]),
                    MaterialOverride = MaterialAt(model.legTextureIndex),
                    Position = new Vector3(0f, general.humanLegHeight, 0f),
                    Scale = Vector3.One * general.humanLegScale,
                });
            }
        }

        /// <summary>
        /// 메시 하나를 텍스처 없이 더한다.
        /// </summary>
        /// <param name="parent">더할 부모.</param>
        /// <param name="relativePath">메시 경로 (exe 폴더 기준). null 이면 아무것도 하지 않는다.</param>
        private static void AddPlainMesh(Node3D parent, string relativePath)
        {
            ArrayMesh mesh = LoadPreviewMesh(relativePath);
            if (mesh != null) parent.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = MapLoader.GetEntityMaterial(null) });
        }

        /// <summary>
        /// 메시 파일을 읽는다.
        /// </summary>
        /// <param name="relativePath">경로 (exe 폴더 기준).</param>
        /// <returns>메시. 없으면 null.</returns>
        private static ArrayMesh LoadPreviewMesh(string relativePath)
        {
            string fullPath = string.IsNullOrEmpty(relativePath) ? null : GamePath.Resolve(relativePath);
            return fullPath != null && File.Exists(fullPath) ? ModelLoader.LoadMesh(fullPath) : null;
        }

        /// <summary>
        /// 오브젝트의 충돌 형상들을 선으로 그린다. 판정과 같은 자리와 크기다 (SmallObject.Contains): 상자는 전체 크기, 구는 반지름, 캡슐은 반지름·높이·방향.
        /// </summary>
        /// <param name="collider">충돌 형상 데이터.</param>
        /// <returns>선으로 그린 노드.</returns>
        private static MeshInstance3D BuildColliderWire(ObjectColliderData collider)
        {
            var mesh = new ImmediateMesh();
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            };
            var lines = new List<Vector3>();
            foreach (ColliderShape shape in collider.shapes)
            {
                Vector3 center = Coord.FromUnity(shape.center);
                switch (shape.type)
                {
                    case ColliderShapeType.Box:
                        AddWireBox(lines, center, shape.size * 0.5f);
                        break;
                    case ColliderShapeType.Sphere:
                        for (int axis = 0; axis < 3; axis++) AddWireCircle(lines, center, shape.size.X, axis);
                        break;
                    case ColliderShapeType.Capsule:
                    {
                        float radius = shape.size.X;
                        int axis = Mathf.Clamp((int)shape.size.Z, 0, 2);
                        Vector3 along = Vector3.Zero;
                        along[axis] = Mathf.Max(0f, shape.size.Y * 0.5f - radius);
                        AddWireCircle(lines, center + along, radius, axis);
                        AddWireCircle(lines, center - along, radius, axis);
                        for (int side = 0; side < 3; side++)
                        {
                            if (side == axis) continue;
                            Vector3 offset = Vector3.Zero;
                            offset[side] = radius;
                            lines.Add(center + along + offset);
                            lines.Add(center - along + offset);
                            lines.Add(center + along - offset);
                            lines.Add(center - along - offset);
                            AddWireCircle(lines, center + along, radius, side);
                            AddWireCircle(lines, center - along, radius, side);
                        }
                        break;
                    }
                }
            }
            if (lines.Count > 0)
            {
                mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
                foreach (Vector3 point in lines)
                {
                    mesh.SurfaceSetColor(s_previewColliderColor);
                    mesh.SurfaceAddVertex(point);
                }
                mesh.SurfaceEnd();
            }
            return new MeshInstance3D { Name = "Collider", Mesh = mesh };
        }

        /// <summary>
        /// 상자의 모서리 12개를 선분 목록에 더한다.
        /// </summary>
        /// <param name="lines">선분의 양 끝점을 차례로 담는 목록.</param>
        /// <param name="center">가운데.</param>
        /// <param name="half">축마다의 절반 크기.</param>
        private static void AddWireBox(List<Vector3> lines, Vector3 center, Vector3 half)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = center + new Vector3((i & 1) != 0 ? half.X : -half.X, (i & 2) != 0 ? half.Y : -half.Y, (i & 4) != 0 ? half.Z : -half.Z);
            }
            for (int i = 0; i < 8; i++)
            {
                foreach (int bit in new[] { 1, 2, 4 })
                {
                    if ((i & bit) != 0) continue;
                    lines.Add(corners[i]);
                    lines.Add(corners[i | bit]);
                }
            }
        }

        /// <summary>
        /// 원 하나를 선분 목록에 더한다.
        /// </summary>
        /// <param name="lines">선분의 양 끝점을 차례로 담는 목록.</param>
        /// <param name="center">가운데.</param>
        /// <param name="radius">반지름.</param>
        /// <param name="normalAxis">원이 놓인 면에 수직인 축 (0 X, 1 Y, 2 Z).</param>
        private static void AddWireCircle(List<Vector3> lines, Vector3 center, float radius, int normalAxis)
        {
            int first = (normalAxis + 1) % 3;
            int second = (normalAxis + 2) % 3;
            for (int i = 0; i < k_circleSegments; i++)
            {
                for (int end = 0; end < 2; end++)
                {
                    float angle = Mathf.Tau * (i + end) / k_circleSegments;
                    Vector3 point = center;
                    point[first] += Mathf.Cos(angle) * radius;
                    point[second] += Mathf.Sin(angle) * radius;
                    lines.Add(point);
                }
            }
        }

        /// <summary>
        /// 미리 보는 것 전체가 화면에 들어오게 시점을 맞춘다.
        /// </summary>
        /// <param name="subject">미리 보는 모델의 루트.</param>
        private void FocusPreview(Node3D subject)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var pending = new Stack<Node>();
            pending.Push(subject);
            while (pending.Count > 0)
            {
                Node node = pending.Pop();
                foreach (Node child in node.GetChildren()) pending.Push(child);
                if (node is not MeshInstance3D { Mesh: not null } instance) continue;

                Aabb bounds = instance.GlobalTransform * instance.Mesh.GetAabb();
                if (bounds.Size == Vector3.Zero) continue;
                min = min.Min(bounds.Position);
                max = max.Max(bounds.End);
            }
            if (min.X > max.X)
            {
                m_previewView.Focus(Vector3.Zero, 1f);
                return;
            }
            m_previewView.Focus((min + max) * 0.5f, Mathf.Max(k_previewMinRadius, (max - min).Length() * 0.5f));
        }

        /// <summary>
        /// 번호로 가리키는 데이터 항목을 찾는다. 에디터에 열려 있는 파일의 내용(저장하지 않은 편집 포함)을 먼저 보고, 없으면 게임이 읽어 둔 데이터를 본다.
        /// 10000 이상의 번호는 지금 미션의 에드온 데이터에서 찾는다.
        /// </summary>
        /// <typeparam name="T">항목의 형식.</typeparam>
        /// <param name="containerType">목록이 든 데이터 클래스.</param>
        /// <param name="listName">목록 필드의 이름.</param>
        /// <param name="index">번호.</param>
        /// <returns>항목. 없으면 null.</returns>
        private T LookupData<T>(Type containerType, string listName, int index) where T : class
        {
            FieldInfo field = containerType.GetField(listName);
            if (field == null || index < 0) return null;

            if (index >= DataList<T>.AddonBase)
            {
                string addonPath = MissionPathField(containerType)?.GetValue(m_document.Mission) as string;
                if (string.IsNullOrEmpty(addonPath)) return null;
                if (!m_assetFiles.TryGetValue(addonPath, out AssetFile addon))
                {
                    if (!AssetFile.Load(addonPath, out addon, out _)) return null;
                    m_assetFiles[addonPath] = addon;
                }
                return ItemAt<T>(addon.FileKind.Type == containerType ? field.GetValue(addon.Container) : null, index - DataList<T>.AddonBase);
            }

            // 보고 있는 파일, 그다음 열려 있는 다른 파일, 그다음 게임의 데이터.
            if (m_asset != null && m_asset.FileKind.Type == containerType && m_asset.Fields.Contains(field)) return ItemAt<T>(field.GetValue(m_asset.Container), index);
            foreach (AssetFile file in m_assetFiles.Values)
            {
                if (file.FileKind.Type == containerType && file.Fields.Contains(field) && file.Path.StartsWith(k_dataFolder + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return ItemAt<T>(field.GetValue(file.Container), index);
                }
            }

            DataManager data = DataManager.Instance;
            object container = containerType == typeof(HumanParameterData) ? data.HumanParameterData
                : containerType == typeof(WeaponParameterData) ? data.WeaponParameterData
                : containerType == typeof(ObjectParameterData) ? data.ObjectParameterData
                : containerType == typeof(EffectParameterData) ? data.EffectParameterData
                : null;
            return container == null ? null : ItemAt<T>(field.GetValue(container), index);
        }

        /// <summary>
        /// 목록의 한 항목을 꺼낸다.
        /// </summary>
        /// <typeparam name="T">항목의 형식.</typeparam>
        /// <param name="list">목록. null 이어도 된다.</param>
        /// <param name="index">목록 안에서의 번호.</param>
        /// <returns>항목. 범위 밖이면 null.</returns>
        private static T ItemAt<T>(object list, int index) where T : class
        {
            return list is IList items && index >= 0 && index < items.Count ? items[index] as T : null;
        }
    }
}
