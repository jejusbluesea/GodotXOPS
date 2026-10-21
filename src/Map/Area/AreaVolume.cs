using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 구역 블록 하나의 모양. 블록과 같은 꼭짓점 8개짜리 육면체이고, 안쪽 판정도 블록(Block.Contains)과 같은 계산이다.
    /// 그리지도 않고 어느 판정(사람, 총알, 시야)에도 걸리지 않는다.
    /// </summary>
    public class AreaVolume
    {
        public Vector3[] vertices;
        public Vector3[] faceNormals = new Vector3[6];
        public Vector3[] faceCenters = new Vector3[6];
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        // 구역 태그 번호 (맵 데이터 파일의 areaTagData).
        public int tag;

        /// <summary>
        /// 점이 이 구역 블록 안에 있는지 판정한다.
        /// </summary>
        /// <param name="point">월드 좌표.</param>
        /// <returns>안이면 true.</returns>
        public bool Contains(Vector3 point)
        {
            if (point.X < boundsMin.X || point.X > boundsMax.X || point.Y < boundsMin.Y || point.Y > boundsMax.Y
                || point.Z < boundsMin.Z || point.Z > boundsMax.Z)
            {
                return false;
            }
            for (int i = 0; i < 6; i++)
            {
                if (faceNormals[i].Dot(faceCenters[i] - point) <= 0f) return false;
            }
            return true;
        }
    }
}
