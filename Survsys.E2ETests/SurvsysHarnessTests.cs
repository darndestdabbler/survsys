using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The harness itself, proved end to end.
/// </summary>
/// <remarks>
/// <strong>These are not walkthrough tests.</strong> A walkthrough test is
/// named after a document under <c>docs/walkthroughs/</c> and performs that
/// document's written steps in their written order;
/// <see cref="ReviewTheConditionsCatalogueTests"/> is the first of those, and
/// it is where the screen's wording is held to a document. What these tests
/// prove is the thing it stands on: two tiers running, a seeded database
/// behind them, a browser driving the screen, and video recording on.
///
/// Every assertion about the screen goes through Playwright's web-first
/// assertions, which retry until the element is there. That is the repository's
/// answer to render-wait flakiness, and the reason it declined a prerelease
/// package offering the same thing.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SurvsysHarnessTests(SurvsysHarness harness) : SurvsysPageTest
{
    [Fact]
    public async Task BothTiersAnswerReadiness()
    {
        using var asking = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        foreach (var tier in new[] { harness.ApiBaseAddress, harness.WebBaseAddress })
        {
            using var answer = await asking.GetAsync(new Uri(new Uri(tier), SurvsysHarness.ReadinessPath));

            Assert.True(
                answer.IsSuccessStatusCode,
                $"{tier}{SurvsysHarness.ReadinessPath} answered {(int)answer.StatusCode}. The "
                + "harness waits on this endpoint before the first click of every walkthrough, "
                + "so a tier that stops answering it stops the whole suite.");
        }
    }

    [Fact]
    public async Task TheConditionsScreenShowsTheSeededConditions()
    {
        await Page.GotoAsync(harness.WebBaseAddress + "conditions");

        await Expect(Page.Locator("#conditions-heading")).ToHaveTextAsync("Conditions");
        await Expect(Page.Locator("#conditions-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#conditions-table")).ToBeVisibleAsync();

        // The seven fixture rows, in the order the endpoint promises and the
        // screen therefore shows: by name. The fixture inserts them in
        // another order, so this is an assertion about the chain and not
        // about the fixture.
        await Expect(Page.Locator("#conditions-table tbody tr"))
            .ToHaveCountAsync(SurvsysHarness.SeededConditionsByName.Length);
        await Expect(Page.Locator("#conditions-table [data-condition-name]"))
            .ToHaveTextAsync(SurvsysHarness.SeededConditionsByName);
    }

    /// <summary>
    /// A browser that has not signed in is sent to the Sign in page rather
    /// than shown a screen: what every walkthrough's Before you start tells
    /// its reader, and the reason every other test here opens signed in.
    /// </summary>
    [Fact]
    public async Task ABrowserNotSignedInIsSentToTheSignInPage()
    {
        await using var unsigned = await Browser.NewContextAsync(new() { RecordVideoDir = VideoDirectory });
        var page = await unsigned.NewPageAsync();

        await page.GotoAsync(harness.WebBaseAddress + "conditions");

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/sign-in"));
        await Expect(page.Locator("#sign-in-heading")).ToHaveTextAsync("Sign in");
        await Expect(page.Locator("#conditions-table")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ThePeopleScreenShowsTheSeededPeople()
    {
        await Open(harness.WebBaseAddress + "people");

        await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");

        // Every box empty lists everyone on file, which is what the screen's
        // own lead-in promises.
        await Page.Locator("#person-search-search").ClickAsync();

        await Expect(Page.Locator("#person-search-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#person-search-total")).ToHaveTextAsync(
            $"{SurvsysHarness.SeededPeopleByName.Length} person(s) found, "
            + $"showing {SurvsysHarness.SeededPeopleByName.Length}.");

        // The seven fixture rows, in the order the endpoint promises and the
        // screen therefore shows: last name then first name. The fixture
        // inserts them in another order, so this is an assertion about the
        // chain and not about the fixture.
        await Expect(Page.Locator("#person-search-results [data-person-name]"))
            .ToHaveTextAsync(SurvsysHarness.SeededPeopleByName);
    }

    /// <summary>
    /// Seeding leaves what the fixtures fill — the conditions, the people and
    /// the people's own details, and the one report nmi-log.sql seeds with
    /// its export history — and nothing a walkthrough fills: no other report,
    /// no share, no specimen, no test, no result, no susceptibility row, no
    /// exposure with its place, contacts and sources, no note, no task, no
    /// attachment with its bytes, no form with its assignments and answers,
    /// no vaccine record, no outbreak and no treatment. Run here over whatever the walkthroughs
    /// before this test left behind — a laboratory walkthrough leaves a
    /// specimen that names its person, and a contacts walkthrough leaves an
    /// exposure contact that names one too — so it proves the deletion order
    /// holds against a database the suite has already used, which is the
    /// database every second run starts from.
    /// </summary>
    [Fact]
    public void SeedingLeavesNothingAWalkthroughFills()
    {
        harness.Seed();

        // The deletion order runs from the attachment, note, task, exposure,
        // laboratory and form chains down to the report, then the outbreaks
        // and treatments, and only then to the people's details and the
        // people, so the tables a walkthrough fills are the ones before the
        // people's details.
        const string FirstFixtureTable = "parent_guardians";
        var filledByWalkthroughs = SurvsysHarness.TablesInDeletionOrder
            .TakeWhile(table => table != FirstFixtureTable);
        var leftOver = new List<string>();

        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        foreach (var table in filledByWalkthroughs)
        {
            // The export history nmi-log.sql seeds is the one report, and the
            // log rows against it, that seeding is meant to leave; anything
            // else in those two tables is a walkthrough's.
            using var count = connection.CreateCommand();
            count.CommandText = table is "person_conditions" or "nmi_log"
                ? $"SELECT COUNT(*) FROM {table} WHERE record_number <> @seeded"
                : $"SELECT COUNT(*) FROM {table}";
            count.Parameters.AddWithValue("@seeded", SurvsysHarness.SeededExportLog);

            if ((int)count.ExecuteScalar()! is var rows && rows > 0)
            {
                leftOver.Add($"{table} holds {rows}");
            }
        }

        Assert.True(
            leftOver.Count == 0,
            $"Seeding left rows behind: {string.Join("; ", leftOver)}. A walkthrough that "
            + "needs a report or a specimen creates it, so one still there after seeding is "
            + "one the next walkthrough's document did not describe.");
    }

    /// <summary>
    /// Seeding clears the staged-message queue and fills it with exactly the
    /// fixture's messages, by their source and in the queue's order, oldest
    /// first. Run here over whatever the walkthroughs before it left — a
    /// promotion walkthrough moves a message to the assigned state in place —
    /// so it proves the clearing takes a promoted message with it and the
    /// queue starts from the seed, which is the queue every second run begins
    /// with. The source is what is asserted, because identity assigns the ids
    /// and a source is the one stable thing a row carries.
    /// </summary>
    [Fact]
    public void SeedingReseedsTheStagedMessages()
    {
        harness.Seed();

        var sources = new List<string>();

        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT source FROM system_messages ORDER BY received_at";

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            sources.Add(reader.GetString(0));
        }

        Assert.Equal(SurvsysHarness.SeededStagedMessageSources, sources);
    }

    /// <summary>
    /// Seeding clears the export log and fills it with exactly the fixture's
    /// history: the two exports of the seeded case, the newer one queued with
    /// no message and the older one sent and accepted with its message, and
    /// one copy of the case they name rather than one per run. Run here over
    /// whatever the walkthroughs before it left, so it proves the clearing
    /// takes the last run's log and queue with it.
    /// </summary>
    [Fact]
    public void SeedingReseedsTheExportLog()
    {
        harness.Seed();

        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        using var reports = connection.CreateCommand();
        reports.CommandText = "SELECT COUNT(*) FROM person_conditions WHERE record_number = @seeded";
        reports.Parameters.AddWithValue("@seeded", SurvsysHarness.SeededExportLog);
        Assert.Equal(1, (int)reports.ExecuteScalar()!);

        using var queued = connection.CreateCommand();
        queued.CommandText = "SELECT COUNT(*) FROM nmi_export_queue";
        Assert.Equal(0, (int)queued.ExecuteScalar()!);

        var exports = new List<(bool Sent, bool HasMessage, string? Outcome)>();

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT record_number, sent_at, xml, outcome FROM nmi_log ORDER BY queue_time DESC";

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            Assert.Equal(SurvsysHarness.SeededExportLog, reader.GetString(0));
            exports.Add((!reader.IsDBNull(1), !reader.IsDBNull(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        Assert.Equal(SurvsysHarness.SeededExportLogRows, exports.Count);
        Assert.Equal((false, false, null), exports[0]);
        Assert.Equal((true, true, "accepted"), exports[1]);
    }

    /// <summary>
    /// The walkthrough contract's fourth clause: every end-to-end run records
    /// video. The recording is on for every test in the suite because the
    /// base class turns it on, and this is the assertion that says so.
    /// </summary>
    [Fact]
    public async Task EveryTestInThisSuiteRecordsVideo()
    {
        await Page.GotoAsync(harness.WebBaseAddress + "conditions");

        var video = Page.Video;

        Assert.NotNull(video);

        var recording = await video.PathAsync();

        Assert.Equal(SurvsysPageTest.VideoDirectory, Path.GetDirectoryName(recording));
        Assert.Equal(".webm", Path.GetExtension(recording));
    }
}

/// <summary>
/// The intake harness, proved as <see cref="SurvsysHarnessTests"/> proves the
/// main one. Not walkthrough tests either.
/// </summary>
[Collection(IntakeCollection.Name)]
public class IntakeHarnessTests(IntakeHarness harness)
{
    /// <summary>
    /// The intake harness runs over a database of its own, so its assignment
    /// pass never reaches the messages the main harness seeds, and its seed
    /// holds the sending facility and the master LOINC rows its messages are
    /// translated by.
    /// </summary>
    [Fact]
    public void ItsDatabaseIsItsOwnAndHoldsTheIntakeReferenceData()
    {
        var main = new SqlConnectionStringBuilder(SurvsysHarness.ConfiguredConnectionString()).InitialCatalog;
        var intake = new SqlConnectionStringBuilder(harness.ConnectionString).InitialCatalog;

        Assert.NotEqual(main, intake);
        Assert.Equal(main + "_intake", intake);
        Assert.True(Directory.Exists(harness.IntakeDirectory));

        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        using var facility = connection.CreateCommand();
        facility.CommandText = "SELECT COUNT(*) FROM facilities WHERE short_name = N'ARUP_LAB'";
        Assert.Equal(1, (int)facility.ExecuteScalar()!);

        var loincs = new List<string>();

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT loinc FROM vocab_master_loinc ORDER BY loinc";

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            loincs.Add(reader.GetString(0));
        }

        Assert.Equal(["22314-9", "5061-8", "6463-4"], loincs);
    }
}
