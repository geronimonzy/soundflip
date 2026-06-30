using System.Diagnostics;
using System.Reflection;

static class AppMetadata
{
    static Assembly Assembly => typeof(AppMetadata).Assembly;

    public static string ProductName =>
        GetAttribute<AssemblyProductAttribute>(attr => attr.Product) ?? "audsw";

    public static string Description =>
        GetAttribute<AssemblyDescriptionAttribute>(attr => attr.Description)
        ?? "Minimal Windows audio output switcher.";

    public static string Company => GetAttribute<AssemblyCompanyAttribute>(attr => attr.Company) ?? "";

    public static string Copyright => GetAttribute<AssemblyCopyrightAttribute>(attr => attr.Copyright) ?? "";

    public static string HomepageUrl => MetadataValue("HomepageUrl") ?? "";

    public static string SupportUrl => MetadataValue("SupportUrl") ?? "";

    public static string VersionText
    {
        get
        {
            string? informational = GetAttribute<AssemblyInformationalVersionAttribute>(attr => attr.InformationalVersion);
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+', 2)[0];

            var version = Assembly.GetName().Version;
            return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }
    }

    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    static string? MetadataValue(string key) =>
        Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => string.Equals(attr.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(attr.Value))
            ?.Value;

    static string? GetAttribute<T>(Func<T, string?> pick) where T : Attribute =>
        Assembly.GetCustomAttribute<T>() is T attribute && !string.IsNullOrWhiteSpace(pick(attribute))
            ? pick(attribute)
            : null;
}
