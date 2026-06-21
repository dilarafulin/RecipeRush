using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI; 

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button levelBackButton;      // level ekranındaki "Geri" (opsiyonel)
    [SerializeField] private GameObject mainMenuPanel;    // logo + Play + Quit grubu
    [SerializeField] private GameObject levelSelectPanel; // bölüm seçim ekranı

    private void Awake()
    {
        playButton.onClick.AddListener(ShowLevelSelect);
        quitButton.onClick.AddListener(() => Application.Quit());
        if (levelBackButton != null) levelBackButton.onClick.AddListener(ShowMainMenu);

        ShowMainMenu(); // başlangıç: menü açık, level paneli kapalı
    }

    // Play → ana menüyü GİZLE, bölüm seçimini GÖSTER (opak arka plan yok)
    public void ShowLevelSelect()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }

    // Geri → bölüm seçimini gizle, ana menüyü göster
    public void ShowMainMenu()
    {
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }
}