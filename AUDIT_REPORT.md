# Auditoría técnica · KARider (Backend + Frontend)

**Fecha:** 13 de noviembre de 2026
**Alcance:** `aric92045-blip/KARider-back` (.NET 9 · PostgreSQL) y `aric92045-blip/KARider-front` (Vue 3 · Vite · PWA), arquitectura, GitFlow, GitHub, CI/CD, SonarCloud, seguridad e integración.
**Método:** se leyó el código y se ejecutaron las herramientas de cada stack. La configuración real de GitHub y SonarCloud se consultó por API, con permisos de colaborador *write*, no de administrador. Todas las correcciones se validaron en local con los mismos pasos del CI.

> Este archivo es idéntico en los dos repositorios.
>
> **Convenciones:**
> - **[verificado]**: comprobado con una herramienta o una petición real.
> - **[no verificado]**: no se pudo comprobar por falta de permisos o acceso; se explica por qué.

---

## Actualización · configuración aplicada en GitHub (13-nov, después del traslado a `KARider-app`)

Los repositorios se trasladaron de `aric92045-blip/…` a la organización **`KARider-app`**. Las referencias `aric92045-blip/<repo>` de este documento corresponden al nombre anterior; GitHub redirige al nuevo.

### Aplicado y verificado por API [verificado]

| Configuración | KARider-back | KARider-front |
|---|---|---|
| Ruleset **"Producción (main y release)"** (`main`, `release`, `release/**`): PR obligatorio, 1 aprobación de Code Owner, descarta aprobaciones obsoletas, exige aprobar el último push y resolver conversaciones; checks obligatorios con rama al día; prohíbe force push y borrado; solo *merge commit*; **sin bypass** | ✅ | ✅ |
| Ruleset **"Integración (develop)"**: PR obligatorio, 1 aprobación, descarta aprobaciones obsoletas, conversaciones resueltas, checks obligatorios, sin force push ni borrado, sin bypass | ✅ | ✅ |
| Checks obligatorios | `Compilar y probar`, `Análisis SonarQube Cloud` | `calidad`, `Análisis SonarQube Cloud` |
| Merge solo con *merge commit*, ramas conservadas al fusionar, botón *Update branch* | ✅ | ✅ |
| Dependabot alerts y security updates | ✅ | ✅ |
| Secret scanning y push protection | ✅ | ✅ |
| Reporte privado de vulnerabilidades | ✅ | ✅ |
| `GITHUB_TOKEN` de solo lectura; las Actions no pueden aprobar PR | ✅ | ✅ |
| Environment **`production`**: aprobación de `aric92045-blip` o `tanis25`, solo desde `main` | ✅ (creado; ver H3) | ✅ |

Efecto comprobado: los PR #31 a #34 pasaron a **`REVIEW_REQUIRED`** y no se pueden fusionar sin la aprobación de otra persona del equipo.

### Hallazgos nuevos por el traslado

| # | Criticidad | Hallazgo | Estado |
|---|---|---|---|
| T1 | **Alto** | **La app de SonarCloud no está instalada en `KARider-app`.** El check "SonarCloud Code Analysis" dejó de publicarse y el Quality Gate ya no bloqueaba nada: el PR #34 tenía el Quality Gate en **ERROR** y GitHub lo mostraba en verde. [verificado] | **Corregido:** `sonar.qualitygate.wait=true` en los dos repos. El job "Análisis SonarQube Cloud" falla si el Quality Gate falla y es un check obligatorio. **Pendiente:** instalar la app de SonarCloud en la organización (*sonarcloud.io › My Organizations › Administration › Organization settings › GitHub*) o importar `KARider-app` como organización nueva de SonarCloud. |
| T2 | **Alto** | **Probable fallo del despliegue del backend.** El inicio de sesión OIDC en Azure valida el *subject* `repo:<dueño>/KARider-back:…`, que ahora es `repo:KARider-app/KARider-back:ref:refs/heads/main`. La credencial federada creada por el portal de Azure apunta al dueño anterior. El último despliegue exitoso (07:13) fue **antes** del traslado (07:35). [no verificado: no ha habido despliegues después] | **Pendiente:** en Azure (Entra ID › App registrations › *la app del despliegue* › Certificates & secrets › Federated credentials), agregar: organización `KARider-app`, repositorio `KARider-back`, entidad *Branch* `main`. Cuando se use el environment, agregar también la entidad *Environment* `production`. |
| T3 | Medio | `npx oxlint/eslint/prettier` en el CI podía descargar paquetes de internet (SonarCloud `githubactions:S6505`/`S8543`, motivo del ERROR del PR #34). | **Corregido:** scripts `npm run lint:check` y `npm run format:check`. |
| T4 | Medio | Se siguen haciendo pushes directos a `main` (p. ej. `8adf830`, 07:13, "nombre completo exactamente 40 caracteres"). | **Corregido** con los rulesets: ya no es posible. |
| T5 | Bajo | Los PR de Dependabot no reciben `SONAR_TOKEN` (GitHub separa los secretos de Dependabot), así que su análisis de Sonar se omite. | **Pendiente:** *Settings › Secrets and variables › Dependabot* › agregar `SONAR_TOKEN`. |
| T6 | Info | La organización `KARider-app` no tiene configuraciones propias revisables con esta cuenta: `tanis25` aparece como miembro *pending*. | **Pendiente (dueña de la org):** exigir 2FA y permiso base *Read* en *Organization › Settings*. |

### Siguientes pasos para el equipo

1. Aprobar y fusionar los PR, en orden:
   - back #31 → `main` (hotfix)
   - back #32 → `develop`
   - front #33 → `develop`
   - front #34 → `develop`

   Ya se pidió revisión a `aric92045-blip` y `17301662-RDGJ`.
2. Completar T2, la credencial federada de Azure, **antes** de la siguiente promoción del backend a `main`.
3. Front: PR `release → main` (sincronizar) y después `develop → main`. El despliegue pedirá aprobación en el environment `production`.

---

## A. Resumen ejecutivo

| | Estado | Nota |
|---|---|---|
| **Backend** | **Bueno** | Clean Architecture bien separada (API / Application / Domain / Infrastructure), validación con FluentValidation, Result pattern, JWT con validación al arrancar, rate limiting, CORS por lista blanca, HSTS y cabeceras de seguridad, migraciones versionadas con prueba de reversión en CI. 0 paquetes vulnerables. Riesgos: **.NET 9 fuera de soporte**, despliegue duplicado sin pruebas y Swagger público en producción. |
| **Frontend** | **Aceptable → Bueno tras las correcciones** | Organización por *features* (api / application / domain / ui), PWA con CSP estricta, 189 pruebas y 83 % de cobertura. Se encontró y corrigió una **caída del servidor de producción con una sola petición** (DoS) y 2 dependencias de producción con vulnerabilidades altas. |
| **Ecosistema** | **Débil en proceso, fuerte en código** | El código es de buena calidad, pero **ninguna rama está protegida**, los últimos 30 PR de cada repo se fusionaron **sin aprobación**, hay **commits directos a `main` y `develop`** y los dos repos **despliegan desde ramas distintas** (back desde `main`, front desde `release`). |

### Principales riesgos

1. **Crítico, corregido en rama.** Front: `GET /%00` tumbaba el servidor de producción.
2. **Crítico, corregido en rama.** Back: dos workflows despliegan a producción en paralelo y uno **no ejecuta pruebas**.
3. **Alto, pendiente externo.** Sin protección de ramas: cualquier colaborador puede hacer push directo a `main`, que es producción.
4. **Alto, corregido en rama.** `workflow_dispatch` permitía desplegar a producción **cualquier rama**.
5. **Alto, pendiente.** .NET 9 (STS) fuera de soporte; hay que migrar a .NET 10 LTS.
6. **Alto, corregido en rama.** `zod` y `follow-redirects` (vía axios) vulnerables en el bundle de producción.

---

## B. Diagnóstico técnico

| # | Criticidad | Repo | Archivo(s) | Problema | Solución |
|---|---|---|---|---|---|
| B1 | **Crítico** | front | `deploy/server.js` | Una petición con byte nulo (`/%00`) hacía que `fs.stat` lanzara `ERR_INVALID_ARG_VALUE` sin control y **el proceso terminaba**. Cualquiera podía dejar el sitio caído repitiéndola. **[verificado]** con el servidor levantado en local, no contra producción. | **Aplicada** (`bugfix/…`): al arrancar se indexan los archivos de `dist/`, y la ruta del navegador solo se usa como clave del índice, nunca toca el sistema de archivos. Lógica en `deploy/manejador.js` con 16 pruebas. |
| B2 | **Crítico** | back | `.github/workflows/main_karider-back.yml` (solo en `main`) | Workflow generado por el portal de Azure. Despliega en cada push a `main` **en paralelo** con `deploy-azure.yml`, **sin pruebas**, sin prueba de humo y sin control de concurrencia. Si las pruebas fallan, `deploy-azure` se detiene pero este publica igual. `deploy-azure.yml` ya decía que lo reemplazaba. **[verificado]**: cada push a `main` muestra dos ejecuciones de despliegue. | **Aplicada** (`hotfix/eliminar-workflow-duplicado-azure`, sale de `main`): se elimina el archivo. |
| B3 | **Alto** | ambos | `deploy-azure.yml`, `deploy.yml` | `workflow_dispatch` permite ejecutar el despliegue **eligiendo cualquier rama**, y cualquier colaborador *write* puede publicar código no revisado. | **Aplicada**: los jobs de despliegue solo corren con `github.ref == 'refs/heads/main'`. |
| B4 | **Alto** | front | `package-lock.json` | Vulnerabilidades altas en dependencias de producción: `zod` 4.6.5 (agotamiento de memoria) y `follow-redirects` 1.16.1 vía `axios` (reenvío de credenciales en redirecciones). **[verificado]** con `npm audit --omit=dev`. | **Aplicada**: `zod` pasa a 4.6.7 y `axios` a 1.21.1 (`follow-redirects` 1.16.3). Ahora `npm audit --omit=dev` da **0**. |
| B5 | **Alto** | back | `Directory.Build.props`, `global.json` | `net9.0` es STS. Según la política de soporte de Microsoft (24 meses para STS), su soporte terminó el 10 de noviembre de 2026; **conviene confirmar la fecha** en la página oficial de *.NET support policy*. Dejará de recibir parches de seguridad. | **Recomendada**: migrar a .NET 10 LTS en una rama `feature/` dedicada: TargetFramework, paquetes `Microsoft.*`, `Npgsql.*` y `EFCore.*` a la versión 10, y probar las migraciones. Dependabot quedó configurado para no proponer saltos de versión mayor de estos paquetes por separado. |
| B6 | **Medio** | back | Configuración de Azure (`Swagger__Habilitado`) | **Swagger UI y `/openapi/v1.json` son públicos en producción.** **[verificado]**: responden HTTP 200. No expone secretos, pero sí todo el mapa de la API. | **Pendiente externo (H8)**: `Swagger__Habilitado=false` en el App Service, o protegerlo. |
| B7 | **Medio** | front | `src/core/auth/session.ts` | El refresh token se guarda en `localStorage`/`sessionStorage`, al alcance de JavaScript. Lo mitigan la CSP `script-src 'self'` y la rotación de tokens en `/auth/refresh`. | **Recomendada**: refresh token en cookie `HttpOnly; Secure; SameSite=Strict` emitida por el backend. Cambia el contrato de `/auth`, así que debe hacerse en los dos repos a la vez. |
| B8 | **Medio** | front | `.github/workflows/ci.yml` | Sin bloque `permissions` (el `GITHUB_TOKEN` hereda el permiso por defecto del repositorio), sin `concurrency`, lint con `--fix` (en CI corrige en lugar de verificar), sin auditoría de dependencias y sin verificación de formato. | **Aplicada**: `permissions: contents: read`, concurrencia, `npm audit --omit=dev --audit-level=high`, `oxlint`/`eslint --max-warnings=0` sin `--fix`, `prettier --check` y `vue-tsc`. |
| B9 | **Medio** | back | `.github/workflows/ci.yml` | No había lint ni verificación de formato aunque existe `.editorconfig`. | **Aplicada**: `dotnet format whitespace` y `dotnet format style --severity warn` con `--verify-no-changes`. |
| B10 | **Medio** | back | (falta `.gitattributes`) | En Windows `core.autocrlf=true` convierte los archivos a CRLF y `dotnet format` marcaba los 121 archivos. El repositorio sí guarda LF. | **Aplicada**: `.gitattributes` (`* text=auto eol=lf`, `*.ps1 eol=crlf`). También se corrigió la sangría real de 3 archivos. |
| B11 | **Medio** | ambos | `.github/` | Faltaban plantillas de PR e issues, CODEOWNERS, Dependabot y documentación del flujo de ramas. | **Aplicada**: `pull_request_template.md`, `ISSUE_TEMPLATE/` (error e historia de usuario), `CODEOWNERS`, `dependabot.yml` y `docs/flujo-ramas.md`. |
| B12 | **Medio** | ecosistema | `deploy.yml` (front) y `deploy-azure.yml` (back) | Los repos despliegan desde ramas distintas: front desde `release`, back desde `main`. En el front, `main` está **21 commits atrás** de `develop` y no representa producción. | **Aplicada**, con transición (ver C3): el front también despliega desde `main`. |
| B13 | **Bajo** | back | `SonarCloud csharpsquid:S8733` en `ViajeService.ObtenerDetalleAsync` | Sonar avisa de una posible "explosión cartesiana" al incluir `Paradas` y `Reservas`. **Falso positivo**: Infrastructure configura `QuerySplittingBehavior.SplitQuery` de forma global. Agregar `.AsSplitQuery()` obligaría a que Application dependa de `EntityFrameworkCore.Relational`, lo que rompe la separación de capas. Se probó y se revirtió. | **Pendiente externo (H7)**: marcarlo como *False positive* en SonarCloud. |
| B14 | **Bajo** | front | `SonarCloud Web:S6840` en `FormDatosPersonales.vue` | `autocomplete="name"` es un valor válido; Sonar no lo reconoce porque pasa por el componente `BaseInput`. **Falso positivo.** | **Pendiente externo (H7)**: marcarlo como *False positive*. |
| B15 | **Bajo** | back | `ApiServiceExtensions.cs` (`SigningKey.PadRight(32)`) | Rellenar la llave JWT sería peligroso, pero nunca ocurre: `JwtOptions` exige `MinLength(32)` con `ValidateOnStart`, así que la API no arranca con una llave corta. **[verificado]** en el código. | Recomendada: quitar `PadRight` para que el código no sugiera un comportamiento inseguro. |
| B16 | **Bajo** | back | `src/KARider.API/KARider.API.http` | Contiene una contraseña de ejemplo (`Karider#2026`) para pruebas locales. | Recomendada: confirmar que no es la contraseña de ninguna cuenta real; si lo es, cambiarla. |
| B17 | **Bajo** | front | `public/staticwebapp.config.json` | Configuración de Azure Static Web Apps; el front se publica en **App Service** (`deploy/server.js`). Duplica la CSP y puede desincronizarse. | Recomendada: eliminarla o dejar documentado que no se usa. |
| B18 | **Bajo** | front | dependencias de desarrollo | 26 vulnerabilidades en herramientas de desarrollo (1 alta en `@vitest/mocker` ≤ 4.1.11). **No llegan al navegador.** `npm install` falló en esta máquina por un error interno de npm (`edgesOut`). | **Pendiente**: Dependabot propondrá `vitest` 4.1.12. |
| B19 | Info | back | cobertura total 50.6 % | El Quality Gate (código nuevo) pasa, pero los controladores y Infrastructure tienen poca cobertura. | Recomendada: pruebas de integración con `WebApplicationFactory`; `Program` ya está expuesto para eso. |
| B20 | Info | ecosistema | Azure Communication Services (correo) | El correo de verificación usa el dominio de prueba de ACS (`…azurecomm.net`), que tiene cuotas muy bajas y mala entregabilidad. Algunos usuarios no reciben el código. | **Pendiente externo (H9).** |

### Aspectos revisados sin hallazgos relevantes

- **Validación en el servidor:** FluentValidation en todos los *requests*. El front repite las validaciones con `zod` solo como ayuda al usuario.
- **Inyección SQL:** EF Core parametrizado y sin SQL crudo con entrada del usuario.
- **Contraseñas:** `PasswordHasher` con *pepper* y bloqueo tras intentos fallidos.
- **Secretos:** en el árbol actual y en el historial no hay secretos reales versionados; `.env` está ignorado y solo se versiona `.env.example`. **[verificado]** con búsqueda en `git log -p`.
- **Integración back ↔ front:**
  - Los enums viajan como texto en los dos lados.
  - Los errores usan ProblemDetails con `code`, que el front interpreta.
  - La URL de la API está en `VITE_API_BASE_URL` y en la CSP `connect-src`.
  - Los contratos de las HU recientes (`/ruta`, `/llegada`, `/perfil/historial`, `/credenciales`) coinciden en los tipos de los dos repos.

---

## C. Auditoría Git y GitHub

### C1. Ramas [verificado]

*Sin integrar* = commits que no están en `develop`. Todas las ramas *feature* tienen **0**: su trabajo ya está integrado.

**KARider-back**

| Rama | Sin integrar | Último commit | Estado / recomendación |
|---|---|---|---|
| `main` | 17 (2 commits directos de CI + merges de promoción) | 13-nov | Producción. Las diferencias son merges de promoción y dos correcciones de CI equivalentes a las de `develop`. |
| `develop` | 1 commit no está en `main` (`237be0d`) | 13-nov | Integración. |
| `release` | 6 (merges de promoción) | 13-nov | Rama permanente intermedia; no es GitFlow estándar. Retirarla cuando haya protección de ramas (ver C3). |
| `Sprint1`, `Sprint-2` | 1 merge cada una | oct | **Contenido ya integrado** (ambos padres del merge están en `develop`). Obsoletas. |
| `Arleth-Sprint1-HU-04…`, `Arleth-Sprint1-HU-05…`, `HU-09-editar-viaje-backend`, `Rocio-Sprint2-HU-06-07-08-ReservarViaje`, `feature/Sprint3-*` (3), `feature/kazumi-*` (5) | 0 | oct–nov | Integradas. Se **conservan** por decisión del equipo, como evidencia del trabajo por HU. |

**KARider-front**

| Rama | Sin integrar | Último commit | Estado / recomendación |
|---|---|---|---|
| `main` | 0, pero **21 commits atrás** de `develop` | 12-nov | **No representa producción.** Hay que sincronizarla antes de cambiar el despliegue a `main` (C3). |
| `develop` | — | 13-nov | Integración. |
| `release` | 6 (merges de promoción) | 13-nov | Origen actual de producción. Retirarla tras la transición. |
| `Sprint1` (2), `Sprint-2` (1) | merges | oct | **Contenido ya integrado.** Obsoletas. |
| `Arleth_*` (2), `HU-09-editar-viaje`, `Rocio_*`, `design/rebrand-mockups`, `docs/historias-usuario`, `feature/Sprint3-*` (3), `feature/kazumi-*` (8) | 0 | oct–nov | Integradas. Se conservan. |

**No se eliminó ninguna rama.** Ninguna tiene trabajo exclusivo, así que eliminar cualquiera sería seguro. Aun así, el equipo decidió conservarlas como evidencia por HU; si en algún momento se quieren limpiar, se pueden convertir en etiquetas `archivo/<rama>` antes de borrarlas.

### C2. Hallazgos de proceso [verificado]

- **Protección de ramas:** `main`, `develop` y `release` tienen `protected=false` en los dos repos, y no hay *rulesets*.
- **Revisiones:** los últimos **30 de 30** PR fusionados de cada repo **no tenían aprobación**, incluidos los de esta auditoría y los de las HU-08/10/12.
- **Commits directos sin PR** (desde el 1 de octubre):

  | Repo | `develop` | `main` |
  |---|---|---|
  | back | 6 | 5 (además del merge `450418c`, hecho el 13-nov) |
  | front | 13 (incluida la HU-11 completa) | 11 |

  Esos cambios no pasaron por CI antes de integrarse.
- **Nombres de rama inconsistentes:** `Arleth_Sprint1_…`, `Arleth-Sprint1-…`, `HU-09-…`, `Sprint-2`, `Sprint1`, `feature/kazumi-…`. Se documenta la convención en `docs/flujo-ramas.md`.
- **Opciones de merge:** se permiten *merge commit*, *squash* y *rebase*; `delete_branch_on_merge=false`, que coincide con la decisión del equipo.

### C3. Estrategia GitFlow recomendada (documentada en `docs/flujo-ramas.md`)

```
feature/* ─┐
bugfix/*  ─┼─PR─► develop ──PR (lanzamiento, opcional release/x.y)──► main ──► producción (solo desde main)
           │                                                          ▲  │
hotfix/* ──┴──────────────────────────────────────PR─────────────────┘  └─PR─► develop (sincronizar)
```

**Transición del front de `release` a `main`:**
1. Fusionar los PR de esta auditoría en `develop`.
2. Abrir el PR `release → main` para que `main` tenga exactamente lo que está en producción.
3. Desde ese momento las promociones son `develop → main`. La rama `release` deja de desplegar en cuanto recibe el nuevo `deploy.yml`, y después se puede archivar.

En el backend basta con dejar de usar `release` como paso intermedio y promover `develop → main`.

---

## D. Auditoría CI/CD

### D1. Workflows existentes

| Repo | Workflow | Disparadores | Qué hace | Hallazgos |
|---|---|---|---|---|
| back | `ci.yml` | PR a develop/main, push develop | restore, build Release, pruebas con cobertura, `has-pending-model-changes`, NuGet vulnerable | Bien. Faltaba lint (**agregado**). |
| back | `sonarcloud.yml` | push develop, PR | dotnet-sonarscanner con cobertura OpenCover | Bien. Excluye las migraciones generadas. |
| back | `migraciones-bd.yml` | manual, PR que cambian migraciones | Aplica, revierte y reaplica todas las migraciones en un PostgreSQL 15 efímero | Muy bien: prueba real de reversión. |
| back | `deploy-azure.yml` | push main, manual | Pruebas, publicación, bundle de migraciones, OIDC, despliegue y prueba de humo `/health/ready` | Bien. Se podía ejecutar desde cualquier rama (**corregido**). No usa *environment* (H3). |
| back | `main_karider-back.yml` (solo en `main`) | push main, manual | Compila y despliega **sin pruebas** | **Duplicado (B2), eliminado en el hotfix.** |
| front | `ci.yml` | PR a develop/main, push develop | lint `--fix`, pruebas, build | Faltaban permisos, auditoría, formato y lint en modo verificación (**corregido**). |
| front | `sonarcloud.yml` | push main/develop, PR | Pruebas con cobertura y scan-action fijado por SHA | El push a `main` siempre queda "not analyzed" (**alineado con back**: solo `develop`). |
| front | `deploy.yml` | push **release**, manual | Pruebas, build con `VITE_*`, copia el servidor y despliega con *publish profile* | Despliegue desde `release` (**cambiado a `main`**), sin protección de rama (**corregido**), sin *environment* (**agregado `production`**). |

### D2. Flujo resultante

| Evento | Backend | Frontend |
|---|---|---|
| PR → `develop` | lint (dotnet format), build, pruebas, migraciones pendientes, NuGet vulnerable, SonarCloud (+ migraciones en PostgreSQL si cambian) | `npm audit` (producción), oxlint, eslint, prettier, vue-tsc, pruebas, build, SonarCloud |
| PR → `main` | lo mismo; aprobación requerida tras H1 | lo mismo; aprobación requerida tras H1 |
| push → `main` | `deploy-azure.yml`: pruebas → bundle → (migraciones) → despliegue → prueba de humo | `deploy.yml`: auditoría → pruebas → build → despliegue (environment `production`) |

### D3. Pendientes de CI/CD

- **Checks requeridos para fusionar:** hoy solo informan, no bloquean (H1).
- **Backend sin *environment*:** usar `environment: production` exige antes una credencial federada nueva en Azure (H3).
- **Credencial del frontend:** usa un *publish profile*, que es una credencial de larga duración. Se recomienda migrar a OIDC como el backend (H4).
- **Acciones de terceros:** `azure/*` y `SonarSource/*` ya están fijadas por SHA. Las de `actions/*` (oficiales de GitHub) usan la etiqueta `@v4`, que es aceptable; Dependabot (`github-actions`) las mantendrá al día.

---

## E. Auditoría SonarCloud

### E1. Configuración [verificado]

| | Backend | Frontend |
|---|---|---|
| Proyecto / organización | `aric92045-blip_KARider-back` / `aric92045-blip` ✔ | `aric92045-blip_KARider-front` / `aric92045-blip` ✔ |
| Configuración | Argumentos de `dotnet sonarscanner begin` en el workflow | `sonar-project.properties` |
| Exclusiones | `**/Persistence/Migrations/**` (código generado) ✔ | `src/**/__tests__/**`, `*.spec.ts` ✔ |
| Fuentes analizadas | Solución completa | `src`, `deploy`, `.github` ✔ |
| Cobertura | OpenCover (`coverlet`) ✔ | LCOV (Vitest). **`deploy/` no se medía y ahora sí**, así que deja de contarse como 0 % |
| Análisis en PR | ✔ (decoración en el PR: "SonarCloud Code Analysis") | ✔ |
| Rama principal | `develop` (OK, 13-nov) | `develop` (OK, 13-nov) |
| Rama `main` | Sin analizar | Sin analizar |

### E2. Métricas de `develop` (antes de estas correcciones) [verificado vía API pública]

| Métrica | Backend | Frontend |
|---|---|---|
| Quality Gate | **OK** | **OK** |
| Bugs | 1 (S8733, falso positivo B13) → Reliability **C** | 0 → A |
| Vulnerabilidades | 0 → Security **A** | **3 BLOCKER** (S2083 en `deploy/server.js`) → Security **E** |
| Security Hotspots | 0 | 0 |
| Code Smells | 6 (informativos) | 1 (falso positivo B14) |
| Cobertura (total) | 50.6 % | 82.6 % |
| Duplicación | 0.3 % | 0.4 % |
| Maintainability | A | A |

El Quality Gate solo mira el **código nuevo**. Por eso estaba en OK aunque el front tenía calificación de seguridad **E** por código antiguo.

Las 3 vulnerabilidades S2083 (*path traversal*) **no eran explotables**: se probaron 5 variantes de `../` y todas respondieron 404 [verificado]. Aun así, el rediseño de B1 elimina el flujo de datos que Sonar marcaba. Ahí apareció la vulnerabilidad real, la caída por byte nulo.

### E3. Pendientes [no verificado]

- No se pudo leer la definición del Quality Gate asignado: la API exige token de administrador. Se espera *Sonar way* (en código nuevo: cobertura ≥ 80 %, duplicación ≤ 3 %, calificaciones A y hotspots revisados). Hay que confirmarlo en *Project Settings › Quality Gate*.
- `main` aparece sin analizar. Para analizar producción, configurar `main` como rama de larga duración (*Administration › Branches and Pull Requests › Long-lived branches pattern*) y agregar `main` al `push` de los dos `sonarcloud.yml`.
- Marcar B13 y B14 como *False positive* en la interfaz de SonarCloud.

---

## F. Auditoría de seguridad

| Área | Backend | Frontend |
|---|---|---|
| Secretos en el repositorio | Ninguno real; `.env.example` sin valores ✔ | Ninguno; el bundle no incluye secretos del backend, solo `VITE_*` públicos ✔ |
| Autenticación | JWT HS256, issuer, audience y vida útil validados, *clock skew* de 30 s, refresh token rotado ✔ | Access token en memoria; refresh token en almacenamiento web (B7) |
| Autorización | Políticas por rol y verificación de propietario/participante en los servicios (p. ej. 403 en credenciales y llegada) ✔ | Guards del router solo para experiencia de usuario; el backend decide ✔ |
| Validación de entradas | FluentValidation en todos los *requests* ✔ | zod (ayuda al usuario) ✔ |
| Inyección | EF Core parametrizado ✔ | Sin `v-html` con datos de usuario; los tooltips del mapa usan nodos de texto ✔ |
| CORS | Lista blanca en producción y loopback solo en Development ✔ | — |
| Cabeceras | HSTS, `nosniff`, `X-Frame-Options`, CSP `default-src 'none'` [verificado] | CSP estricta, HSTS, `no-referrer`, `Permissions-Policy` [verificado] |
| Rate limiting | Global y `/auth` (10/min) por IP; `X-Forwarded-For` limitado al último proxy ✔ | — |
| Disponibilidad | — | **Caída por `%00` (B1), corregida** |
| Dependencias | 0 vulnerables ✔ | Producción: 0 tras la corrección (B4). Desarrollo: B18 |
| Exposición | **Swagger público** (B6) | — |
| Plataforma | **.NET 9 fuera de soporte** (B5) | Node 22 LTS ✔ |
| Logs | Sin datos sensibles en los mensajes revisados; los fallos de envío de correo solo registran el `UsuarioId` ✔ | — |

---

## G. Cambios realizados

Todos los cambios están en **ramas locales**, sin push y sin PR. Los commits van a nombre de Kazumi Barrera Avila.

### KARider-back

| Rama | Commit | Archivos | Motivo |
|---|---|---|---|
| `feature/kazumi-Sprint4-auditoria-devops` (sale de `develop`) | `style: fin de línea LF…` | `.gitattributes`, `HistorialEdicionViaje.cs`, `PerfilServiceTests.cs`, `RutaViajeServiceTests.cs` | B10 (solo espacios y saltos de línea) |
| | `ci: lint con dotnet format y despliegue solo desde main` | `ci.yml`, `deploy-azure.yml` | B3, B9 |
| | `docs: plantillas…` | `.github/{CODEOWNERS,dependabot.yml,pull_request_template.md,ISSUE_TEMPLATE/*}`, `docs/flujo-ramas.md` | B11 |
| | (este reporte) | `AUDIT_REPORT.md` | Entregable |
| `hotfix/eliminar-workflow-duplicado-azure` (sale de `main`) | `ci: eliminar el workflow de despliegue duplicado…` | `.github/workflows/main_karider-back.yml` (eliminado) | B2 |

### KARider-front

| Rama | Commit | Archivos | Motivo |
|---|---|---|---|
| `bugfix/kazumi-Sprint4-seguridad-servidor-y-dependencias` (sale de `develop`) | `fix(seguridad): el servidor del front se caía…` | `deploy/server.js`, `deploy/manejador.js` (nuevo), `deploy/manejador.d.ts`, `src/__tests__/servidorEstatico.spec.ts`, `vitest.config.ts`, `deploy.yml` (copia `manejador.js`) | B1 y alertas S2083 |
| | `fix(deps): actualizar axios y zod…` | `package-lock.json` | B4 |
| `feature/kazumi-Sprint4-auditoria-devops` (**apilada** sobre la anterior) | `ci: validar formato, tipos, lint…` | `ci.yml`, `sonarcloud.yml`, prettier en 3 archivos | B8 |
| | `ci: desplegar el front a producción solo desde main` | `deploy.yml` | B3, B12 |
| | `docs: plantillas…` | `.github/*`, `docs/flujo-ramas.md` | B11 |
| | (este reporte) | `AUDIT_REPORT.md` | Entregable |

### Pruebas ejecutadas y resultados [verificado]

**Backend**, ejecutado igual que el CI:

| Paso | Resultado |
|---|---|
| `dotnet format whitespace --verify-no-changes` | ✔ |
| `dotnet format style --verify-no-changes --severity warn` | ✔ |
| `dotnet build -c Release` | ✔ (0 errores, 0 advertencias) |
| `dotnet test` | ✔ **99/99** |
| `ef migrations has-pending-model-changes` | ✔ sin cambios pendientes |
| `dotnet list package --vulnerable` | ✔ 0 |
| Sintaxis de YAML | ✔ |

**Frontend**, ejecutado igual que el CI:

| Paso | Resultado |
|---|---|
| `npm audit --omit=dev --audit-level=high` | ✔ 0 |
| `oxlint` | ✔ |
| `eslint --max-warnings=0` | ✔ |
| `prettier --check src/ deploy/` | ✔ |
| `vue-tsc` | ✔ |
| `vitest` | ✔ **189/189** (16 nuevas del servidor), cobertura 83 % |
| `vite build` | ✔ |
| Sintaxis de YAML | ✔ |

**Limitaciones:**
- En esta máquina `npm ci` falló con `EPERM`: un proceso local (editor o Vitest) tenía bloqueado un binario de `rolldown`. Se restauró con `npm install`, sin cambios en el lockfile. En GitHub Actions `npm ci` corre en una máquina limpia.
- Los workflows modificados solo se pueden probar de verdad en GitHub: su sintaxis está validada, pero su ejecución real ocurrirá en los PR.

---

## H. Pendientes externos (requieren permisos de administrador u otros servicios)

La cuenta usada en esta auditoría es *write*. Las acciones H1 a H6 las debe hacer **`aric92045-blip`**, que es admin de los dos repos.

**H1. Protección de `main` y `develop`.** *Settings › Rules › Rulesets › New branch ruleset*, en cada repo:
- *Target:* `main` y `develop`. *Enforcement:* Active. *Bypass list:* vacía, para que nadie pueda saltarse las reglas.
- ✅ Restrict deletions · ✅ Block force pushes · ✅ Require linear history: **no** (el equipo usa *merge commit*).
- ✅ Require a pull request before merging:
  - 1 aprobación; para `develop` puede ser 1 o 0, según el tamaño del equipo.
  - ✅ Dismiss stale approvals · ✅ Require review from Code Owners · ✅ Require conversation resolution.
  - *Allowed merge methods:* Merge.
- ✅ Require status checks to pass (con ✅ *Require branches to be up to date*):
  - **back:** `Compilar y probar`, `Análisis SonarQube Cloud`, `SonarCloud Code Analysis`.
  - **front:** `calidad`, `Análisis SonarQube Cloud`, `SonarCloud Code Analysis`.

Alternativa por CLI con un token de administrador: `gh api -X POST repos/aric92045-blip/<repo>/rulesets --input regla.json`, usando un JSON con esas reglas.

**H2. Opciones del repositorio.**
- *Settings › General › Pull Requests:* dejar solo *Allow merge commits* y mantener *Automatically delete head branches* **desactivado**, por decisión del equipo.
- *Settings › Actions › General › Workflow permissions:* **Read repository contents** y desactivar *Allow GitHub Actions to create and approve pull requests*. No se pudo leer el valor actual: la API respondió 403.

**H3. Environment `production` (back).**
1. En Azure (Entra ID), en la App Registration del despliegue, agregar una *federated credential* con subject `repo:aric92045-blip/KARider-back:environment:production`.
2. Crear el environment con *Required reviewers* y *Deployment branches: main*.
3. Agregar `environment: production` al job `deploy` de `deploy-azure.yml`. El workflow ya tiene un comentario que lo indica.

**H4. Environment `production` (front).** El workflow ya lo usa: GitHub lo crea solo en la primera ejecución. Agregar *Required reviewers* y *Deployment branches: main*. Después, migrar el *publish profile* a OIDC y mover `AZURE_WEBAPP_PUBLISH_PROFILE_FRONT` a un *secret* del environment.

**H5. Secretos.** Eliminar `AZURE_WEBAPP_PUBLISH_PROFILE_BACK` del backend: **ningún workflow lo usa** [verificado]. No se tocó ningún secreto.

**H6. Seguridad del repositorio.** *Settings › Code security*:
- Activar *Dependabot alerts* (hoy están **desactivadas** [verificado]).
- Activar *Secret scanning* y *Push protection*.
- Activar *Private vulnerability reporting*.

**H7. SonarCloud.**
- Confirmar el Quality Gate (E3).
- Configurar `main` como rama de larga duración.
- Marcar B13 y B14 como *False positive*.
- Revisar que *Automatic Analysis* siga desactivado.

**H8. Azure App Service (back).**
- `Swagger__Habilitado=false` (B6).
- Revisar `Cors__AllowedOrigins__*`.
- Si se usa `MIGRAR_EN_CI`, definir la variable y el *secret* `DATABASE_CONNECTION_STRING`. Hoy las migraciones se aplican al arrancar la API (`Database__AplicarMigracionesAlIniciar`) [no verificado: no hay acceso a la configuración de Azure].

**H9. Correo (B20).** Conectar un dominio verificado en Azure Communication Services o usar otro proveedor SMTP. Además, **rotar la credencial SMTP** que quedó visible en una captura de pantalla.

**H10. Plan .NET 10 LTS (B5).** Una rama `feature/` dedicada con migración del framework y los paquetes, y pruebas completas, incluido `migraciones-bd.yml`.

**H11. Orden de fusión de los PR de esta auditoría**, una vez autorizados:
1. back `hotfix/eliminar-workflow-duplicado-azure` → `main`. El archivo no existe en `develop`, así que no requiere *back-merge*.
2. back `feature/kazumi-Sprint4-auditoria-devops` → `develop`.
3. front `bugfix/kazumi-Sprint4-seguridad-servidor-y-dependencias` → `develop`.
4. front `feature/kazumi-Sprint4-auditoria-devops` → `develop`. Va apilada sobre la anterior: tras fusionar la 3 su diff se reduce solo.
5. front `release → main` (sincronizar producción), y luego `develop → main` para desplegar la corrección B1 por el nuevo flujo.

---

## I. Estado final

| Dimensión | Backend | Frontend |
|---|---|---|
| Arquitectura y código | ★★★★★ | ★★★★☆ |
| Pruebas | ★★★☆☆ (99 pruebas, 50.6 % total) | ★★★★☆ (189 pruebas, 83 %) |
| Seguridad de la aplicación | ★★★★☆ (Swagger público y .NET 9) | ★★★★☆ tras B1 y B4 (refresh token en almacenamiento web) |
| CI/CD | ★★★★☆ tras los cambios | ★★★★☆ tras los cambios |
| Gobierno de GitHub | ★☆☆☆☆ **hasta aplicar H1–H6** | ★☆☆☆☆ **hasta aplicar H1–H6** |

**Conclusión.** El código de los dos repositorios está listo para producción en el contexto del proyecto, y con los cambios de esta auditoría lo están también las validaciones automáticas.

Lo que **todavía impide trabajar de forma profesional es el gobierno del repositorio**. Hoy cualquiera puede hacer push directo a `main`, que es producción, sin revisión ni checks. Por eso H1 (protección de ramas) es la acción de mayor impacto y menor esfuerzo.

Después siguen, en este orden:
1. Fusionar las correcciones de seguridad: B1 y B2.
2. Migrar a .NET 10.
3. Ocultar Swagger en producción.
