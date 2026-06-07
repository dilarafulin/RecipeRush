using UnityEngine;

public abstract class SousChefChainBase : MonoBehaviour
{
    [SerializeField] protected SousChefAgent agent;

    protected SousChefTaskManager taskManager;
    protected int currentStep = -1;

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
        if (!IsRunning()) return; // Zincir iptal edildiyse devam etme

        currentStep++;
        ExecuteStep(currentStep);
    }

    public bool IsRunning() => currentStep >= 0;

    public virtual void Cancel() => currentStep = -1;
}