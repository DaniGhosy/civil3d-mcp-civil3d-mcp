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

  // ListSurveyNetworksAsync/ListSurveyFiguresAsync moved to CogoCommands.cs:
  // the guessed CivilDocument.GetSurveyNetworkIds()/GetSurveyFigureIds() names
  // never existed (confirmed by the compiler), but reflection via
  // CivilDocument.SurveyDocument does — see CogoCommands.cs for the real
  // implementation and CommandDispatcher.cs for the updated routing.
}
