using System.Net;

namespace diplom2.Models
{
    public class CompletedTestInfo
    {
        public string TestId { get; set; }
        public string Name { get; set; }
        public int UserCount { get; set; }
        public int Duration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TestResult Result { get; set; }
        public string Status => Result != null ? "Completed" : "Failed";
    }
}
