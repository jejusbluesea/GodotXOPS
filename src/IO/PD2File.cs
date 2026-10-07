using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace GodotXOPS.IO
{
    /// <summary>
    /// PD2 파일의 포인트 하나. 위치는 Godot 축의 미터 좌표다.
    /// </summary>
    public class PD2Point
    {
        public Vector3 position;
        // 그 자리에 놓이는 것의 yaw (도, 위에서 볼 때 오른쪽으로 돌수록 +). 사람은 0 일 때 -Z 를 본다. PD1 의 방향에서 사람과 무기는 +180°, 소물은 그대로다.
        public float direction;
        // 파라미터 4개. PD1 의 P1~P4 와 같은 뜻이고 값의 범위만 넓다 (종류, 두 번째, 세 번째, 식별번호).
        public int type;
        public int param1;
        public int param2;
        public int id;
        // 추가 파라미터. 4바이트 칸이고 포인트 종류에 따라 정수, 실수, 불(0 이면 거짓)로 읽는다.
        public int[] extra = Array.Empty<int>();
    }

    /// <summary>
    /// 확장 포인트 데이터(PD2) 파일의 읽기와 쓰기. 게임 싱글톤과 무관해서 에디터 같은 다른 도구에서도 쓸 수 있다.
    /// 구조 (정수는 리틀 엔디안): 매직 8바이트 → 이벤트 시작 번호 개수(uint32) → 시작 번호들(int32) → 포인트 개수(uint32) → 포인트들.
    /// 포인트: 위치(x, y, z float32) → 방향(float32) → 파라미터 4개(int32) → 추가 파라미터 개수(uint32) → 추가 파라미터(4바이트 × 개수).
    /// </summary>
    public class PD2File
    {
        public const string Extension = ".pd2";

        // 추가 파라미터가 없는 포인트 하나의 크기 (바이트).
        private const int k_pointBaseSize = 36;
        private const int k_cellSize = 4;

        private static readonly byte[] s_magic = Encoding.ASCII.GetBytes("GDXOPSPD");

        // 이벤트 줄마다의 시작 식별번호. 개수가 곧 이벤트 줄 수다.
        public List<int> eventEntryIds = new List<int>();
        public List<PD2Point> points = new List<PD2Point>();

        /// <summary>
        /// PD2 파일을 읽는다.
        /// </summary>
        /// <param name="filepath">PD2 파일 전체 경로.</param>
        /// <param name="file">읽은 내용. 실패하면 null.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>읽기에 성공했으면 true.</returns>
        public static bool Read(string filepath, out PD2File file, out string error)
        {
            file = null;
            error = null;

            try
            {
                using var reader = new BinaryReader(File.OpenRead(filepath));
                Stream stream = reader.BaseStream;

                byte[] magic = reader.ReadBytes(s_magic.Length);
                if (!magic.AsSpan().SequenceEqual(s_magic))
                {
                    error = "not a PD2 file (magic mismatch)";
                    return false;
                }

                var result = new PD2File();

                if (stream.Length - stream.Position < k_cellSize)
                {
                    error = "file ends before the event entry count";
                    return false;
                }
                uint entryCount = reader.ReadUInt32();
                if ((long)entryCount * k_cellSize > stream.Length - stream.Position)
                {
                    error = $"file is too short for {entryCount} event entries";
                    return false;
                }
                for (uint i = 0; i < entryCount; i++) result.eventEntryIds.Add(reader.ReadInt32());

                if (stream.Length - stream.Position < k_cellSize)
                {
                    error = "file ends before the point count";
                    return false;
                }
                uint pointCount = reader.ReadUInt32();
                if ((long)pointCount * k_pointBaseSize > stream.Length - stream.Position)
                {
                    error = $"file is too short for {pointCount} points";
                    return false;
                }

                for (uint p = 0; p < pointCount; p++)
                {
                    if (stream.Length - stream.Position < k_pointBaseSize)
                    {
                        error = $"file ends inside point {p}";
                        return false;
                    }

                    var point = new PD2Point();
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    point.position = new Vector3(x, y, z);
                    point.direction = reader.ReadSingle();
                    point.type = reader.ReadInt32();
                    point.param1 = reader.ReadInt32();
                    point.param2 = reader.ReadInt32();
                    point.id = reader.ReadInt32();

                    uint extraCount = reader.ReadUInt32();
                    if ((long)extraCount * k_cellSize > stream.Length - stream.Position)
                    {
                        error = $"file ends inside the extra parameters of point {p}";
                        return false;
                    }
                    point.extra = new int[extraCount];
                    for (uint i = 0; i < extraCount; i++) point.extra[i] = reader.ReadInt32();

                    result.points.Add(point);
                }

                file = result;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// PD2 파일로 쓴다. 같은 이름의 파일이 있으면 덮어쓴다.
        /// </summary>
        /// <param name="filepath">PD2 파일 전체 경로.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>쓰기에 성공했으면 true.</returns>
        public bool Write(string filepath, out string error)
        {
            error = null;

            try
            {
                using var writer = new BinaryWriter(File.Create(filepath));

                writer.Write(s_magic);
                writer.Write((uint)eventEntryIds.Count);
                foreach (int id in eventEntryIds) writer.Write(id);

                writer.Write((uint)points.Count);
                foreach (PD2Point point in points)
                {
                    writer.Write(point.position.X);
                    writer.Write(point.position.Y);
                    writer.Write(point.position.Z);
                    writer.Write(point.direction);
                    writer.Write(point.type);
                    writer.Write(point.param1);
                    writer.Write(point.param2);
                    writer.Write(point.id);

                    int[] extra = point.extra ?? Array.Empty<int>();
                    writer.Write((uint)extra.Length);
                    foreach (int cell in extra) writer.Write(cell);
                }

                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// 실수 값을 추가 파라미터 칸에 넣을 값으로 바꾼다.
        /// </summary>
        /// <param name="value">실수 값.</param>
        /// <returns>칸에 넣을 값.</returns>
        public static int FloatCell(float value)
        {
            return BitConverter.SingleToInt32Bits(value);
        }
    }
}
