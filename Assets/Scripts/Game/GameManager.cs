﻿using Lofelt.NiceVibrations;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public GameplayController gameplayController;

    [Header("Config")]
    public GameConfigs gameConfigs;

    [Header("Settings")]
    public bool disableRemoteConfig = false;

    public GameState CurrentGameState { get; private set; }

    public AudioSource AudioSource { get; private set; }

    public bool IsLoadingFinished
    {
        get { return CurrentGameState != GameState.None && CurrentGameState != GameState.Loading; }
    }

    public bool IsVibrationEnabled
    {
        get { return PlayerPrefs.GetInt(PlayerPrefKeys.IsVibrationEnabled, 1) == 1; }
        set
        {
            if (value != IsVibrationEnabled)
            {
                PlayerPrefs.SetInt(PlayerPrefKeys.IsVibrationEnabled, value ? 1 : 0);
                VibrationSettingChanged(value);
            }
        }
    }

    public int LinearLevelIndex
    {
        get { return PlayerPrefs.GetInt(PlayerPrefKeys.LinearLevelIndex, 0); }
        set { PlayerPrefs.SetInt(PlayerPrefKeys.LinearLevelIndex, value); }
    }

    public int PlayerHighScore
    {
        get { return PlayerPrefs.GetInt(PlayerPrefKeys.PlayerHighScore, 0); }
        set { PlayerPrefs.SetInt(PlayerPrefKeys.PlayerHighScore, value); }
    }

    private float _lastHapticTime;
    private float _lastSoundTime;

    public event Action<GameState /*Old*/, GameState /*New*/> OnGameStateChanged;
    public event Action<int> OnChangeScoreBoard;

    private void Awake()
    {
        Instance = this;

        AudioSource = this.gameObject.AddComponent<AudioSource>();
        SRDebug.Instance.PanelVisibilityChanged += SRDebug_PanelVisibilityChanged;
    }

    private void SRDebug_PanelVisibilityChanged(bool isVisible)
    {
        Time.timeScale = isVisible ? 0f : 1f;
    }

    private void Start()
    {
        if (GameConfigs.Instance == null)
        {
            gameConfigs = GameObject.Instantiate(gameConfigs);
            gameConfigs.Initialize();
        }
        else
        {
            gameConfigs = GameConfigs.Instance;
        }

        gameplayController.OnGameplayFinished += GameplayControllerOnGameplayFinished;
        gameplayController.OnChangeHighScore += GameplayControllerOnChangeHighScore;
        DG.Tweening.DOTween.SetTweensCapacity(500, 500);

        VibrationSettingChanged(IsVibrationEnabled);

        ChangeCurrentGameState(GameState.Loading);
    }

    private void VibrationSettingChanged(bool enabled)
    {
        HapticController.hapticsEnabled = enabled;
    }

    private void ChangeCurrentGameState(GameState newGameState)
    {
        var oldGameState = CurrentGameState;
        CurrentGameState = newGameState;
        OnGameStateChanged?.Invoke(oldGameState, CurrentGameState);
    }

    public void InitializeAfterLoading()
    {
#if UNITY_EDITOR
        Application.targetFrameRate = 9999;
#else
        Application.targetFrameRate = 60;
#endif
        gameplayController.Initialize();
    }

    public void ResetSaveData()
    {
        PlayerPrefs.DeleteAll();

        HardRestart();
    }

    public void PrepareGameplay()
    {
        gameplayController.PrepareGameplay(LinearLevelIndex);
        StartGameplay();
    }

    public void StartGameplay()
    {
        gameplayController.StartGameplay();

        ChangeCurrentGameState(GameState.Gameplay);
    }

    private void GameplayControllerOnGameplayFinished(bool success)
    {
        if (success)
        {
            ChangeCurrentGameState(GameState.FinishSuccess);
            DoHaptic(HapticPatterns.PresetType.Success, true);
        }
        else
        {
            ChangeCurrentGameState(GameState.FinishFail);
            DoHaptic(HapticPatterns.PresetType.Failure, true);
        }
    }

    private void GameplayControllerOnChangeHighScore(int score)
    {
        PlayerHighScore = score > PlayerHighScore ? score : PlayerHighScore;
        OnChangeScoreBoard?.Invoke(score);
    }

    public void FullyFinishGameplay()
    {
        LinearLevelIndex += 1;

        gameplayController.UnloadGameplay();
        PrepareGameplay();
    }

    public void RetryGameplay()
    {
        gameplayController.UnloadGameplay();
        PrepareGameplay();
    }

    public void HardRestart()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    public static void DoHaptic(HapticPatterns.PresetType hapticType, bool dominate = false)
    {
        if (Instance == null)
            return;

        if (dominate || Time.time - Instance._lastHapticTime >= Instance.gameConfigs.HapticIntervalLimit)
        {
            HapticPatterns.PlayPreset(hapticType);
            Instance._lastHapticTime = Time.time;
        }
    }

    public static void PlaySound(AudioClip audioClip, float volume = 0.4f, bool dominate = false)
    {
        if (Instance == null)
            return;

        if (dominate || Time.time - Instance._lastSoundTime >= Instance.gameConfigs.SoundIntervalLimit)
        {
            Instance.AudioSource.volume = volume * GameConfigs.Instance.SoundVolumeMultiplier;
            Instance.AudioSource.PlayOneShot(audioClip);
            Instance._lastSoundTime = Time.time;
        }
    }
}

public enum GameState
{
    None,
    Loading,
    // Menu,
    Gameplay,
    FinishSuccess,
    FinishFail
}
