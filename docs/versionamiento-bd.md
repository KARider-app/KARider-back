# Versionamiento de la base de datos (EF Core Migrations)

KARider versiona el esquema de PostgreSQL con **migraciones de Entity Framework Core 9**
(proveedor Npgsql). Cada cambio del modelo genera una clase de migración con `Up()` (aplicar) y
`Down()` (revertir), y EF Core registra lo aplicado en la tabla `__ef_migrations_history`.

> ⚠️ Las pruebas de reversión se hacen **solo en la base local o de pruebas**, nunca en Neon (producción).

## Migraciones del Sprint 3 (HU-09 · editar viaje publicado)

| # | Migración | Tipo de cambio | Efecto |
|---|---|---|---|
| 1 | `20261023200000_Sprint3_HistorialEdicionesViaje` | Creación de tabla | Tabla `historial_ediciones_viaje` (valores anteriores y nuevos de cada edición) con FK a `viajes` e índice `(viaje_id, creado_en)` |
| 2 | `20261023200100_Sprint3_NumeroEdicionesViaje` | Adición de columna | Columna `viajes.numero_ediciones integer NOT NULL DEFAULT 0` |
| 3 | `20261023200200_Sprint3_RestriccionNumeroEdiciones` | Restricción | `CHECK ck_viajes_numero_ediciones (numero_ediciones >= 0)` |

Archivos: `src/KARider.Infrastructure/Persistence/Migrations/` y el script equivalente
[`database/sprint3_historial_ediciones.sql`](../database/sprint3_historial_ediciones.sql).

## 1. Instalación (una sola vez)

```bash
dotnet tool install --global dotnet-ef --version 9.0.*   # o: dotnet tool update --global dotnet-ef
dotnet ef --version
```

Base local con Docker (usa los valores de tu `.env`):

```bash
docker compose up -d db          # PostgreSQL 15 en localhost:5433
```

`dotnet ef` lee la cadena de conexión de la variable `ConnectionStrings__DefaultConnection`
(ver `DesignTimeDbContextFactory`). Defínela **solo en tu terminal**, nunca en el repositorio:

```powershell
# PowerShell (Windows)
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=karider;Username=karider;Password=<tu_password>"
```

```bash
# bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=karider;Username=karider;Password=<tu_password>"
```

Para no repetir `-p` y `-s` en cada comando, todos los ejemplos usan:

```bash
-p src/KARider.Infrastructure -s src/KARider.API
```

## 2. Ver migraciones aplicadas y pendientes

```bash
dotnet ef migrations list -p src/KARider.Infrastructure -s src/KARider.API
```

Las que aún no están en la base aparecen con **`(Pending)`**. También se puede consultar en SQL:

```sql
SELECT migration_id, product_version FROM __ef_migrations_history ORDER BY migration_id;
```

## 3. Aplicar migraciones

```bash
dotnet ef database update -p src/KARider.Infrastructure -s src/KARider.API
```

## 4. Revertir un cambio (rollback nativo)

`database update` con el **nombre de una migración anterior** ejecuta el `Down()` de las posteriores.
Ejemplo: revertir la restricción CHECK (migración 3) dejando aplicadas la 1 y la 2:

```bash
dotnet ef database update Sprint3_NumeroEdicionesViaje -p src/KARider.Infrastructure -s src/KARider.API
dotnet ef migrations list -p src/KARider.Infrastructure -s src/KARider.API   # la 3 vuelve a (Pending)
```

Comprobación en SQL (la restricción ya no existe):

```sql
SELECT conname FROM pg_constraint WHERE conname = 'ck_viajes_numero_ediciones';
```

Alternativa sin SDK de .NET: [`database/rollback/sprint3_revertir_restriccion_numero_ediciones.sql`](../database/rollback/sprint3_revertir_restriccion_numero_ediciones.sql).

## 5. Reaplicar el cambio

```bash
dotnet ef database update -p src/KARider.Infrastructure -s src/KARider.API
dotnet ef migrations list -p src/KARider.Infrastructure -s src/KARider.API   # ya no hay (Pending)
```

## 6. Todas las migraciones desde una base limpia

```bash
dotnet ef database drop --force -p src/KARider.Infrastructure -s src/KARider.API   # solo en local
dotnet ef database update -p src/KARider.Infrastructure -s src/KARider.API
```

O con Docker, borrando también el volumen: `docker compose down -v && docker compose up -d db`.

## 7. Crear una migración nueva

1. Cambia el modelo (entidad en `KARider.Domain` y su configuración en `Persistence/Configurations`).
2. Genera la migración y revisa el código de `Up()` y `Down()`:

   ```bash
   dotnet ef migrations add <NombreDescriptivo> -p src/KARider.Infrastructure -s src/KARider.API -o Persistence/Migrations
   ```

3. Genera el script SQL para Neon:

   ```bash
   dotnet ef migrations script <MigracionAnterior> -p src/KARider.Infrastructure -s src/KARider.API -o database/<archivo>.sql
   ```

4. Si la migración no se ha subido, se puede quitar con `dotnet ef migrations remove`.

## 8. Integración con el pipeline

- **Demostración** (`migraciones-bd.yml`, ejecución manual desde *Actions › Migraciones de BD (demostración) › Run workflow*):
  crea un PostgreSQL 15 desechable y ejecuta, paso por paso, `migrations list`, `database update`, el rollback a
  `Sprint3_NumeroEdicionesViaje`, la reaplicación y la ejecución completa desde una base limpia. El registro de
  cada paso queda en la ejecución del workflow como evidencia.

- **CI** (`ci.yml`): `dotnet ef migrations has-pending-model-changes` falla si alguien cambia el modelo sin crear la migración.
- **Deploy** (`deploy-azure.yml`): genera un *bundle* (`dotnet ef migrations bundle`) y, si la variable
  `MIGRAR_EN_CI=true`, lo ejecuta contra la base con el secret `DATABASE_CONNECTION_STRING`.
