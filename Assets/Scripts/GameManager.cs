using System;
using UnityEngine;

// Tek bir bölümü yönetir (hangisi olduğu LevelSelection.CurrentLevelIndex'ten gelir).
// Kazanma/kaybetme/sonraki bölüm geçişleri GameScene'i yeniden yükleyerek yapılır,
// böylece ajan, tezgahlar, siparişler her bölümde temiz başlar.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;
    public event EventHandler OnLevelWon;
    public event EventHandler OnLevelLost;

    [Header("Referanslar")]
    [SerializeField] private LevelListSO levelList; // tüm bölüm tanımları (asset)
    [SerializeField] private PlayerInput playerInput;

    private enum State { CountdownToStart, GamePlaying, LevelWon, LevelLost }

    private State state;
    private float countdownToStartTimer = 3f;
    private float gamePlayingTimer;
    private bool IsGamePaused = false;
    private int deliveredThisLevel = 0;

    private LevelConfig Level
    {
        get
        {
            int i = Mathf.Clamp(LevelSelection.CurrentLevelIndex, 0, levelList.levels.Count - 1);
            return levelList.levels[i];
        }
    }

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f; // önceki bölümden kalma 0 olabilir
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

        if (levelList == null || levelList.levels.Count == 0)
            Debug.LogError("GameManager: LevelListSO atanmamış veya boş!");

        gamePlayingTimer = Level.durationSeconds;
    }

    private void GameInput_OnPauseAction(object sender, EventArgs e) => TogglePauseGame();

    private void DeliveryManager_OnRecipeCompleted(object sender, EventArgs e)
    {
        if (state != State.GamePlaying) return;

        deliveredThisLevel++;
        if (deliveredThisLevel >= Level.targetOrders)
        {
            state = State.LevelWon;
            Time.timeScale = 0f; // sahne donsun, sonuç paneli gösterilsin
            OnLevelWon?.Invoke(this, EventArgs.Empty);
            OnStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Update()
    {
        switch (state)
        {
            case State.CountdownToStart:
                countdownToStartTimer -= Time.deltaTime;
                if (countdownToStartTimer < 0f)
                {
                    deliveredThisLevel = 0;
                    gamePlayingTimer = Level.durationSeconds;
                    state = State.GamePlaying;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;

            case State.GamePlaying:
                gamePlayingTimer -= Time.deltaTime;
                if (gamePlayingTimer < 0f)
                {
                    // Süre doldu, hedefe ulaşılamadı → kaybettin
                    state = State.LevelLost;
                    Time.timeScale = 0f;
                    OnLevelLost?.Invoke(this, EventArgs.Empty);
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
        }
    }

    public bool HasNextLevel() => LevelSelection.CurrentLevelIndex < levelList.levels.Count - 1;

    // Sonraki bölüme geç → GameScene yeniden yüklenir (her şey sıfırlanır).
    // Sonraki yoksa ana menüye döner.
    public void GoToNextLevel()
    {
        Time.timeScale = 1f;
        if (!HasNextLevel())
        {
            Loader.Load(Loader.Scene.MainMenuScene);
            return;
        }
        LevelSelection.CurrentLevelIndex++;
        Loader.Load(Loader.Scene.GameScene);
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        Loader.Load(Loader.Scene.GameScene); // aynı bölümü baştan
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Loader.Load(Loader.Scene.MainMenuScene);
    }

    // ── Durum sorguları (UI) ──
    public bool IsGamePlaying() => state == State.GamePlaying;
    public bool IsCountdownToStartActive() => state == State.CountdownToStart;
    public bool IsLevelWon() => state == State.LevelWon;
    public bool IsLevelLost() => state == State.LevelLost;
    public float GetCountdownToStartTimer() => countdownToStartTimer;

    public int GetLevelNumber() => LevelSelection.CurrentLevelIndex + 1;
    public int GetTotalLevels() => levelList != null ? levelList.levels.Count : 0;
    public int GetDeliveredThisLevel() => deliveredThisLevel;
    public int GetTargetOrders() => Level.targetOrders;
    public float GetTimeRemaining() => Mathf.Max(0f, gamePlayingTimer);
    public float GetGamePlayingTimerNormalized()
    {
        float max = Level.durationSeconds;
        return max > 0f ? 1f - (gamePlayingTimer / max) : 0f;
    }

    public void TogglePauseGame()
    {
        // Sadece oyun oynanırken duraklatma anlamlı (kazandın/kaybettin ekranını dondurma)
        if (state != State.GamePlaying && !IsGamePaused) return;

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
