using ResourceBooking.Models;
using System.Threading;

namespace ResourceBooking.Services
{
    public interface IRecurringBookingService
    {
        Task<List<Booking>> GenerateRecurringBookingsAsync(Booking parentBooking, CancellationToken ct = default);
        Task<bool> HasConflictsAsync(List<Booking> bookings, CancellationToken ct = default);
        Task<List<(Booking Candidate, List<Booking> Conflicts)>> GetConflictsAsync(IEnumerable<Booking> bookings, CancellationToken ct = default);
        Task<List<DateTime>> GetRecurringDatesAsync(DateTime startDateUtc, RecurrenceType recurrenceType, int interval, DateTime endDateUtc, CancellationToken ct = default);
        Task DeleteRecurringSeriesAsync(int parentBookingId, CancellationToken ct = default);
        Task UpdateRecurringSeriesAsync(int parentBookingId, Booking updatedBooking, CancellationToken ct = default);
        // Exceptions support: skip dates or override a single occurrence
        Task AddExceptionAsync(int parentBookingId, DateTime occurrenceStartUtc, CancellationToken ct = default);
    }
}