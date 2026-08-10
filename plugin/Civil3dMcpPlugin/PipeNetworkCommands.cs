using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Gravity pipe networks (Mes 5): real Network/Pipe/Structure access confirmed
/// against Autodesk's own documentation and forum threads for the .NET API
/// (Autodesk.Civil.PipeNetwork.DatabaseServices namespace). Design-rule reading
/// (OverrideRuleSet/RuleSetStyleId/GetOverriddenRuleIds) is also confirmed real.
/// Triggering rule validation via API and interference checking have no
/// confirmed API surface — left as documented stubs.
/// </summary>
public static class PipeNetworkCommands
{
  public static Task<object?> ListPipeNetworksAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var networks = new List<object>();

      foreach (ObjectId id in civilDoc.GetPipeNetworkIds())
      {
        var network = tr.GetObject(id, OpenMode.ForRead) as Network;
        if (network == null) continue;

        networks.Add(new
        {
          name = network.Name,
          handle = network.Handle.ToString(),
          pipeCount = network.GetPipeIds().Count,
          structureCount = network.GetStructureIds().Count,
        });
      }

      return new { networks };
    });
  }

  public static Task<object?> GetPipeNetworkAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "networkName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, name);

      return new
      {
        name = network.Name,
        handle = network.Handle.ToString(),
        pipeCount = network.GetPipeIds().Count,
        structureCount = network.GetStructureIds().Count,
      };
    });
  }

  // Network.Create(...) existe pero no con la firma (Document, string) que
  // adiviné — el compilador confirmó que el primer argumento debe ser
  // CivilDocument y el segundo se pasa por "ref" (probablemente un patrón
  // out-param, no un simple nombre). Stub documentado en vez de seguir
  // adivinando la firma completa a ciegas.
  public static Task<object?> CreatePipeNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Network.Create exists but not with signature (Document, string) — first argument must be CivilDocument and the second is passed by 'ref'. Needs the full real signature confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> ListPipesAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var pipes = new List<object>();

      foreach (ObjectId id in network.GetPipeIds())
      {
        var pipe = tr.GetObject(id, OpenMode.ForRead) as Pipe;
        if (pipe == null) continue;

        pipes.Add(new
        {
          handle = pipe.Handle.ToString(),
          partFamilyName = pipe.PartFamilyName,
          partSizeName = pipe.PartSizeName,
          startPoint = new { x = pipe.StartPoint.X, y = pipe.StartPoint.Y, z = pipe.StartPoint.Z },
          endPoint = new { x = pipe.EndPoint.X, y = pipe.EndPoint.Y, z = pipe.EndPoint.Z },
          layer = pipe.Layer,
        });
      }

      return new { networkName, pipes };
    });
  }

  public static Task<object?> GetPipeAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");
    var pipeHandle = PluginRuntime.GetRequiredString(p, "pipeHandle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var pipe = FindPipeByHandle(tr, network, pipeHandle);

      return new
      {
        handle = pipe.Handle.ToString(),
        partFamilyName = pipe.PartFamilyName,
        partSizeName = pipe.PartSizeName,
        startPoint = new { x = pipe.StartPoint.X, y = pipe.StartPoint.Y, z = pipe.StartPoint.Z },
        endPoint = new { x = pipe.EndPoint.X, y = pipe.EndPoint.Y, z = pipe.EndPoint.Z },
        layer = pipe.Layer,
      };
    });
  }

  public static Task<object?> ListStructuresAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var structures = new List<object>();

      foreach (ObjectId id in network.GetStructureIds())
      {
        var structure = tr.GetObject(id, OpenMode.ForRead) as Structure;
        if (structure == null) continue;

        structures.Add(new
        {
          handle = structure.Handle.ToString(),
          partFamilyName = structure.PartFamilyName,
          partSizeName = structure.PartSizeName,
          position = new { x = structure.Position.X, y = structure.Position.Y, z = structure.Position.Z },
          layer = structure.Layer,
        });
      }

      return new { networkName, structures };
    });
  }

  public static Task<object?> GetStructureAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");
    var structureHandle = PluginRuntime.GetRequiredString(p, "structureHandle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var structure = FindStructureByHandle(tr, network, structureHandle);

      return new
      {
        handle = structure.Handle.ToString(),
        partFamilyName = structure.PartFamilyName,
        partSizeName = structure.PartSizeName,
        position = new { x = structure.Position.X, y = structure.Position.Y, z = structure.Position.Z },
        layer = structure.Layer,
      };
    });
  }

  // addStructureToNetwork: el compilador confirmó que Network.AddStructure SÍ
  // existe, pero con la firma real
  // AddStructure(ObjectId, ObjectId, Point3d, double, ref ObjectId, bool) —
  // toma ObjectIds de part family/size (no strings), rotación, y un patrón
  // ref/out para el resultado. Stub documentado con la firma real revelada,
  // en vez de seguir adivinando cómo resolver esos ObjectIds a ciegas.
  public static Task<object?> AddStructureToNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Network.AddStructure exists with signature " +
             "AddStructure(ObjectId partFamilyId, ObjectId partSizeId, Point3d, double rotation, ref ObjectId, bool) " +
             "— confirmed by the compiler. Needs part family/size ObjectId resolution (from a PartsList) and the " +
             "ref/bool parameters' meaning confirmed against a live Civil 3D drawing."
    });

  // addPipeToNetwork: "Network" no contiene "AddPipe" — confirmado por el
  // compilador. Stub documentado.
  public static Task<object?> AddPipeToNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Network.AddPipe does not exist under that name — confirmed by the compiler. The real pipe-creation member needs to be confirmed against a live Civil 3D drawing."
    });

  // listPartsLists: civilDoc.Styles.PartsListSet SÍ existe (confirmado — no
  // dio error), pero el tipo "PartsList" no se encontró con ese nombre.
  // Stub documentado hasta confirmar el tipo real de sus elementos.
  public static Task<object?> ListPartsListsAsync()
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "civilDoc.Styles.PartsListSet exists (confirmed) but the element type is not 'PartsList' under that exact name — confirmed by the compiler. Needs the real type confirmed against a live Civil 3D drawing (try civil3d_object list_by_type/get_properties on a parts list object as a workaround in the meantime)."
    });

  // ─────────────────────────────────────────────
  // Reglas de diseño hidráulico (Mes 5): OverrideRuleSet/RuleSetStyleId/
  // GetOverriddenRuleIds confirmados en un hilo de foro específico del API
  // .NET. NetworkRule se serializa genéricamente por reflexión.
  // ─────────────────────────────────────────────
  public static Task<object?> GetPipeRuleSetAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");
    var pipeHandle = PluginRuntime.GetRequiredString(p, "pipeHandle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var pipe = FindPipeByHandle(tr, network, pipeHandle);

      return new
      {
        networkName,
        pipeHandle,
        overrideRuleSet = pipe.OverrideRuleSet,
        ruleSetStyleName = pipe.RuleSetStyleName,
      };
    });
  }

  public static Task<object?> GetPipeOverriddenRulesAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");
    var pipeHandle = PluginRuntime.GetRequiredString(p, "pipeHandle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var pipe = FindPipeByHandle(tr, network, pipeHandle);

      var rules = new List<object>();
      foreach (ObjectId ruleId in pipe.GetOverriddenRuleIds())
      {
        var rule = tr.GetObject(ruleId, OpenMode.ForRead);
        rules.Add(GenericObjectCommands.SerializeSimpleProperties(rule));
      }

      return new { networkName, pipeHandle, rules };
    });
  }

  public static Task<object?> CheckPipeNetworkInterferenceAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "No confirmed API member found for triggering interference checks. Needs confirmation against a live Civil 3D drawing."
    });

  // ── Helpers ──

  private static Network FindNetworkByName(dynamic civilDoc, Transaction tr, string name)
  {
    var ids = (ObjectIdCollection)civilDoc.GetPipeNetworkIds();
    return CivilObjectLookup.FindByName<Network>(ids.Cast<ObjectId>(), tr, name);
  }

  private static Pipe FindPipeByHandle(Transaction tr, Network network, string handleString)
  {
    foreach (ObjectId id in network.GetPipeIds())
    {
      var pipe = tr.GetObject(id, OpenMode.ForRead) as Pipe;
      if (pipe != null && string.Equals(pipe.Handle.ToString(), handleString, StringComparison.OrdinalIgnoreCase))
        return pipe;
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Pipe '{handleString}' not found on network '{network.Name}'.");
  }

  private static Structure FindStructureByHandle(Transaction tr, Network network, string handleString)
  {
    foreach (ObjectId id in network.GetStructureIds())
    {
      var structure = tr.GetObject(id, OpenMode.ForRead) as Structure;
      if (structure != null && string.Equals(structure.Handle.ToString(), handleString, StringComparison.OrdinalIgnoreCase))
        return structure;
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Structure '{handleString}' not found on network '{network.Name}'.");
  }
}
