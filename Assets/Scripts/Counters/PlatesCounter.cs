using System;
using UnityEngine;

public class PlatesCounter : BaseCounter
{
    // Görsel kodlarýn (PlatesCounterVisual) bu olaylarý dinleyip 
    // masada tabak modelini gösterip gizlemesi için Event'ler hazýrlýyoruz.
    public event EventHandler OnPlateSpawned;
    public event EventHandler OnPlateRemoved;

    [SerializeField] private KitchenObjectSO plateKitchenObjectSO; // Tabak verisi
    [SerializeField] private float spawnPlateTimerMax = 4f;        // Kaç saniyede bir tabak çýksýn?
    [SerializeField] private int platesSpawnAmountMax = 4;         // Masada en fazla kaç tabak birikebilir?

    private float spawnPlateTimer;
    private int platesSpawnAmount;

    private void Update()
    {
        // 1. Zamanlayýcýyý sürekli artýr
        spawnPlateTimer += Time.deltaTime;

        // 2. Süre dolduysa
        if (spawnPlateTimer > spawnPlateTimerMax)
        {
            spawnPlateTimer = 0f; // Zamanlayýcýyý sýfýrla

            // 3. Masadaki tabak sayýsý maksimum sýnýra ulaþmadýysa yeni tabak ekle
            if (platesSpawnAmount < platesSpawnAmountMax)
            {
                platesSpawnAmount++;

                // Görseli güncellemek için Event fýrlat
                OnPlateSpawned?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public override void Interact(Player player)
    {
        // Eðer oyuncunun elleri boþsa
        if (!player.HasKitchenObject())
        {
            // Ve tezgahta en az 1 tane tabak varsa
            if (platesSpawnAmount > 0)
            {
                // Tezgahtaki tabak sayýsýný bir azalt
                platesSpawnAmount--;

                // Oyuncunun eline yeni bir tabak yarat (Spawn)
                KitchenObject.SpawnKitchenObject(plateKitchenObjectSO, player);

                // Görselden bir tabaðý silmek için Event fýrlat
                OnPlateRemoved?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public override void InteractFromAgent(SousChefAgent agent)
    {
        if (!agent.HasKitchenObject())
        {
            KitchenObject.SpawnKitchenObject(plateKitchenObjectSO, agent);
        }
    }

    public override SousChefTask GetTaskForAgent(SousChefAgent agent)
    {
        if (!agent.HasKitchenObject())
        {
            return new SousChefTask(SousChefCommand.FetchIngredient, this);
        }
        return null;
    }
}