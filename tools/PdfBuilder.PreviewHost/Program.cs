using System.Net;
using PdfBuilder.Document.Layout;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(server => server.ListenLocalhost(5080));
builder.Services.AddSingleton<PreviewWorkspace>();

var app = builder.Build();

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    PreviewWorkspace workspace = app.Services.GetRequiredService<PreviewWorkspace>();
    PreviewManifest manifest = workspace.GetManifest();
    byte[] diagnosticPreview = workspace.GetPreview(1, 72, guides: true, cancellationToken: CancellationToken.None);
    byte[] cleanPreview = workspace.GetPreview(1, 72, guides: false, cancellationToken: CancellationToken.None);
    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "preview-host.pdf"), workspace.GetPdf());
    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "preview-host-diagnostic.png"), diagnosticPreview);
    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "preview-host-clean.png"), cleanPreview);
    Console.WriteLine($"Preview host self-test passed: {manifest.Pages.Count} page(s), {manifest.PdfBytes} PDF bytes.");
    return;
}

app.Use(async (context, next) =>
{
    IPAddress? remote = context.Connection.RemoteIpAddress;
    if (remote is not null && !IPAddress.IsLoopback(remote))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "The preview host accepts loopback connections only." });
        return;
    }

    try
    {
        await next();
    }
    catch (Exception exception)
    {
        context.Response.StatusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            OperationCanceledException => 499,
            _ => StatusCodes.Status500InternalServerError
        };
        await context.Response.WriteAsJsonAsync(StructuredPreviewError.From(exception));
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/manifest", (PreviewWorkspace workspace) => workspace.GetManifest());
app.MapGet("/api/trace", (PreviewWorkspace workspace) => workspace.GetTrace());
app.MapGet("/api/hierarchy", (PreviewWorkspace workspace) => workspace.GetHierarchy());
app.MapGet("/api/document.pdf", (PreviewWorkspace workspace) =>
    Results.File(workspace.GetPdf(), "application/pdf", "pdfbuilder-preview.pdf"));
app.MapGet("/api/pages/{pageNumber:int}.png", (
    int pageNumber,
    int? dpi,
    bool? guides,
    PreviewWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    int requestedDpi = dpi ?? 120;
    if (requestedDpi is < 36 or > 300)
        throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be between 36 and 300.");
    return Results.File(workspace.GetPreview(pageNumber, requestedDpi, guides ?? true, cancellationToken), "image/png");
});
app.MapPost("/api/reload", (PreviewWorkspace workspace) =>
{
    workspace.Reload();
    return Results.Ok(workspace.GetManifest());
});

app.Run();
