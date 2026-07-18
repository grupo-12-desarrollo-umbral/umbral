# Users Service

`users-service` es la autoridad sobre los usuarios conocidos por Umbral, sus
roles, estado de acceso, equipos de referencia y membresías. Keycloak sigue
siendo la autoridad de autenticación y credenciales. Por tanto, este servicio
no autentica contraseñas, no almacena credenciales y no emite ni valida tokens.

## Delegación a Keycloak

`users-service` expone casos de uso de registro, invitación y recuperación, pero
los implementa delegando la gestión de identidad en la API administrativa y los
flujos hospedados de Keycloak.

| Keycloak es responsable de | Users Service es responsable de |
| --- | --- |
| crear la identidad y almacenar sus credenciales | mantener el perfil de aplicación asociado al `sub` externo |
| autenticar el login y emitir el JWT | aprovisionar o sincronizar al usuario después del login |
| verificar correo y aplicar la política de contraseñas | mantener rol, estado de acceso y reglas propias de Umbral |
| ejecutar recuperación y cambio de contraseña | solicitar a Keycloak el envío del flujo correspondiente |
| habilitar, deshabilitar y sincronizar roles de realm | coordinar esos cambios y persistirlos en Umbral solo si Keycloak responde correctamente |

El API Gateway, no `users-service`, valida el JWT de Keycloak. Después transmite
la identidad validada mediante `X-User-Id`, `X-User-Role` y `X-User-Email`.

## Responsabilidades y límites

- Aprovisiona o sincroniza el perfil local después del login.
- Invita usuarios y registra participantes delegando la creación de identidad y
  credenciales a Keycloak.
- Cambia roles y activa o desactiva acceso, propagando primero el cambio al
  proveedor de identidad.
- Gestiona equipos de referencia y sus participantes autorizados.
- Responde hechos de elegibilidad y membresía para otros contextos.
- Es dueño de la base PostgreSQL `users`.

Este servicio no valida JWT y no decide la admisión final a una sesión. El API
Gateway valida el token; Session Operations decide si un participante puede
entrar a una sesión concreta.

## API

El acceso local es mediante `http://localhost:8000`. El servicio no publica un
puerto directo al host porque sus headers de identidad solo son confiables
cuando los genera el gateway.

| Superficie | Operaciones principales |
| --- | --- |
| `/api/users/authenticated`, `/api/users/me` | bootstrap y perfil del actor actual |
| `/api/users` | catálogo, rol, activación y desactivación |
| `/api/users/invitations` | invitación administrada mediante Keycloak |
| `/api/users/register` | autorregistro anónimo de participante |
| `/api/users/forgot-password` | inicio anónimo del flujo de recuperación |
| `/api/teams` | catálogo y mantenimiento de equipos de referencia |
| `/api/teams/{id}/participants` | membresías autorizadas del equipo |
| `/api/permissions` | acceso a plataforma y elegibilidad de equipos |
| `/health`, `/alive` | salud de PostgreSQL y disponibilidad del proceso |

Solo `register` y `forgot-password` son anónimos; el gateway les aplica rutas y
límites específicos. El resto requiere JWT y políticas adicionales de
administrador, operador o participante según la operación.

## Integraciones y configuración

| Dependencia | Configuración |
| --- | --- |
| PostgreSQL | `ConnectionStrings__umbral_backendDb` |
| Keycloak Admin API | sección `Keycloak`: `AdminAuthority`, `Realm`, `ClientId`, `ClientSecret` y opciones de reintento |
| Identidad confiable | headers `X-User-Id`, `X-User-Role` y `X-User-Email` generados por el gateway |
| Telemetría opcional | variables `OTEL_*` |

En desarrollo, Compose proporciona los valores de Keycloak y Mailpit captura
los correos de invitación, verificación y recuperación en
`http://localhost:8025`. Fuera de Development, los valores predeterminados de
Keycloak son rechazados durante el arranque. Las migraciones se aplican al
iniciar la API.

## Desarrollo local

Desde la raíz del monorepositorio:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml up -d
curl http://localhost:8000/health
```

La configuración adicional ejecuta `dotnet watch`; todas las llamadas de
negocio deben pasar por el gateway.

Para ejecutar sus pruebas y el gate de cobertura sin levantar Compose:

```bash
make -C backend gate SVC=users-service
```

Antes de integrar, ejecuta también el contrato completo con build y controles
de arquitectura:

```bash
make -C backend ci SVC=users-service
```

El comando ejecuta controles de arquitectura, compilación, pruebas unitarias e
integración con Testcontainers, y cobertura. Requiere el SDK de
`backend/global.json`, GNU Make y Docker.

## Estructura

```text
src/Domain          usuarios, roles, equipos y políticas de acceso
src/Application     casos de uso CQRS y contratos
src/Infrastructure  EF Core y adaptador de administración de Keycloak
src/Api             HTTP, identidad confiable y manejo de errores
tests/              pruebas unitarias y de integración
```

Consulta la [guía transversal del backend](../../README.md) y la
[integración continua local](../../docs/local-ci.md) para el flujo completo.
