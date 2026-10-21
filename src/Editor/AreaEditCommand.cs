using System.Collections.Generic;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 구역을 바꾼 편집 (태그 더하기·지우기·성질 바꾸기, 블록을 구역으로, 구역을 블록으로). 편집 전과 후의 블록 목록, 구역 블록 목록, 태그 목록을 통째로 들고 있다.
    /// 문서의 목록은 화면과 로더가 가리키므로 목록 객체를 바꾸지 않고 내용만 옮긴다.
    /// </summary>
    public sealed class AreaEditCommand : IEditorCommand
    {
        private readonly MapDocument m_document;
        private readonly Snapshot m_before;
        private readonly Snapshot m_after;

        public string Name { get; }

        /// <summary>
        /// 문서의 블록, 구역 블록, 태그를 떠 둔 것.
        /// </summary>
        public sealed class Snapshot
        {
            public BD2Block[] blocks;
            public BD2Area[] areas;
            // 태그 목록 (areaTagData 만 든 맵 데이터의 JSON).
            public string tagsJson;
        }

        /// <summary>
        /// 편집을 만든다. 문서에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="document">문서.</param>
        /// <param name="before">바뀌기 전에 Take 로 떠 둔 것.</param>
        public AreaEditCommand(string name, MapDocument document, Snapshot before)
        {
            Name = name;
            m_document = document;
            m_before = before;
            m_after = Take(document);
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
        /// 문서의 블록, 구역 블록, 태그를 뜬다.
        /// </summary>
        /// <param name="document">문서.</param>
        /// <returns>떠 둔 것.</returns>
        public static Snapshot Take(MapDocument document)
        {
            var areas = new BD2Area[document.Blocks.areas.Count];
            for (int i = 0; i < areas.Length; i++) areas[i] = Clone(document.Blocks.areas[i]);
            return new Snapshot
            {
                blocks = BlockListCommand.Snapshot(document.Blocks.blocks),
                areas = areas,
                tagsJson = JsonData.ToJson(new BlockMapData { areaTagData = document.Textures.areaTagData }),
            };
        }

        /// <summary>
        /// 구역 블록 하나를 복사한다.
        /// </summary>
        /// <param name="area">구역 블록.</param>
        /// <returns>복사본.</returns>
        public static BD2Area Clone(BD2Area area)
        {
            return new BD2Area { vertices = (Godot.Vector3[])area.vertices.Clone(), tag = area.tag };
        }

        /// <summary>
        /// 문서를 떠 둔 내용으로 바꾼다.
        /// </summary>
        /// <param name="snapshot">떠 둔 것.</param>
        private void Restore(Snapshot snapshot)
        {
            List<BD2Block> blocks = m_document.Blocks.blocks;
            blocks.Clear();
            foreach (BD2Block block in snapshot.blocks) blocks.Add(BlockChangeCommand.Clone(block));

            List<BD2Area> areas = m_document.Blocks.areas;
            areas.Clear();
            foreach (BD2Area area in snapshot.areas) areas.Add(Clone(area));

            var copy = new BlockMapData();
            JsonData.Overwrite(snapshot.tagsJson, copy, "area tags");
            m_document.Textures.areaTagData.Clear();
            m_document.Textures.areaTagData.AddRange(copy.areaTagData);
        }
    }
}
