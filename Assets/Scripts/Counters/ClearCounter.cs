using UnityEngine;

public class ClearCounter : BaseCounter
{
    [SerializeField] private KitchenObjectSO kitchenObjectSO; // tezgahýn baþlangýç malzemesi

    public override void Interact(Player player)
    {
        if (!HasKitchenObject())
        {
            if (player.HasKitchenObject())
            {
                player.GetKitchenObject().SetKitchenObjectParent(this);
            }
        }
        else
        {
            if (player.HasKitchenObject())
            {
                // Ýkisinin de eli dolu 
                TryHandlePlateMerge(player);
            }
            else
            {
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
        else if (HasKitchenObject() && agent.HasKitchenObject())
        {
            if (GetKitchenObject() is PlateKitchenObject || agent.GetKitchenObject() is PlateKitchenObject)
            {
                return new SousChefTask(SousChefCommand.DeliverToCounter, this);
            }
        }
        return null;
    }

    public override void InteractFromAgent(SousChefAgent agent)
    {
        if (HasKitchenObject() && agent.HasKitchenObject())
        {
            TryHandlePlateMerge(agent);
        }
        else
        {
            base.InteractFromAgent(agent);
        }
    }

    private bool TryHandlePlateMerge(IKitchenObjectParent interactingEntity)
    {
        //Tezgahtaki þey bir Tabak mý?
        if (GetKitchenObject() is PlateKitchenObject plateKitchenObject)
        {
            if (plateKitchenObject.TryAddIngredient(interactingEntity.GetKitchenObject().GetKitchenObjectSO()))
            {
                interactingEntity.GetKitchenObject().DestroySelf();
                return true; 
            }
        }
        //Gelen varlýðýn elindeki þey bir Tabak mý?
        else if (interactingEntity.GetKitchenObject() is PlateKitchenObject entityPlate)
        {
            if (entityPlate.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO()))
            {
                GetKitchenObject().DestroySelf();
                return true; 
            }
        }
        return false; 
    }
}