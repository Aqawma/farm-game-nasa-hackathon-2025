using UnityEngine;
using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine.SceneManagement;

public class EnvironmentScript : MonoBehaviour
{
    float sustainability = 100f;
    public int emissionKg = 0;
    public float nutrientPpmRunoffed = 0;

    int fieldAmount = 10;

    int runoffOccurency = 10; // in seconds
    int rainDuration = 5; // in seconds
    float nextRain = 10;
    
    bool isRaining = false;
    public float runoffUntill = -1;

    float nextDrought = 30;
    int droughtDuration = 10;
    int droughtOccurency = 100;
    bool isDrought = false;
    public float droughtUntil = -1;

    GameObject[] grounds;
    GameObject cloud = null;

    public float lifetime = 0;

    [SerializeField] GameObject rainParticle;

    public TextMeshProUGUI sustText;

    public GameObject GameOverPanel;
    public GameObject player;
    public PlayerScript playerSS;

    public GameObject waterRsrv;
    public WaterReservoir waterRsrvSS;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sustText.text = (Mathf.Floor(sustainability * 100f) / 100f).ToString();

        PlayerPrefs.SetString("biome", ClimateClassifier.Classify(
            PlayerPrefs.GetFloat("temp"),
            PlayerPrefs.GetFloat("humidity"),
            PlayerPrefs.GetFloat("sun"),
            PlayerPrefs.GetFloat("rain")
        ));

        grounds = GameObject.FindGameObjectsWithTag("Ground");

        cloud = transform.GetChild(0).gameObject;
        cloud.SetActive(false);

        playerSS = player.GetComponent<PlayerScript>();
        waterRsrvSS = waterRsrv.GetComponent<WaterReservoir>();
    }

    // Update is called once per frame
    void Update()
    {
        lifetime += Time.deltaTime;

        if (!isDrought && lifetime >= runoffUntill)
        {
            // StopRaining
            isRaining = false;
            cloud.SetActive(false);
            rainParticle.SetActive(false);
        }
        if (!isDrought && lifetime >= nextRain)
        {
            runoffUntill = lifetime + rainDuration;
            nextRain = runoffUntill + runoffOccurency;

            cloud.SetActive(true);
            rainParticle.SetActive(true);

            isRaining = true;
        }

        // if (!isRaining && lifetime > droughtUntil)
        // {


        //     isDrought = false;
        // }
        // if (!isRaining && lifetime > nextDrought)
        // {
        //     droughtUntil = lifetime + droughtDuration;
        //     nextDrought = droughtUntil + droughtOccurency;
        //     isDrought = true;
        // }

        // sustainability is always recalculated based on the values its given

        sustainability = 100f - Math.Max(0f, emissionKg * 0.1f) - nutrientPpmRunoffed / 400f;
        sustText.text = "Sustainability " + sustainability.ToString();

        // nutrientPpmRunoffed;
        // emissionKg;

        if (sustainability < 70 || playerSS.money < 0 || waterRsrvSS.currentWaterLevel < 0.001f)
        {
            GameOver();
            return;
        }

    }
    
    public void GameOver()
    {
        Time.timeScale = 0f;  // Freeze the game
        GameOverPanel.SetActive(true);  // Show Game Over UI
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;  // Unfreeze time before reloading
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);  // Reload current scene
    }


    public static class ClimateClassifier
    {
        public static string Classify(float tempC, float humidityPct, float ghiKWhPerM2, float rainMmPerYear)
        {
            // --- Hard overrides for very clear cases ---
            if (rainMmPerYear <= 300f || (rainMmPerYear <= 400f && ghiKWhPerM2 >= 1900f && humidityPct <= 40f && tempC >= 18f))
                return "arid";

            if (tempC <= 0f || (tempC <= 5f && ghiKWhPerM2 <= 1200f))
                return "tundra";

            if (tempC >= 18f && humidityPct >= 70f && rainMmPerYear >= 1500f)
                return "tropical";

            // --- Soft scoring for edge cases ---
            float desertScore = ScoreDesert(tempC, humidityPct, ghiKWhPerM2, rainMmPerYear);
            float northScore = ScoreNorth(tempC, ghiKWhPerM2);
            float tropicalScore = ScoreTropical(tempC, humidityPct, ghiKWhPerM2, rainMmPerYear);
            float normalScore = 0.5f; // baseline

            if (desertScore >= northScore && desertScore >= tropicalScore && desertScore >= normalScore) return "arid";
            if (northScore >= desertScore && northScore >= tropicalScore && northScore >= normalScore) return "tundra";
            if (tropicalScore >= desertScore && tropicalScore >= northScore && tropicalScore >= normalScore) return "tropical";
            return "temperate";
        }

        // ---------- scoring helpers ----------
        static float ScoreDesert(float t, float hum, float ghi, float rain)
        {
            float rainTerm = 1f - Clamp01(rain / 400f);
            float sunTerm = Clamp01((ghi - 1600f) / 800f);
            float tempTerm = Clamp01((t - 18f) / 17f);
            float dryTerm = 1f - Clamp01(hum / 60f);
            return 0.45f * rainTerm + 0.25f * sunTerm + 0.20f * tempTerm + 0.10f * dryTerm;
        }

        static float ScoreNorth(float t, float ghi)
        {
            float coldTerm = 1f - Clamp01((t - 0f) / 12f);
            float lowSun = 1f - Clamp01((ghi - 1100f) / 700f);
            return 0.65f * coldTerm + 0.35f * lowSun;
        }

        static float ScoreTropical(float t, float hum, float ghi, float rain)
        {
            float warmTerm = Clamp01((t - 18f) / 10f);
            float humTerm = Clamp01((hum - 65f) / 20f);
            float rainTerm = Clamp01((rain - 1000f) / 1000f);
            float sunTerm = 1f - Clamp01(Mathf.Abs(Mathf.Clamp(ghi, 1500f, 2000f) - ghi) / 600f);
            return 0.35f * warmTerm + 0.30f * humTerm + 0.25f * rainTerm + 0.10f * sunTerm;
        }

        static float Clamp01(float x) => (x < 0f) ? 0f : (x > 1f ? 1f : x);
    }
}

