using UnityEngine;

// Hazırlık makrosu: kesilmiş malzemeyi bir tabağa koyup hazır tabağı boş tezgaha bırakır.
// Teslimat bilinçli olarak YOK — tek malzemelik tabak siparişlerle nadiren eşleşir;
// tam sipariş teslimatı RecipeChain'in işi.
public class ChopAndPlateChain : SousChefChainBase
{
    private BaseCounter dynamicSourceCounter;

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
                GiveWhenAvailable(0, SousChefCommand.FetchIngredient,
                    () => dynamicSourceCounter, "Kaynaktan al");
                break;

            case 1: // Boş kesme tahtasına bırak
                GiveWhenAvailable(1, SousChefCommand.DeliverToCounter,
                    () => FindNearest<CuttingCounter>(c => !c.HasKitchenObject()), "Tahtaya bırak");
                break;

            case 2: // Malzemeli tahtayı bul ve kes
                GiveWhenAvailable(2, SousChefCommand.ChopIngredient,
                    () => FindNearest<CuttingCounter>(c => c.HasKitchenObject()), "Kes");
                break;

            case 3: // Temiz tabak al
                GiveWhenAvailable(3, SousChefCommand.FetchIngredient,
                    () => FindNearest<PlatesCounter>(), "Tabak al");
                break;

            case 4: // Tabağı tahtaya götür (merge)
                GiveWhenAvailable(4, SousChefCommand.DeliverToCounter,
                    () => FindNearest<CuttingCounter>(c => c.HasKitchenObject()), "Tabağı tahtaya götür");
                break;

            case 5: // Hazır tabağı boş tezgaha bırak (boş yer açılana kadar bekler)
                GiveWhenAvailable(5, SousChefCommand.DeliverToCounter,
                    () => FindNearest<ClearCounter>(c => !c.HasKitchenObject()), "Hazır tabağı bırak");
                break;

            default:
                Cancel();
                taskManager.OnChainCompleted();
                break;
        }
    }

}