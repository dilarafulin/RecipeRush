using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class SousChefCommandUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private SousChefTaskManager taskManager;
    [SerializeField] private Player player;
    [SerializeField] private LayerMask countersLayerMask;

    [Header("UI Elemanlarý")]
    [SerializeField] private GameObject menuPanel;   // Menünün arka plan kutusu 
    [SerializeField] private Transform buttonParent; // Butonlarýn içine dizileceði yer 
    [SerializeField] private Button buttonPrefab;    // Çoðaltacaðýmýz buton þablonu 

    private bool menuOpen = false;

    private void Update()
    {
        // Yeni Input Sistemi ile Mouse Sað Týk kontrolü
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (menuOpen) CloseMenu();
            else TryOpenMenu();
        }

        // Yeni Input Sistemi ile ESC tuþu kontrolü
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
    }

    private void TryOpenMenu()
    {
        // 1. Mouse imlecinin olduðu yerden ekranýn derinliðine doðru bir ýþýn (Ray) oluþtur
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        // 2. Bu ýþýn 100 birim boyunca gidip bir Counter'a çarpýyor mu bak
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, countersLayerMask))
        {
            // 3. Çarptýðý objedeki Counter bileþenini al
            BaseCounter counter = hit.collider.GetComponent<BaseCounter>();

            if (counter != null)
            {
                // O tezgaha uygun komutlarý hazýrla
                List<(string label, SousChefCommand cmd)> commands = GetCommandsFor(counter);

                if (commands.Count > 0)
                {
                    OpenMenu(counter, commands);
                }
            }
        }
    }

    // BAÐLAMA DUYARLI MANTIK (Context-Sensitive)
    private List<(string, SousChefCommand)> GetCommandsFor(BaseCounter counter)
    {
        var list = new List<(string, SousChefCommand)>();

        if (counter is SourceCounter)
            list.Add(("Malzeme Getir", SousChefCommand.FetchIngredient));
        else if (counter is CuttingCounter)
            list.Add(("Malzemeyi Kes", SousChefCommand.ChopIngredient));
        else if (counter is StoveCounter)
            list.Add(("Malzemeyi Piþir", SousChefCommand.CookIngredient));
        // Teslimat tezgahlarý (ClearCounter vb.) için eklenebilir 
        else if (counter is ClearCounter)
            list.Add(("Buraya Býrak", SousChefCommand.DeliverToCounter));

        return list;
    }

    private void OpenMenu(BaseCounter counter, List<(string label, SousChefCommand cmd)> commands)
    {
        // 1. Önceki açýlýþtan kalan eski butonlarý temizle 
        foreach (Transform child in buttonParent)
            Destroy(child.gameObject);

        // 2. Yeni butonlarý yarat 
        foreach (var (label, cmd) in commands)
        {
            Button btn = Instantiate(buttonPrefab, buttonParent);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = label;

            // CS Kuralý: Lambda Capture (Closure) için lokal kopya oluþturmak zorundayýz! 
            SousChefCommand capturedCmd = cmd;
            BaseCounter capturedCounter = counter;
            btn.onClick.AddListener(() =>
            {
                taskManager.GiveCommand(capturedCmd, capturedCounter);
                CloseMenu();
            });
        }

        // 3. Menüyü mouse'un olduðu koordinata taþý ve görünür yap 
        menuPanel.transform.position = Mouse.current.position.ReadValue();
        menuPanel.SetActive(true);
        menuOpen = true;
    }

    private void CloseMenu()
    {
        menuPanel.SetActive(false);
        menuOpen = false;
    }
}