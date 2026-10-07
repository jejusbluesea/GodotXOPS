using System.IO;
using GodotXOPS.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        /// <summary>
        /// PD2 의 방향(그 자리에 놓이는 것의 yaw)과 RawPointData.look(사람 기준 yaw)의 차이를 구한다.
        /// 사람과 무기는 둘이 같고, 소물은 look 이 180° 크다. 그 밖의 포인트는 방향을 쓰지 않으므로 그대로 둔다.
        /// </summary>
        /// <param name="type">포인트 종류.</param>
        /// <returns>PD2 의 방향에 더하면 look 이 되는 값 (도).</returns>
        private static float LookOffset(int type)
        {
            return type == PointSmallObject ? k_modelYawOffset : 0f;
        }

        /// <summary>
        /// PD2 파일을 읽어 PD1 을 읽었을 때와 같은 구조로 만든다. 좌표는 이미 Godot 축의 미터라서 변환하지 않는다.
        /// </summary>
        /// <param name="filepath">PD2 파일 전체 경로.</param>
        /// <param name="points">파일 순서대로의 포인트 배열.</param>
        /// <param name="eventEntryIds">이벤트 줄마다의 시작 식별번호.</param>
        /// <returns>파싱에 성공했으면 true.</returns>
        private static bool LoadPD2File(string filepath, out RawPointData[] points, out int[] eventEntryIds)
        {
            points = null;
            eventEntryIds = null;

            if (!PD2File.Read(filepath, out PD2File file, out string error))
            {
                Debugger.LogError($"PD2 read failed: {filepath}\n{error}", nameof(MapLoader));
                return false;
            }

            eventEntryIds = file.eventEntryIds.ToArray();
            points = new RawPointData[file.points.Count];
            for (int i = 0; i < points.Length; i++)
            {
                PD2Point point = file.points[i];
                points[i] = new RawPointData
                {
                    position = point.position,
                    look = point.direction + LookOffset(point.type),
                    param0 = point.type,
                    param1 = point.param1,
                    param2 = point.param2,
                    param3 = point.id,
                    extra = point.extra,
                };
            }

            return true;
        }

        /// <summary>
        /// PD1 파일을 같은 미션이 되는 PD2 로 바꾼다. 이벤트는 PD1 의 세 줄(156, 146, 136) 그대로이고 추가 파라미터는 없다.
        /// </summary>
        /// <param name="pd1Path">PD1 파일 전체 경로.</param>
        /// <param name="file">만든 PD2. 실패하면 null.</param>
        /// <returns>변환에 성공했으면 true.</returns>
        public static bool ConvertPD1(string pd1Path, out PD2File file)
        {
            file = null;

            if (!File.Exists(pd1Path) || !LoadPD1File(pd1Path, out RawPointData[] points))
            {
                return false;
            }

            file = new PD2File();
            file.eventEntryIds.AddRange(s_legacyEventEntryIds);
            foreach (RawPointData raw in points)
            {
                file.points.Add(new PD2Point
                {
                    position = raw.position,
                    direction = raw.look - LookOffset(raw.param0),
                    type = raw.param0,
                    param1 = raw.param1,
                    param2 = raw.param2,
                    id = raw.param3,
                });
            }

            return true;
        }
    }
}
