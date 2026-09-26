using UnityEngine;

public class TrapTrigger : MonoBehaviour
{
    public GameObject playerObject;
    public GameEnding gameEnding;

    private void OnTriggerEnter(Collider other)
    {
        // 玩家可能使用自身 Collider，也可能使用子物体 Collider。
        bool isPlayer = other.gameObject == playerObject
            || other.CompareTag("Player")
            || (playerObject != null && other.transform.IsChildOf(playerObject.transform));

        if (isPlayer)
        {
            if (gameEnding == null)
            {
                gameEnding = FindObjectOfType<GameEnding>();
            }

            if (gameEnding != null)
            {
                gameEnding.CaughtPlayer();
            }
        }
    }
}
