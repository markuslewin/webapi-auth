#:sdk Aspire.AppHost.Sdk@13.3.3+a4615e7c6def6cba4703cdbd84009cd3da9a261b

var builder = DistributedApplication.CreateBuilder(args);

// The aspireify skill will wire up your projects here.

builder.Build().Run();