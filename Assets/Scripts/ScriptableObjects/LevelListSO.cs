using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelConfig
{
    public string levelName = "Bölüm";
    public float durationSeconds = 180f; // bölüm süresi
    public int targetOrders = 5;         // tamamlanması gereken sipariş sayısı
}

// Tüm bölümlerin tanımı. Bir asset olarak oluştur (Create → ScriptableObjects → LevelListSO)
// ve hem LevelSelectUI'a hem GameManager'a sürükle.
[CreateAssetMenu(menuName = "ScriptableObjects/LevelListSO")]
public class LevelListSO : ScriptableObject
{
    public List<LevelConfig> levels = new List<LevelConfig>();
}

// Seçilen bölümü sahneler arasında taşır. Statik olduğu için sahne yüklemeleri
// arasında değerini korur (oyun kapanana kadar).
public static class LevelSelection
{
    public static int CurrentLevelIndex = 0;
}
