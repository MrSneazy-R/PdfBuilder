using System.Reflection;
using FluentAssertions;
using PdfBuilder.Document;
using Xunit;

namespace PdfBuilder.Tests;

public sealed class CanonicalApiArchitectureTests
{
    private static readonly Type[] CanonicalContractTypes =
    [
        typeof(IDocumentDescriptor),
        typeof(IPageDescriptor),
        typeof(IContainer),
        typeof(IColumnDescriptor),
        typeof(IRowDescriptor),
        typeof(IGridDescriptor),
        typeof(IStackDescriptor),
        typeof(ILayerDescriptor),
        typeof(ITextStyleDescriptor),
        typeof(ITextDescriptor),
        typeof(IRichTextDescriptor),
        typeof(ISectionDescriptor),
        typeof(ITableOfContentsDescriptor),
        typeof(IImageDescriptor),
        typeof(ImageSource),
        typeof(ImageSourceInfo),
        typeof(ImageQuality),
        typeof(ImageCropAlignment),
        typeof(ICanvasDescriptor),
        typeof(CanvasSize),
        typeof(CanvasLayer),
        typeof(CanvasLinePattern),
        typeof(IChartDescriptor),
        typeof(IChartAxisDescriptor),
        typeof(IChartSeriesDescriptor),
        typeof(ILineChartSeriesDescriptor),
        typeof(IAreaChartSeriesDescriptor),
        typeof(IBarChartSeriesDescriptor),
        typeof(IPieChartSeriesDescriptor),
        typeof(IScatterChartSeriesDescriptor),
        typeof(ChartPoint),
        typeof(ChartValue),
        typeof(ChartLegendPosition),
        typeof(ChartMarkerShape),
        typeof(ITableDescriptor),
        typeof(ITableColumnsDescriptor),
        typeof(ITableRowDescriptor),
        typeof(ITableCellDescriptor),
        typeof(PageContext),
        typeof(PageTextTokens),
        typeof(PdfNavigationDiagnostic),
        typeof(PdfNavigationDiagnostics),
        typeof(PdfNavigationException),
        typeof(PageOrientation),
        typeof(PageSize),
        typeof(PageSizes),
        typeof(Units)
    ];

    [Fact]
    public void CanonicalApi_PublicSurface_MatchesCheckedInPublicApiBaseline()
    {
        string baseline = string.Join(
            Environment.NewLine,
            File.ReadAllText(FindRepositoryPath("PublicAPI.Shipped.txt")),
            File.ReadAllText(FindRepositoryPath("PublicAPI.Unshipped.txt")));

        foreach (Type contractType in CanonicalContractTypes)
        {
            contractType.IsPublic.Should().BeTrue($"{contractType.FullName} is a canonical public contract");
            baseline.Should().Contain(contractType.FullName!, $"{contractType.FullName} must remain covered by the compiler-enforced public API baseline");
        }
    }

    [Fact]
    public void CanonicalApi_Contracts_DoNotExposeWriterRendererOrSystemDrawingTypes()
    {
        Type[] exposedTypes = CanonicalContractTypes
            .SelectMany(GetPublicSignatureTypes)
            .SelectMany(FlattenType)
            .Distinct()
            .ToArray();

        exposedTypes.Should().NotContain(type => type.Namespace != null && type.Namespace.StartsWith("PdfBuilder.Writer", StringComparison.Ordinal));
        exposedTypes.Should().NotContain(type => type.Namespace != null && type.Namespace.StartsWith("PdfBuilder.Writer.Renderers", StringComparison.Ordinal));
        exposedTypes.Should().NotContain(type => type.Namespace != null && type.Namespace.StartsWith("System.Drawing", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalApi_InternalAdapters_RemainPrivateImplementationDetails()
    {
        string[] expectedAdapters =
        [
            "CanonicalDocumentDescriptor",
            "CanonicalPageDescriptor",
            "CanonicalContainer",
            "CanonicalImageDescriptor",
            "CanonicalCanvasDescriptor",
            "CanonicalChartDescriptor",
            "CanonicalTableDescriptor",
            "CanonicalTableColumnsDescriptor",
            "CanonicalTableRowDescriptor",
            "CanonicalTableCellDescriptor",
            "CanonicalTableBorderDescriptor",
            "CanonicalTableBandingDescriptor",
            "CanonicalColumnDescriptor",
            "CanonicalRowDescriptor",
            "CanonicalGridDescriptor",
            "CanonicalStackDescriptor",
            "CanonicalLayerDescriptor",
            "CanonicalTextStyle",
            "CanonicalRichTextDescriptor",
            "CanonicalSectionDescriptor",
            "CanonicalTableOfContentsDescriptor"
        ];

        Dictionary<string, Type> nestedTypes = typeof(PdfDocument)
            .GetNestedTypes(BindingFlags.NonPublic)
            .ToDictionary(type => type.Name, StringComparer.Ordinal);

        foreach (string adapterName in expectedAdapters)
        {
            nestedTypes.Should().ContainKey(adapterName);
            nestedTypes[adapterName].IsNestedPrivate.Should().BeTrue($"{adapterName} must not become public API");
        }
    }

    [Fact]
    public void CanonicalApi_Source_IsSeparatedIntoFocusedContractAndAdapterFiles()
    {
        string[] contractFiles =
        [
            "Document/Api/DocumentContracts.cs",
            "Document/Api/PageContracts.cs",
            "Document/Api/ContainerContracts.cs",
            "Document/Api/TextContracts.cs",
            "Document/Api/TableContracts.cs",
            "Document/Api/ChartContracts.cs",
            "Document/Api/MediaContracts.cs",
            "Document/Api/ImageSource.cs",
            "Document/Api/GraphicsContracts.cs",
            "Document/Api/NavigationContracts.cs"
        ];
        string[] adapterFiles =
        [
            "Document/Canonical/PdfDocumentCanonicalApi.cs",
            "Document/Canonical/CanonicalDocumentDescriptor.cs",
            "Document/Canonical/CanonicalPageDescriptor.cs",
            "Document/Canonical/CanonicalContainer.cs",
            "Document/Canonical/CanonicalTextDescriptor.cs",
            "Document/Canonical/CanonicalTableDescriptor.cs",
            "Document/Canonical/CanonicalChartDescriptor.cs",
            "Document/Canonical/CanonicalMediaDescriptor.cs",
            "Document/Canonical/CanonicalGraphicsDescriptor.cs",
            "Document/Canonical/CanonicalNavigationDescriptor.cs"
        ];

        File.Exists(Path.Combine(FindRepositoryRoot(), "Document/CanonicalDocumentApi.cs")).Should().BeFalse();

        foreach (string relativePath in contractFiles)
        {
            string source = File.ReadAllText(FindRepositoryPath(relativePath));
            source.Should().NotContain("PdfBuilder.Writer");
            source.Should().NotContain("using System.Drawing");
            source.Should().NotContain("private sealed class Canonical");
        }

        foreach (string relativePath in adapterFiles)
        {
            string source = File.ReadAllText(FindRepositoryPath(relativePath));
            source.Should().NotContain("public interface");
        }
    }

    [Fact]
    public void CanonicalChartRenderer_IsSplitIntoFocusedPdfColorOnlyModules()
    {
        string[] modules =
        [
            "Writer/Renderers/Charts/ChartLayout.cs",
            "Writer/Renderers/Charts/ChartScales.cs",
            "Writer/Renderers/Charts/ChartTicks.cs",
            "Writer/Renderers/Charts/ChartAxesRenderer.cs",
            "Writer/Renderers/Charts/ChartLegendRenderer.cs",
            "Writer/Renderers/Charts/ChartLabelRenderer.cs",
            "Writer/Renderers/Charts/ChartDrawing.cs",
            "Writer/Renderers/Charts/LineChartSeriesRenderer.cs",
            "Writer/Renderers/Charts/BarChartSeriesRenderer.cs",
            "Writer/Renderers/Charts/PieChartSeriesRenderer.cs",
            "Writer/Renderers/Charts/ScatterChartSeriesRenderer.cs"
        ];

        foreach (string module in modules)
        {
            string source = File.ReadAllText(FindRepositoryPath(module));
            source.Should().NotContain("System.Drawing");
        }
    }

    private static IEnumerable<Type> GetPublicSignatureTypes(Type contractType)
    {
        yield return contractType;

        foreach (ConstructorInfo constructor in contractType.GetConstructors())
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
                yield return parameter.ParameterType;
        }

        foreach (MethodInfo method in contractType.GetMethods())
        {
            yield return method.ReturnType;
            foreach (ParameterInfo parameter in method.GetParameters())
                yield return parameter.ParameterType;
        }

        foreach (PropertyInfo property in contractType.GetProperties())
            yield return property.PropertyType;

        foreach (FieldInfo field in contractType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            yield return field.FieldType;
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is Type elementType)
        {
            foreach (Type nested in FlattenType(elementType))
                yield return nested;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in FlattenType(argument))
                yield return nested;
        }
    }

    private static string FindRepositoryPath(string relativePath)
        => Path.Combine(FindRepositoryRoot(), relativePath);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PdfBuilder.csproj")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("PdfBuilder repository root was not found.");
    }
}
