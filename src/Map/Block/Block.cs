using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 블록과의 판정 종류. 블록마다 판정별로 충돌 여부가 다를 수 있다 (BD2 의 블록 플래그). 값은 플래그의 비트 번호와 같다.
    /// </summary>
    public enum BlockLayer
    {
        // 사람의 이동·맵 충돌, 매몰 판정, 발밑 레이, 이동 경로 레이, 벽 블라인드, 떨어진 무기와 소물의 바닥.
        Human = 0,
        // 총알의 소멸과 수류탄의 반사.
        Bullet = 1,
        // AI 의 시야와 사선, 폭발 가림.
        Sight = 2,
    }

    /// <summary>
    /// BD1 이나 BD2 파일에서 읽어낸 블록의 원시 데이터를 담는 구조체. 어느 형식에서 읽었든 같은 모양이다.
    /// 정점은 Godot 좌표이고, 면 f 의 v 번째 정점의 UV 는 uvs[f * 4 + v] 다.
    /// </summary>
    public struct RawBlockData
    {
        public Vector3[] vertices;
        public Vector2[] uvs;
        public int[] textureIndices;
        // 면마다의 재질 번호. BD1 에는 재질이 없어 null 이다.
        public int[] materialIndices;
        // true 면 passFlags 로 충돌 여부를 정한다 (BD2). false 면 정점 모양으로 판형 블록인지 추론한다 (BD1).
        public bool hasPassFlags;
        // 판정을 끄는 비트 (BD2File.PassHuman / PassBullet / PassSight).
        public int passFlags;
    }

    /// <summary>
    /// 블록 메시, 텍스처, 위치, 충돌 정보를 담는 컨테이너 클래스.
    /// </summary>
    public class Block
    {
        // 보이는 면이 하나도 없는 블록이면 null.
        public ArrayMesh mesh;
        // mesh 의 서피스 순서와 같은 순서의 텍스처 인덱스.
        public int[] surfaceTextureIndices;
        public Vector3 position;
        // 파일 안에서의 블록 번호.
        public int index;
        // 충돌하는 판정의 비트 (1 << BlockLayer). 0 이면 어떤 판정에도 걸리지 않는다 (판형 블록).
        public int layerMask;
        // 면마다의 재질 번호. 재질이 없는 맵(BD1)이면 null.
        public int[] faceMaterials;
        public Vector3[] faceNormals;
        public Vector3[] faceCenters;

        // 블록 8정점을 감싸는 월드 AABB. 맵 로드 시 1회 계산. 충돌 브로드페이즈 fast-reject 용.
        public Vector3 boundsMin;
        public Vector3 boundsMax;

        /// <summary>
        /// 이 블록이 해당 판정에서 충돌하는지 알려 준다.
        /// </summary>
        /// <param name="layer">판정 종류.</param>
        /// <returns>충돌하면 true.</returns>
        public bool Collides(BlockLayer layer)
        {
            return (layerMask & (1 << (int)layer)) != 0;
        }

        /// <summary>
        /// 이 블록의 월드 AABB가 주어진 AABB(min~max)와 겹치는지 판정한다. 브로드페이즈 프리필터용 싸구려 테스트.
        /// </summary>
        /// <param name="min">질의 AABB 최소 코너.</param>
        /// <param name="max">질의 AABB 최대 코너.</param>
        /// <returns>겹치면 true.</returns>
        public bool OverlapsAABB(Vector3 min, Vector3 max)
        {
            return boundsMax.X >= min.X && boundsMin.X <= max.X
                && boundsMax.Y >= min.Y && boundsMin.Y <= max.Y
                && boundsMax.Z >= min.Z && boundsMin.Z <= max.Z;
        }

        /// <summary>
        /// 주어진 월드 좌표가 블록 내부에 있는지 판정한다. 블록이 어느 판정에서 충돌하는지는 보지 않는다 (호출하는 쪽이 판정별 목록에서 블록을 고른다).
        /// </summary>
        /// <param name="worldPoint">판정할 월드 좌표.</param>
        /// <returns>내부이면 true, 외부이면 false.</returns>
        public bool Contains(Vector3 worldPoint)
        {
            for (int i = 0; i < 6; i++)
            {
                float d = faceNormals[i].Dot(faceCenters[i] - worldPoint);
                if (d <= 0f) return false;
            }
            return true;
        }

        /// <summary>
        /// 블록 6면 중 보이는 앞면과 레이의 교차를 판정한다. (원본 Collision::CheckBlockIntersectRay 대응)
        /// </summary>
        /// <param name="origin">레이 시작점.</param>
        /// <param name="direction">레이 방향 (정규화 권장).</param>
        /// <param name="maxDist">최대 거리. 0 이하이면 무한.</param>
        /// <param name="hitFace">맞은 면 인덱스 (0~5).</param>
        /// <param name="hitDist">맞은 거리.</param>
        /// <returns>맞았으면 true.</returns>
        public bool IntersectRay(Vector3 origin, Vector3 direction, float maxDist, out int hitFace, out float hitDist)
        {
            hitFace = -1;
            hitDist = 0f;

            float minT = (maxDist > 0f) ? maxDist : float.MaxValue;
            int foundFace = -1;

            for (int i = 0; i < 6; i++)
            {
                Vector3 n = faceNormals[i];
                Vector3 c = faceCenters[i];

                // 원점이 면 앞쪽이어야 함 (면 뒤쪽에서는 무시)
                float ndc = n.Dot(c - origin);
                if (ndc >= 0f) continue;

                float ndd = n.Dot(direction);
                if (ndd >= -1e-6f) continue;

                float t = ndc / ndd;
                if (t < 0f || t > minT) continue;

                // p가 블록 내부(나머지 면들의 안쪽)인가?
                Vector3 p = origin + direction * t;
                bool inside = true;
                for (int j = 0; j < 6; j++)
                {
                    if (j == i) continue;
                    if (faceNormals[j].Dot(faceCenters[j] - p) < -1e-4f)
                    {
                        inside = false;
                        break;
                    }
                }
                if (!inside) continue;

                minT = t;
                foundFace = i;
            }

            if (foundFace == -1) return false;

            hitFace = foundFace;
            hitDist = minT;
            return true;
        }
    }
}
