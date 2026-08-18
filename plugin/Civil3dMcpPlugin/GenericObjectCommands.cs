using System.Reflection;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace Civil3DMcpPlugin;

/// <summary>
/// Núcleo genérico de introspección: primitivas atómicas que funcionan sobre
/// CUALQUIER tipo de objeto de AutoCAD/Civil 3D vía reflexión, en vez de requerir
/// un comando específico ya escrito para cada tipo. Esta es la capa que reduce
/// la necesidad de programar comandos muy puntuales a futuro (ver Plan Maestro,
/// "Metodología: primitivas, no comandos de negocio").
///
/// No incluye un escape hatch de ejecución de comandos crudos de AutoCAD —
/// pospuesto deliberadamente por su mayor superficie de riesgo.
/// </summary>
public static class GenericObjectCommands
{
  public static Task<object?> GetObjectPropertiesAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var obj = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForRead);
      var entity = obj as Autodesk.AutoCAD.DatabaseServices.Entity;

      // Confirmado en pruebas en vivo: pedir get_properties sobre una TinSurface cuelga 120s.
      // SerializeSimpleProperties en sí no enumera colecciones (solo invoca cada getter una
      // vez), así que el hang viene de que UN getter individual de TinSurface bloquea al
      // invocarse — sin diagnóstico en vivo no se puede saber cuál, y un timeout por-hilo es
      // riesgoso (el objeto vive dentro de una transacción con lock de documento de un solo
      // hilo). Mitigación acotada: para Surface, se usa el mismo conjunto de campos ya
      // confirmado rápido en ListSurfacesAsync/GetSurfaceAsync en vez de reflexión abierta.
      if (obj is Autodesk.Civil.DatabaseServices.Surface surface)
      {
        return new
        {
          handle = handleString,
          objectType = obj.GetType().Name,
          layer = entity?.Layer,
          properties = new Dictionary<string, object?>
          {
            ["Name"] = surface.Name,
            ["Handle"] = surface.Handle.ToString(),
            ["Layer"] = surface.Layer,
            ["Type"] = surface is Autodesk.Civil.DatabaseServices.TinSurface ? "TIN" : "Grid",
          },
          note = "Surface objects use a curated safe property set instead of full reflection " +
                 "(get_properties on a TinSurface handle was confirmed to hang 120s in live " +
                 "testing — see CLAUDE.md).",
        };
      }

      return new
      {
        handle = handleString,
        objectType = obj.GetType().Name,
        layer = entity?.Layer,
        properties = SerializeSimpleProperties(obj),
      };
    });
  }

  // ─────────────────────────────────────────────
  // Núcleo genérico de entidades (post-Mes 9, catálogo de comandos): primitivas
  // atómicas que operan sobre CUALQUIER Entity por handle — desbloquean un ciclo
  // completo de prueba/limpieza que hoy no existía (no había forma de deshacer
  // objetos de prueba sin entrar manualmente a Civil3D).
  // ─────────────────────────────────────────────
  public static Task<object?> DeleteEntityAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var entity = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not an Entity.");

      entity.Erase();

      return new { success = true, handle = handleString };
    });
  }

  public static Task<object?> EnsureLayerAsync(JsonObject? parameters)
  {
    var layerName = PluginRuntime.GetRequiredString(parameters, "layerName");
    var colorIndex = PluginRuntime.GetOptionalInt(parameters, "colorIndex");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      EnsureLayerId(db, tr, layerName, colorIndex.HasValue ? (short)colorIndex.Value : (short?)null);
      return new { success = true, layerName };
    });
  }

  public static Task<object?> ListLayersAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
      var layers = new List<object>();

      foreach (ObjectId id in lt)
      {
        var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
        short? colorIndex;
        try { colorIndex = ltr.Color.ColorIndex; } catch { colorIndex = null; }

        layers.Add(new
        {
          name = ltr.Name,
          colorIndex,
          isOff = ltr.IsOff,
          isFrozen = ltr.IsFrozen,
          isLocked = ltr.IsLocked,
        });
      }

      return new { layers };
    });
  }

  public static Task<object?> SetLayerAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");
    var layerName = PluginRuntime.GetRequiredString(parameters, "layerName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
      if (!lt.Has(layerName))
        throw new JsonRpcDispatchException(
          "CIVIL3D.INVALID_INPUT",
          $"Layer '{layerName}' does not exist. Use ensure_layer to create it first.");

      var entity = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not an Entity.");

      entity.Layer = layerName;

      return new { success = true, handle = handleString, layerName };
    });
  }

  public static Task<object?> MoveEntityAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");
    var dx = PluginRuntime.GetRequiredDouble(parameters, "dx");
    var dy = PluginRuntime.GetRequiredDouble(parameters, "dy");
    var dz = PluginRuntime.GetOptionalDouble(parameters, "dz") ?? 0.0;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var entity = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not an Entity.");

      entity.TransformBy(Matrix3d.Displacement(new Vector3d(dx, dy, dz)));

      return new { success = true, handle = handleString, dx, dy, dz };
    });
  }

  public static Task<object?> CopyEntityAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");
    var dx = PluginRuntime.GetRequiredDouble(parameters, "dx");
    var dy = PluginRuntime.GetRequiredDouble(parameters, "dy");
    var dz = PluginRuntime.GetOptionalDouble(parameters, "dz") ?? 0.0;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var original = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not an Entity.");

      var clone = original.Clone() as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' could not be cloned.");

      var owner = (BlockTableRecord)tr.GetObject(original.OwnerId, OpenMode.ForWrite);
      owner.AppendEntity(clone);
      tr.AddNewlyCreatedDBObject(clone, true);

      clone.TransformBy(Matrix3d.Displacement(new Vector3d(dx, dy, dz)));

      return new
      {
        success = true,
        sourceHandle = handleString,
        newHandle = clone.Handle.ToString(),
        dx, dy, dz
      };
    });
  }

  public static Task<object?> RotateEntityAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");
    var basePointX = PluginRuntime.GetRequiredDouble(parameters, "basePointX");
    var basePointY = PluginRuntime.GetRequiredDouble(parameters, "basePointY");
    var angleDegrees = PluginRuntime.GetRequiredDouble(parameters, "angleDegrees");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var entity = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not an Entity.");

      var basePoint = new Point3d(basePointX, basePointY, 0.0);
      var angleRadians = angleDegrees * Math.PI / 180.0;
      entity.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, basePoint));

      return new { success = true, handle = handleString, basePointX, basePointY, angleDegrees };
    });
  }

  public static Task<object?> GetEntityBoundsAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var entity = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not an Entity.");

      try
      {
        var extents = entity.GeometricExtents;
        return new
        {
          handle = handleString,
          entityType = entity.GetType().Name,
          minPoint = new { x = extents.MinPoint.X, y = extents.MinPoint.Y, z = extents.MinPoint.Z },
          maxPoint = new { x = extents.MaxPoint.X, y = extents.MaxPoint.Y, z = extents.MaxPoint.Z },
        };
      }
      catch (Exception ex)
      {
        throw new JsonRpcDispatchException(
          "CIVIL3D.INVALID_INPUT",
          $"Object '{handleString}' has no displayable geometry/bounds: {ex.Message}");
      }
    });
  }

  // setObjectStyle (Mes 7): Entity.StyleName está confirmado como getter Y
  // setter en la mayoría de los objetos Civil3D — Civil3D resuelve el
  // ObjectId del estilo internamente a partir del nombre. El mismo mecanismo
  // aplica igual a un objeto principal (Surface, Alignment...) o a un label
  // ya creado (PipeLabel, etc.), así que esta única primitiva cubre el
  // "motor de estilos" y buena parte del "motor de etiquetas" del Mes 7.
  public static Task<object?> SetObjectStyleAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");
    var styleName = PluginRuntime.GetRequiredString(parameters, "styleName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var obj = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForWrite);

      var prop = obj.GetType().GetProperty("StyleName");
      if (prop == null || prop.PropertyType != typeof(string) || !prop.CanWrite)
        throw new JsonRpcDispatchException(
          "CIVIL3D.INVALID_INPUT",
          $"Object of type '{obj.GetType().Name}' does not have a writable StyleName property.");

      prop.SetValue(obj, styleName);

      return new
      {
        success = true,
        handle = handleString,
        objectType = obj.GetType().Name,
        styleName,
      };
    });
  }

  /// <summary>
  /// Crea la capa si no existe y devuelve su ObjectId. Consolida 3 copias casi
  /// idénticas de este mismo patrón (inline en SurfaceCommands.cs,
  /// helper privado en ProfileCommands.cs, y la falta total del patrón en
  /// GeometryCommands.cs — causa del bug de eKeyNotFound reportado en pruebas
  /// en vivo cuando la capa indicada no existía).
  /// </summary>
  public static ObjectId EnsureLayerId(Database db, Transaction tr, string layerName, short? colorIndex = null)
  {
    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    if (lt.Has(layerName))
      return lt[layerName];

    lt.UpgradeOpen();
    var newLayer = new LayerTableRecord { Name = layerName };
    if (colorIndex.HasValue)
      newLayer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
        Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex.Value);

    var id = lt.Add(newLayer);
    tr.AddNewlyCreatedDBObject(newLayer, true);
    return id;
  }

  internal static ObjectId ResolveHandle(Database db, string handleString)
  {
    try
    {
      var handleValue = Convert.ToInt64(handleString, 16);
      return db.GetObjectId(false, new Handle(handleValue), 0);
    }
    catch (Exception ex)
    {
      throw new JsonRpcDispatchException(
        "CIVIL3D.NOT_FOUND",
        $"No object found for handle '{handleString}': {ex.Message}");
    }
  }

  /// <summary>
  /// Reflects over any object's public instance properties and serializes the
  /// "simple" ones (see TrySerializeSimpleValue). Reused wherever we need to
  /// inspect a Civil3D object/struct whose exact property names aren't known
  /// in advance (e.g. SuperelevationCurve, SuperelevationCriticalStation,
  /// DesignSpeed) instead of guessing specific member names.
  /// </summary>
  public static Dictionary<string, object?> SerializeSimpleProperties(object obj)
  {
    var properties = new Dictionary<string, object?>();

    foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      if (prop.GetIndexParameters().Length > 0 || !prop.CanRead) continue;

      object? raw;
      try { raw = prop.GetValue(obj); }
      catch { continue; } // muchas propiedades de Civil3D lanzan si no aplican al objeto

      if (TrySerializeSimpleValue(raw, out var serialized))
      {
        properties[prop.Name] = serialized;
      }
    }

    return properties;
  }

  // ─────────────────────────────────────────────
  // Ubicación genérica: resuelve una coordenada X,Y,Z a partir de uno de tres
  // modos (coordinates / pick / reference_object), para que el resultado se
  // pueda combinar con cualquier comando de creación ya existente
  // (create_line, create_polyline, puntos COGO, etc.) en vez de necesitar un
  // comando "crear cualquier cosa" monolítico.
  // ─────────────────────────────────────────────
  public static Task<object?> ResolveLocationAsync(JsonObject? parameters)
  {
    var mode = PluginRuntime.GetRequiredString(parameters, "mode");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      switch (mode)
      {
        case "coordinates":
        {
          var x = PluginRuntime.GetRequiredDouble(parameters, "x");
          var y = PluginRuntime.GetRequiredDouble(parameters, "y");
          var z = PluginRuntime.GetOptionalDouble(parameters, "z") ?? 0.0;
          return new { x, y, z, mode };
        }

        case "pick":
        {
          var promptMessage = PluginRuntime.GetOptionalString(parameters, "promptMessage")
            ?? "Seleccione el punto en el dibujo:";

          var options = new PromptPointOptions("\n" + promptMessage);
          var result = doc.Editor.GetPoint(options);

          if (result.Status != PromptStatus.OK)
            throw new JsonRpcDispatchException(
              "CIVIL3D.INVALID_INPUT",
              $"Point selection was not completed (status: {result.Status}).");

          return new { x = result.Value.X, y = result.Value.Y, z = result.Value.Z, mode };
        }

        case "reference_object":
        {
          var handleString = PluginRuntime.GetRequiredString(parameters, "referenceHandle");
          var offsetX = PluginRuntime.GetOptionalDouble(parameters, "offsetX") ?? 0.0;
          var offsetY = PluginRuntime.GetOptionalDouble(parameters, "offsetY") ?? 0.0;
          var offsetZ = PluginRuntime.GetOptionalDouble(parameters, "offsetZ") ?? 0.0;

          var obj = tr.GetObject(ResolveHandle(db, handleString), OpenMode.ForRead);
          var basePoint = FindReferencePoint(obj);

          return new
          {
            x = basePoint.X + offsetX,
            y = basePoint.Y + offsetY,
            z = basePoint.Z + offsetZ,
            mode,
            referenceHandle = handleString,
          };
        }

        default:
          throw new JsonRpcDispatchException(
            "CIVIL3D.INVALID_INPUT",
            $"Unknown location mode '{mode}'. Expected 'coordinates', 'pick', or 'reference_object'.");
      }
    });
  }

  private static readonly string[] ReferencePointPropertyCandidates =
  {
    "Location", "Position", "StartPoint", "Center", "BasePoint", "InsertionPoint"
  };

  private static Point3d FindReferencePoint(object obj)
  {
    // Confirmado en pruebas en vivo: TinSurface (y probablemente otros objetos de área/
    // superficie sin un anclaje puntual real) puede exponer una de las propiedades candidato
    // con un valor no significativo (ej. Point3d.Origin) en vez de fallar — el resultado
    // silenciosamente incorrecto reportado era justo ese offset crudo sin sumar nada real.
    // Se rechaza explícitamente en vez de dejar que el mecanismo de reflexión adivine mal.
    if (obj is Autodesk.Civil.DatabaseServices.Surface)
      throw new JsonRpcDispatchException(
        "CIVIL3D.INVALID_INPUT",
        $"Object of type '{obj.GetType().Name}' has no single meaningful reference point " +
        "(it's an area/surface object, not a point-like entity) — reference_object mode isn't " +
        "supported for it. Use 'coordinates' mode instead.");

    foreach (var propName in ReferencePointPropertyCandidates)
    {
      var prop = obj.GetType().GetProperty(propName);
      if (prop == null || prop.PropertyType != typeof(Point3d)) continue;

      try
      {
        return (Point3d)prop.GetValue(obj)!;
      }
      catch { /* try the next candidate */ }
    }

    throw new JsonRpcDispatchException(
      "CIVIL3D.INVALID_INPUT",
      $"Could not find a position on object of type '{obj.GetType().Name}'. " +
      $"Tried: {string.Join(", ", ReferencePointPropertyCandidates)}.");
  }

  public static Task<object?> ListObjectsByTypeAsync(JsonObject? parameters)
  {
    var objectType = PluginRuntime.GetRequiredString(parameters, "objectType");
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");
    var limit = PluginRuntime.GetOptionalInt(parameters, "limit") ?? 200;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      var matches = new List<object>();
      int total = 0;

      foreach (ObjectId id in btr)
      {
        var ent = tr.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
        if (ent == null || !MatchesType(ent.GetType(), objectType)) continue;
        if (layer != null && !string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

        total++;
        if (matches.Count >= limit) continue;

        matches.Add(new
        {
          handle = ent.Handle.ToString(),
          name = TryGetNameProperty(ent),
          objectType = ent.GetType().Name,
          layer = ent.Layer,
        });
      }

      return new
      {
        objectType,
        layer,
        total,
        truncated = total > matches.Count,
        objects = matches,
      };
    });
  }

  // ── Helpers ──

  /// <summary>Walks the type's base-type chain so a request for e.g. "Curve" also matches "Line"/"Polyline".</summary>
  private static bool MatchesType(Type type, string requestedTypeName)
  {
    for (var t = type; t != null; t = t.BaseType)
    {
      if (string.Equals(t.Name, requestedTypeName, StringComparison.OrdinalIgnoreCase))
        return true;
    }
    return false;
  }

  private static string? TryGetNameProperty(object obj)
  {
    var nameProp = obj.GetType().GetProperty("Name");
    if (nameProp == null || nameProp.PropertyType != typeof(string)) return null;

    try { return nameProp.GetValue(obj) as string; }
    catch { return null; }
  }

  /// <summary>
  /// Serializes only "simple" values (primitives, strings, enums, common Civil3D
  /// structs) into JSON-friendly shapes. Returns false for complex/unsupported
  /// types so the caller can skip that property instead of guessing.
  /// </summary>
  private static bool TrySerializeSimpleValue(object? raw, out object? result)
  {
    if (raw == null) { result = null; return true; }

    var t = raw.GetType();

    if (t.IsPrimitive || raw is string || raw is decimal)
    {
      result = raw;
      return true;
    }

    if (t.IsEnum) { result = raw.ToString(); return true; }
    if (raw is DateTime dt) { result = dt.ToString("o"); return true; }
    if (raw is Guid g) { result = g.ToString(); return true; }

    if (raw is Autodesk.AutoCAD.Geometry.Point3d p3)
    {
      result = new { x = p3.X, y = p3.Y, z = p3.Z };
      return true;
    }

    if (raw is Autodesk.AutoCAD.Geometry.Point2d p2)
    {
      result = new { x = p2.X, y = p2.Y };
      return true;
    }

    if (raw is Handle h) { result = h.ToString(); return true; }

    if (raw is ObjectId oid)
    {
      result = oid.IsNull ? null : oid.Handle.ToString();
      return true;
    }

    result = null;
    return false;
  }
}
