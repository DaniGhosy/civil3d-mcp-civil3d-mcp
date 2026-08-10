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
