using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Web;

namespace LoadTestingApp.Models
{
    public class TestConfiguration
    {
        public string Name { get; set; }
        public int UserCount { get; set; }
        public int Duration { get; set; }
        public int RequestRate { get; set; }
        public List<ApiCall> ApiCalls { get; set; }
        public TestScenario ToScenario()
        {
            return new TestScenario
            {
                Name = this.Name,
                UserCount = this.UserCount,
                Duration = this.Duration,
                apiCalls = this.ApiCalls.Select(c => new ApiCall
                {
                    Method = c.Method,
                    Url = c.Url,
                    Body = c.Body 
                }).ToList()
            };
        }
    }

    public class ApiCall
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public string Body { get; set; } // Строковое представление JSON
    }



}

