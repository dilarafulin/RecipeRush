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


    private SousChefTask activeTask;
    private float episodeTimer;

    private bool isWalking;
    private float interactCooldownTimer = 0f;
    private const float INTERACT_COOLDOWN = 0.3f;


    private KitchenObject heldObject;
    private Vector3 currentMoveDir;

    //Delta Odul
    private float lastDistToTarget = float.MaxValue;
    // Delta ödülünün toplamını sınırlamak için
    private float totalApproachReward = 0f;
    private const float MAX_APPROACH_REWARD = 0.3f; // Yaklaşma ödülü max bu kadar

    // Etkileşim alanına ilk girişte tek seferlik ödül
    private bool hasEnteredInteractRange = false;


    public void SetTask(SousChefTask task)
    {
        activeTask = task;
        // Her yeni görevde sıfırla — zincirde adım değişince timer bitmemeli
        episodeTimer = 0f;
        lastDistToTarget = float.MaxValue;
        totalApproachReward = 0f;
        hasEnteredInteractRange = false;
        interactCooldownTimer = 0f;
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        currentMoveDir = Vector3.zero;
        lastDistToTarget = float.MaxValue;
        totalApproachReward = 0f;
        hasEnteredInteractRange = false;
    }

    // ── GÖZLEMLER: 11 adet, her zaman sabit ──────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        if (activeTask == null || activeTask.targetCounter == null)
        {
            // Görev yoksa 11 sıfır — sayı ASLA değişmemeli
            for (int i = 0; i < 11; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 agentPos = transform.position;
        Vector3 targetPos = activeTask.targetCounter.transform.position;

        // 1-3: Hedefe normalize yön vektörü
        Vector3 dirToTarget = (targetPos - agentPos).normalized;
        sensor.AddObservation(dirToTarget); // x, y, z → 3 değer

        // 4: Hedefe uzaklık (0-1 normalize, sahne boyutuna göre)
        float dist = Vector3.Distance(agentPos, targetPos);
        sensor.AddObservation(Mathf.Clamp01(dist / maxSceneDistance));

        // 5: Elde malzeme var mı
        sensor.AddObservation(HasKitchenObject() ? 1f : 0f);

        // 6: Hedef tezgah dolu mu
        sensor.AddObservation(activeTask.targetCounter.HasKitchenObject() ? 1f : 0f);

        // 7-11: Aktif komut one-hot — ajan ne yapacağını bilsin
        sensor.AddObservation(activeTask.command == SousChefCommand.FetchIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.ChopIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.CookIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.DeliverToCounter ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.Idle ? 1f : 0f);

        // Toplam: 11 gözlem
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. ZAMAN AŞIMI
        episodeTimer += Time.fixedDeltaTime;
        if (episodeTimer >= maxEpisodeTime)
        {
            // Sert ceza değil — öğrenmeyi bloke etmesin
            AddReward(-0.3f);
            EndEpisode();
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

        // 3. ETKİLEŞİM
        int interactAction = actions.DiscreteActions[1];

        // Uzakta E'ye basarsa küçük ceza — "önce yaklaş" öğrensin
        if (interactAction == 1 && activeTask?.targetCounter != null)
        {
            float quickDist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(activeTask.targetCounter.transform.position.x, 0,
                            activeTask.targetCounter.transform.position.z));
            if (quickDist > interactDistance)
                AddReward(-0.005f);
        }

        if (interactAction == 1) TryInteract();

        // 4. SÜREÇ ÖDÜLLERİ
        ApplyProcessRewards();
    }

    private void ApplyProcessRewards()
    {
        // A. Adım cezası — daha küçük, öğrenmeyi engellemesin
        AddReward(-0.0005f);

        if (activeTask?.targetCounter == null) return;

        float dist = Vector3.Distance(transform.position,
                                      activeTask.targetCounter.transform.position);

        // B. Delta ödülü — SINIRLIYIZ, farming engellenir
        if (totalApproachReward < MAX_APPROACH_REWARD)
        {
            float distanceDelta = lastDistToTarget - dist;
            if (distanceDelta > 0.01f)
            {
                float reward = distanceDelta * 0.05f; // Katsayı düşürüldü
                float clampedReward = Mathf.Min(reward,
                                                MAX_APPROACH_REWARD - totalApproachReward);
                AddReward(clampedReward);
                totalApproachReward += clampedReward;
            }
        }
        lastDistToTarget = dist;

        // C. Etkileşim alanına ilk girişte tek seferlik ödül
        if (dist <= interactDistance && !hasEnteredInteractRange)
        {
            AddReward(0.05f);
            hasEnteredInteractRange = true;
        }
        else if (dist > interactDistance + 0.5f)
        {
            hasEnteredInteractRange = false; // Uzaklaşırsa sıfırla
        }
    }


    // ── FİZİKSEL HAREKET ──────────────────────────────────────────
    private void Update()
    {
        if (interactCooldownTimer > 0f)
            interactCooldownTimer -= Time.deltaTime;

        float moveDistance = moveSpeed * Time.deltaTime;
        float playerRadius = 0.5f;
        float playerHeight = 0.5f;

        bool canMove = !Physics.CapsuleCast(
            transform.position,
            transform.position + Vector3.up * playerHeight,
            playerRadius, currentMoveDir, moveDistance, collisionLayerMask);

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

        isWalking = currentMoveDir != Vector3.zero;

        Vector3 lookDir = currentMoveDir != Vector3.zero
            ? currentMoveDir
            : (activeTask?.targetCounter != null
                ? activeTask.targetCounter.transform.position - transform.position
                : Vector3.zero);

        lookDir.y = 0;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir), rotationSpeed * Time.deltaTime);
    }

    // ── ETKİLEŞİM ─────────────────────────────────────────────────
    private void TryInteract()
    {
        if (interactCooldownTimer > 0f) return;
        if (activeTask == null || activeTask.targetCounter == null) return;

        Vector3 agentXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetXZ = new Vector3(activeTask.targetCounter.transform.position.x, 0,
                                       activeTask.targetCounter.transform.position.z);
        float dist = Vector3.Distance(agentXZ, targetXZ);

        if (dist > interactDistance) return;

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
        if (!HasKitchenObject() && activeTask?.targetCounter != null)
        {
            activeTask.targetCounter.InteractFromAgent(this);
            if (HasKitchenObject())
            {
                CompleteAgentTask();
            }
            else
            {
                Debug.LogWarning("[Agent] Eşya alınamadı — Counter kodunu kontrol et.");
            }
        }
    }

    private void HandleChop()
    {
        if (activeTask?.targetCounter is CuttingCounter cut && cut.HasKitchenObject())
        {
            cut.InteractAlternate(null);
            if (cut.IsFullyCut()) CompleteAgentTask();
        }
    }

    private void HandleCook()
    {
        if (activeTask.targetCounter is not StoveCounter stove) return;

        if (!stove.HasKitchenObject() && HasKitchenObject())
        {
            stove.InteractFromAgent(this);
            return;
        }
        if (stove.HasKitchenObject() && stove.IsIdle() && !HasKitchenObject())
        {
            stove.InteractAlternate(null);
            return;
        }
        if (stove.HasKitchenObject() && stove.IsFried() && !HasKitchenObject())
        {
            stove.GetKitchenObject().SetKitchenObjectParent(this);
            stove.ResetStoveFromAgent();
            CompleteAgentTask();
        }
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
            CompleteAgentTask();
        else
            Debug.LogWarning("[Deliver] Eşya bırakılamadı veya birleştirme başarısız.");
    }


    private void CompleteAgentTask()
    {
        AddReward(1f);
        activeTask = null;
        taskManager.OnTaskCompleted();
        interactCooldownTimer = 0f;
        hasEnteredInteractRange = false;
        totalApproachReward = 0f;
        lastDistToTarget = float.MaxValue;
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

            Vector3 agentXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetXZ = new Vector3(activeTask.targetCounter.transform.position.x,
                                           0,
                                           activeTask.targetCounter.transform.position.z);
            float distance = Vector3.Distance(agentXZ, targetXZ);

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