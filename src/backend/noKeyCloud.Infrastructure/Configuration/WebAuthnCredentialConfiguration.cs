using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Configuration;

internal class WebAuthnCredentialConfiguration : IEntityTypeConfiguration<WebAuthnCredential>
{
    public void Configure(EntityTypeBuilder<WebAuthnCredential> builder)
    {
        builder.ToTable("WebAuthnCredentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CredentialId)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(c => c.PublicKey)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(c => c.SignCount)
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(c => c.CoseAlgorithm)
            .IsRequired();

        builder.Property(c => c.AaGuid)
            .IsRequired();

        builder.Property(c => c.Transports)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(c => c.IsBackupEligible).IsRequired();
        builder.Property(c => c.IsBackedUp).IsRequired();
        builder.Property(c => c.PrfSupported).IsRequired();

        builder.Property(c => c.PrfSalt)
            .HasColumnType("bytea")
            .IsRequired(false);

        builder.Property(c => c.DisplayName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.LastUsedAt).IsRequired(false);

        builder.Ignore(c => c.IsPostQuantum);

        builder.HasOne(c => c.User)
            .WithMany(u => u.WebAuthnCredentials)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CredentialId).IsUnique();
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.CoseAlgorithm);
    }
}