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

        public async Task<List<Booking>> GenerateRecurringBookingsAsync(Booking parentBooking, CancellationToken ct = default)
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
                parentBooking.RecurrenceEndDate.Value,
                ct);

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
                    ParentBookingId = null // set after parent save
                };
                recurringBookings.Add(recurringBooking);
            }

            _logger.LogInformation("Generated {Count} recurring bookings from {StartDate} to {EndDate}", recurringBookings.Count, parentBooking.StartTime, parentBooking.RecurrenceEndDate);
            return recurringBookings;
        }

        public async Task<bool> HasConflictsAsync(List<Booking> bookings, CancellationToken ct = default)
        {
            var details = await GetConflictsAsync(bookings, ct);
            return details.Any(d => d.Conflicts.Count > 0);
        }

        public async Task<List<(Booking Candidate, List<Booking> Conflicts)>> GetConflictsAsync(IEnumerable<Booking> bookings, CancellationToken ct = default)
        {
            var result = new List<(Booking, List<Booking>)>();
            foreach (var booking in bookings)
            {
                var conflicts = await _db.Bookings.AsNoTracking()
                    .Where(b => b.ResourceId == booking.ResourceId && !b.Cancelled && b.Id != booking.Id && booking.StartTime < b.EndTime && b.StartTime < booking.EndTime)
                    .ToListAsync(ct);
                result.Add((booking, conflicts));
            }
            return result;
        }

        public async Task<List<DateTime>> GetRecurringDatesAsync(DateTime startDateUtc, RecurrenceType recurrenceType, int interval, DateTime endDateUtc, CancellationToken ct = default)
        {
            var dates = new List<DateTime>();
            var currentDate = DateTime.SpecifyKind(startDateUtc, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc);

            while (currentDate <= endUtc && dates.Count < 100)
            {
                dates.Add(currentDate);
                currentDate = recurrenceType switch
                {
                    RecurrenceType.Daily => currentDate.AddDays(interval),
                    RecurrenceType.Weekly => currentDate.AddDays(7 * interval),
                    RecurrenceType.Monthly => currentDate.AddMonths(interval),
                    _ => currentDate.AddDays(interval)
                };
                if (ct.IsCancellationRequested) break;
            }

            _logger.LogInformation("Generated {Count} recurring dates from {Start} to {End} with {Type} interval {Interval}", dates.Count, startDateUtc, endDateUtc, recurrenceType, interval);
            return await Task.FromResult(dates);
        }

        public async Task DeleteRecurringSeriesAsync(int parentBookingId, CancellationToken ct = default)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var childBookings = await _db.Bookings.Where(b => b.ParentBookingId == parentBookingId).ToListAsync(ct);
                _db.Bookings.RemoveRange(childBookings);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                _logger.LogInformation("Deleted {Count} recurring bookings for parent {ParentId}", childBookings.Count, parentBookingId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error deleting recurring series for parent {ParentId}: {Message}", parentBookingId, ex.Message);
                throw;
            }
        }

        public async Task UpdateRecurringSeriesAsync(int parentBookingId, Booking updatedBooking, CancellationToken ct = default)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var childBookings = await _db.Bookings.Where(b => b.ParentBookingId == parentBookingId && !b.Cancelled).ToListAsync(ct);
                var duration = updatedBooking.EndTime - updatedBooking.StartTime;

                foreach (var childBooking in childBookings)
                {
                    childBooking.Purpose = $"{updatedBooking.Purpose} (Recurring)";
                    childBooking.EndTime = childBooking.StartTime.Add(duration);
                }
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                _logger.LogInformation("Updated {Count} recurring bookings for parent {ParentId}", childBookings.Count, parentBookingId);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error updating recurring series for parent {ParentId}: {Message}", parentBookingId, ex.Message);
                throw;
            }
        }

        public async Task AddExceptionAsync(int parentBookingId, DateTime occurrenceStartUtc, CancellationToken ct = default)
        {
            // Soft-delete or mark a specific occurrence as an exception (skip)
            var occurrence = await _db.Bookings.FirstOrDefaultAsync(b => b.ParentBookingId == parentBookingId && b.StartTime == occurrenceStartUtc, ct);
            if (occurrence != null)
            {
                occurrence.Cancelled = true; // simplest approach; could introduce a separate Exception flag
                await _db.SaveChangesAsync(ct);
            }
        }
    }
}