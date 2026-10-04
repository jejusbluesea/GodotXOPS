using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 에셋 뷰어. data/ 와 addon/ 의 이미지·사운드·모델을 목록에서 골라 로더가 읽은 결과를 눈으로 확인한다.
    /// 이미지는 그대로 표시하고, 사운드는 재생하고, 모델은 마지막으로 본 이미지를 텍스처로 입혀 3D로 돌려 본다.
    /// </summary>
    public partial class AssetViewer : Control
    {
        private static readonly HashSet<string> s_imageExtensions = new HashSet<string> { ".bmp", ".tga", ".dds", ".png", ".jpg", ".jpeg" };

        private readonly List<string> m_allFiles = new List<string>();
        private readonly List<string> m_shownFiles = new List<string>();

        private LineEdit m_filter;
        private ItemList m_list;
        private Label m_info;
        private CheckBox m_doubleSided;
        private CheckBox m_autoRotate;
        private TextureRect m_imageView;
        private SubViewportContainer m_modelView;
        private Node3D m_pivot;
        private MeshInstance3D m_meshInstance;
        private Camera3D m_camera;
        private AudioStreamPlayer m_player;

        private StandardMaterial3D m_material;
        private ImageTexture m_lastTexture;
        private string m_lastTextureName = "(없음)";
        private float m_modelRadius = 1f;
        private float m_zoom = 1f;

        public override void _Ready()
        {
            BuildInterface();
            ScanFiles();
            ApplyFilter(string.Empty);

            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0)
            {
                RunSelfTest();
            }
        }

        public override void _Process(double delta)
        {
            if (m_modelView.Visible && m_autoRotate.ButtonPressed)
            {
                m_pivot.RotateY((float)delta * 0.8f);
            }
        }

        /// <summary>
        /// 화면 구성(파일 목록, 정보 줄, 이미지 뷰, 3D 뷰, 오디오 플레이어)을 코드로 만든다.
        /// </summary>
        private void BuildInterface()
        {
            var split = new HSplitContainer();
            split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            split.SplitOffsets = new[] { 360 };
            AddChild(split);

            var left = new VBoxContainer();
            left.CustomMinimumSize = new Vector2(240, 0);
            split.AddChild(left);

            m_filter = new LineEdit();
            m_filter.PlaceholderText = "필터 (예: .x, map1, weapon)";
            m_filter.TextChanged += ApplyFilter;
            left.AddChild(m_filter);

            m_list = new ItemList();
            m_list.SizeFlagsVertical = SizeFlags.ExpandFill;
            m_list.ItemSelected += OnItemSelected;
            left.AddChild(m_list);

            var right = new VBoxContainer();
            split.AddChild(right);

            m_info = new Label();
            m_info.Text = "왼쪽 목록에서 파일을 고르세요. 모델에는 마지막으로 본 이미지가 텍스처로 입혀집니다.";
            m_info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            right.AddChild(m_info);

            var options = new HBoxContainer();
            right.AddChild(options);

            m_doubleSided = new CheckBox();
            m_doubleSided.Text = "양면 렌더링 (끄면 뒷면이 안 보임 — 와인딩 확인용)";
            m_doubleSided.Toggled += OnDoubleSidedToggled;
            options.AddChild(m_doubleSided);

            m_autoRotate = new CheckBox();
            m_autoRotate.Text = "자동 회전";
            m_autoRotate.ButtonPressed = true;
            options.AddChild(m_autoRotate);

            var stack = new PanelContainer();
            stack.SizeFlagsVertical = SizeFlags.ExpandFill;
            right.AddChild(stack);

            m_imageView = new TextureRect();
            m_imageView.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            m_imageView.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            m_imageView.TextureFilter = TextureFilterEnum.Nearest;
            m_imageView.Visible = false;
            stack.AddChild(m_imageView);

            m_modelView = new SubViewportContainer();
            m_modelView.Stretch = true;
            m_modelView.Visible = false;
            m_modelView.GuiInput += OnModelViewInput;
            stack.AddChild(m_modelView);

            var viewport = new SubViewport();
            viewport.OwnWorld3D = true;
            m_modelView.AddChild(viewport);

            m_pivot = new Node3D();
            viewport.AddChild(m_pivot);

            m_material = new StandardMaterial3D();
            m_material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            m_material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            m_material.AlphaScissorThreshold = 0.5f;

            m_meshInstance = new MeshInstance3D();
            m_meshInstance.MaterialOverride = m_material;
            m_pivot.AddChild(m_meshInstance);

            m_camera = new Camera3D();
            m_camera.Fov = 50f;
            viewport.AddChild(m_camera);

            m_player = new AudioStreamPlayer();
            AddChild(m_player);
        }

        /// <summary>
        /// data/ 와 addon/ 에서 뷰어가 다룰 수 있는 파일을 모은다.
        /// </summary>
        private void ScanFiles()
        {
            foreach (string folder in new[] { GamePath.DataFolder, GamePath.AddonFolder })
            {
                string root = GamePath.Resolve(folder);
                if (root == null || !Directory.Exists(root))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (s_imageExtensions.Contains(extension) || extension == ".wav" || extension == ".x")
                    {
                        m_allFiles.Add(path);
                    }
                }
            }

            m_allFiles.Sort(System.StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 필터 문자열이 경로에 포함된 파일만 목록에 표시한다.
        /// </summary>
        /// <param name="filter">대소문자를 무시하고 찾을 부분 문자열. 비어 있으면 전부 표시.</param>
        private void ApplyFilter(string filter)
        {
            m_list.Clear();
            m_shownFiles.Clear();

            foreach (string path in m_allFiles)
            {
                string relative = Path.GetRelativePath(GamePath.Root, path).Replace('\\', '/');
                if (filter.Length > 0 && !relative.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                m_shownFiles.Add(path);
                m_list.AddItem(relative);
            }
        }

        private void OnItemSelected(long index)
        {
            Show(m_shownFiles[(int)index]);
        }

        /// <summary>
        /// 파일 하나를 확장자에 맞는 로더로 읽어 화면에 보여 주거나 재생한다.
        /// </summary>
        /// <param name="path">파일 전체 경로.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        private bool Show(string path)
        {
            string name = Path.GetRelativePath(GamePath.Root, path).Replace('\\', '/');
            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (s_imageExtensions.Contains(extension))
            {
                ImageTexture texture = ImageLoader.LoadTexture(path);
                if (texture == null)
                {
                    m_info.Text = $"{name}\n이미지 로드 실패";
                    return false;
                }

                m_lastTexture = texture;
                m_lastTextureName = name;
                m_imageView.Texture = texture;
                m_imageView.Visible = true;
                m_modelView.Visible = false;

                Image image = texture.GetImage();
                bool hasAlpha = image.DetectAlpha() != Image.AlphaMode.None;
                m_info.Text = $"{name}\n이미지 {texture.GetWidth()} × {texture.GetHeight()}, 투명 픽셀 {(hasAlpha ? "있음" : "없음")}";
                return true;
            }

            if (extension == ".wav")
            {
                AudioStreamWav stream = SoundLoader.LoadAudio(path);
                if (stream == null)
                {
                    m_info.Text = $"{name}\n사운드 로드 실패";
                    return false;
                }

                m_player.Stream = stream;
                m_player.Play();
                m_info.Text = $"{name}\n사운드 {stream.MixRate} Hz, {(stream.Stereo ? "스테레오" : "모노")}, {stream.Format}, {stream.GetLength():0.00}초 — 재생 중";
                return true;
            }

            if (extension == ".x")
            {
                ArrayMesh mesh = ModelLoader.LoadMesh(path);
                if (mesh == null)
                {
                    m_info.Text = $"{name}\n모델 로드 실패";
                    return false;
                }

                if (m_lastTexture == null)
                {
                    PickDefaultTexture(path);
                }

                Aabb bounds = mesh.GetAabb();
                m_meshInstance.Mesh = mesh;
                m_meshInstance.Position = -bounds.GetCenter();
                m_material.AlbedoTexture = m_lastTexture;
                m_modelRadius = Mathf.Max(0.01f, bounds.Size.Length() * 0.5f);
                m_zoom = 1f;
                m_pivot.Rotation = Vector3.Zero;
                UpdateCamera();

                m_imageView.Visible = false;
                m_modelView.Visible = true;

                Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
                int vertexCount = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
                int triangleCount = arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Length / 3;
                m_info.Text = $"{name}\n모델 정점 {vertexCount}, 삼각형 {triangleCount}, 크기 {bounds.Size.X:0.##} × {bounds.Size.Y:0.##} × {bounds.Size.Z:0.##}" +
                    $" — 텍스처: {m_lastTextureName}\n드래그: 회전 / 휠: 확대·축소";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 아직 본 이미지가 없을 때 모델에 입힐 텍스처를 고른다. 모델과 같은 폴더의 첫 이미지, 없으면 전체 목록의 첫 이미지.
        /// </summary>
        /// <param name="modelPath">모델 파일 전체 경로.</param>
        private void PickDefaultTexture(string modelPath)
        {
            string modelFolder = Path.GetDirectoryName(modelPath);
            string fallback = null;

            foreach (string path in m_allFiles)
            {
                if (!s_imageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                {
                    continue;
                }

                fallback ??= path;
                if (Path.GetDirectoryName(path) == modelFolder)
                {
                    fallback = path;
                    break;
                }
            }

            if (fallback == null)
            {
                return;
            }

            m_lastTexture = ImageLoader.LoadTexture(fallback);
            m_lastTextureName = Path.GetRelativePath(GamePath.Root, fallback).Replace('\\', '/');
        }

        /// <summary>
        /// 모델 크기와 확대 배율에 맞춰 카메라를 원점을 바라보는 위치에 둔다.
        /// </summary>
        private void UpdateCamera()
        {
            float distance = m_modelRadius * 2.4f * m_zoom;
            m_camera.Position = new Vector3(0f, m_modelRadius * 0.4f * m_zoom, distance);
            m_camera.LookAt(Vector3.Zero, Vector3.Up);
            m_camera.Near = Mathf.Max(0.001f, distance * 0.01f);
            m_camera.Far = distance * 20f;
        }

        private void OnModelViewInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
            {
                m_autoRotate.ButtonPressed = false;
                m_pivot.RotateY(motion.Relative.X * 0.01f);
                m_pivot.RotateObjectLocal(Vector3.Right, motion.Relative.Y * 0.01f);
            }
            else if (inputEvent is InputEventMouseButton button && button.Pressed)
            {
                if (button.ButtonIndex == MouseButton.WheelUp) m_zoom = Mathf.Max(0.2f, m_zoom * 0.9f);
                else if (button.ButtonIndex == MouseButton.WheelDown) m_zoom = Mathf.Min(5f, m_zoom * 1.1f);
                else return;
                UpdateCamera();
            }
        }

        private void OnDoubleSidedToggled(bool pressed)
        {
            m_material.CullMode = pressed ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back;
        }

        /// <summary>
        /// 종류별(이미지·사운드·모델) 첫 파일을 하나씩 열어 보고 결과를 출력한 뒤 종료한다. 명령행 "-- --selftest" 로 실행.
        /// </summary>
        private void RunSelfTest()
        {
            bool allOk = true;
            foreach (string wanted in new[] { ".bmp", ".dds", ".wav", ".x" })
            {
                string path = m_allFiles.Find(file => Path.GetExtension(file).ToLowerInvariant() == wanted);
                if (path == null)
                {
                    continue;
                }

                bool ok = Show(path);
                allOk &= ok;
                GD.Print($"{(ok ? "OK  " : "FAIL")} {m_info.Text.Replace("\n", " | ")}");
            }

            GetTree().Quit(allOk ? 0 : 1);
        }
    }
}
