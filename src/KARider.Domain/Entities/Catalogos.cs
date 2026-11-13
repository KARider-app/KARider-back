namespace KARider.Domain.Entities;

///Carrera universitaria de la UTTT catálogo
public sealed class Carrera
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;
}

/// <summary>Punto de encuentro dentro o cerca del campus (catálogo administrado).</summary>
public sealed class PuntoEncuentro
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public double Latitud { get; set; }

    public double Longitud { get; set; }

    public bool Activo { get; set; } = true;
}
