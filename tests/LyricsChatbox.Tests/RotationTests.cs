using System.Text.Json;

namespace LyricsChatbox.Tests;

public sealed class RotationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LyricsChatbox.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void V062ProfileNormalizesWithoutChangingLegacyPresentation()
    {
        var profile = new LegacyProfile("legacy", "My display", "Custom", "Adaptive", true, "Center",
            "{message}\n{lyrics}", "Hello! 日本語", false);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "profiles.json"), JsonSerializer.Serialize(new LegacyLibrary(1, profile.Id, [profile])));

        var restored = new LocalData(root).ReadProfiles(new()).Selected;

        Assert.Equal(profile.Id, restored.Id);
        Assert.Equal(profile.Name, restored.Name);
        Assert.Equal(profile.Preset, restored.Preset);
        Assert.Equal(profile.ContextMode, restored.ContextMode);
        Assert.Equal(profile.Compact, restored.Compact);
        Assert.Equal(profile.Alignment, restored.Alignment);
        Assert.Equal(profile.CustomTemplate, restored.CustomTemplate);
        Assert.Equal(profile.Message, restored.Message);
        Assert.Equal(profile.BuiltIn, restored.BuiltIn);
        AssertRotation(restored.Rotation, false, 15, new RotatingMessage("legacy-message", profile.Message));
        Assert.DoesNotContain("Rotation", JsonSerializer.Serialize(new AppSettings(Message: profile.Message)));
    }

    [Fact]
    public void EmptyLegacyMessageNormalizesToNoItemsAndEmptyFallback()
    {
        var profile = new DisplayProfile("legacy", "Legacy").NormalizeRotation();
        Assert.NotNull(profile.Rotation);
        Assert.Empty(profile.Rotation.Items!);
        Assert.Equal("", new MessageRotator().Current(profile.Id, profile.Rotation, profile.Message, 0, true).Text);
    }

    [Fact]
    public void InvalidNestedRotationFallsBackOnlyForThatProfile()
    {
        var broken = new DisplayProfile("broken", "Broken", Message: "keep me",
            Rotation: new(IntervalSeconds: 7, Items: [new("bad", "ignored")]));
        var sibling = new DisplayProfile("sibling", "Sibling", Message: "other",
            Rotation: new(Enabled: true, Items: [new("other", "other")]));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "profiles.json"), JsonSerializer.Serialize(
            new ProfileLibrary(1, broken.Id, [broken, sibling])));

        var restored = new LocalData(root).ReadProfiles(new());

        Assert.Equal(2, restored.Items.Count);
        Assert.Equal("keep me", restored.Selected.Message);
        AssertRotation(restored.Selected.Rotation, false, 15, new RotatingMessage("legacy-message", "keep me"));
        var restoredSibling = restored.Items.Single(item => item.Id == sibling.Id);
        Assert.Equal(sibling with { Rotation = null }, restoredSibling with { Rotation = null });
        AssertRotation(restoredSibling.Rotation, true, 15, new RotatingMessage("other", "other"));
    }

    [Fact]
    public void NullProfileEntryFallsBackWithoutCrashingOrResettingSettings()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "profiles.json"), """{"Version":1,"SelectedId":"missing","Items":[null]}""");
        var legacy = new AppSettings(Message: "settings fallback", Compact: true);

        var restored = new LocalData(root).ReadProfiles(legacy);

        Assert.Equal(legacy, restored.Selected.Apply(legacy));
    }

    [Fact]
    public void ConfigurationSaveMaintainsLegacyMirrorButRuntimeAdvanceDoesNot()
    {
        var rotation = Rotation(true, 5, Message("a", "A"), Message("b", "B"), Message("c", "C"));
        var profile = new DisplayProfile("profile", "Profile", Message: "stale", Rotation: rotation);
        var data = new LocalData(root);
        Assert.True(data.SaveProfiles(new(1, profile.Id, [profile])));

        var saved = data.ReadProfiles(new()).Selected;
        Assert.Equal("A", saved.Message);
        var rotator = new MessageRotator();
        Assert.Equal("A", rotator.Current(saved.Id, saved.Rotation!, saved.Message, 0, true).Text);
        Assert.Equal("B", rotator.Current(saved.Id, saved.Rotation!, saved.Message, 5, true).Text);
        Assert.Equal("A", data.ReadProfiles(new()).Selected.Message);

        var json = File.ReadAllText(Path.Combine(root, "profiles.json"));
        var rollback = JsonSerializer.Deserialize<LegacyLibrary>(json)!;
        Assert.Equal(1, rollback.Version);
        Assert.Equal("A", rollback.Items.Single().Message);
        Assert.Contains("\"Rotation\"", json);

        var firstDisabled = profile with { Rotation = Rotation(false, 5, Message("a", "A", false), Message("b", "B")) };
        Assert.True(data.SaveProfiles(new(1, firstDisabled.Id, [firstDisabled])));
        Assert.Equal("B", data.ReadProfiles(new()).Selected.Message);

        var disabled = profile with { Rotation = Rotation(false, 5, Message("a", "A", false)) };
        Assert.True(data.SaveProfiles(new(1, disabled.Id, [disabled])));
        Assert.Equal("", data.ReadProfiles(new()).Selected.Message);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void SupportedIntervalsAreValid(int seconds) => Assert.True(new MessageRotation(IntervalSeconds: seconds, Items: []).IsValid);

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(121)]
    public void OtherIntervalsAreInvalid(int seconds) => Assert.False(new MessageRotation(IntervalSeconds: seconds, Items: []).IsValid);

    [Fact]
    public void RotationValidationEnforcesCountsIdsTextLinesAndControls()
    {
        var sixteen = Enumerable.Range(0, 16).Select(i => Message("item-" + i, "text")).ToArray();
        Assert.True(new MessageRotation(Items: sixteen).IsValid);
        Assert.False(new MessageRotation(Items: [.. sixteen, Message("extra", "text")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("same", "one"), Message("same", "two")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("", "text")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message(new string('a', 65), "text")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("not allowed", "text")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("id", new string('x', 513))]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("id", string.Join('\n', Enumerable.Repeat("x", 10)))]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("id", "x\0y")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("id", "x\ty")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("id", "\uD800")]).IsValid);
        Assert.False(new MessageRotation(Items: [Message("id", "  \n ")]).IsValid);
        Assert.True(new MessageRotation(Items: [Message("id", string.Join('\n', Enumerable.Repeat("日本語 🎵", 9)), false)]).IsValid);
        Assert.True(new MessageRotation(Items: []).IsValid);
    }

    [Fact]
    public void MaximumEscapedProfileLibraryFitsBoundedFile()
    {
        var text = new string('\uFFFF', 512);
        var profiles = Enumerable.Range(0, ProfileLibrary.Maximum).Select(profile => new DisplayProfile(
            "profile-" + profile, "Profile " + profile, "Custom", CustomTemplate: text, Message: "stale",
            Rotation: new(Items: Enumerable.Range(0, MessageRotation.Maximum)
                .Select(item => Message("item-" + item, text)).ToArray()))).ToArray();
        var data = new LocalData(root);

        Assert.True(data.SaveProfiles(new(1, profiles[0].Id, profiles)));
        var bytes = new FileInfo(Path.Combine(root, "profiles.json")).Length;
        Assert.InRange(bytes, 160 * 1024 + 1, 2 * 1024 * 1024);
        Assert.Equal(ProfileLibrary.Maximum, data.ReadProfiles(new()).Items.Count);

        var invalid = profiles[0] with { Rotation = new(Items: Enumerable.Range(0, MessageRotation.Maximum + 1)
            .Select(item => Message("item-" + item, "text")).ToArray()) };
        Assert.False(data.SaveProfiles(new(1, invalid.Id, [invalid])));

        File.WriteAllBytes(Path.Combine(root, "profiles.json"), new byte[2 * 1024 * 1024 + 1]);
        Assert.Equal("fallback", data.ReadProfiles(new(Message: "fallback")).Selected.Message);
    }

    [Fact]
    public void SequentialRotationVisitsEveryItemAndWraps()
    {
        var rotation = Rotation(true, 5, Message("a", "A"), Message("b", "B"), Message("c", "C"));
        var rotator = new MessageRotator();
        Assert.Equal("A", rotator.Current("p", rotation, "", 0, true).Text);
        Assert.Equal("B", rotator.Current("p", rotation, "", 5, true).Text);
        Assert.Equal("C", rotator.Current("p", rotation, "", 10, true).Text);
        Assert.Equal("A", rotator.Current("p", rotation, "", 15, true).Text);
    }

    [Fact]
    public void SequentialRotationSkipsDisabledItemsAndWraps()
    {
        var rotation = Rotation(true, 5, Message("a", "A"), Message("b", "B", false), Message("c", "C"));
        var rotator = new MessageRotator();

        AssertCurrent(rotator.Current("p", rotation, "fallback", 0, true), "a", "A", 5);
        AssertCurrent(rotator.Current("p", rotation, "fallback", 4, true), "a", "A", 1);
        AssertCurrent(rotator.Current("p", rotation, "fallback", 5, true), "c", "C", 5);
        AssertCurrent(rotator.Current("p", rotation, "fallback", 10, true), "a", "A", 5);
    }

    [Fact]
    public void ZeroOneAndDisabledRotationsRemainFixed()
    {
        var rotator = new MessageRotator();
        Assert.Equal("fallback", rotator.Current("empty", Rotation(true, 5), "fallback", 500, true).Text);
        Assert.Equal("A", rotator.Current("one", Rotation(true, 5, Message("a", "A")), "fallback", 0, true).Text);
        Assert.Equal("A", rotator.Current("one", Rotation(true, 5, Message("a", "A")), "fallback", 500, true).Text);
        Assert.Equal("A", rotator.Current("off", Rotation(false, 5, Message("a", "A"), Message("b", "B")), "fallback", 0, true).Text);
        Assert.Equal("A", rotator.Current("off", Rotation(false, 5, Message("a", "A"), Message("b", "B")), "fallback", 500, true).Text);
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("Output Off")]
    [InlineData("Output Pause")]
    [InlineData("no message token")]
    public void IneligibleCallerReasonsFreezeAndResumeRemainingTime(string _)
    {
        var rotation = Rotation(true, 10, Message("a", "A"), Message("b", "B"));
        var rotator = new MessageRotator();
        rotator.Current("p", rotation, "", 0, true);
        Assert.Equal(6, rotator.Current("p", rotation, "", 4, false).RemainingSeconds, 6);
        Assert.Equal(6, rotator.Current("p", rotation, "", 34, false).RemainingSeconds, 6);
        Assert.Equal(6, rotator.Current("p", rotation, "", 34, true).RemainingSeconds, 6);
        Assert.Equal("A", rotator.Current("p", rotation, "", 39, true).Text);
        Assert.Equal("B", rotator.Current("p", rotation, "", 40, true).Text);
    }

    [Fact]
    public void LongEligibleStallAdvancesOnlyOneItemAndRestartsInterval()
    {
        var rotation = Rotation(true, 5, Message("a", "A"), Message("b", "B"), Message("c", "C"));
        var rotator = new MessageRotator();
        rotator.Current("p", rotation, "", 0, true);

        AssertCurrent(rotator.Current("p", rotation, "", 300, true), "b", "B", 5);
        AssertCurrent(rotator.Current("p", rotation, "", 305, true), "c", "C", 5);
    }

    [Fact]
    public void ProfileSwitchFreezesAndResumesIndependentState()
    {
        var music = Rotation(true, 5, Message("a", "A"), Message("b", "B"));
        var afk = Rotation(true, 10, Message("x", "X"), Message("y", "Y"));
        var rotator = new MessageRotator();
        rotator.Current("music", music, "", 0, true);
        AssertCurrent(rotator.Current("music", music, "", 8, true), "b", "B", 5);

        AssertCurrent(rotator.Current("afk", afk, "", 8, true), "x", "X", 10);
        AssertCurrent(rotator.Current("afk", afk, "", 11, true), "x", "X", 7);
        AssertCurrent(rotator.Current("music", music, "", 20, true), "b", "B", 5);
        AssertCurrent(rotator.Current("afk", afk, "", 22, true), "y", "Y", 10);
    }

    [Fact]
    public void EnableDisableAndIntervalChangesFollowConfigurationRules()
    {
        var items = new[] { Message("a", "A"), Message("b", "B") };
        var rotator = new MessageRotator();
        rotator.Current("p", Rotation(false, 5, items), "", 0, true);
        AssertCurrent(rotator.Current("p", Rotation(true, 5, items), "", 100, true), "a", "A", 5);
        AssertCurrent(rotator.Current("p", Rotation(true, 5, items), "", 105, true), "b", "B", 5);
        AssertCurrent(rotator.Current("p", Rotation(true, 10, items), "", 106, true), "b", "B", 10);
        AssertCurrent(rotator.Current("p", Rotation(false, 10, items), "", 200, true), "a", "A", 10);
    }

    [Fact]
    public void ReorderAndEditKeepCurrentIdAndTimer()
    {
        var rotator = new MessageRotator();
        var initial = Rotation(true, 10, Message("a", "A"), Message("b", "B"), Message("c", "C"));
        rotator.Current("p", initial, "", 0, true);
        rotator.Current("p", initial, "", 10, true);

        var changed = Rotation(true, 10, Message("c", "C"), Message("b", "B edited"), Message("a", "A"), Message("d", "D"));
        AssertCurrent(rotator.Current("p", changed, "", 13, true), "b", "B edited", 7);
        AssertCurrent(rotator.Current("p", changed, "", 20, true), "a", "A", 10);
    }

    [Fact]
    public void DeleteDisableAndEmptySelectionChooseDeterministicFallbacks()
    {
        var rotator = new MessageRotator();
        var initial = Rotation(true, 5, Message("a", "A"), Message("b", "B"), Message("c", "C"));
        rotator.Current("p", initial, "legacy", 0, true);
        rotator.Current("p", initial, "legacy", 5, true);

        var deleted = Rotation(true, 5, Message("a", "A"), Message("c", "C"));
        AssertCurrent(rotator.Current("p", deleted, "legacy", 5, true), "c", "C", 5);
        var disabled = Rotation(true, 5, Message("a", "A"), Message("c", "C", false));
        AssertCurrent(rotator.Current("p", disabled, "legacy", 6, true), "a", "A", 5);
        var empty = Rotation(true, 5, Message("a", "A", false), Message("c", "C", false));
        AssertCurrent(rotator.Current("p", empty, "legacy", 7, true), null, "legacy", 5);
        AssertCurrent(rotator.Current("p", Rotation(true, 5, Message("c", "C")), "legacy", 100, true), "c", "C", 5);
    }

    private static RotatingMessage Message(string id, string text, bool enabled = true) => new(id, text, enabled);
    private static MessageRotation Rotation(bool enabled, int interval, params RotatingMessage[] items) =>
        new(Enabled: enabled, IntervalSeconds: interval, Items: items);
    private static void AssertCurrent(MessageRotationCurrent current, string? id, string text, double remaining)
    {
        Assert.Equal(id, current.ItemId);
        Assert.Equal(text, current.Text);
        Assert.Equal(remaining, current.RemainingSeconds, 6);
    }
    private static void AssertRotation(MessageRotation? rotation, bool enabled, int interval, params RotatingMessage[] items)
    {
        Assert.NotNull(rotation);
        Assert.Equal(enabled, rotation.Enabled);
        Assert.Equal(interval, rotation.IntervalSeconds);
        Assert.Equal(items, rotation.Items);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed record LegacyProfile(string Id, string Name, string Preset = "Lyrics Only", string ContextMode = "Current only",
        bool Compact = false, string Alignment = "Left", string CustomTemplate = "{lyrics}", string Message = "", bool BuiltIn = false);
    private sealed record LegacyLibrary(int Version, string SelectedId, IReadOnlyList<LegacyProfile> Items);
}
