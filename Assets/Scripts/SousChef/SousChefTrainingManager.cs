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
    // Zincirler görevleri ardışık verir: ajan her görevde spawn'dan değil,
    // önceki görevin bittiği yerden başlamayı da öğrenmeli (train/test dağılım uyumu)
    [SerializeField] private int tasksPerEpisode = 4;

    private int tasksThisEpisode = 0;

    [Header("Source Counter'lar ")]
    [SerializeField] private BaseCounter[] sourceCounters;

    [Header("Diğer Hedefler")]
    [SerializeField] private BaseCounter cuttingCounter;
    [SerializeField] private BaseCounter stoveCounter;
    [SerializeField] private BaseCounter clearCounter;
    [SerializeField] private BaseCounter platesCounter;
    // Zincirin kullandığı ama eğitimde hedef olmayan iki lokasyon — eklenmezse
    // ajan bu konumlara gitmeyi öğrenemez (gözlemde hiç görmemiş olur)
    [SerializeField] private BaseCounter deliveryCounter;
    [SerializeField] private KitchenObjectSO plateKitchenObjectSO; // teslimat pratiği için tabak

    [Header("Eğitim Malzemeleri")]
    [SerializeField] private KitchenObjectSO[] choppableItemSOs; // kesme tarifi olanlar (örn: domates, soğan)
    [SerializeField] private KitchenObjectSO fryableItemSO;   // pişirme tarifi olan (örn: çiğ köfte)

    private bool waitingForTask = false;

    private void Start()
    {
        if (!trainingModeActive) return;

        // Ön kontrol: Cook müfredatı çalışabilir mi? Yanlış konfigürasyonla
        // saatlerce eğitim yapmak yerine ilk karede bağır
        if (fryableItemSO != null)
        {
            if (stoveCounter is StoveCounter stove)
            {
                if (!stove.CanFry(fryableItemSO))
                    Debug.LogError($"[TrainingManager] HATA: '{stove.name}' ocağı '{fryableItemSO.name}' " +
                                   "malzemesini pişiremiyor! FryingRecipeSOArray'de bu malzemenin tarifi yok. " +
                                   "Cook görevleri asla tamamlanamayacak.");
            }
            else
            {
                Debug.LogError("[TrainingManager] HATA: stoveCounter alanı bir StoveCounter değil veya boş!");
            }
        }

        taskManager.OnTaskChanged += OnTaskChanged;
        StartCoroutine(AssignTaskAfterDelay());
    }

    private void Update()
    {
        if (!trainingModeActive) return;

        // Aktif görev yoksa veya bitmişse VE şu an yeni görev beklenmiyorsa
        SousChefTask current = taskManager.GetActiveTask();
        if ((current == null || current.isCompleted) && !waitingForTask)
        {
            StartCoroutine(AssignTaskAfterDelay());
        }
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
        // Ajan eli doluysa temizle
        if (agent.HasKitchenObject())
        {
            agent.GetKitchenObject().DestroySelf();
            agent.ClearKitchenObject();
        }
        ClearTrainingCounters();

        // Bölümü her görevde değil, birkaç görevde bir kapat — aradaki görevler
        // ajanın o anki pozisyonundan başlar, tıpkı zincir çalıştırırken olduğu gibi
        tasksThisEpisode++;
        if (tasksThisEpisode >= tasksPerEpisode)
        {
            agent.EndEpisode();
            tasksThisEpisode = 0;
        }

        // Müfredat: %30 Fetch, %25 Chop, %25 Cook, %20 Deliver.
        // Ön koşul referansları eksikse Fetch'e düşer — görev her zaman tamamlanabilir kalmalı
        float r = Random.value;

        if (r >= 0.3f && r < 0.55f
            && cuttingCounter is CuttingCounter cut
            && choppableItemSOs != null && choppableItemSOs.Length > 0)
        {
            KitchenObjectSO randomChoppable = choppableItemSOs[Random.Range(0, choppableItemSOs.Length)];
            cut.PlaceObjectForTraining(randomChoppable);
            taskManager.GiveCommand(SousChefCommand.ChopIngredient, cuttingCounter);
            return;
        }

        if (r >= 0.55f && r < 0.8f && stoveCounter != null && fryableItemSO != null)
        {
            // Pişirilecek malzeme ajanın elinde başlar; ocağa taşıyıp pişirmesi gerekir
            KitchenObject.SpawnKitchenObject(fryableItemSO, agent);
            taskManager.GiveCommand(SousChefCommand.CookIngredient, stoveCounter);
            return;
        }

        if (r >= 0.8f)
        {
            // Yarısı: tabakla teslimat tezgahına git ve teslim et (zincir finali)
            if (deliveryCounter != null && plateKitchenObjectSO != null && Random.value < 0.5f)
            {
                KitchenObject.SpawnKitchenObject(plateKitchenObjectSO, agent);
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, deliveryCounter);
                return;
            }
            // Yarısı: elindeki malzemeyi boş tezgaha bırak
            if (clearCounter != null && choppableItemSOs != null && choppableItemSOs.Length > 0)
            {
                KitchenObjectSO randomItem = choppableItemSOs[Random.Range(0, choppableItemSOs.Length)];
                KitchenObject.SpawnKitchenObject(randomItem, agent);
                taskManager.GiveCommand(SousChefCommand.DeliverToCounter, clearCounter);
                return;
            }
        }

        // Fetch: bazen tabaklıktan al (zincir oradan tabak çeker), bazen kasadan
        if (platesCounter != null && Random.value < 0.35f)
        {
            taskManager.GiveCommand(SousChefCommand.FetchIngredient, platesCounter);
            return;
        }
        if (sourceCounters.Length == 0) return;
        BaseCounter randomSource = sourceCounters[Random.Range(0, sourceCounters.Length)];
        taskManager.GiveCommand(SousChefCommand.FetchIngredient, randomSource);
    }

    // Önceki görevden kalan malzemeler ortamı kirletmesin
    private void ClearTrainingCounters()
    {
        if (cuttingCounter != null && cuttingCounter.HasKitchenObject())
            cuttingCounter.GetKitchenObject().DestroySelf();

        if (clearCounter != null && clearCounter.HasKitchenObject())
            clearCounter.GetKitchenObject().DestroySelf();

        if (stoveCounter != null && stoveCounter.HasKitchenObject())
        {
            stoveCounter.GetKitchenObject().DestroySelf();
            if (stoveCounter is StoveCounter stove) stove.ResetStoveFromAgent();
        }
    }
}