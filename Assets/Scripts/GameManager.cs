using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    [field: SerializeField] private TutorialManager _tutorialManager;
    
    public void StartGame()
    {
        mainMenuCanvas.SetActive(false);
        if(_tutorialManager) _tutorialManager.StartTutorial();
    }
}