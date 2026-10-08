using FreightDesk.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel;

namespace FreightDesk.Data
{
    public class AppDbContext:IdentityDbContext

    {
        public DbSet<ContainerTR>  Containers { get; set; }
        public DbSet<Shipping>  Shippings { get; set; }
        public DbSet<SteamShipLine> SteamShipLines { get; set; }
        public DbSet<Destination> Destinations { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options):base(options)
        {

        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    optionsBuilder.UseSqlite("data source=appdata.db");
        //}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ContainerTR>()
                .HasOne(c => c.SteamShipLine)
                .WithMany()
                .HasForeignKey(c => c.SteamShipLineId);

            modelBuilder.Entity<ContainerTR>()
            .HasOne(c => c.Destination)
            .WithMany()
            .HasForeignKey(c => c.DestinationId);

            modelBuilder.Entity<ContainerTR>()
                .HasOne(c => c.Shipping)
                .WithMany()
                .HasForeignKey(c => c.ShippingId);



        }
    }
}
