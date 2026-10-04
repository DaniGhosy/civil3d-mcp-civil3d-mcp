using System.Linq;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using AcDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

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

  // listPartsLists: la nota anterior decía que "PartsList" no resolvía como
  // tipo de elemento de PartsListSet — pero ese mismo tipo SÍ resuelve y
  // compila en este archivo (ver FindPartForNetwork más abajo, vía
  // network.PartsListId). El hueco real no era el tipo, era no saber si
  // PartsListSet itera ObjectIds (como las colecciones de civil3d_style) o
  // objetos PartsList directos (lo que sugiere la documentación de Autodesk
  // sobre PartsListCollection). En vez de adivinar cuál de las dos formas es
  // la real, se enumera como IEnumerable no genérico y se resuelve cada
  // elemento por runtime-type-check — compila sin importar cuál sea.
  public static Task<object?> ListPartsListsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var partsLists = GetAllPartsLists(civilDoc, tr)
        .Select(partsList => (object)new { name = partsList.Name, handle = partsList.Handle.ToString() })
        .ToList();

      return new { partsLists };
    });
  }

  // listParts / getPart: reusan la misma cadena PartsList -> GetPartFamilyIdsByDomain
  // -> PartFamily -> PartSize ya confirmada compilando en FindPartForNetwork,
  // pero enumerando todo en vez de buscar un match exacto.
  public static Task<object?> ListPartsAsync(JsonObject? p)
  {
    var partsListName = PluginRuntime.GetRequiredString(p, "partsListName");
    var domainFilter = PluginRuntime.GetOptionalString(p, "domain");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var partsList = FindPartsListByName(civilDoc, tr, partsListName);
      var parts = EnumeratePartsListSizes(tr, partsList, ResolveDomainFilter(domainFilter))
        .Select(entry => (object)new
        {
          domain = entry.Domain.ToString(),
          familyName = entry.Family.Name,
          partName = entry.SizeName,
          handle = entry.SizeObject.Handle.ToString(),
        })
        .ToList();

      return new { partsListName, parts };
    });
  }

  public static Task<object?> GetPartAsync(JsonObject? p)
  {
    var partsListName = PluginRuntime.GetRequiredString(p, "partsListName");
    var partName = PluginRuntime.GetRequiredString(p, "partName");
    var domainFilter = PluginRuntime.GetOptionalString(p, "domain");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var partsList = FindPartsListByName(civilDoc, tr, partsListName);
      var match = EnumeratePartsListSizes(tr, partsList, ResolveDomainFilter(domainFilter))
        .FirstOrDefault(entry => string.Equals(entry.SizeName, partName, StringComparison.OrdinalIgnoreCase));

      if (match.SizeObject == null)
      {
        throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Part '{partName}' was not found in parts list '{partsListName}'.");
      }

      var details = GenericObjectCommands.SerializeSimpleProperties(match.SizeObject);
      return new Dictionary<string, object?>(details)
      {
        ["partsListName"] = partsListName,
        ["domain"] = match.Domain.ToString(),
        ["familyName"] = match.Family.Name,
        ["partName"] = match.SizeName,
        ["handle"] = match.SizeObject.Handle.ToString(),
      };
    });
  }

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

  // resizePipeInNetwork (portado de Civil3D-mcp-main, adaptado a la convención
  // de este repo de ubicar el pipe por handle en vez de por nombre).
  public static Task<object?> ResizePipeInNetworkAsync(JsonObject? p)
  {
    var networkName = PluginRuntime.GetRequiredString(p, "networkName");
    var pipeHandle = PluginRuntime.GetRequiredString(p, "pipeHandle");
    var newPartName = PluginRuntime.GetOptionalString(p, "newPartName");
    var newDiameter = PluginRuntime.GetOptionalDouble(p, "newDiameter");

    if (string.IsNullOrWhiteSpace(newPartName) && !newDiameter.HasValue)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Either 'newPartName' or 'newDiameter' is required.");
    }

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var network = FindNetworkByName(civilDoc, tr, networkName);
      var pipe = FindPipeByHandle(tr, network, pipeHandle);
      var writablePipe = tr.GetObject(pipe.ObjectId, OpenMode.ForWrite) as Pipe
        ?? throw new JsonRpcDispatchException("CIVIL3D.TRANSACTION_FAILED", "Could not open pipe for write.");

      if (!string.IsNullOrWhiteSpace(newPartName))
      {
        var part = FindPartForNetwork(network, tr, newPartName!, DomainType.Pipe);
        writablePipe.SwapPartFamilyAndSize(part.FamilyId, part.SizeId);
      }

      if (newDiameter.HasValue)
      {
        writablePipe.ResizeByInnerDiameterOrWidth(newDiameter.Value, useClosestSize: false);
      }

      return new { networkName, pipeHandle, newPartName, newDiameter, resized = true };
    });
  }

  private readonly record struct NetworkPartIds(ObjectId FamilyId, ObjectId SizeId);

  private static NetworkPartIds FindPartForNetwork(Network network, Transaction transaction, string partName, DomainType domain)
  {
    if (network.PartsListId.IsNull)
    {
      throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Pipe network '{network.Name}' does not have a parts list assigned.");
    }

    var partsList = CivilObjectUtils.GetRequiredObject<PartsList>(transaction, network.PartsListId, OpenMode.ForRead);
    foreach (ObjectId familyId in partsList.GetPartFamilyIdsByDomain(domain))
    {
      var family = CivilObjectUtils.GetRequiredObject<PartFamily>(transaction, familyId, OpenMode.ForRead);
      for (var index = 0; index < family.PartSizeCount; index++)
      {
        var sizeId = family[index];
        var size = transaction.GetObject(sizeId, OpenMode.ForRead);
        var sizeName = CivilObjectUtils.GetName(size) ?? CivilObjectUtils.GetStringProperty(size, "Description");
        if (string.Equals(sizeName, partName, StringComparison.OrdinalIgnoreCase)
          || (family.PartSizeCount == 1 && string.Equals(family.Name, partName, StringComparison.OrdinalIgnoreCase)))
          return new NetworkPartIds(familyId, sizeId);
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Exact {domain} size '{partName}' was not found in the parts list for network '{network.Name}'.");
  }

  private readonly record struct PartSizeEntry(DomainType Domain, PartFamily Family, AcDbObject SizeObject, string? SizeName);

  private static IEnumerable<PartSizeEntry> EnumeratePartsListSizes(Transaction tr, PartsList partsList, IReadOnlyList<DomainType> domains)
  {
    foreach (var domain in domains)
    {
      foreach (ObjectId familyId in partsList.GetPartFamilyIdsByDomain(domain))
      {
        var family = CivilObjectUtils.GetRequiredObject<PartFamily>(tr, familyId, OpenMode.ForRead);
        for (var index = 0; index < family.PartSizeCount; index++)
        {
          var sizeId = family[index];
          var size = tr.GetObject(sizeId, OpenMode.ForRead);
          var sizeName = CivilObjectUtils.GetName(size) ?? CivilObjectUtils.GetStringProperty(size, "Description");
          yield return new PartSizeEntry(domain, family, size, sizeName);
        }
      }
    }
  }

  private static IReadOnlyList<DomainType> ResolveDomainFilter(string? domainFilter)
    => domainFilter?.ToLowerInvariant() switch
    {
      "pipe" => new[] { DomainType.Pipe },
      "structure" => new[] { DomainType.Structure },
      null or "" => new[] { DomainType.Pipe, DomainType.Structure },
      _ => throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Unsupported domain '{domainFilter}'. Valid values: pipe, structure."),
    };

  // civilDoc.Styles.PartsListSet is confirmed to exist and compile, but whether it enumerates
  // ObjectIds (like civil3d_style's collections) or PartsList objects directly is unconfirmed —
  // Autodesk's own docs describe it as a "PartsListCollection", which forum evidence suggests
  // yields typed objects, not ObjectIds. Enumerating as non-generic IEnumerable and resolving each
  // element by runtime type sidesteps the guess entirely: compiles either way.
  private static IEnumerable<PartsList> GetAllPartsLists(dynamic civilDoc, Transaction tr)
  {
    foreach (object item in (System.Collections.IEnumerable)civilDoc.Styles.PartsListSet)
    {
      PartsList? partsList = item as PartsList;
      if (partsList == null && item is ObjectId id && !id.IsNull)
      {
        partsList = tr.GetObject(id, OpenMode.ForRead) as PartsList;
      }
      if (partsList != null) yield return partsList;
    }
  }

  private static PartsList FindPartsListByName(dynamic civilDoc, Transaction tr, string name)
  {
    foreach (var partsList in GetAllPartsLists(civilDoc, tr))
    {
      if (string.Equals(partsList.Name, name, StringComparison.OrdinalIgnoreCase)) return partsList;
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Parts list '{name}' was not found.");
  }

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
