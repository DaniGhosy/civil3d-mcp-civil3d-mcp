using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using AcDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace Civil3DMcpPlugin;

/// <summary>
/// Producción de planos (Mes 7): crear ViewFrame/ViewFrameGroup/Sheets/MatchLine
/// vía API .NET está confirmado como NO soportado por Autodesk (foro oficial:
/// "The ability to create ViewFrames and Sheets has not been exposed in the
/// .NET API"; hay un pedido de feature abierto y sin resolver pidiendo
/// exactamente esto). ListViewFrames/ListMatchLines siguen leyendo objetos ya
/// creados vía UI, mismo patrón de escaneo de ModelSpace que FeatureLine/ProfileView.
///
/// Ported from Civil3D-mcp-main: sheet SET management (a separate, higher-level
/// abstraction reached via reflection on the NamedObjectsDictionary/CivilDocument,
/// not the ViewFrame/MatchLine entities above) — list/get/add-sheet/set-title-block/
/// update-alignment/create-view/set-view-scale are real. createSheetSet,
/// createPlanProfileSheet, publishSheetPdf, and exportSheetSet stay confirmed
/// stubs, consistent with this file's own header note above.
/// </summary>
public static class SheetProductionCommands
{
  public static Task<object?> ListViewFramesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var viewFrames = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var vf = tr.GetObject(id, OpenMode.ForRead) as ViewFrame;
        if (vf == null) continue;

        viewFrames.Add(new
        {
          name = vf.Name,
          handle = vf.Handle.ToString(),
          properties = GenericObjectCommands.SerializeSimpleProperties(vf),
        });
      }

      return new { viewFrames };
    });
  }

  public static Task<object?> ListMatchLinesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var matchLines = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var ml = tr.GetObject(id, OpenMode.ForRead) as MatchLine;
        if (ml == null) continue;

        matchLines.Add(new
        {
          handle = ml.Handle.ToString(),
          properties = GenericObjectCommands.SerializeSimpleProperties(ml),
        });
      }

      return new { matchLines };
    });
  }

  // ─────────────────────────────────────────────
  // Sheet set management (portado de Civil3D-mcp-main)
  // ─────────────────────────────────────────────

  public static Task<object?> ListSheetSetsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var sheetSets = EnumerateSheetSets(civilDoc, transaction)
        .Select(ss => ToSheetSetSummary(ss, transaction))
        .ToList();

      return new Dictionary<string, object?> { ["sheetSets"] = sheetSets };
    });
  }

  public static Task<object?> GetSheetSetInfoAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var sheetSet = FindSheetSetByName(civilDoc, transaction, name);

      var sheets = GetSheetIds(sheetSet, transaction)
        .Select(id => ToSheetSummary(transaction.GetObject(id, OpenMode.ForRead)))
        .ToList();

      return new Dictionary<string, object?>
      {
        ["name"] = GetName(sheetSet) ?? name,
        ["handle"] = GetHandleString(sheetSet),
        ["description"] = GetAnyString(sheetSet, "Description", "Desc"),
        ["sheets"] = sheets,
      };
    });
  }

  public static Task<object?> AddSheetAsync(JsonObject? parameters)
  {
    var sheetSetName = PluginRuntime.GetRequiredString(parameters, "sheetSetName");
    var sheetName = PluginRuntime.GetRequiredString(parameters, "sheetName");
    var sheetNumber = PluginRuntime.GetOptionalString(parameters, "sheetNumber");
    var layoutName = PluginRuntime.GetOptionalString(parameters, "layoutName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var sheetSet = FindSheetSetByName(civilDoc, transaction, sheetSetName);
      var sheet = AddSheetToSet(sheetSet, transaction, sheetName, sheetNumber ?? "1", layoutName);

      return new Dictionary<string, object?>
      {
        ["name"] = GetName(sheet) ?? sheetName,
        ["number"] = sheetNumber ?? "1",
        ["handle"] = GetHandleString(sheet),
        ["added"] = true,
      };
    });
  }

  public static Task<object?> GetSheetPropertiesAsync(JsonObject? parameters)
  {
    var sheetSetName = PluginRuntime.GetRequiredString(parameters, "sheetSetName");
    var sheetName = PluginRuntime.GetRequiredString(parameters, "sheetName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var sheetSet = FindSheetSetByName(civilDoc, transaction, sheetSetName);
      var sheet = FindSheetByName(sheetSet, transaction, sheetName);

      return ToSheetDetail(sheet, transaction);
    });
  }

  public static Task<object?> SetSheetTitleBlockAsync(JsonObject? parameters)
  {
    var sheetSetName = PluginRuntime.GetRequiredString(parameters, "sheetSetName");
    var sheetName = PluginRuntime.GetRequiredString(parameters, "sheetName");
    var titleBlockPath = FileBoundary.ResolveImportPath(
      PluginRuntime.GetRequiredString(parameters, "titleBlockPath"),
      ".dwg", ".dwt");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var sheetSet = FindSheetSetByName(civilDoc, transaction, sheetSetName);
      var sheet = FindSheetByName(sheetSet, transaction, sheetName);

      if (!TrySetStringProperty(sheet, titleBlockPath, "TitleBlockPath", "TitleBlock", "TemplatePath", "BlockPath"))
      {
        throw new JsonRpcDispatchException(
          "CIVIL3D.API_ERROR",
          $"Sheet '{sheetName}' does not expose a writable title-block property. No update was made.");
      }

      return new Dictionary<string, object?>
      {
        ["sheetName"] = sheetName,
        ["titleBlock"] = titleBlockPath,
        ["updated"] = true,
      };
    });
  }

  public static Task<object?> UpdatePlanProfileSheetAlignmentAsync(JsonObject? parameters)
  {
    var sheetSetName = PluginRuntime.GetRequiredString(parameters, "sheetSetName");
    var sheetName = PluginRuntime.GetRequiredString(parameters, "sheetName");
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var profileName = PluginRuntime.GetOptionalString(parameters, "profileName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var sheetSet = FindSheetSetByName(civilDoc, transaction, sheetSetName);
      var sheet = FindSheetByName(sheetSet, transaction, sheetName);
      var alignment = CivilObjectUtils.FindAlignmentByName(civilDoc, transaction, alignmentName);

      if (sheet is AcDbObject dbObj)
      {
        dbObj.UpgradeOpen();
        TrySetObjectIdPropertyOnObj(dbObj, alignment.ObjectId, "AlignmentId", "ReferenceAlignmentId");

        if (!string.IsNullOrWhiteSpace(profileName))
        {
          var profile = CivilObjectUtils.FindProfileByName(alignment, transaction, profileName!, OpenMode.ForRead);
          TrySetObjectIdPropertyOnObj(dbObj, profile.ObjectId, "ProfileId", "ReferenceProfileId");
        }
      }
      else
      {
        TrySetObjectIdPropertyOnObj(sheet, alignment.ObjectId, "AlignmentId", "ReferenceAlignmentId");
      }

      return new Dictionary<string, object?>
      {
        ["sheetName"] = sheetName,
        ["alignmentName"] = alignmentName,
        ["updated"] = true,
      };
    });
  }

  public static Task<object?> CreateSheetViewAsync(JsonObject? parameters)
  {
    var layoutName = PluginRuntime.GetRequiredString(parameters, "layoutName");
    var viewName = PluginRuntime.GetOptionalString(parameters, "viewName");
    var centerX = PluginRuntime.GetOptionalDouble(parameters, "centerX") ?? 0.0;
    var centerY = PluginRuntime.GetOptionalDouble(parameters, "centerY") ?? 0.0;
    var width = PluginRuntime.GetOptionalDouble(parameters, "width") ?? 8.0;
    var height = PluginRuntime.GetOptionalDouble(parameters, "height") ?? 6.0;
    var scale = PluginRuntime.GetOptionalDouble(parameters, "scale");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var layout = FindLayoutByName(database, transaction, layoutName);

      var viewport = new Viewport
      {
        CenterPoint = new Point3d(centerX, centerY, 0),
        Width = width,
        Height = height,
      };

      if (scale.HasValue)
      {
        viewport.CustomScale = 1.0 / scale.Value;
      }

      var layoutBlock = (BlockTableRecord)transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

      layoutBlock.AppendEntity(viewport);
      transaction.AddNewlyCreatedDBObject(viewport, true);
      viewport.On = true;

      if (!string.IsNullOrWhiteSpace(viewName))
      {
        TryApplyNamedViewToViewport(database, transaction, viewport, viewName!);
      }

      return new Dictionary<string, object?>
      {
        ["handle"] = viewport.Handle.ToString(),
        ["layoutName"] = layoutName,
        ["scale"] = scale,
        ["created"] = true,
      };
    });
  }

  public static Task<object?> SetSheetViewScaleAsync(JsonObject? parameters)
  {
    var layoutName = PluginRuntime.GetRequiredString(parameters, "layoutName");
    var viewportHandle = PluginRuntime.GetOptionalString(parameters, "viewportHandle");
    var scale = PluginRuntime.GetRequiredDouble(parameters, "scale");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var layout = FindLayoutByName(database, transaction, layoutName);
      var viewport = FindViewport(database, transaction, layout, viewportHandle);

      viewport.UpgradeOpen();
      viewport.CustomScale = 1.0 / scale;
      viewport.StandardScale = StandardScaleType.CustomScale;

      return new Dictionary<string, object?>
      {
        ["handle"] = viewport.Handle.ToString(),
        ["scale"] = scale,
        ["updated"] = true,
      };
    });
  }

  // -------------------------------------------------------------------------
  // Private helpers (portado de Civil3D-mcp-main)
  // -------------------------------------------------------------------------

  private static IEnumerable<AcDbObject> EnumerateSheetSets(Autodesk.Civil.ApplicationServices.CivilDocument civilDoc, Transaction transaction)
  {
    foreach (var memberName in new[] { "SheetSets", "SheetSetCollection", "PlanProductionSheetSets" })
    {
      var collection = GetNamedMember(civilDoc, memberName);
      if (collection == null) continue;

      foreach (var id in CivilObjectUtils.ToObjectIds(collection))
      {
        if (id != ObjectId.Null)
          yield return transaction.GetObject(id, OpenMode.ForRead);
      }

      foreach (var item in EnumerateObjects(collection))
      {
        if (item is AcDbObject dbObj) yield return dbObj;
      }
    }

    var database = CivilObjectUtils.GetDatabase(civilDoc);
    var nod = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);

    foreach (DictionaryEntry entry in nod)
    {
      if (entry.Value is ObjectId oid && oid != ObjectId.Null)
      {
        AcDbObject? obj = null;
        try { obj = transaction.GetObject(oid, OpenMode.ForRead); } catch { }
        if (obj != null && obj.GetType().Name.Contains("SheetSet"))
          yield return obj;
      }
    }
  }

  private static AcDbObject FindSheetSetByName(Autodesk.Civil.ApplicationServices.CivilDocument civilDoc, Transaction transaction, string name)
  {
    foreach (var sheetSet in EnumerateSheetSets(civilDoc, transaction))
    {
      if (string.Equals(GetName(sheetSet), name, StringComparison.OrdinalIgnoreCase))
        return sheetSet;
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Sheet set '{name}' was not found.");
  }

  private static IEnumerable<ObjectId> GetSheetIds(object sheetSet, Transaction transaction)
  {
    var result = CivilObjectUtils.InvokeMethod(sheetSet, "GetSheetIds");
    if (result != null)
    {
      foreach (var id in CivilObjectUtils.ToObjectIds(result))
        if (id != ObjectId.Null) yield return id;
      yield break;
    }

    foreach (var memberName in new[] { "Sheets", "SheetIds", "SheetCollection", "GetSheets" })
    {
      var value = GetNamedMember(sheetSet, memberName)
        ?? CivilObjectUtils.InvokeMethod(sheetSet, memberName);
      if (value == null) continue;

      foreach (var id in CivilObjectUtils.ToObjectIds(value))
        if (id != ObjectId.Null) yield return id;

      foreach (var item in EnumerateObjects(value))
      {
        if (item is AcDbObject dbObj) yield return dbObj.ObjectId;
      }
    }
  }

  private static AcDbObject FindSheetByName(AcDbObject sheetSet, Transaction transaction, string name)
  {
    foreach (var id in GetSheetIds(sheetSet, transaction))
    {
      var sheet = transaction.GetObject(id, OpenMode.ForRead);
      if (string.Equals(GetName(sheet), name, StringComparison.OrdinalIgnoreCase))
        return sheet;
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Sheet '{name}' was not found.");
  }

  private static AcDbObject AddSheetToSet(AcDbObject sheetSet, Transaction transaction, string sheetName, string sheetNumber, string? layoutName)
  {
    var addResult = CivilObjectUtils.InvokeMethod(sheetSet, "AddSheet", sheetName, sheetNumber)
      ?? CivilObjectUtils.InvokeMethod(sheetSet, "Add", sheetName);

    if (addResult is AcDbObject addedSheet)
      return addedSheet;

    if (addResult is ObjectId addedId && addedId != ObjectId.Null)
      return transaction.GetObject(addedId, OpenMode.ForRead);

    throw new JsonRpcDispatchException(
      "CIVIL3D.API_ERROR",
      $"Civil 3D could not add sheet '{sheetName}' to the sheet set. No simulated sheet was created.");
  }

  private static Dictionary<string, object?> ToSheetSetSummary(AcDbObject sheetSet, Transaction transaction)
  {
    var sheetCount = GetSheetIds(sheetSet, transaction).Count();
    return new Dictionary<string, object?>
    {
      ["name"] = GetName(sheetSet),
      ["handle"] = GetHandleString(sheetSet),
      ["description"] = GetAnyString(sheetSet, "Description", "Desc"),
      ["sheetCount"] = sheetCount,
    };
  }

  private static Dictionary<string, object?> ToSheetSummary(AcDbObject sheet)
  {
    return new Dictionary<string, object?>
    {
      ["name"] = GetName(sheet),
      ["number"] = GetAnyString(sheet, "Number", "SheetNumber") ?? "",
      ["handle"] = GetHandleString(sheet),
      ["layoutName"] = GetAnyString(sheet, "LayoutName", "Layout"),
    };
  }

  private static Dictionary<string, object?> ToSheetDetail(AcDbObject sheet, Transaction transaction)
  {
    double? viewportScale = null;
    var scaleVal = CivilObjectUtils.GetPropertyValue<double?>(sheet, "ViewportScale")
      ?? CivilObjectUtils.GetPropertyValue<double?>(sheet, "Scale");
    if (scaleVal.HasValue && scaleVal.Value > 0) viewportScale = scaleVal.Value;

    string? alignmentName = null;
    var alignmentId = GetFirstObjectId(sheet, "AlignmentId", "ReferenceAlignmentId");
    if (alignmentId != ObjectId.Null)
    {
      try
      {
        var obj = transaction.GetObject(alignmentId, OpenMode.ForRead);
        alignmentName = CivilObjectUtils.GetName(obj);
      }
      catch { }
    }

    string? profileName = null;
    var profileId = GetFirstObjectId(sheet, "ProfileId", "ReferenceProfileId");
    if (profileId != ObjectId.Null)
    {
      try
      {
        var obj = transaction.GetObject(profileId, OpenMode.ForRead);
        profileName = CivilObjectUtils.GetName(obj);
      }
      catch { }
    }

    return new Dictionary<string, object?>
    {
      ["name"] = GetName(sheet),
      ["number"] = GetAnyString(sheet, "Number", "SheetNumber") ?? "",
      ["handle"] = GetHandleString(sheet),
      ["layoutName"] = GetAnyString(sheet, "LayoutName", "Layout"),
      ["viewportScale"] = viewportScale,
      ["alignmentName"] = alignmentName,
      ["profileName"] = profileName,
      ["titleBlock"] = GetAnyString(sheet, "TitleBlockPath", "TitleBlock", "TemplatePath"),
    };
  }

  private static Layout FindLayoutByName(Database database, Transaction transaction, string layoutName)
  {
    var layoutDict = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
    foreach (DictionaryEntry entry in layoutDict)
    {
      var name = entry.Key as string;
      if (string.Equals(name, layoutName, StringComparison.OrdinalIgnoreCase))
      {
        if (entry.Value is ObjectId oid)
          return (Layout)transaction.GetObject(oid, OpenMode.ForRead);
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Layout '{layoutName}' was not found.");
  }

  private static Viewport FindViewport(Database database, Transaction transaction, Layout layout, string? viewportHandle)
  {
    var layoutBlock = (BlockTableRecord)transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);

    Viewport? first = null;
    foreach (ObjectId id in layoutBlock)
    {
      var obj = transaction.GetObject(id, OpenMode.ForRead);
      if (obj is Viewport vp)
      {
        if (!string.IsNullOrWhiteSpace(viewportHandle) &&
            string.Equals(vp.Handle.ToString(), viewportHandle, StringComparison.OrdinalIgnoreCase))
          return vp;
        first ??= vp;
      }
    }

    if (first != null) return first;
    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"No viewport found in layout '{layout.LayoutName}'.");
  }

  private static void TryApplyNamedViewToViewport(Database database, Transaction transaction, Viewport viewport, string viewName)
  {
    var viewTable = (ViewTable)transaction.GetObject(database.ViewTableId, OpenMode.ForRead);
    if (!viewTable.Has(viewName)) return;

    var viewRecord = (ViewTableRecord)transaction.GetObject(viewTable[viewName], OpenMode.ForRead);
    viewport.ViewCenter = new Point2d(viewRecord.CenterPoint.X, viewRecord.CenterPoint.Y);
    viewport.ViewHeight = viewRecord.Height;
  }

  private static string? GetName(object? value) => CivilObjectUtils.GetName(value);

  private static string GetHandleString(object? value)
  {
    if (value is AcDbObject dbObj) return dbObj.Handle.ToString();
    return CivilObjectUtils.GetStringProperty(value, "Handle") ?? "";
  }

  private static string? GetAnyString(object? value, params string[] propertyNames)
  {
    foreach (var name in propertyNames)
    {
      var v = CivilObjectUtils.GetStringProperty(value, name);
      if (!string.IsNullOrWhiteSpace(v)) return v;
    }
    return null;
  }

  private static object? GetNamedMember(object? value, string memberName)
  {
    return Civil3DCompatibility.GetPropertyValue(value, memberName)
      ?? Civil3DCompatibility.GetFieldValue(value, memberName);
  }

  private static IEnumerable<object> EnumerateObjects(object? collection)
  {
    if (collection is IEnumerable enumerable)
      foreach (var item in enumerable)
        if (item != null) yield return item;
  }

  private static bool TrySetStringProperty(object target, string value, params string[] propertyNames)
  {
    foreach (var name in propertyNames)
    {
      if (Civil3DCompatibility.TrySetProperty(target, name, value)) return true;
    }

    return false;
  }

  private static void TrySetObjectIdPropertyOnObj(object target, ObjectId objectId, params string[] propertyNames)
  {
    if (objectId == ObjectId.Null) return;
    foreach (var name in propertyNames)
    {
      if (Civil3DCompatibility.TrySetProperty(target, name, objectId)) return;
    }
  }

  private static ObjectId GetFirstObjectId(object target, params string[] propertyNames)
  {
    foreach (var propertyName in propertyNames)
    {
      var objectId = CivilObjectUtils.GetPropertyValue<ObjectId>(target, propertyName);
      if (objectId != ObjectId.Null)
        return objectId;
    }

    return ObjectId.Null;
  }
}
