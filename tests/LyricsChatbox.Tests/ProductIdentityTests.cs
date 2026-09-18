namespace LyricsChatbox.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void UserAgentUsesTheApplicationAssemblyVersionAndStableRepositoryIdentity()
    {
        var assemblyVersion = typeof(App).Assembly.GetName().Version!;
        Assert.Equal(assemblyVersion.ToString(3), ProductIdentity.VersionText);
        Assert.Equal($"LyricsChatbox/{assemblyVersion.ToString(3)} (+https://github.com/Teyocesu/LyricsChatbox)", ProductIdentity.UserAgent);
        Assert.DoesNotContain("0.3", ProductIdentity.UserAgent);
        Assert.DoesNotContain("0.5.3", ProductIdentity.UserAgent);
    }
    [Fact]
    public void DisplayVersionExtendsNumericVersionAndFlagsPrerelease()
    {
        Assert.StartsWith(ProductIdentity.VersionText, ProductIdentity.DisplayVersion);
        Assert.Equal(ProductIdentity.DisplayVersion.Contains('-'), ProductIdentity.IsPrerelease);
    }
}
