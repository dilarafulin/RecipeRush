using System;
using UnityEngine;

public class CuttingCounter : BaseCounter, IHasProgress
{
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;

    [SerializeField] private CuttingRecipeSO[] cuttingRecipeSOArray; // Desteklenen tüm tarifler

    private int cuttingProgress;

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

    //kesme,f tusu
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
    public override void InteractFromAgent(SousChefAgent agent)
    {
        if (agent == null) return;

        if (!HasKitchenObject()) // 1. DURUM: Kesme tahtası boşsa
        {
            // Tahta boş, ajanın da eli boşsa yapacak bir şey yok
            if (!agent.HasKitchenObject()) return;

            KitchenObject agentObj = agent.GetKitchenObject();
            if (HasRecipeWithInput(agentObj.GetKitchenObjectSO()))
            {
                agentObj.SetKitchenObjectParent(this);
                cuttingProgress = 0;

                CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                {
                    progressNormalized = (float)cuttingProgress / cuttingRecipeSO.cuttingProgressMax
                });
            }
        }
        else // 2. DURUM: Kesme tahtasında malzeme varsa
        {
            if (agent.HasKitchenObject()) // Ajanın eli doluysa (Tabak getirdiyse)
            {
                if (agent.GetKitchenObject() is PlateKitchenObject plateKitchenObject)
                {
                    KitchenObject counterIngredient = GetKitchenObject();
                    if (plateKitchenObject.TryAddIngredient(counterIngredient.GetKitchenObjectSO()))
                    {
                        counterIngredient.DestroySelf();
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
                    }
                }
            }
            else // AJANIN ELİ BOŞSA (İşte ekmeği geri alma kısmı burası!)
            {
                GetKitchenObject().SetKitchenObjectParent(agent);
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
            }
        }
    }

    public override SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        if (HasKitchenObject() && !agent.HasKitchenObject())
        {
            // Tezgahtaki eşyanın kesilme tarifi yoksa, demek ki zaten kesilmiştir
            bool isItemFullyCut = !HasRecipeWithInput(GetKitchenObject().GetKitchenObjectSO());

            if (isItemFullyCut)
            {
                // Kesme bitmiş, ajana "Malzemeyi Eline Al" komutu ver
                return new SousChefTask(SousChefCommand.FetchIngredient, this);
            }
            else
            {
                // Malzemenin hala tarifi var (kesilmemiş). Ajana "Kesmeye Devam Et" komutu ver
                return new SousChefTask(SousChefCommand.ChopIngredient, this);
            }
        }

        // 2. DURUM: Tahta boş ve ajanın elinde malzeme var
        if (!HasKitchenObject() && agent.HasKitchenObject())
        {
            // Ajanın elindeki malzemenin kesilme tarifi var mı? (Örn: Elinde tabak varsa tahtaya koymasın)
            if (HasRecipeWithInput(agent.GetKitchenObject().GetKitchenObjectSO()))
            {
                // Kesilebilir bir malzeme, tahtaya "Bırak" komutu ver
                return new SousChefTask(SousChefCommand.DeliverToCounter, this);
            }
        }

        return null; // Hiçbir şarta uymuyorsa görev verme
    }

    // Dışarıdaki scriptlerin (Agent'ın) malzemenin tamamen kesilip kesilmediğini öğrenmesi için
    public bool IsFullyCut()
    {
        if (!HasKitchenObject()) return false;

        // Eğer malzemenin başka bir kesilme tarifi yoksa (bütün değilse) tamamen kesilmiştir.
        return !HasRecipeWithInput(GetKitchenObject().GetKitchenObjectSO());
    }
}