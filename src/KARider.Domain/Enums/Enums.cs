namespace KARider.Domain.Enums;

public enum Rol
{
    Pasajero = 1,
    Conductor = 2,
    Administrador = 3
}

public enum EstadoViaje
{
    Borrador = 0,
    Programado = 1,
    EnCurso = 2,
    Completado = 3,
    Cancelado = 4
}

public enum EstadoReserva
{
    Pendiente = 1,
    Confirmada = 2,
    Rechazada = 3,
    Cancelada = 4,
    Completada = 5
}

public enum PropositoCodigo
{
    VerificarCorreo = 1,
    RestablecerPassword = 2
}

public enum TipoNotificacion
{
    ReservaSolicitada = 1,
    ReservaConfirmada = 2,
    ReservaRechazada = 3,
    ReservaCancelada = 4,
    ViajeActualizado = 5,
    ViajeIniciado = 6,
    ViajeCompletado = 7,
    ViajeCancelado = 8,
    CalificacionRecibida = 9,
    NuevoViajeDisponible = 10
}
