using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Handles alignment operations: list, get, create, delete,
/// station-to-point and point-to-station conversions.
/// </summary>
public static class AlignmentCommands
{
  public static Task<object?> ListAlignmentsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;

      var alignments = new List<object>();

      foreach (ObjectId id in civilDoc.GetAlignmentIds())
      {
        var alignment = tr.GetObject(id, OpenMode.ForRead) as Alignment;
        if (alignment == null) continue;

        alignments.Add(new
        {
          name = alignment.Name,
          handle = alignment.Handle.ToString(),
          length = alignment.Length,
          startStation = alignment.StartingStation,
          endStation = alignment.EndingStation,
          layer = alignment.Layer,
        });
      }

      return new { alignments };
    });
  }

  public static Task<object?> GetAlignmentAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;

      var alignment = FindAlignmentByName(civilDoc, tr, name);

      return new
      {
        name = alignment.Name,
        handle = alignment.Handle.ToString(),
        length = alignment.Length,
        startStation = alignment.StartingStation,
        endStation = alignment.EndingStation,
        layer = alignment.Layer,
        style = alignment.StyleName,
        entityCount = alignment.Entities.Count,
      };
    });
  }

  public static Task<object?> CreateAlignmentAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return Task.FromResult<object?>(new
    {
      status = "planned",
      message = $"createAlignment for '{name}' requires Civil 3D UI polyline input."
    });
  }

  public static Task<object?> DeleteAlignmentAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;

      var alignment = FindAlignmentByName(civilDoc, tr, name);
      alignment.UpgradeOpen();
      alignment.Erase();

      return new { success = true, deleted = name };
    });
  }

  public static Task<object?> StationToPointAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var station = PluginRuntime.GetRequiredDouble(parameters, "station");
    var offset = PluginRuntime.GetOptionalDouble(parameters, "offset") ?? 0.0;

    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;
      var alignment = FindAlignmentByName(civilDoc, tr, name);

      double x = 0, y = 0;
      alignment.PointLocation(station, offset, ref x, ref y);

      return new
      {
        alignmentName = name,
        station,
        offset,
        x,
        y,
      };
    });
  }

  public static Task<object?> PointToStationAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var x = PluginRuntime.GetRequiredDouble(parameters, "x");
    var y = PluginRuntime.GetRequiredDouble(parameters, "y");

    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;
      var alignment = FindAlignmentByName(civilDoc, tr, name);

      double station = 0, offset = 0;
      alignment.StationOffset(x, y, ref station, ref offset);

      return new
      {
        alignmentName = name,
        x,
        y,
        station,
        offset,
      };
    });
  }

  // ─────────────────────────────────────────────
  // Alineamientos avanzados (Mes 3): superelevación y velocidades de diseño.
  // Los nombres de las colecciones (SuperelevationCurves,
  // SuperelevationCriticalStations, DesignSpeeds) están confirmados por
  // documentación/código real de Autodesk, pero las propiedades internas de
  // cada item (SuperelevationCurve/SuperelevationCriticalStation/DesignSpeed)
  // no se pudieron verificar contra una sesión real, así que se serializan
  // genéricamente vía reflexión (GenericObjectCommands.SerializeSimpleProperties)
  // en vez de adivinar nombres de propiedad puntuales.
  // ─────────────────────────────────────────────
  public static Task<object?> ListSuperelevationCurvesAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;
      var alignment = FindAlignmentByName(civilDoc, tr, name);

      var curves = new List<object>();
      foreach (var curve in alignment.SuperelevationCurves)
        curves.Add(GenericObjectCommands.SerializeSimpleProperties(curve!));

      return new { alignmentName = name, curves };
    });
  }

  public static Task<object?> ListSuperelevationCriticalStationsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;
      var alignment = FindAlignmentByName(civilDoc, tr, name);

      var stations = new List<object>();
      foreach (var station in alignment.SuperelevationCriticalStations)
        stations.Add(GenericObjectCommands.SerializeSimpleProperties(station!));

      return new { alignmentName = name, stations };
    });
  }

  public static Task<object?> ListDesignSpeedsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, _, db, tr) =>
    {
      var civilDoc = CivilApplication.ActiveDocument;
      var alignment = FindAlignmentByName(civilDoc, tr, name);

      var speeds = new List<object>();
      foreach (var speed in alignment.DesignSpeeds)
        speeds.Add(GenericObjectCommands.SerializeSimpleProperties(speed!));

      return new { alignmentName = name, speeds };
    });
  }

  // ── Helper ──
  internal static Alignment FindAlignmentByName(CivilDocument civilDoc, Transaction tr, string name)
    => CivilObjectLookup.FindByName<Alignment>(civilDoc.GetAlignmentIds().Cast<ObjectId>(), tr, name);
}