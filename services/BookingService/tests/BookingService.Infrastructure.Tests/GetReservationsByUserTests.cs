using BookingService.Domain.Entities;
using BookingService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Tests;

public sealed class GetReservationsByUserTests
{
    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyCallersReservations_NewestFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        var callerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

        var callerOlder = await CreateReservationAsync(options, callerId, baseTime, cancellationToken);
        await CreateReservationAsync(options, otherUserId, baseTime.AddMinutes(30), cancellationToken);
        var callerNewer = await CreateReservationAsync(options, callerId, baseTime.AddMinutes(60), cancellationToken);

        IReadOnlyList<Reservation> result;
        await using (var dbContext = new BookingDbContext(options))
        {
            result = await new ReservationRepository(dbContext)
                .GetByUserIdAsync(callerId, cancellationToken);
        }

        Assert.Equal(new[] { callerNewer.Id, callerOlder.Id }, result.Select(r => r.Id));
        Assert.All(result, r => Assert.Equal(callerId, r.UserId));
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenUserHasNoReservations_ReturnsEmptyList()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        var callerId = Guid.NewGuid();
        await CreateReservationAsync(options, Guid.NewGuid(), DateTimeOffset.UtcNow, cancellationToken);

        IReadOnlyList<Reservation> result;
        await using (var dbContext = new BookingDbContext(options))
        {
            result = await new ReservationRepository(dbContext)
                .GetByUserIdAsync(callerId, cancellationToken);
        }

        Assert.Empty(result);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static DbContextOptions<BookingDbContext> CreateDbOptions(
        SqliteConnection connection) =>
        new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options;

    private static async Task<Reservation> CreateReservationAsync(
        DbContextOptions<BookingDbContext> options,
        Guid userId,
        DateTimeOffset reservedAt,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new BookingDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var reservation = Reservation.Create(
            userId,
            Guid.NewGuid(),
            "Floor",
            "A",
            1,
            50m,
            TimeSpan.FromMinutes(10));
        dbContext.Reservations.Add(reservation);
        dbContext.Entry(reservation).Property(nameof(Reservation.ReservedAt)).CurrentValue = reservedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return reservation;
    }
}
