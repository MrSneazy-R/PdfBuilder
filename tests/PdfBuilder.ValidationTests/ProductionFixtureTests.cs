using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PdfBuilder.ValidationTests;

public sealed class ProductionFixtureTests
{
    [Fact]
    public void ProductionFixtures_GenerateDeterministically_WithDeclaredPageCounts()
    {
        var productionFixtures = FixtureManifest.Load()
            .Where(entry => entry.Name.StartsWith("production-", StringComparison.Ordinal))
            .ToArray();
        var counts = new List<string>();
        var pageCountFailures = new List<string>();

        foreach (FixtureManifestEntry fixture in productionFixtures)
        {
            byte[] first;
            byte[] second;
            try
            {
                first = ValidationFixtureFactory.Generate(fixture.Name);
                second = ValidationFixtureFactory.Generate(fixture.Name);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"{fixture.Name} failed generation: {exception}");
            }
            if (fixture.Deterministic)
                first.Should().Equal(second, $"{fixture.Name} is declared deterministic");
            string? outputDirectory = Environment.GetEnvironmentVariable("PDFBUILDER_FIXTURE_OUTPUT_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllBytes(Path.Combine(outputDirectory, fixture.Name + ".pdf"), first);
            }
            int pages = CountPages(first);
            counts.Add($"{fixture.Name}={pages}");
            if (fixture.ExpectedPageCount > 0 && pages != fixture.ExpectedPageCount)
                pageCountFailures.Add($"{fixture.Name}: expected {fixture.ExpectedPageCount}, actual {pages}");
        }

        if (productionFixtures.Any(fixture => fixture.PageCount <= 0))
            throw new Xunit.Sdk.XunitException("Record generated page counts in FixtureManifest.json: " + string.Join(", ", counts));
        if (pageCountFailures.Count > 0)
            throw new Xunit.Sdk.XunitException("Production fixture page-count validation failed: " + string.Join(", ", pageCountFailures));
    }

    [Fact]
    public void ConcurrentBatchGeneration_ProducesIdenticalIndependentDocuments()
    {
        byte[] expected = ValidationFixtureFactory.Generate("production-concurrent-batch");
        byte[][] outputs = Enumerable.Range(0, 24)
            .AsParallel()
            .WithDegreeOfParallelism(Math.Min(8, Environment.ProcessorCount))
            .Select(_ => ValidationFixtureFactory.Generate("production-concurrent-batch"))
            .ToArray();

        outputs.Should().OnlyContain(output => output.SequenceEqual(expected));
    }

    [Fact]
    public void Manifest_ContainsEveryRequiredProductionFixture()
    {
        string[] required =
        {
            "production-invoice", "production-credit-note", "production-customer-statement",
            "production-fuel-transactions", "production-operational-report", "production-management-report",
            "production-multilingual-latin", "production-arabic-rtl", "production-hebrew-mixed", "production-cjk",
            "production-image-heavy", "production-1000-row-table", "production-spanned-split-row",
            "production-navigation", "production-page-variants", "production-concurrent-batch", "production-serializer-edge"
        };

        FixtureManifest.Load().Select(entry => entry.Name).Should().Contain(required);
    }

    private static int CountPages(byte[] pdf)
    {
        string text = Encoding.Latin1.GetString(pdf);
        return Regex.Matches(text, @"/Type\s*/Page(?!s)\b", RegexOptions.CultureInvariant).Count;
    }
}
