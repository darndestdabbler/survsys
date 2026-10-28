using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The frame every screen is drawn in, <c>MainLayout.razor</c>: the one-line
/// application bar and the side menu that holds the links to the screens.
/// </summary>
/// <remarks>
/// The walkthroughs press the links in the side menu as they always pressed
/// them in the bar, by their <c>nav-…</c> identifiers, so they prove each
/// link opens its screen. What they cannot prove is the shape of the frame:
/// that the bar no longer carries the links, that a group is offered only to
/// someone who may open one of its screens, and that the menu's button hides
/// and shows it, and that a group's heading wears the bar's colour. Those
/// are this file's.
///
/// The role and the user the group test adds are cleared by
/// <see cref="SurvsysHarness.Seed"/> on the next run, with everything else a
/// walkthrough fills.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class MainLayoutTests(SurvsysHarness harness) : SurvsysPageTest
{
    private const string RoleName = "Layout user administrator";

    private const string UserName = "layout.administrator";

    private const string DisplayName = "Layout Administrator";

    /// <summary>A password made up for this test's user, and no credential of anything else.</summary>
    private const string Password = "layout-test-password";

    private static readonly string[] AllGroups =
    [
        "nav-group-cases",
        "nav-group-intake",
        "nav-group-notifications",
        "nav-group-reference-data",
        "nav-group-administration",
    ];

    [Fact]
    public async Task TheApplicationBarHoldsNoScreenLink()
    {
        await Open(harness.WebBaseAddress);

        var ids = await Page.Locator(".mud-appbar [id^='nav-']")
            .EvaluateAllAsync<string[]>("elements => elements.map(element => element.id)");

        Assert.Equal(["nav-drawer-toggle", "nav-signed-in-as", "nav-sign-out"], ids);
        await Expect(Page.Locator("#nav-drawer #nav-people")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EachGroupShowsOnlyToAPersonWhoMayOpenOneOfItsLinks()
    {
        await Open(harness.WebBaseAddress);

        // The administrator holds every privilege, and so sees every group.
        foreach (var group in AllGroups)
        {
            await Expect(Page.Locator($"#{group}")).ToBeVisibleAsync();
        }

        // A role holding manage_users and nothing else, and a user holding it.
        await Page.Locator("#nav-roles").ClickAsync();
        await Page.Locator("#role-name").FillAsync(RoleName);
        await Box("manage_users").CheckAsync();
        await Page.Locator("#role-save").ClickAsync();
        await Expect(Page.Locator("#roles-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator($"#roles-table tr[data-role='{RoleName}']")).ToHaveCountAsync(1);

        await Page.Locator("#nav-users").ClickAsync();
        await Page.Locator("#user-user-name").FillAsync(UserName);
        await Page.Locator("#user-display-name").FillAsync(DisplayName);
        await Page.Locator("#user-password").FillAsync(Password);
        await Box(RoleName).CheckAsync();
        await Page.Locator("#user-save").ClickAsync();
        await Expect(Page.Locator("#users-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator($"#users-table tr[data-user='{UserName}']")).ToHaveCountAsync(1);

        // Signed in as that user, only the groups its links open are shown:
        // Reference data for the conditions anyone signed in may read, and
        // Administration for the users and roles.
        await Page.Locator("#nav-sign-out").ClickAsync();
        await Page.Locator("#sign-in-user-name").FillAsync(UserName);
        await Page.Locator("#sign-in-password").FillAsync(Password);
        await Page.Locator("#sign-in-submit").ClickAsync();
        await Page.WaitForURLAsync(url => !url.Contains("/sign-in", StringComparison.Ordinal));
        await Open(Page.Url);
        await Expect(Page.Locator("#nav-signed-in-as")).ToHaveTextAsync(DisplayName);

        await Expect(Page.Locator("#nav-group-reference-data")).ToBeVisibleAsync();
        await Expect(Page.Locator("#nav-group-administration")).ToBeVisibleAsync();
        await Expect(Page.Locator("#nav-group-cases")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#nav-group-intake")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#nav-group-notifications")).ToHaveCountAsync(0);

        await Expect(Page.Locator("#nav-conditions")).ToBeVisibleAsync();
        await Expect(Page.Locator("#nav-agencies")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheMenuButtonHidesAndShowsTheSideMenu()
    {
        await Open(harness.WebBaseAddress);
        var drawer = Page.Locator("#nav-drawer");

        await Expect(drawer).ToBeInViewportAsync();

        await Page.Locator("#nav-drawer-toggle").ClickAsync();
        await Expect(drawer).Not.ToBeInViewportAsync();

        await Page.Locator("#nav-drawer-toggle").ClickAsync();
        await Expect(drawer).ToBeInViewportAsync();
        await Page.Locator("#nav-people").ClickAsync();
        await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");
    }

    [Fact]
    public async Task AGroupHeadingTakesTheApplicationBarsColour()
    {
        await Open(harness.WebBaseAddress);

        var bar = await Background(Page.Locator(".mud-appbar"));
        var heading = await Background(Page.Locator("#nav-group-cases > .mud-nav-link"));
        var link = await Background(Page.Locator("#nav-people"));

        Assert.Equal(bar, heading);
        Assert.NotEqual(bar, link);
    }

    /// <summary>The computed background colour of <paramref name="element"/>.</summary>
    private static Task<string> Background(ILocator element) =>
        element.EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");

    /// <summary>The checkbox labelled <paramref name="label"/>, by the words on it.</summary>
    private ILocator Box(string label) =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true });
}
