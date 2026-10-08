using FreightDesk.Models;
using Microsoft.AspNetCore.Identity;

namespace FreightDesk.Data
{
    public static class SeedData
    {
        public static readonly string[] Roles = { "Admin", "Moderator", "User" };

        public static async Task InitializeAsync(AppDbContext context, IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();

            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // The first admin comes from configuration (user-secrets or environment variables:
            // Seed__AdminEmail / Seed__AdminPassword). Nothing is hardcoded in source.
            var adminEmail = configuration["Seed:AdminEmail"];
            var adminPassword = configuration["Seed:AdminPassword"];

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var admin = await userManager.FindByEmailAsync(adminEmail);
                if (admin == null)
                {
                    admin = new IdentityUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(admin, adminPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                    }
                    else
                    {
                        logger.LogError("Could not create the seed admin: {Errors}",
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
            else if (!userManager.Users.Any())
            {
                logger.LogWarning("No users exist and Seed:AdminEmail / Seed:AdminPassword are not configured. " +
                                  "Set them to create the first admin account.");
            }

            if (!context.Ports.Any())
            {
                context.Ports.AddRange(
                    new Port { Name = "New York" },
                    new Port { Name = "Washington" },
                    new Port { Name = "Los Angeles" });
            }

            if (!context.Carriers.Any())
            {
                context.Carriers.AddRange(
                    new Carrier { Name = "Maersk" },
                    new Carrier { Name = "CMA CGM" },
                    new Carrier { Name = "Hapag-Lloyd" });
            }

            if (!context.Clients.Any())
            {
                context.Clients.AddRange(
                    new Client { Name = "ABC Shipping", JobReference = "REF123", Email = "abc@example.com" },
                    new Client { Name = "XYZ Shipping", JobReference = "REF456", Email = "xyz@example.com" });
            }

            await context.SaveChangesAsync();
        }
    }
}
