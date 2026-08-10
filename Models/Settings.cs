namespace Chat.Models;

/// <summary>
/// All globally accessible settings. Each public static readonly Setting&lt;T&gt; field is
/// auto-registered by <see cref="SettingsManager"/> (its key is the field name). Define
/// settings here only — behaviour lives in <see cref="SettingsManager"/>.
/// </summary>
internal static class Settings
{
    /// <summary>Master logging on/off.</summary>
    public static readonly Setting<bool> EnableLogging = new(true);

    /// <summary>UI theme: "Dark" or "Light".</summary>
    public static readonly Setting<string> Theme = new("Dark");

    /// <summary>SecretStorageMode: "Type0" (Plaintext), "Type1" (Password Encrypted), or "DPAPI" (Windows Protected).</summary>
    public static readonly Setting<string> SecretStorageMode = new("DPAPI");

    /// <summary>The file path to load/save the secret key from. If empty, the DefaultSecret below is used.</summary>
    public static readonly Setting<string> SecretFilePath = new("");

    /// <summary>The Base64 encoded secret key (saved directly in settings). It is encrypted based on SecretStorageMode.</summary>
    public static readonly Setting<string> DefaultSecret = new("");
}
