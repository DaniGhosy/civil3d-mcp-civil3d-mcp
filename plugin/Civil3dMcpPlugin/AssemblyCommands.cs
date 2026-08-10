using System.Text.Json.Nodes;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

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
  // Creación de un ensamble vacío: la firma exacta del factory del API
  // necesita verificarse contra una sesión Civil3D real antes de escribirla.
  // Se deja como stub explícito, mismo patrón que CreateParcelAsync.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateAssemblyAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Assembly creation needs its exact factory signature verified against a live Civil 3D drawing."
    });
}
