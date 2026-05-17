using BelgaAuthAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BelgaAuthAPI.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<License> Licenses { get; set; }
        public DbSet<UserVariable> UserVariables { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Reseller> Resellers { get; set; }
        public DbSet<CreditTransaction> CreditTransactions { get; set; }
        public DbSet<PaymentOrder> PaymentOrders { get; set; }
        public DbSet<Coupon> Coupons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurações de índices
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<License>()
                .HasIndex(l => l.Key)
                .IsUnique();

            modelBuilder.Entity<Session>()
                .HasIndex(s => s.SessionId)
                .IsUnique();

            modelBuilder.Entity<Application>()
                .HasIndex(a => a.OwnerId)
                .IsUnique();

            modelBuilder.Entity<Reseller>()
                .HasIndex(r => r.Username)
                .IsUnique();

            // Relacionamentos
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserVariable>()
                .HasOne(uv => uv.User)
                .WithMany(u => u.Variables)
                .HasForeignKey(uv => uv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Session>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Session>()
                .HasOne(s => s.Application)
                .WithMany()
                .HasForeignKey(s => s.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<License>()
                .HasOne(l => l.Reseller)
                .WithMany(r => r.Licenses)
                .HasForeignKey(l => l.ResellerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasOne(u => u.CreatedByReseller)
                .WithMany()
                .HasForeignKey(u => u.CreatedByResellerId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

