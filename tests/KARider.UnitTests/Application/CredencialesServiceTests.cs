using KARider.Application.Common;
using KARider.Application.Features.Perfil;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using KARider.Domain.Errors;
using KARider.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KARider.UnitTests.Application;

/// <summary>HU-12: consultar credenciales institucionales y del vehículo verificado (P15).</summary>
public sealed class CredencialesServiceTests
{
    private readonly string _baseDatos = Guid.NewGuid().ToString();

    private AppDbContext NuevoContexto() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_baseDatos)
        .Options);

    private PerfilService Servicio(AppDbContext db) => new(db, Options.Create(new EstadisticasOptions()));

    /// <summary>Siembra un usuario (con un vehículo opcional) y devuelve su id.</summary>
    private async Task<Guid> SembrarAsync(Rol rol, bool correoVerificado = true, bool? vehiculoVerificado = null)
    {
        await using var db = NuevoContexto();
        if (!await db.Carreras.AnyAsync())
        {
            db.Carreras.Add(new Carrera { Id = 1, Nombre = "Ing. en Desarrollo y Gestión de Software" });
        }

        var sufijo = Random.Shared.Next(10000, 99999).ToString();
        var usuario = Usuario.Crear("Usuario " + sufijo, "223" + sufijo, "77312" + sufijo, $"u{sufijo}@uttt.edu.mx", "hash", rol, 1, 7);
        if (correoVerificado)
        {
            usuario.MarcarCorreoVerificado(DateTimeOffset.UtcNow);
        }

        db.Usuarios.Add(usuario);

        if (vehiculoVerificado is { } verificado)
        {
            var vehiculo = Vehiculo.Crear(usuario.Id, "Nissan Versa", "Azul", 2022, "HNX-" + sufijo[..3] + "-C", 4);
            if (verificado)
            {
                vehiculo.MarcarVerificado();
            }

            db.Vehiculos.Add(vehiculo);
        }

        await db.SaveChangesAsync();
        return usuario.Id;
    }

    [Fact]
    public async Task Criterio1_el_pasajero_solo_ve_su_ficha_institucional_sin_bloque_de_vehiculo()
    {
        // Aunque existiera un vehículo asociado, la regla 1 dice que solo se muestra a conductores.
        var id = await SembrarAsync(Rol.Pasajero, vehiculoVerificado: true);
        await using var db = NuevoContexto();

        var resultado = await Servicio(db).ObtenerCredencialesAsync(id, id, default);

        Assert.True(resultado.IsSuccess);
        var credenciales = resultado.Value;
        Assert.EndsWith("@uttt.edu.mx", credenciales.CorreoInstitucional);
        Assert.StartsWith("223", credenciales.Matricula);
        Assert.Equal("Ing. en Desarrollo y Gestión de Software", credenciales.Carrera);
        Assert.Equal("Alumno Regular Activo", credenciales.Estatus);
        Assert.Empty(credenciales.Vehiculos);
    }

    [Fact]
    public async Task Criterio2_el_conductor_ve_su_ficha_y_el_vehiculo_verificado()
    {
        var id = await SembrarAsync(Rol.Conductor, vehiculoVerificado: true);
        await using var db = NuevoContexto();

        var credenciales = (await Servicio(db).ObtenerCredencialesAsync(id, id, default)).Value;

        var vehiculo = Assert.Single(credenciales.Vehiculos);
        Assert.Equal("Nissan Versa", vehiculo.Modelo);
        Assert.Equal(4, vehiculo.Capacidad);
        Assert.True(vehiculo.Verificado);
    }

    [Fact]
    public async Task Criterio3_el_vehiculo_pendiente_se_muestra_completo_con_estado_pendiente()
    {
        var id = await SembrarAsync(Rol.Conductor, vehiculoVerificado: false);
        await using var db = NuevoContexto();

        var vehiculo = Assert.Single((await Servicio(db).ObtenerCredencialesAsync(id, id, default)).Value.Vehiculos);

        Assert.False(vehiculo.Verificado);
        Assert.Equal("Nissan Versa", vehiculo.Modelo);
        Assert.StartsWith("HNX-", vehiculo.Placas);
    }

    [Fact]
    public async Task Criterio4_consultar_las_credenciales_de_otro_usuario_responde_403()
    {
        var propio = await SembrarAsync(Rol.Pasajero);
        var otro = await SembrarAsync(Rol.Conductor, vehiculoVerificado: true);
        await using var db = NuevoContexto();

        var resultado = await Servicio(db).ObtenerCredencialesAsync(propio, otro, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(DomainErrors.Usuario.CredencialesAjenas, resultado.Error);
        Assert.Equal(KARider.Domain.Common.ErrorType.Forbidden, resultado.Error.Type);
    }

    [Fact]
    public async Task Regla2_el_estatus_se_deriva_del_correo_verificado()
    {
        var id = await SembrarAsync(Rol.Pasajero, correoVerificado: false);
        await using var db = NuevoContexto();

        var credenciales = (await Servicio(db).ObtenerCredencialesAsync(id, id, default)).Value;

        Assert.False(credenciales.CorreoVerificado);
        Assert.Equal("Pendiente de verificación", credenciales.Estatus);
    }

    [Fact]
    public async Task Un_usuario_inexistente_responde_no_encontrado()
    {
        await using var db = NuevoContexto();
        var id = Guid.NewGuid();

        var resultado = await Servicio(db).ObtenerCredencialesAsync(id, id, default);

        Assert.Equal(DomainErrors.Usuario.NoEncontrado, resultado.Error);
    }
}
