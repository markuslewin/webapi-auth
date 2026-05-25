var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
  .AddPostgres("postgres")
  .WithPgWeb(pgWeb => pgWeb.WithHostPort(5050));
var postgresdb = postgres
  .AddDatabase("postgresdb");

var exampleProject = builder.AddProject<Projects.webapi_auth>("apiservice")
  .WithReference(postgresdb);

builder.Build().Run();