using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class SousChefAgent : Agent, IMovable, IKitchenObjectParent
{
    [Header("Referanslar")]
    [SerializeField] private SousChefTaskManager taskManager;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float interactDistance = 1.5f;
    [SerializeField] private Transform kitchenObjectHoldPoint;
    [SerializeField] private LayerMask collisionLayerMask;

    [Header("RL Ayarları")]
    [SerializeField] private float maxSceneDistance = 15f;
    [SerializeField] private float maxEpisodeTime = 30f;
    // Boş bırakılırsa sahnedeki başlangıç pozisyonu kullanılır.
    // Birden çok nokta vermek hem genellemeyi sağlar hem de engel kaynaklı
    // yerel minimumları kırar (ajan bazen hedefe yakın doğup tamamlamayı keşfeder)
    [SerializeField] private Transform[] trainingSpawnPoints;


    private SousChefTask activeTask;
    private float episodeTimer;

    private bool isWalking;
    private float interactCooldownTimer = 0f;
    private const float INTERACT_COOLDOWN = 0.3f;


    private KitchenObject heldObject;
    private Vector3 currentMoveDir;

    //Delta Odul
    private float lastDistToTarget = float.MaxValue;

    // Etkileşim alanına ilk girişte tek seferlik ödül
    private bool hasEnteredInteractRange = false;

    // Menzil içinde interact'e basma ödülü görev başına tek sefer (farm engeli)
    private bool hasRewardedInteractPress = false;

    // Bölüm başında ajanın döneceği spawn noktası
    private Vector3 startPosition;
    private Quaternion startRotation;

    // Navigasyon güvenlik ağı (yalnızca oyunda/inference'ta, eğitimde KAPALI):
    // RL politikası ~%4 oranında duvar/köşe dolaşmayı çözemeyip titriyor/kilitleniyor;
    // hedefe yaklaşma izlenir, takılınca açık+hedefe-doğru bir yöne yönlendirilir
    private float bestDistToTarget;
    private float noProgressTimer;
    private float escapeTimer;
    private Vector3 escapeMoveDir;


    // Cook: yürüme + 3 etkileşim + kızartma süresi içerir, diğerlerinden uzun bütçe ister
    private float currentTaskTimeLimit;

    // Etkileşim mesafesi pivot yerine collider'ın en yakın noktasına ölçülür;
    // çok karoluk tezgahlarda (ocak adası) pivot 3 birimden uzak kalabiliyor
    private Collider targetCollider;

    private float DistanceToTargetXZ()
    {
        if (activeTask?.targetCounter == null) return float.MaxValue;

        Vector3 targetPoint = targetCollider != null
            ? targetCollider.ClosestPoint(transform.position)
            : activeTask.targetCounter.transform.position;

        Vector3 agentXZ = new Vector3(transform.position.x, 0, transform.position.z);
        targetPoint.y = 0;
        return Vector3.Distance(agentXZ, targetPoint);
    }

    public void SetTask(SousChefTask task)
    {
        activeTask = task;
        targetCollider = task?.targetCounter != null
            ? task.targetCounter.GetComponentInChildren<Collider>()
            : null;
        currentTaskTimeLimit = task != null && task.command == SousChefCommand.CookIngredient
            ? maxEpisodeTime * 2f
            : maxEpisodeTime;
        // Her yeni görevde sıfırla — zincirde adım değişince timer bitmemeli
        episodeTimer = 0f;
        lastDistToTarget = float.MaxValue;
        hasEnteredInteractRange = false;
        hasRewardedInteractPress = false;
        interactCooldownTimer = 0f;
        bestDistToTarget = float.MaxValue;
        noProgressTimer = 0f;
        escapeTimer = 0f;
    }

    public override void Initialize()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public override void OnEpisodeBegin()
    {
        // Pozisyon resetlenmezse sınır dışına çıkan ajan sonsuz -1 döngüsüne girer
        if (trainingSpawnPoints != null && trainingSpawnPoints.Length > 0)
        {
            Transform spawn = trainingSpawnPoints[Random.Range(0, trainingSpawnPoints.Length)];
            transform.position = spawn.position;
            transform.rotation = spawn.rotation;
        }
        else
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }

        episodeTimer = 0f;
        currentMoveDir = Vector3.zero;
        lastDistToTarget = float.MaxValue;
        hasEnteredInteractRange = false;
        hasRewardedInteractPress = false;
    }

    // ── GÖZLEMLER: 13 adet, her zaman sabit ──────────────────────
    // YÖNTEM 1: Yön vektörü VERİLMİYOR. Ajan ham koordinatlardan
    // uzamsal ilişkiyi kendi öğrenir (gerçek RL navigasyonu).

    public override void CollectObservations(VectorSensor sensor)
    {
        if (activeTask == null || activeTask.targetCounter == null)
        {
            // Görev yoksa 13 sıfır — sayı ASLA değişmemeli
            for (int i = 0; i < 13; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 agentPos = transform.position;
        Vector3 targetPos = activeTask.targetCounter.transform.position;
        Vector3 relativePos = targetPos - agentPos;

        // 1-2: Ajanın kendi pozisyonu (normalize)
        // Yön vektörü VERMİYORUZ — ajan ilişkiyi kendi kursun

        sensor.AddObservation(relativePos.x / maxSceneDistance); 
        sensor.AddObservation(relativePos.z / maxSceneDistance);

        // 3-4: Hedefin pozisyonu (normalize)
        sensor.AddObservation(targetPos.x / maxSceneDistance);
        sensor.AddObservation(targetPos.z / maxSceneDistance);

        // 5: Hedefe uzaklık (0-1 normalize)
        float dist = Vector3.Distance(agentPos, targetPos);
        sensor.AddObservation(Mathf.Clamp01(dist / maxSceneDistance));

        // 6: Etkileşim mesafesinde mi? (YÖNTEM 3 için kritik gözlem)
        // TryInteract ile AYNI ölçüm — gözlem ile eylem kapısı tutarlı olmalı
        bool canInteractNow = DistanceToTargetXZ() <= interactDistance;
        sensor.AddObservation(canInteractNow ? 1f : 0f);

        // 7: Elde malzeme var mı
        sensor.AddObservation(HasKitchenObject() ? 1f : 0f);

        // 8: Hedef tezgah dolu mu
        sensor.AddObservation(activeTask.targetCounter.HasKitchenObject() ? 1f : 0f);

        // 9-13: Aktif komut one-hot
        sensor.AddObservation(activeTask.command == SousChefCommand.FetchIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.ChopIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.CookIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.DeliverToCounter ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.Idle ? 1f : 0f);
        // Toplam: 13 gözlem
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        // SINIR DIŞI + ZAMAN AŞIMI: eğitim güvenlikleri, SADECE eğitimde geçerli.
        // Oyunda (inference) bunlar uzun navigasyon / pişirme beklemesinde zinciri
        // haksız yere keserdi; orada stuck-escape ve diğer güvenlikler yeterli.
        if (Academy.Instance.IsCommunicatorOn)
        {
            if (Mathf.Abs(transform.position.x) > 23f || Mathf.Abs(transform.position.z) > 23f)
            {
                AddReward(-1f);
                FailActiveTask();
                EndEpisode(); // hemen spawn'a dönmesi gerekiyor
                return;
            }

            // Görev düşürülür, episode'u yeni görev ataması kapatır. Görev temizlenmezse
            // ajan aynı (imkânsız olabilecek) göreve sonsuza dek yeniden başlar.
            episodeTimer += Time.fixedDeltaTime;
            if (activeTask != null && episodeTimer >= currentTaskTimeLimit)
            {
                AddReward(-0.3f); // sert değil — öğrenmeyi bloke etmesin
                FailActiveTask();
                return;
            }
        }

        // KURTARILAMAZ DURUM: yemek yandıysa görev tamamlanamaz (her iki modda da geçerli)
        if (activeTask?.command == SousChefCommand.CookIngredient
            && activeTask.targetCounter is StoveCounter burnedStove && burnedStove.IsBurned())
        {
            AddReward(-0.5f);
            FailActiveTask();
            return;
        }

        // Görev yoksa hareket etme. Model, eğitimde hiç görmediği "boş gözlem"
        // durumunda rastgele yürür; idle'a zorla (oyun başı / görevler arası / override)
        if (activeTask == null)
        {
            currentMoveDir = Vector3.zero;
            return;
        }

        // 2. HAREKET
        int moveAction = actions.DiscreteActions[0];
        currentMoveDir = Vector3.zero;
        switch (moveAction)
        {
            case 1: currentMoveDir = Vector3.forward; break;
            case 2: currentMoveDir = Vector3.back; break;
            case 3: currentMoveDir = Vector3.left; break;
            case 4: currentMoveDir = Vector3.right; break;
        }

        // Hedefe varıldıysa (etkileşim menzilinde) navigasyona gerek yok — hareketi
        // bastır ki beklerken/keserken/etkileşimde stokastik örnekleme titremesi
        // (sağ-sol) olmasın. Uzaktayken (navigasyon) etkilenmez.
        if (DistanceToTargetXZ() <= interactDistance)
            currentMoveDir = Vector3.zero;

        // OYUNDA: pişen yemeği yanmadan OTOMATİK al — interact basışını bekleme,
        // böylece "piştikten sonra geç basınca yanma" penceresi tamamen kapanır.
        // RL'in yaptığı kısımlar (gitme/koyma/başlatma) korunur; sadece toplama güvenceye alınır.
        if (!Academy.Instance.IsCommunicatorOn
            && activeTask.command == SousChefCommand.CookIngredient
            && DistanceToTargetXZ() <= interactDistance
            && TryAutoCollectFried())
            return;

        // 3. ETKİLEŞİM (YÖNTEM 3: kararı ajan verir, doğru zamanlama ödüllenir)
        int interactAction = actions.DiscreteActions[1];
        if (interactAction == 1 && activeTask?.targetCounter != null)
        {
            if (DistanceToTargetXZ() <= interactDistance)
            {
                // Görev başına TEK SEFER — yoksa menzilde spamlemek farm edilir
                if (!hasRewardedInteractPress)
                {
                    Debug.Log("[Agent] Etkileşim mesafesinde +0.3");
                    AddReward(0.3f);
                    hasRewardedInteractPress = true;
                }
            }
            // Uzakta basmaya ceza YOK — anlık ceza, ajan menzildeki +0.3/+1'i
            // keşfedemeden "interact'e asla basma" politikasına çökmesine yol açıyor
        }
        if (interactAction == 1) TryInteract();
        // 4. SÜREÇ ÖDÜLLERİ
        ApplyProcessRewards();
    }

    private void ApplyProcessRewards()
    {
        // A. Adım cezası — daha küçük, öğrenmeyi engellemesin
        AddReward(-0.0001f);

        if (activeTask?.targetCounter == null) return;

        float dist = Vector3.Distance(transform.position,
                                      activeTask.targetCounter.transform.position);

        // İlk adımda referans mesafeyi kur — yoksa MaxValue ile dev delta
        // oluşur ve tüm yaklaşma bütçesi bedavaya tükenir
        if (lastDistToTarget == float.MaxValue)
        {
            lastDistToTarget = dist;
            return;
        }

        // B. Potansiyel tabanlı shaping: yaklaşmak +, uzaklaşmak −.
        // Simetrik olduğu için gidiş-dönüş net 0 → farm imkânsız, cap gereksiz.
        // Tek yönlü+cap'li eski sürümde bütçe tükenince hedefe çekim kayboluyor,
        // ajan uzun görevlerde (Cook) hedefin dibinden geri dönüp geziniyordu
        AddReward((lastDistToTarget - dist) * 0.05f);
        lastDistToTarget = dist;

        // C. Etkileşim alanına ilk girişte tek seferlik ödül (gate ile aynı ölçüm)
        if (DistanceToTargetXZ() <= interactDistance && !hasEnteredInteractRange)
        {
            AddReward(0.05f);
            hasEnteredInteractRange = true;
        }
        // else if (dist > interactDistance + 0.5f)
       // {   hasEnteredInteractRange = false; // Uzaklaşırsa sıfırla }
    }


    // ── FİZİKSEL HAREKET ──────────────────────────────────────────
    // Kararlar fizik adımında alındığı için hareket de FixedUpdate'te;
    // Update'te kalırsa eğitimdeki time-scale ile davranış kayar
    private void FixedUpdate()
    {
        if (interactCooldownTimer > 0f)
            interactCooldownTimer -= Time.fixedDeltaTime;

        // Politika duvarda/köşede takılırsa currentMoveDir'i kaçış yönüyle ezer
        if (activeTask != null)
            ApplyStuckEscape();

        float moveDistance = moveSpeed * Time.fixedDeltaTime;
        float playerRadius = 0.5f;
        float playerHeight = 2f;              // 0.5f'ti, 2f yap
        float castDistance = moveDistance + 0.5f;  // fazladan kontrol payı

        bool canMove = !Physics.CapsuleCast(
            transform.position,
            transform.position + Vector3.up * playerHeight,
            playerRadius, currentMoveDir, castDistance, collisionLayerMask);

        if (!canMove)
        {
            Vector3 moveDirX = new Vector3(currentMoveDir.x, 0, 0).normalized;
            canMove = currentMoveDir.x != 0 && !Physics.CapsuleCast(
                transform.position, transform.position + Vector3.up * playerHeight,
                playerRadius, moveDirX, moveDistance, collisionLayerMask);

            if (canMove) currentMoveDir = moveDirX;
            else
            {
                Vector3 moveDirZ = new Vector3(0, 0, currentMoveDir.z).normalized;
                canMove = currentMoveDir.z != 0 && !Physics.CapsuleCast(
                    transform.position, transform.position + Vector3.up * playerHeight,
                    playerRadius, moveDirZ, moveDistance, collisionLayerMask);
                if (canMove) currentMoveDir = moveDirZ;
            }
        }

        if (canMove)
            transform.position += currentMoveDir * moveDistance;

        // İSTENEN yöne değil GERÇEK harekete bağla — engele dayanıp ilerleyemezken
        // (canMove false) ya da beklerken yürüme animasyonu oynamasın
        isWalking = canMove && currentMoveDir != Vector3.zero;

        Vector3 lookDir = currentMoveDir != Vector3.zero
            ? currentMoveDir
            : (activeTask?.targetCounter != null
                ? activeTask.targetCounter.transform.position - transform.position
                : Vector3.zero);

        lookDir.y = 0;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir), rotationSpeed * Time.fixedDeltaTime);
    }

    // Yalnızca oyunda devreye girer; eğitimde ajan navigasyonu kendi öğrenmeli
    private void ApplyStuckEscape()
    {
        if (Academy.Instance.IsCommunicatorOn) return; // eğitim sırasında kapalı

        float distNow = DistanceToTargetXZ();

        // Hedefe varmışsa hareket beklenmez (ocakta bekleme vb.) — kilitlenme sayma
        if (distNow <= interactDistance)
        {
            bestDistToTarget = distNow;
            noProgressTimer = 0f;
            escapeTimer = 0f;
            return;
        }

        if (bestDistToTarget == float.MaxValue) bestDistToTarget = distNow;

        // Kaçış sürüyor: seçilen tek yöne KARARLI kay; hedefe belirgin yaklaşınca bitir
        if (escapeTimer > 0f)
        {
            escapeTimer -= Time.fixedDeltaTime;
            currentMoveDir = escapeMoveDir;
            if (distNow < bestDistToTarget - 0.3f)
            {
                bestDistToTarget = distNow;
                noProgressTimer = 0f;
                escapeTimer = 0f;
            }
            return;
        }

        // İLERLEME = hedefe YAKLAŞMA (sadece "hareket etti" değil) — böylece
        // duvarda bir sağ bir sol titreyen "gezinme/sıkışma" da yakalanır
        if (distNow < bestDistToTarget - 0.05f)
        {
            bestDistToTarget = distNow;
            noProgressTimer = 0f;
        }
        else
        {
            noProgressTimer += Time.fixedDeltaTime;
            if (noProgressTimer > 1.2f) // 1.2 sn hedefe yaklaşamadı → kilitli/gezgin
            {
                escapeMoveDir = ChooseEscapeDirection();
                escapeTimer = 1.5f;
                noProgressTimer = 0f;
            }
        }
    }

    // 4 ana yön içinde AÇIK olanlar arasından hedefe en çok yaklaştıranı seç —
    // engelin/duvarın kenarı boyunca amaçlı kayma gibi görünür, rastgele savrulma değil
    private Vector3 ChooseEscapeDirection()
    {
        Vector3 toTarget = activeTask.targetCounter.transform.position - transform.position;
        toTarget.y = 0;
        toTarget.Normalize();

        Vector3 best = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (Vector3 dir in dirs)
        {
            bool blocked = Physics.CapsuleCast(
                transform.position, transform.position + Vector3.up * 2f,
                0.5f, dir, 1f, collisionLayerMask);
            if (blocked) continue;

            float score = Vector3.Dot(dir, toTarget);
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        return best; // hepsi bloksa zero (bir sonraki karede tekrar denenir)
    }

    // ── ETKİLEŞİM ─────────────────────────────────────────────────
    private void TryInteract()
    {
        if (interactCooldownTimer > 0f) return;
        if (activeTask == null || activeTask.targetCounter == null) return;

        if (DistanceToTargetXZ() > interactDistance) return;

        Debug.Log($"[Agent] Etkileşim: {activeTask.command}");

        switch (activeTask.command)
        {
            case SousChefCommand.FetchIngredient: HandleFetch(); break;
            case SousChefCommand.ChopIngredient: HandleChop(); break;
            case SousChefCommand.CookIngredient: HandleCook(); break;
            case SousChefCommand.DeliverToCounter: HandleDeliver(); break;
        }

        interactCooldownTimer = INTERACT_COOLDOWN;
    }


    private void HandleFetch()
    {
        Debug.Log($"[Fetch] Deneniyor. Eli boş mu: {!HasKitchenObject()}, Hedef: {activeTask?.targetCounter?.name}");
        if (!HasKitchenObject() && activeTask?.targetCounter != null)
        {
            activeTask.targetCounter.InteractFromAgent(this);
            Debug.Log($"[Fetch] InteractFromAgent sonrası eli dolu mu: {HasKitchenObject()}");
            if (HasKitchenObject())
                CompleteAgentTask();
            else
                Debug.LogWarning("[Fetch] Eşya alınamadı!");
        }
    }

    private void HandleChop()
    {
        if (activeTask?.targetCounter is CuttingCounter cut && cut.HasKitchenObject())
        {
            cut.InteractAlternate(null);
            if (cut.IsFullyCut())
                CompleteAgentTask();
            else
                AddReward(0.05f); // her geçerli kesme ilerlemesi — çok adımlı görevde ara sinyal
        }
    }

    private void HandleCook()
    {
        if (activeTask.targetCounter is not StoveCounter stove) return;

        if (!stove.HasKitchenObject() && HasKitchenObject())
        {
            if (!stove.CanFry(GetKitchenObject().GetKitchenObjectSO()))
            {
                Debug.LogError($"[Cook] '{stove.name}' ocağı '{GetKitchenObject().GetKitchenObjectSO().name}' " +
                               "malzemesini pişiremiyor — ocağın FryingRecipeSOArray'ine bu malzemenin tarifi ekli mi?");
                return;
            }
            stove.InteractFromAgent(this);
            // Malzeme ocağa kondu mu? (Tarif tutmadıysa elinde kalır)
            if (!HasKitchenObject()) AddReward(0.2f);
            return;
        }
        if (stove.HasKitchenObject() && stove.IsIdle() && !HasKitchenObject())
        {
            stove.InteractAlternate(null);
            AddReward(0.2f); // pişirme başlatıldı — ara sinyal
            return;
        }
        if (stove.HasKitchenObject() && stove.IsFried() && !HasKitchenObject())
        {
            stove.GetKitchenObject().SetKitchenObjectParent(this);
            stove.ResetStoveFromAgent();
            CompleteAgentTask();
        }
    }

    // Oyunda pişen yemeği yanmadan toplamak için otomatik tetikleme.
    // Topladıysa true döner (o adımın geri kalanı atlanır).
    private bool TryAutoCollectFried()
    {
        if (activeTask?.targetCounter is StoveCounter stove
            && stove.HasKitchenObject() && stove.IsFried() && !HasKitchenObject())
        {
            stove.GetKitchenObject().SetKitchenObjectParent(this);
            stove.ResetStoveFromAgent();
            CompleteAgentTask();
            return true;
        }
        return false;
    }

    // SousChefAgent.cs
    private void HandleDeliver()
    {
        if (!HasKitchenObject() || activeTask?.targetCounter == null) return;

        activeTask.targetCounter.InteractFromAgent(this);

        // Normal bırakma: eli boşaldı
        bool normalDrop = !HasKitchenObject();

        // Tabak birleştirme: ajan elinde hâlâ tabak var
        // ama tezgah artık boş (malzeme tabağa girdi, tezgahtan silindi)
        bool plateMerge = HasKitchenObject()
                          && GetKitchenObject() is PlateKitchenObject
                          && !activeTask.targetCounter.HasKitchenObject();

        if (normalDrop || plateMerge)
        {
            CompleteAgentTask();
            return;
        }

        // FAZLALIK MALZEME: ajan bir malzemeyi tabağa eklemeye çalıştı ama o malzeme
        // tabakta ZATEN var (örn. oyuncu eklemiş). Birleştirme reddedildi, malzeme elde kaldı.
        // Hedef (malzeme tabakta) zaten gerçekleşmiş → fazlalığı at, görevi tamamlanmış say.
        // Zincir böylece sıradaki eksik malzemeye geçer.
        if (HasKitchenObject() && GetKitchenObject() is not PlateKitchenObject
            && activeTask.targetCounter.GetKitchenObject() is PlateKitchenObject targetPlate
            && targetPlate.GetKitchenObjectSOList().Contains(GetKitchenObject().GetKitchenObjectSO()))
        {
            Debug.Log("[Deliver] Malzeme tabakta zaten var → fazlalık atılıyor, sıradaki malzemeye geçiliyor.");
            GetKitchenObject().DestroySelf();
            CompleteAgentTask();
            return;
        }

        Debug.LogWarning("[Deliver] Eşya bırakılamadı veya birleştirme başarısız.");

        FailActiveTask();
    }


    private void FailActiveTask()
    {
        // Görevler arası boşlukta (örn. sınır dışı) görev null olabilir
        if (activeTask != null)
            RecordTaskResult(activeTask.command, 0f);
        activeTask = null;
        taskManager.OnTaskFailed(); // TrainingManager yeni görev atar; episode orada kapanır
    }

    // TensorBoard'da görev tipi başına başarı oranı (Tasks/... grafikleri)
    private void RecordTaskResult(SousChefCommand command, float success)
    {
        Academy.Instance.StatsRecorder.Add(
            $"Tasks/{command}Success", success, StatAggregationMethod.Average);
    }

    private void CompleteAgentTask()
    {
        Debug.Log("[Agent] ✅ GÖREV TAMAMLANDI! +1 ödül");
        RecordTaskResult(activeTask.command, 1f);
        AddReward(1f);
        activeTask = null;
        taskManager.OnTaskCompleted();
        interactCooldownTimer = 0f;
        hasEnteredInteractRange = false;
        lastDistToTarget = float.MaxValue;
    }

    public void ForceCancelTask()
    {
        if (activeTask != null)
        {

            // Ajanın elinde malzeme kaldıysa, takılmaması için malzemeyi yok et
            if (HasKitchenObject())
            {
                GetKitchenObject().DestroySelf();
                ClearKitchenObject();
            }

            FailActiveTask();
        }
    }


    // ── HEURİSTİC ─────────────────────────────────────────────────
    // NOT: Behavior Type Default'ta bu metod çağrılmaz — sadece Heuristic Only'de aktif
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> d = actionsOut.DiscreteActions;
        d[0] = 0;
        d[1] = 0;

        // Otopilot — Heuristic Only modda test için
        if (activeTask != null && activeTask.targetCounter != null)
        {
            Vector3 dirToTarget = activeTask.targetCounter.transform.position
                                  - transform.position;
            dirToTarget.y = 0;

            if (activeTask.command != SousChefCommand.ChopIngredient
                && dirToTarget != Vector3.zero)
                transform.forward = dirToTarget.normalized;

            float distance = DistanceToTargetXZ();

            if (distance > interactDistance)
            {
                bool preferX = Mathf.Abs(dirToTarget.x) > Mathf.Abs(dirToTarget.z);
                float r = 0.5f;
                float h = 0.5f;
                Vector3 testX = new Vector3(dirToTarget.x > 0 ? 1 : -1, 0, 0);
                Vector3 testZ = new Vector3(0, 0, dirToTarget.z > 0 ? 1 : -1);
                bool canX = !Physics.CapsuleCast(transform.position,
                    transform.position + Vector3.up * h, r, testX, 0.5f, collisionLayerMask);
                bool canZ = !Physics.CapsuleCast(transform.position,
                    transform.position + Vector3.up * h, r, testZ, 0.5f, collisionLayerMask);

                if (preferX)
                {
                    if (canX) d[0] = dirToTarget.x > 0 ? 4 : 3;
                    else if (canZ) d[0] = dirToTarget.z > 0 ? 1 : 2;
                }
                else
                {
                    if (canZ) d[0] = dirToTarget.z > 0 ? 1 : 2;
                    else if (canX) d[0] = dirToTarget.x > 0 ? 4 : 3;
                }
            }
            else
            {
                d[0] = 0;
                d[1] = 1;
            }
            return; // Otopilot aktifse klavyeye bakma
        }

        // Manuel klavye — görev yoksa
        if (Keyboard.current != null)
        {
            if (Keyboard.current.iKey.isPressed) d[0] = 1;
            if (Keyboard.current.kKey.isPressed) d[0] = 2;
            if (Keyboard.current.jKey.isPressed) d[0] = 3;
            if (Keyboard.current.lKey.isPressed) d[0] = 4;
            if (Keyboard.current.tKey.isPressed) d[1] = 1;
        }
    }


    // interface
    public bool IsWalking() => isWalking;
    public Transform GetKitchenObjectFollowTransform() => kitchenObjectHoldPoint;
    public void SetKitchenObject(KitchenObject obj) => heldObject = obj;
    public KitchenObject GetKitchenObject() => heldObject;
    public void ClearKitchenObject() => heldObject = null;
    public bool HasKitchenObject() => heldObject != null;
}