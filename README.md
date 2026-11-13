# KARider-back

API de **KARider**, una aplicación web progresiva para coordinar viajes compartidos entre miembros verificados de la Universidad Tecnológica de Tula-Tepeji (UTTT). Permite fijar puntos de encuentro, dividir el gasto de combustible y calificarse mutuamente.

**Stack:** .NET 9 · ASP.NET Core · EF Core 9 · PostgreSQL 15 · JWT · FluentValidation · Azure App Service · GitHub Actions

## Estructura

```
KARider-back/
├── KARider.sln
├── global.json · Directory.Build.props · .editorconfig
├── .env.example                  ← plantilla de variables de entorno (sin secretos)
├── docker-compose.yml            ← PostgreSQL 15 local (y la API con --profile full)
├── Dockerfile
├── .github/workflows/
│   ├── ci.yml                    ← compilación, pruebas, migraciones y vulnerabilidades
│   ├── sonarcloud.yml            ← análisis de calidad con SonarQube Cloud (Sprint 3)
│   └── deploy-azure.yml          ← despliegue a Azure App Service con OIDC
├── docs/
│   ├── arquitectura.md           ← capas, mapeo pantallas → endpoints, modelo de datos, escalamiento y seguridad
│   ├── errores.md                ← formato y catálogo de códigos de error
│   ├── despliegue-azure.md       ← guía paso a paso
│   ├── calidad-codigo.md         ← SonarQube Cloud: instalación, configuración y ejecución
│   └── versionamiento-bd.md      ← migraciones: aplicar, ver pendientes, revertir y reaplicar
├── scripts/generar-secretos.ps1
├── src/
│   ├── KARider.Domain/           ← entidades, reglas de negocio y errores (sin dependencias)
│   │   ├── Common/  (Entity, Result, Error)
│   │   ├── Entities/ (Usuario, Vehiculo, Viaje, Reserva, Calificacion, …)
│   │   ├── Enums/
│   │   └── Errors/DomainErrors.cs
│   ├── KARider.Application/      ← casos de uso por módulo del MVP
│   │   ├── Abstractions/ (puertos: DbContext, hashing, tokens, correo, push)
│   │   ├── Common/ (opciones, paginación, reglas de validación)
│   │   └── Features/ Auth · Viajes · Reservas · Calificaciones · Perfil · Vehiculos · Notificaciones · Catalogos
│   ├── KARider.Infrastructure/   ← EF Core + Npgsql, migraciones, JWT, PBKDF2, SMTP, Web Push
│   └── KARider.API/              ← controladores, errores estandarizados, seguridad HTTP
└── tests/KARider.UnitTests/      ← pruebas de dominio, validadores y autenticación
```

## Puesta en marcha local

Requisitos: .NET SDK 9 y Docker (o un PostgreSQL 15 propio).

```bash
cp .env.example .env                       # completa las contraseñas
pwsh ./scripts/generar-secretos.ps1        # pega Jwt__SigningKey y Hashing__Pepper en .env
docker compose up -d db                    # PostgreSQL 15 en localhost:5433
dotnet run --project src/KARider.API       # aplica migraciones y abre /swagger
```

- Documentación interactiva: http://localhost:5014/swagger (solo en Development).
- Salud: `/health/live` (proceso) y `/health/ready` (PostgreSQL).
- Sin SMTP configurado, el código de verificación se imprime en la consola de la API (solo en Development).
- Para no usar `.env`, ejecuta `pwsh ./scripts/generar-secretos.ps1 -UserSecrets` y `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena>" --project src/KARider.API`.
- Hay peticiones de ejemplo en [src/KARider.API/KARider.API.http](src/KARider.API/KARider.API.http).

```bash
dotnet test                                           # pruebas unitarias
dotnet ef migrations add <Nombre> -p src/KARider.Infrastructure -s src/KARider.API -o Persistence/Migrations
```

## Variables de entorno

Todas están documentadas en [.env.example](.env.example). Las obligatorias son:

| Variable | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión a PostgreSQL 15 (`SSL Mode=Require` en producción) |
| `Jwt__SigningKey` | Clave de firma de los JWT (32 caracteres o más) |
| `Hashing__Pepper` | Clave HMAC para códigos y refresh tokens (32 caracteres o más) |
| `Cors__AllowedOrigins__0` | Origen del frontend PWA |

Si falta alguna, la API no arranca y muestra un mensaje que indica qué variable configurar. Ningún secreto vive en `appsettings*.json` ni en el repositorio.

## Base de datos (Neon · PostgreSQL 15)

El esquema se versiona con migraciones de EF Core, una por sprint, y cada una tiene su script SQL en [database/](database/):

| Script | Contenido |
|---|---|
| [sprint1_hu01-hu05.sql](database/sprint1_hu01-hu05.sql) | HU-01 a HU-05: carreras, usuarios, códigos de verificación, sesiones, vehículos, puntos de encuentro, viajes y paradas |
| [sprint2_reservas_calificaciones.sql](database/sprint2_reservas_calificaciones.sql) | Reservas, calificaciones, notificaciones y suscripciones push |
| [sprint3_historial_ediciones.sql](database/sprint3_historial_ediciones.sql) | HU-09: tabla `historial_ediciones_viaje`, columna `viajes.numero_ediciones` y restricción `ck_viajes_numero_ediciones` |

Para crear la base en Neon, ejecuta `sprint1_hu01-hu05.sql` en el SQL Editor. La API aplica sola las migraciones siguientes si arranca con `Database__AplicarMigracionesAlIniciar=true`. Los scripts no se editan a mano: se cambia el modelo, se crea una migración y se regenera el script con `dotnet ef migrations script`.

### Comandos de migraciones (resumen)

Guía completa en [docs/versionamiento-bd.md](docs/versionamiento-bd.md). Requiere `dotnet-ef` y la variable
`ConnectionStrings__DefaultConnection` apuntando a la base **local**.

```bash
dotnet tool install --global dotnet-ef --version 9.0.*
dotnet ef migrations list   -p src/KARider.Infrastructure -s src/KARider.API          # aplicadas y (Pending)
dotnet ef database update   -p src/KARider.Infrastructure -s src/KARider.API          # aplicar todas
dotnet ef database update Sprint3_NumeroEdicionesViaje -p src/KARider.Infrastructure -s src/KARider.API  # revertir la última
dotnet ef database drop --force -p src/KARider.Infrastructure -s src/KARider.API      # base limpia (solo local)
```

## Calidad de código (SonarQube Cloud)

Cada push a `develop`/`main` y cada Pull Request ejecutan [sonarcloud.yml](.github/workflows/sonarcloud.yml):
compila, corre las pruebas con cobertura y publica el análisis en SonarQube Cloud. Necesita el secret
`SONAR_TOKEN`; la configuración paso a paso está en [docs/calidad-codigo.md](docs/calidad-codigo.md).

## Convenciones

- **Ramas:** `main` (producción, se despliega automáticamente), `develop` (integración), `feature/*`, `fix/*`. Todo entra mediante un PR con CI en verde.
- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/es/) (`feat:`, `fix:`, `chore:`, `docs:`…).
- **API:** versionada en `/api/v1`, JSON en camelCase y enums como texto (`"Conductor"`, `"Programado"`).
- **Errores:** siempre en formato Problem Details con un `code` estable; ver [docs/errores.md](docs/errores.md).
- **Nuevas reglas de negocio:** van en Domain y devuelven `Result`. Cada error nuevo se agrega a `DomainErrors` y a `docs/errores.md`.

## Despliegue

Ver [docs/despliegue-azure.md](docs/despliegue-azure.md). Resumen: App Service Linux (.NET 9) más PostgreSQL 15 externo, despliegue con GitHub Actions y OIDC, y verificación automática de `/health/ready`.

## Equipo

Kazumi Barrera Avila · Arleth Cruz Hernández · Rocío Daniela García Jiménez. Grupo 10-IDGS-G3, UTTT. Docente: Juan Carlos Benítez Reyes.
