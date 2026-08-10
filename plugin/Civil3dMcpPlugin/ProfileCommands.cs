using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Handles profile operations: list, get, elevation, create from surface, layout, delete.
/// </summary>
public static class ProfileCommands
{
  public static Task<object?> ListProfilesAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var profiles = new List<object>();

      foreach (ObjectId id in alignment.GetProfileIds())
      {
        var profile = tr.GetObject(id, OpenMode.ForRead) as Profile;
        if (profile == null) continue;

        profiles.Add(new
        {
          name = profile.Name,
          handle = profile.Handle.ToString(),
          type = profile.ProfileType.ToString(),
          layer = profile.Layer,
        });
      }

      return new { alignmentName, profiles };
    });
  }

  public static Task<object?> GetProfileAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var profile = FindProfileByName(alignment, tr, name);

      return new
      {
        name = profile.Name,
        handle = profile.Handle.ToString(),
        type = profile.ProfileType.ToString(),
        startStation = profile.StartingStation,
        endStation = profile.EndingStation,
        layer = profile.Layer,
        style = profile.StyleName,
      };
    });
  }

  public static Task<object?> GetProfileElevationAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var station = PluginRuntime.GetRequiredDouble(parameters, "station");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var profile = FindProfileByName(alignment, tr, name);

      var elevation = profile.ElevationAt(station);

      return new
      {
        alignmentName,
        profileName = name,
        station,
        elevation,
      };
    });
  }

  // ─────────────────────────────────────────────
  // createProfileFromSurface: implementación real vía Profile.CreateFromSurface,
  // confirmada por el compilador con la firma
  // (string name, ObjectId alignmentId, ObjectId surfaceId, ObjectId layerId,
  //  ObjectId styleId, ObjectId labelSetId). El lookup de label set por nombre
  // (civilDoc.Styles.ProfileLabelSetStyles["..."]) NO existe con ese nombre —
  // confirmado por el compilador (StylesRoot no lo contiene) — así que se pasa
  // ObjectId.Null (sin label set) hasta confirmar la ruta real de acceso.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateProfileFromSurfaceAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var surfaceName = PluginRuntime.GetRequiredString(parameters, "surfaceName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var layerName = PluginRuntime.GetOptionalString(parameters, "layer") ?? "C-ROAD-PROF-EG";
    var styleName = PluginRuntime.GetOptionalString(parameters, "style") ?? "Standard";

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var surfaceId = FindSurfaceIdByName(civilDoc, tr, surfaceName);
      var layerId = EnsureLayer(db, tr, layerName);
      var styleId = civilDoc.Styles.ProfileStyles[styleName];

      var profileId = Profile.CreateFromSurface(name, alignment.ObjectId, surfaceId, layerId, styleId, ObjectId.Null);
      var profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;

      return new
      {
        success = true,
        name,
        alignmentName,
        surfaceName,
        handle = profile?.Handle.ToString(),
        note = "Created without a label set — civilDoc.Styles.ProfileLabelSetStyles does not exist; " +
               "the real accessor path needs to be confirmed against a live Civil 3D drawing.",
      };
    });
  }

  // ─────────────────────────────────────────────
  // createLayoutProfile: implementación real vía Profile.CreateByLayout,
  // confirmada por el compilador. Mismo ajuste de label set que arriba.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateLayoutProfileAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var layerName = PluginRuntime.GetOptionalString(parameters, "layer") ?? "C-ROAD-PROF";
    var styleName = PluginRuntime.GetOptionalString(parameters, "style") ?? "Standard";

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var layerId = EnsureLayer(db, tr, layerName);
      var styleId = civilDoc.Styles.ProfileStyles[styleName];

      var profileId = Profile.CreateByLayout(name, alignment.ObjectId, layerId, styleId, ObjectId.Null);
      var profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;

      return new
      {
        success = true,
        name,
        alignmentName,
        handle = profile?.Handle.ToString(),
        note = "Created without a label set — civilDoc.Styles.ProfileLabelSetStyles does not exist; " +
               "the real accessor path needs to be confirmed against a live Civil 3D drawing.",
      };
    });
  }

  // ─────────────────────────────────────────────
  // Geometría vertical de un perfil de layout ya creado: los métodos
  // ProfileEntityCollection.AddFixedTangent / .AddFixedSymmetricParabolaByThreePoints
  // SÍ existen (confirmado por el compilador — el error es de sobrecarga, no
  // de miembro inexistente), pero ninguna de sus sobrecargas acepta la
  // cantidad de argumentos con la que adiviné (4 y 6 respectivamente). Se
  // deja como stub explícito en vez de seguir probando combinaciones de
  // argumentos a ciegas — falta confirmar la firma real contra el IntelliSense
  // de una sesión Civil3D real o el archivo .chm del SDK.
  // ─────────────────────────────────────────────
  public static Task<object?> AddProfileTangentAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "ProfileEntityCollection.AddFixedTangent exists but takes a different argument " +
             "count than guessed (4). Needs the real overload confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> AddProfileParabolaAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "ProfileEntityCollection.AddFixedSymmetricParabolaByThreePoints exists but takes a " +
             "different argument count than guessed (6). Needs the real overload confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> ListProfileEntitiesAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var profile = FindProfileByName(alignment, tr, name);

      var entities = new List<object>();
      foreach (var entity in profile.Entities)
        entities.Add(GenericObjectCommands.SerializeSimpleProperties(entity!));

      return new { alignmentName, profileName = name, entities };
    });
  }

  public static Task<object?> DeleteProfileAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var profile = FindProfileByName(alignment, tr, name);
      profile.UpgradeOpen();
      profile.Erase();

      return new { success = true, deleted = name };
    });
  }

  // ── Helpers ──

  private static Profile FindProfileByName(Alignment alignment, Transaction tr, string name)
    => CivilObjectLookup.FindByName<Profile>(alignment.GetProfileIds().Cast<ObjectId>(), tr, name);

  private static ObjectId FindSurfaceIdByName(dynamic civilDoc, Transaction tr, string name)
  {
    var ids = (ObjectIdCollection)civilDoc.GetSurfaceIds();
    return CivilObjectLookup
      .FindByName<Autodesk.Civil.DatabaseServices.Surface>(ids.Cast<ObjectId>(), tr, name)
      .ObjectId;
  }

  private static ObjectId EnsureLayer(Database db, Transaction tr, string layerName)
    => GenericObjectCommands.EnsureLayerId(db, tr, layerName);
}
