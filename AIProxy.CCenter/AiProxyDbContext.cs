using AIProxy.Common;
using Microsoft.EntityFrameworkCore;

namespace AIProxy.CCenter;

public sealed class AiProxyDbContext(DbContextOptions<AiProxyDbContext> options) : DbContext(options)
{
    public DbSet<UserRequestToken> UserRequestTokens { get; set; }
    public DbSet<Provider> Providers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UserRequestToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(26).HasValueGenerator<UlidGenerator>().ValueGeneratedOnAdd();
            entity.Property(x => x.Token).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ExpiredAt).IsRequired();

            entity.HasIndex(x => x.Token).IsUnique();
        });

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(26).HasValueGenerator<UlidGenerator>().ValueGeneratedOnAdd();
            entity.Property(x => x.Value).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Value).IsUnique();
        });
    }
}