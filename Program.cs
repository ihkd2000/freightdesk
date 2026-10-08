using FreightDesk.Data;
using FreightDesk.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "data source=freightdesk.db";

// Allow ASPNETCORE_URLS / --urls to override; default keeps the previous behaviour.
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5503");
}

builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection("Branding"));

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddHostedService<ArrivalEmailBGService>();
builder.Services.AddHostedService<PaymentReminderBGService>();

// Typed client: the factory manages the HttpClient lifetime. MailGunService reads its settings from IConfiguration.
builder.Services.AddHttpClient<IMailSender, MailGunService>(client =>
{
    client.BaseAddress = new Uri("https://api.mailgun.net/v3/");
});

builder.Services.AddHttpContextAccessor();

// Every endpoint requires a signed-in user; role checks are added per page.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await SeedData.InitializeAsync(context, scope.ServiceProvider);
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
