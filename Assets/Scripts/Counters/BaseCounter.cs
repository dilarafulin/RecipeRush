using UnityEngine;

public class BaseCounter : MonoBehaviour, IKitchenObjectParent
{
    [SerializeField] private Transform counterTopPoint;

    private KitchenObject kitchenObject;
    public virtual void Interact(Player player)
    {
        Debug.LogError("Interact override edilmedi: " + gameObject.name);
    }
    public virtual void InteractAlternate(Player player)
    {
    }

    public virtual SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        return null;
    }

    public virtual void InteractFromAgent(SousChefAgent agent)
    {

        // 1. Tezgah doluysa ve ajanın eli boşsa, malzemeyi ajana ver
        if (HasKitchenObject() && !agent.HasKitchenObject())
        {
            GetKitchenObject().SetKitchenObjectParent(agent);
        }
        // 2. Tezgah boşsa ve ajanın eli doluysa, malzemeyi tezgaha bırak
        else if (!HasKitchenObject() && agent.HasKitchenObject())
        {
            agent.GetKitchenObject().SetKitchenObjectParent(this);
        }
    }

    // ── IKitchenObjectParent ──────────────────────
    public Transform GetKitchenObjectFollowTransform() => counterTopPoint;
    public void SetKitchenObject(KitchenObject obj) => kitchenObject = obj;
    public KitchenObject GetKitchenObject() => kitchenObject;
    public void ClearKitchenObject() => kitchenObject = null;
    public bool HasKitchenObject() => kitchenObject != null;


}