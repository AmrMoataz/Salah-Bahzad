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

// ── Redis ────────────────────────────────────────────────────────────────────
// Backs the SignalR backplane for the QuizHub (NFR-SCAL-002), the connection↔attempt map that makes
// forfeit-on-disconnect survive horizontal scale (FR-PLAT-QZ-004), and the HybridCache L2. Fixed dev port
// (mirroring the Postgres/MinIO precedent) so wiring can reach it; injected as ConnectionStrings__redis.
var redis = builder.AddRedis("redis", port: 6379)
    .WithDataVolume();

// ── Object storage (MinIO, S3-compatible) ────────────────────────────────────
// MinIO emulates Cloudflare R2 locally so the IFileStorage → R2FileStorage path runs offline with the
// exact same code (only the endpoint/creds differ). Throwaway local credentials, matching the Postgres
// rationale above. Wired directly via AddContainer rather than the Aspire Community Toolkit MinIO
// integration, which is deprecated (MinIO OSS archived) and has no Aspire 9.3-compatible stable line.
// The S3 API is on :9000; the browser Console is on :9001 (for inspecting sb-dev-private).
const string minioRootUser = "minioadmin";
const string minioRootPassword = "minioadmin";

var minio = builder.AddContainer("minio", "minio/minio", "latest")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", minioRootUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioRootPassword)
    .WithHttpEndpoint(targetPort: 9000, name: "api")
    .WithHttpEndpoint(targetPort: 9001, name: "console")
    .WithVolume("minio-data", "/data");

// ── Backend API ────────────────────────────────────────────────────────────
// Propagate the AppHost's environment (Development / Staging) down to the API
// so it loads the correct appsettings.{Environment}.json.
// MinIO endpoint + root creds are injected as R2__* env vars — the same keys the API reads for real
// Cloudflare R2 in staging/prod (the dev bucket sb-dev-private is auto-created on first run).
var api = builder.AddProject<Projects.SalahBahazad_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("R2__Endpoint", minio.GetEndpoint("api"))
    .WithEnvironment("R2__AccessKeyId", minioRootUser)
    .WithEnvironment("R2__SecretAccessKey", minioRootPassword)
    .WithEnvironment("R2__BucketPrivate", "sb-dev-private")
    .WaitFor(minio);

// ── Admin Portal (Angular / Nx) ────────────────────────────────────────────
// Aspire injects services__api__http__0 (and https variant); proxy.conf.js
// reads that variable so the Angular dev-server proxies /api to the live API.
builder.AddNpmApp("admin-portal", "../../../frontend", "start")
    .WithReference(api)
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(targetPort: 4200)
    .ExcludeFromManifest();

builder.Build().Run();
