#:package Aspire.Hosting.PostgreSQL@13.3.5
#:sdk Aspire.AppHost.Sdk@13.3.3+a4615e7c6def6cba4703cdbd84009cd3da9a261b
#:project ./api/webapi-auth.csproj

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
  .AddPostgres("postgres")
  .WithPgWeb(pgWeb => pgWeb.WithHostPort(5050));
var postgresdb = postgres
  .AddDatabase("postgresdb");

var exampleProject = builder.AddProject<Projects.webapi_auth>("apiservice")
  .WithReference(postgresdb);

builder.Build().Run();