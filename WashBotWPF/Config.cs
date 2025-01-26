using System.Collections.Generic;
using System;

public class Config
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SiteOpenTime { get; set; } = "07:20:00";
    public string BookingTime { get; set; } = "07:30:00";
    public bool AutoStart { get; set; } = false;
    public bool SaveCredentials { get; set; } = false;
    public BookingMode BookingMode { get; set; } = BookingMode.SingleBooking;
    public List<DayOfWeek> ScheduledDays { get; set; } = new List<DayOfWeek>();
    public int RetryCount { get; set; } = 3;
    public int RetryDelay { get; set; } = 2;
}

public enum BookingMode
{
    SingleBooking,
    ScheduledBooking
}