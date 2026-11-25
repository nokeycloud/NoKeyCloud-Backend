using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Configuration;

internal class OpaqueCredentialConfiguration : IEntityTypeConfiguration<OpaqueCredential>
{
    public void Configure(EntityTypeBuilder<OpaqueCredential> builder)
    {
        builder.ToTable("OpaqueCredentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.RegistrationRecord)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasOne(c => c.User)
            .WithOne(u => u.OpaqueCredential)
            .HasForeignKey<OpaqueCredential>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId)
            .IsUnique();
    }
}