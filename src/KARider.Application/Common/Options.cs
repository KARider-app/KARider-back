using System.ComponentModel.DataAnnotations;

namespace KARider.Application.Common;

public sealed class InstitucionOptions
{
    public const string SectionName = "Institucion";

    [Required]
    public string Nombre { get; set; } = "UTTT";

    /// <summary>Dominio permitido para el registro (sin @).</summary>
    [Required]
    public string DominioCorreo { get; set; } = "uttt.edu.mx";

    /// <summary>Zona horaria IANA para filtros por fecha.</summary>
    [Required]
    public string ZonaHoraria { get; set; } = "America/Mexico_City";
}

public sealed class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    [Range(3, 20)]
    public int MaxIntentosFallidos { get; set; } = 5;

    [Range(1, 1440)]
    public int MinutosBloqueo { get; set; } = 15;

    [Range(5, 1440)]
    public int MinutosExpiracionCodigo { get; set; } = 15;

    [Range(3, 10)]
    public int MaxIntentosCodigo { get; set; } = 5;

    [Range(10, 3600)]
    public int SegundosEntreCodigos { get; set; } = 60;

    [Range(1, 60)]
    public int DiasRefreshToken { get; set; } = 7;

    [Range(1, 90)]
    public int DiasRefreshTokenRecordarme { get; set; } = 30;
}

public sealed class EstadisticasOptions
{
    public const string SectionName = "Estadisticas";

    /// <summary>
    /// Factor de emisión de un auto compacto a gasolina (kg de CO₂ por km). Cada asiento compartido
    /// evita que esa persona haga el mismo trayecto en su propio vehículo.
    /// </summary>
    [Range(0, 2)]
    public decimal KgCo2PorKm { get; set; } = 0.192m;

    /// <summary>Distancia que se asume cuando el viaje no tiene coordenadas de destino (km).</summary>
    [Range(0.1, 500)]
    public double DistanciaPorDefectoKm { get; set; } = 12;

    /// <summary>Banderazo de la tarifa de referencia de transporte privado (taxi / app), en MXN.</summary>
    [Range(0, 1000)]
    public decimal TarifaReferenciaBanderazo { get; set; } = 35m;

    /// <summary>Costo por km de la tarifa de referencia de transporte privado, en MXN.</summary>
    [Range(0, 200)]
    public decimal TarifaReferenciaPorKm { get; set; } = 9m;
}
