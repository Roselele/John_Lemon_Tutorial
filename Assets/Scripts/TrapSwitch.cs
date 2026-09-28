using UnityEngine;
using UnityEngine.UI;

public class TrapSwitch : MonoBehaviour
{
    [Header("交互引用")]
    public SequenceController targetSequence;
    public Text interactionPrompt;

    [Header("开关设置")]
    public float rotationAngle = 180f;
    public string promptMessage = "Press X to turn off TRAP";

    private PlayerMovement playerInRange;
    private bool hasBeenUsed;
    private Quaternion initialLocalRotation;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
        SetPromptVisible(false);
        if (interactionPrompt != null)
        {
            interactionPrompt.text = promptMessage;
        }
    }

    private void Update()
    {
        if (hasBeenUsed || playerInRange == null || !Input.GetKeyDown(KeyCode.X))
        {
            return;
        }

        hasBeenUsed = true;
        transform.localRotation = initialLocalRotation * Quaternion.Euler(0f, rotationAngle, 0f);
        SetPromptVisible(false);

        if (targetSequence != null)
        {
            targetSequence.DisableTrapSequence();
        }
        else
        {
            Debug.LogWarning("TrapSwitch: 请在 Inspector 中指定要关闭的 SequenceController。", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null || hasBeenUsed)
        {
            return;
        }

        playerInRange = playerMovement;
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null || playerMovement != playerInRange)
        {
            return;
        }

        playerInRange = null;
        SetPromptVisible(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.gameObject.SetActive(visible);
        }
    }
}
