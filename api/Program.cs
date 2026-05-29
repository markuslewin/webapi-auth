using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();

builder.AddNpgsqlDbContext<BloggingContext>(connectionName: "postgresdb");
// builder.Services.AddDbContextPool<BloggingContext>(opt =>
//     opt.UseNpgsql(builder.Configuration.GetConnectionString("BloggingContext")));

builder
    .Services
    .AddIdentityApiEndpoints<User>()
    .AddEntityFrameworkStores<BloggingContext>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.MapOpenApi();
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseHttpsRedirection();

var confirmEmailEndpointName = "ConfirmEmail";

app
    .MapGet("/weatherforecast", async (BloggingContext ctx, HttpRequest httpRequest, ClaimsPrincipal user) =>
    {
        var blog = await ctx.Blogs.FindAsync(1);
        return blog.Url;
    })
    .WithName("GetWeatherForecast")
    .RequireAuthorization();

app.MapPost("/register",
    async Task<Results<Ok, ValidationProblem>> (
        RegisterRequest registration,
        UserManager<User> userManager,
        IUserStore<User> userStore,
        IEmailSender<User> emailSender,
        LinkGenerator linkGenerator,
        HttpContext httpContext) =>
    {
        var email = registration.Email;
        if (string.IsNullOrEmpty(email) || !new EmailAddressAttribute().IsValid(email))
        {
            var error = userManager.ErrorDescriber.InvalidEmail(email);
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                {error.Code, [error.Description]}
            });
        }

        var user = new User
        {
            MyName = registration.FullName
        };
        await userStore.SetUserNameAsync(user, email, CancellationToken.None);
        if (userStore is not IUserEmailStore<User> emailStore)
        {
            throw new Exception("Not an email store");
        }
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);
        var result = await userManager.CreateAsync(user, registration.Password);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(
                result
                    .Errors
                    .GroupBy(e => e.Code, e => e.Description)
                    .ToDictionary(g => g.Key, g => g.ToArray()));
        }

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var userId = await userManager.GetUserIdAsync(user);
        var confirmEmailUrl = linkGenerator.GetUriByName(httpContext, confirmEmailEndpointName, new RouteValueDictionary
        {
            ["userId"] = userId,
            ["code"] = encoded
        }) ?? throw new Exception($"Could not find endpoint named '{confirmEmailEndpointName}'.");
        await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
        Console.WriteLine($"Sent email with confirmation link '{confirmEmailUrl}'");

        return TypedResults.Ok();
    });

app
    .MapGet("/confirmEmail", async (string userId, string code, UserManager<User> userManager) =>
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        string? decoded = null;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return TypedResults.Unauthorized();
        }
        var result = await userManager.ConfirmEmailAsync(user, decoded);
        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        return Results.Ok();
    })
    .WithName(confirmEmailEndpointName);

app.MapPost("/login", async Task<Results<EmptyHttpResult, ProblemHttpResult>> (LoginRequest login, SignInManager<User> signInManager) =>
{
    var result = await signInManager.PasswordSignInAsync(login.Email, login.Password, isPersistent: true, lockoutOnFailure: true);
    if (!result.Succeeded)
    {
        return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
    }

    return TypedResults.Empty;
});

app.MapPost("/forgotPassword", async (
    ForgotPasswordRequest forgotPasswordRequest,
    UserManager<User> userManager,
    IEmailSender<User> emailSender) =>
    {
        // Don't leak data; return the same response for all paths
        var response = TypedResults.Ok();

        var user = await userManager.FindByEmailAsync(forgotPasswordRequest.Email);
        if (user is null)
        {
            return response;
        }

        var isEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
        if (!isEmailConfirmed)
        {
            return response;
        }

        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = HtmlEncoder.Default.Encode(WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code)));
        await emailSender.SendPasswordResetCodeAsync(user, forgotPasswordRequest.Email, encoded);
        Console.WriteLine($"Sent email with reset code '{encoded}'");
        
        return response;
    });

app.MapPost("/resetPassword", async Task<Results<Ok, ValidationProblem>> (
    ResetPasswordRequest resetPasswordRequest,
    UserManager<User> userManager,
    IEmailSender<User> emailSender) =>
    {
        // Don't leak data; return the same error for all error responses
        var error = userManager.ErrorDescriber.InvalidToken();
        var errorResponse = TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            { error.Code, [error.Description] }
        });

        var user = await userManager.FindByEmailAsync(resetPasswordRequest.Email);
        if (user is null)
        {
            return errorResponse;
        }

        var isEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
        if (!isEmailConfirmed)
        {
            return errorResponse;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetPasswordRequest.Code));
        }
        catch (FormatException)
        {
            return errorResponse;
        }

        var result = await userManager.ResetPasswordAsync(user, decoded, resetPasswordRequest.Password);
        if (!result.Succeeded)
        {
            return errorResponse;
        }

        return TypedResults.Ok();
    });

app
    .MapPost("/logout", async (SignInManager<User> signInManager) =>
    {
        await signInManager.SignOutAsync();
        return TypedResults.Ok();
    })
    .RequireAuthorization();

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

public class BloggingContext(DbContextOptions<BloggingContext> options) : IdentityDbContext<User>(options)
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

public class User : IdentityUser
{
    public string MyName { get; set; }
}

public class RegisterRequest
{
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class ForgotPasswordRequest
{
    public required string Email { get; set; }
}

public class ResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Code { get; set; }
    public required string Password { get; set; }
}
