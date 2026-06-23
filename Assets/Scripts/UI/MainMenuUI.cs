using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI; 

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button continueButton;       
    [SerializeField] private Button quitButton;
    [SerializeField] private Button levelBackButton;       
    [SerializeField] private GameObject mainMenuPanel;    
    [SerializeField] private GameObject levelSelectPanel;  

    private void Awake()
    {
        playButton.onClick.AddListener(ShowLevelSelect);
        quitButton.onClick.AddListener(() => Application.Quit());
        if (levelBackButton != null) levelBackButton.onClick.AddListener(ShowMainMenu);

        if (continueButton != null)
        {
            // Kayıtlı ilerleme yoksa "Devam Et"i gizle
            continueButton.gameObject.SetActive(SaveManager.HasProgress());
            continueButton.onClick.AddListener(ContinueLastLevel);
        }

        ShowMainMenu(); // başlangıç: menü açık, level paneli kapalı
    }

    // Son oynanan bölümü doğrudan yükler
    public void ContinueLastLevel()
    {
        LevelSelection.CurrentLevelIndex = SaveManager.LastPlayedLevel;
        Time.timeScale = 1f;
        Loader.Load(Loader.Scene.GameScene);
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