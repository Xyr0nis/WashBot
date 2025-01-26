using System;

namespace WashBotWPF
{
    public class BookingStatus
    {
        public string Status { get; set; }
        public bool IsError { get; set; }
        public DateTime LastUpdateTime { get; set; }

        public BookingStatus(string status, bool isError = false)
        {
            Status = status;
            IsError = isError;
            LastUpdateTime = DateTime.Now;
        }
    }
}