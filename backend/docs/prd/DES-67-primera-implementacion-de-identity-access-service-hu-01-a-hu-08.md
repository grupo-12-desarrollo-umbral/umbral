# DES-67 PRD - Primera implementación de identity-access-service (HU-01 a HU-08)

Source:
https://linear.app/desarrollo-equipo-12/issue/DES-67/prd-primera-implementacion-de-identity-access-service-hu-01-a-hu-08

## Problem Statement

El backlog ya define `HU-01` a `HU-08` para el bounded context `Identity`,
pero el repositorio todavía no tiene una especificación de producto e
implementación que convierta esas historias en una secuencia ejecutable,
respetando al mismo tiempo los límites del modelo canónico.

Desde la perspectiva del equipo, el riesgo no es solo “falta login”, sino
mezclar responsabilidades entre `api-gateway`, `identity-access-service`,
`Keycloak` y `session-operations-service`:

- `Keycloak` autentica, pero no debe ser reimplementado dentro de los
  microservicios
- `api-gateway` valida el JWT y propaga identidad confiable por headers
- `identity-access-service` debe encargarse de `User`, `Role`,
  `IdentityProviderSession`, `JoinToken` y `Access Facts`
- `SessionOperations` sigue siendo la autoridad de runtime sobre
  `LiveSession`, `Team`, `SessionParticipant`, `TeamMember`, reconexión y
  estado compartido

Sin un PRD operativo, existe riesgo de:

- implementar validación JWT dentro de cada servicio y romper ADR-0001
- confundir `AuthenticateUser` con un login duplicado de `Keycloak`
- modelar `Team` o `TeamMember` dentro de `Identity` aunque esos conceptos ya
  pertenecen a `SessionOperations`
- bloquear la entrega por no separar el primer slice de autenticación general
  del resto de capacidades de acceso y membresía
- introducir reglas de autorización dispersas en endpoints en vez de
  encapsularlas en módulos profundos y probables

El problema inmediato es definir una primera implementación de
`identity-access-service` para `HU-01` a `HU-08`, donde la primera entrega
priorice `HU-01` mediante login general soportado por `Keycloak` en
`api-gateway`, y deje explícita la progresión hacia desactivación de acceso,
roles, `JoinToken`, validación de membresía, reconexión y sincronización
multi-dispositivo.

## Solution

La solución es implementar `identity-access-service` como una secuencia de
vertical slices centrados en `Access and Identity`, no como una
reimplementación del runtime de sesiones.

La primera entrega debe enfocarse en `HU-01` y cerrar el camino base de
autenticación general:

1. `Keycloak` autentica al actor
2. `api-gateway` valida el JWT, elimina `Authorization` y reenvía
   `X-User-Id`, `X-User-Role` y `X-User-Email`
3. el cliente ejecuta `Post-Login Provisioning` contra
   `identity-access-service` mediante `AuthenticateUser`
4. `identity-access-service` crea o sincroniza el `User`, registra el
   `IdentityProviderSession` relevante y devuelve `Access Facts`
5. los servicios protegidos aplican guards por rol usando los headers
   confiables y las políticas del bounded context

Sobre esa base, las siguientes entregas deben ampliar la capacidad del servicio
en este orden recomendado:

1. `HU-01` inicio de sesión general de usuarios
2. `HU-02` gestión de acceso de usuarios
3. `HU-03` asignación de roles y permisos
4. `HU-04` y `HU-05` validación de acceso vinculada a equipos y membresía sin
   mover ownership de `Team` fuera de `SessionOperations`
5. `HU-06` inicio de sesión de participantes en mobile sobre el mismo camino
   de autenticación general
6. `HU-07A` validación de membresía en sesión mediante `JoinToken` y
   `AccessPolicy` en coordinación con `SessionOperations`
7. `HU-07B` reconexión autorizada apoyada por facts de identidad y por
   autoridad de runtime en `SessionOperations`
8. `HU-08` sincronización multi-dispositivo habilitada por identidad
   autenticada y membresía validada, pero ejecutada por el contexto de sesión

La solución mantiene estas decisiones de ownership:

- `Identity` autentica indirectamente a través de `Keycloak`, provisiona
  usuarios y devuelve `Access Facts`
- `Identity` puede emitir y validar `JoinToken` y resolver checks de acceso
  grueso
- `Identity` no decide por sí solo la admisión final a una `LiveSession`
- `Identity` no pasa a ser dueño de `Team`, `TeamMember`, progreso, score ni
  estado compartido
- `SessionOperations` sigue decidiendo join, reconexión y sincronización en
  tiempo real

## User Stories

1. Como usuario registrado, quiero iniciar sesión con credenciales válidas,
   para acceder a funcionalidades protegidas de Umbral.
2. Como usuario registrado, quiero que mi autenticación dependa de
   `Keycloak`, para centralizar credenciales y políticas de acceso.
3. Como backend developer, quiero que `api-gateway` sea el único punto que
   valide JWT, para evitar validación duplicada en los microservicios.
4. Como backend developer, quiero que el gateway inyecte `X-User-Id`,
   `X-User-Role` y `X-User-Email`, para que los servicios consuman identidad
   confiable sin parsear tokens.
5. Como usuario autenticado, quiero que el sistema identifique mi `Role`, para
   que solo vea y ejecute capacidades permitidas.
6. Como usuario autenticado, quiero completar el paso de
   `Post-Login Provisioning`, para quedar reconocido como `User` dentro de la
   plataforma.
7. Como backend developer, quiero que `AuthenticateUser` sincronice o cree el
   `User`, para que el dominio de `Identity` tenga trazabilidad propia sin
   duplicar el login.
8. Como backend developer, quiero persistir `IdentityProviderSession` cuando
   sea relevante para trazabilidad y revocación, para contar con un modelo de
   sesión útil al dominio.
9. Como administrador, quiero consultar usuarios registrados, para revisar
   quién tiene acceso a la plataforma.
10. Como administrador, quiero desactivar el acceso de un usuario sin borrar su
    historial, para conservar trazabilidad operativa.
11. Como plataforma, quiero impedir que un `User` desactivado inicie sesión
    útil en Umbral, para bloquear acceso a capacidades protegidas.
12. Como plataforma, quiero impedir que un `User` desactivado use endpoints
    protegidos incluso si aún conserva un token, para que la revocación sea
    efectiva del lado de aplicación.
13. Como administrador, quiero asignar o cambiar el `Role` de un `User`, para
    ajustar sus permisos base.
14. Como plataforma, quiero que cada `Role` resuelva permisos base coherentes,
    para que el acceso no dependa de condicionales ad hoc en cada endpoint.
15. Como administrador, quiero registrar equipos participantes y mantenerlos
    activos o desactivados, para preparar contexto operacional visible antes de
    las sesiones.
16. Como administrador, quiero asociar participantes registrados a equipos
    autorizados, para que la membresía exista antes del runtime de la sesión.
17. Como participante, quiero iniciar sesión en mobile con el mismo esquema de
    identidad confiable, para entrar al flujo de juego solo si tengo el rol
    correcto.
18. Como participante autenticado, quiero que el sistema valide mi pertenencia
    al equipo correspondiente antes de exponer estado compartido, para no
    acceder a contexto ajeno.
19. Como plataforma, quiero emitir un `JoinToken` acotado a `LiveSession` y
    `Team`, para agregar una guardia de aplicación encima del JWT ya validado.
20. Como participante autenticado, quiero consumir un `JoinToken` válido una
    sola vez o bajo reglas claras de replay, para ingresar o reconectar sin
    vulnerar la sesión.
21. Como `SessionOperations`, quiero consultar facts de acceso y validación de
    membresía desde `Identity`, para no duplicar lógica de acceso.
22. Como `SessionOperations`, quiero seguir siendo la autoridad de admisión
    final y reconexión, para que `Identity` no invada el runtime.
23. Como participante autorizado, quiero reconectarme a una sesión activa y
    recuperar mi contexto, para continuar después de una desconexión.
24. Como plataforma, quiero impedir reconexiones o joins fuera del equipo
    autorizado, para proteger el estado compartido de cada equipo.
25. Como participante autorizado, quiero que varios dispositivos de mi mismo
    equipo reflejen el mismo estado vigente, para colaborar sin
    inconsistencias.
26. Como plataforma, quiero asegurar que la sincronización multi-dispositivo
    nunca mezcle estado entre equipos distintos, para preservar separación
    operativa.
27. Como backend developer, quiero encapsular guards y políticas de acceso en
    módulos profundos, para evitar reglas dispersas en handlers y endpoints.
28. Como backend developer, quiero que la primera entrega sea `HU-01` antes de
    membresía y runtime, para construir sobre una base de autenticación
    estable.
29. Como reviewer, quiero ver separados los conceptos de autenticación,
    provisión de usuario, autorización gruesa y admisión de sesión, para
    defender claramente los bounded contexts.
30. Como equipo de desarrollo, quiero una secuencia explícita para `HU-01` a
    `HU-08`, para implementar el servicio por slices verificables en vez de por
    carpetas sueltas.

## Implementation Decisions

- La implementación de `identity-access-service` se hará por vertical slices
  orientados al backlog, comenzando por `HU-01` como primer slice obligatorio.
- `HU-01` se implementará como autenticación externalizada en `Keycloak` con
  validación centralizada en `api-gateway`, seguida por `Post-Login
  Provisioning` en `identity-access-service`.
- `AuthenticateUser` se define explícitamente como sincronización o creación
  del `User` a partir de claims ya autenticados. No reimplementa login ni
  valida credenciales directamente.
- El contrato confiable entre gateway y microservicios será solo `X-User-Id`,
  `X-User-Role` y `X-User-Email`. Los microservicios no usarán `AddJwtBearer`
  ni parsing directo de JWT.
- El primer slice debe probar al menos un flujo completo: autenticación válida
  en `Keycloak`, propagación por gateway, llamada a `AuthenticateUser`,
  creación o actualización de `User`, y acceso exitoso a una capacidad
  protegida consistente con el `Role`.
- El agregado `User` será la raíz para identidad interna, estado de acceso y
  asignación de `Role`.
- `IdentityProviderSession` será el agregado o registro persistido para
  correlación, trazabilidad, revocación y control de sesión relevante a
  dominio, no un mero espejo técnico del proveedor.
- Los módulos profundos prioritarios serán:
- `IdentityProvisioningPolicy` para alinear claims externos con el estado
  interno de `User`
- `AccessPolicy` para resolver acceso grueso por rol, estado del usuario y
  target protegido
- `JoinTokenPolicy` para emisión, expiración, consumo y anti-replay de
  `JoinToken`
- `AccessGuard` estilo `Proxy` en aplicación/API para proteger capacidades
  antes de ejecutar casos de uso
- La matriz de permisos base se resolverá desde `Role` y `AccessPolicy`, no
  desde checks ad hoc en endpoints.
- `HU-02` añadirá `DeactivateUserAccess` y catálogo de acceso, reutilizando el
  mismo `User` como fuente de verdad para estado activo/inactivo.
- `HU-03` añadirá `AssignUserRole` y revocación o reasignación explícita de
  roles, manteniendo que el role efectivo que ve la plataforma no sea una
  inferencia distribuida.
- `HU-04` y `HU-05` no autorizan mover `Team` ni `TeamMember` a `Identity`. El
  PRD asume que `SessionOperations` sigue siendo owner de esos conceptos y que
  `Identity` consumirá o validará referencias externas para emitir `Access
  Facts` o `JoinToken`.
- Para `HU-04` y `HU-05`, si el backlog necesita un registro administrativo
  previo de equipos o membresías, debe implementarse como contrato explícito
  entre bounded contexts o como proyección controlada, nunca como doble
  ownership del agregado `Team`.
- `HU-06` reutilizará el mismo backbone de `HU-01`, con el cliente móvil
  limitado al rol `Participante`.
- `HU-07A` se modelará como validación de membresía antes de exponer contexto
  de equipo. `Identity` validará facts de identidad, rol, estado de acceso y
  `JoinToken`; `SessionOperations` decidirá admisión final a la sesión activa.
- `HU-07B` se modelará como reconexión autorizada donde `Identity` aporta facts
  y validación, pero el estado recuperado y la reanudación de presencia
  pertenecen a `SessionOperations`.
- `HU-08` se considera una capacidad de sincronización del runtime y por tanto
  se ejecutará principalmente en `SessionOperations`, apoyada por identidad
  autenticada y autorización previa.
- Los servicios de aplicación mínimos alineados al backlog serán:
  `AuthenticateUser`, `DeactivateUserAccess`, `AssignUserRole`,
  `IssueJoinToken`, `ValidateParticipantMembershipAccess`,
  `ReconnectAuthenticatedParticipant`, `GetUserAccessCatalog`,
  `GetAuthenticatedActorProfile`.
- Los repositorios mínimos serán: `IUserRepository`,
  `IIdentityProviderSessionRepository`, `IJoinTokenRepository`,
  `IAccessReadModelRepository`.
- Los domain events prioritarios serán: `UserProvisioned`,
  `UserAccessDeactivated`, `UserRoleAssigned`, `UserRoleRevoked`,
  `IdentityProviderSessionStarted`, `IdentityProviderSessionEnded`,
  `JoinTokenIssued`, `JoinTokenConsumed`, `AccessDecisionRecorded`.
- La primera entrega debe incluir wiring de `Infrastructure/Identity/Keycloak/`
  solo en `identity-access-service`, y nunca en los demás bounded-context
  services.
- El diseño debe tolerar que `Keycloak` autentique, pero que el acceso
  efectivo a la plataforma dependa además del estado del `User` en Umbral y de
  su `Role`.
- Los endpoints y handlers no deben contener reglas de autorización dispersas;
  deben delegar en guards y políticas estables para reducir drift entre web,
  mobile y servicios backend.

## Testing Decisions

- Una buena prueba validará comportamiento observable: autenticación o
  provisión exitosa, rechazo de acceso, role efectivo, emisión y consumo de
  `JoinToken`, y respuesta del sistema ante usuarios desactivados. No debe
  acoplarse a detalles internos de handlers o infraestructura.
- El primer slice `HU-01` debe probarse de forma vertical: request autenticado
  por gateway, headers confiables presentes, `AuthenticateUser` crea o
  sincroniza `User`, y una capacidad protegida responde según el `Role`.
- `IdentityProvisioningPolicy` debe tener pruebas unitarias aisladas para
  creación de `User`, sincronización de email, sincronización de `Role`,
  idempotencia y rechazo de claims insuficientes.
- `AccessPolicy` debe tener pruebas unitarias aisladas para usuarios activos,
  desactivados, roles válidos, roles sin permiso y targets protegidos.
- `JoinTokenPolicy` debe tener pruebas unitarias aisladas para expiración,
  consumo único, replay y relación correcta con actor, `LiveSession` y `Team`.
- Los handlers de aplicación deben probar orquestación con repositorios y
  guards, especialmente `AuthenticateUser`, `DeactivateUserAccess`,
  `AssignUserRole`, `IssueJoinToken` y `ValidateParticipantMembershipAccess`.
- Debe existir al menos una prueba de integración de persistencia para `User`,
  `IdentityProviderSession` y `JoinToken`.
- Debe existir al menos una prueba HTTP o end-to-end del camino
  `Keycloak -> api-gateway -> identity-access-service` para `HU-01`, porque
  ese slice depende del boundary real del gateway.
- Debe reutilizarse el patrón de pruebas ya establecido en otros servicios
  backend: dominio para invariantes, aplicación para orquestación, integración
  para persistencia y API para contrato observable.

## Out of Scope

- Reimplementar formularios de login, manejo de credenciales o validación JWT
  dentro de cada microservicio
- Transferir ownership de `LiveSession`, `Team`, `SessionParticipant`,
  `TeamMember`, score o progreso a `identity-access-service`
- Permitir admisión final a sesiones sin validación de runtime por
  `SessionOperations`
- Diseñar sincronización multi-dispositivo completa dentro de `Identity`
- UI detallada de web o mobile, theming de `Keycloak`, recuperación de
  contraseña o self-registration público
- Reemplazar `Keycloak` por autenticación propia de Umbral

## Further Notes

- Esta PRD usa como referencias normativas el `Identity` service context,
  `docs/ddd_solution_model.md`, `docs/bd_umbral_entity_spec.md`, ADR-0001 y el
  resumen de diseño del `api-gateway` con `Keycloak`.
- El foco explícito de la primera entrega es `HU-01`, y en particular el camino
  de login general con `Keycloak` en `api-gateway` más `Post-Login
  Provisioning` en `identity-access-service`.
- El término correcto es `Keycloak`; cualquier referencia previa a “kecloak” se
  interpreta como ese proveedor de identidad.
- Si durante la implementación de `HU-04` y `HU-05` aparece conflicto entre
  backlog y ownership canónico de `Team`, debe resolverse manteniendo el
  ownership en `SessionOperations` y ajustando el contrato entre contextos, no
  duplicando aggregates.
