# Checklist de testing end-to-end

Creado en Mes 9 (S2), actualizado tras la ronda de bugs de pruebas en vivo (catálogo post-Mes 9) —
las anotaciones en **negrita** marcan qué cambió en esa ronda.

Esta guía se corre desde la sesión de asistente de IA conectada al plugin ya cargado en Civil3D
(`C3DMCPSTART`), **no** desde esta sesión de desarrollo de Claude Code — esa sesión no tiene las
herramientas `civil3d_*` cargadas. Reemplaza los nombres entre `<>` por objetos reales de tu dibujo
del espigón (o el dibujo de prueba que uses). Solo se listan acciones **reales** — las `(planned)`
no tienen nada que probar todavía.

Recomendación de orden: primero `civil3d_drawing` → `info` para confirmar conexión, luego el resto
en cualquier orden. Marca cada fila al probarla; si algo falla de forma inesperada (no un "planned"
documentado, sino un REAL que debería funcionar y no lo hace), es justo el tipo de hallazgo que
Mes 9 S1 busca cerrar — repórtalo con el mensaje de error completo.

## civil3d_drawing
- [ ] `info` — sin parámetros. Esperado: nombre del dibujo, ruta, unidades.
- [ ] `settings` — sin parámetros.
- [ ] `list_object_types` — sin parámetros. Esperado: lista de tipos de entidad presentes.
- [ ] `get_selected` — con algo seleccionado en Civil3D antes de llamar.
- [ ] `save` — cuidado, guarda el dibujo real.
- [ ] `undo` / `redo` — probar en pareja después de un cambio reversible.

## civil3d_object (primitivas genéricas)
- [ ] `list_by_type` con `objectType: "Surface"` (o el tipo que uses). Esperado: todas las superficies.
- [ ] `get_properties` con `handle` de un objeto NO-superficie (ej. una Line/Polyline) — reflexión completa,
      debería seguir funcionando igual que antes.
- [ ] **`get_properties` con `handle` de una TinSurface** (el caso que colgaba 120s) — ahora debe responder
      rápido con el set curado (`Name`/`Handle`/`Layer`/`Type`) en vez de colgarse. Si sigue colgando, la
      mitigación no fue suficiente y hay que reportarlo con detalle.
- [ ] `resolve_location` en modo `coordinates` — prueba las 3 modalidades si tienes tiempo (coordinates,
      interactive, reference_object+offset).
- [ ] **`resolve_location` en modo `reference_object` con el handle de una TinSurface** — ahora debe
      devolver un error claro ("no single meaningful reference point") en vez del offset crudo silencioso.
- [ ] `set_style_name` sobre un objeto con estilos guardados (ej. una superficie con estilo `Pendientes_Playa`).
- [ ] **`ensure_layer`** con un nombre de capa nuevo — confirma que se crea; vuelve a llamarlo con el mismo
      nombre — no debe fallar ni duplicar.
- [ ] **`list_layers`** — confirma que aparecen las capas del dibujo con `colorIndex`/`isOff`/`isFrozen`/`isLocked`.
- [ ] **`set_layer`** sobre una entidad de prueba, con una capa que SÍ existe (debe funcionar) y luego con
      una que NO existe (debe dar error claro, no crearla).
- [ ] **`move_entity`** / **`copy_entity`** / **`rotate_entity`** sobre una entidad de prueba (ej. una Line
      creada con `civil3d_geometry.create_line`) — confirma visualmente el resultado en Civil3D.
- [ ] **`get_entity_bounds`** sobre la misma entidad de prueba.
- [ ] **`delete_entity`** al final, sobre TODOS los objetos de prueba creados durante esta ronda de
      testing — es el cierre del ciclo prueba/limpieza que antes no existía.

## civil3d_surface — foco especial esta ronda
- [ ] `list` — confirma que aparecen todas tus superficies.
- [ ] `get` con `name: "<superficie_real>"`.
- [ ] `get_elevation` en un punto XY dentro de la superficie.
- [ ] `get_statistics`.
- [ ] `create` de una superficie TIN vacía de prueba (nombre nuevo, no pises una real).
- [ ] `add_points` sobre la superficie de prueba (3-4 puntos con XYZ).
- [ ] **`add_breakline` con `breaklineType: "standard"`** — sobre la superficie de prueba, 2+ puntos XYZ
      formando una línea de quiebre. Ya NO debería fallar con "midOrdinateDistance should be greater than
      zero" (bug confirmado y arreglado esta ronda). Esperado: `success: true`, `pointCount` correcto, y
      visualmente la malla TIN debe redibujarse respetando la línea.
      **No pruebes `breaklineType: "wall"` o `"proximity"` esperando que funcionen — devuelven
      `planned` a propósito, ver nota en la descripción del tool.**
- [ ] **`add_boundary`** — con `boundaryType` uno de `outer`/`hide`/`show`/`data_clip` y 3+ puntos XY.
      Mismo bug de `midOrdinateDistance` arreglado esta ronda. Esperado: `success: true`, y la superficie
      debe recortarse/ocultarse según el tipo. Prueba al menos `outer` y `hide`.
- [ ] `compute_volume` entre dos superficies reales (ej. existente vs. diseño).
- [ ] `get_area_elevation_table` con un `interval` razonable (ej. 0.5).
- [ ] `compute_contour_volume` con `layerName` de una capa real con curvas de nivel dibujadas.
- [ ] **`close_contours_against_boundary`** — el más importante de probar esta ronda: usa el caso real
      del hairpin de sotavento (la misma capa de contornos y de boundary que ya usaste antes con
      `forceTramo`/`fixedWindow`). Corre SIN pasar `forceTramo`/`closeMethod` y compara el resultado
      contra lo que antes solo lograbas forzando manualmente. Si el heurístico de polígono simple elige
      bien solo, es la confirmación que cierra el hairpin de sotavento (Mes 9 S1). Si sigue eligiendo
      mal en algún tramo puntual, los overrides manuales (`forceTramo`, `closeMethod: "straight"` o
      `"fixedWindow"`) siguen disponibles como respaldo.
- [ ] `delete_points` filtrado por XY sobre la superficie de prueba — ya no debería fallar con "The
      vertices can't be empty" si el filtro no encuentra coincidencias (ahora devuelve
      `deletedCount: 0` con una nota, en vez de crashear). Prueba también con XY que SÍ coincidan.
- [ ] `list_triangles` / `get_triangle_at_point`.
- [ ] `paste_surface`.
- [ ] `get_build_options`.
- [ ] `delete` de la superficie de prueba (limpieza).

## civil3d_alignment
- [ ] `list`, `get` con `name` real.
- [ ] `station_to_point` / `point_to_station` (par, validar que sean inversos entre sí).
- [ ] `list_superelevation_curves`, `list_superelevation_critical_stations`, `list_design_speeds`.
- [ ] `delete` — solo sobre un alineamiento de prueba, no uno real del proyecto.

## civil3d_profile / civil3d_profile_view
- [ ] `profile`: `list`, `get`, `get_elevation`, `create_from_surface`, `create_layout`, `list_entities`.
- [ ] `profile_view`: `create`, `list`, `get`, `get_bands`.

## civil3d_corridor
- [ ] `list`, `get`, `rebuild`.
- [ ] `get_surfaces`, `create_surface_from_corridor_surface`.
- [ ] `list_baselines`, `list_baseline_regions`, `add_baseline_region` (sobre un corredor de prueba).
- [ ] `get_targets`, `get_feature_lines`.

## civil3d_sample_line
- [ ] `create_group`, `list_groups`, `create_line`, `list_lines`.
- [ ] `create_section_view_group`, `list_section_views`.
- [ ] `list_mass_haul_lines`, `report_quantities`.
- [ ] `delete_group`, `delete_section_view` (sobre grupos de prueba).

## civil3d_sheet_production
- [ ] `list_view_frames`, `list_match_lines` (solo lectura, sin riesgo).

## civil3d_pipe
- [ ] `list_networks`, `get_network`, `list_pipes`, `get_pipe`, `list_structures`, `get_structure`.
- [ ] `get_rule_set`, `get_overridden_rules`.

## civil3d_point
- [ ] `list`, `get`, `list_groups`.
- [ ] `create` de un punto de prueba, luego `delete`.
- [ ] `create_group` / `delete_group` (grupo de prueba).
- [ ] `import` de un archivo PNEZD/PENZD de prueba pequeño.
- [ ] `export` con y sin `groupName`, confirma el contenido del archivo generado.

## civil3d_parcel
- [ ] `list_sites`, `list`, `get`, `delete` (sobre una parcela de prueba).

## civil3d_assembly
- [ ] `list`, `get`, `list_subassemblies`, `get_subassembly_parameters`.
- [ ] `set_subassembly_parameter` sobre un ensamble de prueba (confirma que el valor cambia en Civil3D).
- [ ] `delete` (ensamble de prueba).

## civil3d_grading
- [ ] `list_feature_lines`, `get_feature_line`.
- [ ] `delete_feature_line` (sobre una de prueba).

## civil3d_data_shortcut
- [ ] `get_project_id`, `associate_project` (requiere un segundo dibujo/proyecto de referencia).
- [ ] `promote_reference` (requiere una DREF ya insertada en el dibujo).

## civil3d_survey
- [ ] **`list_figure_styles` — diagnóstico pendiente**: si tu dibujo actual tiene una base de datos de
      survey activa, prueba primero ahí. Luego, si puedes, prueba en un dibujo SIN survey database
      activa. Reporta cuál de los dos casos cuelga (o si ambos cuelgan) — eso es lo que falta para poder
      escribir un fix real en vez de adivinar uno.

## civil3d_geometry
- [ ] `create_line`, `create_polyline`, `create_3d_polyline`, `create_text`, `create_mtext` — prueba
      con un `layer` que NO existe todavía en el dibujo. Ya no debería fallar con `eKeyNotFound`; debe
      crear la capa automáticamente y la entidad debe aparecer en ella.
- [ ] `offset_lines_to_boundary` — prueba también un caso donde sabes que no debería haber
      intersecciones (ej. boundary y source muy separados). Debe seguir devolviendo `success: true`,
      pero ahora con un campo `warning` explicando que no se creó ninguna línea, en vez de quedar
      engañosamente silencioso con `totalLinesCreated: 0`.

## civil3d_blocks (Módulo A — lectura de planos: inventario de bloques)
- [ ] `list_block_definitions` — sin parámetros. Esperado: todos los nombres de bloque insertados en
      el dibujo con su `insertionCount` y `isDynamicBlock`.
- [ ] `count_blocks_by_name` con `name: "<bloque_real>"` — confirma que el número coincide con un
      conteo manual en Civil3D (ej. `QSELECT`/`FILTER` o simplemente contar visualmente en un dibujo
      pequeño). Prueba también con `layout: "Model"` (u otro layout real) para confirmar el filtro.
- [ ] `get_block_attributes` con un bloque que SÍ tenga atributos definidos (ej. un bloque con
      tag/valor de potencia o código) — confirma que cada instancia devuelve sus atributos reales.
      Prueba también con un bloque SIN atributos — debe devolver `attributes: {}` por instancia, no
      fallar.
- [ ] `list_blocks_by_layer` con una capa real que tenga bloques insertados — confirma que agrupa
      correctamente por nombre efectivo (si hay bloques dinámicos en esa capa, deben agruparse por su
      nombre dinámico, no por instancia individual).
- [ ] `get_block_insertion_points` — confirma que las coordenadas X,Y,Z devueltas coinciden con la
      posición real de al menos una inserción visible en el dibujo.
- [ ] **`list_dynamic_block_states`** con un bloque dinámico real (ej. uno con un parámetro de
      visibilidad) — API no verificada contra un dibujo en vivo todavía. Confirma que devuelve el
      `propertyName`/`value` reales de cada instancia y no cuelga. Prueba también con un bloque NO
      dinámico — debe devolver `instances: []`, no fallar.

## civil3d_quantity (Módulo E parcial — cuantificación y reportes)
- [ ] `count_by_category` — usa los `counts` reales de `civil3d_blocks.list_block_definitions` de tu
      dibujo de prueba más un `categoryMap` armado a mano. Confirma que los totales suman bien y que
      cualquier símbolo sin categoría aparece en `uncategorized` en vez de perderse silenciosamente.
- [ ] `generate_quantity_report` con `outputPath` apuntando a un `.xlsx` de prueba — confirma que el
      archivo se genera, abre correctamente en Excel, y tiene las columnas Categoría/Símbolo/Cantidad
      con los datos esperados. **No requiere Civil3D abierto** — se puede probar esta acción incluso
      sin `C3DMCPSTART` corriendo, es la única del catálogo que no toca el plugin.

## civil3d_label (Módulo B — lectura de planos: texto y anotaciones)
- [ ] `extract_text_entities` sin `layer` — confirma que aparece texto DBText Y MText real del
      dibujo, con posición correcta. Prueba de nuevo con un `layer` real para confirmar el filtro.
- [ ] `extract_leader_annotations` — sobre un dibujo con al menos un LEADER clásico y un MLEADER
      con texto. **API no verificada en vivo (`MLeader.MText`)** — confirma que el texto del
      MLeader aparece; si viene `null` en vez del texto real, es la primera señal de que esa
      propiedad no se comporta como se documentó, repórtalo con el handle exacto.
- [ ] `extract_dimensions` — sobre cotas reales del dibujo, confirma que `measurement` coincide con
      el valor real acotado y que `dimensionText` refleja el texto override si lo hay.
- [ ] `match_text_to_nearby_geometry` — arma `texts` desde `extract_text_entities` y `geometry`
      desde `civil3d_blocks.get_block_insertion_points` sobre el mismo dibujo, con un `radius`
      razonable (ej. la distancia típica etiqueta↔símbolo en tu plano). Confirma que empareja bien
      visualmente y que las etiquetas sin símbolo cerca terminan en `unmatchedTexts`. **No requiere
      Civil3D abierto** una vez que ya tenés los dos arrays.

## civil3d_legend (Módulo C — lectura de planos: simbología y leyenda)
- [ ] `read_legend_table` sin `handle` — sobre un dibujo con al menos una tabla de leyenda insertada.
      Confirma que devuelve todas las tablas del dibujo con sus filas/celdas reales como texto.
      Prueba de nuevo pasando el `handle` de la tabla de leyenda específica.
- [ ] `build_symbol_dictionary` — usa las `legendRows` reales de la tabla de leyenda de arriba más
      los `blockNames` reales de `civil3d_blocks.list_block_definitions` del mismo dibujo. Confirma
      que el cruce por nombre normalizado empareja los símbolos esperados y que los que no calzan
      aparecen en `unmatchedBlocks`/`unmatchedLegendRows` en vez de perderse.
- [ ] `compare_legend_vs_drawing` — con el `dictionary` de arriba, confirma que detecta correctamente
      cualquier bloque insertado que no esté en la leyenda (o viceversa) si armás el caso a propósito.
- [ ] `export_symbol_library` con un `outputPath` de prueba — confirma que el `.json` se genera y
      tiene el `dictionary` completo. Luego `import_symbol_library` con ese mismo `inputPath` —
      confirma que devuelve exactamente el mismo diccionario. **Ninguna de las dos requiere Civil3D
      abierto.**
- [ ] `train_symbol_signature` con un `libraryPath` de prueba — llamalo dos veces con el mismo
      `signature.name` pero `entityTypes` distintos; confirma que la segunda llamada actualiza la
      entrada en vez de duplicarla (`signatureCount` no debe crecer). **No requiere Civil3D abierto.**
- [ ] `load_office_standard` con un `.json` de prueba que combine `dictionary`, `categoryMap` y
      `signatures` — confirma que devuelve las tres secciones intactas. Probá también un archivo que
      solo tenga una de las tres — las otras deben venir `undefined`, no fallar. **No requiere
      Civil3D abierto.**

## civil3d_shape_detection (Módulo D — lectura de planos: geometría cruda / heurística)
- [ ] `detect_parallel_line_pairs` sobre un dibujo con al menos un par de líneas que simule una
      tubería dibujada a mano (dos líneas paralelas a distancia constante) — confirma que el par
      aparece con la `distance` correcta. Prueba también con dos líneas que se crucen en ángulo
      distinto — NO debe aparecer como par.
- [ ] `group_entities_by_proximity` con un `radius` chico sobre un grupo de 3+ entidades sueltas
      cercanas entre sí (ej. un círculo + 2 segmentos simulando un símbolo dibujado a mano) —
      confirma que las agrupa en un solo grupo aunque no todos los pares estén dentro del radio
      entre sí (adyacencia transitiva). Prueba también con entidades lejos entre sí — deben quedar
      en grupos separados.
- [ ] **`get_entity_extended_data` — API investigada (`DBObject.XData`/`GetXDataForApplication`)
      pero no verificada en vivo todavía.** Sobre una entidad SIN xdata — debe devolver
      `applications: []`, no fallar. Si tenés forma de agregar xdata de prueba a una entidad (ej.
      desde otra herramienta o LISP), confirma que aparece agrupada correctamente por `appName` con
      sus `entries` (`typeCode`/`value`).
- [ ] `classify_geometry_by_signature` — usa los `entityTypes` de un grupo real de
      `group_entities_by_proximity` contra un catálogo de `signatures` armado a mano (o cargado con
      `civil3d_legend.load_office_standard`). Confirma que el `bestMatch` tiene sentido y que
      `matches` viene ordenado por `confidence` descendente. **No requiere Civil3D abierto.**

## civil3d_plan_vision (Módulo F — lectura de planos: PDF/imagen escaneada, sin dibujo activo)

Requiere `pip install -r plan-vision/requirements.txt` y Tesseract OCR instalado y en el PATH
(`tesseract --version`) — ver `plan-vision/README.md`. **Ninguna acción de este tool requiere
Civil3D abierto ni `C3DMCPSTART`** — se puede probar incluso sin el plugin corriendo.

- [ ] `rasterize_pdf_page` con un PDF real de plano — confirma que el `.png` generado se ve bien
      (nitidez razonable al `dpi` pedido) y que `width`/`height` tienen sentido.
- [ ] `extract_legend_templates` con una leyenda recortada a mano de ese mismo plano — revisar
      visualmente cada `.png` generado en `libraryPath`: ¿el recorte contiene el símbolo completo,
      sin cortar ni con ruido de la celda vecina? Anotar qué filas cayeron en `unresolvedRows` y
      por qué (según el smoke test interno, esto es sensible al layout real de la leyenda — es el
      punto de mayor riesgo de todo el módulo, no verificado contra una leyenda real todavía).
- [ ] `train_symbol_template` — usar para corregir manualmente una entrada de `unresolvedRows` de
      arriba (o agregar un símbolo que la leyenda no tenía). Confirma que sobrescribe si el `name`
      ya existía (mismo `templateCount`, no duplica).
- [ ] `detect_symbols_cv` sobre el plano completo, usando la librería de arriba — contar a mano
      cuántas detecciones son correctas vs. falsos positivos/negativos para un símbolo conocido, y
      ajustar `matchThreshold` según ese caso real antes de confiar en el resultado. Probar también
      con un símbolo que en el plano aparece rotado — confirma que solo se detecta con `rotations`
      incluyendo ese ángulo. **Ya verificado con un smoke test sintético** (escala + rotación +
      ausencia de falsos positivos) — confirmar ahora contra símbolos reales de un plano. Si algún
      template recortado de la leyenda aparece en `skippedTemplates`, es porque no tiene contraste
      interno visible (recorte demasiado "sólido") — volver a recortarlo con el borde incluido.
- [ ] `ocr_extract_labels` sobre una zona del plano con texto suelto (fuera de la leyenda) —
      confirma que el texto y las posiciones son razonables.
- [ ] `calibrate_scale_from_dimension` con dos puntos de píxel de una cota conocida del plano —
      confirma que `unitsPerPixel`/`pixelsPerUnit` da un resultado coherente con la escala real del
      dibujo. **No requiere Python** — es TS puro.

## civil3d_health
- [ ] `health_verbose` — confirma que devuelve versión del plugin, fecha de build, nombre del dibujo,
      y cantidad de documentos abiertos.

---

## Qué hacer con lo que falle

- Si una acción marcada REAL aquí falla contra tu dibujo real (no un stub `planned`, sino un error
  real de la API o un resultado incorrecto), es un bug de Mes 9 S1 por cerrar: anota el mensaje de
  error completo, la acción, y los parámetros usados exactamente.
- Si una acción `planned` te haría falta para tu flujo de trabajo actual del espigón (no solo
  "sería bueno tenerla"), dilo — eso es señal para reabrir esa pieza específica en una ronda futura,
  aunque el plan de 9 meses ya esté formalmente cerrado.
