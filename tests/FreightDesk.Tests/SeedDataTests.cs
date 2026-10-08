using FreightDesk.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreightDesk.Tests;

public sealed class SeedDataTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SeedDataTests()
    {
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return provider;
    }

    private static async Task RunSeedAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedData.InitializeAsync(db, scope.ServiceProvider);
    }

    [Fact]
    public async Task Without_configuration_no_users_are_created()
    {
        using var provider = Build(new Dictionary<string, string?>());

        await RunSeedAsync(provider);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.Empty(users.Users.ToList());
        Assert.True(await roles.RoleExistsAsync("Admin"));
        Assert.True(await roles.RoleExistsAsync("Moderator"));
    }

    [Fact]
    public async Task Configured_admin_is_created_once_with_the_admin_role()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Seed:AdminEmail"] = "boss@example.com",
            ["Seed:AdminPassword"] = "Str0ng!Passw0rd"
        });

        await RunSeedAsync(provider);
        await RunSeedAsync(provider);

        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = Assert.Single(users.Users.ToList());
        Assert.Equal("boss@example.com", admin.Email);
        Assert.True(admin.EmailConfirmed);
        Assert.True(await users.IsInRoleAsync(admin, "Admin"));
    }

    [Fact]
    public async Task Lookup_data_is_seeded_only_when_tables_are_empty()
    {
        using var provider = Build(new Dictionary<string, string?>());

        await RunSeedAsync(provider);
        await RunSeedAsync(provider);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, db.Destinations.Count());
        Assert.Equal(3, db.SteamShipLines.Count());
        Assert.Equal(2, db.Shippings.Count());
    }
}
