using UnityEngine;

// Her ocağın (StoveCounter) üstüne ekle: pişerken döngü "cızırtı" sesi çalar,
// yemek yanmaya yaklaşınca periyodik uyarı sesi verir.
[RequireComponent(typeof(StoveCounter))]
public class StoveCounterSound : MonoBehaviour
{
    [SerializeField] private AudioClip sizzleClip;          // döngüsel cızırtı klibi
    [SerializeField] private float warningThreshold = 0.5f; // yanma ilerlemesi bu oranı geçince uyarı
    [SerializeField] private float warningInterval = 0.2f;  // uyarı bip aralığı (sn)

    private StoveCounter stoveCounter;
    private AudioSource audioSource;
    private float warningTimer;
    private bool inFriedState; // Fried = yanmaya doğru sayan durum

    private void Awake()
    {
        stoveCounter = GetComponent<StoveCounter>();

        // Döngüsel cızırtı için kendi AudioSource'unu kur (PlayClipAtPoint döngü desteklemez)
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = sizzleClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D konumsal ses
    }

    private void Start()
    {
        stoveCounter.OnStateChanged += StoveCounter_OnStateChanged;
        stoveCounter.OnProgressChanged += StoveCounter_OnProgressChanged;
    }

    private void StoveCounter_OnStateChanged(object sender, StoveCounter.OnStateChangedEventArgs e)
    {
        // Pişerken/kızarırken cızırtı çalsın; idle veya yanık olunca dursun
        bool shouldSizzle = e.state == StoveCounter.State.Frying
                         || e.state == StoveCounter.State.Fried;

        if (shouldSizzle && sizzleClip != null && !audioSource.isPlaying) audioSource.Play();
        else if (!shouldSizzle && audioSource.isPlaying) audioSource.Stop();

        inFriedState = e.state == StoveCounter.State.Fried;
        warningTimer = 0f;
    }

    private void StoveCounter_OnProgressChanged(object sender, IHasProgress.OnProgressChangedEventArgs e)
    {
        // Sadece Fried durumunda ve yanma ilerlemesi eşiği geçtiyse uyarı bip'i
        if (!inFriedState || e.progressNormalized < warningThreshold) return;

        warningTimer -= Time.deltaTime;
        if (warningTimer <= 0f)
        {
            warningTimer = warningInterval;
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayWarningSound(transform.position);
        }
    }
}
