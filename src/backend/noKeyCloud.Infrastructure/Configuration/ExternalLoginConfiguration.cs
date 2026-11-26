using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using noKeyCloud.Domain.Entities;

namespace noKeyCloud.Infrastructure.Configuration;

internal class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogins");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ProviderSubject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.LinkedAt).IsRequired();

        builder.HasOne(e => e.User)
            .WithMany(u => u.ExternalLogins)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.Provider, e.ProviderSubject })
            .IsUnique();

        builder.HasIndex(e => e.UserId);
    }
}