using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;
using BookingService.Application.Mapping;
using BookingService.Domain.Entities;
using BookingService.Domain.Exceptions;
using BookingService.Domain.Interfaces;

namespace BookingService.Application.Services;

/// <summary>
/// Classic application service that orchestrates the Reservation use
/// cases. It coordinates the domain aggregate and the repository but
/// holds no business invariants itself — those live in the entity and,
/// for seat uniqueness, in the database constraint.
/// </summary>
public sealed class ReservationAppService : IReservationService
{
    private readonly IReservationRepository _repository;
    private readonly ReservationOptions _options;

    public ReservationAppService(IReservationRepository repository, ReservationOptions options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task<ReservationResponse> CreateAsync(CreateReservationRequest request, CancellationToken ct = default)
    {
        var reservation = Reservation.Create(
            userId: request.UserId,
            eventId: request.EventId,
            seatSection: request.SeatSection,
            seatRow: request.SeatRow,
            seatNumber: request.SeatNumber,
            price: request.Price,
            holdDuration: _options.HoldDuration);

        // No pre-check for seat availability on purpose: it would race.
        // The DB unique constraint is the source of truth; the repository
        // surfaces a violation as SeatAlreadyReservedException.
        await _repository.AddAsync(reservation, ct);
        await _repository.SaveChangesAsync(ct);

        return reservation.ToResponse();
    }

    public async Task<ReservationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await _repository.GetByIdAsync(id, ct);
        return reservation?.ToResponse();
    }

    public async Task<ReservationResponse> ConfirmAsync(Guid id, ConfirmReservationRequest request, CancellationToken ct = default)
    {
        var reservation = await _repository.GetByIdAsync(id, ct)
                          ?? throw new ReservationNotFoundException(id);

        reservation.Confirm(request.PaymentId);

        _repository.Update(reservation);
        await _repository.SaveChangesAsync(ct);

        return reservation.ToResponse();
    }

    public async Task<ReservationResponse> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await _repository.GetByIdAsync(id, ct)
                          ?? throw new ReservationNotFoundException(id);

        reservation.Cancel();

        _repository.Update(reservation);
        await _repository.SaveChangesAsync(ct);

        return reservation.ToResponse();
    }
}
