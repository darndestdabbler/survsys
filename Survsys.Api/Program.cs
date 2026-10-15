// The entry point the web SDK needs, and nothing beyond it. Endpoints, the
// DbContext registration and the pending-migration startup assertion arrive
// with the first vertical slice.
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
