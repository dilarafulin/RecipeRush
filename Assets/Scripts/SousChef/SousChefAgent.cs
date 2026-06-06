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

    private SousChefTask activeTask;
    private float episodeTimer;
    private const float MAX_EPISODE_TIME = 30f; 

    private bool isWalking;
    private float interactCooldownTimer = 0f;
    private const float INTERACT_COOLDOWN = 0.3f;


    private KitchenObject heldObject;
    private Vector3 currentMoveDir;

    private float lastDistToTarget = float.MaxValue;

    public void SetTask(SousChefTask task)
    {
        activeTask = task;
        // EndEpisode();   // yeni gorev geldiginde zihni sifirla
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        currentMoveDir = Vector3.zero;
        lastDistToTarget = float.MaxValue;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (activeTask == null || activeTask.targetCounter == null)
        {
            sensor.AddObservation(Vector3.zero); // 1-2-3
            sensor.AddObservation(0f);           // 4
            sensor.AddObservation(0f);           // 5 — eli dolu mu
            sensor.AddObservation(0f);           // 6 — hedef dolu mu
            sensor.AddObservation(0f);           // 7
            sensor.AddObservation(0f);           // 8
            sensor.AddObservation(0f);           // 9
            sensor.AddObservation(0f);           // 10
            sensor.AddObservation(0f);           // 11 — komut one-hot (5 değer)
            return;
        }

        // 1-3: Hedefe yön
        Vector3 dirToTarget = (activeTask.targetCounter.transform.position
                               - transform.position).normalized;
        sensor.AddObservation(dirToTarget);

        // 4: Hedefe uzaklık
        float dist = Vector3.Distance(transform.position,
                                      activeTask.targetCounter.transform.position);
        sensor.AddObservation(Mathf.Clamp01(dist / 10f));

        // 5: Elde malzeme var mı
        sensor.AddObservation(HasKitchenObject() ? 1f : 0f);

        // 6: Hedef tezgah dolu mu
        sensor.AddObservation(activeTask.targetCounter.HasKitchenObject() ? 1f : 0f);

        // 7-11: Aktif komut (one-hot) — ajan ne yapması gerektiğini bilsin
        sensor.AddObservation(activeTask.command == SousChefCommand.FetchIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.ChopIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.CookIngredient ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.DeliverToCounter ? 1f : 0f);
        sensor.AddObservation(activeTask.command == SousChefCommand.Idle ? 1f : 0f);
    }

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
            float distanceDelta = lastDistToTarget - dist;
            if (distanceDelta > 0.01f)          // Anlamlı bir yaklaşma olduysa
                AddReward(distanceDelta * 0.1f); // Orantılı ödül
            lastDistToTarget = dist;
        }
    }

    private void Update()
    {
        if (interactCooldownTimer > 0f)
        {
            interactCooldownTimer -= Time.deltaTime;
        }

        float moveDistance = moveSpeed * Time.deltaTime;
        float playerRadius = 0.5f;
        float playerHeight = 0.5f;

        bool canMove = !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, currentMoveDir, moveDistance,collisionLayerMask);

        if (!canMove)
        {
            Vector3 moveDirX = new Vector3(currentMoveDir.x, 0, 0).normalized;
            canMove = currentMoveDir.x != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance, collisionLayerMask);

            if (canMove)
            {
                currentMoveDir = moveDirX;
            }
            else
            {
                Vector3 moveDirZ = new Vector3(0, 0, currentMoveDir.z).normalized;
                canMove = currentMoveDir.z != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance, collisionLayerMask);

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

        isWalking = currentMoveDir != Vector3.zero;

        Vector3 lookDir = Vector3.zero;

        if (currentMoveDir != Vector3.zero)
        {
            lookDir = currentMoveDir;
        }
        //Duruyorsa ve görevi varsa, tezgaha bak
        else if (activeTask?.targetCounter != null)
        {
            lookDir = (activeTask.targetCounter.transform.position - transform.position);
        }

        lookDir.y = 0; // Kafası yere veya havaya bakmasın
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void TryInteract()
    {
        if (interactCooldownTimer > 0f) return; // Cooldown aktifse çık
        if (activeTask == null) return;
        float dist = Vector3.Distance(transform.position, activeTask.targetCounter.transform.position);
        if (dist > interactDistance) return; // Henüz yeterince yakın değilse vazgeç 

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

       // interactCooldown = true;
        interactCooldownTimer = INTERACT_COOLDOWN;
    }

    private void HandleFetch()
    {

        if (!HasKitchenObject() && activeTask?.targetCounter != null)
        {
            // TEK SATIR: Tezgah ne olursa olsun eşyayı vermesini iste
            activeTask.targetCounter.InteractFromAgent(this);

            // Eğer eşyayı başarıyla aldıysak görevi bitir
            if (HasKitchenObject()) CompleteAgentTask();
        }
    }

    // Görev bitirme kodlarını tekrar etmemek için küçük bir yardımcı fonksiyon:
    private void CompleteAgentTask()
    {
        AddReward(1f); 
        taskManager.OnTaskCompleted();
        activeTask = null;

        //Görev bittiği an sayacı sıfırla ki ajan sonraki emre anında tepki versin!
        interactCooldownTimer = 0f;
    }

    private void HandleChop()
    {
        if (activeTask?.targetCounter is CuttingCounter cut && cut.HasKitchenObject())
        {
            cut.InteractAlternate(null); // Kesme animasyonunu/progress'ini ilerlet

            if (cut.IsFullyCut()) // Tamamen kesildiyse görevi bitir
            {
                CompleteAgentTask();
            }
        }
    }

    private void HandleCook()
    {
        if (activeTask.targetCounter is not StoveCounter stove) return;

        if (!stove.HasKitchenObject() && HasKitchenObject())
        {
            stove.InteractFromAgent(this);
            return; // Bu adımda sadece bunu yap
        }

        if (stove.HasKitchenObject() && stove.IsIdle())
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

    private void HandleDeliver()
    {
      
        if (HasKitchenObject() && activeTask?.targetCounter != null)
        {
            // TEK SATIR: Tezgah ne olursa olsun eşyayı bırakmayı dene
            activeTask.targetCounter.InteractFromAgent(this);

            // Eğer eşya elimizden başarıyla çıktıysa görevi bitir
            if (!HasKitchenObject()) CompleteAgentTask();
        }
    }

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

    // interface
    public bool IsWalking() => isWalking;
    public Transform GetKitchenObjectFollowTransform() => kitchenObjectHoldPoint;
    public void SetKitchenObject(KitchenObject obj) => heldObject = obj;
    public KitchenObject GetKitchenObject() => heldObject;
    public void ClearKitchenObject() => heldObject = null;
    public bool HasKitchenObject() => heldObject != null;
}