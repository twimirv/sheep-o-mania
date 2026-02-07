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

    [Header("UI References")]
    public TMPro.TextMeshProUGUI timerText; 
    public TMPro.TextMeshProUGUI sheepCounterText;

    private float _timeRemaining;
    private bool _isGameOver = false;
    private AlphaSheepController _player;

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
        SetupTextBackground(timerText);
        SetupTextBackground(sheepCounterText);
        
        _player = FindFirstObjectByType<AlphaSheepController>();
    }

    private void SetupTextBackground(TMPro.TextMeshProUGUI textComp)
    {
        if (textComp == null) return;

        // Check if background already exists
        Transform existingBg = textComp.transform.parent.Find("TimerBackground");
        if (existingBg != null) return;

        // Create Background Object
        GameObject bgObj = new GameObject("TimerBackground");
        bgObj.transform.SetParent(textComp.transform.parent, false);
        bgObj.transform.SetSiblingIndex(textComp.transform.GetSiblingIndex()); // Render behind text

        // Setup RectTransform to match Text
        RectTransform textRect = textComp.GetComponent<RectTransform>();
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        
        // Copy anchors and position
        bgRect.anchorMin = textRect.anchorMin;
        bgRect.anchorMax = textRect.anchorMax;
        bgRect.pivot = textRect.pivot;
        bgRect.anchoredPosition = textRect.anchoredPosition;
        bgRect.sizeDelta = textRect.sizeDelta + new Vector2(10, 10); // Add Padding

        // Add Image
        UnityEngine.UI.Image img = bgObj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.8f); // Black with slight transparency
        img.sprite = GenerateRoundedSprite();
        img.type = UnityEngine.UI.Image.Type.Sliced; // Enable slicing for corners

        // Ensure text is centered
        textComp.alignment = TMPro.TextAlignmentOptions.Center;
    }

    private Sprite GenerateRoundedSprite()
    {
        int width = 64;
        int height = 64;
        int radius = 8; // Reduced from 16
        Texture2D tex = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        Color c = Color.white;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Check corners
                if (x < radius && y < radius && Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) > radius) colors[y * width + x] = Color.clear; // BL
                else if (x < radius && y >= height - radius && Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) > radius) colors[y * width + x] = Color.clear; // TL
                else if (x >= width - radius && y < radius && Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) > radius) colors[y * width + x] = Color.clear; // BR
                else if (x >= width - radius && y >= height - radius && Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1)) > radius) colors[y * width + x] = Color.clear; // TR
                else colors[y * width + x] = c;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        
        // Create Sprite with borders for slicing
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
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
            
            // Update Timer UI
            if (timerText != null)
            {
                timerText.text = GetFormattedTime();
            }
            
            // Update Sheep Counter UI
            if (sheepCounterText != null && _player != null)
            {
                sheepCounterText.text = $"Sheep: {_player.FollowerCount}";
            }
            else if (sheepCounterText != null && _player == null)
            {
                // Try finding player again if null
                _player = FindFirstObjectByType<AlphaSheepController>();
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
        var player = FindFirstObjectByType<AlphaSheepController>();
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
        var enemies = FindObjectsByType<EnemyAlphaSheepController>(FindObjectsSortMode.None);
        
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
