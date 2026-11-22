using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Configuration;

internal class MasterKeyWrapConfiguration : IEntityTypeConfiguration<MasterKeyWrap>
{
    public void Configure(EntityTypeBuilder<MasterKeyWrap> builder)
    {
        builder.ToTable("MasterKeyWraps", t =>
        {
            t.HasCheckConstraint(
                "CK_MasterKeyWraps_Argon2Required",
                "(\"Type\" NOT IN ('OpaquePassword', 'Passphrase')) OR " +
                "(\"Argon2MemorySizeKiB\" >= 19456 AND \"Argon2Iterations\" >= 2 AND \"Argon2Parallelism\" >= 1)");

            t.HasCheckConstraint(
                "CK_MasterKeyWraps_WebAuthnPrfCredentialRequired",
                "(\"Type\" <> 'WebAuthn') OR (\"WebAuthnCredentialId\" IS NOT NULL)");
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(w => w.WrappedMasterKey)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(w => w.Salt)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();

        builder.OwnsOne(w => w.Argon2idParams, kdf =>
        {
            kdf.Property(p => p.MemorySizeKiB).HasColumnName("Argon2MemorySizeKiB");
            kdf.Property(p => p.Iterations).HasColumnName("Argon2Iterations");
            kdf.Property(p => p.Parallelism).HasColumnName("Argon2Parallelism");
        });

        builder.HasOne(w => w.User)
            .WithMany(u => u.MasterKeyWraps)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.WebAuthnCredential)
            .WithMany()
            .HasForeignKey(w => w.WebAuthnCredentialId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.UserId, w.Type });

        builder.HasIndex(w => w.UserId)
            .IsUnique()
            .HasFilter("\"Type\" = 'OpaquePassword'")
            .HasDatabaseName("IX_MasterKeyWraps_UserId_Opaque");

        builder.HasIndex(w => w.WebAuthnCredentialId)
            .IsUnique()
            .HasFilter("\"WebAuthnCredentialId\" IS NOT NULL")
            .HasDatabaseName("IX_MasterKeyWraps_WebAuthnCredentialId");
    }
}