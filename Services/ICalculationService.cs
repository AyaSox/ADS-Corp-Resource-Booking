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
}