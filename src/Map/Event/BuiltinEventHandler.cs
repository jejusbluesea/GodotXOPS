using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 원본 이벤트 10~19 의 처리기. 원본 EventControl::ProcessEventPoint 의 종류별 분기 (event.cpp:288-345).
    /// 상태를 갖지 않아서 하나를 모든 종류와 줄이 함께 쓴다 (시간 대기 카운터는 줄이 갖는다).
    /// </summary>
    public sealed class BuiltinEventHandler : IEventHandler
    {
        // 도착 판정 거리 (m). 원본 DISTANCE_CHECKPOINT 25.0.
        private const float k_arrivalDistance = 2.5f;
        // 시간 대기 이벤트의 1초에 해당하는 틱 수. 원본 (int)GAMEFPS.
        private const int k_ticksPerSecond = (int)SimClock.FrameRate;

        public int Tick(EventManager events, EventLine line, RawPointData point)
        {
            switch ((EventType)point.param0)
            {
                case EventType.MissionComplete:
                    events.EndMission(MissionResult.Complete);
                    return IEventHandler.Wait;

                case EventType.MissionFailed:
                    events.EndMission(MissionResult.Failed);
                    return IEventHandler.Wait;

                case EventType.WaitDeath:
                {
                    // 대상이 없으면 계속 기다린다.
                    Human target = MapLoader.SearchHuman(point.param1);
                    if (target == null || target.Alive) return IEventHandler.Wait;
                    break;
                }

                case EventType.WaitArrival:
                    if (!Arrived(MapLoader.SearchHuman(point.param1), point.position)) return IEventHandler.Wait;
                    break;

                case EventType.ChangeToWalk:
                {
                    // 경로 포인트(랜덤 분기 포함)의 이동 모드를 걷기로 바꾼다 (원본 SetMovePathMode).
                    RawPointData path = MapLoader.GetPathPoint(point.param1);
                    if (path != null) path.param1 = 0;
                    break;
                }

                case EventType.WaitBreakObject:
                {
                    // 대상이 없으면 부서진 것으로 본다.
                    SmallObject target = MapLoader.SearchSmallObject(point.param1);
                    if (target != null && !target.IsDestroyed) return IEventHandler.Wait;
                    break;
                }

                case EventType.WaitCase:
                {
                    Human target = MapLoader.SearchHuman(point.param1);
                    if (!Arrived(target, point.position) || !HasCaseWeapon(target)) return IEventHandler.Wait;
                    break;
                }

                case EventType.WaitTime:
                    if (k_ticksPerSecond * point.param1 > line.WaitCount)
                    {
                        line.WaitCount++;
                        return IEventHandler.Wait;
                    }
                    line.WaitCount = 0;
                    break;

                case EventType.Message:
                    events.ShowMessage(point.param1);
                    break;

                case EventType.ChangeTeam:
                    MapLoader.SearchHuman(point.param1)?.SetTeam(0);
                    break;
            }

            return 0;
        }

        public bool TryGetNext(RawPointData point, int exit, out int next)
        {
            // 원본 이벤트의 출구는 하나이고 P3(param2)이 다음 번호다.
            next = point.param2;
            return exit == 0;
        }

        /// <summary>
        /// 대상이 지점 근처에 있는지 본다. 원본 EventControl::CheckArrival (event.cpp:102-122).
        /// </summary>
        /// <param name="human">대상. null 이면 false.</param>
        /// <param name="position">지점.</param>
        /// <returns>도착 판정 거리 안이면 true.</returns>
        private static bool Arrived(Human human, Vector3 position)
        {
            return human != null && (human.Controller.Position - position).Length() <= k_arrivalDistance;
        }

        /// <summary>
        /// 대상이 케이스(임무 물품) 무기를 어느 슬롯에든 들고 있는지 본다. 원본 EventControl::CheckHaveWeapon (event.cpp:130-164).
        /// </summary>
        /// <param name="human">대상. null 이면 false.</param>
        /// <returns>들고 있으면 true.</returns>
        private static bool HasCaseWeapon(Human human)
        {
            if (human == null) return false;

            List<int> caseIndices = DataManager.Instance.WeaponParameterData.weaponGeneralData.caseWeaponIndex;
            for (int slot = 0; slot < Human.WeaponSlotCount; slot++)
            {
                if (caseIndices.Contains(human.GetWeapon(slot).WeaponIndex)) return true;
            }
            return false;
        }
    }
}
