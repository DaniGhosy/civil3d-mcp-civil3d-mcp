# CLAUDE.md

Guía para trabajar en este repo. Complementa a `README.md` (setup, tabla de acciones) y a
`C_References/README.md` (DLLs) — no los duplica.

## Qué es esto

MCP de automatización para Autodesk Civil 3D. Dos procesos:

```
Asistente de IA ──stdio (MCP)── Servidor Node/TS ──TCP/JSON-RPC (8080)── Plugin C# dentro de Civil3D
```

- **Servidor MCP** (`src/`, TypeScript/Node): expone tools MCP, valida input con Zod, reenvía cada
  acción al plugin como un método JSON-RPC.
- **Plugin C#** (`plugin/Civil3dMcpPlugin/`, .NET 8.0-windows): corre dentro de Civil3D, recibe el
  JSON-RPC, ejecuta contra la API real de Civil3D/AutoCAD.

Esta sesión de Claude Code (desarrollo) **no tiene las herramientas `civil3d_*` cargadas** — esas
solo existen en la sesión de asistente de IA que el usuario conecta contra el plugin ya corriendo
dentro de Civil3D (`C3DMCPSTART`). Para probar un cambio contra un dibujo real, el usuario debe
recompilar el plugin, recargarlo (`NETLOAD`) y probarlo desde esa otra sesión — no se puede
verificar aquí más allá de que compile.

## Filosofía: primitivas, no comandos de negocio

En vez de un comando monolítico por tarea, se construyen primitivas atómicas reutilizables
(detectar, filtrar, leer propiedades, modificar, calcular) más comandos específicos por dominio.
Esto permite componer tareas no anticipadas de antemano combinando primitivas en el momento, en vez
de depender de que exista un comando exacto ya escrito. Las primitivas genéricas clave, todas en
`plugin/Civil3dMcpPlugin/GenericObjectCommands.cs` (tool `civil3d_object`):

- `resolve_location` — ubicar CUALQUIER punto por coordenadas, click interactivo del mouse, o
  relativo a un objeto de referencia + offset. Diseñado para componerse con comandos de creación
  existentes, no como un "crear cualquier cosa" monolítico.
- `get_properties` / `list_by_type` — introspección genérica vía reflexión
  (`GenericObjectCommands.SerializeSimpleProperties`, reutilizado en ~8 dominios distintos).
- `set_style_name` — motor de estilos universal: cualquier objeto con una propiedad `StyleName`
  escribible puede recibir un estilo por nombre; Civil3D resuelve el ObjectId internamente.
- `EnsureLayerId(db, tr, layerName, colorIndex?)` — crea la capa si no existe, devuelve su
  `ObjectId`. Consolida 3 copias casi idénticas (inline en SurfaceCommands.cs, helper privado en
  ProfileCommands.cs, y la ausencia total del patrón en GeometryCommands.cs — causa raíz del bug de
  `eKeyNotFound` reportado en pruebas en vivo). También expuesta como `ensure_layer`. Usarla siempre
  que se vaya a asignar `Entity.Layer` a un nombre que podría no existir todavía.
- `delete_entity`/`move_entity`/`copy_entity`/`rotate_entity`/`get_entity_bounds`/`set_layer`/
  `list_layers` — primitivas genéricas sobre cualquier `Entity` por handle (post-Mes 9). Cierran el
  ciclo de prueba/limpieza: antes de esto no había forma de deshacer objetos de prueba sin entrar
  manualmente a Civil3D.

## Módulo de lectura de planos y acciones TS-only sin plugin

Desde `civil3d_blocks`/`civil3d_quantity` (catálogo de lectura de planos, Fase 1), no todos los
dominios llaman al plugin. `civil3d_quantity` es el primer caso: sus dos acciones
(`count_by_category`, `generate_quantity_report`) son post-proceso puro en TS sobre datos que el
asistente ya extrajo (típicamente con `civil3d_blocks`) — no abren conexión al plugin y tienen
`requiresActiveDrawing: false`. No es un descuido ni un dominio a medio terminar: hay pasos del
pipeline de "leer un plano" (agrupar, sumar, exportar a Excel) que no necesitan otro round-trip a
Civil3D. El roadmap completo del catálogo (Módulos B–G del PDF original) sigue el mismo criterio:
lo que lee del dibujo va al plugin C#, lo que es heurística/agregación sobre datos ya leídos queda en
TS puro.

**`civil3d_plan_vision` (Módulo F) es un tercer proceso, no solo un tercer patrón.** Cuando no hay
dibujo vivo (PDF/imagen escaneada), el servidor TS invoca un subproceso Python (`plan-vision/`, ver
su propio `README.md`) vía `src/utils/PythonBridge.ts` — mismo principio arquitectónico que
`ConnectionManager.ts`/plugin C# (capa fina de despacho a un proceso separado, JSON por stdin/stdout
en vez de JSON-RPC por TCP), pero un proceso corto por llamada en vez de una conexión persistente,
porque no hay un servidor Python de larga duración al que conectarse. Visión clásica con OpenCV
(contornos + template matching multi-escala/rotación) y OCR con Tesseract — nivel de confianza, no
la exactitud de los dominios que sí leen la base de datos del dibujo. Todas sus acciones tienen
`requiresActiveDrawing: false`; es el único dominio del repo que nunca abre conexión al plugin C#.

## Patrón de dispatch

- **TS**: cada dominio de Civil3D es UN tool MCP con un campo discriminador `action` (no un tool por
  acción). Se define como `DomainToolDefinition` (`domain`, `actions` record, `exposures` array) en
  `src/tools/domainRuntime.ts`. Cada `*Domain.ts` bajo `src/tools/domains/` exporta una constante
  `..._DOMAIN_DEFINITION`. El registro central vive en `src/tools/register.ts`
  (`DOMAIN_DEFINITIONS`, el orden define el orden de discovery).
- **C#**: `CommandDispatcher.DispatchAsync` es un switch grande que mapea el string `method` del
  JSON-RPC a una llamada estática en un archivo `*Commands.cs`. Los handlers usan
  `CivilExecution.ReadAsync<T>`/`WriteAsync<T>` (marshalling al hilo principal de AutoCAD vía
  `ExecuteInCommandContextAsync`, envuelve una transacción; `WriteAsync` hace commit, `ReadAsync`
  no). Extracción de parámetros vía `PluginRuntime.GetRequired*`/`GetOptional*`. Errores como
  `JsonRpcDispatchException("CIVIL3D.CODE", "mensaje")`.
- **Buscar un objeto por nombre**: usar `CivilObjectLookup.FindByName<T>(IEnumerable<ObjectId> ids, ...)`
  (cualquier colección de IDs — respeta el scoping correcto: perfiles de UN alineamiento, red
  específica, etc.) o `CivilObjectLookup.FindEntityByName<T>(tr, db, name)` (escanea todo ModelSpace,
  solo para tipos que heredan de `Entity`). No reimplementar este loop — ya se consolidó en Mes 9
  después de encontrar 6 copias casi idénticas.

## Protocolo para API incierta de Civil3D (crítico, seguido desde Mes 1)

**Nunca iterar adivinanzas contra el compilador en loop.** Flujo obligatorio:
1. Investigar primero (WebSearch/WebFetch) — buscar el Autodesk Civil3D .NET Developer Guide, foros
   oficiales, o devblogs con firma de método citada explícitamente. Confianza alta = documentación
   oficial con firma; confianza media = foro con código real; confianza baja = solo nombre de
   namespace/clase sin firma → considerar dejarlo como stub sin intentar si la evidencia es
   demasiado delgada.
2. Implementar el mejor intento.
3. Correr **una sola** pasada de `dotnet build`.
4. Lo que el compilador rechace vuelve a un stub documentado (`Task.FromResult<object?>(new {
   status = "planned", note = "..." })`) citando la firma exacta intentada y el error exacto del
   compilador (CS-code incluido). Nunca un stub mudo sin nota — un stub sin nota es deuda técnica
   oculta (ver el caso de `add_breakline`/`add_boundary`/`extract_contours`, cerrado en Mes 9: eran
   stubs mudos desde meses atrás, sin ningún intento documentado).
5. **No repetir el ciclo build→ajustar→build para la misma pieza.** Si el primer intento falla,
   documentar y seguir — no hay una segunda vuelta de adivinanza en la misma sesión.

Reflection-loading los DLLs de Civil3D de forma standalone (fuera de AutoCAD) **no funciona** en
este entorno (`Assembly.LoadFrom`/`ReflectionOnlyLoadFrom` fallan con errores de módulo nativo,
confirmado). `dotnet build` (solo metadata) es la única verificación real disponible sin una sesión
de Civil3D en vivo.

## Callejones sin salida ya confirmados — no reintentar

- **Importar LandXML vía .NET**: confirmado imposible, solo COM (`IAeccSurfaces::ImportXML()`) o UI.
  Ver `plugin/Civil3dMcpPlugin/ImportExportCommands.cs`.
- **Redes a presión** (`PressurePipeCommands.cs`): `AeccPressurePipesMgd.dll` está referenciado y
  `PressurePipeNetwork` resuelve, pero el namespace real de `CivilDocumentPressurePipesExtension`
  nunca se confirmó (2 intentos fallidos). Próximo paso sugerido: Object Browser/ILSpy contra el DLL
  desde una sesión en vivo, no otra adivinanza a ciegas.
- **Crear Intersections/rotondas, ViewFrames/Sheets/MatchLines**: sin API .NET, confirmado por foro
  de Autodesk — son operaciones de UI/COM únicamente.
- **GIS/SHP export**: necesita el ensamblado núcleo de AutoCAD Map 3D (`AcMapMgd.dll` o similar), no
  identificado claramente entre los DLLs satélite ya copiados (`AcMap*Mgd.dll` son módulos de
  buffer/overlay/spatial reference, no el núcleo).
- **Reportes del Toolbox de Civil3D**: sin API .NET orientada a objetos — son scripts/plantillas de
  tecnología más antigua. Reportes propios combinando primitivas ya existentes sí son posibles hoy
  (`compute_contour_volume`, `report_quantities`, etc.).
- **`TinSurface.ExtractMinorContours`/`ExtractMajorContours`** (Mes 9): NO toman un intervalo/capa
  directo como parecía razonable adivinar — el compilador reveló que requieren
  `(SurfaceExtractionSettingsType, ContourSmoothingType, int smoothFactor)`. Investigar ese objeto
  de configuración antes de reintentar `extract_contours`.
- **`AddWallBreaklines`/`AddProximityBreaklines`** (Mes 9): no aceptan la misma forma de 4 argumentos
  que `AddStandardBreaklines` (que sí funciona). Firma real sin confirmar.

## Hangs conocidos sin resolver (no adivinar un fix sin diagnóstico en vivo)

Confirmado en pruebas en vivo (Civil3D 2026, dibujo real) — dos comandos cuelgan 120s (timeout del
lado del servidor MCP, no una excepción):

- **`civil3d_object.get_properties` sobre el handle de una TinSurface**: `SerializeSimpleProperties`
  en sí NO enumera colecciones (invoca cada getter una vez, descarta lo que no sea un tipo simple sin
  iterarlo) — el hang viene de que UN getter individual de `TinSurface` bloquea de forma síncrona al
  invocarse. **Mitigación aplicada** (no root-cause fix): `GetObjectPropertiesAsync` detecta `Surface`
  y usa un set curado de campos ya confirmado rápido (`Name`/`Handle`/`Layer`/tipo TIN-o-Grid) en vez
  de reflexión abierta. Cualquier otro tipo de objeto sigue usando reflexión completa sin problema.
- **`civil3d_survey.list_figure_styles`**: accede directo a `civilDoc.Styles.SurveyFigureStyles` sin
  ningún guard. Investigación (WebSearch + intento de WebFetch a la documentación oficial) no
  encontró un guard/propiedad confirmado para detectar "sin survey database activa" antes de acceder.
  **Sin mitigar** — no se tocó el código, cualquier cambio sin diagnóstico real sería otra
  adivinanza. Pendiente: probar en un dibujo CON base de datos de survey activa vs. uno SIN ella para
  aislar el disparador real (ver `docs/TESTING_CHECKLIST.md`).

**Por qué no un timeout por-hilo para ninguno de los dos**: el objeto vive dentro de una transacción
con lock de documento de un solo hilo (`CivilExecution.ReadAsync`/`WriteAsync` vía
`ExecuteInCommandContextAsync`) — abandonar la espera del lado de C# con un `Task.Run`+timeout no
libera el lock ni detiene la llamada real que sigue bloqueada dentro de él; el riesgo es dejar el
documento en un estado inconsistente para llamadas subsecuentes. Un fix real necesita primero
diagnosticar EXACTAMENTE qué getter/miembro cuelga, contra una sesión en vivo con un debugger
adjunto — no algo que se pueda hacer a ciegas desde esta sesión de desarrollo.

## Deuda técnica conocida y aceptada (no ocultada, decidida a propósito)

- 7 archivos (`AssemblyCommands`, `CorridorCommands`, `GradingCommands`, `SampleLineCommands` x2,
  `ProfileViewCommands`, `SheetProductionCommands` x2) reimplementan manualmente un scan de
  ModelSpace en vez de usar `GenericObjectCommands.ListObjectsByTypeAsync` — cada uno filtra además
  por una relación específica del dominio, no solo por tipo. Colapsarlos a la primitiva genérica
  arriesgaba introducir un bug sutil por un ahorro de líneas que no se consideró que valiera la pena
  a esta altura del proyecto (Mes 9, decisión consciente).
- `ToolDomain` (en `toolMetadata.ts`) tiene reuso cosmético: `civil3d_pressure_pipe` comparte
  `"pipe"` con `civil3d_pipe`; `civil3d_data_shortcut`/`civil3d_import_export` comparten `"project"`.
  No afecta el `toolName` (único, es lo que importa para discovery) — no se cambió.
- `ToolCatalogEntry.status` (`"implemented" | "planned"`) no se usa a nivel de acción individual —
  todos los 20 tools reportan `"implemented"` aunque tengan acciones `planned` mezcladas adentro.
  La fuente de verdad real es la tabla de `README.md` y la descripción de cada tool exposure, no
  este campo.

## Dónde está todo

- `src/tools/domains/*.ts` — un archivo por dominio, exporta `..._DOMAIN_DEFINITION`.
- `src/tools/register.ts` — registro central, orden = orden de discovery.
- `src/tools/toolMetadata.ts` — tipos compartidos (`ToolDomain`, `ToolCapability`, etc.).
- `plugin/Civil3dMcpPlugin/*Commands.cs` — un archivo por dominio, handlers estáticos.
- `plugin/Civil3dMcpPlugin/CommandDispatcher.cs` — switch central método→handler.
- `plugin/Civil3dMcpPlugin/CivilObjectLookup.cs`, `GenericObjectCommands.cs` — primitivas
  compartidas, ver arriba.
- `C_References/` — DLLs de Civil3D/AutoCAD (gitignored). Ya tiene ~30 `*Mgd.dll` copiados de la
  instalación — revisar ahí antes de asumir que falta un DLL para un dominio nuevo.
- `docs/TESTING_CHECKLIST.md` — checklist manual por dominio para correr contra un dibujo real desde
  la sesión de asistente conectada al plugin en vivo (no hay suite de tests automatizada — nunca la
  hubo en este proyecto, la verificación siempre fue build + prueba manual en vivo).
- `README.md` — tabla completa de tools/acciones con estado real vs. planned. Es la fuente de verdad
  para "¿esto ya funciona?" — mantenerla sincronizada cuando una acción cambia de estado.

## Historial

El proyecto siguió un plan maestro de 9 meses (Mes 1 = núcleo + categorías base, Meses 2-8 = un
dominio nuevo por mes, Mes 9 = esta consolidación). Cada mes se planificó con el flujo de plan mode
del harness antes de implementar. El detalle mes a mes vive en el historial de conversación, no en
este archivo — este documento describe el estado actual y las reglas de trabajo, no la cronología.
