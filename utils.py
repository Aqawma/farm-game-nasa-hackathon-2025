import math

def relativeHumidity(tempC, dewC):
    rh = 100 * math.exp((17.625 * dewC) / (243.04 + dewC) - (17.625 * tempC) / (243.04 + tempC))
    return round(rh, 1)