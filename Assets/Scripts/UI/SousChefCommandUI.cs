using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class SousChefCommandUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private SousChefTaskManager taskManager;
    [SerializeField] private SousChefAgent agent; // YENÝ: Tezgaha "Ajanýn durumu ne?" diye sorabilmek için
    [SerializeField] private LayerMask countersLayerMask;

    [SerializeField] private ChopAndPlateChain ChopChain;

    [Header("UI Elemanlarý")]
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

            if (counter != null)
            {
                // ARTIK HARDCODE DEÐÝL: Tezgaha akýllý görevi soruyoruz!
                SousChefTask smartTask = counter.GetTaskForAgent(agent);

                // Eðer tezgah mantýklý bir görev döndürdüyse menüyü aç
                if (smartTask != null)
                {
                    OpenMenu(counter,smartTask);
                }
                else
                {
                    Debug.Log("Bu durumda yapýlabilecek bir iþlem yok.");
                    // Ýsteðe baðlý: Ekranda küçük bir kýrmýzý uyarý çýkartabilirsin
                }
            }
        }
    }

    private void OpenMenu(BaseCounter clickedCounter, SousChefTask task)
    {
        // 1. Önceki açýlýþtan kalan eski butonlarý temizle 
        foreach (Transform child in buttonParent)
            Destroy(child.gameObject);

        // 2. ATOMÝK BUTON: Eðer ajan için anlýk bir görev (Örn: Fetch) varsa oluþtur
        if (task != null)
        {
            Button btnAtomik = Instantiate(buttonPrefab, buttonParent);
            btnAtomik.GetComponentInChildren<TextMeshProUGUI>().text = GetLabelForCommand(task.command);
            btnAtomik.onClick.AddListener(() =>
            {
                taskManager.GiveCommand(task.command, task.targetCounter, task.targetItemSO);
                CloseMenu();
            });
        }

        // 3. MAKRO BUTON: Eðer týklanan tezgah malzeme üreten bir Kasaysa, "Otomasyon" butonunu ekle
        if (clickedCounter is SourceCounter sourceCounter)
        {
            Button btnMakro = Instantiate(buttonPrefab, buttonParent);
            btnMakro.GetComponentInChildren<TextMeshProUGUI>().text = "Ýstasyonu Yönet (Otonom)";

            // Opsiyonel: Makro butonun rengini farklý yaparak (örn: Sarý) oyuncunun ayýrt etmesini saðla
            btnMakro.GetComponent<Image>().color = new Color(1f, 0.8f, 0.2f);

            btnMakro.onClick.AddListener(() =>
            {
                if (ChopChain != null) // Sende adý ChopAndPlateChain ise onu kullan
                {
                    // 1. ADIM: Sadece hedef kasayý zincire fýsýlda (Baþlatma yok)
                    ChopChain.SetSourceCounter(sourceCounter);

                    // 2. ADIM: Baþlatma iþini Yönetmene (TaskManager) býrak. 
                    // TaskManager önce zincire kendini tanýtacak (Initialize), SONRA zinciri baþlatacak!
                    taskManager.StartChain(ChopChain);
                }
                else
                {
                    Debug.LogError("Zincir referansý Inspector'dan UI'a atanmamýþ!");
                }
                CloseMenu();
            });
        }

        // 4. Menüyü mouse'un olduðu koordinata taþý ve görünür yap 
        menuPanel.transform.position = Mouse.current.position.ReadValue();
        menuPanel.SetActive(true);
        menuOpen = true;
    }

    // YENÝ: Ajanýn komutunu oyuncunun okuyabileceði güzel bir metne çevirir
    private string GetLabelForCommand(SousChefCommand cmd)
    {
        switch (cmd)
        {
            case SousChefCommand.FetchIngredient: return "Malzemeyi Al";
            case SousChefCommand.ChopIngredient: return "Malzemeyi Kes";
            case SousChefCommand.CookIngredient: return "Malzemeyi Piþir";
            case SousChefCommand.DeliverToCounter: return "Buraya Býrak";
            default: return "Ýþlem Yap";
        }
    }

    private void CloseMenu()
    {
        menuPanel.SetActive(false);
        menuOpen = false;
    }
}