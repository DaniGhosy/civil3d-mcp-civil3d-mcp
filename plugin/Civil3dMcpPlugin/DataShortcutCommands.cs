using System.Text.Json.Nodes;
using Autodesk.Civil.DataShortcuts;

namespace Civil3DMcpPlugin;

/// <summary>
/// Data Shortcuts (Mes 8, S1): Autodesk.Civil.DataShortcuts, in
/// AeccDataShortcutMgd.dll (separate assembly from AeccDbMgd, now referenced
/// in the .csproj). Method names (GetDSProjectId, AssociateDSProject,
/// DataShortcutManager.CreateReference) are cited in an Autodesk forum
/// thread but their exact signatures aren't verified — build-verify
/// determines what's real. Promoting a reference uses the AutoCAD
/// _PROMOTEREFERENCE command (same SendStringToExecute pattern already used
/// by undoDrawing/redoDrawing), which doesn't depend on guessing an API
/// signature.
/// </summary>
public static class DataShortcutCommands
{
  public static Task<object?> GetDataShortcutProjectIdAsync(JsonObject? p)
  {
    var projectPath = PluginRuntime.GetRequiredString(p, "projectPath");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var projectId = DataShortcuts.GetDSProjectId(projectPath);

      return new { projectPath, projectId = projectId.ToString() };
    });
  }

  public static Task<object?> AssociateDataShortcutProjectAsync(JsonObject? p)
  {
    var projectPath = PluginRuntime.GetRequiredString(p, "projectPath");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      DataShortcuts.AssociateDSProject(projectPath);

      return new { success = true, projectPath };
    });
  }

  // "DataShortcutManager" no existe con ese nombre — confirmado por el
  // compilador (el enum DataShortcutEntityType sí existe). Stub documentado
  // en vez de seguir adivinando el nombre real de la clase.
  public static Task<object?> CreateDataReferenceAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "'DataShortcutManager' does not exist under that name — confirmed by the compiler (DataShortcutEntityType enum does exist). Needs the real class name confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> PromoteDataReferenceAsync(JsonObject? p)
  {
    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      doc.SendStringToExecute("_PROMOTEREFERENCE\n", true, false, true);
      return new { success = true };
    });
  }
}
