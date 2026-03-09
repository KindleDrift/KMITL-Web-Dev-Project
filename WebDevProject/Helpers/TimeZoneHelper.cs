namespace WebDevProject.Helpers
{
    public static class TimeZoneHelper
    {
        public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static DateTime FromClientLocalToUtc(DateTime localDateTime, int? clientTimeZoneOffsetMinutes)
        {
            if (!clientTimeZoneOffsetMinutes.HasValue)
            {
                return EnsureUtc(localDateTime);
            }

            var offset = TimeSpan.FromMinutes(-clientTimeZoneOffsetMinutes.Value);
            var local = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, offset).UtcDateTime;
        }
    }
}
