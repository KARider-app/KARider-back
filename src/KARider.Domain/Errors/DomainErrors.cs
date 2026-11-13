using KARider.Domain.Common;
using KARider.Domain.Enums;

namespace KARider.Domain.Errors;

/// <summary>
/// Catálogo central de errores de negocio. Cada código es estable y está documentado en docs/errores.md.
/// </summary>
public static class DomainErrors
{
    public static class Auth
    {
        public static readonly Error CredencialesInvalidas = Error.Unauthorized(
            "AUTH_CREDENCIALES_INVALIDAS",
            "El correo institucional o la contraseña son incorrectos.");

        public static Error CuentaBloqueada(DateTimeOffset hasta, DateTimeOffset ahora)
        {
            var minutos = Math.Max(1, (int)Math.Ceiling((hasta - ahora).TotalMinutes));
            return Error.Locked(
                "AUTH_CUENTA_BLOQUEADA",
                $"Tu cuenta está bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en {minutos} minuto(s) o restablece tu contraseña.");
        }

        public static readonly Error CorreoNoVerificado = Error.Forbidden(
            "AUTH_CORREO_NO_VERIFICADO",
            "Debes verificar tu correo institucional antes de iniciar sesión. Revisa tu bandeja de entrada o solicita un nuevo código.");

        public static readonly Error CuentaInactiva = Error.Forbidden(
            "AUTH_CUENTA_INACTIVA",
            "Tu cuenta está desactivada. Contacta a la administración de KARider.");

        public static readonly Error RolNoAutorizado = Error.Forbidden(
            "AUTH_ROL_NO_AUTORIZADO",
            "Tu cuenta no está registrada como conductor. Registra un vehículo en tu perfil para ingresar con este rol.");

        public static readonly Error RefreshTokenInvalido = Error.Unauthorized(
            "AUTH_REFRESH_TOKEN_INVALIDO",
            "La sesión expiró o fue cerrada. Inicia sesión nuevamente.");

        public static readonly Error CodigoInvalido = Error.Validation(
            "AUTH_CODIGO_INVALIDO",
            "El código de verificación es incorrecto. Revisa el último correo que te enviamos.");

        public static readonly Error CodigoExpirado = Error.Validation(
            "AUTH_CODIGO_EXPIRADO",
            "El código de verificación expiró. Solicita uno nuevo.");

        public static readonly Error CodigoIntentosExcedidos = Error.TooManyRequests(
            "AUTH_CODIGO_INTENTOS_EXCEDIDOS",
            "Superaste el número de intentos permitidos para este código. Solicita uno nuevo.");

        public static readonly Error CorreoYaVerificado = Error.Conflict(
            "AUTH_CORREO_YA_VERIFICADO",
            "Tu correo institucional ya fue verificado. Puedes iniciar sesión.");

        public static readonly Error PasswordActualIncorrecta = Error.Validation(
            "AUTH_PASSWORD_ACTUAL_INCORRECTA",
            "La contraseña actual no es correcta.");
    }

    public static class Usuario
    {
        public static readonly Error CorreoDuplicado = Error.Conflict(
            "USUARIO_CORREO_DUPLICADO",
            "Ya existe una cuenta registrada con este correo institucional. Inicia sesión o recupera tu contraseña.");

        public static readonly Error MatriculaDuplicada = Error.Conflict(
            "USUARIO_MATRICULA_DUPLICADA",
            "Ya existe una cuenta registrada con esta matrícula UTTT.");

        public static readonly Error NoEncontrado = Error.NotFound(
            "USUARIO_NO_ENCONTRADO",
            "El usuario solicitado no existe o fue desactivado.");

        public static readonly Error CredencialesAjenas = Error.Forbidden(
            "USUARIO_CREDENCIALES_AJENAS",
            "Solo puedes consultar tus propias credenciales.");

        public static readonly Error CarreraInvalida = Error.Validation(
            "USUARIO_CARRERA_INVALIDA",
            "La carrera seleccionada no existe en el catálogo de la universidad.");
    }

    public static class Vehiculo
    {
        public static readonly Error PlacasDuplicadas = Error.Conflict(
            "VEHICULO_PLACAS_DUPLICADAS",
            "Ya existe un vehículo activo registrado con estas placas.");

        public static readonly Error NoEncontrado = Error.NotFound(
            "VEHICULO_NO_ENCONTRADO",
            "El vehículo no existe o no pertenece a tu cuenta.");

        public static readonly Error RequeridoParaConductor = Error.Validation(
            "VEHICULO_REQUERIDO_CONDUCTOR",
            "Para registrarte como conductor debes capturar los datos de tu vehículo (modelo, color, año y placas).");

        public static readonly Error ConViajesActivos = Error.Conflict(
            "VEHICULO_CON_VIAJES_ACTIVOS",
            "No puedes dar de baja este vehículo porque tiene viajes programados o en curso.");

        public static Error CapacidadInsuficiente(int solicitados, int capacidad) => Error.BusinessRule(
            "VEHICULO_CAPACIDAD_INSUFICIENTE",
            $"Los asientos ofrecidos ({solicitados}) superan la capacidad autorizada del vehículo ({capacidad}).");

        public static readonly Error SinVehiculo = Error.BusinessRule(
            "VEHICULO_NO_REGISTRADO",
            "Debes registrar un vehículo activo antes de publicar un viaje.");
    }

    public static class Viaje
    {
        public static readonly Error NoEncontrado = Error.NotFound(
            "VIAJE_NO_ENCONTRADO",
            "El viaje solicitado no existe o fue eliminado.");

        public static readonly Error NoEsPropietario = Error.Forbidden(
            "VIAJE_NO_ES_PROPIETARIO",
            "Solo el conductor que publicó el viaje puede realizar esta acción.");

        public static readonly Error FechaPasada = Error.BusinessRule(
            "VIAJE_FECHA_PASADA",
            "La fecha y hora de salida debe ser posterior al momento actual.");

        public static readonly Error PuntoEncuentroInvalido = Error.Validation(
            "VIAJE_PUNTO_ENCUENTRO_INVALIDO",
            "El punto de encuentro seleccionado no existe o no está habilitado en el campus.");

        public static Error NoEditable(EstadoViaje estado) => Error.BusinessRule(
            "VIAJE_NO_EDITABLE",
            $"El viaje está en estado «{estado}» y ya no puede modificarse.");

        public static Error TransicionInvalida(EstadoViaje actual, EstadoViaje nuevo) => Error.BusinessRule(
            "VIAJE_TRANSICION_INVALIDA",
            $"No es posible cambiar el viaje de «{actual}» a «{nuevo}».");

        public static Error SinCupo(int disponibles) => Error.Conflict(
            "VIAJE_SIN_CUPO",
            disponibles == 0
                ? "El viaje ya no tiene asientos disponibles."
                : $"Solo quedan {disponibles} asiento(s) disponible(s) en este viaje.");

        public static Error AsientosInsuficientes(int reservados) => Error.BusinessRule(
            "VIAJE_ASIENTOS_INSUFICIENTES",
            $"Ya hay {reservados} asiento(s) reservado(s): no puedes ofrecer menos de {reservados}.");

        public static readonly Error NoDisponible = Error.BusinessRule(
            "VIAJE_NO_DISPONIBLE",
            "El viaje ya no acepta reservas porque salió, fue cancelado o aún es un borrador.");

        public static readonly Error UbicacionNoDisponible = Error.NotFound(
            "VIAJE_UBICACION_NO_DISPONIBLE",
            "El conductor aún no comparte su ubicación para este viaje.");

        public static readonly Error UbicacionNoPermitida = Error.BusinessRule(
            "VIAJE_UBICACION_NO_PERMITIDA",
            "Solo puedes compartir ubicación en viajes programados o en curso.");

        public static readonly Error AccesoDenegado = Error.Forbidden(
            "VIAJE_ACCESO_DENEGADO",
            "Solo el conductor y los pasajeros con reserva confirmada pueden consultar esta información.");
    }

    public static class Reserva
    {
        public static readonly Error NoEncontrada = Error.NotFound(
            "RESERVA_NO_ENCONTRADA",
            "La reserva solicitada no existe.");

        public static readonly Error ViajePropio = Error.BusinessRule(
            "RESERVA_VIAJE_PROPIO",
            "No puedes apartar asientos en un viaje que tú publicaste.");

        public static Error Duplicada(string folio) => Error.Conflict(
            "RESERVA_DUPLICADA",
            $"Ya tienes una reserva activa en este viaje (folio {folio}).");

        public static Error AsientosInvalidos(int maximo) => Error.Validation(
            "RESERVA_ASIENTOS_INVALIDOS",
            $"Puedes apartar entre 1 y {maximo} asientos por reserva.");

        public static Error EstadoInvalido(EstadoReserva estado, string accion) => Error.BusinessRule(
            "RESERVA_ESTADO_INVALIDO",
            $"La reserva está «{estado}», por lo que no se puede {accion}.");

        public static readonly Error AccesoDenegado = Error.Forbidden(
            "RESERVA_ACCESO_DENEGADO",
            "Solo el pasajero que hizo la reserva o el conductor del viaje pueden consultarla o modificarla.");
    }

    public static class Calificacion
    {
        public static readonly Error ViajeNoCompletado = Error.BusinessRule(
            "CALIFICACION_VIAJE_NO_COMPLETADO",
            "Solo puedes calificar cuando el viaje está marcado como completado.");

        public static readonly Error NoParticipante = Error.Forbidden(
            "CALIFICACION_NO_PARTICIPANTE",
            "Solo el conductor y los pasajeros confirmados del viaje pueden calificar.");

        public static readonly Error EvaluadoInvalido = Error.BusinessRule(
            "CALIFICACION_EVALUADO_INVALIDO",
            "Solo puedes calificar al conductor o a los pasajeros confirmados de este viaje.");

        public static readonly Error Autoevaluacion = Error.BusinessRule(
            "CALIFICACION_AUTOEVALUACION",
            "No puedes calificarte a ti mismo.");

        public static readonly Error Duplicada = Error.Conflict(
            "CALIFICACION_DUPLICADA",
            "Ya calificaste a este compañero para este viaje.");

        public static readonly Error EstrellasInvalidas = Error.Validation(
            "CALIFICACION_ESTRELLAS_INVALIDAS",
            "La calificación debe estar entre 1 y 5 estrellas.");

        public static Error EtiquetaInvalida(string etiqueta) => Error.Validation(
            "CALIFICACION_ETIQUETA_INVALIDA",
            $"La etiqueta «{etiqueta}» no forma parte del catálogo de calificaciones.");
    }

    public static class Notificacion
    {
        public static readonly Error NoEncontrada = Error.NotFound(
            "NOTIFICACION_NO_ENCONTRADA",
            "La notificación no existe o no pertenece a tu cuenta.");
    }
}
