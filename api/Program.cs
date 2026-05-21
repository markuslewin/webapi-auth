using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument();

builder.AddNpgsqlDbContext<BloggingContext>(connectionName: "postgresdb");
// builder.Services.AddDbContextPool<BloggingContext>(opt =>
//     opt.UseNpgsql(builder.Configuration.GetConnectionString("BloggingContext")));

builder.Services.AddAuthorization();
builder
    .Services
    .AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<BloggingContext>();

// builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.MapOpenApi();
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseHttpsRedirection();

app.MapGet("/weatherforecast", async (BloggingContext ctx) =>
{
    var blog = await ctx.Blogs.FindAsync(1);
    return blog.Url;
})
.WithName("GetWeatherForecast")
.RequireAuthorization();

app
    .MapGroup("/account")
    // .MapIdentityApi<User>();
    .MapIdentityApi<IdentityUser>();

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

public class BloggingContext(DbContextOptions<BloggingContext> options) : IdentityDbContext<IdentityUser>(options)
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