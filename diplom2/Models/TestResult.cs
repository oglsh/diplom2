using System.Net;

namespace diplom2.Models
{
    public class TestResult
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int ServerErrors500 { get; set; }
        public double AverageDuration { get; set; }
        public double MaxDuration { get; set; }
        public double MinDuration { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public double RequestsPerSecond { get; set; }
        public List<RequestResult> IndividualResults { get; set; }
        public Dictionary<string, int> StatusCodesDistribution { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public string MetricsSummary { get; set; }
    }

    public class RequestResult
    {
        public HttpStatusCode? StatusCode { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public string ErrorDetails { get; set; }
        public string MetricsData { get; set; }
    }
}
