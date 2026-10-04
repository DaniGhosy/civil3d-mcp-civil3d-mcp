using System.Reflection;
using System.Text.Json.Nodes;

namespace Civil3DMcpPlugin;

/// <summary>
/// Handlers for civil3d_settings tool: list_settings_tree.
///
/// Feature/command settings live under civilDoc.Settings.GetSettings&lt;T&gt;() where T is a
/// per-feature settings root class (SettingsAlignment, SettingsSurface, etc.). Autodesk's own
/// devguide confirms the pattern but not a single consistent namespace across features — the
/// class was seen documented under both Autodesk.Civil.Settings and Autodesk.Civil.Land.Settings.
/// Rather than guess one namespace at compile time (which would need a build cycle per wrong
/// guess), the target type is resolved at RUNTIME via Civil3DCompatibility.FindLoadedType across
/// several candidate full names — a wrong guess just means that candidate doesn't match and the
/// next one is tried, no compile failure either way. GetSettings&lt;T&gt;() itself is invoked via
/// reflection (MakeGenericMethod) since T is only known once objectType is resolved.
///
/// Each leaf setting is wrapped in an object exposing a `.Value` property (confirmed by Autodesk's
/// own code sample: alignmentSettings.Angle.Precision.Value) rather than being a plain scalar —
/// the tree walker below detects this wrapper shape generically instead of hardcoding leaf names.
/// </summary>
public static class SettingsCommands
{
  private const int DefaultMaxDepth = 4;

  private static readonly string[] NamespacePrefixes =
  {
    "Autodesk.Civil.Settings.",
    "Autodesk.Civil.Land.Settings.",
    "Autodesk.Civil.DatabaseServices.",
  };

  private static readonly Dictionary<string, string[]> ObjectTypeClassNames = new(StringComparer.OrdinalIgnoreCase)
  {
    ["general"] = new[] { "SettingsAmbient" },
    ["alignment"] = new[] { "SettingsAlignment" },
    ["profile"] = new[] { "SettingsProfile" },
    ["profile_view"] = new[] { "SettingsProfileView" },
    ["corridor"] = new[] { "SettingsCorridor" },
    ["surface"] = new[] { "SettingsSurface" },
    ["parcel"] = new[] { "SettingsParcel" },
    ["pipe_network"] = new[] { "SettingsPipeNetwork" },
    ["pressure_network"] = new[] { "SettingsPressureNetwork", "SettingsPressurePipeNetwork" },
    ["point"] = new[] { "SettingsPoint" },
    ["point_group"] = new[] { "SettingsPointGroup" },
    ["assembly"] = new[] { "SettingsAssembly" },
    ["structure"] = new[] { "SettingsStructure" },
    ["section"] = new[] { "SettingsSection" },
    ["sample_line"] = new[] { "SettingsSampleLineGroup" },
    ["catchment"] = new[] { "SettingsCatchment" },
    ["survey"] = new[] { "SettingsSurvey" },
  };

  public static Task<object?> ListSettingsTreeAsync(JsonObject? parameters)
  {
    var objectType = PluginRuntime.GetRequiredString(parameters, "objectType");
    var maxDepth = PluginRuntime.GetOptionalInt(parameters, "maxDepth") ?? DefaultMaxDepth;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var (targetType, settingsObject) = ResolveSettingsInstance(civilDoc, objectType);
      var tree = WalkSettingsNode(settingsObject, depth: 0, maxDepth);

      return new Dictionary<string, object?>
      {
        ["objectType"] = objectType,
        ["settingsType"] = targetType.FullName,
        ["settings"] = tree,
      };
    });
  }

  public static Task<object?> SetFeatureSettingAsync(JsonObject? parameters)
  {
    var objectType = PluginRuntime.GetRequiredString(parameters, "objectType");
    var settingPath = PluginRuntime.GetRequiredString(parameters, "settingPath");
    var rawValue = ExtractRawValue(parameters, "value");
    if (rawValue == null)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Missing required parameter 'value'.");
    }

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var (_, settingsObject) = ResolveSettingsInstance(civilDoc, objectType);

      var segments = settingPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (segments.Length == 0)
      {
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
          "settingPath must be a non-empty dot-separated property path (e.g. 'Angle.Precision') — see list_settings_tree for valid paths.");
      }

      object current = settingsObject;
      foreach (var segment in segments)
      {
        var currentType = current.GetType();
        var property = FindMostDerivedProperty(currentType, segment, BindingFlags.Public | BindingFlags.Instance)
          ?? currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
              .FirstOrDefault(p => string.Equals(p.Name, segment, StringComparison.OrdinalIgnoreCase));
        if (property == null)
        {
          throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND",
            $"settingPath segment '{segment}' was not found on '{currentType.Name}'. Use list_settings_tree to see valid paths.");
        }

        object? next;
        try { next = property.GetValue(current); }
        catch (Exception ex)
        {
          throw new JsonRpcDispatchException("CIVIL3D.NOT_SUPPORTED", $"Reading '{segment}' threw: {ex.Message}");
        }
        if (next == null)
        {
          throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"settingPath segment '{segment}' resolved to null.");
        }
        current = next;
      }

      var valueProperty = FindMostDerivedProperty(current.GetType(), "Value", BindingFlags.Public | BindingFlags.Instance);
      if (valueProperty == null)
      {
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
          $"settingPath '{settingPath}' resolves to a settings GROUP ({current.GetType().Name}), not a leaf value — drill down further. Use list_settings_tree to see valid leaf paths.");
      }

      var applied = Civil3DCompatibility.TrySetProperty(current, "Value", rawValue);
      if (!applied)
      {
        throw new JsonRpcDispatchException("CIVIL3D.NOT_SUPPORTED",
          $"'{settingPath}'.Value is read-only or rejected the value '{rawValue}' (expected type {valueProperty.PropertyType.Name}).");
      }

      return new Dictionary<string, object?>
      {
        ["objectType"] = objectType,
        ["settingPath"] = settingPath,
        ["value"] = rawValue,
        ["success"] = true,
      };
    });
  }

  private static (Type TargetType, object SettingsObject) ResolveSettingsInstance(
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc, string objectType)
  {
    if (!ObjectTypeClassNames.TryGetValue(objectType, out var classNames))
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
        $"Unsupported settings object type '{objectType}'. Valid values: {string.Join(", ", ObjectTypeClassNames.Keys)}.");
    }

    var candidates = classNames
      .SelectMany(name => NamespacePrefixes.Select(prefix => prefix + name))
      .ToArray();
    var targetType = Civil3DCompatibility.FindLoadedType(candidates);
    if (targetType == null)
    {
      throw new JsonRpcDispatchException("CIVIL3D.NOT_SUPPORTED",
        $"Could not resolve a loaded settings type for '{objectType}'. Tried: {string.Join(", ", candidates)}.");
    }

    var settingsRoot = civilDoc.Settings;
    var getSettingsMethod = settingsRoot.GetType()
      .GetMethods(BindingFlags.Public | BindingFlags.Instance)
      .FirstOrDefault(m => m.Name == "GetSettings" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
    if (getSettingsMethod == null)
    {
      throw new JsonRpcDispatchException("CIVIL3D.NOT_SUPPORTED",
        "SettingsRoot.GetSettings<T>() could not be located via reflection on this Civil 3D version.");
    }

    object? settingsObject;
    try
    {
      settingsObject = getSettingsMethod.MakeGenericMethod(targetType).Invoke(settingsRoot, null);
    }
    catch (TargetInvocationException ex)
    {
      throw new JsonRpcDispatchException("CIVIL3D.NOT_SUPPORTED",
        $"GetSettings<{targetType.FullName}>() failed for object type '{objectType}': {ex.InnerException?.Message ?? ex.Message}");
    }

    if (settingsObject == null)
    {
      throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND",
        $"No settings instance is available for object type '{objectType}' ({targetType.FullName}).");
    }

    return (targetType, settingsObject);
  }

  private static object? ExtractRawValue(JsonObject? parameters, string name)
  {
    var node = parameters?[name];
    if (node is not JsonValue jsonValue)
    {
      return node?.ToString();
    }

    if (jsonValue.TryGetValue<bool>(out var b)) return b;
    if (jsonValue.TryGetValue<long>(out var l)) return l;
    if (jsonValue.TryGetValue<double>(out var d)) return d;
    if (jsonValue.TryGetValue<string>(out var s)) return s;
    return node.ToString();
  }

  private static object? WalkSettingsNode(object? node, int depth, int maxDepth)
  {
    if (node == null) return null;

    var type = node.GetType();

    try
    {
      if (IsSimpleValue(type))
      {
        return NormalizeSimpleValue(node);
      }

      var valueProperty = FindMostDerivedProperty(type, "Value", BindingFlags.Public | BindingFlags.Instance);
      if (valueProperty != null && valueProperty.CanRead && valueProperty.GetIndexParameters().Length == 0)
      {
        object? raw;
        try { raw = valueProperty.GetValue(node); }
        catch { raw = null; }

        return new Dictionary<string, object?>
        {
          ["value"] = raw == null
            ? null
            : (IsSimpleValue(raw.GetType()) ? NormalizeSimpleValue(raw) : raw.ToString()),
        };
      }

      var isSettingsNamespace = type.Namespace != null
        && type.Namespace.IndexOf("Settings", StringComparison.Ordinal) >= 0;
      if (!isSettingsNamespace)
      {
        return type.Name;
      }

      if (depth >= maxDepth)
      {
        return new Dictionary<string, object?> { ["_type"] = type.Name, ["_truncated"] = true };
      }

      var result = new Dictionary<string, object?>();
      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (property.GetIndexParameters().Length > 0) continue;
        if (!property.CanRead) continue;

        object? child;
        try { child = property.GetValue(node); }
        catch { continue; }
        if (child == null) continue;

        try
        {
          result[property.Name] = WalkSettingsNode(child, depth + 1, maxDepth);
        }
        catch (Exception ex)
        {
          // One misbehaving branch (e.g. a getter that throws for state-dependent reasons)
          // shouldn't take down the whole tree — record it and keep walking siblings.
          result[property.Name] = new Dictionary<string, object?> { ["_error"] = ex.Message };
        }
      }

      return result;
    }
    catch (Exception ex)
    {
      // Defensive: reflection over Civil 3D's settings-wrapper hierarchy is not fully mapped
      // (see class remarks) — surface whatever goes wrong on this one node as data instead of
      // letting it abort the entire list_settings_tree call.
      return new Dictionary<string, object?> { ["_type"] = type.Name, ["_error"] = ex.Message };
    }
  }

  /// <summary>
  /// A derived type can hide a base type's same-named property with `new` — Civil 3D's typed
  /// settings-value wrappers do this pervasively (a generic Value on a base class, redeclared
  /// with a specific type on each leaf subclass; confirmed live: every settings tree tested hit
  /// "Ambiguous match found" before this fix). Plain Type.GetProperty(name, flags) throws
  /// AmbiguousMatchException the instant both show up as matching candidates. Walking the
  /// hierarchy with DeclaredOnly at each level sidesteps the ambiguity and returns the
  /// most-derived declaration, matching normal C# member-hiding semantics.
  /// </summary>
  private static PropertyInfo? FindMostDerivedProperty(Type type, string name, BindingFlags flags)
  {
    for (var current = type; current != null; current = current.BaseType)
    {
      PropertyInfo? property;
      try
      {
        property = current.GetProperty(name, flags | BindingFlags.DeclaredOnly);
      }
      catch (AmbiguousMatchException)
      {
        property = current.GetProperties(flags | BindingFlags.DeclaredOnly)
          .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
      }
      if (property != null)
      {
        return property;
      }
    }
    return null;
  }

  private static bool IsSimpleValue(Type type)
  {
    var underlying = Nullable.GetUnderlyingType(type) ?? type;
    return underlying.IsEnum
      || underlying == typeof(string)
      || underlying == typeof(bool)
      || underlying == typeof(int)
      || underlying == typeof(long)
      || underlying == typeof(double)
      || underlying == typeof(float)
      || underlying == typeof(decimal)
      || underlying == typeof(DateTime);
  }

  private static object? NormalizeSimpleValue(object value)
  {
    return value is Enum ? value.ToString() : value;
  }
}
