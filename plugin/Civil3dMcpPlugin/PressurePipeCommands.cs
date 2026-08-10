using System.Text.Json.Nodes;

namespace Civil3DMcpPlugin;

/// <summary>
/// Pressure pipe networks (Mes 5, S3): a separate object model from gravity
/// networks, living in a separate managed assembly (AeccPressurePipesMgd.dll,
/// now referenced in the .csproj). Confirmed by the compiler: the assembly
/// reference resolves and the `PressurePipeNetwork` type itself is found
/// under `Autodesk.Civil.DatabaseServices` (no error was raised about that
/// type). Everything below is stubbed because two follow-up guesses failed:
///
/// 1. `CivilDocumentPressurePipesExtension` (the static class with
///    GetPressurePipeNetworkIds, cited in a working Autodesk forum sample)
///    is not resolvable from `Autodesk.Civil.DatabaseServices` nor from
///    `Autodesk.Civil.DatabaseServices.PressurePipes` — its real namespace
///    is still unknown.
/// 2. `PressurePipeNetwork.GetPressurePartsIds()` does not exist under that
///    name (guessed by analogy with gravity Network.GetPipeIds()).
///
/// Rather than keep guessing namespaces blindly, this is left as a
/// documented stub set — the fastest way to find the real namespace is
/// Visual Studio's Object Browser pointed at AeccPressurePipesMgd.dll, or
/// ildasm/ILSpy against the same file, from within a live Civil 3D session.
/// </summary>
public static class PressurePipeCommands
{
  public static Task<object?> ListPressureNetworksAsync()
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "AeccPressurePipesMgd.dll is referenced and PressurePipeNetwork resolves, but the real " +
             "namespace of the network-listing extension method (CivilDocumentPressurePipesExtension) " +
             "could not be confirmed. Needs confirmation against a live Civil 3D drawing " +
             "(e.g. via Visual Studio's Object Browser on AeccPressurePipesMgd.dll)."
    });

  public static Task<object?> GetPressureNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned", note = "Depends on listPressureNetworks — see its note." });

  public static Task<object?> ListPressurePartsAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "PressurePipeNetwork.GetPressurePartsIds() does not exist under that name — confirmed by the compiler. Needs the real part-enumeration member confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> GetPressurePartAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned", note = "Depends on listPressureParts — see its note." });

  public static Task<object?> CreatePressureNetworkAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "PressurePipeNetwork.Create exists (per Autodesk's reference index) but its exact signature needs confirmation against a live Civil 3D drawing."
    });
}
