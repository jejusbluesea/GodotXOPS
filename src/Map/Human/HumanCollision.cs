using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 살아있는 Human 끼리 겹치면 서로 밀어내는 분리 처리.
    /// 원본 OpenXOPS ObjectManager::CollideHuman(objectmanager.cpp:641-669) + 매 프레임 호출 루프(objectmanager.cpp:2926-2937) 포팅.
    /// 두 원기둥의 침투 깊이 절반씩을 양쪽 속도에 정반대로 더한다. 수평만 다루고 시체는 제외한다.
    /// 틱 순서상 이동(10)이 먼저라, 더해진 속도는 다음 틱 이동에서 소비된다(원본의 1프레임 지연과 같다).
    /// </summary>
    public class HumanCollision : ISimTickable
    {
        // 원본 인간간충돌(O10) — AI판단보다 먼저.
        public int SimOrder => 100;

        public void SimTick()
        {
            IReadOnlyList<Human> humans = MapLoader.Humans;
            int count = humans.Count;

            for (int i = 0; i < count; i++)
            {
                Human a = humans[i];
                if (!a.Alive || a.HP <= 0f) continue;
                // 비행 모드(디버그 콘솔의 flight)인 사람은 밀지도 밀리지도 않는다.
                if (a.Controller.Flight) continue;

                HumanController ca = a.Controller;
                Vector3 pa = ca.Position;

                for (int j = i + 1; j < count; j++)
                {
                    Human b = humans[j];
                    if (!b.Alive || b.HP <= 0f) continue;
                    if (b.Controller.Flight) continue;

                    HumanController cb = b.Controller;
                    Vector3 pb = cb.Position;

                    // 수직 겹침 — 두 원기둥 [y, y+키] 구간이 겹칠 때만 충돌.
                    if (pa.Y >= pb.Y + cb.Height || pb.Y >= pa.Y + ca.Height) continue;

                    float dx = pa.X - pb.X;
                    float dz = pa.Z - pb.Z;
                    float distSqr = dx * dx + dz * dz;
                    float minDist = ca.HumanRadius + cb.HumanRadius;
                    if (distSqr >= minDist * minDist) continue;

                    float dist = Mathf.Sqrt(distSqr);

                    // 분리 방향 (b → a). 거의 정확히 겹쳤으면 임의 방향(+X).
                    Vector3 direction = dist > 1e-5f ? new Vector3(dx / dist, 0f, dz / dist) : Vector3.Right;

                    // 각자 침투 깊이의 절반만큼 한 틱에 밀려나도록 속도로 환산한다.
                    float push = (minDist - dist) * 0.5f / SimClock.FrameTime;
                    ca.AddKnockbackVector(direction, push);
                    cb.AddKnockbackVector(-direction, push);
                }
            }
        }
    }
}
