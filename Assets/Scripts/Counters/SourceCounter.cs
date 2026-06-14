using UnityEngine;

public class SourceCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO; // hangi malzeme

    // Zincir planlayıcısı doğru kasayı bulabilsin diye
    public KitchenObjectSO GetKitchenObjectSO() => kitchenObjectSO;

    public override void Interact(Player player)
    {
        if (!player.HasKitchenObject())
        {
            // Oyuncunun eli bo� � malzemeyi spawn et, direkt ele ver
            KitchenObject.SpawnKitchenObject(kitchenObjectSO, player);
        }
        // Oyuncunun elinde bir �ey varsa hi�bir �ey yapma
    }

    public override void InteractFromAgent(SousChefAgent agent)
    {
        // Ajan�n eli bo�sa, ona yeni bir malzeme (�rn: Domates) �ret ve ver
        if (!agent.HasKitchenObject())
        {
            KitchenObject.SpawnKitchenObject(kitchenObjectSO, agent);
        }
    }

    public override SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        // Ajan�n eli bo�sa 
        if (!agent.HasKitchenObject())
        {
            return new SousChefTask(SousChefCommand.FetchIngredient, this);
        }
        return null;
    }
}