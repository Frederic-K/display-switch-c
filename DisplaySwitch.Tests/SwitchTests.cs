// Règles de sécurité testées avec de faux écrans : aucun appel à Windows.

using DisplaySwitch;

namespace DisplaySwitch.Tests;

public class SwitchTests
{
    const string Big = "MSI MAG342CQR";
    const string Small = "MAG321CURV";

    // Écran fictif : `source` est l'index du mode source, null si l'écran est inactif.
    static Display FakeDisplay(string name, int pathIndex, bool primary, uint? source) => new(
        name,
        $@"\\?\DISPLAY#{pathIndex}",
        source is not null,
        primary,
        source is null ? null : (3840u, 2160u),
        source,
        pathIndex);

    static Settings Settings(string primary, string secondary) => new(primary, secondary);

    static List<Display> Extended() =>
    [
        FakeDisplay(Big, 0, true, 0),
        FakeDisplay(Small, 3, false, 1),
    ];

    [Fact]
    public void DisableKeepsOnlyThePrimaryPath()
    {
        Assert.Equal([0], Switch.PlanDisable(Extended(), Settings(Big, Small)));
    }

    [Fact]
    public void DisableReportsAlreadyDisabledSecondary()
    {
        List<Display> displays = [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, false, null)];
        Assert.Null(Switch.PlanDisable(displays, Settings(Big, Small)));
    }

    [Fact]
    public void DisableRefusesWhenWindowsPrimaryChanged()
    {
        List<Display> displays = [FakeDisplay(Big, 0, false, 0), FakeDisplay(Small, 3, true, 1)];
        var error = Assert.Throws<InvalidOperationException>(() => Switch.PlanDisable(displays, Settings(Big, Small)));
        Assert.Contains("n'est pas principal", error.Message);
    }

    [Fact]
    public void DisableRefusesInactivePrimary()
    {
        List<Display> displays = [FakeDisplay(Big, 0, false, null), FakeDisplay(Small, 3, true, 1)];
        Assert.Throws<InvalidOperationException>(() => Switch.PlanDisable(displays, Settings(Big, Small)));
    }

    [Fact]
    public void DisableRefusesCloneMode()
    {
        List<Display> displays = [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, true, 0)];
        var error = Assert.Throws<InvalidOperationException>(() => Switch.PlanDisable(displays, Settings(Big, Small)));
        Assert.Contains("duplication", error.Message);
    }

    [Fact]
    public void DisableRefusesMissingOrAmbiguousMonitor()
    {
        var missing = Assert.Throws<InvalidOperationException>(
            () => Switch.PlanDisable(Extended(), Settings(Big, "Autre")));
        Assert.Contains("introuvable", missing.Message);

        List<Display> twins = [.. Extended(), FakeDisplay(Small, 5, false, 2)];
        var ambiguous = Assert.Throws<InvalidOperationException>(
            () => Switch.PlanDisable(twins, Settings(Big, Small)));
        Assert.Contains("Plusieurs", ambiguous.Message);
    }

    [Fact]
    public void DisableKeepsOtherActiveScreens()
    {
        List<Display> three = [.. Extended(), FakeDisplay("Troisième", 7, false, 2)];
        Assert.Equal([0, 7], Switch.PlanDisable(three, Settings(Big, Small)));
    }

    [Fact]
    public void VerifyDisabledNeedsInactiveSecondary()
    {
        Assert.Throws<InvalidOperationException>(() => Switch.VerifyDisabled(Extended(), Settings(Big, Small)));

        List<Display> disabled = [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, false, null)];
        Switch.VerifyDisabled(disabled, Settings(Big, Small));
    }

    [Fact]
    public void VerifyEnabledRejectsCloneAndInactiveSecondary()
    {
        Switch.VerifyEnabled(Extended(), Settings(Big, Small));

        List<Display> clone = [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, true, 0)];
        Assert.Throws<InvalidOperationException>(() => Switch.VerifyEnabled(clone, Settings(Big, Small)));

        List<Display> inactive = [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, false, null)];
        Assert.Throws<InvalidOperationException>(() => Switch.VerifyEnabled(inactive, Settings(Big, Small)));
    }

    [Fact]
    public void DetectsRolesFromWindowsPrimary()
    {
        Assert.Equal(Settings(Big, Small), Switch.SettingsFromDisplays(Extended()));

        List<Display> swapped = [FakeDisplay(Big, 0, false, 0), FakeDisplay(Small, 3, true, 1)];
        Assert.Equal(Settings(Small, Big), Switch.SettingsFromDisplays(swapped));
    }

    [Fact]
    public void DetectionRefusesUnusableConfigurations()
    {
        List<List<Display>> unusable =
        [
            [FakeDisplay(Big, 0, true, 0)],
            [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, false, null)],
            [FakeDisplay(Big, 0, true, 0), FakeDisplay(Big, 3, false, 1)],
            [FakeDisplay(Big, 0, true, 0), FakeDisplay(Small, 3, true, 0)],
            [FakeDisplay(Big, 0, true, 0), FakeDisplay("", 3, false, 1)],
        ];

        foreach (List<Display> displays in unusable)
        {
            Assert.Throws<InvalidOperationException>(() => Switch.SettingsFromDisplays(displays));
        }
    }
}
