using UnityEngine;
using System;
public class SousChefTaskManager : MonoBehaviour
{
    public static SousChefTaskManager Instance { get; private set; }

    public event EventHandler OnTaskChanged;

    [Header("Referanslar")]
    [SerializeField] private SousChefAgent agent;

    private SousChefTask activeTask;
    private SousChefChainBase activeChain;

    private void Awake()
    {
        Instance = this;
    }

    //polimorfizm
    public void AssignTaskBasedOnContext(BaseCounter clickedCounter)
    {
        // Aktif zinciri iptal et — tek adım komut öncelikli
        if (activeChain != null && activeChain.IsRunning())
        {
            activeChain.Cancel();
            activeChain = null;
        }

        SousChefTask newTask = clickedCounter.GetTaskForAgent(agent);
        if (newTask != null)
        {
            GiveCommand(newTask.command, newTask.targetCounter, newTask.targetItemSO);
        }
        else
        {
            Debug.Log("[TaskManager] Uygun görev bulunamadı.");
        }
    }

    public void GiveCommand(SousChefCommand command, BaseCounter targetCounter, KitchenObjectSO itemSO = null)
    {

        // YENİ KONTROL
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(targetCounter))
        {
            Debug.LogError($"[TaskManager] HATA: targetCounter '{targetCounter.name}' bir Prefab Asset! " +
                           "Sahne instance'ı olmalı. GetTaskForAgent'ı hangi obje çağırıyor?");
            return;
        }
#endif
        activeTask = new SousChefTask(command, targetCounter, itemSO);

        if (agent != null)
        {
            agent.SetTask(activeTask);
            Debug.Log($"[TaskManager] Yeni Komut Verildi: {command} → Hedef: {targetCounter.name}");

            OnTaskChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Debug.LogError("TaskManager'da Agent referansı eksik!");
        }
    }

    public void OnTaskCompleted()
    {
        Debug.Log("[TaskManager] Atomik Görev Tamamlandı!");
        OnTaskChanged?.Invoke(this, EventArgs.Empty);

        // Önce zinciri ilerlet — GiveCommand yeni activeTask'ı atar
        if (activeChain != null && activeChain.IsRunning())
        {
            activeChain.OnStepCompleted();
        }
        else
        {
            // Zincir yoksa veya bittiyse null yap
            activeTask = null;
        }
    }

    public SousChefTask GetActiveTask()
    {
        return activeTask;
    }

    public void StartChain(SousChefChainBase newChain)
    {
        if (newChain == null)
        {
            Debug.LogError("[TaskManager] StartChain: null zincir verildi!");
            return;
        }

        if (activeChain != null && activeChain.IsRunning())
            activeChain.Cancel();

        activeChain = newChain;
        activeChain.Initialize(this);
        activeChain.StartChain();

        Debug.Log($"[TaskManager] Yeni zincir başladı: {newChain.GetType().Name}");
    }

    public void OnChainCompleted()
    {
        Debug.Log("[TaskManager] Makro Zincir Tamamlandı!");
        activeChain = null;
        activeTask = null;
        OnTaskChanged?.Invoke(this, EventArgs.Empty);
    }
}