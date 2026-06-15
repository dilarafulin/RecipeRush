using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Ana menüde "Başla"ya basınca açılan bölüm seçim ekranı.
// Her bölüm için bir buton üretir; tıklanınca o bölümle GameScene'i yükler.
public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] private LevelListSO levelList;     // tüm bölüm tanımları (asset)
    [SerializeField] private Transform buttonContainer; // butonların dizileceği yer
    [SerializeField] private Button buttonTemplate;     // tek bir bölüm butonu şablonu

    private void Start()
    {
        if (levelList == null)
        {
            Debug.LogError("LevelSelectUI: LevelListSO atanmamış!");
            return;
        }

        buttonTemplate.gameObject.SetActive(false);

        for (int i = 0; i < levelList.levels.Count; i++)
        {
            int index = i; // closure için kopya
            LevelConfig cfg = levelList.levels[i];

            Button btn = Instantiate(buttonTemplate, buttonContainer);
            btn.gameObject.SetActive(true);

            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = $"Bölüm {index + 1}\n{cfg.targetOrders} sipariş / {Mathf.RoundToInt(cfg.durationSeconds)}sn";

            btn.onClick.AddListener(() =>
            {
                LevelSelection.CurrentLevelIndex = index;
                Time.timeScale = 1f;
                Loader.Load(Loader.Scene.GameScene);
            });
        }
    }
}
