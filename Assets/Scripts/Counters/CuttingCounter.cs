using System;
using UnityEngine;

public class CuttingCounter : BaseCounter, IHasProgress
{
    // Progress Bar (UI) için event
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;

    [SerializeField] private CuttingRecipeSO[] cuttingRecipeSOArray; // Desteklenen tüm tarifler

    private int cuttingProgress;

    // E Tuşu - Eşya Koyma / Alma
    public override void Interact(Player player)
    {
        if (!HasKitchenObject()) // Tezgah boş
        {
            if (player.HasKitchenObject()) // Oyuncuda eşya var
            {
                // Sadece kesilebilir bir şeyse koymasına izin ver
                if (HasRecipeWithInput(player.GetKitchenObject().GetKitchenObjectSO()))
                {
                    player.GetKitchenObject().SetKitchenObjectParent(this);
                    cuttingProgress = 0; // İlerlemeyi sıfırla

                    CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                    {
                        progressNormalized = (float)cuttingProgress / cuttingRecipeSO.cuttingProgressMax
                    });
                }
            }
        }
        else // Tezgahta eşya var
        {
            if (!player.HasKitchenObject()) // Oyuncu boş
            {
                GetKitchenObject().SetKitchenObjectParent(player);

                // Oyuncu eşyayı alınca barı sıfırla/kapat
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
            }
            else
            {
                // OYUNCUNUN ELİ DOLU!
                // Elindeki şey tabak mı?
                if (player.GetKitchenObject() is PlateKitchenObject plateKitchenObject)
                {
                    // Tabaksa, tahtadaki malzemeyi tabağa eklemeyi dene
                    if (plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO()))
                    {
                        // Tabağa başarıyla eklendi! Tahtadaki malzemeyi yok et.
                        GetKitchenObject().DestroySelf();

                        // Kesme tahtasının Progress Bar'ını sıfırla/gizle
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
                    }
                }
            }
        }
    }

    // F Tuşu - Kesme İşlemi
    public override void InteractAlternate(Player player)
    {
        if (HasKitchenObject() && HasRecipeWithInput(GetKitchenObject().GetKitchenObjectSO()))
        {
            // Eşya var ve kesilebilir bir eşya. Kesme işlemi başlar.
            cuttingProgress++;

            CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());

            // UI'a ilerlemeyi bildir
            OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
            {
                progressNormalized = (float)cuttingProgress / cuttingRecipeSO.cuttingProgressMax
            });

            // Kesme işlemi bitti mi?
            if (cuttingProgress >= cuttingRecipeSO.cuttingProgressMax)
            {
                KitchenObjectSO outputKitchenObjectSO = GetOutputForInput(GetKitchenObject().GetKitchenObjectSO());

                // Eski bütün eşyayı yok et
                GetKitchenObject().DestroySelf();

                // Yeni kesilmiş eşyayı spawn et ve tezgaha koy
                KitchenObject.SpawnKitchenObject(outputKitchenObjectSO, this);
            }
        }
    }

    // --- YARDIMCI FONKSİYONLAR ---

    // Elimizdeki malzemeyle eşleşen bir tarif var mı?
    private bool HasRecipeWithInput(KitchenObjectSO inputKitchenObjectSO)
    {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(inputKitchenObjectSO);
        return cuttingRecipeSO != null;
    }

    // Tarif listesinden dönüşecek eşyayı bul
    private KitchenObjectSO GetOutputForInput(KitchenObjectSO inputKitchenObjectSO)
    {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(inputKitchenObjectSO);
        if (cuttingRecipeSO != null)
        {
            return cuttingRecipeSO.output;
        }
        return null;
    }

    // Girdiğimiz malzemeye ait doğru tarifi bul
    private CuttingRecipeSO GetCuttingRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO)
    {
        foreach (CuttingRecipeSO cuttingRecipeSO in cuttingRecipeSOArray)
        {
            if (cuttingRecipeSO.input == inputKitchenObjectSO)
            {
                return cuttingRecipeSO;
            }
        }
        return null;
    }
    // ── YAPAY ZEKA (AJAN) İÇİN YARDIMCI METOTLAR ─────────────────

    public void InteractFromAgent(IKitchenObjectParent agent)
    {
        if (!HasKitchenObject()) // Tezgah boşsa ajan koysun
        {
            if (agent.HasKitchenObject() && HasRecipeWithInput(agent.GetKitchenObject().GetKitchenObjectSO()))
            {
                agent.GetKitchenObject().SetKitchenObjectParent(this);
                cuttingProgress = 0;

                CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                {
                    progressNormalized = (float)cuttingProgress / cuttingRecipeSO.cuttingProgressMax
                });
            }
        }
        else // Tezgah doluysa ajan (kesilmiş malzemeyi) alsın
        {
            if (!agent.HasKitchenObject())
            {
                GetKitchenObject().SetKitchenObjectParent(agent);
                // Barı sıfırla/kapat
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
            }
        }
    }
}