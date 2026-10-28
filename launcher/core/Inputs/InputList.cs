using System.Collections.Generic;
using System.Text.Json;

namespace QuietHome.Core.Inputs;

/// <summary>
/// Reads the TV's source list and tells whether it changed. The TV host gets the JSON text
/// from libsource-api, passes it to <see cref="Parse"/>, and compares
/// <see cref="Fingerprint"/> before and after to decide whether the Home page must be redrawn.
/// </summary>
public static class InputList
{
    static readonly JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Returns the connected inputs from the source list JSON, in the TV's order. A JSON
    /// <c>null</c> gives an empty list. Text that is not valid JSON throws
    /// <see cref="JsonException"/>; the caller records the message and shows no inputs.
    /// </summary>
    public static List<InputSource> Parse(string json)
    {
        var all = JsonSerializer.Deserialize<List<InputSource>>(json, options) ?? new List<InputSource>();
        return all.FindAll(s => s.ConnectionState);
    }

    /// <summary>
    /// Returns a string that changes whenever an input is added, removed, renamed or gets
    /// another icon, so two lists can be compared with a plain string comparison.
    /// </summary>
    public static string Fingerprint(List<InputSource> inputs) =>
        string.Join(";", inputs.ConvertAll(s => s.SourceType + ":" + s.EditName + ":" + s.DetectedName + ":" + s.IconPath));
}
