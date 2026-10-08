using System;
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
        // true 면 passFlags 가 판정별 충돌 여부를 정한다 (BD2). false 면 세 판정 모두 충돌한다 (BD1). 어느 쪽이든 판형 블록은 충돌하지 않는다.
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
        // 정점 모양이 유효한 입체가 아니어서(판형 블록) 플래그와 관계없이 충돌하지 않는 블록인지.
        public bool boardShape;
        // 면마다의 재질 번호. 재질이 없는 맵(BD1)이면 null.
        public int[] faceMaterials;
        public Vector3[] faceNormals;
        public Vector3[] faceCenters;

        // 블록 8정점을 감싸는 월드 AABB. 맵 로드 시 1회 계산. 충돌 브로드페이즈 fast-reject 용.
        public Vector3 boundsMin;
        public Vector3 boundsMax;

        // 레이가 이 블록에 맞을 수 있는 자리 전체를 감싸는 월드 AABB. 맵 로드 시 1회 계산 (ComputeRayBounds).
        // 레이 판정은 "한 면의 평면과 만나고 나머지 면의 안쪽"이라 면이 뒤틀린 블록에서는 맞는 자리가 8정점의 범위를 벗어날 수 있다. 그래서 boundsMin / boundsMax 와 따로 둔다.
        public Vector3 rayBoundsMin;
        public Vector3 rayBoundsMax;
        // false 면 여섯 평면이 닫힌 영역을 이루지 않아 범위를 구할 수 없는 블록이다. 레이를 거르지 않고 항상 검사한다.
        public bool rayBounded;

        // 레이 범위를 구할 때 면의 평면을 바깥으로 미는 거리 (m). IntersectRay 의 허용 오차(1e-4)와 float 계산 오차를 넉넉히 덮는다.
        private const double k_rayBoundsMargin = 0.01;
        // 닫힌 영역인지 볼 때의 허용 오차. 애매하면 "닫히지 않음"으로 본다 (거르지 않을 뿐 결과는 같다).
        private const double k_rayBoundsOpenTolerance = 1e-6;
        // 이보다 큰 범위는 사실상 열린 영역으로 보고 거르지 않는다 (m).
        private const double k_rayBoundsMaxExtent = 1e6;

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
        /// 레이가 이 블록에 맞을 수 있는 자리의 범위(rayBoundsMin / rayBoundsMax)를 구한다. faceNormals 와 faceCenters 가 채워진 뒤에 부른다.
        /// IntersectRay 가 맞았다고 하는 점은 항상 "여섯 평면을 조금씩 바깥으로 민 영역" 안에 있다. 그 영역의 꼭짓점(평면 셋의 교점 중 나머지 평면의 안쪽인 것)을 모아 감싼다.
        /// 영역이 닫혀 있지 않으면(한쪽으로 끝없이 열려 있으면) rayBounded 를 false 로 둔다.
        /// </summary>
        public void ComputeRayBounds()
        {
            rayBounded = false;
            rayBoundsMin = Vector3.Zero;
            rayBoundsMax = Vector3.Zero;

            // 길이가 0 인 법선(찌그러진 면)은 판정에서 아무것도 거르지 않으므로 뺀다.
            var nx = new double[6];
            var ny = new double[6];
            var nz = new double[6];
            var offsets = new double[6];
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = faceNormals[i];
                Vector3 c = faceCenters[i];
                if (!n.IsFinite() || !c.IsFinite()) return;
                if (n == Vector3.Zero) continue;

                nx[count] = n.X;
                ny[count] = n.Y;
                nz[count] = n.Z;
                // 영역: n·p <= n·c + 여유
                offsets[count] = (double)n.X * c.X + (double)n.Y * c.Y + (double)n.Z * c.Z + k_rayBoundsMargin;
                count++;
            }
            if (count < 4) return;

            // 닫힌 영역인지: 모든 면에 대해 n·v <= 0 인 방향 v 가 있으면 그쪽으로 열려 있다. 그런 방향이 있다면 두 평면의 교선 방향 중에 있다.
            bool anyEdge = false;
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    double vx = ny[a] * nz[b] - nz[a] * ny[b];
                    double vy = nz[a] * nx[b] - nx[a] * nz[b];
                    double vz = nx[a] * ny[b] - ny[a] * nx[b];
                    double length = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                    if (length == 0.0) continue;

                    anyEdge = true;
                    vx /= length;
                    vy /= length;
                    vz /= length;

                    bool openForward = true;
                    bool openBackward = true;
                    for (int j = 0; j < count; j++)
                    {
                        double dot = nx[j] * vx + ny[j] * vy + nz[j] * vz;
                        if (dot > k_rayBoundsOpenTolerance) openForward = false;
                        if (dot < -k_rayBoundsOpenTolerance) openBackward = false;
                    }
                    if (openForward || openBackward) return;
                }
            }
            if (!anyEdge) return;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool anyVertex = false;
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    // n_b × n_c 와 섞기 전에 n_a × n_b 를 한 번만 구한다.
                    double abx = ny[a] * nz[b] - nz[a] * ny[b];
                    double aby = nz[a] * nx[b] - nx[a] * nz[b];
                    double abz = nx[a] * ny[b] - ny[a] * nx[b];
                    for (int c = b + 1; c < count; c++)
                    {
                        double det = abx * nx[c] + aby * ny[c] + abz * nz[c];
                        if (det == 0.0) continue;

                        double bcx = ny[b] * nz[c] - nz[b] * ny[c];
                        double bcy = nz[b] * nx[c] - nx[b] * nz[c];
                        double bcz = nx[b] * ny[c] - ny[b] * nx[c];
                        double cax = ny[c] * nz[a] - nz[c] * ny[a];
                        double cay = nz[c] * nx[a] - nx[c] * nz[a];
                        double caz = nx[c] * ny[a] - ny[c] * nx[a];
                        double px = (offsets[a] * bcx + offsets[b] * cax + offsets[c] * abx) / det;
                        double py = (offsets[a] * bcy + offsets[b] * cay + offsets[c] * aby) / det;
                        double pz = (offsets[a] * bcz + offsets[b] * caz + offsets[c] * abz) / det;
                        if (!double.IsFinite(px) || !double.IsFinite(py) || !double.IsFinite(pz)) continue;

                        // 나머지 평면의 안쪽이어야 꼭짓점이다. 여유를 한 번 더 줘서 계산 오차로 진짜 꼭짓점을 놓치지 않게 한다 (더 받아들이면 범위가 커질 뿐이다).
                        bool inside = true;
                        for (int j = 0; j < count; j++)
                        {
                            if (j == a || j == b || j == c) continue;
                            if (nx[j] * px + ny[j] * py + nz[j] * pz > offsets[j] + k_rayBoundsMargin)
                            {
                                inside = false;
                                break;
                            }
                        }
                        if (!inside) continue;

                        anyVertex = true;
                        minX = Math.Min(minX, px);
                        minY = Math.Min(minY, py);
                        minZ = Math.Min(minZ, pz);
                        maxX = Math.Max(maxX, px);
                        maxY = Math.Max(maxY, py);
                        maxZ = Math.Max(maxZ, pz);
                    }
                }
            }
            if (!anyVertex) return;
            if (maxX - minX > k_rayBoundsMaxExtent || maxY - minY > k_rayBoundsMaxExtent || maxZ - minZ > k_rayBoundsMaxExtent) return;
            if (Math.Abs(minX) > k_rayBoundsMaxExtent || Math.Abs(minY) > k_rayBoundsMaxExtent || Math.Abs(minZ) > k_rayBoundsMaxExtent) return;

            // double → float 로 줄일 때 안쪽으로 반올림되지 않게 여유를 한 번 더 준다.
            float margin = (float)k_rayBoundsMargin;
            rayBoundsMin = new Vector3((float)minX - margin, (float)minY - margin, (float)minZ - margin);
            rayBoundsMax = new Vector3((float)maxX + margin, (float)maxY + margin, (float)maxZ + margin);
            rayBounded = true;
        }

        /// <summary>
        /// 레이가 이 블록에 맞을 가능성이 있는지 본다 (레이와 rayBounds 의 겹침). false 면 IntersectRay 도 반드시 false 다.
        /// 원본 Collision::CheckALLBlockIntersectRay 의 범위 프리컷에 해당한다. 범위를 구하지 못한 블록은 항상 true 다.
        /// </summary>
        /// <param name="origin">레이 시작점.</param>
        /// <param name="direction">레이 방향.</param>
        /// <param name="maxDist">최대 거리. 0 이하이면 무한.</param>
        /// <returns>맞을 수 있으면 true.</returns>
        public bool MayIntersectRay(Vector3 origin, Vector3 direction, float maxDist)
        {
            if (!rayBounded) return true;

            float tMin = 0f;
            float tMax = (maxDist > 0f) ? maxDist : float.MaxValue;

            // 축마다 레이가 범위 안에 있는 구간을 구해 겹친다. NaN 이 나오는 비교는 전부 거짓이라 구간을 좁히지 않는다 (거르지 않는 쪽).
            if (direction.X == 0f)
            {
                if (origin.X < rayBoundsMin.X || origin.X > rayBoundsMax.X) return false;
            }
            else
            {
                float inverse = 1f / direction.X;
                float t1 = (rayBoundsMin.X - origin.X) * inverse;
                float t2 = (rayBoundsMax.X - origin.X) * inverse;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return false;
            }

            if (direction.Y == 0f)
            {
                if (origin.Y < rayBoundsMin.Y || origin.Y > rayBoundsMax.Y) return false;
            }
            else
            {
                float inverse = 1f / direction.Y;
                float t1 = (rayBoundsMin.Y - origin.Y) * inverse;
                float t2 = (rayBoundsMax.Y - origin.Y) * inverse;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return false;
            }

            if (direction.Z == 0f)
            {
                if (origin.Z < rayBoundsMin.Z || origin.Z > rayBoundsMax.Z) return false;
            }
            else
            {
                float inverse = 1f / direction.Z;
                float t1 = (rayBoundsMin.Z - origin.Z) * inverse;
                float t2 = (rayBoundsMax.Z - origin.Z) * inverse;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return false;
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
