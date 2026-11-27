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
    [InlineData("Karider#2026Karider#2026Karider", "30 caracteres")] // 31 caracteres
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
    [InlineData("Ana López")] // nombre corto
    [InlineData("María José Hernández García Castro López Ramírez")] // 49 letras
    [InlineData("Ana Sofía Ximena Guadalupe Hernández Gutiérrez de la Fuentes")] // exactamente 60
    public void Registro_acepta_nombres_de_cualquier_longitud_hasta_60_letras(string nombre)
    {
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido() with { NombreCompleto = nombre });

        Assert.DoesNotContain(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.NombreCompleto));
    }

    [Fact]
    public void Registro_rechaza_nombre_de_mas_de_60_letras_con_mensaje_claro()
    {
        var nombre = "Ana Sofía Ximena Guadalupe Hernández Gutiérrez de la Fuente Ruiz"; // 64 letras
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido() with { NombreCompleto = nombre });

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(RegistroRequest.NombreCompleto) && e.ErrorMessage.Contains("60"));
    }

    [Theory]
    [InlineData("")] // vacío
    [InlineData("Ana María López #2")] // tiene caracteres que no son letras/espacios
    [InlineData("Ana María López 2")] // tiene números
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

    [Fact]
    public void Registro_acepta_password_de_exactamente_30_caracteres()
    {
        var password = "Karider#2026Karider#2026Karide"; // 30 caracteres
        var resultado = new RegistroRequestValidator(Institucion, TimeProvider.System).Validate(RegistroValido(password: password));

        Assert.True(resultado.IsValid, string.Join(" | ", resultado.Errors));
    }

    [Fact]
    public void Login_sigue_aceptando_contrasenas_largas_de_cuentas_anteriores()
    {
        var request = new LoginRequest("ana.lopez@uttt.edu.mx", new string('K', 40) + "#1a", null, false);
        var resultado = new LoginRequestValidator(Institucion).Validate(request);

        Assert.True(resultado.IsValid, string.Join(" | ", resultado.Errors));
    }

    [Theory]
    [InlineData("Gris Oxford")]
    [InlineData("Azul Marino")]
    [InlineData("Rojo Cereza Mat")] // exactamente 15 caracteres
    public void Vehiculo_acepta_color_solo_con_letras_hasta_15_caracteres(string color)
    {
        var request = new VehiculoRequest("Nissan Versa", color, 2022, "HNX421BCD", 4);
        var resultado = new VehiculoRequestValidator(TimeProvider.System).Validate(request);

        Assert.DoesNotContain(resultado.Errors, e => e.PropertyName == nameof(VehiculoRequest.Color));
    }

    [Theory]
    [InlineData("Azul 2")] // tiene un número
    [InlineData("Gris-Plata")] // tiene un guion
    [InlineData("Rojo!")] // tiene un símbolo
    [InlineData("Rojo Cereza Mate")] // 16 caracteres
    [InlineData("")] // vacío
    public void Vehiculo_rechaza_color_con_numeros_simbolos_o_mas_de_15_caracteres(string color)
    {
        var request = new VehiculoRequest("Nissan Versa", color, 2022, "HNX421BCD", 4);
        var resultado = new VehiculoRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(VehiculoRequest.Color));
    }

    [Theory]
    [InlineData(202)] // 3 dígitos
    [InlineData(20190)] // 5 dígitos
    [InlineData(-2019)] // negativo
    [InlineData(1985)] // 4 dígitos pero anterior a 1990
    public void Vehiculo_rechaza_anio_que_no_sea_de_4_digitos_y_en_rango(int anio)
    {
        var request = new VehiculoRequest("Nissan Versa", "Azul", anio, "HNX421BCD", 4);
        var resultado = new VehiculoRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(VehiculoRequest.Anio));
    }

    [Fact]
    public void Vehiculo_con_anio_de_3_o_5_digitos_explica_que_son_4_digitos()
    {
        var request = new VehiculoRequest("Nissan Versa", "Azul", 20190, "HNX421BCD", 4);
        var resultado = new VehiculoRequestValidator(TimeProvider.System).Validate(request);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(VehiculoRequest.Anio) && e.ErrorMessage.Contains("4 dígitos"));
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
