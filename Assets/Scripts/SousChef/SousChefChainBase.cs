using UnityEngine;

public abstract class SousChefChainBase : MonoBehaviour
{
    [SerializeField] protected SousChefAgent agent;

    protected SousChefTaskManager taskManager;
    protected int currentStep = -1;

    protected BaseCounter[] cachedCounters;

    protected virtual void Awake()
    {
        cachedCounters = Object.FindObjectsByType<BaseCounter>(FindObjectsSortMode.None);
    }

    public void Initialize(SousChefTaskManager manager)
    {
        taskManager = manager;
    }

    public virtual void StartChain()
    {
        currentStep = 0;
        ExecuteStep(currentStep);
    }

    public abstract void ExecuteStep(int step);

    public virtual void OnStepCompleted()
    {
        if (!IsRunning()) return;
        currentStep++;
        Debug.Log($"[Chain] OnStepCompleted çağrıldı → currentStep: {currentStep}");
        ExecuteStep(currentStep);
    }

    public bool IsRunning() => currentStep >= 0;

    public virtual void Cancel() => currentStep = -1;

    protected T FindNearest<T>(System.Func<T, bool> filter = null) where T : BaseCounter
    {
        T nearest = null;
        float minDist = float.MaxValue;
        Vector3 agentPos = agent.transform.position;

        // BÜYÜK OPTİMİZASYON: Sahneyi taramak yerine, sadece RAM'deki hazır diziyi dönüyoruz!
        for (int i = 0; i < cachedCounters.Length; i++)
        {
            // Dizideki BaseCounter'ı aradığımız T tipine (Örn: StoveCounter) çevirmeyi dene (as operatörü)
            T counter = cachedCounters[i] as T;

            // Eğer bu tezgah aradığımız tipte değilse (null döner) veya filtreyi geçemediyse atla
            if (counter == null || (filter != null && !filter(counter))) continue;

            // Mesafe ölçümü
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