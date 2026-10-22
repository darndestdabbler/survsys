using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// What the ELR intake reads from the database for itself: the names of the
/// Connecticut towns on file, which the Connecticut profile checks a patient's
/// town against, so a town an administrator adds is accepted from the next
/// message on.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ElrIntakeServiceTests(SqlServerFixture server)
{
    [Fact]
    public async Task TownNamesHoldsTheSeededTownsAndOneAddedSinceInEitherCase()
    {
        var added = $"Testtown{Guid.NewGuid():N}"[..20];

        await using (var context = server.NewContext())
        {
            context.CtTowns.Add(new CtTown { Town = added, County = "09003", PlanningRegion = "09110" });
            await context.SaveChangesAsync();
        }

        try
        {
            await using var context = server.NewContext();

            var towns = await ElrIntakeService.TownNames(context, CancellationToken.None);

            Assert.Contains("North Stonington", towns);
            Assert.Contains(added.ToUpperInvariant(), towns);
        }
        finally
        {
            await using var context = server.NewContext();
            await context.CtTowns.Where(town => town.Town == added).ExecuteDeleteAsync();
        }
    }
}
