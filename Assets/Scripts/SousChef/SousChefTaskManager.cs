using UnityEngine;
using System;
using System.Collections;
public class SousChefTaskManager : MonoBehaviour
{
    public static SousChefTaskManager Instance { get; private set; }

    public event EventHandler OnTaskChanged;

    [Header("Referanslar")]
    [SerializeField] private SousChefAgent agent;

    private SousChefTask activeTask;
    private SousChefChainBase activeChain;

    // Override sırasında zincir iptal edilmez, duraklatılır; bu bayrak açıkken
    // tamamlanan görev "araya giren override" demektir → zincir kaldığı adımdan devam eder
    private bool chainPausedForOverride;

    // Ajan elinde malzeme taşırken gelen override hemen kesilmez, SIRAYA alınır;
    // mevcut görev bitip eli boşalınca çalıştırılır (el-durumu çelişkisini önler)
    private BaseCounter pendingOverrideCounter;

    // Ajan eli doluyken makro zincir başlatılırsa, önce elindekini bıraksın diye
    // zincir başlatma ertelenir; bırakma bitince başlatılır
    private SousChefChainBase pendingChain;

    // Bir makro (Otonom Doğrama/Pişirme) çalışan ana zinciri (RecipeChain) yarıda
    // keserse, ana zincir iptal edilmez; burada saklanır ve makro bitince geri yüklenip
    // tabağın durumuna göre kaldığı yerden devam ettirilir
    private SousChefChainBase interruptedChain;

    private void Awake()
    {
        Instance = this;
    }

    //polimorfizm
    public void AssignTaskBasedOnContext(BaseCounter clickedCounter)
    {
        // Zincir çalışıyor VE ajan elinde bir şey taşıyorsa: override'ı HEMEN kesme,
        // SIRAYA al. Ajan mevcut atomik görevini bitirip elindekini bıraksın, SONRA
        // override çalışsın. Yoksa eldeki malzeme zincirle çelişip ajanı takıyor.
        if (activeChain != null && activeChain.IsRunning() && agent.HasKitchenObject())
        {
            pendingOverrideCounter = clickedCounter;
            Debug.Log("[TaskManager] ⏳ Override sıraya alındı — ajan elindekini bitirince yapılacak.");
            return;
        }

        SousChefTask newTask = clickedCounter.GetTaskForAgent(agent);

        if (newTask == null)
        {
            // Tıklanan tezgah, ajanın o anki el durumuna uygun görev veremiyor.
            // Mevcut görevi/zinciri BOZMA — geçersiz tıklamayı yok say, ajan işine devam etsin.
            Debug.Log("[TaskManager] Bu tezgah için uygun override yok — mevcut görev sürüyor.");
            return;
        }

        // Zincir çalışıyorsa İPTAL etme, DURAKLAT — override bitince kaldığı adımdan devam edecek
        if (activeChain != null && activeChain.IsRunning())
        {
            chainPausedForOverride = true;
            Debug.Log("[TaskManager] ⏸ Zincir duraklatıldı, override yapılıyor.");
        }

        GiveCommand(newTask.command, newTask.targetCounter, newTask.targetItemSO);
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

        // Zincir öncesi "elindekini bırak" görevi bitti → bekleyen makro zinciri başlat
        if (pendingChain != null)
        {
            SousChefChainBase c = pendingChain;
            pendingChain = null;
            StartChainNow(c);
            return;
        }

        // TUTARLILIK KORUMASI: override bayrakları var ama zincir artık çalışmıyorsa
        // (iptal/bitti) bunlar bayattır → temizle. Yoksa sonraki alakasız bir görev
        // tamamlanınca yanlışlıkla "override resume / deferred override" sanılabilir.
        if ((chainPausedForOverride || pendingOverrideCounter != null)
            && (activeChain == null || !activeChain.IsRunning()))
        {
            chainPausedForOverride = false;
            pendingOverrideCounter = null;
        }

        // (a) Biten görev araya giren override'dı → zinciri kaldığı adımdan DEVAM ettir
        if (chainPausedForOverride && activeChain != null && activeChain.IsRunning())
        {
            // Override ürünü elde kaldıysa (örn. "köfte al") zincirin sonraki adımıyla
            // çelişmesin diye önce boş tezgaha BIRAKTIR; bırakma bitince tekrar buraya
            // gelir, bu kez eli boş olur ve zincir devam eder.
            if (agent.HasKitchenObject())
            {
                BaseCounter drop = activeChain.FindDropCounter();
                if (drop != null)
                {
                    Debug.Log("[TaskManager] Override ürünü elde kaldı → boş tezgaha bırakılıyor, sonra devam.");
                    GiveCommand(SousChefCommand.DeliverToCounter, drop); // chainPausedForOverride hâlâ true
                    return;
                }
                // Boş tezgah YOK → açılana kadar BEKLE (ajan elinde tutarak boşta durur).
                // Bir tezgah boşalınca bırakıp zincire devam edecek (çelişkiyi zorlama).
                Debug.Log("[TaskManager] Bırakacak boş tezgah yok → açılması bekleniyor.");
                StartCoroutine(WaitForFreeCounterThenDrop());
                return;
            }

            chainPausedForOverride = false;
            Debug.Log("[TaskManager] ▶ Override bitti, zincir kaldığı adımdan devam ediyor.");
            activeChain.ResumeCurrentStep();
            return;
        }

        // (b) Sıraya alınmış override var → ajan mevcut zincir görevini yeni bitirdi (eli boşaldı):
        //     adımı ilerletmiş say, ÖNCE override'ı çalıştır, o bitince zincir devam edecek
        if (pendingOverrideCounter != null && activeChain != null && activeChain.IsRunning())
        {
            BaseCounter c = pendingOverrideCounter;
            pendingOverrideCounter = null;
            activeChain.AdvanceStepOnly(); // mevcut adım bitti, ama bir sonrakini henüz çalıştırma

            SousChefTask ovr = c.GetTaskForAgent(agent);
            if (ovr != null)
            {
                chainPausedForOverride = true;
                Debug.Log("[TaskManager] ▶ Sıradaki override çalıştırılıyor.");
                GiveCommand(ovr.command, ovr.targetCounter, ovr.targetItemSO);
            }
            else
            {
                // override artık geçersiz → zinciri ilerlemiş adımdan sürdür
                activeChain.ResumeCurrentStep();
            }
            return;
        }

        // (c) Normal zincir ilerlemesi — GiveCommand yeni activeTask'ı atar
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

    // Zaman aşımı / kurtarılamaz durum: görevi düşür ki TrainingManager yenisini atayabilsin
    public void OnTaskFailed()
    {
        // Araya giren override başarısız olduysa zinciri iptal etme, devam ettir
        if (chainPausedForOverride && activeChain != null && activeChain.IsRunning())
        {
            chainPausedForOverride = false;
            Debug.Log("[TaskManager] ▶ Override başarısız — zincir kaldığı adımdan devam ediyor.");
            activeChain.ResumeCurrentStep();
            return;
        }

        Debug.Log("[TaskManager] ❌ Görev başarısız — yeni görev atanacak");
        if (activeChain != null && activeChain.IsRunning())
        {
            activeChain.Cancel();
            activeChain = null;
        }
        pendingOverrideCounter = null;
        pendingChain = null;
        activeTask = null;

        // Bir makro yarıda kalan ana zinciri saklamışsa, onu geri yükleyip devam ettir
        if (interruptedChain != null)
        {
            SousChefChainBase resume = interruptedChain;
            interruptedChain = null;
            activeChain = resume;
            Debug.Log("[TaskManager] ▶ Makro başarısız — ana zincir geri yüklendi.");
            resume.ResumeFromState();
            return;
        }

        OnTaskChanged?.Invoke(this, EventArgs.Empty);
    }

    // Override ürününü bırakacak boş tezgah yokken: bir tezgah boşalana kadar bekler,
    // boşalınca bırakma komutunu verir (bu da tamamlanınca zincir kaldığı yerden devam eder).
    // Beklerken ajanın görevi yoktur → boşta durur (elinde malzemeyle).
    private IEnumerator WaitForFreeCounterThenDrop()
    {
        while (activeChain != null && activeChain.IsRunning() && agent.HasKitchenObject())
        {
            BaseCounter drop = activeChain.FindDropCounter();
            if (drop != null)
            {
                Debug.Log("[TaskManager] Boş tezgah açıldı → elindeki bırakılıyor.");
                GiveCommand(SousChefCommand.DeliverToCounter, drop);
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
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

        // Çalışan bir ANA zincir (RecipeChain) varsa İPTAL etme — sakla. Makro bitince
        // geri yüklenip tabağın durumuna göre kaldığı yerden devam edecek.
        if (activeChain != null && activeChain.IsRunning() && activeChain != newChain)
        {
            if (interruptedChain == null) interruptedChain = activeChain;
            else activeChain.Cancel(); // zaten bekleyen bir ana zincir varsa aradakini iptal et
            activeChain = null;
        }

        // Zincirler eli BOŞ başlamayı varsayar (ilk adım = kasadan al). Ajan elinde
        // bir şey tutuyorsa, önce boş tezgaha bıraksın, SONRA zincir başlasın.
        if (agent.HasKitchenObject())
        {
            BaseCounter drop = newChain.FindDropCounter();
            if (drop != null)
            {
                chainPausedForOverride = false;
                pendingOverrideCounter = null;
                pendingChain = newChain;
                Debug.Log("[TaskManager] Zincir öncesi: ajan elindekini bırakıyor, sonra başlayacak.");
                GiveCommand(SousChefCommand.DeliverToCounter, drop);
                return;
            }
            // Boş tezgah yoksa son çare: elindekini temizle ki zincir takılmasın
            agent.GetKitchenObject().DestroySelf();
            agent.ClearKitchenObject();
        }

        StartChainNow(newChain);
    }

    private void StartChainNow(SousChefChainBase newChain)
    {
        if (activeChain != null && activeChain.IsRunning())
            activeChain.Cancel();

        activeChain = newChain;
        chainPausedForOverride = false;
        pendingOverrideCounter = null;
        activeChain.Initialize(this);
        activeChain.StartChain();

        Debug.Log($"[TaskManager] Yeni zincir başladı: {newChain.GetType().Name}");
    }

    public void OnChainCompleted()
    {
        Debug.Log("[TaskManager] Makro Zincir Tamamlandı!");
        activeChain = null;
        chainPausedForOverride = false;
        pendingOverrideCounter = null;
        pendingChain = null;
        activeTask = null;

        // Bu makro bir ana zinciri yarıda kesmişti → geri yükle ve duruma göre devam ettir
        if (interruptedChain != null)
        {
            SousChefChainBase resume = interruptedChain;
            interruptedChain = null;
            activeChain = resume;
            Debug.Log("[TaskManager] ▶ Ana zincir geri yüklendi, kaldığı yerden devam ediyor.");
            resume.ResumeFromState();
            return;
        }

        OnTaskChanged?.Invoke(this, EventArgs.Empty);
    }
}