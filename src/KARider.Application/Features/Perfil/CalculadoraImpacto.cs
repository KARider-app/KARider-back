using KARider.Application.Common;

namespace KARider.Application.Features.Perfil;

/// <summary>
/// Fórmulas documentadas del ahorro en gasolina y del CO₂ evitado que se muestran en el perfil (HU-10).
/// Ver docs/formulas-impacto.md. Todos los valores de referencia viven en <see cref="EstadisticasOptions"/>.
/// </summary>
public static class CalculadoraImpacto
{
    private const double RadioTierraKm = 6371.0;

    /// <summary>
    /// Distancia en línea recta (Haversine) entre el punto de encuentro y el destino. Si el destino no tiene
    /// coordenadas, o alguno de los puntos no está georreferenciado, se usa la distancia por defecto.
    /// </summary>
    public static double DistanciaKm(
        double origenLatitud,
        double origenLongitud,
        double? destinoLatitud,
        double? destinoLongitud,
        double distanciaPorDefectoKm)
    {
        if (destinoLatitud is not { } latDestino || destinoLongitud is not { } lonDestino
            || EsSinUbicacion(origenLatitud, origenLongitud) || EsSinUbicacion(latDestino, lonDestino))
        {
            return distanciaPorDefectoKm;
        }

        var dLat = ARadianes(latDestino - origenLatitud);
        var dLon = ARadianes(lonDestino - origenLongitud);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ARadianes(origenLatitud)) * Math.Cos(ARadianes(latDestino))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var distancia = RadioTierraKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        // Un destino prácticamente encima del origen es un dato mal capturado, no un viaje de 0 km.
        return distancia < 0.5 ? distanciaPorDefectoKm : distancia;
    }

    /// <summary>Tarifa que habría pagado el pasajero en transporte privado: banderazo + costo por km.</summary>
    public static decimal TarifaReferencia(double distanciaKm, EstadisticasOptions opciones) =>
        opciones.TarifaReferenciaBanderazo + opciones.TarifaReferenciaPorKm * (decimal)distanciaKm;

    /// <summary>Ahorro del pasajero: tarifa de referencia menos el aporte que pagó (nunca negativo).</summary>
    public static decimal AhorroPasajero(double distanciaKm, decimal aportePagado, EstadisticasOptions opciones) =>
        Math.Max(0m, TarifaReferencia(distanciaKm, opciones) - aportePagado);

    /// <summary>
    /// CO₂ evitado: cada asiento compartido es una persona que no hizo el trayecto en su propio auto,
    /// así que se evita distancia × factor de emisión por asiento.
    /// </summary>
    public static decimal Co2EvitadoKg(int asientosCompartidos, double distanciaKm, EstadisticasOptions opciones) =>
        asientosCompartidos * (decimal)distanciaKm * opciones.KgCo2PorKm;

    private static bool EsSinUbicacion(double latitud, double longitud) =>
        Math.Abs(latitud) < 1e-9 && Math.Abs(longitud) < 1e-9;

    private static double ARadianes(double grados) => grados * Math.PI / 180.0;
}
