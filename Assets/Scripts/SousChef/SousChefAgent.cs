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

    private SousChefTask activeTask;
    private float episodeTimer;
    private const float MAX_EPISODE_TIME = 30f; // 30 saniyede bitirmezse ceza alıp başa döner 

    private bool isWalking;
    private KitchenObject heldObject;
    private Vector3 currentMoveDir;

    // ── GÖREV ATAMA ────────────────────────────────────────────
    public void SetTask(SousChefTask task)
    {
        activeTask = task;
        episodeTimer = 0f;
        EndEpisode(); // Yeni görev geldiğinde zihni sıfırlayıp baştan başlar 
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
    }

    // ── 1. GÖZLEMLER (Ajanın Gözleri) ──────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        if (activeTask == null || activeTask.targetCounter == null)
        {
            // Görev yoksa her şeyi sıfır (0,0,0) olarak algıla 
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
            return;
        }

        // Hedefe olan yön vektörü (Normalize edilmiş) 
        Vector3 dirToTarget = (activeTask.targetCounter.transform.position - transform.position).normalized;
        sensor.AddObservation(dirToTarget);

        // Hedefe uzaklık [cite: 64]
        float dist = Vector3.Distance(transform.position, activeTask.targetCounter.transform.position);
        sensor.AddObservation(Mathf.Clamp01(dist / 10f));
    }

    // ── 1. BEYİN (Sadece Karar Alır, Fiziksel Hareket Yapmaz) ──
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Zaman aşımı kontrolü (FixedDeltaTime kullanmak ML'de daha sağlıklıdır)
        episodeTimer += Time.fixedDeltaTime;
        if (episodeTimer >= MAX_EPISODE_TIME)
        {
            AddReward(-1f);
            EndEpisode();
            return;
        }

        // Beyin hangi yöne gitmek istediğine karar verir ve hafızaya yazar
        int moveAction = actions.DiscreteActions[0];
        currentMoveDir = Vector3.zero;

        switch (moveAction)
        {
            case 1: currentMoveDir = Vector3.forward; break;
            case 2: currentMoveDir = Vector3.back; break;
            case 3: currentMoveDir = Vector3.left; break;
            case 4: currentMoveDir = Vector3.right; break;
        }

        // Etkileşim kararı (Aynı kalıyor)
        int interactAction = actions.DiscreteActions[1];
        if (interactAction == 1) TryInteract();

        AddReward(-0.001f);
        if (activeTask?.targetCounter != null)
        {
            float dist = Vector3.Distance(transform.position, activeTask.targetCounter.transform.position);
            if (dist < 2f) AddReward(0.01f);
        }
    }

    // ── 2. KASLAR VE BEDEN (Saniyede 60+ kere, Player gibi pürüzsüz çalışır) ──
    private void Update()
    {
        // Tıpkı Player.cs'deki gibi pürüzsüz hareket
        float moveDistance = moveSpeed * Time.deltaTime;
        float playerRadius = 0.5f;
        float playerHeight = 2f;

        bool canMove = !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, currentMoveDir, moveDistance);

        if (!canMove)
        {
            Vector3 moveDirX = new Vector3(currentMoveDir.x, 0, 0).normalized;
            canMove = currentMoveDir.x != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance);

            if (canMove)
            {
                currentMoveDir = moveDirX;
            }
            else
            {
                Vector3 moveDirZ = new Vector3(0, 0, currentMoveDir.z).normalized;
                canMove = currentMoveDir.z != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance);

                if (canMove)
                {
                    currentMoveDir = moveDirZ;
                }
            }
        }

        if (canMove)
        {
            transform.position += currentMoveDir * moveDistance;
        }

        // Animatörü pürüzsüz bir şekilde tetikle
        isWalking = currentMoveDir != Vector3.zero;

        // Vücudunu gideceği veya görevli olduğu yere doğru yumuşakça (Slerp) döndür
        if (activeTask?.targetCounter != null)
        {
            Vector3 lookDir = (activeTask.targetCounter.transform.position - transform.position);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.forward = Vector3.Slerp(transform.forward, lookDir.normalized, rotationSpeed * Time.deltaTime);
        }
        else if (currentMoveDir != Vector3.zero)
        {
            // Eğer görev yoksa (Heuristic ile manuel test ediyorsan) klavyeyle bastığın yöne dönsün
            transform.forward = Vector3.Slerp(transform.forward, currentMoveDir, 10f * Time.deltaTime);
        }
    }

    private void TryInteract()
    {
        if (activeTask == null) return;
        float dist = Vector3.Distance(transform.position, activeTask.targetCounter.transform.position);
        if (dist > interactDistance) return; // Henüz yeterince yakın değilse vazgeç [cite: 79]

        // Görev türüne göre doğru fonksiyonu çağır
        switch (activeTask.command)
        {
            case SousChefCommand.FetchIngredient:
                HandleFetch(); break;
            case SousChefCommand.ChopIngredient:
                HandleChop(); break;
            case SousChefCommand.CookIngredient:
                HandleCook(); break;
            case SousChefCommand.DeliverToCounter: HandleDeliver(); break;
        }
    }

    private void HandleFetch()
    {
        if (!HasKitchenObject() && activeTask.targetCounter is SourceCounter src)
        {
            src.InteractFromAgent(this); // Kendi güvenli arka kapımızı kullanıyoruz

            AddReward(1f); // Görev başarılı, büyük ödül!
            taskManager.OnTaskCompleted();
            EndEpisode();
        }
    }

    private void HandleChop()
    {
        if (activeTask.targetCounter is CuttingCounter cut && cut.HasKitchenObject())
        {
            cut.InteractAlternate(null);
            if (!cut.HasKitchenObject()) // Kesme bittiyse 
            {
                AddReward(1f);
                taskManager.OnTaskCompleted();
                EndEpisode();
            }
        }
    }

    private void HandleCook()
    {
        if (activeTask.targetCounter is StoveCounter stove)
        {
            // 1. DURUM: Ocak boş ve ajanın elinde pişirilecek et var -> Eti ocağa koy
            if (!stove.HasKitchenObject() && HasKitchenObject())
            {
                stove.InteractFromAgent(this); // Yazdığımız arka kapıyı kullanıyoruz
            }

            // 2. DURUM: Ocakta et var ama ateş yanmıyor (Idle) -> F'ye basıp ocağı yak
            else if (stove.HasKitchenObject() && stove.IsIdle())
            {
                // NOT: InteractAlternate içinde 'player' değişkeni kullanılmadığı için null göndermek tamamen güvenlidir!
                stove.InteractAlternate(null);
            }

            // 3. DURUM: Ocakta pişen yemek hazır (Fried) ve ajanın eli boş -> Eti al ve Görevi Bitir
            else if (stove.HasKitchenObject() && stove.IsFried() && !HasKitchenObject())
            {
                // Eti ocaktan kendi eline al
                stove.GetKitchenObject().SetKitchenObjectParent(this);

                // Ocağı sıfırla ki progress bar havada asılı kalmasın
                stove.ResetStoveFromAgent();

                // ML-Agent'a aferin de ve görevi bitir
                AddReward(1f);
                taskManager.OnTaskCompleted();
                EndEpisode();
            }
        }
    }

    private void HandleDeliver()
    {
        if (HasKitchenObject() && !activeTask.targetCounter.HasKitchenObject())
        {
            GetKitchenObject().SetKitchenObjectParent(activeTask.targetCounter);
            AddReward(1f);
            taskManager.OnTaskCompleted();
            EndEpisode();
        }
    }

    // ── 3. HEURISTIC (Klavyeyle Manuel Test Modu) ────────────────
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        discreteActions[0] = 0; // Varsayılan: Dur
        discreteActions[1] = 0; // Varsayılan: Etkileşim Yok

        if (Keyboard.current != null)
        {
            // Oyuncu (Player) ile çakışmamak için I-J-K-L kullanıyoruz
            if (Keyboard.current.iKey.isPressed) discreteActions[0] = 1; // İleri
            if (Keyboard.current.kKey.isPressed) discreteActions[0] = 2; // Geri
            if (Keyboard.current.jKey.isPressed) discreteActions[0] = 3; // Sol
            if (Keyboard.current.lKey.isPressed) discreteActions[0] = 4; // Sağ

            // Etkileşim için T tuşu (Player E ve F kullanıyor)
            discreteActions[1] = Keyboard.current.tKey.isPressed ? 1 : 0;
        }
    }

    // ── INTERFACE UYGULAMALARI (Oyunun Geri Kalanı İçin) ────────
    public bool IsWalking() => isWalking;
    public Transform GetKitchenObjectFollowTransform() => kitchenObjectHoldPoint;
    public void SetKitchenObject(KitchenObject obj) => heldObject = obj;
    public KitchenObject GetKitchenObject() => heldObject;
    public void ClearKitchenObject() => heldObject = null;
    public bool HasKitchenObject() => heldObject != null;
}