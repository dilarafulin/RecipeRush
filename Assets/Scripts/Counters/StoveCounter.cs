using System;
using UnityEngine;

public class StoveCounter : BaseCounter, IHasProgress
{
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;
  

    public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
    public class OnStateChangedEventArgs : EventArgs
    {
        public State state;
    }

    public enum State
    {
        Idle,     // Boş bekliyor
        Frying,   // Pişiyor
        Fried,    // Pişti, alınmayı bekliyor
        Burned    // Yandı
    }

    [SerializeField] private FryingRecipeSO[] fryingRecipeSOArray;
    [SerializeField] private BurningRecipeSO[] burningRecipeSOArray;

    private State state;
    private float fryingTimer;
    private float burningTimer;
    private FryingRecipeSO activeFryingRecipe;
    private BurningRecipeSO activeBurningRecipe;

    private void Start()
    {
        state = State.Idle;
    }

    private void Update()
    {
        if (!HasKitchenObject()) return;

        switch (state)
        {
            case State.Idle:
                break;

            case State.Frying:
                fryingTimer += Time.deltaTime;

                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                {
                    progressNormalized = fryingTimer / activeFryingRecipe.fryingTimerMax
                });

                if (fryingTimer >= activeFryingRecipe.fryingTimerMax)
                {
                    // Pişti → malzemeyi dönüştür
                    GetKitchenObject().DestroySelf();
                    KitchenObject.SpawnKitchenObject(activeFryingRecipe.output, this);

                    // Yanma takibine geç
                    state = State.Fried;
                    burningTimer = 0f;
                    activeBurningRecipe = GetBurningRecipeFor(GetKitchenObject().GetKitchenObjectSO());

                    OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                }
                break;

            case State.Fried:
                burningTimer += Time.deltaTime;

                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                {
                    progressNormalized = burningTimer / activeBurningRecipe.burningTimerMax
                });

                if (burningTimer >= activeBurningRecipe.burningTimerMax)
                {
                    // Yandı
                    GetKitchenObject().DestroySelf();
                    KitchenObject.SpawnKitchenObject(activeBurningRecipe.output, this);

                    state = State.Burned;
                    OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });

                    // Progress bar'ı gizle
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                    {
                        progressNormalized = 0f
                    });
                }
                break;

            case State.Burned:
                break;
        }
    }

    public override void Interact(Player player)
    {
        if (!HasKitchenObject())
        {
            // Tezgah boş — pişirilebilir malzeme varsa koy
            if (player.HasKitchenObject())
            {
                if (HasFryingRecipeFor(player.GetKitchenObject().GetKitchenObjectSO()))
                {
                    player.GetKitchenObject().SetKitchenObjectParent(this);
                    activeFryingRecipe = GetFryingRecipeFor(GetKitchenObject().GetKitchenObjectSO());

                    state = State.Idle;
                    fryingTimer = 0f;

                    OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                    {
                        progressNormalized = 0f
                    });
                }
            }
        }
        else
        {
            // Tezgahta bir şey var — oyuncu boşsa al
            if (!player.HasKitchenObject())
            {
                GetKitchenObject().SetKitchenObjectParent(player);

                state = State.Idle;
                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs
                {
                    progressNormalized = 0f
                });
            } else
            {
                // OYUNCUNUN ELİ DOLU! 
                // Acaba elindeki şey bir Tabak mı?
                if (player.GetKitchenObject() is PlateKitchenObject plateKitchenObject)
                {
                    // Tabaksa, ocaktaki malzemeyi (örneğin pişmiş eti) tabağa eklemeyi dene
                    if (plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO()))
                    {
                        // Tabağa başarıyla eklendi! Ocaktaki eti tamamen yok et.
                        GetKitchenObject().DestroySelf();

                        // KRİTİK NOKTA: Ocağı sıfırla (Ateşi söndür ve UI barı kapat)
                        state = State.Idle;
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
                    }
                }
            }
             
        }
    }

    // F basılı tutunca çalışır — Player.cs'den çağrılır
    public override void InteractAlternate(Player player)
    {
        if (!HasKitchenObject()) return;

        switch (state)
        {
            case State.Idle:
                // Pişirmeyi başlat
                state = State.Frying;
                fryingTimer = 0f;
                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                break;

            case State.Frying:
                // Pişirmeyi durdur
                state = State.Idle;
                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                break;

            case State.Fried:
            case State.Burned:
                // Bu state'lerde F'nin etkisi yok
                break;
        }
    }

    // ── Tarif Arama ───────────────────────────────
    private bool HasFryingRecipeFor(KitchenObjectSO inputSO)
        => GetFryingRecipeFor(inputSO) != null;

    private FryingRecipeSO GetFryingRecipeFor(KitchenObjectSO inputSO)
    {
        foreach (FryingRecipeSO recipe in fryingRecipeSOArray)
            if (recipe.input == inputSO) return recipe;
        return null;
    }

    private BurningRecipeSO GetBurningRecipeFor(KitchenObjectSO inputSO)
    {
        foreach (BurningRecipeSO recipe in burningRecipeSOArray)
            if (recipe.input == inputSO) return recipe;
        return null;
    }

    // Ajanın "Player" olmadan ocağa eti koyabilmesi için
    public void InteractFromAgent(IKitchenObjectParent agent)
    {
        if (!HasKitchenObject() && agent.HasKitchenObject())
        {
            if (HasFryingRecipeFor(agent.GetKitchenObject().GetKitchenObjectSO()))
            {
                agent.GetKitchenObject().SetKitchenObjectParent(this);
                activeFryingRecipe = GetFryingRecipeFor(GetKitchenObject().GetKitchenObjectSO());

                state = State.Idle;
                fryingTimer = 0f;

                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
            }
        }
    }

    // Ajan pişen eti aldığında ocağı sıfırlayıp UI barını kapatmak için
    public void ResetStoveFromAgent()
    {
        state = State.Idle;
        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
    }

    // Ajanın ocağın durumunu (State) uzaktan okuyabilmesi için Getter'lar
    public bool IsIdle() => state == State.Idle;

    // Eğer et yanarsa da ajan onu alabilsin diye Burned durumunu da kabul ediyoruz
    public bool IsFried() => state == State.Fried || state == State.Burned;
}