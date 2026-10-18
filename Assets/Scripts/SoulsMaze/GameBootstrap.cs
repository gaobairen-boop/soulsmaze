using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulsMaze
{
    /// <summary>
    /// 按下 Play 時自動建立整個遊戲（迷宮、玩家、敵人、UI），場景裡不需要手動擺任何東西。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => Spawn();

        static void Spawn()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            new GameObject("SoulsMaze_Game").AddComponent<GameManager>();
        }
    }
}
