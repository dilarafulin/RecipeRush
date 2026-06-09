using UnityEngine;

public class CookAndPlateChain : SousChefChainBase
{
    private BaseCounter dynamicSourceCounter;
    private ClearCounter cachedStagingCounter; 

    public void SetSourceCounter(BaseCounter clickedSourceCounter)
    {
        dynamicSourceCounter = clickedSourceCounter;
    }

    public override void ExecuteStep(int step)
    {
        switch (step)
        {
            case 0:
                cachedStagingCounter = null;
                taskManager.GiveCommand(SousChefCommand.FetchIngredient, dynamicSourceCounter);
                break;

            case 1:
                StoveCounter emptyStove = FindNearest<StoveCounter>(s => !s.HasKitchenObject());
                if (emptyStove == null) { Debug.LogWarning("[Chain] Boş ocak yok!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, emptyStove);
                break;

            case 2:
                StoveCounter fullStove = FindNearest<StoveCounter>(
                    s => s.HasKitchenObject() && s.IsIdle());
                if (fullStove == null) { Debug.LogWarning("[Chain] Pişirilecek ocak yok!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.CookIngredient, fullStove);
                break;

            case 3:
                // Pişmiş eti staging'e bırak ve SAKLA
                cachedStagingCounter = FindNearest<ClearCounter>(c => !c.HasKitchenObject());
                if (cachedStagingCounter == null) { Debug.LogWarning("[Chain] Boş tezgah yok!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, cachedStagingCounter);
                break;

            case 4:
                // Tabak al
                PlatesCounter plates = FindNearest<PlatesCounter>();
                if (plates == null) { Debug.LogWarning("[Chain] Tabaklık yok!"); Cancel(); return; }
                taskManager.GiveCommand(SousChefCommand.FetchIngredient, plates);
                break;

            case 5:
                // Tabağı staging'e götür → et tabağa girer
                if (cachedStagingCounter == null || !cachedStagingCounter.HasKitchenObject())
                {
                    Debug.LogWarning("[Chain] Staging tezgahı boş!");
                    Cancel(); return;
                }
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, cachedStagingCounter);
                break;

            default:
                Cancel();
                taskManager.OnChainCompleted();
                break;
        }
    }

}
