using UnityEngine;

public class EscapeToQuit : MonoBehaviour
{
    private void Update()
    {
        // 按下 Escape 时退出游戏；在 Unity 编辑器中则停止 Play Mode。
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
