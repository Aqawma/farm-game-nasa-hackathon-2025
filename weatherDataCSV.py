from pathlib import Path
import requests
import csv
from datetime import datetime, timedelta

from utils import returnMidPoint, relativeHumidity

# give 4 coordinates and output path
def giveDataCSV(tuple1: (float, float), tuple2: (float, float), tuple3: (float, float), tuple4: (float, float),
                outputPath: str):
    lat, lon = returnMidPoint(tuple1, tuple2, tuple3, tuple4)[0], returnMidPoint(tuple1, tuple2, tuple3, tuple4)[1]
    end_date = datetime(2025, 9, 28)
    start_date = end_date - timedelta(days=3650/2)      # 7 days ago

    start = int(start_date.strftime("%Y%m%d"))
    end = int(end_date.strftime("%Y%m%d"))

    params = {
        "latitude": lat,
        "longitude": lon,
        "start": start,
        "end": end,
        "parameters": "T2M,T2MDEW,ALLSKY_SFC_SW_DWN,PRECTOTCORR",
        "community": "AG",
        "format": "JSON"
    }

    url = "https://power.larc.nasa.gov/api/temporal/daily/point"

    try:
        r = requests.get(url, params=params)
        r.raise_for_status()

        data = r.json()

        if "properties" in data and "parameter" in data["properties"]:
            parameters = data["properties"]["parameter"]

            all_dates = sorted(parameters["T2M"].keys())

            csv_filename = Path(outputPath) / "weatherData.csv"
            
            with open(csv_filename, 'w', newline='', encoding='utf-8') as csvfile:
                writer = csv.writer(csvfile)

                writer.writerow(['Date', 'Temperature_C', 'Humidity_Percent', 'Solar_kWh_per_m2', 'Precipitation_mm'])

                for date in all_dates:
                    temp = parameters["T2M"][date]
                    dew_point = parameters["T2MDEW"][date]

                    humidity = relativeHumidity(temp, dew_point)

                    solar = parameters["ALLSKY_SFC_SW_DWN"][date]
                    precip = parameters["PRECTOTCORR"][date]

                    formatted_date = f"{date[:4]}-{date[4:6]}-{date[6:8]}"

                    if date[4:6] == "05" or date[4:6] == "06" or date[4:6] == "07" or date[4:6] =="08":
                        writer.writerow([formatted_date, f"{temp:.1f}", f"{humidity:.1f}", f"{solar:.2f}", f"{precip:.2f}"])

        else:
            print("Unexpected response structure:")
            print(data)

    except requests.exceptions.RequestException as e:
        print(f"Request failed: {e}")
    except ValueError as e:
        print(f"Failed to parse JSON response: {e}")
    except Exception as e:
        print(f"Unexpected error: {e}")


tuple1, tuple2, tuple3, tuple4 = (41.5896,93.6164), (41.5896,93.6164), (41.5896,93.6164), (41.5896,93.6164)
giveDataCSV(tuple1, tuple2, tuple3, tuple4, "/Users/max/PycharmProjects/farm-game-nasa-hackathon-2025")
