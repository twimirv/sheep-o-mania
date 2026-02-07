using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI playerStatsText;
    public TextMeshProUGUI enemyStatsText;

    private void Start()
    {
        DisplayStats();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void DisplayStats()
    {
        // Player Stats
        if (playerStatsText != null)
        {
            playerStatsText.text = $"YOUR HERD SIZE: {GameOverStats.PlayerHerdCount}";
        }

        // Enemy Stats
        if (enemyStatsText != null)
        {
            if (GameOverStats.TopEnemyHerdCounts.Count > 0)
            {
                string enemyText = "TOP ENEMY HERDS:\n";
                for (int i = 0; i < GameOverStats.TopEnemyHerdCounts.Count; i++)
                {
                    enemyText += $"{i + 1}. Size: {GameOverStats.TopEnemyHerdCounts[i]}\n";
                }
                enemyStatsText.text = enemyText;
            }
            else
            {
                enemyStatsText.text = "NO ENEMIES SURVIVED";
            }
        }
    }

    public void OnRestartButton()
    {
        SceneManager.LoadScene("tanelin_rakennusmaailma");
    }

    public void OnMainMenuButton()
    {
        // Ideally load a main menu, but for now we can just quit or reload
        // SceneManager.LoadScene("MainMenu");
        Application.Quit();
    }
}
