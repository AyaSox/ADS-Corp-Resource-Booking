using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public interface IRecurringBookingService
    {
        Task<List<Booking>> GenerateRecurringBookingsAsync(Booking parentBooking);
        Task<bool> HasConflictsAsync(List<Booking> bookings);
        Task<List<DateTime>> GetRecurringDatesAsync(DateTime startDate, RecurrenceType recurrenceType, int interval, DateTime endDate);
        Task DeleteRecurringSeriesAsync(int parentBookingId);
        Task UpdateRecurringSeriesAsync(int parentBookingId, Booking updatedBooking);
    }
}