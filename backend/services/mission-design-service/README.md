# Mission Design Service

`mission-design-service` es la autoridad sobre el contenido que después ejecuta
una sesión: misiones, su estructura jerárquica, objetivos y cuestionarios de
trivia. No administra sesiones en vivo, participantes ni puntuaciones.

## Responsabilidades y límites

- Crea y edita misiones con `Stage`, `Substage`, `Clue` y `Target`.
- Valida la preparación y activación de una misión.
- Gestiona el ciclo de autoría de trivias: borrador, publicación, archivo y
  retiro.
- Expone planes inmutables de ejecución para que Session Operations cree su
  propio snapshot.
- Es dueño de la base PostgreSQL `mission_design`.

Session Operations consulta este servicio al crear una sesión, pero no comparte
su base de datos. Este servicio tampoco decide progresión, admisión o resultados
de una sesión.

## API

En desarrollo se accede únicamente mediante el API Gateway:
`http://localhost:8000`. El contenedor no publica un puerto directo al host para
evitar que se omita la validación de identidad del gateway.

| Superficie | Operaciones principales |
| --- | --- |
| `/api/missions` | catálogo, detalle, creación, edición, activación y desactivación |
| `/api/missions/{id}/readiness` | diagnóstico de preparación para activar |
| `/api/missions/{id}/runtime-plan` | snapshot consumible por Session Operations |
| `/api/missions/{id}/nodes` | árbol Stage → Substage → Clue |
| `/api/missions/.../targets` | objetivos, QR, coordenadas y asociación de pistas |
| `/api/trivias` | catálogo y ciclo de vida de cuestionarios |
| `/api/trivias/{id}/questions` | preguntas, opciones, puntaje y tiempo límite |
| `/health` | disponibilidad de la API y PostgreSQL |
| `/alive` | disponibilidad del proceso |

Las rutas de negocio requieren un JWT válido en el gateway. Los permisos de
administrador u operador se vuelven a aplicar en la capa de aplicación.

## Dependencias y configuración

| Dependencia | Configuración |
| --- | --- |
| PostgreSQL | `ConnectionStrings__umbral_backendDb` |
| Identidad confiable | headers `X-User-Id`, `X-User-Role` y `X-User-Email` generados por el gateway |
| Telemetría opcional | `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_SERVICE_NAME` |

Las migraciones de Entity Framework se aplican al arrancar la API.

## Desarrollo local

Desde la raíz del monorepositorio:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml up -d
curl http://localhost:8000/health
```

La configuración adicional ejecuta `dotnet watch` y monta el código fuente. El
servicio se alcanza por las rutas del gateway, no por un puerto propio.

Para ejecutar sus pruebas y el gate de cobertura, no hace falta levantar
Compose:

```bash
make -C backend gate SVC=mission-design-service
```

Antes de integrar, ejecuta además el contrato completo, que añade build y
controles de arquitectura:

```bash
make -C backend ci SVC=mission-design-service
```

El comando ejecuta controles de arquitectura, compilación, pruebas unitarias e
integración con Testcontainers, y el gate canónico de cobertura. Requiere el SDK
de `backend/global.json`, GNU Make y Docker.

## Estructura

```text
src/Domain          reglas e invariantes de misiones y trivias
src/Application     casos de uso CQRS y contratos
src/Infrastructure  EF Core y adaptadores
src/Api             HTTP, identidad confiable y manejo de errores
tests/              pruebas unitarias y de integración
```

Consulta la [guía transversal del backend](../../README.md) y la
[integración continua local](../../docs/local-ci.md) para el flujo completo.
