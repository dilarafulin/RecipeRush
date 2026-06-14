using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class SousChefCommandUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private SousChefTaskManager taskManager;
    [SerializeField] private SousChefAgent agent; // YEN�: Tezgaha "Ajan�n durumu ne?" diye sorabilmek i�in
    [SerializeField] private LayerMask countersLayerMask;

    [SerializeField] private ChopAndPlateChain ChopChain;
    [SerializeField] private CookAndPlateChain CookChain;


    [Header("UI Elemanlar�")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Transform buttonParent;
    [SerializeField] private Button buttonPrefab;

    private bool menuOpen = false;
    private void Update()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (menuOpen) CloseMenu();
            else TryOpenMenu();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
    }

    private void TryOpenMenu()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, countersLayerMask))
        {
            BaseCounter counter = hit.collider.GetComponent<BaseCounter>();

            // Menüyü HER ZAMAN aç — ajan elinde bir şey tutarken/meşgulken de komut
            // verilebilmeli. Komutun uygulanabilirliğini AssignTaskBasedOnContext karar verir
            // (boşsa hemen, elinde bir şey varsa mevcut işini bitirince sıraya alır).
            if (counter != null)
                OpenMenu(counter);
        }
    }

    private void OpenMenu(BaseCounter clickedCounter)
    {
        // 1. �nceki a��l��tan kalan eski butonlar� temizle
        foreach (Transform child in buttonParent)
            Destroy(child.gameObject);

        // 2. ATOMİK BUTON: her zaman göster. AssignTaskBasedOnContext deferral'ı yönetir:
        //    ajan boşsa komutu hemen verir, elinde bir şey varsa mevcut işini bitirip
        //    SONRA bu komutu yapar, ardından zincirine kaldığı yerden devam eder.
        {
            Button btnAtomik = Instantiate(buttonPrefab, buttonParent);
            btnAtomik.GetComponentInChildren<TextMeshProUGUI>().text = GetLabelForCounter(clickedCounter);
            btnAtomik.onClick.AddListener(() =>
            {
                taskManager.AssignTaskBasedOnContext(clickedCounter);
                CloseMenu();
            });
        }

        // 3. MAKRO BUTON: E�er t�klanan tezgah malzeme �reten bir Kasaysa, "Otomasyon" butonunu ekle
        if (clickedCounter is SourceCounter sourceCounter)
        {
            // BUTON 1: OTONOM DO�RAMA
            Button btnChop = Instantiate(buttonPrefab, buttonParent);
            btnChop.GetComponentInChildren<TextMeshProUGUI>().text = "Otonom Do�rama";
            btnChop.GetComponent<Image>().color = new Color(1f, 0.8f, 0.2f); // Sar�
            btnChop.onClick.AddListener(() =>
            {
                if (ChopChain != null)
                {
                    ChopChain.SetSourceCounter(sourceCounter);
                    taskManager.StartChain(ChopChain);
                }
                CloseMenu();
            });

            // BUTON 2: OTONOM P���RME
            Button btnCook = Instantiate(buttonPrefab, buttonParent);
            btnCook.GetComponentInChildren<TextMeshProUGUI>().text = "Otonom Pi�irme";
            btnCook.GetComponent<Image>().color = new Color(1f, 0.4f, 0.2f); // Turuncu (Farkl� renk)
            btnCook.onClick.AddListener(() =>
            {
                if (CookChain != null)
                {
                    CookChain.SetSourceCounter(sourceCounter);
                    taskManager.StartChain(CookChain);
                }
                CloseMenu();
            });
        }

        // 4. Men�y� mouse'un oldu�u koordinata ta�� ve g�r�n�r yap 
        menuPanel.transform.position = Mouse.current.position.ReadValue();
        menuPanel.SetActive(true);
        menuOpen = true;
    }

    // Tezgah tipine göre buton etiketi (gerçek komut, çalışma anında
    // AssignTaskBasedOnContext → GetTaskForAgent ile çözülür; bu sadece ipucu)
    private string GetLabelForCounter(BaseCounter counter)
    {
        switch (counter)
        {
            case SourceCounter _: return "Malzemeyi Al";
            case PlatesCounter _: return "Tabak Al";
            case CuttingCounter _: return "Kesme Tahtası";
            case StoveCounter _: return "Ocak";
            case DeliveryCounter _: return "Teslim Et";
            case ClearCounter _: return "Tezgaha Bırak / Al";
            default: return "Komut Ver";
        }
    }

    private void CloseMenu()
    {
        menuPanel.SetActive(false);
        menuOpen = false;
    }
}