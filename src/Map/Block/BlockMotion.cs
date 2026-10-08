using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 이벤트가 미션 도중에 블록을 옮기고 돌리거나(Move Block) 끄고 켜는(Toggle Block) 부분. 원본에는 없는 동작이다.
    /// 블록은 꼭짓점 8개의 월드 좌표로 저장되고 자기 위치나 회전이 없다. 그래서 "처음 모양에서 얼마나 옮기고 돌렸는가"를 블록마다 들고,
    /// 바뀔 때마다 면의 법선·중심과 범위 상자를 처음 모양에서 다시 구한다. 회전의 중심은 블록의 가운데다.
    /// 판정이 따라가야 하므로 틱에서 옮기고, 메시 노드는 틱 사이를 보간한다. 미션을 다시 시작하면(포인트 데이터를 내리면) 전부 처음으로 돌아간다.
    /// </summary>
    public partial class MapLoader
    {
        /// <summary>
        /// 움직이는 중인 블록 하나의 상태. 변위는 처음 모양 기준의 이동량(m)과 각도(UnityXOPS 오일러, 도)다.
        /// </summary>
        private sealed class BlockMove
        {
            public Block block;
            public Vector3 fromOffset;
            public Vector3 fromAngles;
            public Vector3 toOffset;
            public Vector3 toAngles;
            public int ticks;
            public int elapsed;
            public bool ease;
            // 한 틱 전의 변위. 메시 노드가 틱 사이를 보간하는 데 쓴다.
            public Vector3 previousOffset;
            public Vector3 previousAngles;
        }

        /// <summary>
        /// 움직이는 블록들을 틱마다 옮긴다. 사람(10)보다 먼저 돌아서 사람이 이번 틱의 블록 자리로 충돌한다.
        /// </summary>
        private sealed class BlockMover : ISimTickable
        {
            public int SimOrder => 5;

            public void SimTick()
            {
                Instance.TickBlockMoves();
            }
        }

        private readonly List<MeshInstance3D> m_blockNodes = new List<MeshInstance3D>();
        private readonly List<BlockMove> m_blockMoves = new List<BlockMove>();
        // 처음 상태에서 달라진 블록들 (옮겼거나 껐다). 미션을 다시 시작할 때 이것들만 되돌린다.
        private readonly HashSet<Block> m_changedBlocks = new HashSet<Block>();
        private readonly BlockMover m_blockMover = new BlockMover();

        public override void _Process(double delta)
        {
            if (m_blockMoves.Count == 0) return;

            float alpha = SimClock.InterpolationAlpha;
            foreach (BlockMove move in m_blockMoves)
            {
                PlaceBlockNode(move.block, move.previousOffset.Lerp(move.block.offset, alpha), move.previousAngles.Lerp(move.block.angles, alpha));
            }
        }

        /// <summary>
        /// 블록 하나를 처음 모양 기준의 변위로 옮기고 돌린다. 정해진 시간에 걸쳐 지금 변위에서 새 변위로 간다. 다른 블록과 사람을 무시한다.
        /// 같은 블록에 다시 걸면 누적되지 않고, 그 순간의 변위에서 새 변위로 간다.
        /// </summary>
        /// <param name="index">블록 번호 (파일 안의 순번).</param>
        /// <param name="offset">처음 자리에서의 이동량 (m).</param>
        /// <param name="angles">처음 모양에서의 회전 (UnityXOPS 오일러 x pitch, y yaw, z roll, 도). 블록의 가운데 둘레로 돈다.</param>
        /// <param name="ticks">걸리는 틱 수. 0 이하면 바로 옮긴다.</param>
        /// <param name="ease">true 면 천천히 출발해 천천히 멈춘다.</param>
        /// <returns>시작했으면 true. 없는 블록이거나 올바른 수가 아니면 false.</returns>
        public static bool MoveBlock(int index, Vector3 offset, Vector3 angles, int ticks, bool ease)
        {
            MapLoader loader = Instance;
            if (index < 0 || index >= loader.m_blocks.Count || !offset.IsFinite() || !angles.IsFinite()) return false;

            Block block = loader.m_blocks[index];
            loader.m_blockMoves.RemoveAll(move => move.block == block);
            loader.m_changedBlocks.Add(block);
            if (ticks <= 0)
            {
                loader.ApplyBlockTransform(block, offset, angles);
                loader.PlaceBlockNode(block, offset, angles);
                loader.UpdateBlockMover();
                return true;
            }

            loader.m_blockMoves.Add(new BlockMove
            {
                block = block, fromOffset = block.offset, fromAngles = block.angles, toOffset = offset, toAngles = angles,
                ticks = ticks, ease = ease, previousOffset = block.offset, previousAngles = block.angles,
            });
            loader.UpdateBlockMover();
            return true;
        }

        /// <summary>
        /// 블록 하나를 켜거나 끈다. 끄면 그려지지 않고 어느 판정에도 걸리지 않는다. 켜면 원래의 판정으로 돌아온다.
        /// </summary>
        /// <param name="index">블록 번호 (파일 안의 순번).</param>
        /// <param name="enabled">켤지.</param>
        /// <returns>바꿨으면 true. 없는 블록이면 false.</returns>
        public static bool SetBlockEnabled(int index, bool enabled)
        {
            MapLoader loader = Instance;
            if (index < 0 || index >= loader.m_blocks.Count) return false;

            Block block = loader.m_blocks[index];
            if (block.enabled == enabled) return true;

            block.enabled = enabled;
            block.layerMask = enabled ? block.baseLayerMask : 0;
            loader.m_changedBlocks.Add(block);
            if (loader.m_blockNodes[index] != null) loader.m_blockNodes[index].Visible = enabled;
            loader.RebuildLayerColliders();
            return true;
        }

        /// <summary>
        /// 블록이 움직이는 중인지.
        /// </summary>
        /// <param name="index">블록 번호.</param>
        /// <returns>움직이는 중이면 true.</returns>
        public static bool IsBlockMoving(int index)
        {
            MapLoader loader = Instance;
            return index >= 0 && index < loader.m_blocks.Count && loader.m_blockMoves.Exists(move => move.block == loader.m_blocks[index]);
        }

        /// <summary>
        /// 옮기거나 끈 블록을 전부 처음 상태로 되돌린다. 미션을 다시 시작할 때(포인트 데이터를 내릴 때) 부른다.
        /// </summary>
        public static void ResetBlockMotion()
        {
            MapLoader loader = Instance;
            loader.m_blockMoves.Clear();
            bool toggled = false;
            foreach (Block block in loader.m_changedBlocks)
            {
                loader.ApplyBlockTransform(block, Vector3.Zero, Vector3.Zero);
                loader.PlaceBlockNode(block, Vector3.Zero, Vector3.Zero);
                if (block.enabled) continue;

                block.enabled = true;
                block.layerMask = block.baseLayerMask;
                if (loader.m_blockNodes[block.index] != null) loader.m_blockNodes[block.index].Visible = true;
                toggled = true;
            }
            loader.m_changedBlocks.Clear();
            if (toggled) loader.RebuildLayerColliders();
            loader.UpdateBlockMover();
        }

        /// <summary>
        /// 움직이는 블록들을 한 틱 진행한다.
        /// </summary>
        private void TickBlockMoves()
        {
            for (int i = m_blockMoves.Count - 1; i >= 0; i--)
            {
                BlockMove move = m_blockMoves[i];
                // 도착한 다음 틱에 끝낸다. 그 사이에 메시 노드가 마지막 한 틱을 보간해 따라온다.
                if (move.elapsed >= move.ticks)
                {
                    PlaceBlockNode(move.block, move.block.offset, move.block.angles);
                    m_blockMoves.RemoveAt(i);
                    continue;
                }

                move.previousOffset = move.block.offset;
                move.previousAngles = move.block.angles;
                move.elapsed++;
                float progress = (float)move.elapsed / move.ticks;
                if (move.ease) progress = progress * progress * (3f - 2f * progress);
                // 각도는 축마다 그대로 보간한다. 180° 를 넘는 회전과 여러 바퀴도 적은 대로 돈다.
                ApplyBlockTransform(move.block, move.fromOffset.Lerp(move.toOffset, progress), move.fromAngles.Lerp(move.toAngles, progress));
            }
            UpdateBlockMover();
        }

        /// <summary>
        /// 블록의 판정 정보(면의 법선과 중심, 범위 상자)를 처음 모양에서 주어진 변위만큼 옮기고 돌린 것으로 다시 구한다.
        /// </summary>
        /// <param name="block">블록.</param>
        /// <param name="offset">처음 자리에서의 이동량 (m).</param>
        /// <param name="angles">처음 모양에서의 회전 (UnityXOPS 오일러, 도).</param>
        private void ApplyBlockTransform(Block block, Vector3 offset, Vector3 angles)
        {
            Basis rotation = Basis.FromEuler(Coord.FromUnityEuler(angles));
            Vector3 pivot = block.basePosition;
            block.offset = offset;
            block.angles = angles;
            block.position = pivot + offset;

            for (int f = 0; f < 6; f++)
            {
                block.faceNormals[f] = rotation * block.baseFaceNormals[f];
                block.faceCenters[f] = pivot + rotation * (block.baseFaceCenters[f] - pivot) + offset;
            }

            Vector3 min = pivot + rotation * (block.baseVertices[0] - pivot) + offset;
            Vector3 max = min;
            for (int i = 1; i < 8; i++)
            {
                Vector3 vertex = pivot + rotation * (block.baseVertices[i] - pivot) + offset;
                min = min.Min(vertex);
                max = max.Max(vertex);
            }
            block.boundsMin = min - Vector3.One * k_collisionAddSize;
            block.boundsMax = max + Vector3.One * k_collisionAddSize;
            block.ComputeRayBounds();
        }

        /// <summary>
        /// 블록의 메시 노드를 변위에 맞춰 놓는다. 메시는 블록의 가운데 기준으로 만들어져 있다.
        /// </summary>
        /// <param name="block">블록.</param>
        /// <param name="offset">처음 자리에서의 이동량 (m).</param>
        /// <param name="angles">처음 모양에서의 회전 (UnityXOPS 오일러, 도).</param>
        private void PlaceBlockNode(Block block, Vector3 offset, Vector3 angles)
        {
            MeshInstance3D node = block.index < m_blockNodes.Count ? m_blockNodes[block.index] : null;
            if (node == null) return;

            node.Position = block.basePosition + offset;
            node.Rotation = Coord.FromUnityEuler(angles);
        }

        /// <summary>
        /// 판정별 블록 목록을 블록 번호 순서대로 다시 만든다 (블록을 끄거나 켠 뒤). 순서가 같아야 같은 거리에 맞은 블록들 가운데 고르는 것이 로드 직후와 같다.
        /// </summary>
        private void RebuildLayerColliders()
        {
            foreach (List<Block> colliders in m_layerColliders) colliders.Clear();
            foreach (Block block in m_blocks)
            {
                for (int layer = 0; layer < k_layerCount; layer++)
                {
                    if (block.Collides((BlockLayer)layer)) m_layerColliders[layer].Add(block);
                }
            }
        }

        /// <summary>
        /// 움직이는 블록이 있을 때만 틱에 등록해 둔다.
        /// </summary>
        private void UpdateBlockMover()
        {
            if (m_blockMoves.Count > 0) SimClock.Register(m_blockMover);
            else SimClock.Unregister(m_blockMover);
        }
    }
}
