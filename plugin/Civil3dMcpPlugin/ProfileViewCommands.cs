using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Profile Views (Mes 3, new file): the graphic/annotation counterpart of a
/// Profile. Creation uses the confirmed real signature
/// ProfileView.Create(ObjectId, Point3d, string, ObjectId, StackedProfileViewsCreationOptions)
/// from a published Autodesk DevBlog sample, with NumberOfViews = 1 for a
/// simple single view (there is no separately-confirmed "simple" overload,
/// so we reuse the one signature that is actually verified).
/// </summary>
public static class ProfileViewCommands
{
  public static Task<object?> CreateProfileViewAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var x = PluginRuntime.GetRequiredDouble(parameters, "x");
    var y = PluginRuntime.GetRequiredDouble(parameters, "y");
    var z = PluginRuntime.GetOptionalDouble(parameters, "z") ?? 0.0;
    var bandSetStyleName = PluginRuntime.GetOptionalString(parameters, "bandSetStyle") ?? "Standard";
    var viewStyleName = PluginRuntime.GetOptionalString(parameters, "viewStyle") ?? "Standard";

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);

      var bandSetId = civilDoc.Styles.ProfileViewBandSetStyles[bandSetStyleName];
      var viewStyleId = civilDoc.Styles.ProfileViewStyles[viewStyleName];

      var stackedOptions = new StackedProfileViewsCreationOptions(viewStyleId, viewStyleId, viewStyleId)
      {
        NumberOfViews = 1,
      };

      var insertPosition = new Point3d(x, y, z);
      var profileViewIds = ProfileView.Create(alignment.ObjectId, insertPosition, name, bandSetId, stackedOptions);

      var handles = new List<string>();
      foreach (ObjectId id in profileViewIds)
      {
        var pv = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
        if (pv != null) handles.Add(pv.Handle.ToString());
      }

      return new
      {
        success = true,
        name,
        alignmentName,
        handles,
      };
    });
  }

  public static Task<object?> ListProfileViewsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var views = new List<object>();

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var pv = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
        if (pv == null) continue;

        views.Add(new
        {
          name = pv.Name,
          handle = pv.Handle.ToString(),
          layer = pv.Layer,
        });
      }

      return new { views };
    });
  }

  public static Task<object?> GetProfileViewAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var pv = CivilObjectLookup.FindEntityByName<ProfileView>(tr, db, name);

      return new
      {
        name = pv.Name,
        handle = pv.Handle.ToString(),
        layer = pv.Layer,
      };
    });
  }

  public static Task<object?> DeleteProfileViewAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var pv = CivilObjectLookup.FindEntityByName<ProfileView>(tr, db, name);
      pv.UpgradeOpen();
      pv.Erase();

      return new { success = true, deleted = name };
    });
  }

  // ─────────────────────────────────────────────
  // Bandas: pv.Bands.GetTopBandItems()/GetBottomBandItems() están confirmados
  // por documentación de Autodesk, pero el tipo exacto de cada item no — se
  // serializan genéricamente vía reflexión en vez de adivinar sus propiedades.
  // ─────────────────────────────────────────────
  public static Task<object?> GetProfileViewBandsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var pv = CivilObjectLookup.FindEntityByName<ProfileView>(tr, db, name);

      var topBands = new List<object>();
      foreach (var item in pv.Bands.GetTopBandItems())
        topBands.Add(GenericObjectCommands.SerializeSimpleProperties(item!));

      var bottomBands = new List<object>();
      foreach (var item in pv.Bands.GetBottomBandItems())
        bottomBands.Add(GenericObjectCommands.SerializeSimpleProperties(item!));

      return new { profileViewName = name, topBands, bottomBands };
    });
  }
}
