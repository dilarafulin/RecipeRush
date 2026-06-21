using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioClipRefsSO audioClipRefsSO;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        DeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSuccess;
        DeliveryManager.Instance.OnRecipeFailed += DeliveryManager_OnRecipeFailed;

        CuttingCounter.OnAnyCut += CuttingCounter_OnAnyCut;
        KitchenObject.OnAnyObjectPickedUp += KitchenObject_OnAnyObjectPickedUp;
        KitchenObject.OnAnyObjectDropped += KitchenObject_OnAnyObjectDropped;
        TrashCounter.OnAnyObjectTrashed += TrashCounter_OnAnyObjectTrashed;
    }

    private void OnDestroy()
    {
        CuttingCounter.OnAnyCut -= CuttingCounter_OnAnyCut;
        KitchenObject.OnAnyObjectPickedUp -= KitchenObject_OnAnyObjectPickedUp;
        KitchenObject.OnAnyObjectDropped -= KitchenObject_OnAnyObjectDropped;
        TrashCounter.OnAnyObjectTrashed -= TrashCounter_OnAnyObjectTrashed;
    }

    private void DeliveryManager_OnRecipeFailed(object sender, System.EventArgs e)
    {
        DeliveryCounter deliveryCounter = DeliveryCounter.Instance;
        PlaySound(audioClipRefsSO.deliveryFail, deliveryCounter.transform.position);
    }

    private void DeliveryManager_OnRecipeSuccess(object sender, System.EventArgs e)
    {
        DeliveryCounter deliveryCounter = DeliveryCounter.Instance;
        PlaySound(audioClipRefsSO.deliverySuccess, deliveryCounter.transform.position);
    }

    private void CuttingCounter_OnAnyCut(object sender, System.EventArgs e)
    {
        CuttingCounter cuttingCounter = sender as CuttingCounter;
        if (cuttingCounter != null)
            PlaySound(audioClipRefsSO.chop, cuttingCounter.transform.position);
    }

    private void KitchenObject_OnAnyObjectPickedUp(object sender, System.EventArgs e)
    {
        KitchenObject ko = sender as KitchenObject;
        if (ko != null)
            PlaySound(audioClipRefsSO.objectPickup, ko.transform.position);
    }

    private void KitchenObject_OnAnyObjectDropped(object sender, System.EventArgs e)
    {
        KitchenObject ko = sender as KitchenObject;
        if (ko != null)
            PlaySound(audioClipRefsSO.objectDrop, ko.transform.position);
    }

    private void TrashCounter_OnAnyObjectTrashed(object sender, System.EventArgs e)
    {
        TrashCounter trashCounter = sender as TrashCounter;
        if (trashCounter != null)
            PlaySound(audioClipRefsSO.trash, trashCounter.transform.position);
    }

    // ── Dış bileşenlerin çağırdığı sesler (FootstepSounds, StoveCounterSound) ──
    public void PlayFootstepSound(Vector3 position, float volume = 1f)
    {
        PlaySound(audioClipRefsSO.footstep, position, volume);
    }

    public void PlayWarningSound(Vector3 position, float volume = 1f)
    {
        PlaySound(audioClipRefsSO.warning, position, volume);
    }

    // ── Yardımcılar ──
    private void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volume = 1f)
    {
        if (audioClipArray == null || audioClipArray.Length == 0) return;
        PlaySound(audioClipArray[Random.Range(0, audioClipArray.Length)], position, volume);
    }

    private void PlaySound(AudioClip audioClip, Vector3 position, float volume = 1f)
    {
        if (audioClip == null) return;
        AudioSource.PlayClipAtPoint(audioClip, position, volume);
    }
}
