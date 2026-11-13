using KARider.Application.Common;
using KARider.Application.Features.Auth;
using KARider.Application.Features.Viajes;
using KARider.Domain.Enums;
using Microsoft.Extensions.Options;

namespace KARider.UnitTests.Application;

public sealed class ValidadoresTests
{
    private static readonly IOptions<InstitucionOptions> Institucion = Options.Create(new InstitucionOptions());

    private static RegistroRequest RegistroValido(string correo = "ana.lopez@uttt.edu.mx", string password = "Karider#2026") =>
        new(Rol.Pasajero, "Ana López", "22300631", "773 123 4567", 1, 3, correo, password, password, null);

    [Fact]
    public void Registro_valido_no_tiene_errores()
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido());
        Assert.True(resultado.IsValid, string.Join(" | ", resultado.Errors));
    }

    [Fact]
    public void Registro_rechaza_correos_fuera_del_dominio_institucional()
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido("ana@gmail.com"));

        var error = Assert.Single(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.CorreoInstitucional));
        Assert.Contains("@uttt.edu.mx", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("corta1!", "al menos 8 caracteres")]
    [InlineData("sinmayuscula1!", "mayúscula")]
    [InlineData("SinNumero!!", "número")]
    [InlineData("SinEspecial123", "carácter especial")]
    public void Registro_exige_password_segura_con_mensaje_especifico(string password, string mensaje)
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido(password: password));
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains(mensaje, StringComparison.Ordinal));
    }

    [Fact]
    public void Registro_como_conductor_exige_vehiculo()
    {
        var request = RegistroValido() with { Rol = Rol.Conductor };
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.Vehiculo));
    }

    [Fact]
    public void Publicar_viaje_valida_asientos_gasto_y_paradas()
    {
        var request = new PublicarViajeRequest(
            null, 1, "Pachuca", null, null, DateTimeOffset.UtcNow.AddHours(2),
            Asientos: 6, GastoTotal: 0, Notas: null, Paradas: ["a", "b", "c", "d", "e", "f"]);

        var resultado = new PublicarViajeRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(PublicarViajeRequest.Asientos));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(PublicarViajeRequest.GastoTotal));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(PublicarViajeRequest.Paradas));
    }

    [Fact]
    public void Editar_viaje_valido_no_tiene_errores()
    {
        var request = new EditarViajeRequest(
            1, "Centro de Tula", null, null, DateTimeOffset.UtcNow.AddHours(3),
            3, 200m, "Nota", ["Plaza Galerías"], null);

        var resultado = new EditarViajeRequestValidator(TimeProvider.System).Validate(request);

        Assert.True(resultado.IsValid, string.Join(" | ", resultado.Errors));
    }

    [Fact]
    public void Editar_viaje_comparte_las_reglas_de_ruta_y_gastos_con_publicar()
    {
        var request = new EditarViajeRequest(
            1, "Centro de Tula", null, null, DateTimeOffset.UtcNow.AddHours(3),
            6, 0, null, ["a", "b", "c", "d", "e", "f"], null);

        var resultado = new EditarViajeRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarViajeRequest.Asientos));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarViajeRequest.GastoTotal));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarViajeRequest.Paradas));
    }

    [Fact]
    public void Editar_viaje_rechaza_fecha_pasada_y_mensaje_de_aviso_demasiado_largo()
    {
        var request = new EditarViajeRequest(
            1, "Centro de Tula", null, null, DateTimeOffset.UtcNow.AddMinutes(-5),
            3, 200m, null, [], new string('a', 301));

        var resultado = new EditarViajeRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarViajeRequest.FechaSalida));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EditarViajeRequest.MensajeAviso));
    }
}
