using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI; 

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject levelSelectPanel; // Play'e basınca açılan bölüm seçim paneli

    private void Awake()
    {
        // Play artık doğrudan oyunu açmaz; bölüm seçim panelini gösterir
        playButton.onClick.AddListener(() =>
        {
            if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
            else Loader.Load(Loader.Scene.GameScene); // panel atanmadıysa eski davranış
        });

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
    }
}