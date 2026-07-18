# Scoring Monitoring Service

`scoring-monitoring-service` deriva puntuaciones, rankings e historial auditable
a partir de los hechos emitidos durante una sesión. Es un contexto de lectura y
monitoreo; no controla el estado de la sesión.

## Responsabilidades y límites

- Consume respuestas, objetivos resueltos, cambios de estado y asignaciones de
  operador desde RabbitMQ.
- Registra entradas de puntuación append-only y penalizaciones justificadas.
- Mantiene rankings con tiempo de resolución como desempate.
- Proyecta el historial de eventos de una sesión.
- Difunde cambios de scoring por SignalR.
- Es dueño de la base PostgreSQL `scoring_monitoring`.

Session Operations conserva la autoridad sobre el runtime. Este servicio no
admite participantes, no libera pistas y no muta una sesión.

## API y tiempo real

Las rutas se consumen mediante el API Gateway en `http://localhost:8000`; el
contenedor no expone un puerto de host propio.

| Método y ruta | Uso |
| --- | --- |
| `GET /api/sessions/{id}/ranking?teamId=...` | ranking para participante u operador |
| `GET /api/sessions/{id}/ranking/operator` | ranking validado para el operador asignado |
| `POST /api/sessions/{id}/penalties` | aplicar una penalización |
| `GET /api/sessions/{id}/history?teamId=...` | historial completo o filtrado por equipo |
| `/hubs/scoring` | actualizaciones SignalR de puntuación y ranking |
| `GET /health`, `GET /alive` | salud de PostgreSQL y disponibilidad del proceso |

El gateway valida el JWT y el servicio aplica políticas por rol. Las operaciones
del operador también comprueban su asignación a la sesión consultando Session
Operations.

## Integraciones y configuración

| Dependencia | Configuración |
| --- | --- |
| PostgreSQL | `ConnectionStrings__umbral_backendDb` |
| RabbitMQ | `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__UserName`, `RabbitMq__Password` |
| Session Operations | `SessionOperations__BaseAddress` (por defecto `http://session-operations-service:8080`) |
| Telemetría opcional | variables `OTEL_*` |

MassTransit usa inbox/outbox para consumo idempotente y publicación fiable. Las
migraciones se aplican al arrancar la API.

## Desarrollo local

Desde la raíz del monorepositorio:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml up -d
curl http://localhost:8000/health
```

La configuración adicional ejecuta `dotnet watch`. Si cambias consumidores,
registros de DI, middleware o código de arranque, reinicia el servicio:

```bash
make -C backend rewire SVC=scoring-monitoring-service
make -C backend doctor SVC=scoring-monitoring-service
```

Para ejecutar las pruebas y el gate de cobertura de manera aislada:

```bash
make -C backend gate SVC=scoring-monitoring-service
```

Antes de integrar, ejecuta también el contrato completo con build y controles
de arquitectura:

```bash
make -C backend ci SVC=scoring-monitoring-service
```

No hace falta levantar Compose. El comando requiere el SDK fijado en
`backend/global.json`, GNU Make y Docker para Testcontainers.

## Estructura

```text
src/Domain          políticas de score, penalización y ranking
src/Application     consumidores, comandos, consultas y contratos
src/Infrastructure  EF Core, RabbitMQ, inbox/outbox y clientes HTTP
src/Api             REST, SignalR, identidad y manejo de errores
tests/              pruebas unitarias y de integración
```

Consulta la [guía transversal del backend](../../README.md) y la
[integración continua local](../../docs/local-ci.md) para el flujo completo.
