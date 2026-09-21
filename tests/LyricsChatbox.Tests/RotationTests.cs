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
    public void FreshMigrationIncludesCanonicalStatusProfile()
    {
        var library = ProfileLibrary.Migrate(new());
        var status = Assert.Single(library.Items, profile => profile.Id == "status");

        Assert.Equal("Status", status.Name);
        Assert.Equal("Status / Time", status.Preset);
        Assert.True(status.BuiltIn);
        Assert.NotNull(status.Rotation);
        Assert.True(status.Rotation.IsValid);
        Assert.Empty(status.Rotation.Items!);
        Assert.Equal("Status / Time", status.Apply(new()).Preset);
    }

    [Fact]
    public void ExistingLibraryReceivesStatusWithoutChangingSelectionProfilesOrRotations()
    {
        var rotation = Rotation(true, 30, Message("a", "A"), Message("b", "B", false));
        var builtIn = new DisplayProfile("lyrics", "Lyrics customized", "Custom", "Adaptive", true, "Right",
            "{lyrics}\ncustom", Rotation: Rotation(false, 15));
        var user = new DisplayProfile("user", "User", "Custom", "Current + next", false, "Center",
            "{message}\n{lyrics}", "A", Rotation: rotation);
        var data = new LocalData(root);
        Assert.True(data.SaveProfiles(new(1, user.Id, [builtIn, user])));

        var restored = data.ReadProfiles(new());

        Assert.Equal(user.Id, restored.SelectedId);
        Assert.Equal(3, restored.Items.Count);
        Assert.Equal(JsonSerializer.Serialize(builtIn),
            JsonSerializer.Serialize(restored.Items.Single(profile => profile.Id == builtIn.Id)));
        Assert.Equal(JsonSerializer.Serialize(user),
            JsonSerializer.Serialize(restored.Items.Single(profile => profile.Id == user.Id)));
        Assert.Single(restored.Items, profile => profile.Id == "status");
    }

    [Fact]
    public void ExistingStatusIsPreservedAndRoundtripNeverDuplicatesIt()
    {
        var customized = new DisplayProfile("status", "My Status", "Custom", "Adaptive", true, "Right",
            "{message}\n{time}", "A", true, Rotation(true, 60, Message("a", "A"), Message("b", "B")));
        var data = new LocalData(root);
        Assert.True(data.SaveProfiles(new(1, customized.Id, [customized])));

        var first = data.ReadProfiles(new());
        Assert.Equal(JsonSerializer.Serialize(customized), JsonSerializer.Serialize(first.Selected));
        Assert.Single(first.Items, profile => profile.Id == "status");
        Assert.True(data.SaveProfiles(first));
        var second = data.ReadProfiles(new());
        Assert.Equal(JsonSerializer.Serialize(customized), JsonSerializer.Serialize(second.Selected));
        Assert.Single(second.Items, profile => profile.Id == "status");
    }

    [Fact]
    public void FullLibraryMissingStatusIsKeptWithoutEvictionOrFallback()
    {
        var items = Enumerable.Range(0, ProfileLibrary.Maximum)
            .Select(index => new DisplayProfile("profile-" + index, "Profile " + index,
                Rotation: Rotation(false, 15))).ToArray();
        var library = new ProfileLibrary(1, items[7].Id, items);
        var data = new LocalData(root);
        Assert.True(data.SaveProfiles(library));

        var restored = data.ReadProfiles(new(Message: "fallback must not win"));

        Assert.Equal(items[7].Id, restored.SelectedId);
        Assert.Equal(JsonSerializer.Serialize(items), JsonSerializer.Serialize(restored.Items));
        Assert.DoesNotContain(restored.Items, profile => profile.Id == "status");
    }

    [Fact]
    public void ExistingProfileUsingStatusIdIsNeverOverwritten()
    {
        var existing = new DisplayProfile("status", "User-owned status ID", "Lyrics Only", BuiltIn: false,
            Rotation: Rotation(false, 15));
        var data = new LocalData(root);
        Assert.True(data.SaveProfiles(new(1, existing.Id, [existing])));

        var restored = data.ReadProfiles(new());

        Assert.Single(restored.Items);
        Assert.Equal(JsonSerializer.Serialize(existing), JsonSerializer.Serialize(restored.Selected));
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

        Assert.Equal(3, restored.Items.Count);
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

    [Fact]
    public void UnrelatedDisplayEditsRenameAndDuplicatePreserveRotationConfiguration()
    {
        var rotation = Rotation(true, 30, Message("a", "A"), Message("b", "B", false), Message("c", "C"));
        var profile = new DisplayProfile("profile", "Profile", "Custom", "Adaptive", true, "Center",
            "{message}\n{lyrics}", "A", Rotation: rotation);
        var changed = profile.UpdatePresentation(new(Preset: "Status / Time", CustomTemplate: "{message}",
            Compact: false, CustomAlignment: "Right", Message: "must not replace mirror"), "Current + next");

        Assert.Same(rotation, changed.Rotation);
        Assert.Equal("A", changed.Message);
        Assert.Equal("Status / Time", changed.Preset);
        Assert.Equal("{message}", changed.CustomTemplate);
        Assert.Equal("Right", changed.Alignment);
        Assert.False(changed.Compact);
        Assert.Equal("Current + next", changed.ContextMode);

        var rotator = new MessageRotator();
        Assert.Equal("A", rotator.Current(profile.Id, rotation, "A", 0, true).Text);
        Assert.Equal("C", rotator.Current(profile.Id, rotation, "A", 30, true).Text);

        var library = new ProfileLibrary(1, profile.Id, [profile]);
        var renamed = library.Save(changed with { Name = "Renamed" })!;
        Assert.Same(rotation, renamed.Selected.Rotation);
        Assert.Equal("C", rotator.Current(renamed.SelectedId, rotation, "A", 31, true).Text);
        var duplicate = renamed.Duplicate(renamed.Selected)!;
        Assert.NotEqual(renamed.SelectedId, duplicate.SelectedId);
        Assert.Equal(rotation, duplicate.Selected.Rotation);
        Assert.Equal("A", rotator.Current(duplicate.SelectedId, duplicate.Selected.Rotation!, "A", 31, true).Text);
    }

    [Fact]
    public void ImmutableRotationMutationsEnforceBoundsAndLegacyMirror()
    {
        var rotation = Rotation(false, 15, Message("a", "A"), Message("b", "B"));
        rotation = rotation.SetEnabled(true)!;
        rotation = rotation.SetInterval(30)!;
        rotation = rotation.Edit("a", "A edited")!;
        rotation = rotation.SetItemEnabled("a", false)!;
        rotation = rotation.Move("b", -1)!;
        rotation = rotation.Add(Message("c", "C"))!;

        Assert.True(rotation.Enabled);
        Assert.Equal(30, rotation.IntervalSeconds);
        Assert.Equal(["b", "a", "c"], rotation.Items!.Select(item => item.Id));
        Assert.False(rotation.Items!.Single(item => item.Id == "a").Enabled);
        Assert.Equal("B", rotation.LegacyMessage);
        Assert.Null(rotation.Move("b", -1));
        Assert.Null(rotation.Move("c", 1));
        Assert.Null(rotation.Edit("missing", "text"));
        Assert.Null(rotation.SetItemEnabled("missing", false));
        Assert.Null(rotation.SetInterval(7));
        Assert.Null(rotation.Add(Message("blank", "  ")));

        rotation = rotation.Delete("b")!;
        Assert.Equal("C", rotation.LegacyMessage);
        rotation = rotation.SetItemEnabled("c", false)!;
        Assert.Equal("", rotation.LegacyMessage);
        Assert.Null(rotation.Delete("missing"));

        var full = Rotation(false, 15, Enumerable.Range(0, MessageRotation.Maximum)
            .Select(index => Message("item-" + index, "Text " + index)).ToArray());
        Assert.Null(full.Add(Message("extra", "Extra")));
    }

    [Fact]
    public void EditorAddInvalidSaveThenSuccessfulSaveTransitionsToEditing()
    {
        var rotation = Rotation(false, 15);
        var state = RotationEditorState.Adding;

        Assert.Null(rotation.Add(Message("a", "  ")));
        Assert.Equal(RotationEditorMode.Adding, state.Mode);
        Assert.False(state.CanRemove);

        rotation = rotation.Add(Message("a", "A"))!;
        state = RotationEditorState.Editing("a");
        Assert.Equal(RotationEditorMode.Editing, state.Mode);
        Assert.Equal("a", state.MessageId);
        Assert.True(state.CanRemove);
        Assert.Equal("A", rotation.Items!.Single().Text);

        rotation = rotation.Edit(state.MessageId!, "A edited")!;
        Assert.Single(rotation.Items!);
        Assert.Equal("A edited", rotation.Items![0].Text);
        Assert.Equal("a", state.MessageId);
    }

    [Fact]
    public void EditorDeleteLeavesNoneAndAddAfterDeleteSelectsOnlyTheNewItem()
    {
        var rotation = Rotation(false, 15, Message("a", "A"), Message("b", "B"));
        var state = RotationEditorState.Editing("b");

        state = state.ConfirmRemoval();
        Assert.True(state.ConfirmingRemoval);
        state = state.CancelRemoval();
        Assert.False(state.ConfirmingRemoval);
        Assert.Equal("b", state.MessageId);

        rotation = rotation.Delete("b")!;
        state = RotationEditorState.None;
        Assert.Equal(RotationEditorMode.None, state.Mode);
        Assert.Null(state.MessageId);
        Assert.Single(rotation.Items!);

        state = RotationEditorState.Adding;
        rotation = rotation.Add(Message("c", "C"))!;
        state = RotationEditorState.Editing("c");
        Assert.Equal("c", state.MessageId);
        Assert.True(state.CanRemove);
        Assert.Equal(["a", "c"], rotation.Items!.Select(item => item.Id));

        rotation = rotation.Delete("a")!.Delete("c")!;
        state = RotationEditorState.None;
        Assert.Empty(rotation.Items!);
        Assert.Equal(RotationEditorMode.None, state.Mode);
        Assert.True(RotationEditorState.Adding.ShowsEditor);
    }

    [Fact]
    public void EditorSelectionSurvivesReorderAndEnableChangesById()
    {
        var rotation = Rotation(false, 15, Message("a", "A"), Message("b", "B"));
        var state = RotationEditorState.Editing("b");

        rotation = rotation.Move("b", -1)!;
        Assert.Equal("b", state.MessageId);
        Assert.Equal("b", rotation.Items![0].Id);

        rotation = rotation.SetItemEnabled("b", false)!;
        Assert.Equal("b", state.MessageId);
        Assert.False(rotation.Items![0].Enabled);
        Assert.True(state.CanRemove);
    }

    [Fact]
    public void ActiveMessageFeedsCompositionAndSchedulerWithoutChangingTemplateOrPlaybackContent()
    {
        var rotation = Rotation(true, 5, Message("a", "A"), Message("b", "B"), Message("c", "C"));
        var rotator = new MessageRotator();
        const string template = "{message}\n♫ {title} — {artist}\n{lyrics}";
        var context = new LyricContext("previous", "current", "next");
        var at = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

        string Compose(double now, bool eligible, TrackIdentity track, LyricContext lyrics)
        {
            var message = rotator.Current("profile", rotation, "A", now, eligible).Text;
            return LyricContextComposer.ComposeProfile(lyrics, track, "Custom", template, message,
                "Current only", false, at, 10, "Left");
        }

        var changedTrack = CoreTests.Track with { Title = "Changed song", Album = "changed" };
        var changedLyrics = context with { Current = "changed lyric" };
        var first = Compose(0, true, CoreTests.Track, context);
        var metadataChanged = Compose(3, true, changedTrack, changedLyrics);
        var second = Compose(5, true, changedTrack, changedLyrics);
        Assert.Equal("A\n♫ Song — Artist\ncurrent", first);
        Assert.Equal("A\n♫ Changed song — Artist\nchanged lyric", metadataChanged);
        Assert.Equal("B\n♫ Changed song — Artist\nchanged lyric", second);
        Assert.Equal(template, "{message}\n♫ {title} — {artist}\n{lyrics}");

        var scheduler = new ChatboxScheduler();
        scheduler.Set(1, second, true);
        var packet = scheduler.Take(5)!.Value;
        Assert.Equal(ChatboxFormatter.Visible(ChatboxFormatter.Format(second)), ChatboxFormatter.Visible(packet.Text));
        Assert.Equal(1.05, ChatboxScheduler.IntervalSeconds);
    }

    [Fact]
    public void EligibilityPolicyFreezesForOffPauseManualAndMissingMessageThenResumesRemainder()
    {
        var rotation = Rotation(true, 10, Message("a", "A"), Message("b", "B"));
        var rotator = new MessageRotator();
        var manual = new ManualChat();
        bool Eligible(bool consumes = true, bool output = true, bool paused = false, double now = 0) =>
            MessageRotator.IsEligible(consumes, output, paused, manual.AutomaticAvailable(now));

        Assert.Equal("A", rotator.Current("p", rotation, "A", 0, Eligible(now: 0)).Text);
        Assert.Equal(6, rotator.Current("p", rotation, "A", 4, Eligible(output: false, now: 4)).RemainingSeconds, 6);
        Assert.Equal(6, rotator.Current("p", rotation, "A", 24, Eligible(paused: true, now: 24)).RemainingSeconds, 6);
        Assert.Equal(6, rotator.Current("p", rotation, "A", 44, Eligible(consumes: false, now: 44)).RemainingSeconds, 6);

        manual.PrepareDraft("manual");
        Assert.Equal(6, rotator.Current("p", rotation, "A", 64, Eligible(now: 64)).RemainingSeconds, 6);
        manual.Resume();
        Assert.Equal("A", rotator.Current("p", rotation, "A", 64, Eligible(now: 64)).Text);
        Assert.Equal("A", rotator.Current("p", rotation, "A", 69, Eligible(now: 69)).Text);
        Assert.Equal("B", rotator.Current("p", rotation, "A", 70, Eligible(now: 70)).Text);

        manual.Edit("manual", false, 72);
        manual.Send();
        manual.Sent(72);
        Assert.Equal(8, rotator.Current("p", rotation, "A", 72, Eligible(now: 72)).RemainingSeconds, 6);
        Assert.Equal("B", rotator.Current("p", rotation, "A", 80, Eligible(now: 80)).Text);
        Assert.Equal("A", rotator.Current("p", rotation, "A", 88, Eligible(now: 88)).Text);
    }

    [Fact]
    public void RuntimeAdvanceNeverChangesProfileSettingsOrPersistedLegacyMessage()
    {
        var profile = new DisplayProfile("profile", "Profile", "Custom", CustomTemplate: "{message}", Message: "stale",
            Rotation: Rotation(true, 5, Message("a", "A"), Message("b", "B"), Message("c", "C")));
        var data = new LocalData(root);
        var library = new ProfileLibrary(1, profile.Id, [profile]);
        Assert.True(data.SaveProfiles(library));
        var saved = data.ReadProfiles(new()).Selected;
        var settings = saved.Apply(new());
        Assert.True(data.SaveSettings(settings));

        var rotator = new MessageRotator();
        Assert.Equal("A", rotator.Current(saved.Id, saved.Rotation!, saved.Message, 0, true).Text);
        Assert.Equal("B", rotator.Current(saved.Id, saved.Rotation!, saved.Message, 5, true).Text);
        Assert.Equal("A", saved.Message);
        Assert.Equal("A", settings.Message);
        Assert.Equal("A", data.ReadProfiles(new()).Selected.Message);
        Assert.Equal("A", data.ReadSettings().Message);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "profiles.json")));
        Assert.Equal("A", json.RootElement.GetProperty("Items")[0].GetProperty("Message").GetString());
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
