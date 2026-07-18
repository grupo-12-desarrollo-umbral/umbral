# Integración continua local y verificación reproducible del backend

## Propósito

El backend tiene un único contrato de verificación para los equipos de
desarrollo y GitHub Actions. El flujo remoto invoca el mismo punto de entrada
del `backend/Makefile` que se usa localmente.

La separación de responsabilidades es intencional:

- `.github/workflows/backend-tests.yml` decide **cuándo y dónde** se ejecuta la
  integración continua.
- `backend/Makefile` define **qué significa verificar el backend**.
- `backend/scripts/cover-gate.sh` calcula la cobertura y aplica el umbral.
- `backend/global.json` fija el SDK de .NET para local y GitHub Actions.
- `backend/.config/dotnet-tools.json` fija la versión del generador de reportes.

## Requisitos previos

Instala estas herramientas en el equipo:

- GNU Make.
- El SDK exacto declarado en `backend/global.json` (`10.0.110`). El resolvedor
  no avanza automáticamente a otra banda de características.
- Docker con un daemon accesible. Las pruebas de integración usan
  Testcontainers para crear dependencias desechables.

No es necesario levantar el entorno persistente de Docker Compose antes de
ejecutar la integración continua local.

La primera ejecución necesita acceso a la red para restaurar paquetes NuGet y
ReportGenerator. Las siguientes ejecuciones reutilizan la caché normal de
NuGet.

## Verificar una carga de trabajo

Desde la raíz del monorepositorio:

```bash
make -C backend ci SVC=mission-design-service
```

Valores válidos para `SVC`:

- `users-service`
- `mission-design-service`
- `scoring-monitoring-service`
- `session-operations-service`
- `api-gateway`

Para un servicio de dominio, `ci` realiza estas operaciones:

1. Restaura las herramientas .NET fijadas por el repositorio.
2. Ejecuta los controles estructurales y de Arquitectura Limpia.
3. Compila la API y todos los proyectos de prueba descubiertos.
4. Ejecuta pruebas unitarias y de integración con Testcontainers y Coverlet.
5. Combina la cobertura de todos los proyectos de prueba.
6. Exige una cobertura agregada de ramas de al menos 95%.
7. Exige reportes legibles por máquinas y personas.

El API gateway usa el mismo punto de entrada, pero no tiene umbral de Coverlet.
Su prueba de autenticación ejecuta el gateway en otro contenedor, por lo que
Coverlet no puede medirla de forma representativa dentro del proceso.

## Verificar todo el backend

```bash
make -C backend ci-all
```

`ci-all` comprueba los servicios de dominio y el API gateway. Continúa después
de un fallo para producir todos los resultados y reportes posibles. Al final,
devuelve un código distinto de cero si alguna carga falló.

El código de salida es el veredicto. Un reporte favorable no invalida un fallo
de compilación, pruebas, arquitectura o cobertura.

## Reportes de cobertura

Cada servicio sujeto al gate escribe en:

```text
backend/coverage/<servicio>/
├── merged.cobertura.xml
├── Summary.txt
├── Summary.csv
└── index.html
```

- `merged.cobertura.xml` contiene el resultado legible por sistemas de
  integración continua y otras herramientas.
- `Summary.txt` y `Summary.csv` contienen resúmenes compactos.
- `index.html` permite revisar la cobertura línea por línea.

Los reportes se generan desde el mismo archivo Cobertura combinado sobre el
que se aplica el umbral de ramas. La cobertura de líneas se conserva como dato
de diagnóstico, pero no determina el veredicto.

Abre `index.html` en un navegador al terminar. GitHub Actions publica los mismos
directorios por servicio como artefactos del flujo.

Para exigir un umbral local más estricto:

```bash
THRESHOLD=97 make -C backend ci SVC=mission-design-service
```

El mínimo del proyecto es 95% de cobertura agregada de ramas. No lo reduzcas
para hacer pasar un cambio fallido.

## Registros y diagnóstico de fallos

La salida de consola es compacta. Los registros detallados se escriben en:

```text
backend/.make-logs/build-<servicio>.log
backend/.make-logs/test-<servicio>.log
backend/.make-logs/gate-<servicio>.log
```

Fallos frecuentes:

- **Daemon de Docker inaccesible:** Testcontainers no puede crear PostgreSQL o
  RabbitMQ. Inicia Docker y repite el comando.
- **Pruebas omitidas:** es un fallo de infraestructura, no un éxito. El flujo de
  GitHub rechaza explícitamente las pruebas omitidas.
- **Cobertura de ramas insuficiente:** revisa `index.html` y `Summary.txt`.
  Añade pruebas de comportamiento; no excluyas código propio de Domain o
  Application.
- **Directorios `bin`/`obj` con otro propietario:** sigue la corrección indicada
  por la comprobación previa del Makefile.

## Reproducir el entorno local en ejecución

La integración continua local verifica de forma aislada. Docker Compose ejecuta
el entorno persistente de desarrollo. Desde `backend/`:

```bash
cp .env.example .env         # opcional: personaliza valores de desarrollo
docker compose up -d         # archivo base + configuración de recarga en caliente
docker compose ps
make doctor                  # comprueba los observadores de .NET
```

Docker Compose aplica automáticamente `docker-compose.override.yml`, monta el
código fuente y ejecuta `dotnet watch`. Este entorno conserva estado y no es un
requisito para `make ci`.

Para construir imágenes limpias, similares a producción y sin la configuración
de recarga en caliente:

```bash
docker compose -f docker-compose.yml build
docker compose -f docker-compose.yml up -d
```

Para detener el entorno sin borrar el volumen de PostgreSQL:

```bash
docker compose down
```

La eliminación de volúmenes reinicia las bases de datos locales y es
destructiva; por eso no forma parte del procedimiento estándar.

## Relación con GitHub Actions

El flujo del backend ejecuta `make -C backend ci SVC=<service>` mediante una
matriz y usa el mismo comando para el gateway.

Los pasos exclusivos de GitHub preparan el ejecutor, comprueban Docker, rechazan
pruebas omitidas y publican registros y cobertura.

Cuando cambie la verificación, actualiza el Makefile o el script de cobertura y
mantén esta guía sincronizada. No dupliques la secuencia con comandos `dotnet`
directos dentro del flujo.
