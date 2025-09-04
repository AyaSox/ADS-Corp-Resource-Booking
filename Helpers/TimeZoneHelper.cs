using System;

namespace ResourceBooking.Helpers
{
    public static class TimeZoneHelper
    {
        /// <summary>
        /// Converts datetime-local input (which is in local time) to UTC for storage
        /// </summary>
        public static DateTime ConvertToUtc(DateTime localDateTime)
        {
            // datetime-local inputs are unspecified kind, but represent local time
            if (localDateTime.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(localDateTime, DateTimeKind.Local).ToUniversalTime();
            }
            else if (localDateTime.Kind == DateTimeKind.Local)
            {
                return localDateTime.ToUniversalTime();
            }
            else
            {
                // Already UTC
                return localDateTime;
            }
        }

        /// <summary>
        /// Converts UTC datetime from database to local time for display
        /// </summary>
        public static DateTime ConvertToLocal(DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Utc)
            {
                return utcDateTime.ToLocalTime();
            }
            else if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                // Assume it's UTC if unspecified (common with EF Core)
                return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc).ToLocalTime();
            }
            else
            {
                // Already local
                return utcDateTime;
            }
        }

        /// <summary>
        /// Formats datetime for datetime-local input (requires specific format)
        /// </summary>
        public static string FormatForDateTimeLocal(DateTime dateTime)
        {
            var localTime = ConvertToLocal(dateTime);
            return localTime.ToString("yyyy-MM-ddTHH:mm");
        }

        /// <summary>
        /// Formats datetime for display with timezone info
        /// </summary>
        public static string FormatForDisplay(DateTime dateTime, string format = "MMM dd, yyyy HH:mm")
        {
            var localTime = ConvertToLocal(dateTime);
            return localTime.ToString(format);
        }
    }
}