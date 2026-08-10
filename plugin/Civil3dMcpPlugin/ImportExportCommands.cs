using System.Text.Json.Nodes;

namespace Civil3DMcpPlugin;

/// <summary>
/// LandXML / GIS import-export (Mes 8, S3). Import from LandXML is confirmed
/// NOT supported by the .NET API at all (Autodesk's own documentation:
/// COM-only via IAeccSurfaces::ImportXML(), or the UI). Export has a real
/// but tangential signal (Autodesk.Civil.Settings exposes a
/// LandXMLSurfaceDataExportType used by a SurfaceData settings property),
/// not an actual confirmed export entry-point class/method — rather than
/// guess an entire unconfirmed call chain, this stays a documented stub.
/// GIS/SHP export needs the Autodesk.Gis.Map.ImportExport namespace, which
/// typically lives in AutoCAD Map 3D's core managed assembly — not clearly
/// identifiable among the already-copied AcMap*Mgd.dll satellite modules
/// (buffer/overlay/spatial-reference/etc.), so also left as a stub rather
/// than guessing which DLL to wire up.
///
/// Plain-text point import/export (PNEZD/PENZD) doesn't need any of this —
/// see PointCommands.ImportCogoPointsAsync/ExportCogoPointsAsync instead.
/// </summary>
public static class ImportExportCommands
{
  public static Task<object?> ImportLandXmlAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "LandXML import is confirmed NOT supported by the Civil 3D .NET API — Autodesk's own documentation states this requires the COM API (IAeccSurfaces::ImportXML()) or the UI. Not something to work around with a guessed .NET signature."
    });

  public static Task<object?> ExportSurfaceToLandXmlAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "No confirmed .NET export entry-point was found (only a tangential settings type, LandXMLSurfaceDataExportType, in Autodesk.Civil.Settings). Needs the real exporter class/method confirmed against a live Civil 3D drawing before implementing."
    });

  public static Task<object?> ExportToShapefileAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Autodesk.Gis.Map.ImportExport is the real namespace for this, but it needs AutoCAD Map 3D's core managed assembly, which isn't clearly identified among the AcMap*Mgd.dll satellite modules already in C_References/. Needs the exact DLL confirmed before implementing (also note: Autodesk documents a separate downloadable 'SHP Import/Export Utility' as an alternative, not part of the base install)."
    });
}
