# Session Operations Service

`session-operations-service` es la autoridad sobre la ejecución de sesiones en
vivo. Convierte una misión publicada en un snapshot de runtime y gobierna su
ciclo de vida, equipos, participantes, progresión y evidencias.

## Responsabilidades y límites

- Crea sesiones y asigna el operador responsable.
- Controla las transiciones `Scheduled`, `Preparing`, `Active`, `Paused`,
  `Finished` y `Cancelled`.
- Asocia equipos de referencia y decide la admisión o reconexión de
  participantes.
- Registra respuestas de trivia, escaneos QR y trazas de evidencia.
- Libera pistas, mantiene temporizadores y publica actualizaciones por SignalR.
- Publica hechos de runtime en RabbitMQ para scoring, ranking y auditoría.
- Es dueño de la base PostgreSQL `session_operations`.

No edita misiones ni calcula el ranking. Lee contratos de Mission Design y Users,
y entrega hechos a Scoring Monitoring sin acceder a sus bases de datos.

## API y tiempo real

Todo el tráfico externo entra por `http://localhost:8000`; el servicio no
publica un puerto directo al host.

| Superficie | Operaciones principales |
| --- | --- |
| `/api/sessions` | crear y listar sesiones |
| `/api/sessions/{id}/operator-assignment` | asignar operador |
| `/api/sessions/{id}/state` | cambiar estado de la sesión |
| `/api/sessions/{id}/teams` | asociar y consultar equipos |
| `/api/sessions/by-code/{code}/...` | lobby y selección de equipo |
| `/api/sessions/{id}/participants/...` | reconexión, temporizador, tablero, respuestas y escaneos |
| `/api/sessions/{id}/operator-panel` | vista operativa de la sesión |
| `/api/sessions/{id}/clues/...` | pistas liberables y liberación |
| `/api/sessions/{id}/evidence-submissions` | trazabilidad de evidencias |
| `/hubs/sessions` | eventos SignalR para participantes y operadores |
| `/health`, `/alive` | salud de PostgreSQL y disponibilidad del proceso |

El gateway valida el JWT y genera los headers de identidad confiable. La API
aplica además políticas por rol y comprueba que el operador o participante tenga
acceso a la sesión concreta.

## Integraciones y configuración

| Dependencia | Configuración |
| --- | --- |
| PostgreSQL | `ConnectionStrings__umbral_backendDb` |
| RabbitMQ | `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__UserName`, `RabbitMq__Password` |
| Users Service | `UsersService__BaseAddress` |
| Mission Design | `PublishedTriviaQuizSource__BaseAddress`, `MissionReadinessSource__BaseAddress`, `MissionRuntimeSource__BaseAddress` |
| Telemetría opcional | variables `OTEL_*` |

Las migraciones se aplican al arrancar. La publicación de eventos usa MassTransit
y outbox transaccional para mantener coordinados el commit y la mensajería.

## Desarrollo local

Desde la raíz del monorepositorio:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml up -d
curl http://localhost:8000/health
```

La configuración adicional ejecuta `dotnet watch`. Después de cambiar registros
de DI, consumidores, middleware o el arranque, reinicia la composición del
servicio y verifica el watcher:

```bash
make -C backend rewire SVC=session-operations-service
make -C backend doctor SVC=session-operations-service
```

Para ejecutar las pruebas y el gate de cobertura sin levantar previamente
Compose:

```bash
make -C backend gate SVC=session-operations-service
```

Antes de integrar, ejecuta el contrato completo con build y controles de
arquitectura:

```bash
make -C backend ci SVC=session-operations-service
```

Requiere el SDK de `backend/global.json`, GNU Make y Docker; las pruebas de
integración crean sus dependencias con Testcontainers.

## Estructura

```text
src/Domain          estado e invariantes de la sesión
src/Application     casos de uso CQRS, eventos y puertos
src/Infrastructure  EF Core, clientes HTTP, RabbitMQ y outbox
src/Api             REST, SignalR, identidad y manejo de errores
tests/              pruebas unitarias y de integración
```

Consulta la [guía transversal del backend](../../README.md) y la
[integración continua local](../../docs/local-ci.md) para el flujo completo.
