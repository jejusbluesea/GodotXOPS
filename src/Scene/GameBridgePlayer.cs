using Godot;

namespace GodotXOPS
{
    // GameBridge 의 플레이어 담당 partial: HUD 가 읽는 플레이어 값과 3D 무기 표시.
    public partial class GameBridge
    {
        private HudWeaponView m_weaponView;

        private static Human Player
        {
            get
            {
                Human player = MapLoader.Loaded ? MapLoader.Player : null;
                return player != null && IsInstanceValid(player) ? player : null;
            }
        }

        /// <summary>
        /// 조작하는 사람이 있는지.
        /// </summary>
        /// <returns>있으면 true.</returns>
        public bool PlayerExists()
        {
            return Player != null;
        }

        /// <summary>
        /// 플레이어가 살아 있는지.
        /// </summary>
        /// <returns>살아 있으면 true. 플레이어가 없으면 false.</returns>
        public bool PlayerAlive()
        {
            Human player = Player;
            return player != null && player.Alive;
        }

        /// <summary>
        /// 플레이어의 사람 목록 순번. 조작 대상이 바뀌었는지 알아볼 때 쓴다.
        /// </summary>
        /// <returns>순번. 플레이어가 없으면 −1.</returns>
        public int PlayerIndex()
        {
            return Player != null ? MapLoader.PlayerIndex : -1;
        }

        /// <summary>
        /// 플레이어의 표시 위치 (틱 사이를 보간한 발 위치).
        /// </summary>
        /// <returns>위치 (UnityXOPS 공간). 플레이어가 없으면 원점.</returns>
        public Vector3 PlayerPosition()
        {
            Human player = Player;
            return player != null ? Coord.FromUnity(player.Controller.VisualPosition) : Vector3.Zero;
        }

        /// <summary>
        /// 플레이어의 HP.
        /// </summary>
        /// <returns>HP. 플레이어가 없으면 0.</returns>
        public float PlayerHP()
        {
            Human player = Player;
            return player != null ? player.HP : 0f;
        }

        /// <summary>
        /// 든 무기의 장전된 탄 수.
        /// </summary>
        /// <returns>탄 수. 플레이어가 없으면 0.</returns>
        public int Magazine()
        {
            Human player = Player;
            return player != null ? player.CurrentWeapon.Magazine : 0;
        }

        /// <summary>
        /// 든 무기의 예비 탄 수.
        /// </summary>
        /// <returns>탄 수. 플레이어가 없으면 0.</returns>
        public int Reserve()
        {
            Human player = Player;
            return player != null ? player.CurrentWeapon.Reserve : 0;
        }

        /// <summary>
        /// 든 무기의 이름.
        /// </summary>
        /// <returns>이름. 플레이어가 없으면 빈 문자열.</returns>
        public string WeaponName()
        {
            Human player = Player;
            return player != null ? player.CurrentWeapon.Data.name ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// 재장전 중인지.
        /// </summary>
        /// <returns>재장전 중이면 true.</returns>
        public bool IsReloading()
        {
            Human player = Player;
            return player != null && player.IsReloading;
        }

        /// <summary>
        /// 무기를 바꾸는 중인지 (슬롯 전환, 종류 전환).
        /// </summary>
        /// <returns>바꾸는 중이면 true.</returns>
        public bool IsSwitchingWeapon()
        {
            Human player = Player;
            return player != null && player.IsSwitchingWeapon;
        }

        /// <summary>
        /// 든 무기가 조준선을 표시하는 무기인지. 죽었으면 false 다. 스코프가 조준선을 숨기는지는 ActiveScope 의 hideCrosshair 로 따로 본다.
        /// </summary>
        /// <returns>조준선을 표시해야 하면 true.</returns>
        public bool ShowsCrosshair()
        {
            Human player = Player;
            return player != null && player.Alive && player.CurrentWeapon.Data.crosshair;
        }

        /// <summary>
        /// 지금의 조준 오차 (이동·점프·저체력 + 반동). 조준선이 벌어지는 양이며 640×480 기준 1 이 1 픽셀이다 (원본 gamemain.cpp:3195-3198).
        /// </summary>
        /// <returns>조준 오차. 플레이어가 없으면 0.</returns>
        public int ErrorRange()
        {
            Human player = Player;
            return player != null ? player.GunsightErrorRange : 0;
        }

        /// <summary>
        /// 스코프를 쓰는 중인지.
        /// </summary>
        /// <returns>쓰는 중이면 true.</returns>
        public bool IsScoping()
        {
            Human player = Player;
            return player != null && player.IsScoping;
        }

        /// <summary>
        /// 1인칭 시점인지. 스코프 화면과 조준선은 1인칭에서만 그린다.
        /// </summary>
        /// <returns>1인칭이면 true. 조작 중이 아니면 false.</returns>
        public bool IsFirstPerson()
        {
            PlayerController controller = PlayerController.Current;
            return controller != null && controller.FirstPersonView;
        }

        /// <summary>
        /// 든 무기의 스코프 번호.
        /// </summary>
        /// <returns>스코프 번호. 플레이어가 없으면 −1.</returns>
        public int ScopeIndex()
        {
            Human player = Player;
            return player != null ? player.CurrentWeapon.Data.scopeIndex : -1;
        }

        /// <summary>
        /// 쓰고 있는 스코프의 표시 정보.
        /// 조준선 좌표는 화면 가운데가 원점이고 화면 높이가 480 인 기준이며 위쪽이 + 다.
        /// </summary>
        /// <returns>aspect, texturePath, hideCrosshair, lines(각각 x1, y1, x2, y2, color, width)를 담은 사전. 스코프를 쓰지 않으면 빈 사전.</returns>
        public Godot.Collections.Dictionary ActiveScope()
        {
            var result = new Godot.Collections.Dictionary();
            ScopeData scope = Player?.ActiveScope;
            if (scope == null) return result;

            var lines = new Godot.Collections.Array();
            if (scope.lines != null)
            {
                foreach (ScopeLine line in scope.lines)
                {
                    lines.Add(new Godot.Collections.Dictionary
                    {
                        { "x1", line.start.X },
                        { "y1", line.start.Y },
                        { "x2", line.end.X },
                        { "y2", line.end.Y },
                        { "color", line.color },
                        { "width", line.width },
                    });
                }
            }

            result["aspect"] = scope.textureAspect;
            result["texturePath"] = scope.texturePath ?? string.Empty;
            result["hideCrosshair"] = scope.hideCrosshair;
            result["lines"] = lines;
            return result;
        }

        /// <summary>
        /// 플레이어가 마지막 확인 이후 맞았는지 확인하고 표시를 지운다. 피격 번쩍임용이다.
        /// </summary>
        /// <returns>맞았으면 true.</returns>
        public bool ConsumeHit()
        {
            Human player = Player;
            return player != null && player.ConsumeHit(out _);
        }

        /// <summary>
        /// 3D 무기 표시를 만든다. 이미 있으면 그것의 텍스처를 돌려준다.
        /// </summary>
        /// <param name="size">정사각형 텍스처의 한 변 (픽셀).</param>
        /// <returns>무기 표시가 그려지는 텍스처.</returns>
        public Texture2D CreateWeaponView(int size)
        {
            if (m_weaponView == null)
            {
                m_weaponView = new HudWeaponView { Name = "WeaponView", Size = new Vector2I(size, size) };
                AddChild(m_weaponView);
            }
            return m_weaponView.GetTexture();
        }

        /// <summary>
        /// 3D 무기 표시를 없앤다. 메인게임 화면이 나갈 때 부른다.
        /// </summary>
        public void FreeWeaponView()
        {
            if (m_weaponView == null) return;

            RemoveChild(m_weaponView);
            m_weaponView.Free();
            m_weaponView = null;
        }

        /// <summary>
        /// 든 무기 자리를 놓는다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="scale">크기 배율.</param>
        /// <param name="yawDeg">Y 축 회전 (도).</param>
        public void SetWeaponViewMain(Vector3 position, float scale, float yawDeg)
        {
            m_weaponView?.SetMainSlot(position, scale, yawDeg);
        }

        /// <summary>
        /// 멘 무기 자리를 놓는다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="scale">크기 배율.</param>
        /// <param name="yawDeg">Y 축 회전 (도).</param>
        public void SetWeaponViewSub(Vector3 position, float scale, float yawDeg)
        {
            m_weaponView?.SetSubSlot(position, scale, yawDeg);
        }

        /// <summary>
        /// 무기를 비추는 카메라의 구도를 정한다.
        /// </summary>
        /// <param name="position">위치 (UnityXOPS 공간).</param>
        /// <param name="euler">회전 (UnityXOPS 오일러 각, 도).</param>
        /// <param name="fov">세로 시야각 (도).</param>
        public void SetWeaponViewCamera(Vector3 position, Vector3 euler, float fov)
        {
            m_weaponView?.SetViewCamera(position, euler, fov);
        }
    }
}
