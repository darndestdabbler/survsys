using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A reportable eICR written to cases against the real database: a new
/// patient gets a new person and case with the eICR's values on it; a patient
/// on file with an open case on the condition has that case linked; a match
/// that is not confident waits for a reviewer, who settles it; a case ELR
/// opened for the patient is linked; a second version updates what the first
/// wrote and nothing a user edited; and a lab is added once by its key.
/// </summary>
/// <remarks>
/// Each test takes in the HL7 eICR R3.1.1 sample with a patient, address and
/// ids of its own, and an RR to it finding a test code reportable that is
/// mapped to Malaria, or for the ELR-opened case to the condition the ELR
/// fixture's vocabulary maps its result to.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class EcrCaseWriterTests(SqlServerFixture server) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly XNamespace V3 = "urn:hl7-org:v3";

    private static readonly CdaParser Parser = new();

    private static readonly CdaValidator Validator = new();

    /// <summary>The responsible agency the RR names, configured as this jurisdiction.</summary>
    private const string Agency = "2.16.840.1.113883.4.6^12341234";

    /// <summary>The code system of the RR's condition code and its mapping.</summary>
    private const string TestCodeSystem = "2.16.840.1.113883.9.9.9.ITEST-ECR-CASE";

    private const string EicrRoot = "2.16.840.1.113883.9.9.9.9.9";

    private const string Sample = "CDAR2_IG_PHCASERPT_R2_STU3.1.1_SAMPLE.xml";

    /// <summary>The sending lab of the ELR fixture message, oru-r01-chlamydia.hl7.</summary>
    private const string ElrSender = "TEST_LAB";

    /// <summary>The ELR fixture patient's zip.</summary>
    private const string ElrZip = "84101";

    private readonly SurvsysApi api = new(server.ConnectionString);
    private readonly string run = Guid.NewGuid().ToString("N");
    private int malaria;

    private string LastName => $"Ecr{run}";

    private string Street => $"1 {run} Street";

    public async Task InitializeAsync()
    {
        await using var context = server.NewContext();
        malaria = await context.Conditions.Where(condition => condition.Name == "Malaria")
            .Select(condition => condition.Id)
            .SingleAsync();
        context.EcrConditionMappings.Add(
            new EcrConditionMapping { CodeSystem = TestCodeSystem, Code = run, ConditionId = malaria });
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var context = server.NewContext())
        {
            await context.EcrDocuments.Where(document => document.DocumentId.Contains(run)).ExecuteDeleteAsync();
            await context.EcrConditionMappings.Where(mapping => mapping.Code == run).ExecuteDeleteAsync();

            var persons = context.Persons.Where(person => person.LastName == LastName).Select(person => person.Id);
            var cases = context.PersonConditions.Where(report => persons.Contains(report.PersonId))
                .Select(report => report.Id);
            var labs = context.Labs.Where(lab => persons.Contains(lab.PersonId)).Select(lab => lab.Id);
            var tests = context.LabTests.Where(test => labs.Contains(test.LabId)).Select(test => test.Id);
            await context.LabTestResults.Where(result => tests.Contains(result.LabTestId)).ExecuteDeleteAsync();
            await context.LabTests.Where(test => labs.Contains(test.LabId)).ExecuteDeleteAsync();
            await context.PersonConditionLabs.Where(link => labs.Contains(link.LabId)).ExecuteDeleteAsync();
            await context.Labs.Where(lab => persons.Contains(lab.PersonId)).ExecuteDeleteAsync();
            await context.Notes.Where(note => note.PersonConditionId != null && cases.Contains(note.PersonConditionId.Value))
                .ExecuteDeleteAsync();
            await context.Exposures
                .Where(exposure => exposure.PersonConditionId != null && cases.Contains(exposure.PersonConditionId.Value))
                .ExecuteDeleteAsync();
            await context.PersonTreatments.Where(row => cases.Contains(row.PersonConditionId)).ExecuteDeleteAsync();
            await context.Encounters.Where(row => cases.Contains(row.PersonConditionId)).ExecuteDeleteAsync();
            await context.PersonConditions.Where(report => persons.Contains(report.PersonId)).ExecuteDeleteAsync();
            await context.PersonAddresses.Where(address => persons.Contains(address.PersonId)).ExecuteDeleteAsync();
            await context.Persons.Where(person => person.LastName == LastName).ExecuteDeleteAsync();
        }

        api.Dispose();
    }

    [Fact]
    public async Task ANewPatientGetsANewPersonAndCaseWithTheEicrsValues()
    {
        var custodian = Parser.Read(Eicr(1)).Custodian!;
        var settings = Settings(labSenders: [custodian]);

        var eicr = await Take(Eicr(1), settings);
        await Take(Rr(1), settings);

        await using var context = server.NewContext();
        var stored = await context.EcrDocuments.SingleAsync(document => document.Id == eicr.Id);
        Assert.Equal(EcrDocument.LinkedCaseStatus, stored.CaseStatus);
        var link = await context.EcrCaseLinks.Include(found => found.Values)
            .SingleAsync(found => found.EcrDocumentId == eicr.Id);

        var report = await context.PersonConditions.SingleAsync(found => found.Id == link.PersonConditionId);
        var person = await context.Persons.SingleAsync(found => found.Id == report.PersonId);
        Assert.Equal(malaria, report.ConditionId);
        Assert.Equal(LastName, person.LastName);
        Assert.Equal("Eve", person.FirstName);
        Assert.Equal("Yes", report.Pregnant);
        Assert.False(string.IsNullOrEmpty(report.Occupation));
        Assert.NotNull(report.DiseaseOnsetDate);

        var address = await context.PersonAddresses.SingleAsync(found => found.PersonId == person.Id);
        Assert.Equal(Street, address.Street);
        Assert.Equal("09003", address.County);
        Assert.Equal("09110", address.CountyEquivalent);

        var document = Parser.Read(Eicr(1));
        Assert.Single(await context.Encounters.Where(row => row.PersonConditionId == report.Id).ToListAsync());
        Assert.Equal(
            DistinctProblems(document),
            await context.Notes.CountAsync(note => note.PersonConditionId == report.Id
                && note.NoteType == "clinical"));
        Assert.NotEmpty(await context.PersonTreatments.Where(row => row.PersonConditionId == report.Id).ToListAsync());
        Assert.NotEmpty(await context.Exposures.Where(row => row.PersonConditionId == report.Id).ToListAsync());
        Assert.Equal(document.Patient.Orders.Count, await context.Labs.CountAsync(lab => lab.PersonId == person.Id));

        Assert.Equal(
            ["encounters", "exposures", "labs", "notes", "person_addresses", "person_conditions", "person_treatments", "persons"],
            link.Values.Select(value => value.Target).Distinct().Order());
        Assert.Contains(link.Values, value => value.Target == "person_conditions" && value.Key == "pregnant"
            && value.Written == "Yes");
    }

    /// <summary>
    /// A new patient's address geocoded by the Census Geocoder, here a local
    /// stub, sent the one-line street address.
    /// </summary>
    [Fact]
    public async Task ANewPatientsAddressIsGeocoded()
    {
        await using var stub = await GeocodingStub.Start();
        var geocoder = new Geocoder(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(stub.Options()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Geocoder>.Instance);
        var settings = new EcrCaseSettings(new CountyPrograms(), [], geocoder);

        var eicr = await Take(Eicr(1), settings);
        await Take(Rr(1), settings);

        await using var context = server.NewContext();
        var link = await context.EcrCaseLinks.SingleAsync(found => found.EcrDocumentId == eicr.Id);
        var report = await context.PersonConditions.SingleAsync(found => found.Id == link.PersonConditionId);
        var address = await context.PersonAddresses.SingleAsync(found => found.PersonId == report.PersonId);
        Assert.Equal(
            (41.7637m, -72.6851m, PersonAddress.Census),
            (address.Latitude, address.Longitude, address.GeocodedBy));
        Assert.Equal($"{Street}, Hartford, CT, 06103", Assert.Single(stub.CensusAsked));
    }

    [Fact]
    public async Task AnRrArrivingFirstWaitsForItsEicrAndAnUnlistedSendersResultsAreNotWritten()
    {
        await Take(Rr(1), Settings());
        var eicr = await Take(Eicr(1), Settings());

        await using var context = server.NewContext();
        var link = await context.EcrCaseLinks.SingleAsync(found => found.EcrDocumentId == eicr.Id);
        var personId = await context.PersonConditions.Where(report => report.Id == link.PersonConditionId)
            .Select(report => report.PersonId)
            .SingleAsync();
        Assert.Equal(0, await context.Labs.CountAsync(lab => lab.PersonId == personId));
    }

    [Fact]
    public async Task ACaseElrOpenedForThePatientIsLinked()
    {
        var facilityId = await EnsureElrSender();
        var zipAdded = await EnsureElrZipAgency();
        await using var vocabulary = await IntakeVocabulary.SeedAccepting(server, ElrSender);

        try
        {
            await using (var context = server.NewContext())
            {
                await context.EcrConditionMappings.Where(mapping => mapping.Code == run)
                    .ExecuteUpdateAsync(set => set.SetProperty(mapping => mapping.ConditionId, vocabulary.ConditionId));

                var raw = (await File.ReadAllTextAsync(
                        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-chlamydia.hl7")))
                    .Replace("DOE^JOHN^A", $"{LastName}^JOHN^A", StringComparison.Ordinal)
                    .Replace("123 Main St", Street, StringComparison.Ordinal);
                Assert.Single(await ElrIntakeService.Receive(
                    new OruR01Parser(), context, raw, $"{run}.hl7", CancellationToken.None));
                await MessageProcessingService.Assign(
                    context, new OruR01Parser(), new PersonMatchThresholds(), new MessageProcessingOptions(),
                    DateTimeOffset.Now, CancellationToken.None);
            }

            int opened;
            await using (var context = server.NewContext())
            {
                var message = await context.StagedMessages.SingleAsync(staged => staged.Source == $"{run}.hl7");
                Assert.Equal((MessageReviewStatusRules.AssignedCode, null), (message.ReviewStatus, message.ExceptionDetail));
                opened = message.PersonConditionId!.Value;
            }

            var eicr = await Take(Eicr(1, elrPatient: true), Settings());
            await Take(Rr(1), Settings());

            await using (var context = server.NewContext())
            {
                var link = await context.EcrCaseLinks.SingleAsync(found => found.EcrDocumentId == eicr.Id);
                Assert.Equal(opened, link.PersonConditionId);
                var personId = await context.PersonConditions.Where(report => report.Id == opened)
                    .Select(report => report.PersonId)
                    .SingleAsync();
                Assert.Equal(1, await context.Persons.CountAsync(person => person.LastName == LastName));
                Assert.Equal(1, await context.PersonConditions.CountAsync(report => report.PersonId == personId));
                Assert.Equal("Yes", (await context.PersonConditions.SingleAsync(report => report.Id == opened)).Pregnant);
            }
        }
        finally
        {
            await using var context = server.NewContext();
            await context.StagedMessages.Where(staged => staged.Source == $"{run}.hl7").ExecuteDeleteAsync();
            if (zipAdded)
            {
                await context.ZipCodeAgencies.Where(row => row.ZipCode == ElrZip).ExecuteDeleteAsync();
            }

            await context.Facilities.Where(facility => facility.Id == facilityId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ALabAVersionLeftOutIsNotAddedAgainWhenALaterOneBringsItBack()
    {
        var listed = Settings(labSenders: [Parser.Read(Eicr(1)).Custodian!]);

        await Take(Eicr(1), listed);
        await Take(Rr(1), listed);
        await Take(Eicr(2), Settings());
        await Take(Rr(2), Settings());
        var third = await Take(Eicr(3), listed);
        await Take(Rr(3), listed);

        await using var context = server.NewContext();
        var link = await context.EcrCaseLinks.Include(found => found.Values)
            .SingleAsync(found => found.EcrDocumentId == third.Id);
        var personId = await context.PersonConditions.Where(report => report.Id == link.PersonConditionId)
            .Select(report => report.PersonId)
            .SingleAsync();
        var orders = Parser.Read(Eicr(1)).Patient.Orders.Count;
        Assert.Equal(orders, await context.Labs.CountAsync(lab => lab.PersonId == personId));
        Assert.Equal(orders, link.Values.Count(value => value.Target == "labs"));
    }

    [Fact]
    public async Task AnUncertainMatchWaitsForAReviewerWhoSettlesIt()
    {
        var personId = await SeedPerson("Someone");

        var eicr = await Take(Eicr(1), Settings());
        await Take(Rr(1), Settings());

        await using (var context = server.NewContext())
        {
            var held = await context.EcrDocuments.SingleAsync(document => document.Id == eicr.Id);
            Assert.Equal(EcrDocument.ReviewCaseStatus, held.CaseStatus);
            Assert.False(string.IsNullOrWhiteSpace(held.CaseReason));
            Assert.False(await context.EcrCaseLinks.AnyAsync(found => found.EcrDocumentId == eicr.Id));
        }

        var client = api.CreateClient();
        var matches = await client.GetFromJsonAsync<StagedMessagePersonMatchResponse>(
            $"/api/ecr-documents/{eicr.Id}/person-matches", Json);
        Assert.Contains(matches!.Candidates, candidate => candidate.Person.Id == personId
            && candidate.Verdict == PersonMatchCandidateResponse.Probable);

        using var settled = await client.PostAsJsonAsync(
            $"/api/ecr-documents/{eicr.Id}/case", new LinkEcrCaseRequest(personId), Json);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        var detail = await settled.Content.ReadFromJsonAsync<EcrDocumentDetailResponse>(Json);
        Assert.Equal(EcrDocument.LinkedCaseStatus, detail!.CaseStatus);
        var caseId = Assert.Single(detail.CaseIds!);

        await using (var context = server.NewContext())
        {
            var report = await context.PersonConditions.SingleAsync(found => found.Id == caseId);
            Assert.Equal(personId, report.PersonId);
            Assert.Equal(malaria, report.ConditionId);
        }

        using var again = await client.PostAsJsonAsync(
            $"/api/ecr-documents/{eicr.Id}/case", new LinkEcrCaseRequest(personId), Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task AHeldEicrSettledWithNoPersonOpensItsCaseUnderANewPerson()
    {
        var personId = await SeedPerson("Someone");

        var eicr = await Take(Eicr(1), Settings());
        await Take(Rr(1), Settings());

        var client = api.CreateClient();
        using var settled = await client.PostAsJsonAsync(
            $"/api/ecr-documents/{eicr.Id}/case", new LinkEcrCaseRequest(null), Json);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        var detail = await settled.Content.ReadFromJsonAsync<EcrDocumentDetailResponse>(Json);
        Assert.Equal(EcrDocument.LinkedCaseStatus, detail!.CaseStatus);
        var caseId = Assert.Single(detail.CaseIds!);

        await using var context = server.NewContext();
        var report = await context.PersonConditions.SingleAsync(found => found.Id == caseId);
        Assert.NotEqual(personId, report.PersonId);
        Assert.Equal(malaria, report.ConditionId);
    }

    [Fact]
    public async Task ASecondVersionUpdatesWhatTheFirstWroteAndNothingAUserEdited()
    {
        var first = await Take(Eicr(1), Settings());
        await Take(Rr(1), Settings());

        int caseId;
        await using (var context = server.NewContext())
        {
            caseId = (await context.EcrCaseLinks.SingleAsync(found => found.EcrDocumentId == first.Id))
                .PersonConditionId;
            var report = await context.PersonConditions.SingleAsync(found => found.Id == caseId);
            report.Occupation = "Teacher";
            await context.SaveChangesAsync();
        }

        var second = await Take(Eicr(2, notPregnant: true), Settings());
        await Take(Rr(2), Settings());

        await using (var context = server.NewContext())
        {
            var link = await context.EcrCaseLinks.Include(found => found.Values)
                .Include(found => found.EcrDocument)
                .SingleAsync(found => found.EcrDocumentId == second.Id);
            Assert.Equal(caseId, link.PersonConditionId);
            Assert.Equal(2, link.EcrDocument!.VersionNumber);

            var report = await context.PersonConditions.SingleAsync(found => found.Id == caseId);
            Assert.Equal("No", report.Pregnant);
            Assert.Equal("Teacher", report.Occupation);
            Assert.DoesNotContain(link.Values, value => value.Key == "occupation");
            Assert.Single(await context.Encounters.Where(row => row.PersonConditionId == caseId).ToListAsync());
            Assert.Equal(
                DistinctProblems(Parser.Read(Eicr(2))),
                await context.Notes.CountAsync(note => note.PersonConditionId == caseId));
            Assert.Equal(1, await context.PersonConditions.CountAsync(found => found.PersonId == report.PersonId));
        }
    }

    /// <summary>The problems a note is written for: one per coded problem and onset, the sample repeating some.</summary>
    private static int DistinctProblems(CdaDocument document) =>
        document.Problems
            .Where(problem => problem.Value is not null)
            .Select(problem => (problem.Value!.CodeSystem, problem.Value.Code, problem.Onset))
            .Distinct()
            .Count();

    private static EcrCaseSettings Settings(params string[] labSenders) => new(new CountyPrograms(), labSenders);

    /// <summary>Stores a document as the intake does and hands it to the case writer.</summary>
    private async Task<EcrDocument> Take(string raw, EcrCaseSettings settings)
    {
        await using var context = server.NewContext();
        var stored = await EcrIntakeService.Receive(
            Parser, Validator, context, raw, EcrDocument.TestFeed, null, [Agency], CancellationToken.None);
        Assert.NotNull(stored);
        await EcrIntakeService.WriteCases(
            Parser, context, new PersonMatchThresholds(), settings, stored, DateTimeOffset.Now, CancellationToken.None);

        return stored;
    }

    /// <summary>A person on file with the sample patient's last name, birth date, sex and address.</summary>
    private async Task<int> SeedPerson(string firstName)
    {
        var patient = Parser.Read(Eicr(1)).Patient;
        await using var context = server.NewContext();
        var person = new Person
        {
            LastName = LastName,
            FirstName = firstName,
            BirthDate = patient.Person.BirthDate,
            BirthGender = patient.Person.BirthGender,
        };
        person.Addresses.Add(new PersonAddress
        {
            Street = patient.Street, PostalCode = patient.Zip, Beginning = new DateOnly(2020, 1, 1),
        });
        context.Persons.Add(person);
        await context.SaveChangesAsync();

        return person.Id;
    }

    /// <summary>The ELR fixture's sending lab on file, answering its id where this test added it.</summary>
    private async Task<int?> EnsureElrSender()
    {
        await using var context = server.NewContext();
        if (await context.Facilities.AnyAsync(facility => facility.ShortName == ElrSender))
        {
            return null;
        }

        var facility = new Facility { Name = "eCR case test lab", ShortName = ElrSender, AgencyId = 1 };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        return facility.Id;
    }

    /// <summary>The ELR fixture patient's zip mapped to an agency, answering whether this test mapped it.</summary>
    private async Task<bool> EnsureElrZipAgency()
    {
        await using var context = server.NewContext();
        if (await context.ZipCodeAgencies.AnyAsync(row => row.ZipCode == ElrZip))
        {
            return false;
        }

        context.ZipCodeAgencies.Add(new ZipCodeAgency { ZipCode = ElrZip, AgencyId = 1 });
        await context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// The sample eICR as version <paramref name="version"/> of this test's
    /// set, its patient this test's and living at this test's Hartford address
    /// -- or, where <paramref name="elrPatient"/>, the ELR fixture's patient,
    /// moved to this test's street in Salt Lake City.
    /// </summary>
    private string Eicr(int version, bool notPregnant = false, bool elrPatient = false)
    {
        var document = XDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cda", Sample)));
        var root = document.Root!;

        root.Element(V3 + "id")!.SetAttributeValue("extension", $"{run}-v{version}");
        root.Element(V3 + "setId")!.SetAttributeValue("extension", run);
        root.Element(V3 + "versionNumber")!.SetAttributeValue("value", version);

        var patientRole = root.Element(V3 + "recordTarget")!.Element(V3 + "patientRole")!;
        foreach (var family in patientRole.Element(V3 + "patient")!.Elements(V3 + "name").Elements(V3 + "family"))
        {
            family.Value = LastName;
        }

        var address = patientRole.Element(V3 + "addr")!;
        address.Element(V3 + "streetAddressLine")!.Value = Street;
        address.Element(V3 + "city")!.Value = "Hartford";
        address.Element(V3 + "state")!.Value = "CT";
        address.Element(V3 + "postalCode")!.Value = "06103";
        address.Element(V3 + "county")!.Value = "09003";

        if (elrPatient)
        {
            var patient = patientRole.Element(V3 + "patient")!;
            foreach (var given in patient.Elements(V3 + "name").Select(name => name.Element(V3 + "given")!))
            {
                given.Value = "JOHN";
            }

            patient.Element(V3 + "administrativeGenderCode")!.SetAttributeValue("code", "M");
            patient.Element(V3 + "birthTime")!.SetAttributeValue("value", "19800115");
            address.Element(V3 + "city")!.Value = "Salt Lake City";
            address.Element(V3 + "state")!.Value = "UT";
            address.Element(V3 + "postalCode")!.Value = ElrZip;
            address.Element(V3 + "county")!.Remove();
        }

        if (notPregnant)
        {
            root.Descendants(V3 + "value")
                .First(value => (string?)value.Attribute("code") == "77386006")
                .Parent!
                .SetAttributeValue("negationInd", "true");
        }

        return document.ToString();
    }

    /// <summary>An RR to version <paramref name="version"/> finding this test's mapped code reportable.</summary>
    private string Rr(int version)
    {
        var agency = Agency.Split('^');

        return $"""
            <ClinicalDocument xmlns="urn:hl7-org:v3">
              <templateId root="2.16.840.1.113883.10.20.15.2.1.2" extension="2017-04-01" />
              <id root="{run}-rr{version}" />
              <component><structuredBody><component><section>
                <entry><act>
                  <templateId root="2.16.840.1.113883.10.20.15.2.3.10" />
                  <id root="{EicrRoot}" extension="{run}-v{version}" />
                </act></entry>
                <entry><observation>
                  <templateId root="2.16.840.1.113883.10.20.15.2.3.12" />
                  <value code="{run}" codeSystem="{TestCodeSystem}" displayName="Malaria (test)" />
                  <entryRelationship><organizer>
                    <templateId root="2.16.840.1.113883.10.20.15.2.3.13" />
                    <code code="RRVS7" />
                    <participant>
                      <templateId root="2.16.840.1.113883.10.20.15.2.4.2" />
                      <participantRole><id root="{agency[0]}" extension="{agency[1]}" /></participantRole>
                    </participant>
                    <component><observation>
                      <templateId root="2.16.840.1.113883.10.20.15.2.3.19" />
                      <value code="{EcrReportability.Reportable}" />
                    </observation></component>
                  </organizer></entryRelationship>
                </observation></entry>
              </section></component></structuredBody></component>
            </ClinicalDocument>
            """;
    }
}
