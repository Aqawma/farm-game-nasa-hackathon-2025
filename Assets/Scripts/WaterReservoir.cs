using System;
using UnityEngine;
using TMPro;

public class WaterReservoir : MonoBehaviour
{
    public TextMeshProUGUI waterLevelText;

    public int areaPlanted = 0;

    Transform waterLevelSprite;

    public float currentWaterLevel = 0.6f;
    float previousWaterLevel = 0.6f;

    public float totalCapacity = 10000;

    float _t = 0; // sec passed until last day
    float secPerDay = 0.25f; // should be same as UsableLandScript.secPerDay

    // every day <this> L of water is used for 1 m^2
    float waterConsumedPerDayPerM2 = 0.05f;

    EnvironmentScript environment;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        waterLevelSprite = transform.GetChild(0).transform;

        Vector3 newScale = waterLevelSprite.localScale;
        newScale.y = currentWaterLevel;
        waterLevelSprite.localScale = newScale;

        environment = GameObject.FindGameObjectWithTag("Env").GetComponent<EnvironmentScript>();
    }

    // Update is called once per frame
    void Update()
    {
        _t += Time.deltaTime;
        if (_t > secPerDay)
        {
            _t = 0;
            SimOneDay();
        }
    }

    void SimOneDay()
    {
        bool isDrought = environment.lifetime < environment.droughtUntil;
        float toBeConsumed = areaPlanted * waterConsumedPerDayPerM2 * (isDrought ? 3 : 1);

        if (currentWaterLevel * totalCapacity - toBeConsumed < 0)
        {
            toBeConsumed = currentWaterLevel * totalCapacity;
        }


        currentWaterLevel -= toBeConsumed / totalCapacity;

        // change the costume of needed
        currentWaterLevel = Math.Min(currentWaterLevel, 1f);
        if (previousWaterLevel != currentWaterLevel)
        {
            Vector3 newScale = waterLevelSprite.localScale;
            newScale.y = currentWaterLevel;
            waterLevelSprite.localScale = newScale;

            previousWaterLevel = currentWaterLevel;
        }

        waterLevelText.text = "Water Level: " + Mathf.Floor(currentWaterLevel * 10000) / 100f;
    }
}
