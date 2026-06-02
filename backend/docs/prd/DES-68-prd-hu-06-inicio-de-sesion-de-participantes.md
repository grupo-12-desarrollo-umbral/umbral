# DES-68 PRD - HU-06 inicio de sesión de participantes

Source:
https://linear.app/desarrollo-equipo-12/issue/DES-68/prd-hu-06-inicio-de-sesion-de-participantes

## Problem Statement

`DES-10` ya define que cada participante debe iniciar sesión antes de entrar
al flujo de juego desde una aplicación móvil construida con Expo sobre React
Native, pero esa historia todavía necesita una especificación de producto e
implementación que la separe con claridad de `HU-01`, `HU-07A`, `HU-07B` y
del enabler `DES-58`.

El riesgo no es “agregar otro login”, sino mezclar cuatro responsabilidades
distintas:

- `Keycloak` autentica credenciales y emite el JWT
- `api-gateway` valida el JWT y propaga identidad confiable por headers
- `identity-access-service` realiza `Post-Login Provisioning`, evalúa
  `AccessPolicy` y devuelve `Access Facts`
- la aplicación móvil de participantes construida con Expo sobre React Native
  consume solo las capacidades permitidas al rol `Participant`

Sin esta especificación, el equipo puede caer en errores de diseño que rompen
el modelo canónico:

- duplicar autenticación dentro del cliente móvil o dentro del microservicio
- permitir que actores con rol `Administrator` u `Operator` entren al flujo
  móvil de participantes
- confundir login con validación de membresía de sesión, que pertenece a
  `HU-07A`
- depender de estado de runtime de `SessionOperations` para resolver una
  historia que todavía es de identidad y acceso grueso
- dispersar checks de rol en endpoints y pantallas en vez de encapsularlos en
  módulos profundos reutilizables

El problema concreto de `HU-06` es habilitar un camino de autenticación
individual para participantes en una aplicación móvil Expo/React Native que
reutilice el backbone de `HU-01`, bloquee usuarios no autenticados o
desactivados, y exponga solamente `Access Facts` y capacidades compatibles con
el rol `Participant`.

## Solution

La solución es tratar `HU-06` como una especialización de `HU-01`, no como un
flujo de autenticación independiente, y consumirla desde una aplicación móvil
de participantes implementada con Expo sobre React Native.

El participante inicia sesión contra `Keycloak` desde la aplicación móvil Expo
/ React Native; luego el `api-gateway` valida el JWT, elimina el token de los
servicios internos y reenvía `X-User-Id`, `X-User-Role` y `X-User-Email`. Con
esos headers confiables, el cliente completa `Post-Login Provisioning`
llamando a `AuthenticateUser` en `identity-access-service`, que crea o
sincroniza el `User`, registra la `IdentityProviderSession` relevante y
devuelve `Access Facts` suficientes para que la aplicación móvil continúe solo
si el actor está activo y su `Role` es `Participant`.

La entrega debe demostrar estos resultados visibles:

1. El participante no puede entrar al flujo de juego sin autenticación previa.
2. El cliente móvil rechaza o redirige actores cuyo `Role` no sea
   `Participant`.
3. Un `User` desactivado queda bloqueado aun cuando llegue con identidad
   previamente autenticada por el proveedor.
4. Las capacidades accesibles desde mobile se resuelven a partir de
   `AccessPolicy`, sin checks ad hoc por endpoint.
5. La historia termina en el borde de identidad/autorización gruesa; la
   admisión a sesión, la membresía por equipo y la reconexión siguen delegadas
   a `HU-07A` y `HU-07B`.

## User Stories

1. Como participante, quiero iniciar sesión desde una aplicación móvil
   construida con Expo sobre React Native, para acceder al flujo de juego con
   mi identidad individual.
2. Como participante, quiero autenticarme con el mismo proveedor central
   (`Keycloak`) que usa el resto de la plataforma, para no depender de un
   login paralelo.
3. Como plataforma, quiero exigir autenticación antes de cualquier acceso al
   cliente móvil de participantes, para evitar entrada anónima al flujo de
   juego.
4. Como plataforma, quiero que el `api-gateway` sea el único validador del
   JWT, para no duplicar lógica de autenticación en microservicios.
5. Como `identity-access-service`, quiero recibir `X-User-Id`, `X-User-Role` y
   `X-User-Email` como identidad confiable, para provisionar al actor sin
   parsear el token.
6. Como participante autenticado, quiero que `AuthenticateUser` cree o
   sincronice mi `User`, para quedar reconocido por el bounded context
   `Identity`.
7. Como plataforma, quiero registrar la `IdentityProviderSession` relevante
   del participante autenticado, para mantener trazabilidad y base de
   revocación.
8. Como participante autenticado, quiero obtener mi perfil autenticado dentro
   de Umbral, para que el cliente sepa quién soy y qué rol tengo realmente.
9. Como plataforma, quiero restringir el cliente móvil al rol `Participant`,
   para impedir que `Administrator` u `Operator` entren al flujo móvil de
   juego.
10. Como participante, quiero ver solo funcionalidades compatibles con mi rol,
    para no mezclar experiencia móvil de juego con capacidades
    administrativas u operativas.
11. Como plataforma, quiero bloquear a un `User` desactivado aunque el
    proveedor de identidad ya lo haya autenticado, para que la revocación de
    acceso sea efectiva dentro de Umbral.
12. Como desarrollador backend, quiero encapsular la decisión de acceso en
    `AccessPolicy`, para no repetir checks de rol y estado en cada endpoint.
13. Como desarrollador backend, quiero usar un `Proxy` de acceso delante de
    operaciones protegidas, para que la restricción del rol participante sea
    consistente en transport y aplicación.
14. Como desarrollador móvil en Expo/React Native, quiero un endpoint estable de
    `Post-Login Provisioning`, para continuar el flujo después del login del
    proveedor.
15. Como desarrollador móvil en Expo/React Native, quiero un endpoint estable
    para consultar el perfil autenticado, para hidratar la sesión local del
    participante.
16. Como desarrollador móvil en Expo/React Native, quiero un endpoint o
    capability check de acceso protegido, para determinar rápido si el actor
    puede seguir en el flujo móvil.
17. Como participante con rol incorrecto, quiero ser rechazado antes de entrar
    al flujo de juego, para no ver contexto que no me corresponde.
18. Como plataforma, quiero separar autenticación gruesa de validación de
    membresía de sesión, para que `HU-06` no absorba responsabilidades de
    `HU-07A`.
19. Como plataforma, quiero separar autenticación móvil de reconexión a sesión
    activa, para que `HU-06` no absorba responsabilidades de `HU-07B`.
20. Como equipo de desarrollo, quiero una definición explícita de límites
    entre `Identity`, `api-gateway`, cliente móvil y `SessionOperations`, para
    implementar `DES-10` sin ambigüedad arquitectónica.
21. Como reviewer, quiero ver que el flujo móvil reutiliza el backbone de
    `HU-01`, para evitar una segunda solución de autenticación dentro del
    sistema.
22. Como equipo de producto, quiero que `HU-06` quede lista para ser consumida
    por el enabler `DES-58`, para que el cliente móvil se construya sobre
    contratos ya defendibles.

## Implementation Decisions

- `HU-06` se implementa como un slice de identidad especializado para
  `Participant`, reutilizando los contratos base de `HU-01`.
- `Keycloak` sigue siendo el único responsable de autenticar credenciales;
  `identity-access-service` no reimplementa login.
- `api-gateway` sigue siendo el único punto que valida JWT y propaga identidad
  confiable por `X-User-Id`, `X-User-Role` y `X-User-Email`.
- `AuthenticateUser` sigue significando `Post-Login Provisioning`: crear o
  sincronizar `User` desde claims ya autenticados y registrar
  `IdentityProviderSession` cuando corresponda.
- El flujo mobile debe apoyarse en tres capacidades ya coherentes con el
  bounded context: bootstrap/autenticación provisionada, perfil autenticado y
  chequeo de acceso protegido.
- El consumidor explícito de este slice es una aplicación móvil de
  participantes construida con Expo sobre React Native; el PRD asume ese
  cliente como el entry point del actor `Participant`.
- `AccessPolicy` es la autoridad única para decidir si el actor autenticado y
  activo puede consumir capacidades del cliente móvil de participantes.
- El patrón requerido para `HU-06` es `Proxy`; la restricción del rol
  participante debe vivir en un guard estructural y no en condicionales
  dispersos.
- El `Role` efectivo consumido por mobile es el del `User` provisionado y
  validado por la política de acceso; no se debe derivar de estado de sesión
  ni de parámetros de cliente.
- Los módulos profundos de esta entrega son `IdentityProvisioningPolicy`,
  `AccessPolicy`, el adaptador de headers confiables y el `Proxy` de acceso
  para el login/autorización del actor autenticado.
- Los `Access Facts` devueltos por el servicio deben incluir lo necesario para
  distinguir identidad autenticada, rol, estado activo y resultado de acceso
  grueso.
- El rechazo de usuarios no autenticados se resuelve antes de entrar al
  microservicio mediante gateway; el rechazo de usuarios desactivados o de rol
  incorrecto se resuelve en `identity-access-service`.
- `HU-06` no introduce ownership nuevo sobre `LiveSession`, `Team`,
  `SessionParticipant`, `JoinContext` ni `JoinToken`.
- La validación de pertenencia al equipo y la autorización para entrar a una
  sesión activa se posponen explícitamente a `HU-07A`.
- La recuperación de estado y reconexión autorizada se posponen explícitamente
  a `HU-07B`.
- El enabler `DES-58` consume este slice como prerrequisito contractual, pero
  el PRD no especifica UX completa, navegación móvil ni almacenamiento local
  detallado fuera de la sesión autenticada mínima.
- Si el servicio expone una capacidad protegida específica para mobile, esta
  debe mapearse en `ProtectedCapability` y quedar cubierta por la matriz de
  `AccessPolicy`.
- La solución debe mantener la separación entre autorización gruesa de
  identidad y admisión final de runtime, que sigue siendo responsabilidad de
  `SessionOperations`.

## Testing Decisions

- Una buena prueba valida comportamiento observable y decisiones de acceso, no
  detalles internos de implementación ni estructura de clases.
- Deben probarse los módulos profundos que sostienen el slice:
  `IdentityProvisioningPolicy`, `AccessPolicy`, parsing/adaptación de rol
  desde headers confiables y el `Proxy` que restringe el flujo a
  `Participant`.
- Los handlers y queries que soportan bootstrap, perfil autenticado y chequeo
  de acceso deben cubrir camino válido, usuario desactivado, rol incorrecto y
  falta de identidad confiable.
- Las pruebas de integración deben demostrar el round-trip mínimo del flujo
  mobile: headers confiables válidos, provisioning exitoso, lectura del perfil
  autenticado y rechazo de acceso cuando el actor no es `Participant`.
- Las pruebas de API deben verificar el mapeo correcto de errores a respuestas
  HTTP para usuario desactivado, actor no autenticado y actor autenticado con
  rol no permitido.
- La prior art debe seguir el patrón ya existente en
  `identity-access-service`: pruebas unitarias de `AccessPolicy`,
  `IdentityProvisioningPolicy`, handlers de `AuthenticateUser` y pruebas de
  integración de endpoints autenticados por headers confiables.
- No deben escribirse pruebas que simulen validación JWT dentro del servicio,
  porque esa responsabilidad pertenece al `api-gateway`.
- Tampoco deben escribirse pruebas que mezclen `HU-06` con membresía de equipo,
  join a sesión o reconexión, porque eso cubriría otro bounded context o
  historias posteriores.

## Out of Scope

- Validación de membresía del participante respecto a un `Team` o una
  `LiveSession`.
- Emisión o consumo de `JoinToken` para admisión a sesión.
- Reconexión autorizada y recuperación de estado compartido.
- Sincronización multi-dispositivo del equipo.
- Implementación completa de UX del cliente móvil más allá del contrato de
  autenticación y acceso grueso.
- Decisiones específicas de navegación, storage local o packaging de Expo que
  no cambien el contrato de autenticación del backend.
- Revalidación de JWT dentro de microservicios distintos al `api-gateway`.
- Cualquier cambio de ownership sobre agregados de `SessionOperations`.

## Further Notes

- `DES-10` depende de `HU-01 / DES-5` y prepara el camino para
  `HU-07A / DES-11`, `HU-07B / DES-12` y el enabler `DES-58`.
- El canal de acceso de `HU-06` es la aplicación móvil de participantes en
  Expo/React Native, no la web operativa de administradores u operadores.
- El vocabulario del PRD debe mantenerse en términos de `User`, `Role`,
  `IdentityProviderSession`, `Access Facts`, `Post-Login Provisioning` y
  `AccessPolicy`.
- Si el equipo necesita una capacidad protegida explícita para el cliente
  móvil de participantes, debe agregarse como decisión visible de dominio y no
  como convención implícita de frontend.
