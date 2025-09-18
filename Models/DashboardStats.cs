using System.Collections.Generic;
using System;

namespace ResourceBooking.Models
{
    public class DashboardStats
    {
        public int TotalResources { get; set; }
        public int AvailableResources { get; set; }
        public int UnavailableResources { get; set; }
        public int TotalBookingsToday { get; set; }
        public int TotalBookingsThisWeek { get; set; }
        public int TotalBookingsThisMonth { get; set; }
        public double AverageUtilizationPercentage { get; set; }
        public int ActiveUsers { get; set; }
        public List<UnavailableResourceInfo> UnavailableResourcesInfo { get; set; } = new();
        public List<TodayBookingInfo> TodayBookings { get; set; } = new();
    }

    public class UnavailableResourceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime? UnavailableUntil { get; set; }
        public string BadgeClass { get; set; } = string.Empty;
    }

    public class TodayBookingInfo
    {
        public string ResourceName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsRecurring { get; set; }
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
        public double PopularityStars { get; set; }
    }
}
