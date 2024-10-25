namespace PingConMonitor.DataStructures
{
    internal class PingPoint(long pingTime)
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public long PingTime { get; set; } = pingTime;
    }
}
