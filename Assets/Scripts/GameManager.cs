using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public GameObject mainMenuLevel;
    public GameObject sewLevel;
    
    [field: SerializeField] private TutorialManager _tutorialManager;
    [field: SerializeField] private UIManager _UIManager;
    [SerializeField] private InputActionReference bButtonAction;

    private void OnEnable()
    {
        sewLevel.SetActive(false);
        mainMenuLevel.SetActive(true);
        bButtonAction.action.performed += OpenMainMenu;
    }

    private void OpenMainMenu(InputAction.CallbackContext obj)
    {
        OpenMainMenu();
    }

    public void OpenMainMenu()
    {
        _UIManager.OpenMainMenu();
        ToggleLevel();
    }
    
    public void StartGame()
    {
        if (!_tutorialManager) return;
        _tutorialManager.OnTutorialComplete += _UIManager.OpenSewMenu;

        ToggleLevel();
        
        _tutorialManager.StartTutorial();
        _UIManager.OpenTutorialCanvas();
    }

    private void ToggleLevel()
    {
        sewLevel.SetActive(!sewLevel.activeSelf);
        mainMenuLevel.SetActive(!mainMenuLevel.activeSelf);
    }
    
    public void CloseGame()
    {
        Application.Quit();
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    
    private void OnDisable()
    {
        _tutorialManager.OnTutorialComplete -= _UIManager.OpenSewMenu;
        bButtonAction.action.performed -= OpenMainMenu;
    }
}