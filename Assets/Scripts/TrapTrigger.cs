using UnityEngine;

public class TrapTrigger : MonoBehaviour
{
    public GameObject playerObject;
    public GameEnding gameEnding;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == playerObject || other.CompareTag("Player"))
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
