using UnityEngine;
using System.Collections;

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

    // Override sonrası: adımı İLERLETMEDEN mevcut adımı yeniden ver
    // (hedefler resolveTarget ile o anki duruma göre yeniden çözülür)
    public virtual void ResumeCurrentStep()
    {
        if (!IsRunning()) return;
        Debug.Log($"[Chain] ▶ Devam: adım {currentStep} yeniden veriliyor");
        ExecuteStep(currentStep);
    }

    // Sıraya alınmış override için: mevcut adım bitti say, ama bir sonrakini ÇALIŞTIRMA
    // (önce override koşacak, o bitince ResumeCurrentStep bu adımı verecek)
    public virtual void AdvanceStepOnly()
    {
        if (!IsRunning()) return;
        currentStep++;
        Debug.Log($"[Chain] Adım ilerletildi (çalıştırılmadan) → {currentStep}");
    }

    // Bir makro araya girip yarıda kestikten sonra ana zincir buradan devam eder.
    // Varsayılan: mevcut adımı yeniden ver. RecipeChain bunu ezip duruma göre
    // (tabağın içeriğine göre) baştan planlar — çünkü makro el durumunu değiştirmiş olabilir.
    public virtual void ResumeFromState()
    {
        ResumeCurrentStep();
    }

    public bool IsRunning() => currentStep >= 0;

    // Override ürününü bırakmak için en yakın boş tezgah (TaskManager kullanır)
    public BaseCounter FindDropCounter() => FindNearest<ClearCounter>(c => !c.HasKitchenObject());

    // Bir adımın hedefi (örn. boş tezgah) ŞU AN müsait değilse: İPTAL ETME, açılana
    // kadar BEKLE ve tekrar dene. Ajan bu sırada (varsa elindekiyle) boşta durur.
    // Tüm tezgahlar dolu → bir yer boşalınca zincir kaldığı adımdan devam eder.
    protected void GiveWhenAvailable(int step, SousChefCommand cmd,
                                     System.Func<BaseCounter> resolver, string label)
    {
        BaseCounter target = resolver();
        if (target != null)
        {
            Debug.Log($"[Chain] {label} → {target.name}");
            taskManager.GiveCommand(cmd, target);
            return;
        }
        Debug.Log($"[Chain] Hedef müsait değil, açılması bekleniyor: {label}");
        StartCoroutine(WaitThenGive(step, cmd, resolver, label));
    }

    private IEnumerator WaitThenGive(int step, SousChefCommand cmd,
                                    System.Func<BaseCounter> resolver, string label)
    {
        // Adım değişmediği (zincir hâlâ bu adımda) ve çalışır olduğu sürece bekle
        while (IsRunning() && currentStep == step)
        {
            yield return new WaitForSeconds(0.5f);
            if (!IsRunning() || currentStep != step) yield break;

            BaseCounter target = resolver();
            if (target != null)
            {
                Debug.Log($"[Chain] Hedef açıldı → {label} → {target.name}");
                taskManager.GiveCommand(cmd, target);
                yield break;
            }
        }
    }

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