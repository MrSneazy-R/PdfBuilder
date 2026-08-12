using System.Text.Json;

namespace PdfBuilder.ValidationTests;

public sealed record FixtureManifestEntry(
    string Name,
    string Coverage,
    int PageCount,
    string[] TextMarkers,
    bool Visual,
    bool Deterministic = true,
    int[]? VisualPages = null,
    int? LinuxPageCount = null,
    int[]? LinuxVisualPages = null)
{
    public int ExpectedPageCount => OperatingSystem.IsLinux() ? LinuxPageCount ?? PageCount : PageCount;
    public IReadOnlyList<int>? ExpectedVisualPages => OperatingSystem.IsLinux() ? LinuxVisualPages ?? VisualPages : VisualPages;
}

public static class FixtureManifest
{
    public static IReadOnlyList<FixtureManifestEntry> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "FixtureManifest.json");
        var entries = JsonSerializer.Deserialize<List<FixtureManifestEntry>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return entries ?? throw new InvalidOperationException("Fixture manifest is empty.");
    }
}
