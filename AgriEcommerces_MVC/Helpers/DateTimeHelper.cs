namespace AgriEcommerces_MVC.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        /// <summary>
        /// Lấy thời gian Việt Nam (UTC+7) dạng Unspecified cho PostgreSQL
        /// </summary>
        public static DateTime GetVietnamTime()
        {
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            return DateTime.SpecifyKind(vnTime, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Chuyển DateTime bất kỳ sang Unspecified
        /// </summary>
        public static DateTime ToUnspecified(DateTime dateTime)
        {
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Lấy thời gian Việt Nam với offset (ví dụ: +15 phút)
        /// </summary>
        public static DateTime GetVietnamTimeWithOffset(TimeSpan offset)
        {
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            return DateTime.SpecifyKind(vnTime.Add(offset), DateTimeKind.Unspecified);
        }
    }
}