namespace LyricsChatbox;

public static class ProductIdentity
{
    public const string Repository = "https://github.com/Teyocesu/LyricsChatbox";

    public static Version Version => typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);
    public static string VersionText => Version.ToString(3);
    public static string InformationalVersion =>
        typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? VersionText;
    public static string DisplayVersion => InformationalVersion.Split('+')[0];
    public static string UserAgent => $"LyricsChatbox/{VersionText} (+{Repository})";
}
