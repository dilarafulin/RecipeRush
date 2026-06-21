using UnityEngine;

// Player veya SousChefAgent (IMovable) üstüne ekle: yürürken periyodik ayak sesi çalar.
public class FootstepSounds : MonoBehaviour
{
    [SerializeField] private float footstepInterval = 0.3f; // adım aralığı (sn)
    [SerializeField] private float volume = 1f;

    private IMovable movable;
    private float timer;

    private void Awake()
    {
        movable = GetComponent<IMovable>();
        if (movable == null)
            Debug.LogError("[FootstepSounds] IMovable bulunamadı — Player/Agent üstünde olmalı.");
    }

    private void Update()
    {
        if (movable == null || SoundManager.Instance == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = footstepInterval;
            if (movable.IsWalking())
                SoundManager.Instance.PlayFootstepSound(transform.position, volume);
        }
    }
}
