using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public enum TaskType
{
    None,
    ConnectFabrics,
    Make10Stitches
}

public class Quest : MonoBehaviour
{
    public SewingMachine sewingMachine;
    public UIManager uiManager;

    [Header("UI References")]
    public Button startConnectTaskBtn;
    public Button startStitchTaskBtn;

    [Header("Task State")]
    private bool _isTaskActive = false;
    private TaskType _currentTaskType = TaskType.None;
    private float _startTime;

    private int _stitchesCount = 0;
    private float _totalAccuracyAccumulator = 0f; 
    private int _accuracySampleCount = 0;

    private void Start()
    {

        sewingMachine.OnStitchCreated += HandleStitchCreated;
        sewingMachine.OnClothConnected += HandleClothConnected;
    }

    private void OnDestroy()
    {
        if (sewingMachine)
        {
            sewingMachine.OnStitchCreated -= HandleStitchCreated;
            sewingMachine.OnClothConnected -= HandleClothConnected;
        }
    }

    public void StartButton_ConnectFabrics()
    {
        StartTask(TaskType.ConnectFabrics);
    }

    public void StartButton_Make10Stitches()
    {
        StartTask(TaskType.Make10Stitches);
    }

    private void StartTask(TaskType type)
    {
        if (_isTaskActive)
        {
            return;
        }

        _isTaskActive = true;
        _currentTaskType = type;
        _startTime = Time.time;

        _stitchesCount = 0;
        _totalAccuracyAccumulator = 0f;
        _accuracySampleCount = 0;

    }

    private void HandleStitchCreated(float accuracy)
    {
        if (!_isTaskActive) return;

        if (_currentTaskType == TaskType.Make10Stitches)
        {
            _stitchesCount++;
            _totalAccuracyAccumulator += accuracy;
            _accuracySampleCount++;

            if (_stitchesCount >= 10)
            {
                FinishTask();
            }
        }
    }

    private void HandleClothConnected(float accuracy)
    {
        if (!_isTaskActive) return;

        if (_currentTaskType == TaskType.ConnectFabrics)
        {
            _totalAccuracyAccumulator += accuracy;
            _accuracySampleCount++;

            FinishTask();
        }
    }


    private void FinishTask()
    {
        _isTaskActive = false;
        float endTime = Time.time;
        float duration = endTime - _startTime;

        float finalAccuracy = _accuracySampleCount > 0 ? (_totalAccuracyAccumulator / _accuracySampleCount) : 0f;

        int score = Mathf.RoundToInt((finalAccuracy * 1000) - (duration * 10));
        if (score < 0) score = 0;

        _currentTaskType = TaskType.None;
        uiManager.OpenSumUpMenu(score, finalAccuracy, duration);
    }
}
