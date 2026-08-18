using System.Text.Json.Nodes;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using AcDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace Civil3DMcpPlugin;

/// <summary>
/// Primitives for Civil 3D Assemblies: list/get/delete assemblies (scanning
/// ModelSpace directly, same pattern as CivilObjectLookup), plus (Mes 4)
/// real subassembly enumeration (Assembly.Groups[i].GetSubassemblyIds()) and
/// subassembly parameter read/write (ParamsDouble/Bool/String).
///
/// Creating a new empty assembly baseline still needs its exact factory
/// signature verified against a live Civil 3D session — see CreateAssemblyAsync.
/// </summary>
public static class AssemblyCommands
{
  public static Task<object?> ListAssembliesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assemblies = new List<object>();

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var a = tr.GetObject(id, OpenMode.ForRead) as Assembly;
        if (a == null) continue;

        assemblies.Add(new
        {
          name = a.Name,
          handle = a.Handle.ToString(),
          layer = a.Layer,
        });
      }

      return new { assemblies };
    });
  }

  public static Task<object?> GetAssemblyAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var a = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, name);

      return new
      {
        name = a.Name,
        handle = a.Handle.ToString(),
        layer = a.Layer,
      };
    });
  }

  // ─────────────────────────────────────────────
  // Enumerar los subensambles de un Assembly (Mes 4): el Mes 2 asumió
  // Assembly.GetSubassemblyIds() directo, que no existe. La ruta real
  // (confirmada por código de foro de Autodesk) es Assembly.Groups[i]
  // (AssemblyGroup) -> GetSubassemblyIds().
  // ─────────────────────────────────────────────
  public static Task<object?> ListSubassembliesAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assembly = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, name);
      var subassemblies = new List<object>();

      foreach (AssemblyGroup group in assembly.Groups)
      {
        foreach (ObjectId subId in group.GetSubassemblyIds())
        {
          var sub = tr.GetObject(subId, OpenMode.ForRead) as Subassembly;
          if (sub == null) continue;

          subassemblies.Add(new
          {
            name = sub.Name,
            handle = sub.Handle.ToString(),
            objectType = sub.GetType().Name,
          });
        }
      }

      return new { assemblyName = name, subassemblies };
    });
  }

  // getSubassemblyParameters / setSubassemblyParameter (Mes 4): patrón real
  // confirmado en foro de Autodesk — subassembly.ParamsDouble es una colección
  // de ParamDouble con DisplayName/Value. Se intenta también ParamsBool/
  // ParamsString/ParamsInteger por la misma convención (build-verify único).
  public static Task<object?> GetSubassemblyParametersAsync(JsonObject? parameters)
  {
    var assemblyName = PluginRuntime.GetRequiredString(parameters, "assemblyName");
    var subassemblyName = PluginRuntime.GetRequiredString(parameters, "subassemblyName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assembly = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, assemblyName);
      var subassembly = FindSubassemblyByName(tr, assembly, subassemblyName);

      var doubles = new List<object>();
      foreach (Autodesk.Civil.Runtime.ParamDouble p in subassembly.ParamsDouble)
        doubles.Add(new { displayName = p.DisplayName, value = p.Value });

      var bools = new List<object>();
      foreach (Autodesk.Civil.Runtime.ParamBool p in subassembly.ParamsBool)
        bools.Add(new { displayName = p.DisplayName, value = p.Value });

      var strings = new List<object>();
      foreach (Autodesk.Civil.Runtime.ParamString p in subassembly.ParamsString)
        strings.Add(new { displayName = p.DisplayName, value = p.Value });

      return new { assemblyName, subassemblyName, doubles, bools, strings };
    });
  }

  public static Task<object?> SetSubassemblyParameterAsync(JsonObject? parameters)
  {
    var assemblyName = PluginRuntime.GetRequiredString(parameters, "assemblyName");
    var subassemblyName = PluginRuntime.GetRequiredString(parameters, "subassemblyName");
    var displayName = PluginRuntime.GetRequiredString(parameters, "displayName");
    var value = PluginRuntime.GetRequiredDouble(parameters, "value");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assembly = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, assemblyName);
      var subassembly = FindSubassemblyByName(tr, assembly, subassemblyName);

      subassembly.UpgradeOpen();

      foreach (Autodesk.Civil.Runtime.ParamDouble p in subassembly.ParamsDouble)
      {
        if (string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
        {
          p.Value = value;
          return new { success = true, assemblyName, subassemblyName, displayName, value };
        }
      }

      throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Double parameter '{displayName}' not found on subassembly '{subassemblyName}'.");
    });
  }

  private static Subassembly FindSubassemblyByName(Transaction tr, Assembly assembly, string name)
  {
    foreach (AssemblyGroup group in assembly.Groups)
    {
      foreach (ObjectId subId in group.GetSubassemblyIds())
      {
        var sub = tr.GetObject(subId, OpenMode.ForRead) as Subassembly;
        if (sub != null && string.Equals(sub.Name, name, StringComparison.OrdinalIgnoreCase))
          return sub;
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Subassembly '{name}' not found on assembly '{assembly.Name}'.");
  }

  public static Task<object?> DeleteAssemblyAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var a = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, name);
      a.UpgradeOpen();
      a.Erase();

      return new { success = true, deleted = name };
    });
  }

  // ─────────────────────────────────────────────
  // Creación de un ensamble vacío (portado de Civil3D-mcp-main):
  // civilDoc.AssemblyCollection.Add(name, assemblyType, insertionPoint) confirmado real,
  // reemplaza el stub anterior de este repo.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateAssemblyAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var insertX = PluginRuntime.GetRequiredDouble(parameters, "insertX");
    var insertY = PluginRuntime.GetRequiredDouble(parameters, "insertY");
    var description = PluginRuntime.GetOptionalString(parameters, "description") ?? string.Empty;
    var assemblyTypeText = PluginRuntime.GetRequiredString(parameters, "assemblyType");
    if (!Enum.TryParse<AssemblyType>(assemblyTypeText, ignoreCase: true, out var assemblyType))
    {
      throw new JsonRpcDispatchException(
        "CIVIL3D.INVALID_INPUT",
        $"Invalid assemblyType '{assemblyTypeText}'. Use {string.Join(", ", Enum.GetNames<AssemblyType>())}.");
    }

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assemblyId = civilDoc.AssemblyCollection.Add(name, assemblyType, new Autodesk.AutoCAD.Geometry.Point3d(insertX, insertY, 0));
      var assembly = tr.GetObject(assemblyId, OpenMode.ForWrite) as Assembly
        ?? throw new JsonRpcDispatchException("CIVIL3D.TRANSACTION_FAILED", "AssemblyCollection.Add did not return a valid Assembly.");
      assembly.Description = description;

      return new
      {
        name = assembly.Name,
        handle = assembly.Handle.ToString(),
        insertX,
        insertY,
        assemblyType = assembly.Type.ToString(),
        created = true,
      };
    });
  }

  // ─────────────────────────────────────────────
  // Crear subensamble de catálogo (portado de Civil3D-mcp-main):
  // SubassemblyCollection.ImportStockSubassembly + Assembly.AddSubassembly.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateSubassemblyAsync(JsonObject? parameters)
  {
    var assemblyName = PluginRuntime.GetRequiredString(parameters, "assemblyName");
    var subassemblyType = PluginRuntime.GetRequiredString(parameters, "subassemblyType");
    var side = PluginRuntime.GetRequiredString(parameters, "side");
    if (!new[] { "Left", "Right", "Both" }.Contains(side, StringComparer.OrdinalIgnoreCase))
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "side must be Left, Right, or Both.");

    var subParams = ReadParameters(parameters?["parameters"] as JsonObject);
    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assembly = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, assemblyName);
      var requestedSides = side.Equals("Both", StringComparison.OrdinalIgnoreCase)
        ? new[] { "Left", "Right" }
        : new[] { side };
      var created = new List<object>();

      foreach (var requestedSide in requestedSides)
      {
        var subassemblyName = $"{subassemblyType}-{requestedSide}-{Guid.NewGuid():N}";
        var subassemblyId = civilDoc.SubassemblyCollection.ImportStockSubassembly(
          subassemblyName,
          subassemblyType,
          assembly.Location);
        assembly.AddSubassembly(subassemblyId);

        var subassembly = tr.GetObject(subassemblyId, OpenMode.ForWrite) as Subassembly
          ?? throw new JsonRpcDispatchException("CIVIL3D.TRANSACTION_FAILED", "ImportStockSubassembly did not return a valid Subassembly.");
        if (!Civil3DCompatibility.TrySetProperty(subassembly, "Side", requestedSide))
        {
          throw new JsonRpcDispatchException(
            "CIVIL3D.API_ERROR",
            $"Stock subassembly '{subassemblyType}' does not expose a writable Side parameter. No implicit side was assumed.");
        }
        ApplySubassemblyParameters(subassembly, subParams);

        created.Add(new
        {
          name = CivilObjectUtils.GetName(subassembly) ?? subassemblyName,
          handle = CivilObjectUtils.GetHandle(subassembly),
          side = requestedSide,
        });
      }

      return new
      {
        assemblyName,
        subassemblyType,
        subassemblies = created,
        added = true,
      };
    });
  }

  // ─────────────────────────────────────────────
  // Editar/eliminar un subensamble existente (portado de Civil3D-mcp-main).
  // Sin subassemblyName: lista subensambles. Con delete=true: elimina.
  // En otro caso: aplica parámetros por nombre.
  // ─────────────────────────────────────────────
  public static Task<object?> EditAssemblyAsync(JsonObject? parameters)
  {
    var assemblyName = PluginRuntime.GetRequiredString(parameters, "assemblyName");
    var subassemblyName = PluginRuntime.GetOptionalString(parameters, "subassemblyName");
    var deleteSubassembly = PluginRuntime.GetOptionalBool(parameters, "delete") ?? false;
    var editParameters = ReadParameters(parameters?["parameters"] as JsonObject);

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var assembly = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, assemblyName);
      var subassemblyIds = assembly.Groups
        .SelectMany(group => group.GetSubassemblyIds().Cast<ObjectId>())
        .Where(id => !id.IsNull)
        .Distinct()
        .ToList();

      if (string.IsNullOrWhiteSpace(subassemblyName))
      {
        var subassemblies = subassemblyIds
          .Select(id => tr.GetObject(id, OpenMode.ForRead))
          .Select(ToSubassemblySummary)
          .ToList();
        return new
        {
          assemblyName,
          subassemblyCount = subassemblies.Count,
          subassemblies,
        };
      }

      var targetId = subassemblyIds.FirstOrDefault(id =>
      {
        var candidate = tr.GetObject(id, OpenMode.ForRead);
        return string.Equals(CivilObjectUtils.GetName(candidate), subassemblyName, StringComparison.OrdinalIgnoreCase);
      });
      if (targetId.IsNull)
        throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Subassembly '{subassemblyName}' not found in assembly '{assemblyName}'.");

      var target = tr.GetObject(targetId, OpenMode.ForWrite);
      if (deleteSubassembly)
      {
        target.Erase();
        return new
        {
          assemblyName,
          subassemblyName,
          deleted = true,
        };
      }

      var updated = ApplySubassemblyParameters(target, editParameters);
      if (editParameters.Count > 0 && updated.Count != editParameters.Count)
      {
        var missing = editParameters.Keys.Except(updated, StringComparer.OrdinalIgnoreCase);
        throw new JsonRpcDispatchException(
          "CIVIL3D.INVALID_INPUT",
          $"Subassembly parameters were not writable: {string.Join(", ", missing)}. The transaction was not committed.");
      }

      return new
      {
        assemblyName,
        subassemblyName,
        updatedParameters = updated,
        updated = updated.Count > 0,
      };
    });
  }

  private static Dictionary<string, object?> ReadParameters(JsonObject? values)
  {
    var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    if (values == null) return result;
    foreach (var pair in values)
      result[pair.Key] = pair.Value?.GetValue<object>();
    return result;
  }

  /// <summary>
  /// Tries a direct reflective property set first (as Civil3D-mcp-main wrote it),
  /// then falls back to this repo's own confirmed convention for stock
  /// subassembly parameters — matching by DisplayName inside ParamsDouble —
  /// since most stock subassembly values are not plain settable properties.
  /// </summary>
  private static List<string> ApplySubassemblyParameters(AcDbObject subassembly, IReadOnlyDictionary<string, object?> parameters)
  {
    var updated = new List<string>();
    foreach (var pair in parameters)
    {
      if (Civil3DCompatibility.TrySetProperty(subassembly, pair.Key, pair.Value))
      {
        updated.Add(pair.Key);
        continue;
      }

      if (subassembly is Subassembly typedSubassembly && pair.Value != null)
      {
        var matched = false;
        foreach (Autodesk.Civil.Runtime.ParamDouble p in typedSubassembly.ParamsDouble)
        {
          if (string.Equals(p.DisplayName, pair.Key, StringComparison.OrdinalIgnoreCase))
          {
            try
            {
              p.Value = Convert.ToDouble(pair.Value);
              updated.Add(pair.Key);
              matched = true;
            }
            catch { /* value not convertible to double — leave unmatched */ }
            break;
          }
        }
        if (matched) continue;
      }
    }
    return updated;
  }

  private static Dictionary<string, object?> ToSubassemblySummary(AcDbObject subassembly)
  {
    return new Dictionary<string, object?>
    {
      ["name"] = CivilObjectUtils.GetName(subassembly) ?? subassembly.Handle.ToString(),
      ["handle"] = CivilObjectUtils.GetHandle(subassembly),
      ["type"] = subassembly.GetType().Name,
      ["className"] = subassembly.GetType().Name,
      ["side"] = Civil3DCompatibility.GetPropertyValue(subassembly, "Side")?.ToString()?.ToLowerInvariant() ?? "none",
      ["parameters"] = Civil3DCompatibility.GetReadableScalarProperties(subassembly),
    };
  }
}
