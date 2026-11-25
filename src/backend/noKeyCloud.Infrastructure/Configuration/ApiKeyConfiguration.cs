using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Configuration;

internal class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(k => k.KeyPrefix)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(k => k.KeyHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(k => k.Scopes)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.ExpiresAt).IsRequired(false);
        builder.Property(k => k.LastUsedAt).IsRequired(false);
        builder.Property(k => k.RevokedAt).IsRequired(false);

        builder.Ignore(k => k.IsActive);

        builder.HasOne(k => k.User)
            .WithMany(u => u.ApiKeys)
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(k => k.KeyPrefix).IsUnique();
        builder.HasIndex(k => k.UserId);
    }
}