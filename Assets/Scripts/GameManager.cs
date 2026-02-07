using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [Tooltip("Time limit in minutes")]
    public float timeLimitMinutes = 5f;

    [Header("UI References (Optional for now)")]
    // public TMPro.TextMeshProUGUI timerText; 

    private float _timeRemaining;
    private bool _isGameOver = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        _timeRemaining = timeLimitMinutes * 60f;
    }

    private void Update()
    {
        if (_isGameOver) return;

        if (_timeRemaining > 0)
        {
            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0)
            {
                _timeRemaining = 0;
                EndGame();
            }
        }
    }

    // Helper to get formatted time for UI
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(_timeRemaining / 60F);
        int seconds = Mathf.FloorToInt(_timeRemaining - minutes * 60);
        return string.Format("{0:0}:{1:00}", minutes, seconds);
    }

    private void EndGame()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        Debug.Log("Game Over! Time's up.");

        // 1. Gather Stats
        GameOverStats.Reset();

        // Player Stats
        var player = FindObjectOfType<AlphaSheepController>();
        if (player != null)
        {
            GameOverStats.PlayerHerdCount = player.FollowerCount;
        }
        else
        {
             // Fallback if player destroyed (unlikely in this game mode but possible)
             GameOverStats.PlayerHerdCount = 0;
        }

        // Enemy Stats
        var enemies = FindObjectsOfType<EnemyAlphaSheepController>();
        
        // Sort enemies by herd size (Descending)
        // We need to add a public property to EnemyAlphaSheepController to get herd size
        var sortedEnemies = enemies.OrderByDescending(e => e.HerdCount).ToList();

        // Take Top 3
        int count = Mathf.Min(sortedEnemies.Count, 3);
        for (int i = 0; i < count; i++)
        {
            GameOverStats.TopEnemyHerdCounts.Add(sortedEnemies[i].HerdCount);
        }

        Debug.Log($"Stats Collected. Player: {GameOverStats.PlayerHerdCount}. Enemies: {enemies.Length}");

        // 2. Load Game Over Scene
        // Ensure "game_over" is in Build Settings
        SceneManager.LoadScene("game_over");
    }
}
