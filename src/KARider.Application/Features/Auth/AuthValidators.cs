using FluentValidation;
using KARider.Application.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;
using Microsoft.Extensions.Options;

namespace KARider.Application.Features.Auth;

public sealed class VehiculoRequestValidator : AbstractValidator<VehiculoRequest>
{
    public VehiculoRequestValidator(TimeProvider time)
    {
        var anioMaximo = time.GetUtcNow().Year + 1;

        RuleFor(x => x.Modelo)
            .NotEmpty().WithMessage("El modelo del vehículo es obligatorio (ej. Nissan Versa).")
            .MaximumLength(20).WithMessage("El modelo del vehículo no puede exceder 20 caracteres.")
            .Matches(@"^[\p{L}\s]+$").WithMessage("El modelo del vehículo solo puede contener letras y espacios.");
        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("El color del vehículo es obligatorio.")
            .MaximumLength(30).WithMessage("El color no puede exceder 30 caracteres.");
        RuleFor(x => x.Anio)
            .InclusiveBetween(1990, anioMaximo).WithMessage($"El año del vehículo debe estar entre 1990 y {anioMaximo}.");
        RuleFor(x => x.Placas)
            .NotEmpty().WithMessage("Las placas del vehículo son obligatorias.")
            .Matches("^[A-Za-z0-9]{9}$").WithMessage("Las placas deben tener exactamente 9 caracteres alfanuméricos, sin guiones (ej. HNX421BCD).");
        RuleFor(x => x.Capacidad)
            .InclusiveBetween(1, Vehiculo.CapacidadMaxima)
            .WithMessage($"La capacidad para pasajeros debe estar entre 1 y {Vehiculo.CapacidadMaxima}.");
    }
}

public sealed class RegistroRequestValidator : AbstractValidator<RegistroRequest>
{
    public RegistroRequestValidator(IOptions<InstitucionOptions> institucion, TimeProvider time)
    {
        var dominio = institucion.Value.DominioCorreo;

        RuleFor(x => x.Rol)
            .Must(r => r is Rol.Pasajero or Rol.Conductor)
            .WithMessage("El rol debe ser «Pasajero» o «Conductor».");
        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .Length(40).WithMessage("El nombre completo debe tener exactamente 40 caracteres.")
            .Matches(@"^[\p{L}\s]+$").WithMessage("El nombre completo solo puede contener letras y espacios, como aparece en tu credencial.");
        RuleFor(x => x.Matricula)
            .NotEmpty().WithMessage("La matrícula UTTT es obligatoria.")
            .Matches(@"^\d{8}$").WithMessage("La matrícula UTTT debe contener exactamente 8 dígitos.");
        RuleFor(x => x.Telefono).Telefono();
        RuleFor(x => x.CarreraId)
            .GreaterThan(0).WithMessage("Selecciona tu carrera universitaria.");
        RuleFor(x => x.Cuatrimestre)
            .InclusiveBetween(1, 11).When(x => x.Cuatrimestre.HasValue)
            .WithMessage("El cuatrimestre debe estar entre 1 y 11.");
        RuleFor(x => x.CorreoInstitucional).CorreoInstitucional(dominio);
        RuleFor(x => x.Password).PasswordSegura();
        RuleFor(x => x.ConfirmarPassword)
            .Equal(x => x.Password).WithMessage("La confirmación no coincide con la contraseña.");
        RuleFor(x => x.Vehiculo)
            .NotNull().When(x => x.Rol == Rol.Conductor)
            .WithMessage("Para registrarte como conductor debes capturar los datos de tu vehículo.");
        RuleFor(x => x.Vehiculo!)
            .SetValidator(new VehiculoRequestValidator(time))
            .When(x => x.Vehiculo is not null);
    }
}

public sealed class VerificarCorreoRequestValidator : AbstractValidator<VerificarCorreoRequest>
{
    public VerificarCorreoRequestValidator(IOptions<InstitucionOptions> institucion)
    {
        RuleFor(x => x.CorreoInstitucional).CorreoInstitucional(institucion.Value.DominioCorreo);
        RuleFor(x => x.Codigo).CodigoVerificacion();
    }
}

public sealed class CorreoRequestValidator : AbstractValidator<CorreoRequest>
{
    public CorreoRequestValidator(IOptions<InstitucionOptions> institucion) =>
        RuleFor(x => x.CorreoInstitucional).CorreoInstitucional(institucion.Value.DominioCorreo);
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator(IOptions<InstitucionOptions> institucion)
    {
        RuleFor(x => x.CorreoInstitucional).CorreoInstitucional(institucion.Value.DominioCorreo);
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña no puede exceder 128 caracteres.");
        RuleFor(x => x.Rol)
            .Must(r => r is null or Rol.Pasajero or Rol.Conductor)
            .WithMessage("El rol de ingreso debe ser «Pasajero» o «Conductor».");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() =>
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("El refresh token es obligatorio.")
            .MaximumLength(200).WithMessage("El refresh token no tiene un formato válido.");
}

public sealed class RestablecerPasswordRequestValidator : AbstractValidator<RestablecerPasswordRequest>
{
    public RestablecerPasswordRequestValidator(IOptions<InstitucionOptions> institucion)
    {
        RuleFor(x => x.CorreoInstitucional).CorreoInstitucional(institucion.Value.DominioCorreo);
        RuleFor(x => x.Codigo).CodigoVerificacion();
        RuleFor(x => x.NuevaPassword).PasswordSegura();
        RuleFor(x => x.ConfirmarPassword)
            .Equal(x => x.NuevaPassword).WithMessage("La confirmación no coincide con la nueva contraseña.");
    }
}

public sealed class CambiarPasswordRequestValidator : AbstractValidator<CambiarPasswordRequest>
{
    public CambiarPasswordRequestValidator()
    {
        RuleFor(x => x.PasswordActual).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
        RuleFor(x => x.NuevaPassword)
            .PasswordSegura()
            .NotEqual(x => x.PasswordActual).WithMessage("La nueva contraseña debe ser diferente a la actual.");
        RuleFor(x => x.ConfirmarPassword)
            .Equal(x => x.NuevaPassword).WithMessage("La confirmación no coincide con la nueva contraseña.");
    }
}
