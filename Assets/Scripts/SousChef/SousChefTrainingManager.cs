using UnityEngine;
using System.Collections;

public class SousChefTrainingManager : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private SousChefAgent agent;
    [SerializeField] private SousChefTaskManager taskManager;

    [Header("Eğitim Ayarları")]
    [SerializeField] private bool trainingModeActive = false;
    [SerializeField] private float taskAssignDelay = 0.5f;

    [Header("Source Counter'lar ")]
    [SerializeField] private BaseCounter[] sourceCounters;

    [Header("Diğer Hedefler")]
    [SerializeField] private BaseCounter cuttingCounter;
    [SerializeField] private BaseCounter stoveCounter;
    [SerializeField] private BaseCounter clearCounter;
    [SerializeField] private BaseCounter platesCounter;

    private bool waitingForTask = false;

    private void Start()
    {
        if (!trainingModeActive) return;
        taskManager.OnTaskChanged += OnTaskChanged;
        StartCoroutine(AssignTaskAfterDelay());
    }

    private void OnTaskChanged(object sender, System.EventArgs e)
    {
        if (!trainingModeActive) return;
        if (waitingForTask) return;

        SousChefTask current = taskManager.GetActiveTask();
        if (current == null || current.isCompleted)
            StartCoroutine(AssignTaskAfterDelay());
    }

    private IEnumerator AssignTaskAfterDelay()
    {
        waitingForTask = true;
        yield return new WaitForSeconds(taskAssignDelay);
        AssignRandomTask();
        waitingForTask = false;
    }

    private void AssignRandomTask()
    {
        // 5 görev tipi rastgele seçilir
        int scenarioIndex = Random.Range(0, 5);

        switch (scenarioIndex)
        {
            case 0:
                // 6 source counter arasından rastgele biri
                if (sourceCounters.Length == 0) break;
                BaseCounter randomSource = sourceCounters[Random.Range(0, sourceCounters.Length)];
                Debug.Log($"[Training] FetchIngredient → {randomSource.name}");
                taskManager.GiveCommand(SousChefCommand.FetchIngredient, randomSource);
                break;

            case 1:
                if (cuttingCounter == null) break;
                Debug.Log("[Training] ChopIngredient → CuttingCounter");
                taskManager.GiveCommand(SousChefCommand.ChopIngredient, cuttingCounter);
                break;

            case 2:
                if (stoveCounter == null) break;
                Debug.Log("[Training] CookIngredient → StoveCounter");
                taskManager.GiveCommand(SousChefCommand.CookIngredient, stoveCounter);
                break;

            case 3:
                if (clearCounter == null) break;
                Debug.Log("[Training] DeliverToCounter → ClearCounter");
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, clearCounter);
                break;

            case 4:
                if (platesCounter == null) break;
                Debug.Log("[Training] FetchIngredient → PlatesCounter");
                taskManager.GiveCommand(SousChefCommand.FetchIngredient, platesCounter);
                break;
        }
    }
}