using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using noKeyCloud.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace noKeyCloud.Infrastructure.Configuration
{
    internal class TrashConfiguration : IEntityTypeConfiguration<Trash>
    {
        public void Configure(EntityTypeBuilder<Trash> builder)
        {
            builder.ToTable("Trashes");
            builder.HasKey(p => p.Id);
        }

    }
}
