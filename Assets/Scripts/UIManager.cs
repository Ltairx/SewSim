using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject sewMenuCanvas;
    [SerializeField] private GameObject tutorialCanvas;
    [SerializeField] private GameObject sumupCanvas;

    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private TextMeshProUGUI timeText;
    
    [SerializeField] private Button prevGameBtn;
    

    private void Start()
    {
        if(mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        else Debug.LogWarning("Main menu is null");
        
        if(sewMenuCanvas == null) Debug.LogWarning("Sew Menu is null");

        if (prevGameBtn != null) prevGameBtn.interactable = false; // TODO if there is a saved game it should be enabled
    }

    public void OpenSewMenu()
    {
        sewMenuCanvas.SetActive(true);
        tutorialCanvas.SetActive(false);
    }

    public void OpenSumUpMenu(float points, float accuracy, float time)
    {
        accuracyText.text = accuracy.ToString("P2") + "%";
        pointsText.text = points.ToString();
        timeText.text = time.ToString("F2");

        sumupCanvas.SetActive(true);
    }

    public void OpenTutorialCanvas()
    {
        tutorialCanvas.SetActive(true);
    }
    
    public void OpenMainMenu()
    {
        mainMenuCanvas.SetActive(true);
        
        tutorialCanvas.SetActive(false);
        sewMenuCanvas.SetActive(false);
    }

    
}
