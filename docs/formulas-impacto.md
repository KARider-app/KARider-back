# Fórmulas del perfil: ahorro en gasolina y CO₂ evitado (HU-10)

Estas son las fórmulas de los tres indicadores de la pantalla P13 "Perfil - Historial de viajes". La regla de negocio 2 de la HU-10 pide que sean documentadas y consistentes y que no haya números inventados en pantalla. El backend las calcula en
[`CalculadoraImpacto`](../src/KARider.Application/Features/Perfil/CalculadoraImpacto.cs), y la PWA solo muestra el resultado de `GET /api/v1/perfil`.

## Qué viajes cuentan

Los tres indicadores cuentan solo los viajes **completados**:

- Como conductor: viajes propios en estado `Completado`.
- Como pasajero: reservas en estado `Completada`.

Los viajes en curso y los cancelados aparecen en el historial, pero no suman a los indicadores.

## Viajes hechos

```
viajes_hechos = viajes completados como conductor + reservas completadas como pasajero
```

## Distancia del trayecto

```
d = Haversine(punto de encuentro, destino)   [km, línea recta]
```

Si el destino no tiene coordenadas, se usa `DistanciaPorDefectoKm`, que es 12 km, un trayecto típico desde la UTTT a Tula y sus alrededores. Lo mismo pasa si algún punto está en (0, 0) o si los dos puntos quedan a menos de 0.5 km.

## Ahorro en gasolina (MXN)

**Como pasajero.** Se compara el aporte pagado contra lo que habría costado el mismo trayecto en transporte privado (taxi o app):

```
tarifa_referencia = TarifaReferenciaBanderazo + TarifaReferenciaPorKm × d
ahorro_pasajero   = max(0, tarifa_referencia − aporte_pagado)
```

**Como conductor.** Los aportes que recibió de sus pasajeros son gasolina que ya no pagó solo:

```
ahorro_conductor = Σ aportes de las reservas completadas del viaje
```

El indicador final es la suma de ambos, redondeada a 2 decimales.

## CO₂ evitado (kg)

Cada asiento compartido corresponde a una persona que no hizo el mismo trayecto en su propio auto:

```
co2_evitado = Σ asientos_compartidos × d × KgCo2PorKm
```

- Como conductor: `asientos_compartidos` son los asientos de las reservas completadas de su viaje.
- Como pasajero: son los asientos de su propia reserva.

El resultado se redondea a 1 decimal.

## Valores de referencia (configurables)

Los valores están en la sección `Estadisticas` de `appsettings.json`. En Azure se pueden cambiar sin recompilar, por ejemplo con `Estadisticas__KgCo2PorKm`.

| Parámetro | Valor | Significado |
|---|---|---|
| `KgCo2PorKm` | 0.192 | Factor de emisión de un auto compacto a gasolina (kg CO₂/km) |
| `DistanciaPorDefectoKm` | 12 | Distancia que se asume sin coordenadas de destino |
| `TarifaReferenciaBanderazo` | 35 | Banderazo de taxi o app (MXN) |
| `TarifaReferenciaPorKm` | 9 | Costo por km de taxi o app (MXN) |

## Ejemplo

Un pasajero hace un viaje completado de 10 km y paga $40 de aporte por 1 asiento:

- Tarifa de referencia: 35 + 9 × 10 = **$125**
- Ahorro: 125 − 40 = **$85**
- CO₂ evitado: 1 × 10 × 0.192 = **1.9 kg**
