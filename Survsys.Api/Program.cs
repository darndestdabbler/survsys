using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;

// The API tier: the only module that reaches the database, and so the only
// one that starts with a connection string.
const string connectionStringName = "Survsys";

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString(connectionStringName);

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        $"No connection string named '{connectionStringName}' is configured. The API tier "
        + "is the only tier that reaches the database and cannot start without one: set "
        + $"ConnectionStrings:{connectionStringName} for this environment.");
}

builder.Services.AddDbContext<SurvsysDbContext>(options => options.UseSqlServer(connectionString));

// Every failure leaves as Problem Details (RFC 7807), carrying a correlation
// id the caller can quote back and nothing else: no exception text, no SQL,
// no server path and no connection detail. What went wrong is in this tier's
// log under the same id, where it is readable by someone entitled to read it.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] =
        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

builder.Services.AddConditions();

var app = builder.Build();

// Before the first request, not during it: a database behind this assembly's
// migrations is one loud startup failure rather than a 500 on whichever
// request happens to touch a column that is not there yet.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>().Database;
    MigrationGate.AssertNonePending(await database.GetPendingMigrationsAsync());
}

app.UseExceptionHandler();

// Not every failure arrives as an exception. A query-string value that will
// not parse into the parameter it is bound to never reaches an endpoint at
// all: the framework sets 400 and writes no body, which the exception handler
// above never sees. This turns those into Problem Details as well, so a
// caller reads one error shape from this tier and not two.
app.UseStatusCodePages();

app.MapConditions();

app.Run();

// What WebApplicationFactory needs of an entry point written as top-level
// statements: a Program type it can name. The integration suite names it.
public partial class Program;
