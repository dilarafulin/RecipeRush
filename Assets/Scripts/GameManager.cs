using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;

    [SerializeField] private PlayerInput playerInput;

    private enum State
    {
        // WaitingToStart tamamen silindi! Direkt geri sayýmla baþlýyoruz.
        CountdownToStart,
        GamePlaying,
        GameOver,
    }

    private State state;
    private float countdownToStartTimer = 3f;
    private float gamePlayingTimer;
    private float gamePlayingTimerMax = 60f; // Oyun süresi
    private bool IsGamePaused = false;

    private void Awake()
    {
        Instance = this;

        // Oyun açýlýr açýlmaz durumu "Geri Sayým" olarak ayarlýyoruz
        state = State.CountdownToStart;
    }

    private void Start()
    {
        if (playerInput != null)
        {
            playerInput.OnPauseAction += GameInput_OnPauseAction;
        }
        else
        {
            Debug.LogError("GameManager: PlayerInput referansý atanmamýþ!");
        }
    }

    private void GameInput_OnPauseAction(object sender, EventArgs e)
    {
        TogglePauseGame();
    }

    private void Update()
    {
        switch (state)
        {
            case State.CountdownToStart:
                countdownToStartTimer -= Time.deltaTime;
                if (countdownToStartTimer < 0f)
                {
                    state = State.GamePlaying;
                    gamePlayingTimer = gamePlayingTimerMax;
                    OnStateChanged?.Invoke(this, EventArgs.Empty); // UI'a oyunun baþladýðýný haber ver
                }
                break;

            case State.GamePlaying:
                gamePlayingTimer -= Time.deltaTime;
                if (gamePlayingTimer < 0f)
                {
                    state = State.GameOver;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;

            case State.GameOver:
                break;
        }
    }

    public bool IsGamePlaying() => state == State.GamePlaying;
    public bool IsCountdownToStartActive() => state == State.CountdownToStart;
    public float GetCountdownToStartTimer() => countdownToStartTimer;
    public bool IsGameOver() => state == State.GameOver;
    public float GetGamePlayingTimerNormalized() => 1 - (gamePlayingTimer / gamePlayingTimerMax);

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