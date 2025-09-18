// ... existing code
        public int TotalBookingsThisMonth { get; set; }
        public double AverageUtilizationPercentage { get; set; }
        public int ActiveUsers { get; set; }
        public List<UnavailableResourceInfo> UnavailableResourcesInfo { get; set; } = new();
        public List<TodayBookingInfo> TodayBookings { get; set; } = new();
    }

    public class UnavailableResourceInfo
// ... existing code
