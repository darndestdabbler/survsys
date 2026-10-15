// The entry point the web SDK needs, and nothing beyond it. The Blazor
// Server host, MudBlazor and the HTTP client onto the API tier arrive with
// the first vertical slice.
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
