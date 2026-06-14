using UnityEngine;

// Hazırlık makrosu: pişmiş malzemeyi bir tabağa koyup hazır tabağı boş tezgaha bırakır.
// Teslimat bilinçli olarak YOK — tam sipariş teslimatı RecipeChain'in işi.
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
        // Hedef tezgah o an müsait değilse adımlar iptal etmez, açılana kadar bekler
        switch (step)
        {
            case 0: // Kaynaktan malzemeyi al
                cachedStagingCounter = null;
                GiveWhenAvailable(0, SousChefCommand.FetchIngredient,
                    () => dynamicSourceCounter, "Kaynaktan al");
                break;

            case 1: // Boş ocağa koy
                GiveWhenAvailable(1, SousChefCommand.DeliverToCounter,
                    () => FindNearest<StoveCounter>(s => !s.HasKitchenObject()), "Ocağa koy");
                break;

            case 2: // Ocaktaki malzemeyi pişir
                GiveWhenAvailable(2, SousChefCommand.CookIngredient,
                    () => FindNearest<StoveCounter>(s => s.HasKitchenObject() && s.IsIdle()), "Pişir");
                break;

            case 3: // Pişmişi boş tezgaha (staging) koy ve SAKLA
                GiveWhenAvailable(3, SousChefCommand.DeliverToCounter,
                    () =>
                    {
                        cachedStagingCounter = FindNearest<ClearCounter>(c => !c.HasKitchenObject());
                        return cachedStagingCounter;
                    }, "Pişmişi tezgaha koy");
                break;

            case 4: // Tabak al
                GiveWhenAvailable(4, SousChefCommand.FetchIngredient,
                    () => FindNearest<PlatesCounter>(), "Tabak al");
                break;

            case 5: // Tabağı staging'e götür → et tabağa girer
                GiveWhenAvailable(5, SousChefCommand.DeliverToCounter,
                    () => (cachedStagingCounter != null && cachedStagingCounter.HasKitchenObject())
                            ? cachedStagingCounter : null, "Tabağı tezgaha götür");
                break;

            case 6: // Hazır tabağı boş tezgaha bırak (boş yer açılana kadar bekler)
                GiveWhenAvailable(6, SousChefCommand.DeliverToCounter,
                    () => FindNearest<ClearCounter>(c => !c.HasKitchenObject()), "Hazır tabağı bırak");
                break;

            default:
                Cancel();
                taskManager.OnChainCompleted();
                break;
        }
    }

}
