using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public class FindLemon : MonoBehaviour
{
    [Header("拾取效果")]
    public Image pickupImage;
    public float imageDuration = 3f;
    public float speedMultiplier = 1.5f;
    public float speedBoostDuration = 5f;

    [Header("音效")]
    public AudioSource audioSource;
    public AudioClip pickupSound;
    public AudioSource breathingAudioSource;
    public AudioClip breathingClip;

    [Header("加速期间的后处理")]
    public PostProcessVolume fearVolume;
    [Range(0f, 1f)] public float fearVolumeMinWeight = 0.65f;
    [Range(0f, 1f)] public float fearVolumeMaxWeight = 1f;
    public float fearPulseSpeed = 2f;

    [Header("拾取设置")]
    public bool destroyOnPickup = true;

    private Coroutine imageRoutine;
    private bool collected;
    private PlayerMovement cachedPlayer;
    private Collider[] pickupColliders;
    private Coroutine fearEffectRoutine;
    private float originalFearVolumeWeight;

    private void Awake()
    {
        pickupColliders = GetComponentsInChildren<Collider>();
        cachedPlayer = FindObjectOfType<PlayerMovement>();

        // 图片由场景中的 Canvas 提供，开始时保持隐藏且不拦截 UI 点击。
        if (pickupImage != null)
        {
            pickupImage.enabled = false;
            pickupImage.raycastTarget = false;
        }

        if (fearVolume != null)
        {
            originalFearVolumeWeight = fearVolume.weight;
        }
    }

    private void Update()
    {
        // 轮询碰撞体范围作为兜底，不依赖双方 Collider 的 Trigger/Rigidbody 组合。
        if (collected)
        {
            return;
        }

        if (cachedPlayer == null)
        {
            cachedPlayer = FindObjectOfType<PlayerMovement>();
        }

        if (cachedPlayer == null)
        {
            return;
        }

        Collider[] playerColliders = cachedPlayer.GetComponentsInChildren<Collider>();
        foreach (Collider pickupCollider in pickupColliders)
        {
            if (pickupCollider == null || !pickupCollider.enabled)
            {
                continue;
            }

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider != null && playerCollider.enabled && pickupCollider.bounds.Intersects(playerCollider.bounds))
                {
                    Collect(cachedPlayer);
                    return;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryCollect(collision.collider);
    }

    private void TryCollect(Collider other)
    {
        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
        {
            return;
        }

        Collect(playerMovement);
    }

    private void Collect(PlayerMovement playerMovement)
    {
        if (collected)
        {
            return;
        }

        collected = true;
        playerMovement.ApplySpeedBoost(speedMultiplier, speedBoostDuration);
        StartFearEffect();
        ShowPickupImage();
        PlayPickupSound();

        if (destroyOnPickup)
        {
            // 保留脚本运行图片计时协程，只隐藏拾取物模型并禁用碰撞。
            foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>())
            {
                itemRenderer.enabled = false;
            }

            foreach (Collider itemCollider in GetComponentsInChildren<Collider>())
            {
                itemCollider.enabled = false;
            }
        }
    }

    private void StartFearEffect()
    {
        if (fearEffectRoutine != null)
        {
            StopCoroutine(fearEffectRoutine);
        }

        if (fearVolume != null)
        {
            fearVolume.weight = Mathf.Clamp01(fearVolumeMinWeight);
        }
        else
        {
            Debug.LogWarning("FindLemon: 请指定专用的 Fear Volume 和 Post Process Profile。", this);
        }

        if (breathingAudioSource != null && breathingClip != null)
        {
            breathingAudioSource.Stop();
            breathingAudioSource.clip = breathingClip;
            breathingAudioSource.loop = true;
            breathingAudioSource.Play();
        }
        else if (breathingClip != null)
        {
            Debug.LogWarning("FindLemon: 已指定喘息音频，但没有指定独立的 Breathing Audio Source。", this);
        }

        fearEffectRoutine = StartCoroutine(PlayFearEffect());
    }

    private IEnumerator PlayFearEffect()
    {
        float elapsed = 0f;
        while (elapsed < speedBoostDuration)
        {
            elapsed += Time.deltaTime;
            if (fearVolume != null)
            {
                float pulse = (Mathf.Sin(elapsed * fearPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                fearVolume.weight = Mathf.Lerp(fearVolumeMinWeight, fearVolumeMaxWeight, pulse);
            }

            yield return null;
        }

        if (fearVolume != null)
        {
            fearVolume.weight = originalFearVolumeWeight;
        }

        if (breathingAudioSource != null)
        {
            breathingAudioSource.Stop();
            breathingAudioSource.clip = null;
        }

        fearEffectRoutine = null;
    }

    private void ShowPickupImage()
    {
        if (pickupImage == null)
        {
            Debug.LogWarning("FindLemon: 请在 Inspector 中指定 Pickup Image。", this);
            return;
        }

        if (imageRoutine != null)
        {
            StopCoroutine(imageRoutine);
        }

        pickupImage.gameObject.SetActive(true);
        Transform current = pickupImage.transform.parent;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            if (current.GetComponent<Canvas>() != null)
            {
                break;
            }

            current = current.parent;
        }

        pickupImage.enabled = true;
        Color imageColor = pickupImage.color;
        imageColor.a = 1f;
        pickupImage.color = imageColor;
        pickupImage.transform.SetAsLastSibling();

        // 父级 CanvasGroup 可能仍处于隐藏状态，拾取时将其恢复为可见。
        CanvasGroup[] canvasGroups = pickupImage.GetComponentsInParent<CanvasGroup>(true);
        foreach (CanvasGroup canvasGroup in canvasGroups)
        {
            canvasGroup.alpha = 1f;
        }

        imageRoutine = StartCoroutine(HideImageAfterDelay());
    }

    private IEnumerator HideImageAfterDelay()
    {
        yield return new WaitForSeconds(imageDuration);
        if (pickupImage != null)
        {
            pickupImage.enabled = false;
        }

        imageRoutine = null;
    }

    private void PlayPickupSound()
    {
        // 优先使用道具上的 AudioSource；未指定时在道具位置播放一次性音效。
        if (pickupSound == null)
        {
            Debug.LogWarning("FindLemon: 未指定 Pickup Sound，拾取仍会生效但不会播放音效。", this);
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }
        else
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }
    }
}
