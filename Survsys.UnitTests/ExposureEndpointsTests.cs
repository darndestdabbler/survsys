using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the exposure endpoints make of a stored exposure, its place, its
/// contacts and its sources, and what they refuse before asking the database
/// anything.
/// </summary>
/// <remarks>
/// No database and no host: that an exposure records and reads back whole,
/// that it is scoped to its report, and that a missing report is a 404 and a
/// missing person a 400 are the queries' to answer against a real SQL Server,
/// which no session-012 snapshot seeds a row for. The values here are made up
/// in the shape the legacy tables take: an exposure in the second week of
/// March at a named place, one contact and one source.
/// </remarks>
public class ExposureEndpointsTests
{
    private static Exposure Event() => new()
    {
        Id = 31,
        PersonConditionId = 14,
        ConditionId = 7,
        AgencyId = 2,
        ExposurePlaceId = 41,
        ExposureStart = new DateOnly(2026, 3, 8),
        ExposureEnd = new DateOnly(2026, 3, 10),
        Name = "Church supper",
        Description = "Potluck in the parish hall",
    };

    [Fact]
    public void ExposureToResponseCarriesTheExposuresListColumns()
    {
        var response = ExposureEndpoints.ExposureToResponse.Compile().Invoke(Event());

        Assert.Equal(
            new ExposureResponse(
                31,
                "Church supper",
                "Potluck in the parish hall",
                new DateOnly(2026, 3, 8),
                new DateOnly(2026, 3, 10),
                41),
            response);
    }

    /// <summary>
    /// An exposure with no name, no dates and no place: every one of those
    /// columns is nullable, and an exposure that has none comes back with
    /// nulls rather than with zeros or a default date.
    /// </summary>
    [Fact]
    public void ExposureToResponseCarriesABareExposure()
    {
        var response = ExposureEndpoints.ExposureToResponse.Compile().Invoke(new Exposure { Id = 32 });

        Assert.Equal(new ExposureResponse(32, null, null, null, null, null), response);
    }

    [Fact]
    public void PlaceToResponseCarriesThePlacesColumns()
    {
        var place = new ExposurePlace
        {
            Id = 41,
            ExposurePlaceTypeId = 3,
            Name = "St Mark's parish hall",
            Street = "12 Chapel St",
            City = "Hartford",
            Zip = "06103",
            State = "CT",
            Latitude = 41.763710m,
            Longitude = -72.685097m,
            Phone = "860-555-0100",
        };

        var response = ExposureEndpoints.PlaceToResponse.Compile().Invoke(place);

        Assert.Equal(
            new ExposurePlaceResponse(
                41, 3, "St Mark's parish hall", "12 Chapel St", "Hartford", "06103", "CT",
                41.763710m, -72.685097m, "860-555-0100"),
            response);
    }

    [Fact]
    public void ContactToResponseCarriesTheContactsColumns()
    {
        var contact = new ExposureContact
        {
            Id = 51,
            ExposureId = 31,
            PersonId = 15,
            ExposureStart = new DateOnly(2026, 3, 8),
            ExposureEnd = new DateOnly(2026, 3, 8),
            DispositionId = 4,
            ContactType = "household",
            DispositionDate = new DateOnly(2026, 3, 12),
        };

        var response = ExposureEndpoints.ContactToResponse.Compile().Invoke(contact);

        Assert.Equal(
            new ExposureContactResponse(
                51, 31, 15, new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8), 4, "household",
                new DateOnly(2026, 3, 12)),
            response);
    }

    /// <summary>
    /// A contact recorded with no disposition yet: the disposition, its date
    /// and the type are nullable and come back null.
    /// </summary>
    [Fact]
    public void ContactToResponseCarriesAContactWithNoDisposition()
    {
        var contact = new ExposureContact { Id = 52, ExposureId = 31, PersonId = 16 };

        var response = ExposureEndpoints.ContactToResponse.Compile().Invoke(contact);

        Assert.Equal(new ExposureContactResponse(52, 31, 16, null, null, null, null, null), response);
    }

    [Fact]
    public void SourceToResponseCarriesTheSourcesColumns()
    {
        var source = new ExposureSource
        {
            Id = 61,
            ExposureId = 31,
            PersonId = 17,
            PersonConditionId = 19,
            SourceName = "Index case",
            SourceType = "person",
        };

        var response = ExposureEndpoints.SourceToResponse.Compile().Invoke(source);

        Assert.Equal(
            new ExposureSourceResponse(61, 31, 17, 19, "Index case", "person"),
            response);
    }

    /// <summary>
    /// A source named rather than referenced: no person and no report, just a
    /// name and a kind.
    /// </summary>
    [Fact]
    public void SourceToResponseCarriesANamedSource()
    {
        var source = new ExposureSource
        {
            Id = 62,
            ExposureId = 31,
            SourceName = "Well water",
            SourceType = "environmental",
        };

        var response = ExposureEndpoints.SourceToResponse.Compile().Invoke(source);

        Assert.Equal(
            new ExposureSourceResponse(62, 31, null, null, "Well water", "environmental"),
            response);
    }

    /// <summary>
    /// The detail carries what it is handed: the exposure's own columns map,
    /// including the report, condition and agency the list leaves out, and the
    /// place, contacts and sources read by their own queries hang off it.
    /// </summary>
    [Fact]
    public void RespondDetailCarriesTheExposuresColumnsAndWhatHangsOffIt()
    {
        var place = new ExposurePlaceResponse(
            41, 3, "St Mark's parish hall", null, "Hartford", null, "CT", null, null, null);
        IReadOnlyList<ExposureContactResponse> contacts =
        [
            new ExposureContactResponse(51, 31, 15, null, null, 4, "household", null),
        ];
        IReadOnlyList<ExposureSourceResponse> sources =
        [
            new ExposureSourceResponse(61, 31, 17, 19, "Index case", "person"),
        ];

        var response = ExposureEndpoints.RespondDetail(Event(), place, contacts, sources);

        Assert.Equal(
            new ExposureDetailResponse(
                31, 14, 7, 2, "Church supper", "Potluck in the parish hall",
                new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10), place, contacts, sources),
            response);
    }

    /// <summary>
    /// An exposure with no place and nothing hung off it: the place is null
    /// and the lists are the empty ones it was handed.
    /// </summary>
    [Fact]
    public void RespondDetailCarriesAnExposureWithNoPlaceContactsOrSources()
    {
        var response = ExposureEndpoints.RespondDetail(new Exposure { Id = 32 }, null, [], []);

        Assert.Equal(32, response.Id);
        Assert.Null(response.Place);
        Assert.Empty(response.Contacts);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public void WhyNotAnExposureAcceptsAnExposureWithPlaceContactsAndSources()
    {
        var request = new CreateExposureRequest(
            Name: "Church supper",
            ExposureStart: new DateOnly(2026, 3, 8),
            ExposureEnd: new DateOnly(2026, 3, 10),
            Place: new ExposurePlaceRequest(Name: "St Mark's parish hall", State: "CT"),
            Contacts:
            [
                new ExposureContactRequest(
                    PersonId: 15,
                    ExposureStart: new DateOnly(2026, 3, 8),
                    ExposureEnd: new DateOnly(2026, 3, 9),
                    DispositionId: 4,
                    ContactType: "household",
                    DispositionDate: new DateOnly(2026, 3, 12)),
            ],
            Sources: [new ExposureSourceRequest(SourceName: "Well water", SourceType: "environmental")]);

        Assert.Null(ExposureEndpoints.WhyNotAnExposure(request));
    }

    /// <summary>
    /// A bare exposure — no name, no dates, no place, nothing hung off it — is
    /// accepted: an exposure can be recorded before anything is known of it.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureAcceptsABareExposure()
    {
        Assert.Null(ExposureEndpoints.WhyNotAnExposure(new CreateExposureRequest()));
    }

    /// <summary>
    /// An exposure that starts and ends on the same day is in order: the
    /// refusal is of an end before the start, not of an end on it.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureAcceptsAnExposureOfOneDay()
    {
        var request = new CreateExposureRequest(
            ExposureStart: new DateOnly(2026, 3, 8),
            ExposureEnd: new DateOnly(2026, 3, 8));

        Assert.Null(ExposureEndpoints.WhyNotAnExposure(request));
    }

    /// <summary>
    /// One end given without the other is not a range to check: an exposure
    /// may be known to have ended without its start being known.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureAcceptsAnExposureWithOneDateOnly()
    {
        Assert.Null(ExposureEndpoints.WhyNotAnExposure(
            new CreateExposureRequest(ExposureEnd: new DateOnly(2026, 3, 8))));
        Assert.Null(ExposureEndpoints.WhyNotAnExposure(
            new CreateExposureRequest(ExposureStart: new DateOnly(2026, 3, 8))));
    }

    [Fact]
    public void WhyNotAnExposureRefusesAnExposureThatEndsBeforeItStarts()
    {
        var request = new CreateExposureRequest(
            ExposureStart: new DateOnly(2026, 3, 10),
            ExposureEnd: new DateOnly(2026, 3, 8));

        var why = ExposureEndpoints.WhyNotAnExposure(request);

        Assert.NotNull(why);
        Assert.StartsWith("exposureEnd 2026-03-08 is before exposureStart 2026-03-10", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A contact's range is checked as the exposure's is, and the refusal
    /// names which contact so the caller can find it in the list it sent.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureRefusesAContactThatEndsBeforeItStarts()
    {
        var request = new CreateExposureRequest(
            Contacts:
            [
                new ExposureContactRequest(PersonId: 15),
                new ExposureContactRequest(
                    PersonId: 16,
                    ExposureStart: new DateOnly(2026, 3, 9),
                    ExposureEnd: new DateOnly(2026, 3, 8)),
            ]);

        var why = ExposureEndpoints.WhyNotAnExposure(request);

        Assert.NotNull(why);
        Assert.StartsWith("contacts[1].exposureEnd 2026-03-08 is before contacts[1].exposureStart 2026-03-09", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A contact with no person is refused before the database is asked: the
    /// column that holds the person is not nullable, and the refusal names the
    /// field rather than letting the insert fail.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureRefusesAContactWithNoPerson()
    {
        var request = new CreateExposureRequest(
            Contacts: [new ExposureContactRequest(ContactType: "household")]);

        var why = ExposureEndpoints.WhyNotAnExposure(request);

        Assert.NotNull(why);
        Assert.StartsWith("contacts[0].personId is missing", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A source with no person is not a fault: a source may be named rather
    /// than referenced.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureAcceptsASourceWithNoPerson()
    {
        var request = new CreateExposureRequest(
            Sources: [new ExposureSourceRequest(SourceName: "Well water")]);

        Assert.Null(ExposureEndpoints.WhyNotAnExposure(request));
    }

    /// <summary>
    /// An exposure correction with its dates in order, with one date, or with
    /// none is accepted: the edit refuses only an end before a start.
    /// </summary>
    [Fact]
    public void WhyNotAnExposureEditAcceptsDatesInOrderOrAbsent()
    {
        Assert.Null(ExposureEndpoints.WhyNotAnExposureEdit(new UpdateExposureRequest()));
        Assert.Null(ExposureEndpoints.WhyNotAnExposureEdit(
            new UpdateExposureRequest(ExposureStart: new DateOnly(2026, 3, 8))));
        Assert.Null(ExposureEndpoints.WhyNotAnExposureEdit(
            new UpdateExposureRequest(
                ExposureStart: new DateOnly(2026, 3, 8), ExposureEnd: new DateOnly(2026, 3, 10))));
    }

    [Fact]
    public void WhyNotAnExposureEditRefusesAnEndBeforeItsStart()
    {
        var why = ExposureEndpoints.WhyNotAnExposureEdit(
            new UpdateExposureRequest(
                ExposureStart: new DateOnly(2026, 3, 10), ExposureEnd: new DateOnly(2026, 3, 8)));

        Assert.NotNull(why);
        Assert.StartsWith(
            "exposureEnd 2026-03-08 is before exposureStart 2026-03-10", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A contact correction keeps the person it must name: an edit with no
    /// person is refused before the database is asked, the column that holds
    /// the person being not nullable.
    /// </summary>
    [Fact]
    public void WhyNotAContactEditRefusesAnEditThatDropsThePerson()
    {
        var why = ExposureEndpoints.WhyNotAContactEdit(
            new ExposureContactRequest(ContactType: "household"));

        Assert.NotNull(why);
        Assert.StartsWith("personId is missing", why, StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAContactEditRefusesAnEndBeforeItsStart()
    {
        var why = ExposureEndpoints.WhyNotAContactEdit(
            new ExposureContactRequest(
                PersonId: 15,
                ExposureStart: new DateOnly(2026, 3, 9),
                ExposureEnd: new DateOnly(2026, 3, 8)));

        Assert.NotNull(why);
        Assert.StartsWith(
            "exposureEnd 2026-03-08 is before exposureStart 2026-03-09", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A contact correction that names a person and sets its disposition, with
    /// its dates in order, is accepted before the database is asked: the
    /// disposition and its date are the correction a contact takes once the
    /// exposure is on file.
    /// </summary>
    [Fact]
    public void WhyNotAContactEditAcceptsAWellFormedCorrection()
    {
        var request = new ExposureContactRequest(
            PersonId: 15,
            ExposureStart: new DateOnly(2026, 3, 8),
            ExposureEnd: new DateOnly(2026, 3, 9),
            DispositionId: 4,
            ContactType: "household",
            DispositionDate: new DateOnly(2026, 3, 12));

        Assert.Null(ExposureEndpoints.WhyNotAContactEdit(request));
    }
}
