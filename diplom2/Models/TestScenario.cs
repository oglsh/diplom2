using System.Collections.Generic;

namespace LoadTestingApp.Models
{
    public class TestScenario
    {
        public string Name { get; set; }
        public int UserCount { get; set; }
        public int Duration { get; set; }
        public List<ApiCall> apiCalls { get; set; }
        public int RequestRate { get; set; }
        public TimeSpan DelayBetweenRequests { get; set; }
        public int RequestCount { get; set; }
    }
}