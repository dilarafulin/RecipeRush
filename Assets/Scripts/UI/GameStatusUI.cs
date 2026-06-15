using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Seviye HUD'u + kazandın/kaybettin sonuç paneli.
// Mantık GameManager'da; bu sadece gösterim + buton bağlama.
public class GameStatusUI : MonoBehaviour
{
    [Header("Üst HUD (sürekli görünür)")]
    [SerializeField] private TextMeshProUGUI levelText;   // "BÖLÜM 1/3"
    [SerializeField] private TextMeshProUGUI ordersText;  // "3 / 5"
    [SerializeField] private TextMeshProUGUI timeText;    // kalan saniye

    [Header("Sonuç Paneli")]
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button nextButton;   // kazanınca: "Sonraki Bölüm"
    [SerializeField] private Button retryButton;  // kaybedince: "Tekrar Dene"
    [SerializeField] private Button menuButton;   // "Ana Menü"

    private void Start()
    {
        GameManager gm = GameManager.Instance;
        gm.OnLevelWon += (sender, e) => OnWon();
        gm.OnLevelLost += (sender, e) => OnLost();

        if (nextButton != null) nextButton.onClick.AddListener(() => GameManager.Instance.GoToNextLevel());
        if (retryButton != null) retryButton.onClick.AddListener(() => GameManager.Instance.RetryLevel());
        if (menuButton != null) menuButton.onClick.AddListener(() => GameManager.Instance.GoToMainMenu());

        if (statusPanel != null) statusPanel.SetActive(false);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (levelText != null) levelText.text = $"BÖLÜM {gm.GetLevelNumber()}/{gm.GetTotalLevels()}";
        if (ordersText != null) ordersText.text = $"{gm.GetDeliveredThisLevel()} / {gm.GetTargetOrders()}";
        if (timeText != null) timeText.text = Mathf.CeilToInt(gm.GetTimeRemaining()).ToString();
    }

    private void OnWon()
    {
        GameManager gm = GameManager.Instance;
        bool hasNext = gm.HasNextLevel();
        // Son bölümse "Sonraki Bölüm" yok, sadece kutlama + Ana Menü
        ShowPanel(
            hasNext ? $"BÖLÜM {gm.GetLevelNumber()} TAMAMLANDI!" : "TEBRİKLER! OYUNU BİTİRDİN!",
            showNext: hasNext, showRetry: false, showMenu: true);
    }

    private void OnLost()
    {
        ShowPanel("KAYBETTİN — süre doldu.", showNext: false, showRetry: true, showMenu: true);
    }

    private void ShowPanel(string msg, bool showNext, bool showRetry, bool showMenu)
    {
        if (statusPanel != null) statusPanel.SetActive(true);
        if (statusText != null) statusText.text = msg;
        if (nextButton != null) nextButton.gameObject.SetActive(showNext);
        if (retryButton != null) retryButton.gameObject.SetActive(showRetry);
        if (menuButton != null) menuButton.gameObject.SetActive(showMenu);
    }
}
