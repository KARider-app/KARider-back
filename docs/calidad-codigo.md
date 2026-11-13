# Calidad de código con SonarQube Cloud

El análisis estático se ejecuta automáticamente con **SonarQube Cloud** (antes SonarCloud), gratuito
para repositorios públicos. Cubre C# en este repositorio y TypeScript/Vue en `KARider-front`.

## Qué se analiza

- Bugs, vulnerabilidades, *code smells* y *security hotspots* con el perfil **Sonar way**.
- Cobertura de pruebas (xUnit + coverlet en formato OpenCover).
- Duplicación de código y deuda técnica.
- Se excluyen únicamente las migraciones de EF Core (`**/Persistence/Migrations/**`): son código generado
  por la herramienta y ya están marcadas como `generated_code = true` en `.editorconfig`.

## Instalación y configuración (una sola vez por repositorio)

1. Entra a <https://sonarcloud.io> con la cuenta de GitHub dueña del repositorio.
2. **+ › Analyze new project**, elige la organización de GitHub e importa `KARider-back`.
3. En el proyecto: **Administration › Analysis Method** y desactiva **Automatic Analysis**
   (el análisis lo hace GitHub Actions).
4. **My Account › Security**: genera un token.
5. En GitHub: **Settings › Secrets and variables › Actions › New repository secret**
   - Nombre: `SONAR_TOKEN` · Valor: el token. **Nunca se escribe en el código.**
6. Si la organización o la clave del proyecto no son `aric92045-blip` / `aric92045-blip_KARider-back`,
   crea las variables del repositorio `SONAR_ORGANIZATION` y `SONAR_PROJECT_KEY`.

## Ejecución

- **Automática:** el workflow [`.github/workflows/sonarcloud.yml`](../.github/workflows/sonarcloud.yml) corre en cada
  push a `develop`/`main` y en cada Pull Request. Compila, ejecuta las pruebas con cobertura y envía los resultados.
- **Manual:** GitHub › Actions › *Calidad de código (SonarQube Cloud)* › **Run workflow**.
- **Local (opcional):**

  ```bash
  dotnet tool install --global dotnet-sonarscanner
  dotnet sonarscanner begin /k:"aric92045-blip_KARider-back" /o:"aric92045-blip" /d:sonar.token="$SONAR_TOKEN" /d:sonar.host.url="https://sonarcloud.io"
  dotnet build KARider.sln
  dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
  ```

## Consultar resultados

En sonarcloud.io › proyecto **KARider-back**: *Overview* (Quality Gate y métricas), *Issues* (hallazgos),
*Security Hotspots*, *Measures* (cobertura, duplicación, deuda) y *Activity* (comparación entre análisis).
