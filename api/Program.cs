using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddNpgsqlDbContext<BloggingContext>(connectionName: "postgresdb");
// builder.Services.AddDbContextPool<BloggingContext>(opt =>
//     opt.UseNpgsql(builder.Configuration.GetConnectionString("BloggingContext")));

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", async (BloggingContext ctx) =>
{
    var blog = await ctx.Blogs.FindAsync(1);
    return blog.Url;

    // var forecast = Enumerable.Range(1, 5).Select(index =>
    //     new WeatherForecast
    //     (
    //         DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
    //         Random.Shared.Next(-20, 55),
    //         summaries[Random.Shared.Next(summaries.Length)]
    //     ))
    //     .ToArray();
    // return forecast;
})
.WithName("GetWeatherForecast");

// app
//     .MapGroup("/account")
//     .MapIdentityApi<User>();

using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<BloggingContext>();
    ctx.Database.EnsureCreated();
    ctx.Blogs.Add(new Blog
    {
        BlogId = 1,
        Url = "hey"
    });
    ctx.SaveChanges();
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class BloggingContext(DbContextOptions<BloggingContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}

class User
{
    public string? Name { get; set; }
}