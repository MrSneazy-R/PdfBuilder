# Contributing to PdfBuilder

PdfBuilder is currently pre-release. Contributions should be small, focused, and preserve existing rendering unless a change explicitly includes generated-output validation.

## Prerequisites

- The .NET 10 SDK selected by `global.json`
- Windows for the full image feature set; WebP decoding uses Windows Imaging Component (WIC)

## Local validation

Run the following from the repository root:

```powershell
dotnet restore PdfBuilder.sln
dotnet format PdfBuilder.sln --verify-no-changes
dotnet build PdfBuilder.sln -c Debug
dotnet build PdfBuilder.sln -c Release
dotnet test tests/PdfBuilder.Tests/PdfBuilder.Tests.csproj -c Release
dotnet pack PdfBuilder.csproj -c Release -o ./artifacts
pwsh -File eng/Invoke-PackageSmokeTest.ps1 -PackageDirectory ./artifacts
```

Keep public API changes documented with XML comments, an example, unit tests, a public API baseline update, and a compatibility assessment. Do not commit generated PDFs, fonts, secrets, or sensitive business data.

Use Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, and so on), and complete the pull request template.
