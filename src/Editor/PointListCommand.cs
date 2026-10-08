using System.Collections.Generic;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 포인트의 수나 순서를 바꾼 편집 (더하기, 복제, 지우기). 편집 전과 후의 포인트 목록을 통째로 들고 있다.
    /// 되돌리면 목록의 포인트 객체가 새것으로 바뀌므로, 호출한 쪽이 표식과 목록을 다시 만들어야 한다.
    /// </summary>
    public sealed class PointListCommand : IEditorCommand
    {
        private readonly List<PD2Point> m_points;
        private readonly PD2Point[] m_before;
        private readonly PD2Point[] m_after;

        public string Name { get; }

        /// <summary>
        /// 편집을 만든다. 포인트 목록에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="points">문서의 포인트 목록.</param>
        /// <param name="before">바뀌기 전의 목록 (Snapshot 으로 떠 둔 것).</param>
        public PointListCommand(string name, List<PD2Point> points, PD2Point[] before)
        {
            Name = name;
            m_points = points;
            m_before = before;
            m_after = Snapshot(points);
        }

        public void Undo()
        {
            Restore(m_before);
        }

        public void Redo()
        {
            Restore(m_after);
        }

        /// <summary>
        /// 포인트 목록 전체의 복사본을 뜬다.
        /// </summary>
        /// <param name="points">포인트 목록.</param>
        /// <returns>복사본.</returns>
        public static PD2Point[] Snapshot(List<PD2Point> points)
        {
            var result = new PD2Point[points.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = PointChangeCommand.Clone(points[i]);
            }
            return result;
        }

        /// <summary>
        /// 목록을 떠 둔 내용으로 바꾼다.
        /// </summary>
        /// <param name="snapshot">떠 둔 내용.</param>
        private void Restore(PD2Point[] snapshot)
        {
            m_points.Clear();
            foreach (PD2Point point in snapshot)
            {
                m_points.Add(PointChangeCommand.Clone(point));
            }
        }
    }
}
