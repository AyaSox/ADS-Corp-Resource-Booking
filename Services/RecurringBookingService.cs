using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public class RecurringBookingService : IRecurringBookingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecurringBookingService> _logger;

        public RecurringBookingService(ApplicationDbContext db, ILogger<RecurringBookingService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<Booking>> GenerateRecurringBookingsAsync(Booking parentBooking)
        {
            if (!parentBooking.IsRecurring || !parentBooking.RecurrenceType.HasValue || !parentBooking.RecurrenceEndDate.HasValue)
            {
                _logger.LogInformation("Not a recurring booking, returning single booking");
                return new List<Booking> { parentBooking };
            }

            var recurringBookings = new List<Booking> { parentBooking };
            var dates = await GetRecurringDatesAsync(
                parentBooking.StartTime,
                parentBooking.RecurrenceType.Value,
                parentBooking.RecurrenceInterval ?? 1,
                parentBooking.RecurrenceEndDate.Value);

            var duration = parentBooking.EndTime - parentBooking.StartTime;

            foreach (var date in dates.Skip(1)) // Skip first date as it's the parent
            {
                var recurringBooking = new Booking
                {
                    UserId = parentBooking.UserId,
                    ResourceId = parentBooking.ResourceId,
                    StartTime = date,
                    EndTime = date.Add(duration),
                    Purpose = $"{parentBooking.Purpose} (Recurring)",
                    IsRecurring = true,
                    RecurrenceType = parentBooking.RecurrenceType,
                    RecurrenceInterval = parentBooking.RecurrenceInterval,
                    RecurrenceEndDate = parentBooking.RecurrenceEndDate,
                    ParentBookingId = null // Will be set after parent is saved
                };

                recurringBookings.Add(recurringBooking);
            }

            _logger.LogInformation("Generated {Count} recurring bookings from {StartDate} to {EndDate}", 
                recurringBookings.Count, parentBooking.StartTime, parentBooking.RecurrenceEndDate);
            
            return recurringBookings;
        }

        public async Task<bool> HasConflictsAsync(List<Booking> bookings)
        {
            try
            {
                foreach (var booking in bookings)
                {
                    var conflicts = await _db.Bookings
                        .Where(b => b.ResourceId == booking.ResourceId && 
                                   !b.Cancelled &&
                                   b.Id != booking.Id &&
                                   booking.StartTime < b.EndTime && 
                                   b.StartTime < booking.EndTime)
                        .AnyAsync();

                    if (conflicts)
                    {
                        _logger.LogWarning("Conflict detected for booking on {Date} for resource {ResourceId}", 
                            booking.StartTime, booking.ResourceId);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for conflicts: {Message}", ex.Message);
                return true; // Assume conflict if we can't check properly
            }
        }

        public async Task<List<DateTime>> GetRecurringDatesAsync(DateTime startDate, RecurrenceType recurrenceType, int interval, DateTime endDate)
        {
            var dates = new List<DateTime>();
            var currentDate = startDate;

            while (currentDate <= endDate && dates.Count < 100) // Limit to prevent infinite loops
            {
                dates.Add(currentDate);

                currentDate = recurrenceType switch
                {
                    RecurrenceType.Daily => currentDate.AddDays(interval),
                    RecurrenceType.Weekly => currentDate.AddDays(7 * interval),
                    RecurrenceType.Monthly => currentDate.AddMonths(interval),
                    _ => currentDate.AddDays(interval)
                };
            }

            _logger.LogInformation("Generated {Count} recurring dates from {Start} to {End} with {Type} interval {Interval}", 
                dates.Count, startDate, endDate, recurrenceType, interval);
            
            return await Task.FromResult(dates);
        }

        public async Task DeleteRecurringSeriesAsync(int parentBookingId)
        {
            try
            {
                var childBookings = await _db.Bookings
                    .Where(b => b.ParentBookingId == parentBookingId)
                    .ToListAsync();

                _db.Bookings.RemoveRange(childBookings);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Deleted {Count} recurring bookings for parent {ParentId}", 
                    childBookings.Count, parentBookingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recurring series for parent {ParentId}: {Message}", 
                    parentBookingId, ex.Message);
                throw;
            }
        }

        public async Task UpdateRecurringSeriesAsync(int parentBookingId, Booking updatedBooking)
        {
            try
            {
                var childBookings = await _db.Bookings
                    .Where(b => b.ParentBookingId == parentBookingId && !b.Cancelled)
                    .ToListAsync();

                var duration = updatedBooking.EndTime - updatedBooking.StartTime;

                foreach (var childBooking in childBookings)
                {
                    // Maintain the same day/time but update other properties
                    childBooking.Purpose = $"{updatedBooking.Purpose} (Recurring)";
                    childBooking.EndTime = childBooking.StartTime.Add(duration);
                    // Note: We're not changing ResourceId to avoid complex conflict checking
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Updated {Count} recurring bookings for parent {ParentId}", 
                    childBookings.Count, parentBookingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recurring series for parent {ParentId}: {Message}", 
                    parentBookingId, ex.Message);
                throw;
            }
        }
    }
}