using System;
using UnityEngine;

public class Player : MonoBehaviour, IKitchenObjectParent, IMovable
{
    public static Player Instance { get; private set; } 

    public event EventHandler<OnSelectedCounterChangedEventArgs> OnSelectedCounterChanged;
    public class OnSelectedCounterChangedEventArgs: EventArgs
    {
        public BaseCounter selectedCounter;
    }

    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float interactDistance = 2;
    [SerializeField] private LayerMask countersLayerMask;
    [SerializeField] private Transform kitchenObjectHoldPoint;

    private bool isWalking;
    private Vector3 lastInteractDir;
    private BaseCounter selectedCounter;

    private KitchenObject kitchenObject;
    // ── IKitchenObjectParent ──────────────────────
    public Transform GetKitchenObjectFollowTransform() => kitchenObjectHoldPoint;
    public void SetKitchenObject(KitchenObject obj) => kitchenObject = obj;
    public KitchenObject GetKitchenObject() => kitchenObject;
    public void ClearKitchenObject() => kitchenObject = null;
    public bool HasKitchenObject() => kitchenObject != null;

    private void Awake()
    {
        if (Instance != null) {
            Debug.Log("Birden fazla oyuncu var");
        }
        Instance = this;

        Debug.unityLogger.logEnabled = false;
    }

    private void Update()
    {
        HandleMovement();
        HandleInteractions();
    }
    private void Start()
    {
        // PlayerInput event'ini dinle
        playerInput.OnInteractAction += PlayerInput_OnInteractAction;
        playerInput.OnInteractAlternateAction += PlayerInput_OnInteractAlternateAction;
    }

    private void PlayerInput_OnInteractAlternateAction(object sender, EventArgs e)
    {
        // F'ye basıldığında seçili bir tezgah varsa onun Alternatif Etkileşimini çalıştır
        if (selectedCounter != null)
        {
            selectedCounter.InteractAlternate(this);
        }
    }

    private void PlayerInput_OnInteractAction(object sender, EventArgs e)
    {
        // E'ye basılınca burası çalışır
        if (selectedCounter != null)
        {
            selectedCounter.Interact(this);
        }
    }

    public bool IsWalking()
    {
        return isWalking;
    }

    private void HandleInteractions()
    {
        Vector2 inputVector = playerInput.GetMovementVectorNormalized();
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        // Hareket ediyorsa yönü güncelle, durunca son yönü hatırla
        if (moveDir != Vector3.zero)
        {
            lastInteractDir = moveDir;
        }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.95f;

        // 1. AŞAMA: SÜREKLİ RAYCAST AT VE NEYE BAKTIĞIMIZI BUL
        if (Physics.Raycast(rayOrigin, lastInteractDir, out RaycastHit hit, interactDistance, countersLayerMask))
        {
            BaseCounter baseCounter = hit.collider.GetComponent<BaseCounter>();

            if (baseCounter != selectedCounter) // Baktığımız obje az öncekiyle AYNI DEĞİLSE
            {
                SetSelectedCounter(baseCounter); // Yeni tezgaha odaklan
            }
        }
        else
        {
            // Eğer Raycast hiçbir şeye çarpmazsa, seçili tezgahı sıfırla
            SetSelectedCounter(null);
        }
        
    }

    private void SetSelectedCounter(BaseCounter newCounter)
    {
        selectedCounter = newCounter;


        OnSelectedCounterChanged?.Invoke(this, new OnSelectedCounterChangedEventArgs
        {
            selectedCounter = selectedCounter
        });
    }

    private void HandleMovement()
    {
        Vector2 inputVector = playerInput.GetMovementVectorNormalized();

        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);

        float moveDistance = moveSpeed * Time.deltaTime;
        float playerRadius = .5f;
        float playerHeight = 2f;
        bool canMove = !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDir, moveDistance);

        if (!canMove) //cannot move towards moveDir
        {
            //attempt only x movement
            Vector3 moveDirX = new Vector3(moveDir.x, 0, 0).normalized;
            canMove = moveDir.x != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance); //x yonunde bir seye carpma durumu 

            if (canMove)
            {
                //can move only on the x 
                moveDir = moveDirX;
            }
            else
            {
                //cannot move only on the X

                //attempt only z movement
                Vector3 moveDirZ = new Vector3(0, 0, moveDir.z).normalized;
                canMove = moveDir.z != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance); // z ekseninde bos mu onu kontrol et


                if (canMove)
                {
                    //can move only on the z
                    moveDir = moveDirZ;
                }
                else //cannot move in any direction
                {
                    //hicbir yone hareket etmiyor
                }

            }
        }

        if (canMove)
        {
            transform.position += moveDir * moveDistance;
        }

        isWalking = moveDir != Vector3.zero;

        float rotateSpeed = 10f;
        if (moveDir != Vector3.zero)
        {
            transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);
        }
    }

    public BaseCounter GetSelectedCounter()
    {
        return selectedCounter;
    }
}
