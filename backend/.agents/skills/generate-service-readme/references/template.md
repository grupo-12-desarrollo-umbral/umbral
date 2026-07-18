# Canonical Service README skeleton

Fill each section from real source (see the sourcing map in SKILL.md). Keep the
order. Omit a section entirely if it does not apply to the service — do not pad.
Language follows the repo convention (Spanish for Umbral). Placeholders in
`«guillemets»`.

````md
# «Service Name»

`«service-id»` implementa el contexto delimitado (*bounded context*) de **«Dominio»**. Se encarga de «responsabilidades en una frase» — delegando «lo que delega» en «sistema externo».

> «Callout de frontera clave: qué NO hace este servicio (p. ej. no valida JWT, lo hace el gateway).»

---

## Tabla de contenidos

1. [Por qué existe este servicio](#por-qué-existe-este-servicio)
2. [Arquitectura general](#arquitectura-general)
3. [Configuración](#configuración)
4. [Cómo ejecutar el servicio](#cómo-ejecutar-el-servicio)
5. [Autenticación: cabeceras de confianza](#autenticación-cabeceras-de-confianza)
6. [Modelo de dominio](#modelo-de-dominio)
7. [Referencia de la API](#referencia-de-la-api)
8. [Respuestas de error (ProblemDetails)](#respuestas-de-error-problemdetails)
9. [«Integraciones específicas»](#integraciones)   ← solo si aplica
10. [Persistencia](#persistencia)
11. [Testing](#testing)
12. [Decisiones de arquitectura (ADR) y reglas de frontera](#adr)

---

## Por qué existe este servicio

«Párrafo: qué hace el sistema externo vs. qué aporta este servicio.»

| «Sistema externo» hace | «service-id» hace |
|------------------------|-------------------|
| «…» | **«Capacidad»** — «…» |

«Cierre: qué problema evita su existencia (frontera del bounded context).»

---

## Arquitectura general

```
«diagrama ASCII del flujo de petición: origen → gateway → servicio → datos»
```

- **«Componente»** «rol».
- Construido con **Clean Architecture** (`Domain`, `Application`, `Infrastructure`, `Api`) y **CQRS sobre MediatR».

---

## Configuración

«Indica si NO hay `appsettings.json` y que todo se inyecta por variables de entorno (claves anidadas con `__`).»

### Variables de entorno

| Variable | Obligatoria | Por defecto | Descripción |
|----------|:-----------:|-------------|-------------|
| `ConnectionStrings__umbral_backendDb` | ✅ | — | «…» |
| `«VAR»` | — | `«default»` | «…» |

### Connection string

«Nombre lógico + formato Npgsql de ejemplo.»

### Puertos

| Contexto | Dirección | Notas |
|----------|-----------|-------|
| Dentro del contenedor | `http://+:8080` | `ASPNETCORE_URLS` |
| Host (docker-compose) | `http://localhost:«PUERTO»` | mapeo `«PUERTO»:8080` |
| A través del gateway | `http://localhost:8000` | añade cabeceras de confianza |

### Ejemplo de definición en `docker-compose.yml`

```yaml
«bloque del servicio extraído de docker-compose.yml»
```

---

## Cómo ejecutar el servicio

### Con Docker Compose (recomendado)

```bash
docker compose up --build «service-id»
```

«Nota sobre migraciones automáticas al arrancar, si aplica.»

### En local con el toolchain .NET

```bash
make build SVC=«service-id»
make test  SVC=«service-id»
make gate  SVC=«service-id»
make ef    SVC=«service-id» ARGS="migrations add «Nombre»"
```

«Comando de arranque directo con la connection string, si aplica.»

### «Scripts de seed / utilidades» (si existen)

---

## Autenticación: cabeceras de confianza

«Describe el esquema de auth del servicio. Para Umbral: el gateway valida el JWT
e inyecta las cabeceras; el servicio no valida JWT.»

| Cabecera | Origen | Ejemplo |
|----------|--------|---------|
| `X-User-Id` | `sub` | `abc-123` |
| `X-User-Role` | rol de realm | `Administrator` |
| `X-User-Email` | `email` | `user@example.com` |

«Comportamiento: faltan cabeceras → 401; rol incorrecto → 403; usuario desactivado → 403; niveles de autorización (políticas de endpoint + AuthorizationBehaviour).»

---

## Modelo de dominio

### Entidades

| Entidad | Descripción |
|---------|-------------|
| `«Entity»` | «campos clave» |

### Roles

| Rol | Valor del enum |
|-----|:--------------:|
| `«Role»` | `«n»` |

### Capacidades protegidas y matriz de acceso

| Capacidad (`ProtectedCapability`) | Roles permitidos |
|-----------------------------------|------------------|
| `«Capability»` | «roles» |

### Servicios de dominio

- **`«Service»`** — «…».

### Eventos de dominio

`«Event1»`, `«Event2»`, … «+ cómo se publican (interceptor / MediatR)».

---

## Referencia de la API

Convenciones:

- **Auth** = rol(es) exigido(s); *Cabeceras de confianza* = solo las tres cabeceras.
- Cuerpos JSON (`Content-Type: application/json`), claves en `camelCase`.
- Errores en formato [ProblemDetails](#respuestas-de-error-problemdetails).

### Resumen de endpoints

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| `«VERB»` | `«/ruta»` | `«rol»` | «…» |

«Luego, una subsección por endpoint, agrupadas por recurso:»

### «Recurso»

#### `«VERB» «/ruta»`

«Descripción de 1–2 frases. Idempotencia / efectos secundarios si aplica.»

**Auth:** «rol o cabeceras de confianza».

**Query params:** «si aplica».

**Request**
```json
{ "«campo»": "«valor»" }
```

**Response `«código»`**
```json
{ "«campo»": "«valor»" }
```

**Errores notables:** «código + excepción, si aplica».

---

## Respuestas de error (ProblemDetails)

«Frase + ejemplo de cuerpo.»

```json
{ "title": "«…»", "detail": "«…»", "status": «código» }
```

| Excepción | HTTP |
|-----------|:----:|
| `«Exception»` | «código» |
| Cualquier otra | 500 |

«Nota: las políticas de autorización devuelven 401/403 antes del manejador.»

---

## «Integraciones específicas»   ← solo si aplica

«p. ej. Sincronización de roles con Keycloak: pasos, fuente de verdad, timeouts.»

---

## Persistencia

- **Motor:** PostgreSQL vía Npgsql.
- **Connection string:** `umbral_backendDb`.
- **Migraciones:** «automáticas al arrancar / manuales».
- **Interceptores:** `AuditableEntityInterceptor`, `DispatchDomainEventsInterceptor`.
- **Repositorios:** `«…»`.
- **Fábrica de diseño:** `«…»` lee `«ENV_VAR»` (solo `dotnet ef`).

---

## Testing

```
tests/
  UnitTests/        — «…»
  IntegrationTests/ — WebApplicationFactory + Testcontainers
```

```bash
make test SVC=«service-id»
make gate SVC=«service-id»   # gate ADR-0005, cobertura agregada de ramas >= 95%
```

«No hardcodear porcentajes ni número de tests.»

---

## Decisiones de arquitectura (ADR) y reglas de frontera

ADR relevantes (en `backend/docs/adr/`):

- **ADR-«NNNN»** — «…».

Reglas de frontera del *bounded context*:

- «service-id» **no** «…».
- «service-id» devuelve «…»; los servicios aguas abajo deciden «…».
````
