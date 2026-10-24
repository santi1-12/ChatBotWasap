# Feature Specification: POC de Bot de WhatsApp Business

**Feature Branch**: `Not created by this specification command`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Crear una POC funcional de un Bot de WhatsApp Business para ser presentada a un cliente, con conexión oficial de Meta, interacción con IA y visualización administrativa de conversaciones."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Conectar WhatsApp Business (Priority: P1)

Como administrador, quiero vincular un número empresarial mediante el flujo oficial de Meta
para que el sistema pueda recibir y responder mensajes reales de WhatsApp.

**Why this priority**: La demostración depende de probar una integración empresarial real,
no una conexión simulada ni un número ingresado manualmente.

**Independent Test**: Completar el proceso autorizado de Meta y comprobar en el Panel que se
muestra el número vinculado, sus estados y la fecha de conexión.

**Acceptance Scenarios**:

1. **Given** que el administrador aún no tiene un número conectado, **When** abre
   Configuración de WhatsApp e inicia la conexión, **Then** puede autorizar mediante Meta,
   seleccionar o crear una cuenta de WhatsApp Business y seleccionar o registrar un número,
   verificar su propiedad y completar la configuración oficial.
2. **Given** que la autorización y configuración oficial terminaron correctamente, **When**
   el administrador vuelve al Panel, **Then** ve el número, el nombre asociado cuando exista,
   el estado conectado, el estado del bot y la fecha de conexión.
3. **Given** que el administrador solo ha escrito un número en un formulario, **When** aún no
   se completó la autorización y verificación requeridas por Meta, **Then** el sistema no lo
   muestra como conectado ni permite activarlo.
4. **Given** que el flujo de conexión está pendiente o falla, **When** el administrador
   consulta la configuración, **Then** el Panel muestra respectivamente «En proceso de
   configuración» o «Error de conexión» sin presentarlo como conectado.

---

### User Story 2 - Automatizar respuestas a clientes (Priority: P1)

Como cliente, quiero enviar texto, audio o una fotografía al número empresarial y recibir
una respuesta pertinente para poder completar una interacción real con el bot.

**Why this priority**: La recepción, procesamiento y respuesta demuestran la función principal
del producto y su utilidad para el cliente.

**Independent Test**: Con un número conectado y el bot activo, enviar cada tipo de contenido
desde WhatsApp y verificar que el resultado (respuesta o no-respuesta) cumple el modo elegido,
respeta la política de mensajería y queda conservado en la conversación del Panel.

**Acceptance Scenarios**:

1. **Given** que el número está conectado y el bot activo, **When** un cliente envía un mensaje
   de texto dentro de la ventana de mensajería permitida, **Then** el sistema aplica el modo
   configurado, responde mediante una respuesta frecuente o IA cuando corresponda y registra
   ambos mensajes y el resultado del procesamiento.
2. **Given** que el número está conectado y el bot activo, **When** un cliente envía una nota
   de voz o audio, **Then** el sistema obtiene el contenido de audio, genera una transcripción,
   aplica sobre esa transcripción el modo configurado, responde cuando corresponda por
   WhatsApp y muestra la transcripción y el resultado en el historial.
3. **Given** que el número está conectado y el bot activo, **When** un cliente envía una
   fotografía, **Then** el sistema obtiene y conserva la imagen, aplica el modo configurado
   para decidir si analiza/genera una respuesta mediante IA y registra el resultado en el
   historial.
4. **Given** que el bot está desactivado, **When** un cliente envía un mensaje, **Then** el
   sistema no genera ni envía una respuesta automática del bot y la interacción recibida sigue
   disponible para consulta administrativa.

---

### User Story 3 - Consultar y controlar conversaciones (Priority: P2)

Como administrador, quiero revisar las conversaciones, sus mensajes, el estado de atención y el
responsable para entender qué interacción tuvo cada cliente y controlar la automatización.

**Why this priority**: La visibilidad administrativa permite demostrar que el sistema recibió
y procesó las interacciones, y permite pausar respuestas automáticas.

**Independent Test**: Abrir Conversaciones después de una interacción y comprobar el contacto,
la hora del último mensaje, el ciclo de vida, el modo de atención, el responsable y el historial
visualmente diferenciado.

**Acceptance Scenarios**:

1. **Given** que hay mensajes recibidos de una integración de la empresa, **When** el administrador
   abre Conversaciones, **Then** puede ver el contacto, la fecha y hora del último mensaje, el
   ciclo de vida, el modo de atención, el responsable y el historial de cada conversación.
2. **Given** que el historial contiene mensajes entrantes y salientes de distintos tipos,
   **When** el administrador consulta la conversación, **Then** distingue mensajes del cliente
   del bot, del administrador y del sistema; distingue texto, audio y fotografías, ve la
   transcripción disponible y puede ver la fotografía recibida.
3. **Given** que una respuesta fue generada mediante IA, **When** el administrador consulta el
   historial, **Then** la respuesta aparece registrada como mensaje enviado por el bot.
4. **Given** que el bot está activo, **When** el administrador lo desactiva, **Then** el Panel
   muestra el nuevo estado y dejan de enviarse respuestas automáticas; **When** lo activa de
   nuevo, **Then** el estado visible cambia a activo.
5. **Given** que una conversación está abierta en modo Bot, **When** un administrador la toma,
   **Then** pasa a modo Humano, se registra el administrador y el bot queda pausado exclusivamente
   para esa conversación.
6. **Given** que una conversación está en modo Humano, **When** el administrador responde desde
   el Panel, **Then** el mensaje queda registrado como enviado por Admin, se somete a las reglas
   de mensajería de Meta y conserva su resultado de entrega.
7. **Given** que una conversación está en modo Humano, **When** el administrador selecciona
   «Devolver al bot», **Then** pasa a modo Bot y las nuevas interacciones pueden volver a procesarse
   automáticamente.
8. **Given** que una conversación está abierta o en modo Humano, **When** el administrador la
   cierra, **Then** queda en ciclo de vida Cerrada; **When** llega un mensaje nuevo y el bot está
   habilitado, **Then** la misma conversación pasa a Abierta + Bot sin crear otro historial.
9. **Given** que el bot detecta una petición de persona, una incapacidad para resolver, una regla
   de negocio aplicable o un error bloqueante, **When** decide transferir, **Then** pasa la
   conversación a Humano, registra el motivo y no genera una respuesta automática concurrente.
10. **Given** que una conversación pertenece a una empresa, **When** un administrador autorizado
    la consulta o modifica, **Then** no puede ver ni alterar conversaciones, mensajes, media o
    configuración de otra empresa.

---

### User Story 4 - Administrar respuestas frecuentes (Priority: P2)

Como administrador, quiero configurar respuestas frecuentes y elegir cómo se combinan con la
IA para que el bot responda automáticamente de manera consistente a mensajes conocidos.

**Why this priority**: Las respuestas configuradas permiten controlar respuestas repetidas y
demostrar que el administrador puede ajustar el comportamiento del bot sin cambiar su código.

**Independent Test**: Crear una respuesta activa con una expresión asociada, enviar un mensaje
que la incluya y comprobar que se usa el texto configurado; cambiar el modo y confirmar el
comportamiento cuando no exista coincidencia.

**Acceptance Scenarios**:

1. **Given** que el administrador abre Respuestas Frecuentes, **When** consulta la lista,
   **Then** puede ver las respuestas, su estado, prioridad, categoría cuando exista y fechas de
   creación y modificación desde Configuración → Respuestas Frecuentes.
2. **Given** que el administrador crea o edita una respuesta frecuente, **When** proporciona
   una pregunta, intención o descripción, una o varias palabras o expresiones, texto de
   respuesta, prioridad y, opcionalmente, categoría, **Then** los datos quedan guardados y
   disponibles en la lista.
3. **Given** que existe una respuesta frecuente, **When** el administrador la activa,
   desactiva o elimina, **Then** el Panel refleja el cambio y las respuestas desactivadas o
   eliminadas dejan de participar en la búsqueda.
4. **Given** que el modo elegido es «Respuestas configuradas + Inteligencia Artificial como
   fallback», **When** un mensaje recibido coincide con una respuesta frecuente activa,
   **Then** el bot envía el texto configurado y no solicita una respuesta de fallback a la IA.
5. **Given** que el modo elegido es «Respuestas configuradas + Inteligencia Artificial como
   fallback», **When** ningún registro activo coincide suficientemente, **Then** el sistema
   procesa el mensaje mediante IA y envía la respuesta generada.
6. **Given** que el modo es «Solo respuestas configuradas» y no existe coincidencia,
   **When** llega un mensaje, **Then** no se envía una respuesta generada por IA.
7. **Given** que el modo es «Inteligencia Artificial», **When** llega un mensaje,
   **Then** se procesa mediante IA sin utilizar respuestas frecuentes para generar la respuesta.
8. **Given** que una respuesta fue contestada desde una regla frecuente, **When** el
   administrador revisa la conversación, **Then** puede ver el texto enviado y que provino de
   una respuesta configurada.

---

### Edge Cases

- Si el usuario cancela o no completa la autorización de Meta, la integración no se activa y el
  Panel conserva un estado que no implica conexión exitosa.
- Si falla la autorización, verificación, registro del número o configuración de webhooks, el
  Panel muestra «Error de conexión» y el sistema no declara el número conectado.
- Si Meta entrega un mensaje repetido, el cliente envía contenido no admitido o el medio no se
  puede obtener, el sistema evita respuestas duplicadas o fabricadas y registra un resultado
  visible para el administrador.
- Si el servicio de IA no puede procesar el texto, la transcripción o la imagen, la interacción
  queda en el historial y el fallo se puede identificar; no se presenta como respuesta exitosa.
- Si el bot está desactivado durante la recepción de un mensaje, el mensaje se conserva pero
  no se procesa para una respuesta automática.
- Si un audio no se puede transcribir o una imagen no se puede visualizar, el Panel indica que
  el contenido o análisis no está disponible.
- Si hay varias respuestas frecuentes activas aplicables, el sistema selecciona la de mayor
  prioridad; si empatan, utiliza un orden estable y consistente.
- Si no hay coincidencia y el modo es «Solo respuestas configuradas», el sistema registra el
  mensaje sin enviar una respuesta automática generada por IA.
- Si una respuesta frecuente o una respuesta de IA no puede enviarse conforme a la ventana o
  política vigente de Meta, el sistema no la presenta como enviada ni la utiliza como sustituto
  de una plantilla oficial requerida.
- Si la respuesta configurada no tiene una expresión asociada válida, no participa en la
  búsqueda hasta que el administrador la corrija.
- Si falla el guardado de una respuesta frecuente, el Panel informa el error y no indica que
  el cambio se haya guardado.
- Si Bot y Humano compiten por procesar una interacción, Humano tiene prioridad; la respuesta
  automática debe revalidar el modo y la versión de la conversación justo antes de entrar al
  outbox.
- Si dos acciones administrativas compiten, solo una transición válida puede aplicarse; la
  operación perdedora recibe un conflicto seguro y no modifica el historial.
- Si una conversación Cerrada recibe un mensaje nuevo con el bot habilitado, se reabre como
  Abierta + Bot conservando el mismo `ConversationId`; si el bot está deshabilitado, se reabre
  sin iniciar procesamiento automático y queda disponible para atención humana.
- Una transferencia automática a Humano registra un motivo auditable: solicitud del cliente,
  incapacidad del bot, regla de negocio o error bloqueante.
- Una respuesta manual se conserva como mensaje de Admin y nunca se presenta como enviada si
  Meta la rechaza, la ventana expiró o una plantilla es obligatoria.
- La expiración de un archivo de audio o imagen no elimina el mensaje ni su metadata permitida;
  la transcripción disponible se conserva según la política de retención.
- Los eventos `history`, `smb_app_state_sync` y `smb_message_echoes` de Coexistence no se
  procesan en esta POC; deben ignorarse de forma segura y dejar un punto de extensión para una
  fase posterior.
- La POC no implementa equipos ni colas, pero el diseño permite múltiples administradores por
  empresa y asignación simple mediante un administrador responsable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El Panel Administrativo MUST ofrecer una sección de configuración de WhatsApp
  desde la que el administrador pueda iniciar y consultar la conexión.
- **FR-002**: La conexión MUST utilizar únicamente WhatsApp Business Platform / Cloud API
  oficial de Meta, siguiendo autorización, selección o creación de WABA, selección o registro
  del número empresarial, verificación de propiedad y configuración requerida por Meta.
- **FR-003**: El proceso de vinculación MUST utilizar Meta Embedded Signup como mecanismo
  preferido. El ingreso de un número en un formulario, sin completar los pasos oficiales,
  MUST NOT establecer una conexión activa.
- **FR-004**: El sistema MUST mostrar los estados «No conectado», «En proceso de configuración»,
  «Conectado» y «Error de conexión» de forma que el administrador pueda distinguirlos.
- **FR-005**: Para una integración conectada, el Panel MUST mostrar como mínimo el número
  empresarial, el nombre asociado cuando esté disponible, el estado de conexión, el estado del
  bot y la fecha de conexión.
- **FR-006**: El administrador MUST poder activar y desactivar el procesamiento automático del
  bot, y el Panel MUST mostrar el estado vigente.
- **FR-007**: Con el bot activo, el sistema MUST recibir mensajes de texto, notas de voz o
  audio y fotografías enviados al número conectado y conservarlos dentro de la conversación
  de la empresa correspondiente.
- **FR-008**: Para texto, el sistema MUST aplicar el modo de respuesta configurado: utilizar la
  respuesta frecuente aplicable en los modos que la habilitan, utilizar IA en «Solo IA» o como
  fallback únicamente en «Respuestas configuradas + Inteligencia Artificial como fallback»,
  y no utilizar IA cuando el modo «Solo respuestas configuradas» no encuentre coincidencia.
- **FR-009**: Para audio, el sistema MUST obtener el contenido recibido, producir una
  transcripción, aplicar sobre ella las mismas reglas del modo de respuesta configurado y
  enviar una respuesta solo cuando el modo produzca una y las reglas de mensajería lo permitan.
- **FR-010**: Para fotografías, el sistema MUST obtener y conservar la imagen para consulta.
  Solo MUST analizarla y generar una respuesta mediante IA si el modo configurado permite IA;
  el modo «Solo respuestas configuradas» MUST NOT invocar IA por una fotografía sin texto
  coincidente.
- **FR-011**: El sistema MUST proporcionar al administrador una sección de conversaciones con
  el contacto o número del cliente, fecha y hora del último mensaje, ciclo de vida, modo de
  atención, responsable e historial.
- **FR-012**: El historial MUST diferenciar mensajes del cliente, del bot, de un administrador y
  del sistema, además de identificar si cada mensaje contiene texto, audio o fotografía.
- **FR-013**: El Panel MUST mostrar la transcripción disponible para mensajes de audio y la
  imagen recibida para mensajes de fotografía.
- **FR-014**: Las respuestas generadas mediante IA MUST formar parte del historial de la
  conversación.
- **FR-015**: Cuando el bot esté desactivado, el sistema MUST conservar los mensajes entrantes
  para consulta, pero MUST NOT generar ni enviar respuestas automáticas.
- **FR-016**: El sistema MUST comunicar fallos de conexión o procesamiento de forma visible
  para el administrador y MUST NOT presentar una conexión o respuesta fallida como exitosa.
- **FR-017**: La demostración MUST permitir verificar visualmente que el número está conectado
  y que un mensaje real enviado a ese número llega al sistema y queda procesado y registrado.
- **FR-018**: La POC MUST soportar un único número activo por empresa para el flujo operativo
  inicial, pero MUST modelar desde ahora una empresa/tenant, sus membresías y sus integraciones
  sin asumir un singleton global. `IntegrationId` puede ser la frontera técnica de los datos de
  una integración, pero no sustituye a la frontera de empresa ni impide futuras integraciones
  por empresa.
- **FR-019**: La solución MUST cumplir la constitución del proyecto, que gobierna la
  arquitectura, la separación de responsabilidades y el tratamiento seguro de credenciales.
- **FR-020**: El Panel Administrativo MUST ofrecer la sección Configuración → Respuestas
  Frecuentes y permitir consultar, crear, editar, activar, desactivar y eliminar respuestas
  frecuentes desde ella.
- **FR-021**: Cada respuesta frecuente MUST permitir definir una pregunta, intención o
  descripción, una o varias palabras o expresiones asociadas, texto de respuesta, prioridad y
  una categoría opcional; el sistema MUST registrar fechas de creación y modificación.
- **FR-022**: Las respuestas frecuentes MUST persistirse en la base de datos y administrarse
  exclusivamente mediante WhatsAppBot.Api; WhatsAppBot.Admin MUST NOT acceder directamente
  a SQL Server.
- **FR-023**: El administrador MUST poder elegir entre «Solo respuestas configuradas»,
  «Respuestas configuradas + Inteligencia Artificial como fallback» e «Inteligencia Artificial».
  El modo predeterminado MUST ser «Respuestas configuradas + Inteligencia Artificial como
  fallback».
- **FR-024**: Para los modos que utilizan respuestas frecuentes, Business MUST consultar las
  respuestas activas primero. Si existe una coincidencia suficiente, MUST usar la respuesta
  configurada; si no existe y el modo permite fallback, MUST procesar el mensaje mediante IA.
- **FR-025**: Cuando varias respuestas frecuentes activas coincidan, el sistema MUST elegir la
  de mayor prioridad y MUST resolver empates de forma estable y consistente.
- **FR-026**: Las respuestas frecuentes MUST ser contenido y reglas propios del bot y MUST
  NOT confundirse con las plantillas oficiales de mensajes de WhatsApp Business de Meta.
- **FR-027**: El historial de conversación MUST registrar las respuestas frecuentes enviadas e
  identificar que su origen fue una respuesta configurada.
- **FR-028**: El sistema MUST evaluar texto de mensajes recibidos y, para audio, su
  transcripción disponible al buscar respuestas frecuentes; si no hay coincidencia suficiente,
  el modo seleccionado MUST determinar si procede una respuesta de IA o si no se responde
  automáticamente.
- **FR-029**: Si falla el guardado o actualización de una respuesta frecuente, el Panel MUST
  informar el error y MUST NOT mostrar el cambio como guardado exitosamente.
- **FR-030**: Antes de enviar cualquier respuesta frecuente o generada por IA, el sistema MUST
  verificar que el mensaje esté permitido por la ventana y las reglas vigentes de WhatsApp
  Business. Para la POC, la demostración MUST limitarse a conversaciones iniciadas por el
  cliente y respuestas dentro de la ventana de atención permitida. Si Meta requiere una
  plantilla oficial, el sistema MUST NOT sustituirla por contenido FAQ o IA y MUST registrar
  un resultado visible que no indique envío exitoso.
- **FR-031**: Cada conversación MUST mantener separadas las dimensiones `AttentionMode`, con
  valores `Bot` y `Humano`, y `LifecycleStatus`, con valores `Abierta` y `Cerrada`; MUST NOT
  representar las tres condiciones en un único enum.
- **FR-032**: Mientras `AttentionMode` sea `Humano`, el bot MUST permanecer pausado para esa
  conversación. La prioridad de procesamiento MUST ser `Humano > Bot`, incluso si existe una
  tarea automática pendiente.
- **FR-033**: El Panel MUST permitir tomar una conversación, pasarla a Humano, responder
  manualmente, devolverla al bot, cerrarla y reabrirla; cada acción MUST informar un resultado
  seguro y conservar quién actuó y cuándo.
- **FR-034**: El bot MUST poder transferir automáticamente una conversación a Humano cuando el
  cliente pida una persona, el bot no pueda resolver, una regla de negocio lo determine o un
  error impida continuar. El motivo MUST quedar modelado y auditable.
- **FR-035**: Una conversación Cerrada que reciba un mensaje nuevo MUST conservar su identidad y
  pasar a Abierta + Bot cuando el bot esté habilitado; si el bot está deshabilitado, MUST quedar
  abierta sin respuesta automática.
- **FR-036**: Las transiciones de atención, ciclo de vida, asignación y respuestas manuales MUST
  registrar actor, origen, fecha, motivo cuando corresponda y resultado de la operación.
- **FR-037**: La solución MUST modelar `Tenant`/Empresa, membresías, múltiples administradores
  por empresa, integraciones asociadas a empresa y aislamiento de configuración, conversaciones,
  mensajes y multimedia. La POC no requiere equipos ni colas.
- **FR-038**: Audio e imagen MUST almacenarse mediante referencias privadas con acceso autorizado,
  expiración inicial de 30 días y retención configurable a futuro. La expiración MUST conservar
  el mensaje, metadata permitida y transcripción disponible.
- **FR-039**: Coexistence MUST soportar en esta POC la conexión y los mensajes nuevos. Los eventos
  `history`, `smb_app_state_sync`, `smb_message_echoes`, la sincronización de contactos y la
  recuperación histórica MUST quedar fuera de la ejecución actual y documentados como fase
  posterior.
- **FR-040**: El modelo de mensajes y multimedia MUST permitir incorporar posteriormente video,
  documentos, ubicación, contactos y otros tipos sin rediseñar el flujo principal; esos tipos no
  forman parte de la validación funcional actual.
- **FR-041**: Para `PhoneNumberId`, el sistema MUST usar el valor recibido cuando exista y, si
  falta, descubrirlo mediante el WABA; solo puede continuar ante una coincidencia inequívoca y
  nunca elegir arbitrariamente. Coexistence MUST usar `Meta:CoexistenceEmbeddedSignupConfigId`
  cuando exista y hacer fallback a `Meta:EmbeddedSignupConfigId`; Standard usa
  `Meta:EmbeddedSignupConfigId`.

### Key Entities

- **Tenant/Empresa**: Frontera de propiedad y autorización que agrupa administradores,
  integraciones, configuración, conversaciones, mensajes y multimedia.
- **Membresía**: Relación entre un administrador y una empresa, con estado de acceso; la POC no
  requiere todavía un sistema complejo de roles.
- **Integración de WhatsApp**: La conexión autorizada de una empresa con WhatsApp Business;
  incluye número, nombre disponible, modalidad Standard/Coexistence, estados de conexión y bot,
  y fecha de conexión.
- **Cuenta de WhatsApp Business**: La cuenta empresarial seleccionada o creada durante la
  autorización oficial de Meta.
- **Conversación**: El conjunto de mensajes asociados con un cliente y una integración de una
  empresa, junto con el ciclo de vida, el modo de atención, el responsable, la versión de
  concurrencia y la fecha del último mensaje.
- **Mensaje**: Una interacción entrante o saliente con actor, origen, fecha y hora, tipo de
  contenido y datos disponibles para mostrar o procesar.
- **Evento de atención**: Registro auditable de toma, transferencia automática, devolución al bot,
  cierre, reapertura, asignación o respuesta administrativa.
- **Contenido multimedia**: Audio o fotografía recibidos durante la POC, junto con la referencia
  privada, estado de disponibilidad, expiración, transcripción o análisis cuando esté disponible;
  el modelo queda abierto a futuros tipos multimedia.
- **Estado del bot**: Indica si se encuentra habilitado o deshabilitado para procesamiento
  automático.
- **Respuesta frecuente**: Regla administrable propia del bot, con pregunta, intención o
  descripción, expresiones asociadas, texto de respuesta, prioridad, categoría opcional,
  estado activo o desactivado y fechas de creación y modificación.
- **Modo de respuesta**: Preferencia que determina si el bot usa solo respuestas configuradas,
  respuestas configuradas con fallback de IA o únicamente IA.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En una demostración con acceso válido a Meta, el administrador puede completar
  el flujo oficial de conexión y confirmar en el Panel el número vinculado, el estado y la fecha
  de conexión en una sola sesión.
- **SC-002**: En pruebas controladas, los tres tipos de mensaje requeridos (texto, audio y
  fotografía) aparecen en la conversación correspondiente y el resultado cumple el modo
  seleccionado y la política vigente: se registra una respuesta enviada solo cuando ese modo
  produce una respuesta y Meta permite el envío; de lo contrario se registra el motivo de
  no-respuesta sin indicar éxito.
- **SC-003**: El 100% de los mensajes de audio completados satisfactoriamente muestran su
  transcripción en el historial; cada fotografía recibida correctamente puede abrirse desde el
  historial.
- **SC-004**: En pruebas de demostración, el 100% de las respuestas generadas mediante IA
  aparecen en el historial de la conversación asociada.
- **SC-005**: En una prueba con el bot desactivado, ningún mensaje recibido produce respuesta
  automática y el mensaje se conserva en el Panel.
- **SC-006**: En cinco repeticiones de una prueba con integración, estado del bot y conversación
  reciente previamente preparados, un administrador puede identificar correctamente los tres
  datos en menos de 60 segundos en cada repetición; el procedimiento y los datos iniciales se
  documentan en quickstart.md.
- **SC-007**: En pruebas de autorización cancelada o fallida, el sistema nunca presenta la
  integración como conectada o activa.
- **SC-008**: Un administrador puede completar las operaciones de consulta, creación,
  edición, activación, desactivación y eliminación de una respuesta frecuente desde el Panel;
  los cambios guardados se reflejan en la lista y en su fecha de modificación.
- **SC-009**: Con el modo predeterminado, una coincidencia con una respuesta activa produce
  el texto configurado, y un mensaje sin coincidencia produce una respuesta de IA cuando esta
  está disponible.
- **SC-010**: En los tres modos de respuesta, las pruebas confirman que el bot sigue el
  comportamiento seleccionado para coincidencias y ausencia de coincidencia.
- **SC-011**: En todas las pruebas de respuestas frecuentes, los registros inactivos o
  eliminados no se utilizan, y las coincidencias múltiples seleccionan la prioridad mayor.
- **SC-012**: El 100% de las respuestas originadas por una regla frecuente se registran en la
  conversación con su origen identificable.
- **SC-013**: En una prueba de atención humana, tomar, responder, devolver al bot, cerrar y
  reabrir una conversación conserva el historial, registra el actor y nunca permite respuestas
  concurrentes de Bot y Humano.
- **SC-014**: En una prueba con dos empresas y varios administradores, ningún administrador puede
  consultar o modificar datos fuera de la empresa autorizada, y las conversaciones conservan su
  `TenantId` e `IntegrationId`.
- **SC-015**: En una prueba de retención, los archivos de audio e imagen dejan de estar
  disponibles después de 30 días o la política configurada, mientras el mensaje, metadata
  permitida y transcripción disponible permanecen consultables.
- **SC-016**: En una prueba Coexistence, la conexión y los mensajes nuevos siguen el flujo POC;
  los eventos históricos y `smb_*` no se procesan y no generan respuestas fabricadas.

## Assumptions

- La demostración dispone de una aplicación de Meta configurada y de las autorizaciones,
  permisos y condiciones de cuenta necesarios para completar el onboarding oficial y operar
  webhooks con un número empresarial.
- El administrador que utiliza la POC tiene acceso válido al Panel Administrativo; la creación
  de cuentas administrativas y la administración avanzada de permisos no forman parte de esta
  especificación.
- La respuesta mediante IA, transcripción de audio y análisis de fotografías dependen de que
  los servicios necesarios estén disponibles; los fallos se reflejan en la conversación y no se
  representan como éxito.
- La POC puede operar inicialmente con un número activo por empresa para la demostración, pero no
  debe convertir esa limitación operativa en un singleton global. No incluye equipos, colas,
  facturación, reportería avanzada, campañas, CRM completo ni automatizaciones empresariales
  avanzadas.
- Cada administrador pertenece a una o más empresas mediante membresías autorizadas. La primera
  versión usa asignación simple con `AssignedAdminId`; no implementa equipos ni colas.
- La respuesta manual de Admin es inicialmente texto; audio e imagen forman parte del inbound de
  la POC y su procesamiento, no de un compositor outbound multimedia.
- Una respuesta frecuente se considera aplicable cuando el texto del cliente o la
  transcripción disponible contiene una de sus expresiones asociadas, sin distinguir
  mayúsculas y minúsculas; si hay varias coincidencias, se usa la prioridad mayor. La
  descripción de intención ayuda al administrador a definir la regla, pero no implica una
  clasificación semántica adicional fuera de la coincidencia configurada.
- Para fotografías, si no hay texto disponible que permita evaluar una expresión asociada, el
  modo seleccionado determina el uso de IA o la ausencia de respuesta automática.
- Las operaciones de administración de respuestas frecuentes se realizan a través de
  WhatsAppBot.Api; el Panel no consulta ni modifica SQL Server directamente.
- Las reglas de arquitectura, seguridad y desarrollo se rigen por la constitución del proyecto
  y no se redefinen en esta especificación.
