using System;
using UnityEngine;

public class KitchenObject : MonoBehaviour
{
    // Ses için statik olaylar: herhangi bir nesne alındığında / tezgaha bırakıldığında
    // tetiklenir. sender = bu KitchenObject (konum için transform'undan okunur).
    public static event EventHandler OnAnyObjectPickedUp; // ele alındı (oyuncu/ajan)
    public static event EventHandler OnAnyObjectDropped;   // bir tezgaha bırakıldı

    [SerializeField] private KitchenObjectSO kitchenObjectSO;

    private IKitchenObjectParent kitchenObjectParent;

    public KitchenObjectSO GetKitchenObjectSO()
    {
        return kitchenObjectSO;
    }

    // Ebeveyni değiştir (tezgahtan ele, elden tezgaha vs.)
    public void SetKitchenObjectParent(IKitchenObjectParent parent)
    {
        // 1. Eski ebeveynden temizle
        if (kitchenObjectParent != null)
        {
            kitchenObjectParent.ClearKitchenObject();
        }

        kitchenObjectParent = parent;

        // 2. Yeni ebeveyn zaten bir şey tutuyorsa hata ver
        if (parent.HasKitchenObject())
        {
            Debug.LogError("Ebeveynin zaten bir KitchenObject'i var!");
        }

        parent.SetKitchenObject(this);

        // 3. Modeli ebeveynin üstüne taşı
        transform.parent = parent.GetKitchenObjectFollowTransform();
        transform.localPosition = Vector3.zero;

        // ── ML-AGENTS VE FİZİK KORUMA KİLİDİ (YENİ) ──
        // Obje ele alındığı an sahnede bir engel oluşturmaması için Collider'ını kapatıyoruz
        if (TryGetComponent<Collider>(out Collider col))
        {
            col.enabled = false;
        }

        // Eğer objede Rigidbody varsa, yerçekimiyle düşmesin veya ajanı itmesin diye Kinematic yapıyoruz
        if (TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        // Ses: yeni ebeveyn bir tezgahsa "bırakma", değilse (oyuncu/ajan eli) "alma"
        if (parent is BaseCounter)
            OnAnyObjectDropped?.Invoke(this, EventArgs.Empty);
        else
            OnAnyObjectPickedUp?.Invoke(this, EventArgs.Empty);
    }

    public IKitchenObjectParent GetKitchenObjectParent()
    {
        return kitchenObjectParent;
    }

    // Malzemeyi sahnede spawn et
    public static KitchenObject SpawnKitchenObject(KitchenObjectSO so, IKitchenObjectParent parent)
    {
        Transform kitchenObjectTransform = Instantiate(so.prefab);
        KitchenObject kitchenObject = kitchenObjectTransform.GetComponent<KitchenObject>();
        kitchenObject.SetKitchenObjectParent(parent);
        return kitchenObject;
    }

    // Malzemeyi yok et
    public void DestroySelf()
    {
        kitchenObjectParent.ClearKitchenObject();
        Destroy(gameObject);
    }
}