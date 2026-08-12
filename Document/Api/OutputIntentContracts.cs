namespace PdfBuilder.Document;

/// <summary>Configures an ICC output intent without exposing mutable profile storage.</summary>
public interface IPdfOutputIntentDescriptor
{
    /// <summary>Sets and validates caller-provided ICC profile bytes.</summary>
    void Profile(ReadOnlyMemory<byte> profile);
    /// <summary>Sets the required output-condition identifier.</summary>
    void Identifier(string identifier);
    /// <summary>Sets optional human-readable output-condition information.</summary>
    void Info(string info);
    /// <summary>Sets the ICC registry name. The default is http://www.color.org.</summary>
    void RegistryName(string registryName);
}
