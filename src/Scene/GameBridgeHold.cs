using Godot;

namespace GodotXOPS
{
    public partial class GameBridge
    {
        // 트리에서 떼어 맡아 둔 씬 (에디터). 플레이 테스트가 끝나면 그대로 돌려놓는다. 없으면 null.
        private Node m_heldScene;

        public override void _Notification(int what)
        {
            // 맡아 둔 씬이 있을 때의 창 닫기는 끝내기가 아니라 그 씬으로 돌아가기다 (저장하지 않은 내용을 그 씬이 묻는다).
            if (what == NotificationWMCloseRequest && m_heldScene != null) ReturnToHeldScene();
        }

        public override void _ExitTree()
        {
            if (m_heldScene != null && IsInstanceValid(m_heldScene)) m_heldScene.Free();
            m_heldScene = null;
        }

        /// <summary>
        /// 맡아 둔 씬이 있는지. 있으면 메인게임은 나갈 때 메뉴나 결과 화면이 아니라 그 씬으로 돌아간다 (에디터의 플레이 테스트).
        /// </summary>
        /// <returns>있으면 true.</returns>
        public bool HasHeldScene()
        {
            return m_heldScene != null;
        }

        /// <summary>
        /// 지금 씬을 지우지 않고 트리에서 떼어 맡아 둔 채 다른 화면으로 바꾼다. 맡긴 씬은 상태(편집 내용, 되돌리기 기록)를 그대로 갖고 있다가 ReturnToHeldScene 에서 돌아온다.
        /// 실제 전환은 이번 프레임이 끝난 뒤에 일어난다.
        /// </summary>
        /// <param name="scene">맡길 씬. 지금 씬이어야 한다.</param>
        /// <param name="sceneName">대신 띄울 씬 이름 (scenes 폴더 기준, 확장자 없이).</param>
        public void HoldSceneAndChange(Node scene, string sceneName)
        {
            m_heldScene = scene;
            Callable.From(() =>
            {
                SceneTree tree = GetTree();
                tree.Root.RemoveChild(scene);
                tree.CurrentScene = null;
                tree.ChangeSceneToFile(string.Format(k_scenePathFormat, sceneName));
            }).CallDeferred();
        }

        /// <summary>
        /// 지금 화면을 지우고 미션을 내린 뒤 맡아 둔 씬을 돌려놓는다. 실제 전환은 이번 프레임이 끝난 뒤에 일어난다. 맡아 둔 씬이 없으면 아무것도 하지 않는다.
        /// </summary>
        public void ReturnToHeldScene()
        {
            if (m_heldScene == null) return;

            Node held = m_heldScene;
            m_heldScene = null;
            Callable.From(() =>
            {
                SceneTree tree = GetTree();
                Node current = tree.CurrentScene;
                if (current != null)
                {
                    tree.Root.RemoveChild(current);
                    current.Free();
                }
                UnloadMission();
                tree.Root.AddChild(held);
                tree.CurrentScene = held;
            }).CallDeferred();
        }
    }
}
