var builder = DistributedApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────────────
// Fixed local-dev password and host port so the Aspire-managed Postgres is reachable at a
// known connection string — lets EF Core migrations run from the CLI against the very
// database the app uses. This is a throwaway local DB, so the password lives in code.
var postgresPassword = builder.AddParameter("postgres-password", "postgres");

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithDataVolume()
    .WithPgAdmin();

// "DefaultConnection" matches the appsettings key — Aspire injects
// ConnectionStrings__DefaultConnection into the API automatically (service discovery).
var db = postgres.AddDatabase("DefaultConnection");

// ── Backend API ────────────────────────────────────────────────────────────
// Propagate the AppHost's environment (Development / Staging) down to the API
// so it loads the correct appsettings.{Environment}.json.
var api = builder.AddProject<Projects.SalahBahazad_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// ── Admin Portal (Angular / Nx) ────────────────────────────────────────────
// Aspire injects services__api__http__0 (and https variant); proxy.conf.js
// reads that variable so the Angular dev-server proxies /api to the live API.
builder.AddNpmApp("admin-portal", "../../../frontend", "start")
    .WithReference(api)
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(targetPort: 4200)
    .ExcludeFromManifest();

builder.Build().Run();
