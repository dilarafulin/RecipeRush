using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Seviye HUD'u + bölüm sonu / kazanma / kaybetme paneli.
// Referansları Inspector'dan bağla; mantık tamamen GameManager'da.
public class GameStatusUI : MonoBehaviour
{
    [Header("Üst HUD (sürekli görünür)")]
    [SerializeField] private TextMeshProUGUI levelText;   // "Bölüm 1/3"
    [SerializeField] private TextMeshProUGUI ordersText;  // "3 / 5"
    [SerializeField] private TextMeshProUGUI timeText;    // kalan saniye

    [Header("Durum Paneli (bölüm sonu/kazandın/kaybettin)")]
    [SerializeField] private GameObject statusPanel;      // ortadaki büyük panel
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button retryButton;          // sadece kaybedince görünür
    [SerializeField] private Button restartButton;        // sadece kazanınca görünür

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        gm.OnStateChanged += (sender, e) => RefreshPanel();
        gm.OnLevelCompleted += (sender, e) =>
            ShowMessage($"Bölüm {gm.GetLevelNumber()} tamamlandı!", showRetry: false, showRestart: false);
        gm.OnGameWon += (sender, e) =>
            ShowMessage("Tebrikler! Oyunu bitirdin!", showRetry: false, showRestart: true);
        gm.OnGameLost += (sender, e) =>
            ShowMessage("Kaybettin — süre doldu.", showRetry: true, showRestart: false);

        if (retryButton != null) retryButton.onClick.AddListener(() => GameManager.Instance.RetryLevel());
        if (restartButton != null) restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());

        if (statusPanel != null) statusPanel.SetActive(false);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (levelText != null) levelText.text = $"Bölüm {gm.GetLevelNumber()}/{gm.GetTotalLevels()}";
        if (ordersText != null) ordersText.text = $"{gm.GetDeliveredThisLevel()} / {gm.GetTargetOrders()}";
        if (timeText != null) timeText.text = Mathf.CeilToInt(gm.GetTimeRemaining()).ToString();
    }

    private void ShowMessage(string msg, bool showRetry, bool showRestart)
    {
        if (statusPanel != null) statusPanel.SetActive(true);
        if (statusText != null) statusText.text = msg;
        if (retryButton != null) retryButton.gameObject.SetActive(showRetry);
        if (restartButton != null) restartButton.gameObject.SetActive(showRestart);
    }

    // Oyun tekrar oynanır duruma (geri sayım / oynama) geçince paneli gizle
    private void RefreshPanel()
    {
        GameManager gm = GameManager.Instance;
        if (statusPanel != null && (gm.IsGamePlaying() || gm.IsCountdownToStartActive()))
            statusPanel.SetActive(false);
    }
}
