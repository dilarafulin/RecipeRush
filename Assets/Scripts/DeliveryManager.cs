using System;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryManager : MonoBehaviour
{
    // Singleton (Oyunun her yerinden kolayca eri�ebilmek i�in)
    public static DeliveryManager Instance { get; private set; }

    [Header("Veri Havuzu")]
    [SerializeField] private RecipeListSO recipeListSO; // Olu�turdu�un "AllRecipes" dosyas�n� buraya s�r�kle

    private List<RecipeSO> waitingRecipeSOList; // Ekranda bekleyen aktif sipari�ler
    private float spawnRecipeTimer;
    private float spawnRecipeTimerMax = 4f; // Her 4 saniyede bir sipari� gelsin
    private int waitingRecipesMax = 4; // Ekranda maksimum 4 sipari� birikebilsin

    // UI'�n haberdar olmas� i�in Event'ler
    public event EventHandler OnRecipeSpawned;
    public event EventHandler OnRecipeCompleted;

    private void Awake()
    {
        Instance = this;
        waitingRecipeSOList = new List<RecipeSO>();
    }

    private void Update()
    {
        // Sipari�ler sadece bir b�l�m aktif oynan�rken gelsin (geri say�m / b�l�m
        // sonu / oyun bitti ekranlar�nda yeni sipari� spawn olmamal�)
        if (GameManager.Instance != null && !GameManager.Instance.IsGamePlaying()) return;

        spawnRecipeTimer -= Time.deltaTime;
        if (spawnRecipeTimer <= 0f)
        {
            spawnRecipeTimer = spawnRecipeTimerMax;

            // E�er ekrandaki sipari� say�s� s�n�r� a�mad�ysa yeni sipari� ver
            if (waitingRecipeSOList.Count < waitingRecipesMax)
            {
                // Havuzdan rastgele bir tarif se�
                RecipeSO waitingRecipeSO = recipeListSO.recipeSOList[UnityEngine.Random.Range(0, recipeListSO.recipeSOList.Count)];

               // Debug.Log(waitingRecipeSO.recipeName);
                // Bekleyenler listesine ekle
                waitingRecipeSOList.Add(waitingRecipeSO);

                // Aray�ze (UI) haber ver: "Yeni sipari� geldi, ekrana �iz!"
                OnRecipeSpawned?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // Oyuncu elinde bir tabakla teslimat tezgah�na geldi�inde bu fonksiyon �al��acak
    public void DeliverRecipe(List<KitchenObjectSO> plateKitchenObjectSOList)
    {
        for (int i = 0; i < waitingRecipeSOList.Count; i++)
        {
            RecipeSO waitingRecipeSO = waitingRecipeSOList[i];

            // 1. Kural: Tabaktaki malzeme say�s� ile tarifteki malzeme say�s� e�it mi?
            if (waitingRecipeSO.kitchenObjectSOList.Count == plateKitchenObjectSOList.Count)
            {
                bool plateContentsMatchesRecipe = true;

                // 2. Kural: Tarifteki her bir malzeme, tabakta var m�?
                foreach (KitchenObjectSO recipeKitchenObjectSO in waitingRecipeSO.kitchenObjectSOList)
                {
                    bool ingredientFound = false;
                    foreach (KitchenObjectSO plateKitchenObjectSO in plateKitchenObjectSOList)
                    {
                        if (plateKitchenObjectSO == recipeKitchenObjectSO)
                        {
                            ingredientFound = true;
                            break;
                        }
                    }

                    if (!ingredientFound)
                    {
                        // Bu malzeme tabakta yok! Demek ki bu tarif de�il.
                        plateContentsMatchesRecipe = false;
                        break;
                    }
                }

                if (plateContentsMatchesRecipe)
                {
                    // BA�ARILI TESL�MAT!
                    Debug.Log("Sipari� Ba�ar�yla Teslim Edildi: " + waitingRecipeSO.recipeName);

                    waitingRecipeSOList.RemoveAt(i);

                    // UI'a haber ver: "Bu sipari� bitti, ekrandan sil!"
                    OnRecipeCompleted?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }

        // E�er d�ng� bittiyse ve return olmad�ysa, oyuncu yanl�� yemek getirmi�tir.
        Debug.Log("Hata: Oyuncu yanl�� bir yemek getirdi veya b�yle bir sipari� yok!");
    }

    // UI'�n bekleyen listeyi okuyabilmesi i�in
    public List<RecipeSO> GetWaitingRecipeSOList()
    {
        return waitingRecipeSOList;
    }

    // Yeni b�l�m ba�larken bekleyen sipari�leri temizle (sayaca dokunmaz; teslim
    // say�m� GameManager taraf�nda OnRecipeCompleted ile tutulur)
    public void ResetOrders()
    {
        waitingRecipeSOList.Clear();
        spawnRecipeTimer = spawnRecipeTimerMax;
        OnRecipeSpawned?.Invoke(this, EventArgs.Empty); // UI'� bo� listeyle yenile
    }
}