using MudBlazor.Services;
using Survsys.Web;
using Survsys.Web.Components;

// The application tier: MudBlazor screens over what the API tier returned.
// It takes an API address and never a connection string -- it cannot reach
// the database, and ArchitectureTests holds it to that.
const string apiBaseAddressKey = "Survsys:ApiBaseAddress";

var builder = WebApplication.CreateBuilder(args);

var apiBaseAddress = builder.Configuration[apiBaseAddressKey];

if (string.IsNullOrWhiteSpace(apiBaseAddress))
{
    throw new InvalidOperationException(
        $"No API address is configured at '{apiBaseAddressKey}'. This tier shows only what "
        + "the API tier returns and cannot start without knowing where that tier is: set "
        + $"{apiBaseAddressKey.Replace(':', '_')} for this environment, or supply it in "
        + "appsettings.");
}

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// The trailing slash matters: a base address without one would drop the last
// path segment when a relative request is resolved against it, so an API
// hosted under a path prefix would be asked the wrong URL.
var api = new Uri(apiBaseAddress.TrimEnd('/') + '/');

// One typed client per resource this tier reads or writes, each against the
// one API address. A screen takes a client and never an HttpClient: what a
// failure says is the client's to decide, and a screen's to show.
builder.Services.AddHttpClient<ConditionsClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<PersonsClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<AgenciesClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<PersonConditionsClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<ClinicalClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<InvestigationClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<AdministrativeClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<LabsClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<ExposuresClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<NotesClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<AttachmentsClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<TasksClient>(http => http.BaseAddress = api);
builder.Services.AddHttpClient<WorkQueueClient>(http => http.BaseAddress = api);

var app = builder.Build();

// Required by the component endpoints below, which refuse to be mapped
// without it.
app.UseAntiforgery();
app.MapStaticAssets();

// What the walkthrough harness waits on before its first click. A tier
// answers here only once startup has finished, so a 200 means this tier is
// serving -- which is the whole of what the harness needs to know and the
// whole of what this endpoint says.
app.MapGet("/health/ready", () => Results.Ok("ready"));

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
