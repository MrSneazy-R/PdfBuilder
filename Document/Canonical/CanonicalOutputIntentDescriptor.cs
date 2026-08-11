using PdfBuilder.Models;

namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalOutputIntentDescriptor : IPdfOutputIntentDescriptor
    {
        private readonly PdfOutputIntent _intent;
        internal CanonicalOutputIntentDescriptor(PdfOutputIntent intent) => _intent = intent;

        public void Profile(ReadOnlyMemory<byte> profile) => _intent.SetProfile(profile.ToArray());
        public void Identifier(string identifier)
            => _intent.Identifier = Require(identifier, nameof(identifier));
        public void Info(string info) => _intent.Info = Require(info, nameof(info));
        public void RegistryName(string registryName)
            => _intent.RegistryName = Require(registryName, nameof(registryName));

        private static string Require(string value, string parameterName)
            => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("A non-empty output-intent value is required.", parameterName)
                : value.Trim();
    }
}
