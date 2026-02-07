using System.Collections.Generic;

public static class GameOverStats
{
    public static int PlayerHerdCount;
    public static List<int> TopEnemyHerdCounts = new List<int>();

    public static void Reset()
    {
        PlayerHerdCount = 0;
        TopEnemyHerdCounts.Clear();
    }
}
