using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

public static class SurfaceCommands
{
  public static Task<object?> ListSurfacesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surfaces = new List<object>();

      foreach (ObjectId id in civilDoc.GetSurfaceIds())
      {
        var surface = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
        if (surface == null) continue;

        surfaces.Add(new
        {
          name = surface.Name,
          handle = surface.Handle.ToString(),
          type = surface is TinSurface ? "TIN" : "Grid",
          layer = surface.Layer
        });
      }

      return new { surfaces };
    });
  }

  public static Task<object?> GetSurfaceAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);

      return new
      {
        name = surface.Name,
        handle = surface.Handle.ToString(),
        type = surface is TinSurface ? "TIN" : "Grid",
        layer = surface.Layer,
        style = surface.StyleName
      };
    });
  }

  public static Task<object?> GetSurfaceElevationAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var x = PluginRuntime.GetRequiredDouble(parameters, "x");
    var y = PluginRuntime.GetRequiredDouble(parameters, "y");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);
      var elevation = surface.FindElevationAtXY(x, y);

      return new
      {
        surfaceName = name,
        elevation,
        x,
        y
      };
    });
  }

  public static Task<object?> GetSurfaceStatisticsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);
      var props = surface.GetGeneralProperties();

      return new
      {
        surfaceName = name,
        minimumElevation = props.MinimumElevation,
        maximumElevation = props.MaximumElevation,
        numberOfPoints = props.NumberOfPoints
      };
    });
  }

  public static Task<object?> CreateSurfaceAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var description = PluginRuntime.GetOptionalString(parameters, "description");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surfaceId = TinSurface.Create(db, name);
      var surface = tr.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

      if (surface != null && description != null)
        surface.Description = description;

      return new
      {
        success = true,
        name,
        handle = surface?.Handle.ToString()
      };
    });
  }

  public static Task<object?> DeleteSurfaceAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);
      surface.UpgradeOpen();
      surface.Erase();

      return new { success = true, deleted = name };
    });
  }

  public static Task<object?> AddSurfacePointsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var pointsNode = parameters?["points"] as JsonArray;

    if (pointsNode == null || pointsNode.Count == 0)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "points required");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

      surface.UpgradeOpen();

      var pts = new Point3dCollection();

      foreach (var pt in pointsNode)
      {
        pts.Add(new Point3d(
          pt!["x"]!.GetValue<double>(),
          pt["y"]!.GetValue<double>(),
          pt["z"]!.GetValue<double>()
        ));
      }

      surface.AddVertices(pts);

      return new
      {
        success = true,
        surfaceName = name,
        added = pts.Count
      };
    });
  }

  // ─────────────────────────────────────────────
  // FIX IMPORTANTE: NO existe volume API directo
  // ─────────────────────────────────────────────
  public static Task<object?> ComputeSurfaceVolumeAsync(JsonObject? parameters)
  {
    var baseName = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var compName = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var baseSurface = FindSurfaceByName(civilDoc, tr, baseName) as TinSurface;
      var compSurface = FindSurfaceByName(civilDoc, tr, compName) as TinSurface;

      if (baseSurface == null || compSurface == null)
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Both surfaces must be TIN");

      return new
      {
        baseSurface = baseName,
        comparisonSurface = compName,
        cutVolume = 0.0,
        fillVolume = 0.0,
        netVolume = 0.0,
        note = "Civil 3D .NET API does not expose direct volume calculation. Requires Surface Analysis workflow."
      };
    });
  }

  // ─────────────────────────────────────────────
  // FIX CRÍTICO: wrapper seguro sin CivilDocument real
  // ─────────────────────────────────────────────
  private static Autodesk.Civil.DatabaseServices.Surface FindSurfaceByName(
    dynamic civilDoc,
    Transaction tr,
    string name)
  {
    foreach (ObjectId id in civilDoc.GetSurfaceIds())
    {
      var surface = tr.GetObject(id, OpenMode.ForRead)
        as Autodesk.Civil.DatabaseServices.Surface;

      if (surface != null &&
          string.Equals(surface.Name, name, StringComparison.OrdinalIgnoreCase))
      {
        return surface;
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Surface '{name}' not found");
  }

  // ─────────────────────────────────────────────
  // FIX PARA COMMAND DISPATCHER (OBLIGATORIO)
  // ─────────────────────────────────────────────
  public static Task<object?> AddSurfaceBreaklineAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> AddSurfaceBoundaryAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> ExtractSurfaceContoursAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });
}