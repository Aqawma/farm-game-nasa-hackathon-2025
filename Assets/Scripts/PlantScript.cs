using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class PlantScript : MonoBehaviour
{
    /// /// /// BELOW ARE Bach's VARIABLES 💻💻💻
    /// 
    

    int current_day;
    List<Dictionary<string, string>> weather_data = new List<Dictionary<string, string>>();
    double RUE; // Radiation use efficiency (grams / MJ /Day)
    double PAR; // Photosynthetic active radiation (MJ/M^2/Day)
    double IPAR; // intercepted radiation
    double LAI; // Leaf area (Plant leaf M^2/M^2)
    double BioMass;
    double k; // const
    double rainfall; // in mm. Right now doing it as avg rainfall per day.
    double irrigation; // fixed value for simplicity
    double temperature; // c
    double humidity; // %
    double nutrientusage;
    double nowaterpenalty;
    double nonutrientpenalty;
    double BioMassPerDay;
    int PlantGrowthStage; // 0 → VE, 6 → harvest

    //plant specific variables:
    public double[] ideal = new double[] { 0, 0, 0 };
    public double RUE_optimum = 0.0;
    public double T_opt = 0.0;
    public double H_opt = 0.0;
    public double min_nitrogen = 0.0;
    public double min_phosphorus = 0.0;
    public double min_potassium = 0.0;
    double water_requirement = 6.0;

    // INPUT FROM HAMROZ's SIDE
    double[] nutrients = new double[] { 0, 0, 0 }; // [Nitrogen, Phosphorus, Potassium]

    // OUTPUTS TO HAMROZ's SIDE
    public double cropquality;

    /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// /// ///

    public UsableLandScript land;

    public EnvironmentScript environment;

    [SerializeField][Header("Runoff Growth-Stage Sprites")] private Sprite[] runoffGrowthStageSprites = new Sprite[5];
    [SerializeField][Header("Growth-Stage Sprites")] private Sprite[] growthStageSprites = new Sprite[5];
    [SerializeField][Header("Tiles (those 3x2)")] private SpriteRenderer[] tiles = new SpriteRenderer[6];

    int sprite_G_Stage;

    void Start() // for interdependent setups
    {

    }

    public void Init(int isMonocrop, string biome)
    {
        simulation_init(isMonocrop: isMonocrop, biome: biome);

        sprite_G_Stage = PlantGrowthStage + 1;
        SetSprite(growthStageSprites[1]);
    }

    void Update()
    {
        bool isRaining = environment.lifetime <= environment.runoffUntill;

        // if (sprite_G_Stage != PlantGrowthStage + 1)
        {
            sprite_G_Stage = PlantGrowthStage + 1;
            if (PlantGrowthStage == 6)
            {
                SetSprite((!isRaining) ? growthStageSprites[4] : runoffGrowthStageSprites[4]);
            }
            if (PlantGrowthStage == 4 || PlantGrowthStage == 5)
            {
                SetSprite((!isRaining) ? growthStageSprites[3] : runoffGrowthStageSprites[3]);
            }
            if (PlantGrowthStage == 1 || PlantGrowthStage == 2 || PlantGrowthStage == 3)
            {
                SetSprite((!isRaining) ? growthStageSprites[2] : runoffGrowthStageSprites[2]);
            }
            if (PlantGrowthStage == 0)
            {
                Debug.Log("in herer   " + (!isRaining));
                SetSprite((!isRaining) ? growthStageSprites[1] : runoffGrowthStageSprites[1]);
            }
        }
    }

    void SetSprite(Sprite to)
    {
        for (int i = 0; i < 6; i++)
        {
            tiles[i].sprite = to;
        }
    }

    // Bach's functions

    public void SimOneDay(int haswater, int isDrought, int isRunoff, string biome)
    {
        LAI = FINDLAI(BioMass);
        RUE = FINDRUE(rainfall, irrigation, temperature, humidity);
        BioMassPerDay = BioMassGain(PAR, RUE, LAI, k);


        // Environment specific water penalty
        double waterPenaltyFactor = 1.0;
        switch (biome)
        {
            case "arid":
                waterPenaltyFactor = 1.6;
                break;

            case "tropical":
                waterPenaltyFactor = 0.6;
                break;

            case "temperate":
                waterPenaltyFactor = 1.0;
                break;

            case "tundra":
                waterPenaltyFactor = 0.8;
                break;
        }

        if (haswater == 0)
        {
            cropquality -= nowaterpenalty * waterPenaltyFactor;
        }
        if (isDrought == 1)
        {
            cropquality -= 4 * nowaterpenalty * waterPenaltyFactor;
        }
        cropquality = Math.Max(0, cropquality);

        if (isRunoff == 1) {
            double loss_factor = (100 - land.nutrientRetention) / 100.0;

            double[] losses = new double[nutrients.Length];

            double totalLoss = 0;

            for (int i = 0; i < nutrients.Length; i++) {
                losses[i] = nutrients[i] * loss_factor;

                totalLoss += losses[i];

                nutrients[i] -= losses[i];
            }

            environment.nutrientPpmRunoffed += (float)totalLoss;
        }

        if (haswater == 1 && isDrought == 0 && isRunoff == 0)
        {
            BioMass += BioMassPerDay;
            double usecompound = 1 + (100 - land.nutrientRetention) / 1000.0;
            for (int i = 0; i < nutrients.Length; i++)
            {
                nutrients[i] = nutrients[i] - nutrientusage * usecompound;
            }

            if (BioMass < 50)
            {
                PlantGrowthStage = 0;
            }
            else if (BioMass < 100)
            {
                PlantGrowthStage = 1;
            }
            else if (BioMass < 300)
            {
                PlantGrowthStage = 2;
            }
            else if (BioMass < 400)
            {
                PlantGrowthStage = 3;
            }
            else if (BioMass < 500)
            {
                PlantGrowthStage = 4;
            }
            else if (BioMass < 600)
            {
                PlantGrowthStage = 5;
            }
            else
            {
                PlantGrowthStage = 6;
                land.PlantFinishedGrowing();
            }
        }

        land.ppm_nitrogen = (float)nutrients[0];
        land.ppm_potassium = (float)nutrients[1];
        land.ppm_phosphorus = (float)nutrients[2];
    }

    public void simulation_init(int isMonocrop, string biome)
    {
        PAR = 6;
        k = 0.5;
        rainfall = 15.0;
        irrigation = 4.0;
        temperature = 27.0;
        humidity = 60;
        nutrientusage = 0.25;
        nutrients = new double[] { land.ppm_nitrogen, land.ppm_phosphorus, land.ppm_potassium };
        cropquality = 1.0;
        land.nutrientRetention = 90;
        nowaterpenalty = 0.02;
        nonutrientpenalty = 0.03;

        (RUE_optimum, T_opt, H_opt, ideal, min_nitrogen, min_phosphorus, min_potassium, water_requirement) = biome switch
        {
            "arid" => (1.9, 35.5, 25.0, new double[] { 3, 1.7, 4.5 }, 30.0, 17.0, 45.0, 3.0),//sorghum
            "tropical" => (3.0, 30.0, 55.0, new double[] { 4, 2, 5 }, 36.0, 17.0, 45.0, 10.0), // rice
            "temperate" => (3.8, 27.5, 74.0, new double[] { 5, 1, 1 }, 40.0, 8.0, 8.0, 6.0), // corn
            "tundra" => (1.6, 12.0, 70.0, new double[] { 1, 2, 2 }, 24.0, 48.0, 48.0, 4.0), //turnip
            _ => (3.0, 25.0, 60.0, new double[] { 2, 1, 1 }, 20.0, 10.5, 10.5, 5.0) // fallback
        };

        if (isMonocrop == 1)
        {
            land.nutrientRetention -= Math.Max(0, 25);
        }
        else
        {
            // land.nutrientRetent/ion = Math.Min(100, land.nutrientRetention + 15);
        }
    }

    public string getdata(string filename = "weather.csv")
    {
        return "Okay";
        // if (weather_data.Count == 0)
        // {
        //     using (var reader = new StreamReader(filename))
        //     {
        //         var header = reader.ReadLine().Split(',');
        //         while (!reader.EndOfStream)
        //         {
        //             var line = reader.ReadLine().Split(',');
        //             var dict = new Dictionary<string, string>();
        //             for (int i = 0; i < header.Length; i++)
        //             {
        //                 dict[header[i]] = line[i];
        //             }
        //             weather_data.Add(dict);
        //         }
        //     }
        // }

        // if (current_day >= weather_data.Count)
        // {
        //     return null;
        // }

        // var row = weather_data[current_day];
        // string csvDate = row["csvDate"];

        // PAR = double.Parse(row["Radiation_PAR (µmol/m²/s)"], CultureInfo.InvariantCulture) / 2.3e5;
        // rainfall = double.Parse(row["Rainfall (mm)"], CultureInfo.InvariantCulture);
        // temperature = double.Parse(row["Temperature (°C)"], CultureInfo.InvariantCulture);
        // humidity = double.Parse(row["Humidity (%)"], CultureInfo.InvariantCulture);

        // current_day += 1;
        // return csvDate;
    }

    public double nutrient_scores(double value, double low, double high)
    {
        if (value < low)
        {
            cropquality -= nonutrientpenalty / 3;
            return Math.Max(0.0, value / low);
        }
        else if (value > high)
        {
            return Math.Max(0.0, high / value);
        }
        else
        {
            return 1.0;
        }
    }

    public double BioMassGain(double PAR, double RUE, double LAI, double k)
    {
        double IPAR = PAR * (1 - Math.Exp(-k * LAI));
        double BioMassPerDay = RUE * IPAR;
        return BioMassPerDay;
    }

    public double FINDLAI(double biomass)
    {
        double LAI_start = 0.5;
        double LAI_max = 7.0;
        double scaling_factor = 0.005;

        double LAI_current = LAI_start + (LAI_max - LAI_start) *
            (Math.Log(1 + scaling_factor * biomass) / Math.Log(1 + scaling_factor * 1500));

        if (LAI_current > LAI_max)
        {
            LAI_current = LAI_max;
        }

        return LAI_current;
    }

    public double FINDRUE(double rainfall, double irrigation, double temperature, double humidity)
    {

        double Nitrogenfactor = nutrient_scores(nutrients[0], min_nitrogen, 1000);
        double Phosphorusfactor = nutrient_scores(nutrients[1], min_phosphorus, 1000);
        double Postassiumfactor = nutrient_scores(nutrients[2], min_potassium, 1000);
        double Rainfactor = nutrient_scores(rainfall, 2, 20);

        double nutrient_factor = Math.Pow(Nitrogenfactor * Phosphorusfactor * Postassiumfactor * Rainfactor, 0.25);

        double total_water = rainfall + irrigation;
        double water = Math.Min(total_water / water_requirement, 1.0);
        double water_factor;

        if (water >= 0.5)
        {
            water_factor = 1.0;
        }
        else
        {
            water_factor = Math.Exp(-5 * (0.5 - water));
        }
        double[] actual = nutrients;
        if (actual.Sum() == 0)
        {
            nutrient_factor = 0.0;
        }
        else
        {
            double sumIdeal = ideal.Sum();
            double sumActual = actual.Sum();
            double[] ideal_norm = ideal.Select(x => x / sumIdeal).ToArray();
            double[] actual_norm = actual.Select(x => x / sumActual).ToArray();
            double diff = 0;
            for (int i = 0; i < ideal_norm.Length; i++)
            {
                diff += Math.Abs(ideal_norm[i] - actual_norm[i]);
            }
            nutrient_factor = Math.Max(0.4, 1.0 - diff);
        }

        double T_range = 15;
        double temperature_factor = Math.Exp(-Math.Pow((temperature - T_opt) / T_range, 2));

        double H_range = 20;
        double humidity_factor = Math.Exp(-Math.Pow((humidity - H_opt) / H_range, 2));

        double adjusted_RUE =
            RUE_optimum *
            water_factor *
            nutrient_factor *
            temperature_factor *
            humidity_factor;

        return adjusted_RUE;
    }

}