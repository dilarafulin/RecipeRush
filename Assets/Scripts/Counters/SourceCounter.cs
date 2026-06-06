using UnityEngine;

public class SourceCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO; // hangi malzeme

    public override void Interact(Player player)
    {
        if (!player.HasKitchenObject())
        {
            // Oyuncunun eli boþ — malzemeyi spawn et, direkt ele ver
            KitchenObject.SpawnKitchenObject(kitchenObjectSO, player);
        }
        // Oyuncunun elinde bir þey varsa hiçbir þey yapma
    }

    public override void InteractFromAgent(SousChefAgent agent)
    {
        // Ajanýn eli boþsa, ona yeni bir malzeme (Örn: Domates) üret ve ver
        if (!agent.HasKitchenObject())
        {
            KitchenObject.SpawnKitchenObject(kitchenObjectSO, agent);
        }
    }

    public override SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        // Ajanýn eli boþsa 
        if (!agent.HasKitchenObject())
        {
            return new SousChefTask(SousChefCommand.FetchIngredient, this);
        }
        return null;
    }
}