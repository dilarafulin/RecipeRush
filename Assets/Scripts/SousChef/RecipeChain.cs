using System.Collections.Generic;
using UnityEngine;

// Verilen RecipeSO'yu okuyup atomik görev planına çevirir:
// Tabak al → park et → [her malzeme: üret → tabağa birleştir] → tabağı al → teslim et.
// Üretim hattı geriye doğru türetilir: final ürün ← (pişirme) ← (kesme) ← ham kaynak.
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
        Debug.Log($"[RecipeChain] 📋 '{activeRecipe.recipeName}' planı: {plan.Count} adım");
        base.StartChain();
    }

    public override void ExecuteStep(int step)
    {
        if (step >= plan.Count)
        {
            Debug.Log($"[RecipeChain] 🍽 Tarif tamamlandı: {activeRecipe.recipeName}");
            Cancel();
            taskManager.OnChainCompleted();
            return;
        }

        // Hedef (örn. park için boş tezgah) o an yoksa iptal etme — açılana kadar bekle
        PlannedStep s = plan[step];
        GiveWhenAvailable(step, s.command, s.resolveTarget, $"Adım {step + 1}/{plan.Count}: {s.label}");
    }

    // Makro araya girip yarıda kestikten sonra: tabağın O ANKİ içeriğine bakıp
    // eksik malzemeler için yeniden planla ve devam et (statik plana takılıp kalma)
    public override void ResumeFromState()
    {
        Debug.Log("[RecipeChain] ▶ Kesintiden devam — tabağın durumuna göre yeniden planlanıyor");
        if (!BuildPlan(resuming: true))
        {
            Debug.LogError("[RecipeChain] Devam planı kurulamadı.");
            Cancel();
            taskManager.OnChainCompleted();
            return;
        }
        currentStep = 0;
        ExecuteStep(0);
    }

    // ── PLAN KURULUMU ────────────────────────────────────────────

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
            if (!AddStepsToProduceInHand(finalSO)) return false;
            Add(SousChefCommand.DeliverToCounter, () => plateParkCounter, $"Tabağa ekle: {finalSO.name}");
        }

        // 3. Tabağı al ve teslim et
        Add(SousChefCommand.FetchIngredient, () => plateParkCounter, "Tabağı al");
        Add(SousChefCommand.DeliverToCounter, () => FindNearest<DeliveryCounter>(), "Siparişi teslim et");
        return true;
    }

    // İstenen ürünü ajanın ELİNE getirecek adımları ekler (özyinelemeli geri türetme)
    private bool AddStepsToProduceInHand(KitchenObjectSO targetSO)
    {
        // Pişirilerek mi elde ediliyor? (örn: pişmiş köfte ← çiğ köfte)
        if (TryGetFryingInput(targetSO, out KitchenObjectSO fryInput))
        {
            if (!AddStepsToProduceInHand(fryInput)) return false;
            Add(SousChefCommand.CookIngredient,
                () => FindNearest<StoveCounter>(s => !s.HasKitchenObject() && s.CanFry(fryInput)),
                $"Pişir: {targetSO.name}");
            return true; // pişen ürün elde biter
        }

        // Kesilerek mi elde ediliyor? (örn: dilim domates ← bütün domates)
        if (TryGetCuttingInput(targetSO, out KitchenObjectSO cutInput))
        {
            if (!AddStepsToProduceInHand(cutInput)) return false;

            // Bu kesme işlemine ayrılan tahtayı yakala ve KES/AL adımlarında da
            // AYNI tahtayı kullan. Yoksa adımlar "en yakın dolu tahta"yı bulur ve
            // oyuncunun kendi malzemesini koyduğu başka tahtayı yanlışlıkla kapabilir.
            CuttingCounter reservedCutter = null;
            Add(SousChefCommand.DeliverToCounter,
                () =>
                {
                    reservedCutter = FindNearest<CuttingCounter>(c => !c.HasKitchenObject());
                    return reservedCutter;
                },
                $"Tahtaya bırak: {cutInput.name}");
            Add(SousChefCommand.ChopIngredient,
                () => reservedCutter,
                $"Kes: {targetSO.name}");
            Add(SousChefCommand.FetchIngredient,
                () => reservedCutter,
                $"Kesilmişi al: {targetSO.name}");
            return true; // kesilen ürün elde biter
        }

        // Ham malzeme — eşleşen kasadan al
        KitchenObjectSO rawSO = targetSO;
        SourceCounter source = FindNearest<SourceCounter>(sc => sc.GetKitchenObjectSO() == rawSO);
        if (source == null)
        {
            Debug.LogError($"[RecipeChain] '{rawSO.name}' üreten SourceCounter yok ve işleme tarifi de bulunamadı!");
            return false;
        }
        Add(SousChefCommand.FetchIngredient,
            () => FindNearest<SourceCounter>(sc => sc.GetKitchenObjectSO() == rawSO),
            $"Kasadan al: {rawSO.name}");
        return true;
    }

    private void Add(SousChefCommand cmd, System.Func<BaseCounter> resolver, string label)
        => plan.Add(new PlannedStep { command = cmd, resolveTarget = resolver, label = label });

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
