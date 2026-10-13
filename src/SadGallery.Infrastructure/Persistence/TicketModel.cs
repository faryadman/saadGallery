using Microsoft.EntityFrameworkCore;
using SadGallery.Infrastructure.Identity;
using SadGallery.Infrastructure.Persistence.Entities;
namespace SadGallery.Infrastructure.Persistence;

internal static class TicketModel
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<SupportTicket>(e =>
        {
            e.ToTable("SupportTickets"); e.HasKey(x => x.Id);
            e.Property(x => x.Number).UseIdentityColumn(); e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Subject).HasMaxLength(150).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Subject);
            e.HasIndex(x => new { x.CustomerUserId, x.LastActivityAtUtc });
            e.HasIndex(x => new { x.Status, x.LastActivityAtUtc });
            e.HasIndex(x => new { x.AssignedOperatorUserId, x.Status });
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedOperatorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.RelatedProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<TicketMessage>(e =>
        {
            e.ToTable("TicketMessages"); e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.TicketId, x.CreatedAtUtc });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<TicketInternalNote>(e =>
        {
            e.ToTable("TicketInternalNotes"); e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.TicketId, x.CreatedAtUtc });
            e.HasOne<SupportTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<TicketEvent>(e =>
        {
            e.ToTable("TicketEvents"); e.Property(x => x.Kind).HasMaxLength(80).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(500); e.HasIndex(x => new { x.TicketId, x.CreatedAtUtc });
            e.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
