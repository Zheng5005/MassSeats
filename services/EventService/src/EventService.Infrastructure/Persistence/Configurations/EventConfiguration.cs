using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the Event entity. This is the ONLY place where
/// the database shape (table/column names, snake_case, constraints)
/// is allowed to live — it never leaks into the domain entity.
/// </summary>
public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.CategoryId)
            .HasColumnName("category_id");

        builder.Property(e => e.VenueId)
            .HasColumnName("venue_id");

        builder.Property(e => e.EventDate)
            .HasColumnName("event_date");

        builder.Property(e => e.TicketPrice)
            .HasColumnName("ticket_price")
            .HasPrecision(18, 2);

        builder.Property(e => e.TotalSeats)
            .HasColumnName("total_seats");

        builder.Property(e => e.BannerImage)
            .HasColumnName("banner_image");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => e.Title)
            .IsUnique();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Venue>()
            .WithMany()
            .HasForeignKey(e => e.VenueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
