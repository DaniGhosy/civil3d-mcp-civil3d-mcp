using System.Text.Json.Nodes;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Base primitives for Civil 3D Parcels (Mes 1 scope): list sites, list/get/delete
/// parcels. Parcel creation via the .NET API requires a full layout workflow
/// (segments/boundaries defined against a Site) that cannot be safely written and
/// verified without a live Civil 3D session, so it is left as a documented stub —
/// see CreateParcelAsync.
/// </summary>
public static class ParcelCommands
{
  public static Task<object?> ListParcelSitesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var sites = new List<object>();

      foreach (ObjectId id in civilDoc.GetSiteIds())
      {
        var site = tr.GetObject(id, OpenMode.ForRead) as Site;
        if (site == null) continue;

        sites.Add(new
        {
          name = site.Name,
          handle = site.Handle.ToString(),
          parcelCount = site.GetParcelIds().Count,
        });
      }

      return new { sites };
    });
  }

  public static Task<object?> ListParcelsAsync(JsonObject? parameters)
  {
    var siteName = PluginRuntime.GetOptionalString(parameters, "siteName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var parcels = new List<object>();

      foreach (ObjectId siteId in civilDoc.GetSiteIds())
      {
        var site = tr.GetObject(siteId, OpenMode.ForRead) as Site;
        if (site == null) continue;

        if (siteName != null && !string.Equals(site.Name, siteName, StringComparison.OrdinalIgnoreCase))
          continue;

        foreach (ObjectId parcelId in site.GetParcelIds())
        {
          var parcel = tr.GetObject(parcelId, OpenMode.ForRead) as Parcel;
          if (parcel == null) continue;

          parcels.Add(new
          {
            name = parcel.Name,
            handle = parcel.Handle.ToString(),
            siteName = site.Name,
            area = parcel.Area,
            layer = parcel.Layer,
          });
        }
      }

      return new { parcels };
    });
  }

  public static Task<object?> GetParcelAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var (parcel, siteName) = FindParcelByName(civilDoc, tr, name);

      return new
      {
        name = parcel.Name,
        handle = parcel.Handle.ToString(),
        siteName,
        area = parcel.Area,
        style = parcel.StyleName,
        layer = parcel.Layer,
      };
    });
  }

  public static Task<object?> DeleteParcelAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var (parcel, _) = FindParcelByName(civilDoc, tr, name);
      parcel.UpgradeOpen();
      parcel.Erase();

      return new { success = true, deleted = name };
    });
  }

  // ─────────────────────────────────────────────
  // Creación de parcelas por layout: se intentó una implementación real usando
  // Parcel.CreateByLayout(ObjectIdCollection, ObjectId, ObjectId, ObjectId), pero
  // el compilador confirmó que ese miembro no existe con esa firma ('Parcel' no
  // contiene una definición para 'CreateByLayout'). Revertido a stub explícito
  // en vez de seguir adivinando la firma real, siguiendo el mismo patrón que
  // createAssembly/createGradingGroup.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateParcelAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Parcel creation requires a layout workflow (segments/boundary against a Site) " +
             "whose exact factory method needs to be confirmed against a live Civil 3D drawing " +
             "(Parcel.CreateByLayout, the initial guess, does not exist with that signature)."
    });

  private static (Parcel parcel, string siteName) FindParcelByName(dynamic civilDoc, Transaction tr, string name)
  {
    foreach (ObjectId siteId in civilDoc.GetSiteIds())
    {
      var site = tr.GetObject(siteId, OpenMode.ForRead) as Site;
      if (site == null) continue;

      foreach (ObjectId parcelId in site.GetParcelIds())
      {
        var parcel = tr.GetObject(parcelId, OpenMode.ForRead) as Parcel;
        if (parcel != null && string.Equals(parcel.Name, name, StringComparison.OrdinalIgnoreCase))
        {
          return (parcel, site.Name);
        }
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Parcel '{name}' not found");
  }
}
