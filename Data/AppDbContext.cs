using FreightDesk.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel;

namespace FreightDesk.Data
{
    public class AppDbContext:IdentityDbContext

    {
        public DbSet<Shipment>  Shipments { get; set; }
        public DbSet<Client>  Clients { get; set; }
        public DbSet<Carrier> Carriers { get; set; }
        public DbSet<Port> Ports { get; set; }
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

            modelBuilder.Entity<Shipment>()
                .HasOne(c => c.Carrier)
                .WithMany()
                .HasForeignKey(c => c.CarrierId);

            modelBuilder.Entity<Shipment>()
            .HasOne(c => c.Port)
            .WithMany()
            .HasForeignKey(c => c.PortId);

            modelBuilder.Entity<Shipment>()
                .HasOne(c => c.Client)
                .WithMany()
                .HasForeignKey(c => c.ClientId);



        }
    }
}
