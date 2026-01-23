using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject sewMenuCanvas;
    [SerializeField] private GameObject tutorialCanvas;
    
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
