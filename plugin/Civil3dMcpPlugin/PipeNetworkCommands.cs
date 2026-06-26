using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.ApplicationServices;

namespace Civil3DMcpPlugin;

public static class PipeNetworkCommands
{
  public static Task<object?> ListPipeNetworksAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var networks = new List<object>();

      foreach (ObjectId id in civilDoc.GetPipeNetworkIds())
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj != null)
        {
          networks.Add(new
          {
            name = obj.GetType().Name,
            handle = obj.Handle.ToString()
          });
        }
      }

      return new { networks };
    });
  }

  public static Task<object?> GetPipeNetworkAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "networkName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      foreach (ObjectId id in civilDoc.GetPipeNetworkIds())
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj != null &&
            obj.GetType().GetProperty("Name")?.GetValue(obj)?.ToString()
            ?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
        {
          return new
          {
            name,
            handle = obj.Handle.ToString()
          };
        }
      }

      throw new JsonRpcDispatchException(
        "CIVIL3D.NOT_FOUND",
        $"Pipe network '{name}' not found."
      );
    });
  }

  public static Task<object?> GetPipeAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> GetStructureAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> CreatePipeNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> AddPipeToNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> AddStructureToNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> CheckPipeNetworkInterferenceAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });
}