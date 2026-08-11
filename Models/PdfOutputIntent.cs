namespace PdfBuilder.Models;

/// <summary>An ICC-based output intent embedded in the generated PDF.</summary>
public sealed class PdfOutputIntent
{
    private byte[] _profile = Array.Empty<byte>();

    /// <summary>Gets the output-condition identifier.</summary>
    public string Identifier { get; internal set; } = string.Empty;
    /// <summary>Gets optional human-readable output-condition information.</summary>
    public string? Info { get; internal set; }
    /// <summary>Gets the ICC registry name.</summary>
    public string RegistryName { get; internal set; } = "http://www.color.org";
    /// <summary>Gets a defensive copy of the embedded ICC profile bytes.</summary>
    public ReadOnlyMemory<byte> Profile => _profile.ToArray();
    /// <summary>Gets the ICC profile colour-component count.</summary>
    public int Components { get; internal set; }

    internal byte[] GetProfileBytes() => _profile;
    internal void SetProfile(byte[] profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Length < 128)
            throw new ArgumentException("An ICC profile must contain at least the 128-byte ICC header.", nameof(profile));
        if (profile[36] != (byte)'a' || profile[37] != (byte)'c' || profile[38] != (byte)'s' || profile[39] != (byte)'p')
            throw new ArgumentException("The supplied profile does not contain the ICC 'acsp' signature.", nameof(profile));

        string colourSpace = System.Text.Encoding.ASCII.GetString(profile, 16, 4);
        Components = colourSpace switch
        {
            "GRAY" => 1,
            "RGB " => 3,
            "CMYK" => 4,
            _ => throw new ArgumentException($"ICC colour space '{colourSpace}' is not supported; use GRAY, RGB, or CMYK.", nameof(profile))
        };
        _profile = profile.ToArray();
    }
}
