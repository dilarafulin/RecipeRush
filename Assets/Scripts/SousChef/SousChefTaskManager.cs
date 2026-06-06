using UnityEngine;
using System;
public class SousChefTaskManager : MonoBehaviour
{
    public static SousChefTaskManager Instance { get; private set; }

    public event EventHandler OnTaskChanged;

    [Header("Referanslar")]
    [SerializeField] private SousChefAgent agent;

    private SousChefTask activeTask;

    private void Awake()
    {
        Instance = this;
    }

    //polimorfizm
    public void AssignTaskBasedOnContext(BaseCounter clickedCounter)
    {
        // 1. Tıklanan tezgaha "Şu anki durumda ajanın ne yapması lazım?" diye soruyoruz
        SousChefTask newTask = clickedCounter.GetTaskForAgent(agent);

        // 2. Eğer tezgah mantıklı bir görev döndürdüyse, senin orijinal sistemine yolluyoruz
        if (newTask != null)
        {
            GiveCommand(newTask.command, newTask.targetCounter, newTask.targetItemSO);
        }
        else
        {
            Debug.Log("[TaskManager] Bu duruma uygun mantıklı bir görev bulunamadı (Belki tezgah boş, belki de ajanın eli yanlış dolu).");
        }
    }

    public void GiveCommand(SousChefCommand command, BaseCounter targetCounter, KitchenObjectSO itemSO = null)
    {
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
        if (activeTask != null)
        {
            activeTask.isCompleted = true;
            Debug.Log("[TaskManager] Görev Başarıyla Tamamlandı!");
            OnTaskChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SousChefTask GetActiveTask()
    {
        return activeTask;
    }
}