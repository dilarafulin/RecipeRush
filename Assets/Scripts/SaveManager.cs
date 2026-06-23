using UnityEngine;

// Oyuncu ilerlemesini cihazda kalıcı olarak saklar (PlayerPrefs).
// Bölümler süreli olduğu için bölüm-ortası kayıt yok; bunun yerine
// "hangi bölümler açıldı" ve "en son hangi bölüm oynandı" bilgisi tutulur.
public static class SaveManager
{
    private const string KeyHighestUnlocked = "RR_HighestUnlockedLevel"; // açılmış en yüksek bölüm index'i
    private const string KeyLastPlayed = "RR_LastPlayedLevel";           // en son seçilen bölüm index'i

    // Açılmış en yüksek bölüm (0 tabanlı). Varsayılan: sadece ilk bölüm açık.
    public static int HighestUnlockedLevel
    {
        get => PlayerPrefs.GetInt(KeyHighestUnlocked, 0);
        private set
        {
            PlayerPrefs.SetInt(KeyHighestUnlocked, value);
            PlayerPrefs.Save();
        }
    }

    // Oyuncunun en son girdiği bölüm. "Devam Et" bunu kullanır.
    public static int LastPlayedLevel
    {
        get => PlayerPrefs.GetInt(KeyLastPlayed, 0);
        set
        {
            PlayerPrefs.SetInt(KeyLastPlayed, value);
            PlayerPrefs.Save();
        }
    }

    // Verilen bölüm index'i oynanabilir mi?
    public static bool IsLevelUnlocked(int levelIndex) => levelIndex <= HighestUnlockedLevel;

    // Daha önce hiç oynanmış kayıt var mı? (Devam Et butonunu göstermek için)
    public static bool HasProgress() => HighestUnlockedLevel > 0 || PlayerPrefs.HasKey(KeyLastPlayed);

    // Bir bölüm geçildiğinde çağrılır → bir sonraki bölümün kilidini açar.
    public static void MarkLevelCompleted(int levelIndex)
    {
        if (levelIndex + 1 > HighestUnlockedLevel)
            HighestUnlockedLevel = levelIndex + 1;
    }

    // Tüm ilerlemeyi sıfırla (test / "ilerlemeyi sil" için).
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(KeyHighestUnlocked);
        PlayerPrefs.DeleteKey(KeyLastPlayed);
        PlayerPrefs.Save();
    }
}
