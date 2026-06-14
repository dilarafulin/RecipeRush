using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    public override void Interact(Player player)
    {
        // 1. Oyuncunun elinde bir �ey var m�?
        if (player.HasKitchenObject())
        {
            // 2. MODERN C#: Pattern Matching (Desen E�le�tirme)
            // E�er elindeki obje bir 'PlateKitchenObject' ise, onu an�nda 'plateKitchenObject' de�i�kenine d�n��t�r ve i�eri gir!
            if (player.GetKitchenObject() is PlateKitchenObject plateKitchenObject)
            {
                // 3. Taba��n i�indeki malzemelerin listesini al ve Hakem'e (Manager) g�nder!
                DeliveryManager.Instance.DeliverRecipe(plateKitchenObject.GetKitchenObjectSOList());

                // 4. Teslimat yap�ld�ktan sonra taba�� yok et
                player.GetKitchenObject().DestroySelf();
            }
            else
            {
                // Oyuncu elinde tabak olmayan bir �eyle (�rn: Domates) geldi. Hi�bir �ey yapma.
                Debug.Log("Sadece tabakla teslimat yapabilirsin!");
            }
        }
    }

    public override void InteractFromAgent(SousChefAgent agent)
    {
        if (agent.HasKitchenObject() && agent.GetKitchenObject() is PlateKitchenObject plateKitchenObject)
        {
            DeliveryManager.Instance.DeliverRecipe(plateKitchenObject.GetKitchenObjectSOList());
            // Tabak yok edilince ajanın eli boşalır → HandleDeliver görevi tamamlanmış sayar
            agent.GetKitchenObject().DestroySelf();
        }
    }

    public override SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        if (agent.HasKitchenObject() && agent.GetKitchenObject() is PlateKitchenObject)
            return new SousChefTask(SousChefCommand.DeliverToCounter, this);
        return null;
    }
}