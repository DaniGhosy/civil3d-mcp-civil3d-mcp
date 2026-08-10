using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

public static class CorridorCommands
{
  public static Task<object?> ListCorridorsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var corridors = new List<object>();

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var c = tr.GetObject(id, OpenMode.ForRead) as Corridor;
        if (c == null) continue;

        corridors.Add(new
        {
          name = c.Name,
          handle = c.Handle.ToString(),
          baselineCount = c.Baselines?.Count ?? 0,
          layer = c.Layer
        });
      }

      return new { corridors };
    });
  }

  public static Task<object?> GetCorridorAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);

      return new
      {
        name = c.Name,
        handle = c.Handle.ToString(),
        layer = c.Layer,
        baselineCount = c.Baselines?.Count ?? 0
      };
    });
  }

  public static Task<object?> RebuildCorridorAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);

      c.UpgradeOpen();
      c.Rebuild();

      return new { success = true, rebuilt = name };
    });
  }

  // ─────────────────────────────────────────────
  // Baselines y regions (Mes 4): corridor.Baselines ya se usaba para el
  // conteo; se agrega el listado real y la creación de regions vía
  // BaselineRegionCollection.Add, confirmado por Autodesk DevBlog.
  // ─────────────────────────────────────────────
  public static Task<object?> ListBaselinesAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);
      var baselines = new List<object>();

      foreach (Baseline baseline in c.Baselines)
      {
        baselines.Add(new
        {
          name = baseline.Name,
          alignmentId = baseline.AlignmentId.Handle.ToString(),
          regionCount = baseline.BaselineRegions?.Count ?? 0,
        });
      }

      return new { corridorName = name, baselines };
    });
  }

  public static Task<object?> ListBaselineRegionsAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");
    var baselineName = PluginRuntime.GetRequiredString(p, "baselineName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);
      var baseline = FindBaselineByName(c, baselineName);

      var regions = new List<object>();
      foreach (BaselineRegion region in baseline.BaselineRegions)
      {
        regions.Add(new
        {
          name = region.Name,
          startStation = region.StartStation,
          endStation = region.EndStation,
        });
      }

      return new { corridorName = name, baselineName, regions };
    });
  }

  public static Task<object?> AddBaselineRegionAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");
    var baselineName = PluginRuntime.GetRequiredString(p, "baselineName");
    var regionName = PluginRuntime.GetRequiredString(p, "regionName");
    var assemblyName = PluginRuntime.GetRequiredString(p, "assemblyName");
    var startStation = PluginRuntime.GetRequiredDouble(p, "startStation");
    var endStation = PluginRuntime.GetRequiredDouble(p, "endStation");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);
      var baseline = FindBaselineByName(c, baselineName);
      var assembly = CivilObjectLookup.FindEntityByName<Assembly>(tr, db, assemblyName);

      c.UpgradeOpen();
      baseline.BaselineRegions.Add(regionName, assembly.ObjectId, startStation, endStation);

      return new { success = true, corridorName = name, baselineName, regionName };
    });
  }

  // getCorridorTargets (Mes 4): GetTargets()/SetTargets() confirmados en
  // documentación oficial (viven en BaselineRegion, no en AppliedAssembly).
  // Forma exacta del objeto de targets no verificada — se serializa
  // genéricamente por reflexión.
  public static Task<object?> GetCorridorTargetsAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");
    var baselineName = PluginRuntime.GetRequiredString(p, "baselineName");
    var regionName = PluginRuntime.GetRequiredString(p, "regionName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);
      var baseline = FindBaselineByName(c, baselineName);

      BaselineRegion? region = null;
      foreach (BaselineRegion r in baseline.BaselineRegions)
      {
        if (string.Equals(r.Name, regionName, StringComparison.OrdinalIgnoreCase)) { region = r; break; }
      }

      if (region == null)
        throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Region '{regionName}' not found on baseline '{baselineName}'.");

      var targets = GenericObjectCommands.SerializeSimpleProperties(region.GetTargets()!);

      return new { corridorName = name, baselineName, regionName, targets };
    });
  }

  // ─────────────────────────────────────────────
  // Superficies de corredor (Mes 4): patrón confirmado por documentación
  // oficial de Autodesk — corridor.CorridorSurfaces por nombre, y
  // TinSurface.CreateFromCorridorSurface para desprender una superficie
  // independiente (dinámicamente vinculada) a partir de una corridor surface.
  // ─────────────────────────────────────────────
  public static Task<object?> GetCorridorSurfacesAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);
      var surfaces = new List<object>();

      foreach (CorridorSurface cs in c.CorridorSurfaces)
      {
        surfaces.Add(new
        {
          name = cs.Name,
          overhangCorrection = cs.OverhangCorrection.ToString(),
        });
      }

      return new { corridorName = name, surfaces };
    });
  }

  public static Task<object?> CreateSurfaceFromCorridorSurfaceAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");
    var corridorSurfaceName = PluginRuntime.GetRequiredString(p, "corridorSurfaceName");
    var newSurfaceName = PluginRuntime.GetRequiredString(p, "newSurfaceName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);

      CorridorSurface? corridorSurface = null;
      foreach (CorridorSurface cs in c.CorridorSurfaces)
      {
        if (string.Equals(cs.Name, corridorSurfaceName, StringComparison.OrdinalIgnoreCase)) { corridorSurface = cs; break; }
      }

      if (corridorSurface == null)
        throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Corridor surface '{corridorSurfaceName}' not found on corridor '{name}'.");

      var surfaceId = TinSurface.CreateFromCorridorSurface(newSurfaceName, corridorSurface);
      var surface = tr.GetObject(surfaceId, OpenMode.ForRead) as TinSurface;

      return new
      {
        success = true,
        corridorName = name,
        corridorSurfaceName,
        newSurfaceName,
        handle = surface?.Handle.ToString(),
      };
    });
  }

  // getCorridorFeatureLines (Mes 4, intento): terminología real confirmada
  // (BaselineFeatureLines, FeatureLineCollectionMap, acceso por código de
  // punto como "CL"/"EOR") pero sin código completo verificado.
  public static Task<object?> GetCorridorFeatureLinesAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");
    var baselineName = PluginRuntime.GetRequiredString(p, "baselineName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = CivilObjectLookup.FindEntityByName<Corridor>(tr, db, name);
      var baseline = FindBaselineByName(c, baselineName);

      var codes = new List<object>();
      foreach (var codeEntry in baseline.MainBaselineFeatureLines.FeatureLineCollectionMap)
      {
        codes.Add(GenericObjectCommands.SerializeSimpleProperties(codeEntry!));
      }

      return new { corridorName = name, baselineName, codes };
    });
  }

  public static Task<object?> ComputeCorridorVolumesAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "No direct corridor volume API — combine getCorridorSurfaces + createSurfaceFromCorridorSurface " +
             "with civil3d_surface's compute_volume/get_area_elevation_table on the resulting surfaces instead."
    });

  private static Baseline FindBaselineByName(Corridor corridor, string name)
  {
    foreach (Baseline baseline in corridor.Baselines)
    {
      if (string.Equals(baseline.Name, name, StringComparison.OrdinalIgnoreCase))
        return baseline;
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Baseline '{name}' not found on corridor '{corridor.Name}'.");
  }
}