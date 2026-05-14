using System;
using UnityEngine;
using TMPro; // TextMeshPro kütüphanesi

public class GameStartCountdownUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countdownText;

    private void Start()
    {
        GameManager.Instance.OnStateChanged += GameManager_OnStateChanged;

        // Oyun baþlar baþlamaz durumu kontrol et. 
        // Eðer GameManager "Geri Sayým" modundaysa ekraný hemen göster!
        if (GameManager.Instance.IsCountdownToStartActive())
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void GameManager_OnStateChanged(object sender, EventArgs e)
    {
        // Eðer GameManager "Geri Sayým" durumuna geçtiyse bu UI'ý göster
        if (GameManager.Instance.IsCountdownToStartActive())
        {
            Show();
        }
        else // Baþka bir durumdaysa (Oyun baþladýysa vs.) UI'ý gizle
        {
            Hide();
        }
    }

    private void Update()
    {
        // Sadece geri sayým aktifken Update içinde yazýyý güncelle
        if (GameManager.Instance.IsCountdownToStartActive())
        {
            // GameManager'dan kalan süreyi al (Örn: 2.871 saniye)
            float timer = GameManager.Instance.GetCountdownToStartTimer();

            // Mathf.CeilToInt: Küsüratlý sayýyý daima bir ÜST tam sayýya yuvarlar. 
            // (2.1 -> 3 yapar. Böylece ekranda 0 görmeyiz, 3-2-1 yazar)
            int countdownNumber = Mathf.CeilToInt(timer);

            countdownText.text = countdownNumber.ToString();
        }
    }

    private void Show()
    {
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // Bu UI silinirse memory leak (hafýza sýzýntýsý) olmamasý için aboneliði kaldýr
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= GameManager_OnStateChanged;
        }
    }
}