import math

def returnMidPoint(tuple1: (float, float), tuple2: (float, float), tuple3: (float, float), tuple4: (float, float)) -> (float, float):
    return (tuple1[0] + tuple2[0] + tuple3[0] + tuple4[0]) / 4, (tuple1[1] + tuple2[1] + tuple3[1] + tuple4[1]) / 4

def relativeHumidity(tempC, dewC):
    rh = 100 * math.exp((17.625 * dewC) / (243.04 + dewC) - (17.625 * tempC) / (243.04 + tempC))
    return round(rh, 1)