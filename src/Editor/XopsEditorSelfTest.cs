using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        private const string k_selfTestFolder = "build/editor_check";
        private const int k_selfTestMission = 0;

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();

        /// <summary>
        /// 점검 한 건을 센다.
        /// </summary>
        /// <param name="ok">통과했는지.</param>
        /// <param name="what">실패했을 때 남길 설명.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// 에디터를 수치로 점검하고 종료한다 (--selftest, 헤드리스 가능). 공식 미션 하나를 확장 형식으로 바꿔 열고,
        /// 표식의 수, 클릭 선택, Shift 선택, 사각형 선택, X-RAY, 전부 선택, 목록과의 동기, 시점 맞추기와 정해진 시점, 옮기기·돌리기·되돌리기, 격자와 면에 붙이기, 값 고치기·놓기·복제·지우기·저장, 블록 편집(모양, 면의 값, 텍스처 목록, 새 맵), 원본 형식 가져오기, 플레이 테스트용 파일, 이벤트 편집(이름 붙은 칸, 고르기, 다음 이벤트, 줄의 시작 번호, 이벤트 보기, 메시지), 미션 모드, 에셋 모드를 확인한다. 문제가 있으면 종료 코드 1.
        /// </summary>
        private void RunSelfTest()
        {
            string mission = null;
            if (MapLoader.LoadMissionData(k_selfTestMission, false, 0))
            {
                mission = MapLoader.ConvertMissionToExtended(k_selfTestFolder, "check", out _);
            }
            if (mission == null || !OpenMission(mission))
            {
                Expect(false, "점검용 미션을 확장 형식으로 바꿔 열지 못함");
                FinishSelfTest();
                return;
            }

            List<PD2Point> points = m_document.Points.points;
            Camera3D camera = m_view.Camera;
            Rect2 screen = GetViewport().GetVisibleRect();
            Expect(points.Count > 0 && MapLoader.Blocks.Count > 0 && m_markers.GetChildCount() == points.Count, "블록이나 포인트가 없거나 표식의 수가 포인트 수와 다름");
            Expect(m_pointList.ItemCount == points.Count, "목록의 줄 수가 포인트 수와 다름");
            Expect(MapLoader.HumanCount == 0, "에디터가 사람을 스폰함 (파일의 내용만 보여 줘야 한다)");

            // 전부 선택과 해제.
            SelectAll();
            Expect(m_selection.Count == points.Count && m_pointList.GetSelectedItems().Length == points.Count, "전부 선택이 모든 포인트와 목록의 모든 줄을 고르지 않음");
            SelectNone();
            Expect(m_selection.Count == 0 && m_pointList.GetSelectedItems().Length == 0, "선택 해제 뒤에 선택이 남음");

            // 위에서 직교로 내려다보면 모든 포인트가 화면 안에 있다. X-RAY 를 켠 사각형은 전부, 끈 사각형은 지붕에 가린 것을 뺀 나머지를 고른다.
            ViewTop(false);
            if (!m_view.Orthographic) m_view.ToggleOrthographic();
            FocusAll();
            SetXray(true);
            SelectBox(screen, false);
            int withXray = m_selection.Count;
            SetXray(false);
            SelectBox(screen, false);
            int withoutXray = m_selection.Count;
            Expect(withXray == points.Count, $"X-RAY 를 켠 사각형 선택이 화면 안의 포인트를 전부 고르지 않음 ({withXray} / {points.Count})");
            Expect(withoutXray <= withXray, "X-RAY 를 끈 사각형 선택이 켠 것보다 많이 고름");

            // 클릭: 포인트 하나가 찍히는 자리를 누르면, 그 자리에서 가장 가까이 보이는 포인트가 선택된다.
            SetXray(true);
            int target = points.Count / 2;
            Vector2 at = camera.UnprojectPosition(PointMarkers.Center(points[target]));
            SelectAt(at, false);
            int picked = m_selection.Count == 1 ? m_selection.Min : -1;
            Expect(picked >= 0 && camera.UnprojectPosition(PointMarkers.Center(points[picked])).DistanceTo(at) < 1f, "클릭한 자리의 포인트가 선택되지 않음");
            Expect(m_pointList.GetSelectedItems().Length == 1 && m_listToPoint[m_pointList.GetSelectedItems()[0]] == picked, "클릭 선택이 목록에 반영되지 않음");
            Expect(m_details.Text.StartsWith($"Point #{picked}", StringComparison.Ordinal), "선택한 포인트의 값이 오른쪽에 나오지 않음");

            // Shift + 클릭은 더하고, 같은 것을 다시 누르면 뺀다. 빈 곳을 그냥 누르면 선택이 풀린다.
            int other = FindSeparatePoint(camera, points, picked);
            if (other >= 0)
            {
                Vector2 otherAt = camera.UnprojectPosition(PointMarkers.Center(points[other]));
                SelectAt(otherAt, true);
                Expect(m_selection.Count == 2, "Shift + 클릭이 선택에 더하지 않음");
                Expect(m_details.Text.StartsWith("2 points selected", StringComparison.Ordinal), "여러 개를 선택했을 때의 표시가 다름");
                SelectAt(otherAt, true);
                Expect(m_selection.Count == 1 && m_selection.Contains(picked), "Shift + 클릭이 선택된 것을 빼지 않음");
            }
            SelectAt(new Vector2(-100f, -100f), false);
            Expect(m_selection.Count == 0, "빈 곳을 눌렀는데 선택이 남음");

            // 목록에서 고르기 (걸러 보는 중에는 보이는 줄만 바뀐다).
            SetSelection(new[] { 0 });
            m_filter.Select(1);
            RefreshPointList();
            int humans = m_pointList.ItemCount;
            Expect(humans > 0 && humans < points.Count, "걸러 보기가 목록을 줄이지 않음");
            m_pointList.Select(0, false);
            OnListSelectionChanged();
            bool zeroIsHuman = points[0].type == MapLoader.PointHuman || points[0].type == MapLoader.PointHuman2;
            Expect(m_selection.Contains(m_listToPoint[0]) && (zeroIsHuman || m_selection.Contains(0)), "목록에서 고른 것이 선택에 반영되지 않거나, 걸러져 안 보이는 선택이 지워짐");
            m_filter.Select(0);
            RefreshPointList();

            // 시점: 정해진 방향과 선택한 것으로 맞추기.
            ViewFront(false);
            Expect((-camera.GlobalBasis.Z).DistanceTo(Coord.YawForward(0f)) < 1e-3f, "정면 시점의 방향이 다름");
            ViewRight(false);
            Expect((-camera.GlobalBasis.Z).DistanceTo(Vector3.Left) < 1e-3f, "측면 시점이 +X 쪽에서 −X 를 보지 않음");
            ViewTop(false);
            Expect((-camera.GlobalBasis.Z).DistanceTo(Vector3.Down) < 1e-2f, "위 시점이 아래를 보지 않음");
            SetSelection(new[] { target });
            FocusSelection();
            Expect(m_view.Pivot.DistanceTo(PointMarkers.Center(points[target])) < 1e-3f, "선택한 것으로 시점을 맞췄는데 중심이 그 포인트가 아님");
            Vector2 centered = camera.UnprojectPosition(PointMarkers.Center(points[target]));
            Expect(centered.DistanceTo(screen.GetCenter()) < 2f, "시점을 맞춘 포인트가 화면 가운데에 오지 않음");

            CheckTransform(points);
            CheckPointEditing(points);
            CheckBlockEditing();
            CheckBlockValues();
            CheckPlayAndImport();
            CheckEvents();
            CheckMission();
            CheckAssets();

            // 열 수 없는 파일.
            Expect(!OpenBlock("data/map0/temp.bd1") && m_messageIsError, "원본 형식의 블록 파일을 열어 버림");
            List<PD2Point> current = m_document.Points.points;
            Expect(!OpenPoints($"{k_selfTestFolder}/no_such.pd2") && current == m_document.Points.points, "없는 포인트 파일을 열었거나 실패했는데 내용이 바뀜");

            FinishSelfTest();
        }

        /// <summary>
        /// 옮기기, 돌리기, 되돌리기를 확인한다: 축과 숫자 입력, 마우스로 옮긴 거리, 격자, 취소, 선택의 가운데 둘레로 돌리기, 되돌리기와 다시 하기.
        /// </summary>
        /// <param name="points">문서의 포인트들.</param>
        private void CheckTransform(List<PD2Point> points)
        {
            const float tolerance = 1e-3f;
            Camera3D camera = m_view.Camera;
            int a = 0;
            int b = points.Count - 1;
            Vector3 startA = points[a].position;
            float directionA = points[a].direction;
            // 면에 붙이기는 아래에서 따로 본다. 여기서는 화면과 나란한 면 위의 이동을 확인한다.
            m_surfaceSnap = false;

            // 숫자 입력: X 축으로 1.5 m.
            SetSelection(new[] { a });
            BeginTransform(TransformMode.Move);
            SetTransformAxis(TransformAxis.X);
            foreach (char typed in "1.5")
            {
                TypeNumeric(new InputEventKey { Unicode = typed, Pressed = true });
            }
            ConfirmTransform();
            Expect(points[a].position.DistanceTo(startA + new Vector3(1.5f, 0f, 0f)) < tolerance, "축과 숫자로 옮긴 거리가 다름");
            Expect(m_dirty && m_history.UndoCount == 1 && !Transforming, "옮기기를 확정했는데 기록이나 바뀜 표시가 없음");
            Undo();
            Expect(points[a].position.DistanceTo(startA) < tolerance && m_history.RedoCount == 1, "되돌리기가 옮기기 전으로 돌려놓지 않음");
            Redo();
            Expect(points[a].position.DistanceTo(startA + new Vector3(1.5f, 0f, 0f)) < tolerance, "다시 하기가 옮긴 자리로 돌려놓지 않음");
            Undo();

            // 마우스: 위에서 직교로 볼 때 포인트가 마우스와 같은 픽셀만큼 따라온다. 취소하면 제자리로 돌아간다.
            ViewTop(false);
            if (!m_view.Orthographic) m_view.ToggleOrthographic();
            FocusSelection();
            Vector2 screenBefore = camera.UnprojectPosition(points[a].position);
            int undoBefore = m_history.UndoCount;
            BeginTransform(TransformMode.Move);
            m_transformStartMouse = screenBefore;
            m_transformMouse = screenBefore + new Vector2(60f, 25f);
            UpdateTransform();
            Expect(camera.UnprojectPosition(points[a].position).DistanceTo(screenBefore + new Vector2(60f, 25f)) < 1f, "마우스로 옮길 때 포인트가 마우스를 따라오지 않음");
            Expect(Mathf.Abs(points[a].position.Y - startA.Y) < tolerance, "위에서 볼 때 옮겼는데 높이가 바뀜");
            CancelTransform();
            Expect(points[a].position.DistanceTo(startA) < tolerance && m_history.UndoCount == undoBefore, "취소했는데 포인트가 제자리로 돌아가지 않거나 기록이 남음");

            // 축을 묶으면 그 축으로만 움직인다. 격자를 켜면 격자 단위로 붙는다.
            BeginTransform(TransformMode.Move);
            m_transformStartMouse = screenBefore;
            m_transformMouse = screenBefore + new Vector2(60f, 25f);
            SetTransformAxis(TransformAxis.Z);
            Vector3 alongZ = points[a].position - startA;
            Expect(Mathf.Abs(alongZ.X) < tolerance && Mathf.Abs(alongZ.Y) < tolerance && Mathf.Abs(alongZ.Z) > tolerance, "Z 축에 묶었는데 다른 축으로 움직임");
            SetTransformAxis(TransformAxis.Z);
            m_gridLock = true;
            m_gridSize = 0.5f;
            UpdateTransform();
            Vector3 snapped = points[a].position / 0.5f;
            Expect(Mathf.Abs(snapped.X - Mathf.Round(snapped.X)) < tolerance && Mathf.Abs(snapped.Z - Mathf.Round(snapped.Z)) < tolerance,
                "격자를 켰는데 옮긴 자리가 격자점이 아님");
            Expect(Mathf.Abs(points[a].position.Y - startA.Y) < tolerance, "격자를 켰더니 움직이지 않은 축(높이)까지 격자로 튐");
            m_gridLock = false;
            CancelTransform();

            // 면에 붙이기: 마우스 아래의 블록 면 위에 기준 포인트가 놓인다. 격자와 함께면 가로는 격자점, 높이는 그 자리의 면이다.
            m_surfaceSnap = true;
            Vector2 over = camera.UnprojectPosition(points[b].position);
            bool surfaceThere = SurfaceUnder(over, out Vector3 surface);
            BeginTransform(TransformMode.Move);
            m_transformMouse = over;
            UpdateTransform();
            Expect(!surfaceThere || points[a].position.DistanceTo(surface) < tolerance, "면에 붙이기를 켰는데 포인트가 마우스 아래의 면에 놓이지 않음");
            m_gridLock = true;
            UpdateTransform();
            Vector3 onGrid = points[a].position / 0.5f;
            Expect(!surfaceThere || (Mathf.Abs(onGrid.X - Mathf.Round(onGrid.X)) < tolerance && Mathf.Abs(onGrid.Z - Mathf.Round(onGrid.Z)) < tolerance),
                "면에 붙인 채 격자를 켰는데 가로 자리가 격자점이 아님");
            SetTransformAxis(TransformAxis.Y);
            Vector3 lifted = points[a].position - startA;
            Expect(Mathf.Abs(lifted.X) < tolerance && Mathf.Abs(lifted.Z) < tolerance, "축을 묶었는데도 면에 붙이기가 들음");
            m_gridLock = false;
            m_gridSize = k_defaultGridSize;
            m_surfaceSnap = false;
            CancelTransform();

            // 돌리기: 두 포인트를 가운데 둘레로 90° 돌리면 서로의 거리는 그대로이고 방향이 90° 는다.
            Vector3 startB = points[b].position;
            float directionB = points[b].direction;
            float gap = startA.DistanceTo(startB);
            SetSelection(new[] { a, b });
            BeginTransform(TransformMode.Rotate);
            foreach (char typed in "90")
            {
                TypeNumeric(new InputEventKey { Unicode = typed, Pressed = true });
            }
            ConfirmTransform();
            Expect(Mathf.Abs(points[a].position.DistanceTo(points[b].position) - gap) < tolerance, "돌렸는데 두 포인트의 거리가 바뀜");
            Expect(Mathf.Abs(Coord.DeltaAngle(points[a].direction, directionA + 90f)) < tolerance && Mathf.Abs(Coord.DeltaAngle(points[b].direction, directionB + 90f)) < tolerance,
                "돌렸는데 포인트의 방향이 같은 만큼 돌지 않음");
            Vector3 center = (startA.Min(startB) + startA.Max(startB)) * 0.5f;
            Vector3 offset = startA - center;
            Vector3 expected = center + Coord.YawForward(90f) * (-offset.Z) + Coord.YawRight(90f) * offset.X + Vector3.Up * offset.Y;
            Expect(points[a].position.DistanceTo(expected) < tolerance, "돌린 뒤의 자리가 yaw 가 커지는 쪽으로 90° 가 아님");
            Undo();
            Expect(points[a].position.DistanceTo(startA) < tolerance && points[b].position.DistanceTo(startB) < tolerance
                && Mathf.Abs(Coord.DeltaAngle(points[a].direction, directionA)) < tolerance, "돌리기를 되돌렸는데 처음 상태가 아님");

            // 선택이 없으면 시작하지 않는다.
            SelectNone();
            BeginTransform(TransformMode.Move);
            Expect(!Transforming, "선택이 없는데 변형이 시작됨");

            // X-RAY 를 켜면 표식이 블록에 가려지지 않게 그려진다.
            SetXray(true);
            Expect(m_markers.GetChild(0).GetChild<MeshInstance3D>(0).MaterialOverride is StandardMaterial3D { NoDepthTest: true }, "X-RAY 를 켰는데 표식이 깊이 검사를 그대로 함");
            SetXray(false);
            Expect(m_markers.GetChild(0).GetChild<MeshInstance3D>(0).MaterialOverride is StandardMaterial3D { NoDepthTest: false }, "X-RAY 를 껐는데 표식이 블록을 뚫고 보임");
        }

        /// <summary>
        /// 포인트 편집을 확인한다: 키(물리 키 위치), 값 칸(하나와 여럿), 추가 파라미터, 놓기, 복제, 지우기, 되돌리기, 저장과 백업.
        /// </summary>
        /// <param name="points">문서의 포인트들.</param>
        private void CheckPointEditing(List<PD2Point> points)
        {
            const float tolerance = 1e-3f;
            int count = points.Count;
            int a = 0;
            int b = count - 1;

            // 키는 물리 키 위치로 읽는다. 글자 코드가 없는 이벤트(한글 입력 상태)도 듣는다.
            bool xrayBefore = m_xray;
            OnKey(new InputEventKey { PhysicalKeycode = Key.Z, AltPressed = true, Pressed = true });
            Expect(m_xray != xrayBefore, "Alt+Z (물리 키) 로 X-RAY 가 바뀌지 않음");
            OnKey(new InputEventKey { Keycode = Key.Z, AltPressed = true, Pressed = true });
            Expect(m_xray == xrayBefore, "Alt+Z (글자 코드만 있는 이벤트) 로 X-RAY 가 바뀌지 않음");
            OnKey(new InputEventKey { PhysicalKeycode = Key.Z, Pressed = true });
            Expect(m_xray == xrayBefore, "Alt 없이 Z 만 눌렀는데 X-RAY 가 바뀜");

            // 값 칸: 하나.
            int idBefore = points[a].id;
            int undoBefore = m_history.UndoCount;
            SetSelection(new[] { a });
            Expect(m_singleBox.Visible && !m_multiBox.Visible && Mathf.Abs((float)m_fieldBoxes[PointField.X].Value - points[a].position.X) < 0.01f
                && (int)m_fieldBoxes[PointField.Id].Value == idBefore, "하나를 선택했는데 값 칸에 그 포인트의 값이 없음");
            Expect(m_p2Label.Text == PointTypeInfo.Get(points[a].type).P2, "값 칸의 이름이 포인트 종류에 맞지 않음");
            ApplyField(PointField.Id, 777);
            Expect(points[a].id == 777 && m_history.UndoCount == undoBefore + 1 && m_pointList.GetItemText(0).Contains("id 777"), "식별번호를 고쳤는데 포인트나 목록이 바뀌지 않음");
            ApplyField(PointField.Id, 777);
            Expect(m_history.UndoCount == undoBefore + 1, "같은 값을 다시 넣었는데 되돌리기 기록이 늘어남");
            int typeBefore = points[a].type;
            ApplyField(PointField.Type, MapLoader.PointSmallObject);
            Expect(points[a].type == MapLoader.PointSmallObject && m_p2Label.Text == "Object data", "종류를 바꿨는데 칸의 이름이 따라 바뀌지 않음");
            Undo();
            Undo();
            Expect(points[a].id == idBefore && points[a].type == typeBefore, "값 고치기를 되돌렸는데 처음 값이 아님");

            // 추가 파라미터.
            ApplyExtra("1, 2,3");
            Expect(points[a].extra.Length == 3 && points[a].extra[2] == 3, "추가 파라미터를 쉼표로 나눠 넣지 못함");
            ApplyExtra("1, x");
            Expect(points[a].extra.Length == 3 && m_messageIsError, "틀린 추가 파라미터를 받아들임");
            Undo();
            Expect(points[a].extra.Length == 0, "추가 파라미터 고치기를 되돌리지 못함");

            // 값 칸: 여럿.
            int p3A = points[a].param2;
            int p3B = points[b].param2;
            SetSelection(new[] { a, b });
            Expect(!m_singleBox.Visible && m_multiBox.Visible, "여럿을 선택했는데 '무엇을 고칠지' 칸이 보이지 않음");
            ApplyField(PointField.P3, 4321);
            Expect(points[a].param2 == 4321 && points[b].param2 == 4321, "여럿의 값을 한 번에 바꾸지 못함");
            Undo();
            Expect(points[a].param2 == p3A && points[b].param2 == p3B, "여럿의 값 고치기를 되돌리지 못함");

            // 놓기: 화면 가운데 아래의 면에, 같은 종류의 가장 큰 식별번호 다음 번호로.
            int maxHumanId = -1;
            foreach (PD2Point point in points)
            {
                if (point.type == MapLoader.PointHuman) maxHumanId = Math.Max(maxHumanId, point.id);
            }
            Vector2 center = ViewportCenter();
            bool surfaceThere = SurfaceUnder(center, out Vector3 surface);
            AddPoint(MapLoader.PointHuman, center);
            Expect(points.Count == count + 1 && m_selection.Count == 1 && m_selection.Min == count, "놓은 포인트가 목록 끝에 더해져 선택되지 않음");
            Expect(points[count].type == MapLoader.PointHuman && points[count].id == maxHumanId + 1, "놓은 포인트의 종류나 식별번호가 다름");
            Expect(points[count].position.DistanceTo(surfaceThere ? surface : m_view.Pivot) < tolerance, "놓은 포인트가 화면의 그 자리 아래의 면에 있지 않음");
            Expect(m_markers.GetChildCount() == count + 1 && m_pointList.ItemCount == count + 1, "놓은 뒤에 표식이나 목록의 수가 맞지 않음");
            Undo();
            Expect(points.Count == count && m_markers.GetChildCount() == count && m_selection.Count == 0, "놓기를 되돌렸는데 포인트가 남음");
            Redo();
            Expect(points.Count == count + 1, "놓기를 다시 하지 못함");

            // 복제: 복제한 것들이 선택되고 옮기기가 시작된다. 취소해도 복제한 것은 남는다.
            SetSelection(new[] { a, b });
            DuplicateSelection();
            Expect(points.Count == count + 3 && Transforming && m_selection.Count == 2 && m_selection.Min == count + 1, "복제한 포인트가 선택된 채 옮기기가 시작되지 않음");
            CancelTransform();
            Expect(points.Count == count + 3 && points[count + 1].position.DistanceTo(points[a].position) < tolerance && points[count + 1].id == points[a].id,
                "복제한 포인트가 원본과 같지 않거나 취소하자 사라짐");

            // 지우기.
            DeleteSelection();
            Expect(points.Count == count + 1 && m_selection.Count == 0 && m_markers.GetChildCount() == count + 1, "선택한 포인트를 지우지 못함");
            Undo();
            Expect(points.Count == count + 3, "지우기를 되돌리지 못함");
            Redo();

            // 저장: 쓴 파일을 다시 읽으면 같은 내용이고, 두 번째 저장은 전의 파일을 .bak 으로 남긴다.
            string saved = $"{k_selfTestFolder}/saved.pd2";
            string savedFull = GamePath.Resolve(saved);
            Expect(m_dirty && SavePointsTo(saved) && !m_dirty && m_document.PointPath == saved, "저장한 뒤에 경로나 바뀜 표시가 맞지 않음");
            bool read = PD2File.Read(savedFull, out PD2File reread, out _);
            Expect(read && reread.points.Count == points.Count && reread.points[count].id == points[count].id
                && reread.points[count].position.DistanceTo(points[count].position) < tolerance && reread.eventEntryIds.Count == m_document.Points.eventEntryIds.Count,
                "저장한 파일을 다시 읽은 내용이 다름");
            Expect(!File.Exists(savedFull + k_backupExtension), "처음 저장인데 백업 파일이 생김");
            ApplyField(PointField.Id, 5);
            SavePoints();
            Expect(File.Exists(savedFull + k_backupExtension) && !m_dirty, "덮어쓰면서 전의 파일을 .bak 으로 남기지 않음");
            Expect(!SavePointsTo("../outside.pd2"), "게임 폴더 밖에 저장함");
        }

        /// <summary>
        /// 블록 편집을 확인한다: 모드 전환, 꼭짓점·모서리·면·블록 선택(클릭, 겹친 것, 사각형, X-RAY), 옮기기·돌리기·크기 바꾸기, 놓기·복제·지우기, 되돌리기, 저장.
        /// </summary>
        private void CheckBlockEditing()
        {
            const float tolerance = 1e-3f;
            List<BD2Block> blocks = m_document.Blocks.blocks;
            Camera3D camera = m_view.Camera;
            int count = blocks.Count;
            int pointsSelected = m_selection.Count;

            SetEditMode(EditMode.Block);
            Expect(m_blockOverlay.Visible && !m_singleBox.Visible && !m_multiBox.Visible, "블록 모드인데 덧그림이 보이지 않거나 포인트의 값 칸이 남아 있음");
            Expect(count > 0 && MapLoader.Blocks.Count == count, "문서의 블록 수와 화면의 블록 수가 다름");

            // 꼭짓점: 누른 자리의 꼭짓점과, 같은 자리에 겹친 다른 블록의 꼭짓점이 함께 선택된다.
            SetXray(true);
            SetBlockElement(BlockElement.Vertex);
            ViewTop(false);
            if (!m_view.Orthographic) m_view.ToggleOrthographic();
            FocusAll();
            int shared = -1;
            var coincident = new List<int>();
            for (int b = 0; b < count && shared < 0; b++)
            {
                for (int v = 0; v < BD2Block.VertexCount && shared < 0; v++)
                {
                    coincident.Clear();
                    CollectCoincident(b * ElementStride + v, coincident);
                    if (coincident.Count > 1) shared = b * ElementStride + v;
                }
            }
            int vertexKey = shared >= 0 ? shared : 0;
            Vector3 vertexAt = VertexPosition(vertexKey);
            BlockSelectAt(camera.UnprojectPosition(vertexAt), false, false);
            bool allThere = m_blockSelection.Count > 0;
            foreach (int key in m_blockSelection)
            {
                // 위에서 보면 같은 가로 자리에 높이만 다른 꼭짓점이 겹쳐 찍힌다. 그중 하나와 그것에 겹친 것들이 선택된다.
                allThere &= camera.UnprojectPosition(VertexPosition(key)).DistanceTo(camera.UnprojectPosition(vertexAt)) < 1f;
            }
            Expect(allThere, "누른 자리의 꼭짓점이 선택되지 않음");
            int first = m_blockSelection.Count > 0 ? m_blockSelection.Min : 0;
            coincident.Clear();
            CollectCoincident(first, coincident);
            Expect(m_blockSelection.Count == coincident.Count && coincident.TrueForAll(m_blockSelection.Contains), "같은 자리에 겹친 꼭짓점이 함께 선택되지 않음");

            // Ctrl + 클릭: 겹친 것이 여럿이면 바로 고르지 않고 묻는다. 하나를 고르면 그것만 선택된다.
            if (shared >= 0)
            {
                m_blockSelection.Clear();
                BlockSelectAt(camera.UnprojectPosition(vertexAt), false, true);
                Expect(m_blockSelection.Count == 0 && m_coincidentKeys.Count > 1 && m_coincidentMenu.ItemCount == m_coincidentKeys.Count + 1,
                    "겹친 꼭짓점을 Ctrl + 클릭했는데 고르는 메뉴가 준비되지 않음");
                int chosen = m_coincidentKeys[1];
                OnCoincidentChosen(chosen);
                Expect(m_blockSelection.Count == 1 && m_blockSelection.Contains(chosen), "겹친 것 가운데 고른 블록의 꼭짓점만 선택되지 않음");
            }

            // 전부 선택, 사각형 선택과 X-RAY.
            BlockSelectAll(true);
            Expect(m_blockSelection.Count == count * BD2Block.VertexCount, "전부 선택이 모든 꼭짓점을 고르지 않음");
            Rect2 screen = GetViewport().GetVisibleRect();
            BlockSelectBox(screen, false);
            int withXray = m_blockSelection.Count;
            SetXray(false);
            BlockSelectBox(screen, false);
            int withoutXray = m_blockSelection.Count;
            Expect(withXray == count * BD2Block.VertexCount, $"X-RAY 를 켠 사각형이 화면 안의 꼭짓점을 전부 고르지 않음 ({withXray})");
            Expect(withoutXray > 0 && withoutXray < withXray, $"X-RAY 를 끈 사각형이 가려진 꼭짓점을 빼지 않음 ({withoutXray} / {withXray})");
            SetXray(true);

            // 모서리: 모서리의 가운데를 누르면 그 모서리(와 겹친 모서리)가 선택된다.
            SetBlockElement(BlockElement.Edge);
            Expect(m_blockSelection.Count == 0, "선택 단위를 바꿨는데 선택이 남음");
            int edgeKey = vertexKey / ElementStride * ElementStride;
            Vector3 edgeMiddle = ElementCenter(BlockElement.Edge, edgeKey);
            BlockSelectAt(camera.UnprojectPosition(edgeMiddle), false, false);
            bool edgeThere = m_blockSelection.Count > 0;
            foreach (int key in m_blockSelection)
            {
                edgeThere &= camera.UnprojectPosition(ElementCenter(BlockElement.Edge, key)).DistanceTo(camera.UnprojectPosition(edgeMiddle)) < 1f;
            }
            Expect(edgeThere, "누른 자리의 모서리가 선택되지 않음");

            // 새 블록: 화면 가운데 아래의 면 위에 한 변 1 m 의 상자가 놓이고 블록 단위로 선택된다.
            Vector2 center = ViewportCenter();
            bool surfaceThere = SurfaceUnder(center, out Vector3 surface);
            int undoBefore = m_history.UndoCount;
            AddBlock(center);
            int added = count;
            Expect(blocks.Count == count + 1 && MapLoader.Blocks.Count == count + 1 && m_blockDirty, "새 블록이 문서와 화면에 더해지지 않음");
            Expect(m_blockElement == BlockElement.Block && m_blockSelection.Count == 1 && m_blockSelection.Contains(added * ElementStride), "새 블록이 블록 단위로 선택되지 않음");
            Vector3 boxCenter = ElementCenter(BlockElement.Block, added * ElementStride);
            Expect(blocks[added].vertices[0].DistanceTo(blocks[added].vertices[6]) > 1.7f && blocks[added].vertices[0].DistanceTo(blocks[added].vertices[1]) < 1f + tolerance
                && (!surfaceThere || boxCenter.DistanceTo(surface + Vector3.Up * 0.5f) < tolerance), "새 블록이 면 위에 놓인 한 변 1 m 의 상자가 아님");
            // 위에서 내려 쏜 레이가 새 블록의 윗면에 맞는다 (면의 앞뒤가 바르게 만들어졌다).
            Expect(MapLoader.RaycastBlock(BlockLayer.Human, boxCenter + Vector3.Up * 0.75f, Vector3.Down, 1f, out float toTop) && Mathf.Abs(toTop - 0.25f) < 0.01f,
                "새 블록의 윗면이 위에서 쏜 레이에 맞지 않음");

            // 면과 블록: 새 블록의 윗면 가운데를 누르면 그 면이, 블록 단위에서는 그 블록이 선택된다.
            Vector2 topAt = camera.UnprojectPosition(boxCenter + Vector3.Up * 0.5f);
            SetBlockElement(BlockElement.Face);
            BlockSelectAt(topAt, false, false);
            Expect(m_blockSelection.Count == 1 && m_blockSelection.Min / ElementStride == added && ElementCenter(BlockElement.Face, m_blockSelection.Min).DistanceTo(boxCenter + Vector3.Up * 0.5f) < tolerance,
                "누른 자리의 면(새 블록의 윗면)이 선택되지 않음");

            // 면을 위로 0.5 m 올리면 그 면의 꼭짓점 넷만 움직이고, 화면의 블록도 다시 만들어진다.
            BeginTransform(TransformMode.Move);
            SetTransformAxis(TransformAxis.Y);
            foreach (char typed in "0.5")
            {
                TypeNumeric(new InputEventKey { Unicode = typed, Pressed = true });
            }
            ConfirmTransform();
            float top = float.MinValue;
            float bottom = float.MaxValue;
            foreach (Vector3 vertex in blocks[added].vertices)
            {
                top = Mathf.Max(top, vertex.Y);
                bottom = Mathf.Min(bottom, vertex.Y);
            }
            Expect(Mathf.Abs(top - bottom - 1.5f) < tolerance, "면을 올렸는데 블록의 높이가 그만큼 늘지 않음");
            Expect(MapLoader.RaycastBlock(BlockLayer.Human, boxCenter + Vector3.Up * 1.25f, Vector3.Down, 1f, out toTop) && Mathf.Abs(toTop - 0.25f) < 0.01f,
                "면을 올린 뒤에 화면의 블록(판정)이 다시 만들어지지 않음");
            Undo();
            top = float.MinValue;
            foreach (Vector3 vertex in blocks[added].vertices)
            {
                top = Mathf.Max(top, vertex.Y);
            }
            Expect(Mathf.Abs(top - bottom - 1f) < tolerance, "면 옮기기를 되돌리지 못함");

            SetBlockElement(BlockElement.Block);
            BlockSelectAt(topAt, false, false);
            Expect(m_blockSelection.Count == 1 && m_blockSelection.Contains(added * ElementStride), "블록 단위에서 누른 블록이 선택되지 않음");

            // 블록 돌리기와 크기 바꾸기: 가운데는 그대로이고, 90° 돌리면 꼭짓점의 가로·세로가 바뀌고, 2배면 한 변이 2 m 가 된다.
            BeginTransform(TransformMode.Rotate);
            foreach (char typed in "90")
            {
                TypeNumeric(new InputEventKey { Unicode = typed, Pressed = true });
            }
            ConfirmTransform();
            Vector3 corner = s_boxCorners[0];
            Vector3 rotated = boxCenter + new Vector3(-corner.Z, corner.Y, corner.X);
            Expect(ElementCenter(BlockElement.Block, added * ElementStride).DistanceTo(boxCenter) < tolerance && blocks[added].vertices[0].DistanceTo(rotated) < tolerance,
                "블록을 세로축 둘레로 90° 돌린 결과가 다름");
            BeginTransform(TransformMode.Scale);
            TypeNumeric(new InputEventKey { Unicode = '2', Pressed = true });
            ConfirmTransform();
            Expect(ElementCenter(BlockElement.Block, added * ElementStride).DistanceTo(boxCenter) < tolerance
                && Mathf.Abs(blocks[added].vertices[0].DistanceTo(blocks[added].vertices[1]) - 2f) < tolerance, "블록을 2배로 키운 결과가 다름");
            BeginTransform(TransformMode.Scale);
            SetTransformAxis(TransformAxis.Y);
            foreach (char typed in "0.5")
            {
                TypeNumeric(new InputEventKey { Unicode = typed, Pressed = true });
            }
            ConfirmTransform();
            Expect(Mathf.Abs(blocks[added].vertices[0].DistanceTo(blocks[added].vertices[4]) - 1f) < tolerance
                && Mathf.Abs(blocks[added].vertices[0].DistanceTo(blocks[added].vertices[1]) - 2f) < tolerance, "한 축으로만 크기를 바꾸지 못함");

            // 격자: 블록을 옮기면 맨 앞의 꼭짓점이 격자점에 온다.
            m_gridLock = true;
            m_gridSize = 0.5f;
            Vector2 mouseFrom = camera.UnprojectPosition(boxCenter);
            BeginTransform(TransformMode.Move);
            m_transformStartMouse = mouseFrom;
            m_transformMouse = mouseFrom + new Vector2(37f, 21f);
            UpdateTransform();
            Vector3 gridded = VertexPosition(m_transformVertexKeys[0]) / 0.5f;
            Expect(Mathf.Abs(gridded.X - Mathf.Round(gridded.X)) < tolerance && Mathf.Abs(gridded.Z - Mathf.Round(gridded.Z)) < tolerance, "격자를 켜고 블록을 옮겼는데 꼭짓점이 격자점에 오지 않음");
            CancelTransform();
            m_gridLock = false;
            m_gridSize = k_defaultGridSize;

            // 복제와 지우기, 되돌리기.
            DuplicateBlocks();
            Expect(blocks.Count == count + 2 && Transforming && m_blockSelection.Contains((count + 1) * ElementStride), "복제한 블록이 선택된 채 옮기기가 시작되지 않음");
            CancelTransform();
            Expect(blocks.Count == count + 2 && blocks[count + 1].vertices[0].DistanceTo(blocks[added].vertices[0]) < tolerance, "복제한 블록이 원본과 같지 않거나 취소하자 사라짐");
            DeleteBlocks();
            Expect(blocks.Count == count + 1 && MapLoader.Blocks.Count == count + 1 && m_blockSelection.Count == 0, "선택한 블록을 지우지 못함");
            Undo();
            Expect(blocks.Count == count + 2, "블록 지우기를 되돌리지 못함");
            Redo();

            // 저장: 쓴 파일을 다시 읽으면 같은 내용이고, 두 번째 저장은 전의 파일을 .bak 으로 남긴다.
            string saved = $"{k_selfTestFolder}/saved.bd2";
            string savedFull = GamePath.Resolve(saved);
            Expect(SaveBlocksTo(saved) && !m_blockDirty && m_document.BlockPath == saved, "블록을 저장한 뒤에 경로나 바뀜 표시가 맞지 않음");
            bool read = BD2File.Read(savedFull, out BD2File reread, out _);
            Expect(read && reread.blocks.Count == blocks.Count && reread.blocks[added].vertices[0].DistanceTo(blocks[added].vertices[0]) < tolerance
                && reread.textureListPath == m_document.Blocks.textureListPath, "저장한 블록 파일을 다시 읽은 내용이 다름");
            AddBlock(center);
            SaveAll();
            Expect(File.Exists(savedFull + k_backupExtension) && !m_blockDirty && blocks.Count == count + 2, "블록을 덮어쓰면서 전의 파일을 .bak 으로 남기지 않음");

            // 처음 추가하기 전까지 되돌리면 블록 수가 처음으로 돌아간다. 포인트의 선택은 블록을 편집하는 동안 그대로다.
            while (m_history.UndoCount > undoBefore)
            {
                Undo();
            }
            Expect(blocks.Count == count && MapLoader.Blocks.Count == count, "블록 편집을 전부 되돌렸는데 블록 수가 처음과 다름");
            SetEditMode(EditMode.Point);
            Expect(!m_blockOverlay.Visible && m_selection.Count == pointsSelected, "포인트 모드로 돌아왔는데 덧그림이 남거나 포인트의 선택이 바뀜");
        }

        /// <summary>
        /// 블록의 값을 확인한다: 면의 텍스처·재질·UV, 블록의 통과 플래그, 텍스처 목록(더하기, 바꾸기, 저장), 빈 맵에서 새로 시작하기.
        /// </summary>
        private void CheckBlockValues()
        {
            const float tolerance = 1e-4f;
            const int topFace = 0;
            List<BD2Block> blocks = m_document.Blocks.blocks;
            List<BlockTextureData> textures = m_document.Textures.blockTextureData;
            int textureCount = textures.Count;

            SetEditMode(EditMode.Block);
            Expect(m_blockBox.Visible && textureCount > 0 && m_textureList.ItemCount == textureCount, "블록 모드인데 블록의 값 칸이나 텍스처 목록이 보이지 않음");
            AddBlock(ViewportCenter());
            int added = blocks.Count - 1;
            BD2Block block = blocks[added];

            // 면 하나: 텍스처, 그리지 않기, 재질, UV.
            SetBlockElement(BlockElement.Face);
            m_blockSelection.Add(added * ElementStride + topFace);
            UpdateDetails();
            Expect(m_faceBox.Visible && m_faceTexture.Selected == 1 && m_faceTexture.ItemCount == textureCount + 1, "면을 선택했는데 면의 값 칸이 그 면의 값을 보여 주지 않음");
            SetFaceTexture(1);
            Expect(block.textureIndices[topFace] == 1 && block.textureIndices[1] == 0 && m_faceTexture.Selected == 2, "면의 텍스처를 그 면만 바꾸지 못함");
            int surfaces = MapLoader.Blocks[added].mesh.GetSurfaceCount();
            Expect(surfaces == 2, $"텍스처를 바꾼 뒤 화면의 블록이 다시 만들어지지 않음 (서피스 {surfaces}개)");
            SetFaceTexture(-1);
            Expect(block.textureIndices[topFace] == -1 && m_faceTexture.Selected == 0 && MapLoader.Blocks[added].mesh.GetSurfaceCount() == 1, "면을 그리지 않게 하지 못함");
            Undo();
            Expect(block.textureIndices[topFace] == 1, "면의 텍스처 바꾸기를 되돌리지 못함");
            SetFaceMaterial(0);
            Expect(block.materialIndices[topFace] == 0 && block.materialIndices[1] == -1, "면의 재질을 바꾸지 못함");
            SetFaceUV(2, new Vector2(3f, 4f));
            Expect(block.uvs[topFace * 4 + 2].DistanceTo(new Vector2(3f, 4f)) < tolerance && Mathf.Abs((float)m_uvBoxes[4].Value - 3f) < 0.01f, "면의 UV 한 모서리를 바꾸지 못함");
            TransformFaceUV(UVOperation.Fit);
            Expect(block.uvs[topFace * 4].DistanceTo(Vector2.Zero) < tolerance && block.uvs[topFace * 4 + 2].DistanceTo(Vector2.One) < tolerance, "UV 꼭 맞추기가 다름");
            TransformFaceUV(UVOperation.Rotate);
            Expect(block.uvs[topFace * 4].DistanceTo(new Vector2(1f, 0f)) < tolerance && block.uvs[topFace * 4 + 3].DistanceTo(Vector2.Zero) < tolerance, "UV 돌리기가 다름");
            TransformFaceUV(UVOperation.Fit);
            TransformFaceUV(UVOperation.FlipU);
            Expect(block.uvs[topFace * 4].DistanceTo(new Vector2(1f, 0f)) < tolerance && block.uvs[topFace * 4 + 1].DistanceTo(Vector2.Zero) < tolerance, "UV 가로 뒤집기가 다름");
            TransformFaceUV(UVOperation.FlipV);
            Expect(block.uvs[topFace * 4].DistanceTo(Vector2.One) < tolerance, "UV 세로 뒤집기가 다름");

            // 블록 단위: 여섯 면 전부와 통과 플래그.
            SetBlockElement(BlockElement.Block);
            m_blockSelection.Add(added * ElementStride);
            UpdateDetails();
            SetFaceTexture(0);
            bool allFaces = true;
            foreach (int texture in block.textureIndices)
            {
                allFaces &= texture == 0;
            }
            Expect(allFaces, "블록 단위에서 텍스처를 바꿨는데 여섯 면 전부가 바뀌지 않음");
            Expect(MapLoader.Blocks[added].Collides(BlockLayer.Bullet) && !m_flagBoxes[1].ButtonPressed, "새 블록이 처음부터 총알을 통과시킴");
            SetBlockFlag(BD2File.PassBullet, true);
            Expect((block.flags & BD2File.PassBullet) != 0 && !MapLoader.Blocks[added].Collides(BlockLayer.Bullet) && MapLoader.Blocks[added].Collides(BlockLayer.Human)
                && m_flagBoxes[1].ButtonPressed, "총알 통과 플래그를 켰는데 판정이나 체크 표시가 바뀌지 않음");
            SetBlockFlag(BD2File.PassBullet, false);
            Expect(block.flags == 0 && MapLoader.Blocks[added].Collides(BlockLayer.Bullet), "총알 통과 플래그를 끄지 못함");

            // 꼭짓점 단위에서는 면의 칸이 없고 플래그만 있다.
            SetBlockElement(BlockElement.Vertex);
            m_blockSelection.Add(added * ElementStride);
            UpdateDetails();
            Expect(!m_faceBox.Visible && !m_flagBoxes[0].Disabled, "꼭짓점 단위에서 면의 값 칸이 보이거나 플래그 칸이 막혀 있음");

            // 텍스처 목록: 더하기, 바꾸기, 되돌리기. 없는 파일은 받지 않는다.
            string firstTexture = textures[0].diffusePath;
            string secondTexture = textures[1].diffusePath;
            Expect(SetTexture(firstTexture, -1) && textures.Count == textureCount + 1 && m_textureList.ItemCount == textureCount + 1, "텍스처를 목록에 더하지 못함");
            Expect(SetTexture(secondTexture, textureCount) && textures[textureCount].diffusePath == secondTexture, "목록의 텍스처를 바꾸지 못함");
            Expect(!SetTexture("data/no_such_texture.bmp", -1) && textures.Count == textureCount + 1, "없는 이미지 파일을 텍스처로 받아들임");
            Undo();
            Expect(textures[textureCount].diffusePath == firstTexture, "텍스처 바꾸기를 되돌리지 못함");
            Redo();

            // 저장: 다른 이름으로 저장하면 텍스처 목록도 그 옆에 따로 쓰인다.
            string saved = $"{k_selfTestFolder}/values.bd2";
            string savedTextures = $"{k_selfTestFolder}/values_textures.json";
            Expect(SaveBlocksTo(saved) && m_document.Blocks.textureListPath == savedTextures && File.Exists(GamePath.Resolve(savedTextures)),
                "다른 이름으로 저장했는데 텍스처 목록이 블록 파일 옆에 쓰이지 않음");
            Expect(OpenBlock(saved) && m_document.Textures.blockTextureData.Count == textureCount + 1 && m_document.Blocks.blocks.Count == added + 1
                && m_document.Textures.blockTextureData[textureCount].diffusePath == secondTexture, "저장한 블록과 텍스처 목록을 다시 연 내용이 다름");

            // 빈 맵에서 시작: 텍스처를 더하고, 블록을 놓고, 저장한 뒤 다시 열면 그려지는 블록 하나가 있다.
            NewMap();
            Expect(m_document.Blocks.blocks.Count == 0 && m_document.Points.points.Count == 0 && MapLoader.Blocks.Count == 0 && m_markers.GetChildCount() == 0
                && m_document.BlockPath.Length == 0 && !m_dirty && !m_blockDirty && m_history.UndoCount == 0, "새 맵이 비어 있지 않음");
            SetTexture(firstTexture, -1);
            AddBlock(ViewportCenter());
            string fresh = $"{k_selfTestFolder}/fresh/new.bd2";
            Expect(m_document.Blocks.blocks.Count == 1 && SaveBlocksTo(fresh), "새 맵에 블록을 놓고 저장하지 못함");
            Expect(OpenBlock(fresh) && m_document.Blocks.blocks.Count == 1 && m_document.Textures.blockTextureData.Count == 1 && MapLoader.Blocks.Count == 1
                && MapLoader.Blocks[0].mesh != null && MapLoader.Blocks[0].mesh.GetSurfaceCount() == 1, "새로 만든 맵을 다시 열었는데 그려지는 블록이 없음");
            // 게임의 로더도 같은 파일을 그대로 읽는다.
            Expect(MapLoader.LoadBlockData(GamePath.Resolve(fresh)) && MapLoader.Blocks.Count == 1 && MapLoader.Blocks[0].mesh != null, "에디터가 쓴 블록 파일을 게임의 로더가 읽지 못함");
            SetEditMode(EditMode.Point);
        }

        /// <summary>
        /// 원본 형식 가져오기와 플레이 테스트를 확인한다: BD1 / PD1 을 이름 없는 내용으로 가져오기, 공식 미션을 확장 형식 한 벌로 가져오기,
        /// 저장하지 않은 편집까지 담은 임시 파일을 게임이 로드하는지, 문서의 경로와 "바뀜" 표시가 그대로인지, 돌아온 뒤 화면이 문서의 내용인지, 플레이할 사람이 없는 맵을 거절하는지.
        /// 씬을 실제로 바꿨다가 돌아오는 것은 여기서 보지 않는다 (--play 로 직접 본다).
        /// </summary>
        private void CheckPlayAndImport()
        {
            OfficialMissionData official = DataManager.Instance.MissionData.officialMissions[k_selfTestMission];

            Expect(ImportBlock(official.bd1Path) && m_document.Blocks.blocks.Count > 0 && MapLoader.Blocks.Count == m_document.Blocks.blocks.Count
                && m_document.BlockPath.Length == 0 && m_blockDirty && m_document.Textures.blockTextureData.Count > 0, "원본 블록 데이터를 이름 없는 내용으로 가져오지 못함");
            Expect(ImportPoints(official.pd1Path) && m_document.Points.points.Count > 0 && m_markers.GetChildCount() == m_document.Points.points.Count
                && m_document.PointPath.Length == 0 && m_dirty && m_document.Points.eventEntryIds.Count == 3, "원본 포인트 데이터를 이름 없는 내용으로 가져오지 못함");
            Expect(!ImportBlock($"{k_selfTestFolder}/no_such.bd1") && m_messageIsError && m_document.Blocks.blocks.Count > 0, "없는 원본 블록 파일을 가져왔거나 실패했는데 내용이 바뀜");
            string importedBlocks = $"{k_selfTestFolder}/imported_block.bd2";
            Expect(SaveBlocksTo(importedBlocks) && SavePointsTo($"{k_selfTestFolder}/imported_point.pd2") && !m_dirty && !m_blockDirty
                && MapLoader.LoadBlockData(GamePath.Resolve(importedBlocks)) && MapLoader.Blocks.Count == m_document.Blocks.blocks.Count, "가져온 내용을 저장한 파일을 게임의 로더가 읽지 못함");

            // 미션 가져오기: 한 벌을 쓰고 그것을 연다.
            m_importSource = null;
            m_importOfficial = k_selfTestMission;
            string target = $"{k_selfTestFolder}/imported/mission.mif2";
            Expect(ImportMission(target) && m_document.Mission != null && m_document.MissionPath == target && m_document.BlockPath == $"{k_selfTestFolder}/imported/mission.bd2"
                && File.Exists(GamePath.Resolve(target)) && !m_dirty && !m_blockDirty && MapLoader.Instance.MissionName.Length == 0, "공식 미션을 확장 형식 한 벌로 가져와 열지 못함");
            m_importSource = target;
            Expect(!ImportMission($"{k_selfTestFolder}/imported/again.mif2") && m_messageIsError, "이미 확장 형식인 미션을 가져오기로 받아들임");

            // 플레이 테스트: 저장하지 않은 편집(블록 하나)까지 임시 파일에 담기고, 문서는 그대로다.
            SetEditMode(EditMode.Block);
            AddBlock(ViewportCenter());
            SetEditMode(EditMode.Point);
            int blockCount = m_document.Blocks.blocks.Count;
            string blockPath = m_document.BlockPath;
            string textureListPath = m_document.Blocks.textureListPath;
            string playMission = WritePlayFiles(out string error);
            Expect(playMission != null && error == null && m_document.BlockPath == blockPath && m_document.Blocks.textureListPath == textureListPath && m_blockDirty && !m_dirty,
                $"플레이 테스트용 파일을 쓰지 못했거나 쓰면서 문서의 경로나 바뀜 표시를 건드림 ({error})");
            GameBridge game = GameBridge.Instance;
            Expect(playMission != null && game.LoadMissionFile(GamePath.Resolve(playMission)) && MapLoader.Player != null && MapLoader.HumanCount > 1
                && MapLoader.Blocks.Count == blockCount && MapLoader.Instance.SkyIndex == m_document.Mission.skyIndex && MapLoader.Instance.AdjustCollision == m_document.Mission.adjustCollision,
                $"플레이 테스트용 미션을 게임이 로드하지 못했거나 편집한 내용·미션 설정과 다름 ({game.LastLoadError()})");
            game.UnloadMission();
            RestoreView();
            Expect(MapLoader.HumanCount == 0 && MapLoader.Blocks.Count == blockCount && MapLoader.Blocks[0].mesh != null, "플레이 테스트에서 돌아온 뒤 화면이 문서의 내용이 아님");

            // 플레이할 사람이 없는 맵은 넘어가지 않고 이유를 알린다.
            NewMap();
            Expect(!PlayTest() && m_messageIsError && !m_playing && !game.HasHeldScene() && IsInsideTree(), "사람이 없는 맵으로 플레이 테스트를 시작해 버림");
            Expect(OpenMission(target), "가져온 미션을 다시 열지 못함");
        }

        /// <summary>
        /// 이벤트 편집을 확인한다: 등록 정보로 그린 칸(정수·실수 칸, 추가 파라미터 늘리기), 화면에서 고르기, 다음 이벤트 더하기, 줄의 시작 번호,
        /// 이벤트 보기, 연결선, 메시지, 그리고 에디터가 놓은 스크립트 이벤트를 게임이 로드하는지.
        /// </summary>
        private void CheckEvents()
        {
            List<PD2Point> points = m_document.Points.points;
            Camera3D camera = m_view.Camera;
            Expect(m_catalog.Get(12)?.name == "Wait Death" && m_catalog.Get(20)?.name == "Wait Variable" && m_catalog.ExitsOf(60).Count == 2
                && m_catalog.ExitsOf(10).Count == 0 && m_catalog.ExitsOf(9999).Count == 1, "이벤트 종류의 목록(원본, 기본 제공 묶음)을 읽지 못함");

            // 스크립트 이벤트를 놓으면 이름 붙은 칸이 나오고 P2 / P3 / Extra 칸은 감춰진다.
            int eventsBefore = NextEventId();
            AddPoint(20, ViewportCenter());
            int first = m_selection.Min;
            Expect(points[first].type == 20 && points[first].id == eventsBefore && m_eventBox.Visible && m_slotRows.Count == 4
                && !m_fieldBoxes[PointField.P2].Visible && !m_extraEdit.Visible, "스크립트 이벤트를 놓았는데 이름 붙은 칸이 나오지 않음");
            EventDefinitionData waitVariable = m_catalog.Get(20);
            ApplySlot(waitVariable.parameters[0], 3);
            ApplySlot(waitVariable.parameters[2], 7);
            Expect(points[first].param1 == 3 && points[first].extra.Length == 2 && points[first].extra[1] == 7 && (int)m_slotRows[2].Box.Value == 7, "이벤트의 정수 칸을 고치지 못했거나 추가 파라미터가 늘지 않음");
            ApplySlot(waitVariable.parameters[1], 4);
            Expect(points[first].extra[0] == 4 && m_slotRows[1].Hint.Text == ">", "비교 칸의 값이나 그 뜻이 다름");
            Undo();
            Expect(points[first].extra[0] == 0, "이벤트 칸의 편집을 되돌리지 못함");

            // 실수 칸은 추가 파라미터에 실수의 비트로 들어간다.
            AddPoint(22, ViewportCenter());
            int area = m_selection.Min;
            EventDefinitionData waitArea = m_catalog.Get(22);
            ApplySlot(waitArea.parameters[1], 2.5);
            Expect(points[area].extra.Length >= 1 && BitConverter.Int32BitsToSingle(points[area].extra[0]) == 2.5f && Mathf.Abs((float)m_slotRows[1].Box.Value - 2.5f) < 1e-4f,
                "이벤트의 실수 칸이 실수로 들어가지 않음");

            // 화면에서 고르기: 사람을 받는 칸은 사람을 누르면 그 식별번호가 들어가고, 다른 종류를 누르면 거절한다. 선택은 그대로다.
            ViewTop(false);
            if (!m_view.Orthographic) m_view.ToggleOrthographic();
            FocusAll();
            SetXray(true);
            int human = -1;
            int other = -1;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 at = camera.UnprojectPosition(PointMarkers.Center(points[i]));
                if (m_markers.Pick(camera, at, true) != i) continue;
                if (KindAccepts("human", points[i].type)) human = human < 0 ? i : human;
                else if (points[i].type == MapLoader.PointAIPath) other = other < 0 ? i : other;
            }
            Expect(human >= 0 && other >= 0, "점검에 쓸 사람과 경로 포인트를 화면에서 찾지 못함");
            if (human >= 0 && other >= 0)
            {
                BeginPick(waitArea.parameters[0]);
                SelectAt(camera.UnprojectPosition(PointMarkers.Center(points[other])), false);
                Expect(m_messageIsError && m_pickSlot == null && points[area].param1 == 0 && m_selection.Count == 1 && m_selection.Min == area, "사람을 받는 칸이 다른 종류의 포인트를 받아들임");
                BeginPick(waitArea.parameters[0]);
                SelectAt(camera.UnprojectPosition(PointMarkers.Center(points[human])), false);
                Expect(points[area].param1 == points[human].id && m_selection.Min == area && m_slotRows[0].Hint.Text.StartsWith($"→ #{human}", StringComparison.Ordinal),
                    "화면에서 고른 사람의 식별번호가 칸에 들어가지 않음");
            }
            BeginPick(waitArea.parameters[0]);
            Expect(CancelPick() && m_pickSlot == null, "고르기를 취소하지 못함");

            // 다음 이벤트 더하기: 새 이벤트를 놓고 앞 이벤트의 출구를 잇는다. 되돌리면 둘 다 돌아간다.
            SetSelection(new[] { first });
            int count = points.Count;
            int nextBefore = points[first].param2;
            m_nextSlot = m_catalog.ExitsOf(20)[0];
            AddNextEvent(25);
            Expect(points.Count == count + 1 && points[count].type == 25 && points[first].param2 == points[count].id && m_selection.Min == count
                && points[count].id != points[area].id, "다음 이벤트 더하기가 새 이벤트를 놓고 출구를 잇지 않음");
            Undo();
            Expect(points.Count == count && points[first].param2 == nextBefore, "다음 이벤트 더하기를 되돌리지 못함");
            Redo();

            // 이벤트 줄의 시작 번호.
            int lines = m_document.Points.eventEntryIds.Count;
            Expect(lines == 3 && ApplyEntryIds($"156, 146, 136, {points[first].id}") && m_document.Points.eventEntryIds.Count == 4 && m_dirty, "이벤트 줄의 시작 번호를 고치지 못함");
            Expect(!ApplyEntryIds("156, x") && m_messageIsError && m_document.Points.eventEntryIds.Count == 4, "숫자가 아닌 시작 번호를 받아들임");

            // 이벤트 보기: 줄의 머리말이 있고, 모든 이벤트가 한 번은 나온다. 새 줄의 두 이벤트는 이어서 나온다.
            int eventFilter = Array.FindIndex(s_filters, filter => filter.Name == k_eventFilterName);
            m_filter.Select(eventFilter);
            RefreshPointList();
            var listed = new HashSet<int>(m_listToPoint);
            bool allListed = true;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].type >= MapLoader.PointEventFirst) allListed &= listed.Contains(i);
            }
            int firstRow = m_listToPoint.IndexOf(first);
            Expect(m_entryBox.Visible && m_listToPoint[0] == k_listNoPoint && allListed && firstRow > 0 && m_listToPoint[firstRow + 1] == count,
                "이벤트 보기가 줄의 머리말과 줄을 따라간 순서를 보여 주지 않음");
            SetSelection(new[] { count });
            Expect(m_pointList.GetSelectedItems().Length == 1 && m_listToPoint[m_pointList.GetSelectedItems()[0]] == count, "이벤트 보기에서 선택이 목록에 반영되지 않음");
            m_filter.Select(0);
            RefreshPointList();
            Expect(!m_entryBox.Visible && m_pointList.ItemCount == points.Count, "이벤트 보기에서 전체 보기로 돌아오지 못함");
            Undo();
            Expect(m_document.Points.eventEntryIds.Count == 3, "시작 번호 편집을 되돌리지 못함");
            Redo();

            RedrawLinks();
            Expect(m_links.Visible && !m_linksDirty, "연결선을 그리지 못함");

            // 메시지.
            List<string> messages = m_document.Messages;
            int messageCount = messages.Count;
            InsertMessage(messageCount, "first");
            InsertMessage(messageCount + 1, "second");
            Expect(messages.Count == messageCount + 2 && messages[messageCount] == "first" && m_messageList.ItemCount == messages.Count, "메시지를 더하지 못함");
            MoveMessage(messageCount + 1, -1);
            EditMessage(messageCount, "second edited");
            Expect(messages[messageCount] == "second edited" && messages[messageCount + 1] == "first", "메시지를 옮기거나 고치지 못함");
            RemoveMessage(messageCount + 1);
            Expect(messages.Count == messageCount + 1, "메시지를 지우지 못함");
            Undo();
            Expect(messages.Count == messageCount + 2 && messages[messageCount + 1] == "first", "메시지 지우기를 되돌리지 못함");

            // 저장하고 다시 열면 이벤트의 칸, 시작 번호, 메시지가 그대로다.
            string saved = $"{k_selfTestFolder}/events.pd2";
            int[] extra = points[first].extra;
            Expect(SavePointsTo(saved) && OpenPoints(saved) && m_document.Points.eventEntryIds.Count == 4 && m_document.Messages.Count == messageCount + 2
                && m_document.Points.points[first].type == 20 && m_document.Points.points[first].extra.Length == extra.Length && m_document.Points.points[first].extra[1] == 7,
                "저장한 이벤트를 다시 연 내용이 다름");

            // 에디터가 놓은 스크립트 이벤트가 든 맵을 게임이 로드한다.
            GameBridge game = GameBridge.Instance;
            string playMission = WritePlayFiles(out _);
            Expect(playMission != null && game.LoadMissionFile(GamePath.Resolve(playMission)) && MapLoader.Player != null && MapLoader.EventEntryIds.Count == 4,
                $"에디터가 놓은 스크립트 이벤트가 든 맵을 게임이 로드하지 못함 ({game.LastLoadError()})");
            game.UnloadMission();
            RestoreView();
        }

        /// <summary>
        /// 미션 모드를 확인한다: 화면이 바뀌는지, 설정을 고치고 되돌리는지, 미션 전용 이벤트 묶음이 이벤트 목록에 들어오는지, 미션 파일을 저장하는지.
        /// </summary>
        private void CheckMission()
        {
            SetEditMode(EditMode.Mission);
            Expect(m_missionPanel.Visible && !m_links.Visible && m_modeOption.Selected == (int)EditMode.Mission, "미션 모드의 화면이 보이지 않음");

            string name = m_document.Mission.name;
            Expect(EditMission("Edit Name", mission => mission.name = "Edited") && m_document.Mission.name == "Edited" && m_missionDirty, "미션의 이름을 고치지 못함");
            Expect(!EditMission("Edit Name", mission => mission.name = "Edited"), "바뀐 것이 없는데 편집으로 기록함");
            Undo();
            Expect(m_document.Mission.name == name, "미션의 편집을 되돌리지 못함");
            Redo();

            int sky = m_document.Mission.skyIndex;
            EditMission("Edit Sky", mission => mission.skyIndex = sky == 1 ? 2 : 1);
            Expect(m_shownSky == m_document.Mission.skyIndex && m_shownSky != sky, "하늘 번호를 바꿨는데 화면의 하늘이 바뀌지 않음");

            // 미션 전용 이벤트 묶음: 등록 파일을 가리키면 그 이벤트가 목록에 들어온다.
            string pack = $"{k_selfTestFolder}/mission_events.json";
            File.WriteAllText(GamePath.Resolve(pack), "{ \"scriptPath\": \"\", \"events\": [ { \"type\": 10000, \"name\": \"Custom\", \"function\": \"custom\", "
                + "\"parameters\": [ { \"name\": \"amount\", \"slot\": \"e0\", \"kind\": \"float\" } ] } ] }");
            EditMission("Edit pack", mission => mission.addonEventDataPath = pack);
            Expect(m_catalog.Get(10000)?.name == "Custom" && PointTypeInfo.Get(10000).Name == "Custom", "미션 전용 이벤트 묶음이 이벤트 목록에 들어오지 않음");
            Undo();
            Expect(m_catalog.Get(10000) == null, "이벤트 묶음 편집을 되돌렸는데 그 이벤트가 목록에 남음");

            string saved = $"{k_selfTestFolder}/saved.mif2";
            Expect(SaveMissionTo(saved) && !m_missionDirty && m_document.MissionPath == saved && MIF2File.Read(GamePath.Resolve(saved), out ExtendedMissionData written, out _)
                && written.name == "Edited" && written.blockPath == m_document.BlockPath && written.pointPath == m_document.PointPath && written.skyIndex == m_document.Mission.skyIndex,
                "미션 파일을 저장하지 못했거나 내용이 다름");
            Expect(MapLoader.LoadMissionFile(GamePath.Resolve(saved)) && MapLoader.Instance.MissionName == "Edited", "에디터가 쓴 미션 파일을 게임이 읽지 못함");
            MapLoader.UnloadMissionData();

            SetEditMode(EditMode.Point);
            Expect(!m_missionPanel.Visible && m_links.Visible, "미션 모드에서 돌아왔는데 3D 화면이 돌아오지 않음");

            // 이름 없는 맵은 미션 파일을 쓸 수 없다 (미션 파일이 블록과 포인트의 경로를 담는다).
            NewMap();
            Expect(!SaveMissionTo($"{k_selfTestFolder}/empty.mif2") && m_messageIsError && m_document.MissionPath.Length == 0, "저장하지 않은 맵의 미션 파일을 써 버림");
            Expect(OpenMission($"{k_selfTestFolder}/imported/mission.mif2"), "가져온 미션을 다시 열지 못함");
        }

        /// <summary>
        /// 에셋 모드를 확인한다: 데이터 파일의 종류를 알아내는지, 값(글, 수, 참·거짓, 벡터의 성분)을 고치고 되돌리는지, 목록의 항목을 더하고 복제하고 옮기고 지우는지,
        /// 저장하면 파일에 있던 키만 쓰이는지, 새 에드온 데이터 파일을 만들면 미션이 그것을 가리키는지. 기본 데이터 파일은 고치지 않고 복사본으로 본다.
        /// </summary>
        private void CheckAssets()
        {
            SetEditMode(EditMode.Asset);
            Expect(m_assetPanel.Visible && !m_missionPanel.Visible && !m_links.Visible && m_assetListPaths.Contains("godotdata/weapon/list.json")
                && !m_assetListPaths.Contains("godotdata/config.json"), "에셋 모드의 화면이나 파일 목록이 다름");

            string copy = $"{k_selfTestFolder}/weapon_list.json";
            string original = EncodingHelper.ReadAllText(GamePath.Resolve("godotdata/weapon/list.json"));
            File.WriteAllText(GamePath.Resolve(copy), original);
            Expect(OpenAsset(copy) && m_asset.FileKind.Type == typeof(WeaponParameterData) && m_asset.Fields.Count == 1 && m_asset.Fields[0].Name == "weaponData"
                && m_asset.UnknownKeys == 0 && m_assetExtraPaths.Contains(copy) && m_assetTree.GetRoot().GetChildCount() == 1, "무기 목록 파일을 무기 데이터로 열지 못함");
            Expect(m_asset.Serialize().Replace("\r", string.Empty).Length > 0 && !m_asset.Dirty, "연 파일을 다시 JSON 으로 만들지 못함");

            var weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            int weaponCount = weapons.Count;
            AssetNode list = m_assetNodes.Find(node => node.Label == "weaponData");
            AssetNode first = m_assetNodes.Find(node => node.Parent == list && node.Index == 0);
            AssetNode name = m_assetNodes.Find(node => node.Parent == first && node.Label == "name");
            string oldName = weapons[0].name;
            Expect(list != null && list.ElementType == typeof(WeaponData) && first != null && name != null && first.Item.GetText(1) == oldName, "트리에 무기 목록과 첫 무기의 이름이 없음");

            CheckAssetPreview(first, name);
            // 미리 보기 점검이 다른 파일을 열었다 돌아오면서 트리를 다시 만들었다. 줄을 다시 찾는다.
            list = m_assetNodes.Find(node => node.Label == "weaponData");
            first = m_assetNodes.Find(node => node.Parent == list && node.Index == 0);
            name = m_assetNodes.Find(node => node.Parent == first && node.Label == "name");

            // 값 하나: 글, 수, 참·거짓, 벡터의 성분.
            Expect(SetAssetValue(name, "Renamed") && weapons[0].name == "Renamed" && m_asset.Dirty && first.Item.GetText(1) == "Renamed" && AssetsDirty(), "글 값을 고치지 못함");
            Expect(!SetAssetValue(name, "Renamed"), "바뀐 것이 없는데 편집으로 기록함");
            Undo();
            weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            Expect(weapons[0].name == oldName, "데이터 파일의 편집을 되돌리지 못함");
            Redo();
            weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            Expect(weapons[0].name == "Renamed", "데이터 파일의 편집을 다시 하지 못함");

            first = m_assetNodes.Find(node => node.Parent != null && node.Parent.Label == "weaponData" && node.Index == 0);
            AssetNode number = m_assetNodes.Find(node => node.Parent == first && node.Type == typeof(int));
            AssetNode real = m_assetNodes.Find(node => node.Parent == first && node.Type == typeof(float));
            AssetNode flag = m_assetNodes.Find(node => node.Parent == first && node.Type == typeof(bool));
            Expect(number != null && real != null, "무기에 정수나 실수 값이 없음");
            if (number != null && real != null)
            {
                int oldNumber = (int)number.Get();
                Expect(TryParseAssetValue(typeof(int), " 42 ", out object parsed) && SetAssetValue(number, parsed) == (oldNumber != 42) && (int)number.Get() == 42, "정수 값을 고치지 못함");
                Expect(TryParseAssetValue(typeof(float), "1.25", out parsed) && SetAssetValue(real, parsed) && (float)real.Get() == 1.25f && real.Item.GetText(1) == "1.25", "실수 값을 고치지 못함");
                Expect(!TryParseAssetValue(typeof(int), "abc", out _) && !TryParseAssetValue(typeof(float), "NaN", out _), "수가 아닌 글을 수로 받아들임");
            }
            if (flag != null)
            {
                bool oldFlag = (bool)flag.Get();
                Expect(SetAssetValue(flag, !oldFlag) && (bool)flag.Get() == !oldFlag && flag.Item.IsChecked(1) == !oldFlag, "참·거짓 값을 고치지 못함");
            }
            AssetNode component = m_assetNodes.Find(node => node.Parent != null && node.Parent.Type == typeof(Vector3) && node.Label == "y");
            if (component != null)
            {
                Expect(SetAssetValue(component, 9.5f) && ((Vector3)component.Parent.Get()).Y == 9.5f && component.Parent.Item.GetText(1).Contains("9.5"), "벡터의 성분 하나를 고치지 못함");
            }

            // 목록: 더하기, 복제, 옮기기, 지우기.
            list = m_assetNodes.Find(node => node.Label == "weaponData");
            list.Item.Select(0);
            AssetAddItem(false);
            weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            Expect(weapons.Count == weaponCount + 1 && m_assetTree.GetSelected() != null && AssetNodeOf(m_assetTree.GetSelected()).Index == weaponCount, "목록에 항목을 더하지 못했거나 새 항목이 선택되지 않음");
            m_assetNodes.Find(node => node.Parent != null && node.Parent.Label == "weaponData" && node.Index == 0).Item.Select(0);
            AssetAddItem(true);
            weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            Expect(weapons.Count == weaponCount + 2 && weapons[1].name == "Renamed" && !ReferenceEquals(weapons[0], weapons[1]), "항목을 복제해 바로 뒤에 넣지 못함");
            AssetMoveItem(1);
            weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            Expect(weapons[2].name == "Renamed" && weapons[1].name != "Renamed" && AssetNodeOf(m_assetTree.GetSelected()).Index == 2, "항목을 뒤로 옮기지 못함");
            AssetRemoveItem();
            weapons = ((WeaponParameterData)m_asset.Container).weaponData;
            Expect(weapons.Count == weaponCount + 1 && weapons[2].name != "Renamed", "항목을 지우지 못함");
            Undo();
            Expect(((WeaponParameterData)m_asset.Container).weaponData.Count == weaponCount + 2, "항목 지우기를 되돌리지 못함");

            // 저장: 파일에 있던 키만 쓰이고, 다시 읽으면 고친 내용이다.
            Expect(SaveAsset(m_asset) && !m_asset.Dirty && File.Exists(GamePath.Resolve(copy) + ".bak"), "데이터 파일을 저장하지 못함");
            string written = EncodingHelper.ReadAllText(GamePath.Resolve(copy));
            Expect(AssetFile.Load(copy, out AssetFile reread, out _) && reread.Fields.Count == 1 && ((WeaponParameterData)reread.Container).weaponData.Count == weaponCount + 2
                && ((WeaponParameterData)reread.Container).weaponData[0].name == "Renamed" && !written.Contains("weaponGeneralData"), "저장한 데이터 파일의 내용이나 키가 다름");
            Expect(DataManager.Instance.WeaponParameterData.weaponData.Count == weaponCount && DataManager.Instance.WeaponParameterData.weaponData[0].name == oldName, "복사본을 고쳤는데 게임의 기본 데이터가 바뀜");

            // 기본 데이터를 다시 읽어도 내용이 같다.
            int humans = DataManager.Instance.HumanParameterData.humanData.Count;
            DataManager.Instance.Reload();
            Expect(DataManager.Instance.HumanParameterData.humanData.Count == humans && DataManager.Instance.WeaponParameterData.weaponData.Count == weaponCount
                && DataManager.Instance.WeaponParameterData.weaponData[0].name == oldName, "기본 데이터를 다시 읽은 내용이 다름");

            // 새 에드온 데이터 파일: 목록 섹션만 갖고, 미션에 그 종류의 파일이 없었으면 미션이 가리킨다.
            string addon = $"{k_selfTestFolder}/addon_objects.json";
            AssetFile.Kind objectKind = Array.Find(AssetFile.Kinds, kind => kind.Type == typeof(ObjectParameterData));
            string before = m_document.Mission.addonObjectDataPath;
            Expect(before.Length == 0 && CreateAsset(addon, objectKind) && m_document.Mission.addonObjectDataPath == addon && m_missionDirty && m_asset.Path == addon
                && m_asset.Fields.Count == 3 && m_assetListPaths.Contains(addon), "새 에드온 데이터 파일을 만들지 못했거나 미션이 그것을 가리키지 않음");
            string addonText = EncodingHelper.ReadAllText(GamePath.Resolve(addon));
            Expect(addonText.Contains("objectData") && addonText.Contains("objectColliderData") && !addonText.Contains("objectGeneralData"), "새 에드온 데이터 파일에 목록 섹션이 아닌 것이 들어감");

            // 모르는 파일과 깨진 파일은 거절한다.
            string broken = $"{k_selfTestFolder}/broken.json";
            File.WriteAllText(GamePath.Resolve(broken), "{ \"nothingKnown\": 1 }");
            Expect(!OpenAsset(broken) && m_messageIsError && m_asset.Path == addon, "아는 키가 없는 파일을 데이터 파일로 열어 버림");
            File.WriteAllText(GamePath.Resolve(broken), "{ not json");
            Expect(!OpenAsset(broken) && m_messageIsError, "JSON 이 아닌 파일을 열어 버림");

            SetEditMode(EditMode.Point);
            Expect(!m_assetPanel.Visible && m_links.Visible, "에셋 모드에서 돌아왔는데 3D 화면이 돌아오지 않음");
            m_assetFiles.Clear();
            m_assetExtraPaths.Clear();
            m_asset = null;
            m_missionDirty = false;
            Expect(OpenMission($"{k_selfTestFolder}/imported/mission.mif2"), "가져온 미션을 다시 열지 못함");
        }

        /// <summary>
        /// 에셋 모드의 미리 보기를 확인한다: 무기를 선택하면 그 모델이, 그 안의 값을 선택해도 같은 모델이 나오는지, 오브젝트는 충돌 형상과 함께, 사람은 몸통·팔·다리가,
        /// 이미지 경로를 선택하면 그림이 나오는지. 기본 데이터 파일은 열어 보기만 한다.
        /// </summary>
        /// <param name="weapon">무기 목록의 첫 무기 줄.</param>
        /// <param name="weaponName">그 무기의 이름 줄.</param>
        private void CheckAssetPreview(AssetNode weapon, AssetNode weaponName)
        {
            int MeshCount()
            {
                int count = 0;
                var pending = new Stack<Node>();
                pending.Push(m_previewRoot);
                while (pending.Count > 0)
                {
                    Node node = pending.Pop();
                    foreach (Node child in node.GetChildren()) pending.Push(child);
                    if (node is MeshInstance3D { Mesh: not null }) count++;
                }
                return count;
            }

            Expect(m_previewRoot.GetChildCount() == 0 && m_previewContainer.Visible, "아무것도 선택하지 않았는데 미리 보기에 무언가 있음");

            // 모델이 있는 무기를 찾는다 (맨손은 모델이 없다).
            AssetNode list = weapon.Parent;
            AssetNode armed = m_assetNodes.Find(node => node.Parent == list && node.Get() is WeaponData data
                && DataManager.Instance.WeaponParameterData.weaponModelData.Has(data.modelIndex)
                && DataManager.Instance.WeaponParameterData.weaponModelData[data.modelIndex].modelData.Count > 0);
            Expect(armed != null, "모델이 있는 무기를 찾지 못함");
            if (armed != null)
            {
                armed.Item.Select(0);
                RefreshAssetPreview();
                int meshes = MeshCount();
                Expect(meshes > 0 && m_previewCaption.Text.StartsWith("Weapon:", StringComparison.Ordinal) && ReferenceEquals(m_previewSubject, armed.Get()), "무기를 선택했는데 그 모델이 미리 보기에 나오지 않음");
                m_assetNodes.Find(node => node.Parent == armed && node.Label == "name").Item.Select(0);
                RefreshAssetPreview();
                Expect(MeshCount() == meshes && ReferenceEquals(m_previewSubject, armed.Get()), "무기 안의 값을 선택했는데 그 무기의 미리 보기가 아님");
            }

            Expect(OpenAsset("godotdata/weapon/model.json"), "무기 모델 파일을 열지 못함");
            AssetNode texture = m_assetNodes.Find(node => node.Type == typeof(string) && node.Parent != null && node.Parent.Label == "textures" && !string.IsNullOrEmpty(node.Get() as string));
            Expect(texture != null, "무기 모델에 텍스처 경로가 없음");
            if (texture != null)
            {
                texture.Item.Select(0);
                RefreshAssetPreview();
                Expect(m_previewImage.Visible && m_previewImage.Texture != null && !m_previewContainer.Visible, "이미지 경로를 선택했는데 그림이 나오지 않음");
                texture.Parent.Parent.Item.Select(0);
                RefreshAssetPreview();
                Expect(!m_previewImage.Visible && m_previewContainer.Visible && MeshCount() > 0 && m_previewCaption.Text.StartsWith("Weapon model:", StringComparison.Ordinal), "무기 모델을 선택했는데 모델이 나오지 않음");
            }

            Expect(OpenAsset("godotdata/object/list.json"), "오브젝트 목록 파일을 열지 못함");
            AssetNode firstObject = m_assetNodes.Find(node => node.Get() is ObjectData);
            if (firstObject != null)
            {
                firstObject.Item.Select(0);
                RefreshAssetPreview();
                Expect(MeshCount() >= 2 && m_previewRoot.FindChild("Collider", true, false) != null && m_previewCaption.Text.StartsWith("Object:", StringComparison.Ordinal), "오브젝트를 선택했는데 모델과 충돌 형상이 나오지 않음");
            }

            Expect(OpenAsset("godotdata/human/list.json"), "사람 목록 파일을 열지 못함");
            AssetNode firstHuman = m_assetNodes.Find(node => node.Get() is HumanData);
            if (firstHuman != null)
            {
                firstHuman.Item.Select(0);
                RefreshAssetPreview();
                Expect(MeshCount() >= 3 && m_previewRoot.FindChild("Legs", true, false) != null && m_previewRoot.FindChild("Arms", true, false) != null, "사람을 선택했는데 몸통·팔·다리가 나오지 않음");
            }

            // 팔과 다리의 메시 넘겨 보기: 번호가 바뀌고 끝에서 처음으로 돌아온다. 다른 사람을 고르면 처음부터다.
            if (firstHuman != null)
            {
                Expect(m_previewArmCount > 1 && m_previewLegCount > 1 && m_previewArmBox.Visible && m_previewLegBox.Visible && m_previewLegLabel.Text == $"1 / {m_previewLegCount}",
                    "사람을 선택했는데 팔과 다리의 메시를 고르는 칸이 없음");
                Mesh firstLeg = ((MeshInstance3D)m_previewRoot.FindChild("Legs", true, false)).Mesh;
                object subject = m_previewSubject;
                m_previewLeg++;
                RefreshAssetPreview();
                Expect(m_previewLeg == 1 && ((MeshInstance3D)m_previewRoot.FindChild("Legs", true, false)).Mesh != firstLeg && ReferenceEquals(subject, m_previewSubject)
                    && m_previewLegLabel.Text == $"2 / {m_previewLegCount}", "다리의 다음 메시로 넘어가지 않음");
                m_previewLeg = m_previewLegCount;
                m_previewArm = -1;
                RefreshAssetPreview();
                Expect(m_previewLeg == 0 && m_previewArm == m_previewArmCount - 1, "메시 번호가 끝에서 처음으로(처음에서 끝으로) 돌아가지 않음");
                AssetNode secondHuman = m_assetNodes.Find(node => node.Get() is HumanData && node.Index == 1);
                secondHuman.Item.Select(0);
                RefreshAssetPreview();
                Expect(m_previewArm == 0 && m_previewLeg == 0, "다른 사람을 선택했는데 팔과 다리가 첫 메시가 아님");
            }

            // 이펙트: 되풀이해 재생한다.
            Expect(OpenAsset("godotdata/effect_data.json"), "이펙트 파일을 열지 못함");
            AssetNode effectNode = m_assetNodes.Find(node => node.Get() is EffectData effect && effect.emitters.Count > 0 && effect.emitters[0].spawnCount > 0);
            Expect(effectNode != null, "emitter 가 있는 이펙트를 찾지 못함");
            if (effectNode != null)
            {
                effectNode.Item.Select(0);
                RefreshAssetPreview();
                var player = m_previewRoot.FindChild("Effect", true, false) as EffectPreview;
                Expect(player != null && player.Bursts == 1 && player.ParticleCount > 0 && m_previewCaption.Text.StartsWith("Effect:", StringComparison.Ordinal) && !m_previewArmBox.Visible,
                    "이펙트를 선택했는데 재생되지 않음");
                if (player != null)
                {
                    // 입자가 모두 사라지고 쉬는 시간이 지나면 다시 낸다.
                    for (int i = 0; i < 4000 && player.Bursts < 2; i++) player._Process(0.05);
                    Expect(player.Bursts >= 2 && player.ParticleCount > 0, "이펙트가 끝난 뒤 다시 재생되지 않음");
                }
            }

            Expect(!AssetsDirty(), "미리 보기만 했는데 데이터 파일이 바뀐 것으로 표시됨");
            Expect(OpenAsset($"{k_selfTestFolder}/weapon_list.json") && m_previewRoot.GetChildCount() == 0, "다른 파일로 바꿨는데 미리 보기가 남음");
        }

        /// <summary>
        /// 화면에서 한 포인트와 충분히 떨어져 찍히는 다른 포인트를 찾는다 (클릭이 서로를 잡지 않게).
        /// </summary>
        /// <param name="camera">카메라.</param>
        /// <param name="points">포인트들.</param>
        /// <param name="from">기준 포인트의 번호.</param>
        /// <returns>포인트 번호. 없으면 −1.</returns>
        private static int FindSeparatePoint(Camera3D camera, List<PD2Point> points, int from)
        {
            const float separation = 40f;
            Vector2 origin = camera.UnprojectPosition(PointMarkers.Center(points[from]));
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 at = camera.UnprojectPosition(PointMarkers.Center(points[i]));
                if (at.DistanceTo(origin) < separation) continue;

                // 그 자리를 눌렀을 때 정말 이 포인트가 잡혀야 한다 (다른 포인트와 겹쳐 있지 않아야 한다).
                bool alone = true;
                for (int k = 0; k < points.Count && alone; k++)
                {
                    alone = k == i || camera.UnprojectPosition(PointMarkers.Center(points[k])).DistanceTo(at) > separation * 0.5f;
                }
                if (alone) return i;
            }
            return -1;
        }

        /// <summary>
        /// 점검 결과를 출력하고 종료한다. 점검용 파일은 지운다.
        /// </summary>
        private void FinishSelfTest()
        {
            MapLoader.UnloadBlockData();
            string folder = GamePath.Resolve(k_selfTestFolder);
            if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, true);

            GD.Print($"에디터 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }
    }
}
