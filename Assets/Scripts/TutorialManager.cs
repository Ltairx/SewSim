using System;
using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [Serializable]
    public class TutorialStep
    {
        [TextArea] public string instruction;
        public GameObject highlightTarget; 
        public string actionID;            
        public bool completeByButton;         
    }

    public List<TutorialStep> steps;
    private int currentStepIndex = 0;
    private TutorialHighlighter lastHighlighter;
    
    [Header("UI References")]
    public TMPro.TextMeshProUGUI tutorialText;
    public GameObject nextButton;

    public void StartTutorial()
    {
        gameObject.SetActive(true);
    }

    void Start() => ShowStep(0);

    private void ShowStep(int index)
    {
        currentStepIndex = index;
        var step = steps[index];
        
        tutorialText.text = step.instruction;
        nextButton.SetActive(step.completeByButton);
        
        HighlightElement(step.highlightTarget);
    }

    private void HighlightElement(GameObject highlightTarget)
    {
        if (lastHighlighter != null)
        {
            lastHighlighter.SetHighlight(false);
        }

        if (highlightTarget != null)
        {
            TutorialHighlighter highlighter = highlightTarget.GetComponent<TutorialHighlighter>();
        
            if (highlighter != null)
            {
                highlighter.SetHighlight(true);
                lastHighlighter = highlighter;
            }
        }
    }

    public void OnActionPerformed(string actionID)
    {
        if (steps[currentStepIndex].actionID == actionID)
        {
            NextStep();
        }
    }

    public void NextStep()
    {
        if (currentStepIndex + 1 < steps.Count)
            ShowStep(currentStepIndex + 1);
        else
            EndTutorial();
    }

    private void EndTutorial()
    {
        Debug.Log("Tutorial skonczony");
    }
}