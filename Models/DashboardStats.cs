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
        public string Name { get; set; }
        public string Reason { get; set; }
        public string Type { get; set; }
        public DateTime? UnavailableUntil { get; set; }
        public string BadgeClass { get; set; }
    }

    public class TodayBookingInfo
    {
        public string ResourceName { get; set; }
        public string UserName { get; set; }
        public string Purpose { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsRecurring { get; set; }
    }
}
