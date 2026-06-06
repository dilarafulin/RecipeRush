using UnityEngine;

public class ClearCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO; // tezgahýn baþlangýç malzemesi

    public override void Interact(Player player)
    {
        if (!HasKitchenObject())
        {
            // 1. SENARYO: Tezgah tamamen boþ
            if (player.HasKitchenObject())
            {
                // Oyuncuda eþya var, tezgaha býrak
                player.GetKitchenObject().SetKitchenObjectParent(this);
            }
        }
        else
        {
            // 2. SENARYO: Tezgahta kesinlikle bir eþya var
            if (player.HasKitchenObject())
            {
                // A) OYUNCUNUN DA ELÝ DOLU (Birleþtirme Senaryolarý)

                // DURUM 1: Tezgahtaki þey bir Tabak mý?
                if (GetKitchenObject() is PlateKitchenObject plateKitchenObject)
                {
                    if (plateKitchenObject.TryAddIngredient(player.GetKitchenObject().GetKitchenObjectSO()))
                    {
                        player.GetKitchenObject().DestroySelf();
                    }
                }
                // DURUM 2: Oyuncunun elindeki þey bir Tabak mý?
                else if (player.GetKitchenObject() is PlateKitchenObject playerPlateKitchenObject)
                {
                    if (playerPlateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO()))
                    {
                        GetKitchenObject().DestroySelf();
                    }
                }
            }
            else
            {
                // oyuncunun eli boþ
                GetKitchenObject().SetKitchenObjectParent(player);
            }
        }
    }

    public override SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        // tezgah boþ, ajanýn elinde malzeme var
        if (!HasKitchenObject() && agent.HasKitchenObject())
        {
            return new SousChefTask(SousChefCommand.DeliverToCounter, this);
        }
        // tezgahta malzeme var, ajanýn eli boþ
        else if (HasKitchenObject() && !agent.HasKitchenObject())
        {
            return new SousChefTask(SousChefCommand.FetchIngredient, this);
        }
        return null;
    }
}