using FluentValidation;
using KARider.Application.Common;
using KARider.Domain.Entities;
using KARider.Domain.Enums;

namespace KARider.Application.Features.Viajes;

public sealed class PublicarViajeRequestValidator : AbstractValidator<PublicarViajeRequest>
{
    public const int MaximoParadas = 5;
    public const int DiasMaximosAnticipacion = 30;

    public PublicarViajeRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.PuntoEncuentroId)
            .GreaterThan(0).WithMessage("Selecciona el punto de encuentro en el campus UTTT.");
        RuleFor(x => x.Destino)
            .NotEmpty().WithMessage("El destino final es obligatorio.")
            .Length(3, 150).WithMessage("El destino debe tener entre 3 y 150 caracteres.");
        RuleFor(x => x.DestinoLatitud!.Value).Latitud().When(x => x.DestinoLatitud.HasValue);
        RuleFor(x => x.DestinoLongitud!.Value).Longitud().When(x => x.DestinoLongitud.HasValue);
        RuleFor(x => x)
            .Must(x => x.DestinoLatitud.HasValue == x.DestinoLongitud.HasValue)
            .WithName("DestinoLatitud")
            .WithMessage("Envía latitud y longitud del destino juntas, o ninguna de las dos.");
        RuleFor(x => x.FechaSalida)
            .Must(f => f > time.GetUtcNow()).WithMessage("La fecha y hora de salida debe ser posterior al momento actual.")
            .Must(f => f <= time.GetUtcNow().AddDays(DiasMaximosAnticipacion))
            .WithMessage($"Solo puedes publicar viajes con hasta {DiasMaximosAnticipacion} días de anticipación.");
        RuleFor(x => x.Asientos)
            .InclusiveBetween(1, Vehiculo.CapacidadMaxima)
            .WithMessage($"Los asientos disponibles deben estar entre 1 y {Vehiculo.CapacidadMaxima}.");
        RuleFor(x => x.GastoTotal)
            .GreaterThan(0).WithMessage("El gasto total (gasolina + casetas) debe ser mayor a 0.")
            .LessThanOrEqualTo(5000).WithMessage("El gasto total no puede exceder $5,000 MXN; el carpooling no tiene fines de lucro.")
            .PrecisionScale(8, 2, true).WithMessage("El gasto total admite como máximo 2 decimales.");
        RuleFor(x => x.Notas)
            .MaximumLength(500).WithMessage("Las indicaciones para pasajeros no pueden exceder 500 caracteres.");
        RuleFor(x => x.Paradas)
            .Must(p => p is null || p.Count <= MaximoParadas)
            .WithMessage($"Puedes agregar como máximo {MaximoParadas} paradas intermedias.");
        RuleForEach(x => x.Paradas)
            .NotEmpty().WithMessage("Las paradas intermedias no pueden estar vacías.")
            .MaximumLength(100).WithMessage("Cada parada intermedia puede tener como máximo 100 caracteres.");
    }
}

public sealed class EditarViajeRequestValidator : AbstractValidator<EditarViajeRequest>
{
    public EditarViajeRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.PuntoEncuentroId)
            .GreaterThan(0).WithMessage("Selecciona el punto de encuentro en el campus UTTT.");
        RuleFor(x => x.Destino)
            .NotEmpty().WithMessage("El destino final es obligatorio.")
            .Length(3, 150).WithMessage("El destino debe tener entre 3 y 150 caracteres.");
        RuleFor(x => x.DestinoLatitud!.Value).Latitud().When(x => x.DestinoLatitud.HasValue);
        RuleFor(x => x.DestinoLongitud!.Value).Longitud().When(x => x.DestinoLongitud.HasValue);
        RuleFor(x => x)
            .Must(x => x.DestinoLatitud.HasValue == x.DestinoLongitud.HasValue)
            .WithName("DestinoLatitud")
            .WithMessage("Envía latitud y longitud del destino juntas, o ninguna de las dos.");
        RuleFor(x => x.FechaSalida)
            .Must(f => f > time.GetUtcNow()).WithMessage("La nueva hora de salida debe ser posterior al momento actual.")
            .Must(f => f <= time.GetUtcNow().AddDays(PublicarViajeRequestValidator.DiasMaximosAnticipacion))
            .WithMessage($"Solo puedes programar viajes con hasta {PublicarViajeRequestValidator.DiasMaximosAnticipacion} días de anticipación.");
        RuleFor(x => x.Asientos)
            .InclusiveBetween(1, Vehiculo.CapacidadMaxima)
            .WithMessage($"Los asientos ofrecidos deben estar entre 1 y {Vehiculo.CapacidadMaxima}.");
        RuleFor(x => x.GastoTotal)
            .GreaterThan(0).WithMessage("El gasto total (gasolina + casetas) debe ser mayor a 0.")
            .LessThanOrEqualTo(5000).WithMessage("El gasto total no puede exceder $5,000 MXN; el carpooling no tiene fines de lucro.")
            .PrecisionScale(8, 2, true).WithMessage("El gasto total admite como máximo 2 decimales.");
        RuleFor(x => x.Notas)
            .MaximumLength(500).WithMessage("Las indicaciones para pasajeros no pueden exceder 500 caracteres.");
        RuleFor(x => x.Paradas)
            .Must(p => p is null || p.Count <= PublicarViajeRequestValidator.MaximoParadas)
            .WithMessage($"Puedes agregar como máximo {PublicarViajeRequestValidator.MaximoParadas} paradas intermedias.");
        RuleForEach(x => x.Paradas)
            .NotEmpty().WithMessage("Las paradas intermedias no pueden estar vacías.")
            .MaximumLength(100).WithMessage("Cada parada intermedia puede tener como máximo 100 caracteres.");
        RuleFor(x => x.MensajeAviso)
            .MaximumLength(300).WithMessage("El aviso para pasajeros no puede exceder 300 caracteres.");
    }
}

public sealed class CambiarEstadoViajeRequestValidator : AbstractValidator<CambiarEstadoViajeRequest>
{
    public CambiarEstadoViajeRequestValidator()
    {
        RuleFor(x => x.Estado)
            .IsInEnum().WithMessage("El estado debe ser Programado, EnCurso, Completado o Cancelado.")
            .NotEqual(EstadoViaje.Borrador).WithMessage("Un viaje no puede regresar a borrador.");
        RuleFor(x => x.Mensaje)
            .MaximumLength(300).WithMessage("El mensaje para pasajeros no puede exceder 300 caracteres.");
    }
}

public sealed class UbicacionRequestValidator : AbstractValidator<UbicacionRequest>
{
    public UbicacionRequestValidator()
    {
        RuleFor(x => x.Latitud).Latitud();
        RuleFor(x => x.Longitud).Longitud();
    }
}

public sealed class BuscarViajesQueryValidator : AbstractValidator<BuscarViajesQuery>
{
    public BuscarViajesQueryValidator()
    {
        this.ReglasPaginacion();
        RuleFor(x => x.Destino)
            .MaximumLength(100).WithMessage("El texto de búsqueda no puede exceder 100 caracteres.");
        RuleFor(x => x.PuntoEncuentroId)
            .GreaterThan(0).When(x => x.PuntoEncuentroId.HasValue)
            .WithMessage("El punto de encuentro no es válido.");
        RuleFor(x => x.Orden)
            .IsInEnum().WithMessage("El criterio de orden debe ser Horario, MenorAporte o Calificacion.");
    }
}

public sealed class MisViajesQueryValidator : AbstractValidator<MisViajesQuery>
{
    public MisViajesQueryValidator()
    {
        this.ReglasPaginacion();
        RuleFor(x => x.Estado).IsInEnum().When(x => x.Estado.HasValue).WithMessage("El estado del viaje no es válido.");
    }
}
