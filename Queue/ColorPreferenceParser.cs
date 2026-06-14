namespace MaichessMatchMakerService.Queue;

internal static class ColorPreferenceParser
{
    // Parses the REST wire value. null/empty/"any"/"random" → Any ("random" is the
    // vs-bot synonym for "let the server flip a coin"); "white"/"black" → the fixed
    // colors. Returns false for anything else so the boundary can reject it.
    internal static bool TryParse(string? value, out ColorPreference preference)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "any" or "random":
                preference = ColorPreference.Any;
                return true;
            case "white":
                preference = ColorPreference.White;
                return true;
            case "black":
                preference = ColorPreference.Black;
                return true;
            default:
                preference = ColorPreference.Any;
                return false;
        }
    }

    // The lowercase token persisted in Redis / read back by ParseEntry.
    internal static string ToWire(ColorPreference preference) => preference switch
    {
        ColorPreference.White => "white",
        ColorPreference.Black => "black",
        _ => "any",
    };
}
