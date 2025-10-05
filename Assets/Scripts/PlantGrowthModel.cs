// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Globalization;
// // using System.Math;

// public class Program
// {
//     // Variables
//     public int current_day = 0;
//     public List<Dictionary<string, string>> weather_data = new List<Dictionary<string, string>>();
//     public double RUE = 0.0; // Radiation use efficiency (grams / MJ /Day)
//     public double IPAR = 0.0; // intercepted radiation
//     public double LAI = 0.0; // Leaf area (Plant leaf M^2/M^2)
//     public double BioMass = 0;
//     public double k = 0; // const
//     public double irrigation = 0.0; // fixed value for simplicity
//     public double nutrientusage = 0;
//     public double nowaterpenalty = 0.0;
//     public double nonutrientpenalty = 0.0;
//     //Nasa data
//     public double rainfall = 0.0; // in mm. Right now doing it as avg rainfall per day.
//     public double humidity = 0; // %
//     public double temperature = 0.0; // c
//     public double PAR = 0.0; // Photosynthetic active radiation (MJ/M^2/Day)
//     //plant specific parameters
//     public double[] ideal = new double[] { 0, 0, 0 };
//     public double RUE_optimum = 0.0;
//     public double T_opt = 0.0;
//     public double H_opt = 0.0;
//     public double min_nitrogen = 0.0;
//     public double min_phosphorus = 0.0;
//     public double min_potassium = 0.0;

//     // inputs
//     double[] nutrients = new double[] { 0, 0, 0 }; // [Nitrogen, Phosphorus, Potassium]
//     int nutrientretention = 0; // percent value
//     int monocropping = 0;

//     // outputs
//     int PlantGrowthStage = 0; // 0 → VE, 6 → harvest
//     double total_runoff = 0.0;
//     double BioMassPerDay = 0.0;
//     double cropquality = 1.0;

//     public void init(string biome)
//     {
//         //NASA data specific
//         PAR = 6;
//         rainfall = 15.0;
//         temperature = 27.0;
//         humidity = 60;
//         //Plant specific
//       (RUE_optimum, T_opt, H_opt, ideal,min_nitrogen,min_phosphorus,min_potassium) = biome switch
//       {
//           "arid" => (1.9, 35.5, 25.0, new double[] { 3, 1.7, 4.5 },30.0,17.0,45.0),//sorghum
//           "tropical" => (3.0, 30.0, 55.0, new double[] { 4, 2, 5 },36.0,17.0,45.0), // rice
//           "temperate" => (3.8, 27.5, 74.0, new double[] { 5, 1, 1 },40.0,8.0,8.0), // corn
//           "tundra" => (1.6, 12.0, 70.0, new double[] { 1, 2, 2 },24.0,48.0,48.0), //turnip
//          _ => (3.0, 25.0, 60.0, new double[] { 2, 1, 1 },20.0,10.5,10.5) // fallback
//         };
//         //Game specific
//         irrigation = 4.0;
//         k = 0.5;
//         nutrientusage = 0.25;
//         nutrients = new double[] { 200, 40, 30 };
//         cropquality = 1.0;
//         nutrientretention = 90;
//         nowaterpenalty = 0.02;
//         nonutrientpenalty = 0.03;
//         if (monocropping == 1)
//         {
//             nutrientretention -= 25;
//         }
//         else
//         {
//             nutrientretention = Math.Min(100, nutrientretention + 15);
//         }

//         PlantGrowthStage = 0; // 0 → VE, 6 → harvest
//         total_runoff = 0.0;
//         BioMassPerDay = 0.0;

//     }

//     public string getdata(string filename = "weather.csv")
//     {
//         if (weather_data.Count == 0)
//         {
//             using (var reader = new StreamReader(filename))
//             {
//                 var header = reader.ReadLine().Split(',');
//                 while (!reader.EndOfStream)
//                 {
//                     var line = reader.ReadLine().Split(',');
//                     var dict = new Dictionary<string, string>();
//                     for (int i = 0; i < header.Length; i++)
//                     {
//                         dict[header[i]] = line[i];
//                     }
//                     weather_data.Add(dict);
//                 }
//             }
//         }

//         if (current_day >= weather_data.Count)
//         {
//             return null;
//         }

//         var row = weather_data[current_day];
//         string csvDate = row["csvDate"];

//         PAR = double.Parse(row["Radiation_PAR (µmol/m²/s)"], CultureInfo.InvariantCulture) / 2.3e5;
//         rainfall = double.Parse(row["Rainfall (mm)"], CultureInfo.InvariantCulture);
//         temperature = double.Parse(row["Temperature (°C)"], CultureInfo.InvariantCulture);
//         humidity = double.Parse(row["Humidity (%)"], CultureInfo.InvariantCulture);

//         current_day += 1;
//         return csvDate;
//     }

//     public double nutrient_scores(double value, double low, double high)
//     {
//         if (value < low)
//         {
//             cropquality -= nonutrientpenalty / 3;
//             return Math.Max(0.0, value / low);
//         }
//         else if (value > high)
//         {
//             return Math.Max(0.0, high / value);
//         }
//         else
//         {
//             return 1.0;
//         }
//     }

//     public double BioMassGain(double PAR, double RUE, double LAI, double k)
//     {
//         double IPAR = PAR * (1 - Math.Exp(-k * LAI));
//         double BioMassPerDay = RUE * IPAR;
//         return BioMassPerDay;
//     }

//     public double FINDLAI(double biomass)
//     {
//         double LAI_start = 0.5;
//         double LAI_max = 7.0;
//         double scaling_factor = 0.005;

//         double LAI_current = LAI_start + (LAI_max - LAI_start) *
//             (Math.Log(1 + scaling_factor * biomass) / Math.Log(1 + scaling_factor * 1500));

//         if (LAI_current > LAI_max)
//         {
//             LAI_current = LAI_max;
//         }

//         return LAI_current;
//     }

//     public double FINDRUE(double rainfall, double irrigation, double[] nutrients, double temperature, double humidity)
//     {

//         double Nitrogenfactor = nutrient_scores(nutrients[0], min_nitrogen, 1000);
//         double Phosphorusfactor = nutrient_scores(nutrients[1], min_phosphorus, 1000);
//         double Postassiumfactor = nutrient_scores(nutrients[2], min_potassium, 1000);
//         double Rainfactor = nutrient_scores(rainfall, 2, 20);

//         double nutrient_factor = Math.Pow(Nitrogenfactor * Phosphorusfactor * Postassiumfactor * Rainfactor, 0.25);

//         double total_water = rainfall + irrigation;
//         double water_requirement = 6.0;
//         double water = Math.Min(total_water / water_requirement, 1.0);
//         double water_factor;

//         if (water >= 0.5)
//         {
//             water_factor = 1.0;
//         }
//         else
//         {
//             water_factor = Math.Exp(-5 * (0.5 - water));
//         }
//         double[] actual = nutrients;
//         if (actual.Sum() == 0)
//         {
//             nutrient_factor = 0.0;
//         }
//         else
//         {
//             double sumIdeal = ideal.Sum();
//             double sumActual = actual.Sum();
//             double[] ideal_norm = ideal.Select(x => x / sumIdeal).ToArray();
//             double[] actual_norm = actual.Select(x => x / sumActual).ToArray();
//             double diff = 0;
//             for (int i = 0; i < ideal_norm.Length; i++)
//             {
//                 diff += Math.Abs(ideal_norm[i] - actual_norm[i]);
//             }
//             nutrient_factor = Math.Max(0.4, 1.0 - diff);
//         }

//         double T_range = 15;
//         double temperature_factor = Math.Exp(-Math.Pow((temperature - T_opt) / T_range, 2));

//         double H_range = 20;
//         double humidity_factor = Math.Exp(-Math.Pow((humidity - H_opt) / H_range, 2));

//         double adjusted_RUE =
//             RUE_optimum *
//             water_factor *
//             nutrient_factor *
//             temperature_factor *
//             humidity_factor;

//         return adjusted_RUE;
//     }

//     public int PlantGrowth(int haswater, int isDrought, int isRunoff)
//     {
//         LAI = FINDLAI(BioMass);
//         RUE = FINDRUE(rainfall, irrigation, nutrients, temperature, humidity);
//         BioMassPerDay = BioMassGain(PAR, RUE, LAI, k);
//         BioMass += BioMassPerDay;

//         if (haswater == 0)
//         {
//             cropquality -= nowaterpenalty;
//         }
//         if (isDrought == 1)
//         {
//             cropquality -= 4 * nowaterpenalty;
//         }
//         if (isRunoff == 1)
//         {
//             double loss_factor = (100 - nutrientretention) / 100.0;
//             double[] losses = nutrients.Select(n => n * loss_factor).ToArray();
//             for (int i = 0; i < nutrients.Length; i++)
//             {
//                 nutrients[i] -= losses[i];
//             }
//             total_runoff += losses.Sum();
//         }
//         else
//         {
//             double usecompound = 1 + (100 - nutrientretention) / 1000.0;
//             for (int i = 0; i < nutrients.Length; i++)
//             {
//                 nutrients[i] = nutrients[i] - nutrientusage * usecompound;
//             }

//             if (BioMass < 50)
//             {
//                 PlantGrowthStage = 0;
//             }
//             else if (BioMass < 150)
//             {
//                 PlantGrowthStage = 1;
//             }
//             else if (BioMass < 400)
//             {
//                 PlantGrowthStage = 2;
//             }
//             else if (BioMass < 800)
//             {
//                 PlantGrowthStage = 3;
//             }
//             else if (BioMass < 1200)
//             {
//                 PlantGrowthStage = 4;
//             }
//             else if (BioMass < 2000)
//             {
//                 PlantGrowthStage = 5;
//             }
//             else
//             {
//                 PlantGrowthStage = 6;
//             }
//         }
//         return PlantGrowthStage;
//     }
// }