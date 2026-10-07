using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 이펙트 뷰어 겸 점검 씬. 이펙트 프리셋을 골라 눈으로 보고, 가산 블렌딩으로 미리 볼 수 있다.
    /// 배경이 회색이라 알파로 덮는 것과 가산으로 더하는 것이 구분된다.
    /// 창에서: ← → 프리셋 바꾸기, Space 다시 재생, B 가산 미리보기 켜기/끄기, ↑ ↓ 카메라 거리.
    /// 명령행 인자("--" 뒤): --selftest 는 수치 점검만 하고 종료(헤드리스 가능, 문제가 있으면 종료 코드 1),
    /// --effect 번호 는 그 프리셋으로 시작, --additive 는 가산 미리보기로 시작,
    /// --screenshot 경로 는 화면을 PNG 로 저장하고 종료한다.
    /// 실행: Godot 콘솔 실행 파일로 --path . res://scenes/dev/effect_viewer.tscn
    /// </summary>
    public partial class EffectViewer : Node3D
    {
        // 화면을 저장하기 전에 기다리는 프레임 수. 이펙트가 한창일 때를 찍는다.
        private const int k_screenshotWaitFrames = 8;
        // 자동으로 다시 재생하는 간격 (초).
        private const float k_replayInterval = 1.2f;
        // 이펙트를 내는 자리와 카메라가 보는 곳.
        private static readonly Vector3 s_origin = new Vector3(0f, 1f, 0f);
        // 가산 미리보기에서 쓰는 발광 세기. 데이터의 brightness 는 쓰이지 않던 값이라 거의 0 이어서 그대로 두면 보이지 않는다.
        private const float k_previewBrightness = 1f;

        private Camera3D m_camera;
        private Label m_info;
        private int m_index = 1;
        private float m_distance = 6f;
        // 사용자가 거리를 직접 바꾸면 프리셋을 넘겨도 그 거리를 유지한다.
        private bool m_manualDistance;
        private float m_replayTimer;
        private bool m_additivePreview;
        private string m_screenshotPath;
        private int m_screenshotCountdown = -1;

        // 가산 미리보기를 켜기 전의 발광 세기. 끌 때 되돌린다.
        private float[] m_savedBrightness;

        private readonly List<string> m_problems = new List<string>();
        private int m_checks;

        public override void _Ready()
        {
            string[] args = OS.GetCmdlineUserArgs();

            if (Array.IndexOf(args, "--selftest") >= 0)
            {
                RunSelfTest();
                return;
            }

            // 게임 설정(ConfigManager)이 적용한 전체화면·저해상도 렌더를 도구용 창 설정으로 되돌린다.
            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            root.Mode = Window.ModeEnum.Windowed;
            root.Size = new Vector2I(960, 640);
            root.MoveToCenter();

            BuildScene();

            int effectArg = Array.IndexOf(args, "--effect");
            if (effectArg >= 0 && effectArg + 1 < args.Length && int.TryParse(args[effectArg + 1], out int index))
            {
                m_index = index;
            }
            if (Array.IndexOf(args, "--additive") >= 0)
            {
                m_additivePreview = true;
                ApplyAdditivePreview(true);
            }

            int screenshotArg = Array.IndexOf(args, "--screenshot");
            if (screenshotArg >= 0 && screenshotArg + 1 < args.Length)
            {
                m_screenshotPath = args[screenshotArg + 1];
                m_screenshotCountdown = k_screenshotWaitFrames;
            }

            Replay();
        }

        public override void _Process(double delta)
        {
            if (m_camera == null) return;

            m_replayTimer -= (float)delta;
            if (m_replayTimer <= 0f) Replay();

            m_camera.Position = s_origin + new Vector3(0f, 0.5f, m_distance);
            m_camera.LookAt(s_origin);

            if (m_screenshotCountdown >= 0)
            {
                // 이펙트 매니저의 _Process 가 먼저 돌아 입자를 늙힌 뒤이므로, 여기서 다시 내면 갓 나온 모습이 찍힌다.
                if (m_screenshotCountdown > 0) Replay();
                if (m_screenshotCountdown-- == 0)
                {
                    Error error = GetViewport().GetTexture().GetImage().SavePng(m_screenshotPath);
                    GD.Print($"스크린샷 {(error == Error.Ok ? "저장" : "실패")}: {m_screenshotPath}");
                    GetTree().Quit(error == Error.Ok ? 0 : 1);
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

            List<EffectData> all = DataManager.Instance.EffectParameterData.effectData;
            switch (key.Keycode)
            {
                case Key.Right:
                    SelectEffect(m_index + 1, all.Count);
                    break;
                case Key.Left:
                    SelectEffect(m_index - 1, all.Count);
                    break;
                case Key.Space:
                    Replay();
                    break;
                case Key.B:
                    ApplyAdditivePreview(false);
                    m_additivePreview = !m_additivePreview;
                    ApplyAdditivePreview(true);
                    Replay();
                    break;
                case Key.Up:
                    m_distance = Mathf.Max(1f, m_distance * 0.8f);
                    m_manualDistance = true;
                    break;
                case Key.Down:
                    m_distance *= 1.25f;
                    m_manualDistance = true;
                    break;
            }
        }

        /// <summary>
        /// 보여 줄 프리셋을 바꾼다. 가산 미리보기는 바꾸기 전 프리셋에서 걷어내고 새 프리셋에 입힌다.
        /// </summary>
        /// <param name="index">프리셋 번호.</param>
        /// <param name="count">프리셋 수.</param>
        private void SelectEffect(int index, int count)
        {
            ApplyAdditivePreview(false);
            m_index = Mathf.PosMod(index, count);
            ApplyAdditivePreview(true);
            Replay();
        }

        /// <summary>
        /// 카메라, 배경, 안내 문구를 만든다.
        /// </summary>
        private void BuildScene()
        {
            // 알파로 덮는 것과 가산으로 더하는 것을 구분해 보려면 배경이 검정이 아니어야 한다.
            var environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.35f, 0.36f, 0.40f),
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
            };
            m_camera = new Camera3D
            {
                Position = s_origin + new Vector3(0f, 0.5f, m_distance),
                Environment = environment,
            };
            AddChild(m_camera);
            // GameBridge 가 만든 장면 카메라가 현재 카메라이므로 이쪽으로 바꾼다.
            m_camera.MakeCurrent();
            m_camera.LookAt(s_origin);

            var layer = new CanvasLayer();
            AddChild(layer);
            m_info = new Label { Position = new Vector2(12f, 10f) };
            layer.AddChild(m_info);
        }

        /// <summary>
        /// 지금 고른 프리셋을 다시 낸다. 개수가 트리거값에 비례하는 emitter(혈흔)도 보이도록 값을 넣는다.
        /// </summary>
        private void Replay()
        {
            m_replayTimer = k_replayInterval;

            List<EffectData> all = DataManager.Instance.EffectParameterData.effectData;
            if (all.Count == 0) return;
            m_index = Mathf.PosMod(m_index, all.Count);

            if (!m_manualDistance) m_distance = FitDistance(all[m_index]);

            EffectManager.Instance.Clear();
            EffectManager.Instance.Play(m_index, s_origin, 100f);

            if (m_info != null)
            {
                EffectData data = all[m_index];
                m_info.Text = $"[{m_index}/{all.Count - 1}] {data.name}  emitter {data.emitters.Count}  " +
                    $"풀 {EffectManager.Instance.CountActive()}/{EffectManager.Instance.PoolCapacity}\n" +
                    $"가산 미리보기 {(m_additivePreview ? $"켜짐 (세기 {k_previewBrightness})" : "꺼짐")} (B)   ← → 프리셋   Space 다시   ↑ ↓ 거리";
            }
        }

        /// <summary>
        /// 프리셋이 화면에 들어오는 카메라 거리를 구한다. emitter 가 수명 동안 커지는 것까지 본다.
        /// </summary>
        /// <param name="data">프리셋.</param>
        /// <returns>카메라 거리 (m).</returns>
        private static float FitDistance(EffectData data)
        {
            float largest = 0f;
            foreach (EffectEmitter emitter in data.emitters)
            {
                float size = emitter.size + emitter.sizeRandomRange + Mathf.Max(0f, emitter.sizeRate) * emitter.lifetime;
                float reach = Mathf.Abs(emitter.positionOffset.Length()) + emitter.positionRandomRange.Length();
                largest = Mathf.Max(largest, size + reach);
            }
            return Mathf.Max(3f, largest * 1.8f);
        }

        /// <summary>
        /// 지금 고른 프리셋의 emitter 를 가산 블렌딩으로 바꾸거나 되돌린다. 메모리에 올린 데이터만 바꾸고 파일은 건드리지 않는다.
        /// </summary>
        /// <param name="on">true 면 가산으로, false 면 알파로 돌린다.</param>
        private void ApplyAdditivePreview(bool on)
        {
            if (!m_additivePreview) return;

            List<EffectData> all = DataManager.Instance.EffectParameterData.effectData;
            if (m_index < 0 || m_index >= all.Count) return;

            List<EffectEmitter> emitters = all[m_index].emitters;
            if (on)
            {
                m_savedBrightness = new float[emitters.Count];
                for (int i = 0; i < emitters.Count; i++)
                {
                    m_savedBrightness[i] = emitters[i].brightness;
                    emitters[i].blendMode = EffectBlendMode.Additive;
                    emitters[i].brightness = k_previewBrightness;
                }
                return;
            }

            for (int i = 0; i < emitters.Count; i++)
            {
                emitters[i].blendMode = EffectBlendMode.Alpha;
                if (m_savedBrightness != null && i < m_savedBrightness.Length) emitters[i].brightness = m_savedBrightness[i];
            }
            m_savedBrightness = null;
        }

        /// <summary>
        /// 이펙트 재생·풀 증가·블렌드 모드·발광 감쇠를 수치로 확인하고 종료한다. 문제가 있으면 종료 코드 1.
        /// </summary>
        private void RunSelfTest()
        {
            EffectParameterData data = DataManager.Instance.EffectParameterData;
            EffectManager manager = EffectManager.Instance;
            EffectGeneralData general = data.effectGeneralData;

            Expect(data.effectData.Count > 0, "이펙트 프리셋이 없음");
            Expect(manager.PoolCapacity == general.poolInitialSize,
                $"풀 초기 크기가 데이터와 다름 (확보 {manager.PoolCapacity}, 데이터 {general.poolInitialSize})");

            CheckPresets(data, manager);
            CheckPoolGrowth(manager, general);
            CheckBlendMaterials(data, manager);
            CheckBrightnessDecay(data, manager);
            CheckSurface(data, manager);

            manager.Clear();
            GD.Print($"이펙트 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 프리셋마다 emitter 가 내놓는 입자 수가 데이터와 맞는지 확인한다.
        /// </summary>
        /// <param name="data">이펙트 데이터.</param>
        /// <param name="manager">이펙트 매니저.</param>
        private void CheckPresets(EffectParameterData data, EffectManager manager)
        {
            const float trigger = 100f;
            for (int i = 0; i < data.effectData.Count; i++)
            {
                manager.Clear();

                int expected = 0;
                foreach (EffectEmitter emitter in data.effectData[i].emitters)
                {
                    // 텍스처를 읽지 못하는 emitter 는 아무것도 내지 않는다 (헤드리스에서도 이미지 로더는 동작한다).
                    if (!data.effectTextureData.Has(emitter.textureIndex)) continue;
                    expected += emitter.countPerTrigger > 0f
                        ? Mathf.FloorToInt(trigger * emitter.countPerTrigger)
                        : emitter.spawnCount;
                }

                manager.Play(i, s_origin, trigger);
                Expect(manager.CountActive() == expected,
                    $"프리셋 {i}({data.effectData[i].name}) 의 입자 수가 다름 (낸 것 {manager.CountActive()}, 데이터 {expected})");
            }
        }

        /// <summary>
        /// 풀이 가득 찼을 때 묶음 단위로 늘어나는지, 한계를 두면 거기서 멈추고 버리는지 확인한다.
        /// </summary>
        /// <param name="manager">이펙트 매니저.</param>
        /// <param name="general">이펙트 전역 데이터.</param>
        private void CheckPoolGrowth(EffectManager manager, EffectGeneralData general)
        {
            manager.Clear();
            int before = manager.PoolCapacity;

            // 한 번에 하나를 내는 프리셋을 골라 풀을 넘겨 본다.
            int single = FindSingleEmitterEffect();
            if (single < 0)
            {
                Expect(false, "입자 하나를 내는 프리셋을 찾지 못해 풀 증가를 확인할 수 없음");
                return;
            }

            for (int i = 0; i < before + general.poolGrowStep; i++) manager.Play(single, s_origin);

            Expect(manager.PoolCapacity > before,
                $"풀이 가득 찼는데 늘지 않음 (확보 {manager.PoolCapacity}, 처음 {before})");
            Expect(manager.PoolCapacity == before + general.poolGrowStep,
                $"풀이 묶음 단위로 늘지 않음 (확보 {manager.PoolCapacity}, 기대 {before + general.poolGrowStep})");
            Expect(manager.CountActive() == before + general.poolGrowStep,
                $"늘어난 자리를 쓰지 못함 (활성 {manager.CountActive()}, 기대 {before + general.poolGrowStep})");

            // 한계에 닿으면 더 늘리지 않고 버린다 (원본 동작).
            int limited = manager.PoolCapacity;
            manager.Play(single, s_origin);
            bool kept = manager.PoolCapacity > limited;
            Expect(kept || manager.CountActive() == limited,
                "풀 한계에서 버리지도 늘리지도 않음");

            manager.Clear();
        }

        /// <summary>
        /// 같은 텍스처라도 블렌드 모드가 다르면 다른 머티리얼(다른 셰이더)이 되는지 확인한다.
        /// </summary>
        /// <param name="data">이펙트 데이터.</param>
        /// <param name="manager">이펙트 매니저.</param>
        private void CheckBlendMaterials(EffectParameterData data, EffectManager manager)
        {
            int single = FindSingleEmitterEffect();
            if (single < 0) return;

            EffectEmitter emitter = data.effectData[single].emitters[0];
            EffectBlendMode saved = emitter.blendMode;
            float savedBrightness = emitter.brightness;

            manager.Clear();
            emitter.blendMode = EffectBlendMode.Alpha;
            manager.Play(single, s_origin);
            Material alphaMaterial = FindActiveMaterial();

            manager.Clear();
            emitter.blendMode = EffectBlendMode.Additive;
            emitter.brightness = 1f;
            manager.Play(single, s_origin);
            Material additiveMaterial = FindActiveMaterial();

            Expect(alphaMaterial != null && additiveMaterial != null, "블렌드 모드별 머티리얼을 만들지 못함");
            Expect(alphaMaterial != additiveMaterial, "알파와 가산이 같은 머티리얼을 쓴다");
            if (alphaMaterial is ShaderMaterial alphaShader && additiveMaterial is ShaderMaterial additiveShader)
            {
                Expect(alphaShader.Shader != additiveShader.Shader, "알파와 가산이 같은 셰이더를 쓴다");
            }

            emitter.blendMode = saved;
            emitter.brightness = savedBrightness;
            manager.Clear();
        }

        /// <summary>
        /// 가산 이펙트는 발광 세기가 0 이 되면 회수되고, 알파 이펙트는 세기와 무관하게 남는지 확인한다.
        /// </summary>
        /// <param name="data">이펙트 데이터.</param>
        /// <param name="manager">이펙트 매니저.</param>
        private void CheckBrightnessDecay(EffectParameterData data, EffectManager manager)
        {
            int single = FindSingleEmitterEffect();
            if (single < 0) return;

            EffectEmitter emitter = data.effectData[single].emitters[0];
            EffectBlendMode savedBlend = emitter.blendMode;
            float savedBrightness = emitter.brightness;
            float savedRate = emitter.brightnessRate;
            float savedAlphaRate = emitter.alphaRate;
            float savedLifetime = emitter.lifetime;

            emitter.alphaRate = 0f;
            emitter.lifetime = 10f;
            emitter.brightness = 0.5f;
            emitter.brightnessRate = -1f;

            // 가산: 0.5 세기가 초당 1 씩 줄면 0.5 초 뒤에 사라진다.
            emitter.blendMode = EffectBlendMode.Additive;
            manager.Clear();
            manager.Play(single, s_origin);
            Expect(manager.CountActive() == 1, "가산 이펙트가 나오지 않음");
            manager._Process(0.3);
            Expect(manager.CountActive() == 1, "가산 이펙트가 세기가 남았는데 사라짐");
            manager._Process(0.3);
            Expect(manager.CountActive() == 0, "가산 이펙트가 세기가 0 이 되어도 남아 있음");

            // 알파: 같은 값이어도 세기는 보지 않는다.
            emitter.blendMode = EffectBlendMode.Alpha;
            manager.Clear();
            manager.Play(single, s_origin);
            manager._Process(0.6);
            Expect(manager.CountActive() == 1, "알파 이펙트가 발광 세기 때문에 사라짐");

            emitter.blendMode = savedBlend;
            emitter.brightness = savedBrightness;
            emitter.brightnessRate = savedRate;
            emitter.alphaRate = savedAlphaRate;
            emitter.lifetime = savedLifetime;
            manager.Clear();
        }

        /// <summary>
        /// 면 위에 재생할 때 데칼(빌보드가 아닌 emitter)만 면에 눕고 면에서 떠 있는지, 빌보드 emitter 는 그대로인지 확인한다.
        /// </summary>
        /// <param name="data">이펙트 데이터.</param>
        /// <param name="manager">이펙트 매니저.</param>
        private void CheckSurface(EffectParameterData data, EffectManager manager)
        {
            const float tolerance = 1e-4f;
            EffectGeneralData general = data.effectGeneralData;

            int index = general.wallBloodEffectIndex;
            bool usable = data.effectData.Has(index) && data.effectData[index].emitters.Count == 1;
            Expect(usable, "벽 혈흔 프리셋이 emitter 하나가 아니어서 면 재생을 확인할 수 없음");
            if (!usable) return;

            EffectEmitter emitter = data.effectData[index].emitters[0];
            EffectFlags savedFlags = emitter.flags;
            Vector3 billboardPoint = new Vector3(3f, 2f, 1f);
            Vector3 surfacePoint = new Vector3(3.1f, 2f, 1f);

            // 데칼: 벽(옆을 보는 면)과 바닥(위를 보는 면).
            emitter.flags = EffectFlags.NoBillboard;
            foreach (Vector3 normal in new[] { Vector3.Left, Vector3.Up })
            {
                manager.Clear();
                manager.PlayOnSurface(index, billboardPoint, surfacePoint, normal);
                MeshInstance3D decal = FindActiveNode();
                Expect(decal != null, $"면 위에 데칼이 나오지 않음 (법선 {normal})");
                if (decal == null) continue;

                Vector3 expected = surfacePoint + normal * general.decalSurfaceOffset;
                Expect(decal.Position.DistanceTo(expected) < tolerance,
                    $"데칼이 면에서 decalSurfaceOffset 만큼 떠 있지 않음 (자리 {decal.Position}, 기대 {expected})");
                Vector3 facing = decal.Basis.Z.Normalized();
                Expect(facing.DistanceTo(normal) < tolerance, $"데칼의 앞면이 법선을 향하지 않음 (앞면 {facing}, 법선 {normal})");
            }

            // 빌보드: 면과 무관하게 착탄 지점에 그대로 나온다.
            emitter.flags = EffectFlags.None;
            manager.Clear();
            manager.PlayOnSurface(index, billboardPoint, surfacePoint, Vector3.Left);
            MeshInstance3D billboard = FindActiveNode();
            Expect(billboard != null && billboard.Position.DistanceTo(billboardPoint) < tolerance,
                "면 위에 재생한 빌보드가 착탄 지점에서 벗어남");

            // 법선이 없으면 방향 없이 재생한다.
            emitter.flags = EffectFlags.NoBillboard;
            manager.Clear();
            manager.PlayOnSurface(index, billboardPoint, surfacePoint, Vector3.Zero);
            Expect(manager.CountActive() == 1, "법선이 없을 때 이펙트가 나오지 않음");

            emitter.flags = savedFlags;
            manager.Clear();
        }

        /// <summary>
        /// 재생 중인 이펙트 노드를 하나 가져온다.
        /// </summary>
        /// <returns>보이는 노드. 없으면 null.</returns>
        private static MeshInstance3D FindActiveNode()
        {
            foreach (Node child in EffectManager.Instance.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.Visible) return mesh;
            }
            return null;
        }

        /// <summary>
        /// 입자 하나만 내는 프리셋을 찾는다. 풀 증가와 블렌드 모드를 한 개 단위로 확인하는 데 쓴다.
        /// </summary>
        /// <returns>프리셋 번호. 없으면 −1.</returns>
        private static int FindSingleEmitterEffect()
        {
            EffectParameterData data = DataManager.Instance.EffectParameterData;
            for (int i = 0; i < data.effectData.Count; i++)
            {
                List<EffectEmitter> emitters = data.effectData[i].emitters;
                if (emitters.Count != 1) continue;
                if (emitters[0].spawnCount != 1 || emitters[0].countPerTrigger > 0f) continue;
                if (emitters[0].textureIndex < 0 || emitters[0].textureIndex >= data.effectTextureData.Count) continue;
                return i;
            }
            return -1;
        }

        /// <summary>
        /// 재생 중인 이펙트 노드의 머티리얼을 하나 가져온다.
        /// </summary>
        /// <returns>머티리얼. 보이는 노드가 없으면 null.</returns>
        private static Material FindActiveMaterial()
        {
            foreach (Node child in EffectManager.Instance.GetChildren())
            {
                if (child is MeshInstance3D mesh && mesh.Visible) return mesh.MaterialOverride;
            }
            return null;
        }

        /// <summary>
        /// 조건을 확인하고, 거짓이면 문제 목록에 넣는다.
        /// </summary>
        /// <param name="ok">확인할 조건.</param>
        /// <param name="what">무엇을 확인했는지.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }
    }
}
