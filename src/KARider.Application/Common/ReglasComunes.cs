using FluentValidation;

namespace KARider.Application.Common;

//Reglas de validación reutilizables con mensajes específicos en español para hacer la validación completa

public static class ReglasComunes
{
    public static IRuleBuilderOptions<T, string> PasswordSegura<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(128).WithMessage("La contraseña no puede exceder 128 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe incluir al menos un carácter especial (ej. !@#$%).");

    public static IRuleBuilderOptions<T, string> CorreoInstitucional<T>(this IRuleBuilder<T, string> rule, string dominio) =>
        rule
            .NotEmpty().WithMessage("El correo institucional es obligatorio.")
            .MaximumLength(150).WithMessage("El correo institucional no puede exceder 150 caracteres.")
            .EmailAddress().WithMessage("El correo institucional no tiene un formato válido.")
            .Must(c => c.Trim().EndsWith("@" + dominio, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Solo se permiten correos institucionales con dominio @{dominio}.");

    public static IRuleBuilderOptions<T, string> Telefono<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("El número de WhatsApp/celular es obligatorio.")
            .Matches(@"^\+?[0-9\s\-]{10,18}$").WithMessage("El número de WhatsApp/celular debe contener 10 dígitos (puede incluir lada, espacios o guiones).")
            .Must(t => t.Count(char.IsDigit) is >= 10 and <= 13).WithMessage("El número de WhatsApp/celular debe tener entre 10 y 13 dígitos.");

    public static IRuleBuilderOptions<T, string> CodigoVerificacion<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("El código de verificación es obligatorio.")
            .Matches(@"^\d{6}$").WithMessage("El código de verificación debe tener 6 dígitos.");

    public static IRuleBuilderOptions<T, double> Latitud<T>(this IRuleBuilder<T, double> rule) =>
        rule.InclusiveBetween(-90, 90).WithMessage("La latitud debe estar entre -90 y 90.");

    public static IRuleBuilderOptions<T, double> Longitud<T>(this IRuleBuilder<T, double> rule) =>
        rule.InclusiveBetween(-180, 180).WithMessage("La longitud debe estar entre -180 y 180.");
}
