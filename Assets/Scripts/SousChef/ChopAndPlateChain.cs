using UnityEngine;

public class ChopAndPlateChain : SousChefChainBase
{
    // deliveryCounter hala sabit kalabilir cunku teslimat noktasi genelde tekdir
    // Ama istersen onu da dinamik yapabiliriz
    [Header("Sabit Referanslar")]
    [SerializeField] private BaseCounter deliveryCounter;

    private BaseCounter dynamicSourceCounter;

    public void SetSourceCounter(BaseCounter clickedSourceCounter)
    {
        dynamicSourceCounter = clickedSourceCounter;
    }

    public override void ExecuteStep(int step)
    {
        switch (step)
        {
            case 0:
                // 1. ADIM: Kaynaktan malzemeyi al (zaten dinamikti)
                taskManager.GiveCommand(SousChefCommand.FetchIngredient, dynamicSourceCounter);
                break;

            case 1:
                // 2. ADIM: Bos bir kesme tahtasi bul ve malzemeyi birak
                CuttingCounter emptyCutter = FindNearest<CuttingCounter>(c => !c.HasKitchenObject());
                if (emptyCutter == null) { Debug.LogWarning("[Chain] Bos kesme tahtasi bulunamadi!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, emptyCutter);
                break;

            case 2:
                // 3. ADIM: Uzerinde malzeme olan kesme tahtasini bul ve kes
                CuttingCounter fullCutter = FindNearest<CuttingCounter>(c => c.HasKitchenObject());
                if (fullCutter == null) { Debug.LogWarning("[Chain] Malzemeli kesme tahtasi bulunamadi!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.ChopIngredient, fullCutter);
                break;

            case 3:
                // 4. ADIM: Temiz bir tabak al
                BaseCounter platesCounter = FindNearest<PlatesCounter>();
                if (platesCounter == null) { Debug.LogWarning("[Chain] Tabaklik bulunamadi!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.FetchIngredient, platesCounter);
                break;

            case 4:
                // 5. ADIM: Tabagi kesme tahtasina gotur (PlateMerge otomatik calisacak)
                CuttingCounter cutterWithItem = FindNearest<CuttingCounter>(c => c.HasKitchenObject());
                if (cutterWithItem == null) { Debug.LogWarning("[Chain] Malzemeli kesme tahtasi bulunamadi!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, cutterWithItem);
                break;

            default:
                Cancel();
                taskManager.OnChainCompleted();
                break;
        }
    }

    // Sahnedeki en yakin T tipindeki counter'i bulur
    // filter: opsiyonel kosul (ornek: bos olmali, dolu olmali vs.)
    private T FindNearest<T>(System.Func<T, bool> filter = null) where T : BaseCounter
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        T nearest = null;
        float minDist = float.MaxValue;
        Vector3 agentPos = agent.transform.position;

        foreach (T counter in all)
        {
            if (filter != null && !filter(counter)) continue;
            float dist = Vector3.Distance(agentPos, counter.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = counter;
            }
        }
        return nearest;
    }
}