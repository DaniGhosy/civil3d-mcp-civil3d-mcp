using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Survey (Mes 8, S2): civilDoc.Styles.SurveyFigureStyles is confirmed real
/// (real code cited from an Autodesk forum, same StylesRoot convention used
/// in Mes 7). The path from CivilDocument down to actual survey data
/// (SurveyDatabase/SurveyNetwork/SurveyFigure) is attempted by analogy with
/// the Get*Ids() convention used everywhere else in this API — build-verify
/// determines what's real.
/// </summary>
public static class SurveyCommands
{
  public static Task<object?> ListSurveyFigureStylesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var styles = new List<object>();

      foreach (ObjectId id in civilDoc.Styles.SurveyFigureStyles)
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);
        styles.Add(GenericObjectCommands.SerializeSimpleProperties(obj));
      }

      return new { styles };
    });
  }

  // "CivilDocument.GetSurveyNetworkIds()" y "GetSurveyFigureIds()" no
  // existen con esos nombres — confirmado por el compilador. La cadena real
  // de acceso (probablemente vía un SurveyDatabaseManager o similar) queda
  // sin confirmar en vez de seguir adivinando.
  public static Task<object?> ListSurveyNetworksAsync()
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "CivilDocument.GetSurveyNetworkIds() does not exist under that name — confirmed by the compiler. Needs the real access chain confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> ListSurveyFiguresAsync()
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "CivilDocument.GetSurveyFigureIds() does not exist under that name — confirmed by the compiler. Needs the real access chain confirmed against a live Civil 3D drawing."
    });
}
