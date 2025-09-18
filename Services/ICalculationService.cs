using ResourceBooking.Models;

namespace ResourceBooking.Services
{
    public interface ICalculationService
    {
        Task<ResourceUtilizationStats> GetResourceUtilizationAsync(int resourceId, DateTime startDate, DateTime endDate);
        Task<UserBookingStats> GetUserBookingStatsAsync(string userId, DateTime startDate, DateTime endDate);
        Task<DashboardStats> GetDashboardStatsAsync();
        Task<List<ResourcePopularityStats>> GetResourcePopularityStatsAsync(DateTime startDate, DateTime endDate);
        Task<double> CalculateResourceUtilizationPercentageAsync(int resourceId, DateTime startDate, DateTime endDate);
    }

    public class ResourceUtilizationStats
    {
        public string ResourceName { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public double TotalHoursBooked { get; set; }
        public double UtilizationPercentage { get; set; }
        public double AverageBookingDuration { get; set; }
        public DateTime MostPopularTimeSlot { get; set; }
    }

    public class UserBookingStats
    {
        public string UserName { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public int CancelledBookings { get; set; }
        public double TotalHoursBooked { get; set; }
        public double AverageBookingDuration { get; set; }
        public string MostUsedResource { get; set; } = string.Empty;
        public double CancellationRate => TotalBookings > 0 ? (double)CancelledBookings / TotalBookings * 100 : 0;
    }
    public class ResourcePopularityStats
    {
        public string ResourceName { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public double TotalHours { get; set; }
        public double UtilizationPercentage { get; set; }
        // 0..5 stars derived from a simple weighted score (bookings & hours)
        public double PopularityStars { get; set; }
    }
}