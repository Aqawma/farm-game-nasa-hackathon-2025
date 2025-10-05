using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class UsableLandScript : MonoBehaviour
{
    public TextMeshProUGUI landInfo;

    // Units
    // int kgSoilPerM2 = 260;
    // int NitrogenMgPerM2 = 13000;

    // int ppmOfNutrientsPerFert = 10;

    // int KG_SOIL_PER_M2 = 260;
    // int BLOCK_AREA = 72;
    // int KG_SOIL_PER_BLOCK = 18720;

    // recommended values
    // 280800 min - 936000mg - max 3.7e6 nitrogen per block
    // 187200 min - 936000mg - max 5.6e6 phosphorus per block
    // 2.8e6 min - 5616000mg - 18.7e6 mg potassium per block

    // grams consumed per day on a 72 m² corn tile (average across season)
    // int needN_g_perDay = 11;
    // int needP_g_perDay = 2;
    // int needK_g_perDay = 8;

    ////////////////////////////////////////////////////////////////////////////////////////////////

    // when player approaches this object, they may plant something. 
    // this object needs to handle initialization process, and the growth is gonna be handled by plantScript

    GameObject player;
    PlayerScript playerScript;

    public GameObject alfa, wheat, corn;
    string previousPlantName = "";

    public GameObject currentPlant = null;
    public PlantScript currentPlantScript = null;

    WaterReservoir waterReservoirScript;

    public int soilArea = 72;

    // Nutrition variables in mg per 1 kg of soil
    // begins with poor values
    // worst value - perfect value - max value
    public float ppm_nitrogen = 15; // 15 - 50 - 200
    public float ppm_phosphorus = 5; // 10 - 50 - 300 
    public float ppm_potassium = 5; // 150 - 300 - 1000

    public float nutrientRetention = 97; // 0.97 - after 10 days nutrient level goes from 100 to 73

    // 20 secs for plant to reach its max growth
    // 120 days

    public bool interactable = false;

    int fertPrize = 10;
    int coverCropPrice = 15;

    float _t = 0; // sec passed since creation
    int daysPassed = 0;
    float secPerDay = 0.25f; // should be same as WaterReservoir.secPerDay

    EnvironmentScript environment;

    void Start()
    {
        landInfo = GameObject.FindGameObjectWithTag("uiinfo").GetComponent<TextMeshProUGUI>();

        player = GameObject.FindWithTag("Player");
        playerScript = player.GetComponent<PlayerScript>();
        waterReservoirScript = GameObject.FindGameObjectWithTag("Reservoir").GetComponent<WaterReservoir>();

        environment = GameObject.FindGameObjectWithTag("Env").GetComponent<EnvironmentScript>();

        currentPlant = null;
    }

    // Update is called once per frame
    void Update()
    {
        _t += Time.deltaTime;

        if (_t > daysPassed * secPerDay)
        {
            daysPassed += 1;
            SimOneDay();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            Plant(alfa);
            UpdateSoilInfo();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            Plant(corn);
            UpdateSoilInfo();
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            Plant(wheat);
            UpdateSoilInfo();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            HarvestAndSell();
            UpdateSoilInfo();
        }

        if (currentPlant == null && playerScript.money >= fertPrize && interactable)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) // type 1 1 5
            {
                playerScript.money -= fertPrize;
                ppm_nitrogen += 43; ppm_phosphorus += 43; ppm_potassium += 214;
                environment.emissionKg += 6;
                UpdateSoilInfo();
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) // type 1 3 6
            {
                playerScript.money -= fertPrize;
                ppm_nitrogen += 30; ppm_phosphorus += 90; ppm_potassium += 180;
                environment.emissionKg += 3;
                UpdateSoilInfo();
            }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) // type 2 1 1
            {
                playerScript.money -= fertPrize;
                ppm_nitrogen += 150; ppm_phosphorus += 75; ppm_potassium += 75;
                environment.emissionKg += 10;
                UpdateSoilInfo();
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) // buy soilRetention
        {
            if (playerScript.money >= coverCropPrice)
            {
                playerScript.money -= coverCropPrice;
                nutrientRetention += 1f;
            }
        }
    }

    void SimOneDay()
    {
        if (currentPlant != null)
        {
            currentPlantScript.SimOneDay(
                haswater: (waterReservoirScript.currentWaterLevel > 0f) ? 1 : 0,
                isDrought: (environment.lifetime < environment.droughtUntil) ? 1 : 0,
                isRunoff: (environment.lifetime < environment.runoffUntill) ? 1 : 0,
                biome: PlayerPrefs.GetString("biome"))
                ;

            // to-do after Bach's code is integrated into PlantScript, make it use the following data from my code:
            //  1. soil retention and nutrient ppms from this script
            //  2. amount runned off from EnvironmentScript and add accordingly to it.
        }
    }

    //  planting features
    void Plant(GameObject plant)
    {
        if (!interactable)
        {
            return;
        }
        if (currentPlant != null)
        {
            Debug.Log("Land occupied.");
            return;
        }

        waterReservoirScript.areaPlanted += (int)(soilArea);

        playerScript.money -= 75; // maybe different cost for each plant

        currentPlant = Instantiate(plant, transform);
        currentPlant.transform.SetParent(transform);

        currentPlantScript = currentPlant.GetComponent<PlantScript>();
        currentPlantScript.land = this;
        currentPlantScript.environment = environment;

        currentPlantScript.Init(
            isMonocrop: (previousPlantName == plant.tag) ? 1 : 0,
            biome: "temperate"
                // to-do get the lat and lon so player doesnt have to copy paste it on their own. maybe wake the unity game, and then Arunabh's code
                // send some kinda API req? idk, ask

                // EnvironmentScript.ClimateClassifier.Classify(
                //     PlayerPrefs.GetFloat("TheLat"),
                //     PlayerPrefs.GetFloat("TheLon"))
                );
        Debug.Log("Planted!");
    }

    public void PlantFinishedGrowing()
    {
        waterReservoirScript.areaPlanted -= (int)(soilArea);
    }

    void HarvestAndSell()
    {
        if (!interactable)
        {
            return;
        }
        if (currentPlant == null)
        {
            Debug.Log("Nothing to harvest.");
            return;
        }

        int price = (int)currentPlantScript.cropquality * 100;

        playerScript.money += price;
        Debug.Log("Sold for " + price);

        previousPlantName = currentPlant.tag;

        Destroy(currentPlant);
        currentPlant = null;
        currentPlantScript = null;
    }

    // hitbox features

    private void OnTriggerEnter2D(Collider2D other)
    {
        UpdateSoilInfo();
        if (other.name == "Player")
        {
            interactable = true;
        }
    }

    void UpdateSoilInfo()
    {
        landInfo.text = "Nitrogen PPM: " + cut2(ppm_nitrogen) + "\nPhosphorus PPM: " + cut2(ppm_phosphorus) + "\nPotassium PPM: " + cut2(ppm_potassium) + "\n\nSoil Retention% " + cut2(nutrientRetention);
        if (currentPlant != null)
        {
            landInfo.text = landInfo.text + "\n\nPlant Quality: " + cut2((float)currentPlantScript.cropquality*100);
        }
    }

    int cut2(float x)
    {
        return (int)Mathf.Floor(x * 10f) / 10;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        landInfo.text = "Go near a land to get its information";
        if (other.name == "Player")
        {
            interactable = false;
        }
    }
}
