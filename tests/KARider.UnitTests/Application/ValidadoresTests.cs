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
        new(Rol.Pasajero, "María José Hernández García Castro López", "22300631", "7731234567", 1, 3, correo, password, password, null);

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

    [Theory]
    [InlineData("Ana López")] // menos de 40 caracteres
    [InlineData("María José Hernández García Castro López Ramírez")] // más de 40 caracteres
    [InlineData("Ana María López Hernández García Pérez #2")] // tiene caracteres que no son letras/espacios
    public void Registro_rechaza_nombre_completo_fuera_de_las_reglas(string nombre)
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido() with { NombreCompleto = nombre });

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.NombreCompleto));
    }

    [Theory]
    [InlineData("2230063")] // 7 dígitos
    [InlineData("223006311")] // 9 dígitos
    [InlineData("2230063A")] // contiene una letra
    public void Registro_rechaza_matricula_que_no_sean_exactamente_8_digitos(string matricula)
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido() with { Matricula = matricula });

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.Matricula));
    }

    [Theory]
    [InlineData("773123456")] // 9 dígitos
    [InlineData("77312345678")] // 11 dígitos
    [InlineData("773123A567")] // contiene una letra
    [InlineData("773 123 4567")] // tiene espacios
    public void Registro_rechaza_telefono_que_no_sean_exactamente_10_digitos(string telefono)
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido() with { Telefono = telefono });

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.Telefono));
    }

    [Theory]
    [InlineData("Nissan Versa 2024")] // contiene números
    [InlineData("Un Modelo Demasiado Largo Para Entrar")] // más de 20 caracteres
    public void Vehiculo_rechaza_modelo_fuera_de_las_reglas(string modelo)
    {
        var request = new VehiculoRequest(modelo, "Azul", 2022, "HNX421BCD", 4);
        var resultado = new VehiculoRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(VehiculoRequest.Modelo));
    }

    [Theory]
    [InlineData("HNX421BC")] // 8 caracteres
    [InlineData("HNX421BCDE")] // 10 caracteres
    [InlineData("HNX-421-C")] // tiene guiones
    public void Vehiculo_rechaza_placas_que_no_sean_exactamente_9_alfanumericos(string placas)
    {
        var request = new VehiculoRequest("Nissan Versa", "Azul", 2022, placas, 4);
        var resultado = new VehiculoRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(VehiculoRequest.Placas));
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
