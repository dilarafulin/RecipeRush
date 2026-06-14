using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Mevcut UI'ların dinlediği olaylar (korunuyor)
    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

    // Seviye sistemi olayları
    public event EventHandler OnLevelStarted;   // yeni bölüm başladı
    public event EventHandler OnLevelCompleted;  // bölüm hedefi tutturuldu (son bölüm değilse)
    public event EventHandler OnGameWon;         // tüm bölümler tamamlandı
    public event EventHandler OnGameLost;        // süre doldu, hedefe ulaşılamadı

    [Serializable]
    public class LevelConfig
    {
        public float durationSeconds = 180f; // bölüm süresi
        public int targetOrders = 5;         // tamamlanması gereken sipariş sayısı
    }

    [Header("Bölümler (sırayla)")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();
    [SerializeField] private float levelCompletePauseSeconds = 3f; // "Bölüm tamamlandı" gösterim süresi

    [Header("Referanslar")]
    [SerializeField] private PlayerInput playerInput;

    private enum State
    {
        CountdownToStart, // her bölüm öncesi 3-2-1
        GamePlaying,
        LevelComplete,    // bölüm tamamlandı, sıradakine geçiş bekleniyor
        GameWon,          // tüm bölümler bitti
        GameOver,         // kaybedildi (süre doldu)
    }

    private State state;
    private float countdownToStartTimer = 3f;
    private float gamePlayingTimer;
    private float levelCompleteTimer;
    private bool IsGamePaused = false;

    private int currentLevelIndex = 0;
    private int deliveredThisLevel = 0;

    private void Awake()
    {
        Instance = this;
        state = State.CountdownToStart;
        countdownToStartTimer = 3f;
    }

    private void Start()
    {
        if (playerInput != null)
            playerInput.OnPauseAction += GameInput_OnPauseAction;
        else
            Debug.LogError("GameManager: PlayerInput referansı atanmamış!");

        if (DeliveryManager.Instance != null)
            DeliveryManager.Instance.OnRecipeCompleted += DeliveryManager_OnRecipeCompleted;
        else
            Debug.LogError("GameManager: DeliveryManager bulunamadı!");

        if (levels.Count == 0)
            Debug.LogError("GameManager: Hiç bölüm tanımlanmamış! Inspector'dan Levels listesini doldur.");
    }

    private void GameInput_OnPauseAction(object sender, EventArgs e) => TogglePauseGame();

    // Bir sipariş başarıyla teslim edildi (oyuncu ya da ajan — fark etmez)
    private void DeliveryManager_OnRecipeCompleted(object sender, EventArgs e)
    {
        if (state != State.GamePlaying) return;

        deliveredThisLevel++;
        if (deliveredThisLevel >= CurrentLevel().targetOrders)
            CompleteLevel();
    }

    private void Update()
    {
        switch (state)
        {
            case State.CountdownToStart:
                countdownToStartTimer -= Time.deltaTime;
                if (countdownToStartTimer < 0f)
                    StartLevel();
                break;

            case State.GamePlaying:
                gamePlayingTimer -= Time.deltaTime;
                if (gamePlayingTimer < 0f)
                {
                    // Süre doldu ve hedefe ulaşılamadı → kaybettin
                    state = State.GameOver;
                    OnGameLost?.Invoke(this, EventArgs.Empty);
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;

            case State.LevelComplete:
                levelCompleteTimer -= Time.deltaTime;
                if (levelCompleteTimer < 0f)
                {
                    // Sıradaki bölüme geç (geri sayımla)
                    currentLevelIndex++;
                    countdownToStartTimer = 3f;
                    state = State.CountdownToStart;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;

            case State.GameWon:
            case State.GameOver:
                break;
        }
    }

    private LevelConfig CurrentLevel()
        => levels[Mathf.Clamp(currentLevelIndex, 0, Mathf.Max(0, levels.Count - 1))];

    private void StartLevel()
    {
        deliveredThisLevel = 0;
        gamePlayingTimer = CurrentLevel().durationSeconds;

        if (DeliveryManager.Instance != null)
            DeliveryManager.Instance.ResetOrders(); // bölüm temiz başlasın

        state = State.GamePlaying;
        OnLevelStarted?.Invoke(this, EventArgs.Empty);
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteLevel()
    {
        if (currentLevelIndex >= levels.Count - 1)
        {
            // Son bölüm de tamamlandı → oyunu bitirdin
            state = State.GameWon;
            OnGameWon?.Invoke(this, EventArgs.Empty);
            OnStateChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Bölüm tamamlandı → kısa bir gösterim, sonra sıradaki bölüm
            state = State.LevelComplete;
            levelCompleteTimer = levelCompletePauseSeconds;
            OnLevelCompleted?.Invoke(this, EventArgs.Empty);
            OnStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Kaybedince mevcut bölümü baştan dene (GameOver ekranındaki "Tekrar Dene" butonu çağırır)
    public void RetryLevel()
    {
        countdownToStartTimer = 3f;
        state = State.CountdownToStart;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Oyunu en baştan başlat (örn. kazandıktan sonra "Yeniden Oyna")
    public void RestartGame()
    {
        currentLevelIndex = 0;
        countdownToStartTimer = 3f;
        state = State.CountdownToStart;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Durum sorguları (UI için) ──
    public bool IsGamePlaying() => state == State.GamePlaying;
    public bool IsCountdownToStartActive() => state == State.CountdownToStart;
    public bool IsLevelComplete() => state == State.LevelComplete;
    public bool IsGameWon() => state == State.GameWon;
    public bool IsGameOver() => state == State.GameOver;
    public float GetCountdownToStartTimer() => countdownToStartTimer;

    // Seviye bilgisi (HUD için)
    public int GetLevelNumber() => currentLevelIndex + 1;       // 1 tabanlı
    public int GetTotalLevels() => levels.Count;
    public int GetDeliveredThisLevel() => deliveredThisLevel;
    public int GetTargetOrders() => CurrentLevel().targetOrders;
    public float GetTimeRemaining() => Mathf.Max(0f, gamePlayingTimer);
    public float GetGamePlayingTimerNormalized()
    {
        float max = CurrentLevel().durationSeconds;
        return max > 0f ? 1f - (gamePlayingTimer / max) : 0f;
    }

    public void TogglePauseGame()
    {
        IsGamePaused = !IsGamePaused;
        if (IsGamePaused)
        {
            Time.timeScale = 0f;
            OnGamePaused?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Time.timeScale = 1f;
            OnGameUnpaused?.Invoke(this, EventArgs.Empty);
        }
    }
}
