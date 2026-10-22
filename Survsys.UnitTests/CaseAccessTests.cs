using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Which route patterns are about one case, and so are held to the caller's
/// jurisdiction.
/// </summary>
public class CaseAccessTests
{
    [Theory]
    [InlineData("/api/person-conditions/{id:int}", "/api/person-conditions/{id:int}")]
    [InlineData("/api/person-conditions/{id:int}/labs", "/api/person-conditions/{id:int}")]
    [InlineData("/api/cases/{personConditionId:int}/treatments", "/api/cases/{personConditionId:int}")]
    [InlineData("/api/cases/forms/{assignmentId:int}/answers", "/api/cases/forms/{assignmentId:int}")]
    [InlineData("/api/cases/encounters/{id:int}", "/api/cases/encounters/{id:int}")]
    [InlineData("/api/cases/vaccines/{id:int}", "/api/cases/vaccines/{id:int}")]
    [InlineData("/api/notification-queue/{personConditionId:int}/retract", "/api/notification-queue/{personConditionId:int}")]
    [InlineData("/api/labs/{id:int}", "/api/labs/{id:int}")]
    [InlineData("/api/lab-test-results/{id:int}", "/api/lab-test-results/{id:int}")]
    [InlineData("/api/exposures/{id:int}/contact-events", "/api/exposures/{id:int}")]
    [InlineData("/api/exposure-places/{id:int}", "/api/exposure-places/{id:int}")]
    [InlineData("/api/notes/{id:int}/strikethrough", "/api/notes/{id:int}")]
    [InlineData("/api/attachments/{id:int}/content", "/api/attachments/{id:int}")]
    [InlineData("/api/tasks/{id:int}/status", "/api/tasks/{id:int}")]
    [InlineData("/API/Labs/{id:int}", "/api/labs/{id:int}")]
    public void CaseRouteOfACaseRouteIsTheRouteItStartsWith(string pattern, string expected)
    {
        Assert.Equal(expected, CaseAccess.CaseRouteOf(pattern));
    }

    [Theory]
    [InlineData("/api/person-conditions")]
    [InlineData("/api/cases")]
    [InlineData("/api/persons/{id:int}")]
    [InlineData("/api/notification-queue/process")]
    [InlineData("/api/outbreaks/{id:int}/attachments")]
    [InlineData("/api/labs/{id:int}x")]
    public void CaseRouteOfARouteAboutNoOneCaseIsNull(string pattern)
    {
        Assert.Null(CaseAccess.CaseRouteOf(pattern));
    }
}
