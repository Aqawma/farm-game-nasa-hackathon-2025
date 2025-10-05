import fs from "fs";
import path from "path";
import fetch from "node-fetch";


function relativeHumidity(tempC, dewC) {
  const rh =
    100 *
    Math.exp(
      (17.625 * dewC) / (243.04 + dewC) -
        (17.625 * tempC) / (243.04 + tempC)
    );
  return Math.round(rh * 10) / 10;
}


async function giveDataCSV(coordinates, outputPath) {
  const [lat, lon] = coordinates;


  const endDate = new Date("2025-09-28");
  const startDate = new Date(endDate.getTime() - (3650 / 2) * 24 * 60 * 60 * 1000);

  const formatDate = (d) =>
    d.toISOString().slice(0, 10).replace(/-/g, "");

  const params = new URLSearchParams({
    latitude: lat,
    longitude: lon,
    start: formatDate(startDate),
    end: formatDate(endDate),
    parameters: "T2M,T2MDEW,ALLSKY_SFC_SW_DWN,PRECTOTCORR",
    community: "AG",
    format: "JSON",
  });

  const url = `https://power.larc.nasa.gov/api/temporal/daily/point?${params.toString()}`;

  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    const parameters = data?.properties?.parameter;
    if (!parameters) throw new Error("Unexpected API structure");

    const allDates = Object.keys(parameters.T2M).sort();

    const csvFile = path.join(outputPath, "weatherData.csv");
    const writer = fs.createWriteStream(csvFile, { encoding: "utf-8" });
    writer.write("Date,Temperature_C,Humidity_Percent,Solar_kWh_per_m2,Precipitation_mm\n");

    for (const date of allDates) {
      const month = date.slice(4, 6);
      if (!["05", "06", "07", "08"].includes(month)) continue;

      const temp = parameters.T2M[date];
      const dew = parameters.T2MDEW[date];
      const humidity = relativeHumidity(temp, dew);
      const solar = parameters.ALLSKY_SFC_SW_DWN[date];
      const precip = parameters.PRECTOTCORR[date];
      const formatted = `${date.slice(0, 4)}-${date.slice(4, 6)}-${date.slice(6, 8)}`;

      writer.write(
        `${formatted},${temp.toFixed(1)},${humidity.toFixed(1)},${solar.toFixed(2)},${precip.toFixed(2)}\n`
      );
    }

    writer.end();
    console.log(`Data written to ${csvFile}`);
  } catch (err) {
    console.error("Error:", err.message);
  }
}
