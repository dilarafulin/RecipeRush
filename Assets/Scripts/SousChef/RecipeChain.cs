using System.Collections.Generic;
using UnityEngine;

public class RecipeChain : SousChefChainBase
{
    [Header("Tarif Veritabanı")]
    [SerializeField] private CuttingRecipeSO[] cuttingRecipeSOArray;
    [SerializeField] private FryingRecipeSO[] fryingRecipeSOArray;

    private RecipeSO activeRecipe;
    private readonly List<PlannedStep> plan = new List<PlannedStep>();

    // Tabağın beklediği tezgah — park adımında seçilir, sonraki adımlar aynı tezgahı kullanır
    private ClearCounter plateParkCounter;

    private struct PlannedStep
    {
        public SousChefCommand command;
        // Hedef tezgah adım ÇALIŞIRKEN çözülür; plan kurulurken sahne durumu farklı olabilir
        public System.Func<BaseCounter> resolveTarget;
        public string label;
        public KitchenObjectSO relatedFinalSO;
    }

    [Header("Test")]
    [SerializeField] private bool debugHotkeyEnabled = true; // R: bekleyen ilk siparişi başlat

    private void Update()
    {
        if (!debugHotkeyEnabled) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame && !IsRunning())
            StartNextWaitingRecipe();
    }

    private void Add(SousChefCommand cmd, System.Func<BaseCounter> resolver, string label, KitchenObjectSO finalSO = null)
    {
        plan.Add(new PlannedStep { command = cmd, resolveTarget = resolver, label = label, relatedFinalSO = finalSO });
    }

    public void SetRecipe(RecipeSO recipe) => activeRecipe = recipe;

    // DeliveryManager'da bekleyen ilk siparişi al ve zinciri başlat
    public bool StartNextWaitingRecipe()
    {
        List<RecipeSO> waiting = DeliveryManager.Instance.GetWaitingRecipeSOList();
        if (waiting.Count == 0)
        {
            Debug.LogWarning("[RecipeChain] Bekleyen sipariş yok.");
            return false;
        }
        SetRecipe(waiting[0]);
        SousChefTaskManager.Instance.StartChain(this);
        return true;
    }

    public override void StartChain()
    {
        if (activeRecipe == null)
        {
            Debug.LogError("[RecipeChain] Tarif atanmadı! Önce SetRecipe çağır.");
            return;
        }
        if (!BuildPlan())
        {
            Debug.LogError($"[RecipeChain] '{activeRecipe.recipeName}' için plan kurulamadı.");
            Cancel();
            return;
        }
        Debug.Log($"[RecipeChain]  '{activeRecipe.recipeName}' planı: {plan.Count} adım");
        base.StartChain();
    }

    public override void ExecuteStep(int step)
    {
        if (step >= plan.Count)
        {
            Debug.Log($"[RecipeChain]  Tarif tamamlandı: {activeRecipe.recipeName}");
            Cancel();
            taskManager.OnChainCompleted();
            return;
        }

        PlannedStep s = plan[step];

        // ─── ÖN KONTROL (LOOK-AHEAD) SİSTEMİ ───
        if (s.relatedFinalSO != null && plateParkCounter != null && plateParkCounter.HasKitchenObject())
        {
            if (plateParkCounter.GetKitchenObject() is PlateKitchenObject plate)
            {
                if (plate.GetKitchenObjectSOList().Contains(s.relatedFinalSO))
                {
                    Debug.Log($"[RecipeChain]  ÖN KONTROL: {s.relatedFinalSO.name} zaten tabakta! Adımlar ayıklanıyor.");

                    // 1. Bu iptal olan malzemeyle ilgili OLMAYAN ilk adımın indeksini bul (Yeni malzemenin adımı)
                    int nextValidStep = step;
                    while (nextValidStep < plan.Count && plan[nextValidStep].relatedFinalSO == s.relatedFinalSO)
                    {
                        nextValidStep++;
                    }

                    // 2. Eğer ajanın elinde bir şey kalmışsa, mekan dünyasına uyması için hedef tezgah bulalım
                    if (agent.HasKitchenObject())
                    {
                        BaseCounter disposalCounter = FindNearest<TrashCounter>(c => !c.HasKitchenObject());

                        if (disposalCounter != null)
                        {
                            Debug.Log($"[RecipeChain]  Fazlalık malzeme ({agent.GetKitchenObject().GetKitchenObjectSO().name}) tezgaha/çöpe bırakılmaya götürülüyor.");

                            // KRİTİK ADIM: Bırakma görevi bittiğinde sistemin tam olarak 'nextValidStep'e 
                            // geçebilmesi için zincir sayacını (nextValidStep - 1) yapıyoruz. 
                            // Görev tamamlanınca otomatik +1 alıp tam hedef adıma uyanacak.
                            currentStep = nextValidStep - 1;

                            GiveWhenAvailable(currentStep, SousChefCommand.DeliverToCounter, () => disposalCounter, "Fazlalık malzemeyi elden çıkar");
                            return; // Kodun aşağıya akmasını ve eski adımı çalıştırmasını engelliyoruz
                        }
                        else
                        {
                            // Güvenlik ağı: Eğer mutfaktaki tüm tezgahlar ağzına kadar doluysa mecbur yok ediyoruz
                            Debug.LogWarning("[RecipeChain] Fazlalığı bırakacak hiçbir boş yer yok! Mecburen yok ediliyor.");
                            agent.GetKitchenObject().DestroySelf();
                            agent.ClearKitchenObject();
                        }
                    }

                    // 3. Eğer ajanın eli zaten boşsa, hiç beklemeden doğrudan sonraki malzemeye ışınlan
                    currentStep = nextValidStep;
                    ExecuteStep(currentStep);
                    return;
                }
            }
        }

        // Hedef yoksa iptal etme — açılana kadar bekle
        GiveWhenAvailable(step, s.command, s.resolveTarget, $"Adım {step + 1}/{plan.Count}: {s.label}");
    }


    private bool BuildPlan(bool resuming = false)
    {
        plan.Clear();

        // Kesintiden devam ediyorsak var olan tabağı ve içindekileri bul
        PlateKitchenObject existingPlate = resuming ? FindExistingPlate() : null;
        List<KitchenObjectSO> onPlate = existingPlate != null
            ? existingPlate.GetKitchenObjectSOList()
            : new List<KitchenObjectSO>();

        if (existingPlate == null)
        {
            // 1. Tabak al ve boş bir tezgaha park et
            plateParkCounter = null;
            Add(SousChefCommand.FetchIngredient, () => FindNearest<PlatesCounter>(), "Tabak al");
            Add(SousChefCommand.DeliverToCounter, ResolveParkCounter, "Tabağı park et");
        }
        // else: plateParkCounter zaten var olan tabağın tezgahı (FindExistingPlate doğruladı)

        // 2. Tabakta OLMAYAN her malzemeyi üret ve tabağa birleştir
        foreach (KitchenObjectSO finalSO in activeRecipe.kitchenObjectSOList)
        {
            if (onPlate.Contains(finalSO)) continue; // zaten tabakta → atla

            // DEĞİŞEN KISIM: finalSO'yu metoda iletiyoruz
            if (!AddStepsToProduceInHand(finalSO, finalSO)) return false;

            // DEĞİŞEN KISIM: finalSO etiketini ekliyoruz
            Add(SousChefCommand.DeliverToCounter, () => plateParkCounter, $"Tabağa ekle: {finalSO.name}", finalSO);
        }

        // 3. Tabağı al ve teslim et
        Add(SousChefCommand.FetchIngredient, () => plateParkCounter, "Tabağı al");
        Add(SousChefCommand.DeliverToCounter, () => FindNearest<DeliveryCounter>(), "Siparişi teslim et");
        return true;
    }

    // İstenen ürünü ajanın ELİNE getirecek adımları ekler (özyinelemeli geri türetme)
    // DEĞİŞEN KISIM: 2. parametre olarak finalSO eklendi
    private bool AddStepsToProduceInHand(KitchenObjectSO targetSO, KitchenObjectSO finalSO)
    {
        if (TryGetFryingInput(targetSO, out KitchenObjectSO fryInput))
        {
            if (!AddStepsToProduceInHand(fryInput, finalSO)) return false;
            Add(SousChefCommand.CookIngredient,
                () => FindNearest<StoveCounter>(s => !s.HasKitchenObject() && s.CanFry(fryInput)),
                $"Pişir: {targetSO.name}", finalSO); // finalSO eklendi
            return true;
        }

        if (TryGetCuttingInput(targetSO, out KitchenObjectSO cutInput))
        {
            if (!AddStepsToProduceInHand(cutInput, finalSO)) return false;

            CuttingCounter reservedCutter = null;
            Add(SousChefCommand.DeliverToCounter,
                () => { reservedCutter = FindNearest<CuttingCounter>(c => !c.HasKitchenObject()); return reservedCutter; },
                $"Tahtaya bırak: {cutInput.name}", finalSO); // finalSO eklendi

            Add(SousChefCommand.ChopIngredient, () => reservedCutter, $"Kes: {targetSO.name}", finalSO); // finalSO eklendi
            Add(SousChefCommand.FetchIngredient, () => reservedCutter, $"Kesilmişi al: {targetSO.name}", finalSO); // finalSO eklendi
            return true;
        }

        KitchenObjectSO rawSO = targetSO;
        SourceCounter source = FindNearest<SourceCounter>(sc => sc.GetKitchenObjectSO() == rawSO);
        if (source == null) return false;

        Add(SousChefCommand.FetchIngredient,
            () => FindNearest<SourceCounter>(sc => sc.GetKitchenObjectSO() == rawSO),
            $"Kasadan al: {rawSO.name}", finalSO); // finalSO eklendi
        return true;
    }

    private BaseCounter ResolveParkCounter()
    {
        plateParkCounter = FindNearest<ClearCounter>(c => !c.HasKitchenObject());
        return plateParkCounter;
    }

    // Kesintiden devam için: daha önce park ettiğimiz tezgahta tabağımız hâlâ duruyor mu?
    private PlateKitchenObject FindExistingPlate()
    {
        if (plateParkCounter != null && plateParkCounter.HasKitchenObject()
            && plateParkCounter.GetKitchenObject() is PlateKitchenObject plate)
            return plate;
        return null;
    }

    private bool TryGetCuttingInput(KitchenObjectSO output, out KitchenObjectSO input)
    {
        foreach (CuttingRecipeSO r in cuttingRecipeSOArray)
            if (r.output == output) { input = r.input; return true; }
        input = null;
        return false;
    }

    private bool TryGetFryingInput(KitchenObjectSO output, out KitchenObjectSO input)
    {
        foreach (FryingRecipeSO r in fryingRecipeSOArray)
            if (r.output == output) { input = r.input; return true; }
        input = null;
        return false;
    }
}
