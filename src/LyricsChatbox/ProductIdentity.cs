using System.Text.RegularExpressions;

namespace LyricsChatbox;

public static class ProductIdentity
{
    public const string Repository = "https://github.com/Teyocesu/LyricsChatbox";
    public const string AuthorGitHubUrl = "https://github.com/Teyocesu";
    public const string VrChatProfileUrl = "https://vrchat.com/home/user/usr_5560dde5-e00b-4784-a0ef-c6a6f36130d1";
    public const string DiscordUsername = "teyocesu";
    public static string ReleasesUrl => Repository + "/releases";
    public static string ReportIssueUrl => Repository + "/issues/new";
    public static string ThirdPartyNoticesUrl => Repository + "/blob/main/THIRD_PARTY_NOTICES.md";

    public static Version Version => typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);
    public static string VersionText => Version.ToString(3);
    public static bool IsPrerelease =>
        Regex.IsMatch(DisplayVersion, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)-[0-9A-Za-z.-]+$");
    public static string InformationalVersion =>
        typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? VersionText;
    public static string DisplayVersion => InformationalVersion.Split('+')[0];
    public static string UserAgent => $"LyricsChatbox/{VersionText} (+{Repository})";
}
