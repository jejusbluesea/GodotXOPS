using System.Collections.Generic;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 포인트 몇 개의 값을 바꾼 편집 (옮기기, 돌리기, 값 고치기). 바뀐 포인트마다 전과 후의 값을 통째로 들고 있다.
    /// 포인트의 수와 순서는 바꾸지 않는다 (더하기와 지우기는 다른 편집이다).
    /// </summary>
    public sealed class PointChangeCommand : IEditorCommand
    {
        private readonly List<PD2Point> m_points;
        private readonly int[] m_indices;
        private readonly PD2Point[] m_before;
        private readonly PD2Point[] m_after;

        public string Name { get; }
        // 바뀐 포인트의 번호들.
        public IReadOnlyList<int> Indices => m_indices;

        /// <summary>
        /// 편집을 만든다. 포인트 목록에는 이미 바뀐 값이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="points">문서의 포인트 목록.</param>
        /// <param name="indices">바뀐 포인트의 번호들.</param>
        /// <param name="before">바뀌기 전의 값 (indices 와 같은 순서의 복사본).</param>
        public PointChangeCommand(string name, List<PD2Point> points, int[] indices, PD2Point[] before)
        {
            Name = name;
            m_points = points;
            m_indices = indices;
            m_before = before;
            m_after = new PD2Point[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                m_after[i] = Clone(points[indices[i]]);
            }
        }

        public void Undo()
        {
            for (int i = 0; i < m_indices.Length; i++)
            {
                Copy(m_before[i], m_points[m_indices[i]]);
            }
        }

        public void Redo()
        {
            for (int i = 0; i < m_indices.Length; i++)
            {
                Copy(m_after[i], m_points[m_indices[i]]);
            }
        }

        /// <summary>
        /// 포인트의 복사본을 만든다.
        /// </summary>
        /// <param name="point">원본.</param>
        /// <returns>복사본 (추가 파라미터 배열도 따로 갖는다).</returns>
        public static PD2Point Clone(PD2Point point)
        {
            var copy = new PD2Point();
            Copy(point, copy);
            return copy;
        }

        /// <summary>
        /// 한 포인트의 값을 다른 포인트에 덮어쓴다. 문서의 포인트 객체는 표식과 목록이 가리키고 있으므로 객체를 바꾸지 않고 값만 옮긴다.
        /// </summary>
        /// <param name="from">값을 가져올 포인트.</param>
        /// <param name="to">값을 받을 포인트.</param>
        public static void Copy(PD2Point from, PD2Point to)
        {
            to.position = from.position;
            to.direction = from.direction;
            to.type = from.type;
            to.param1 = from.param1;
            to.param2 = from.param2;
            to.id = from.id;
            to.extra = (int[])from.extra.Clone();
        }
    }
}
