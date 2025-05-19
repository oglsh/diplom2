using diplom2.Models;
using LoadTestingApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace diplom2.Controllers
{
    [ApiController]
    [Route("api/stats")]
    public class DashboardController : Controller
    {
        private readonly LoadTestService _loadTestService;
        private readonly IMemoryCache _cache;

        public DashboardController(LoadTestService loadTestService, IMemoryCache cache)
        {
            _loadTestService = loadTestService;
            _cache = cache;
        }

        [HttpGet("getStats")]
        public IActionResult GetStats()
        {
            var stats = new
            {
                ActiveTests = _loadTestService.GetActiveTestsCount(),
                CompletedToday = _loadTestService.GetCompletedTestCount(),
                AvgResponseTime = GetAverageResponseTime()
            };

            return Ok(stats);
        }

        private double GetAverageResponseTime()
        {
            return _cache.Get<List<TestResult>>("completed_tests")
                ?.Average(t => t.AverageDuration) ?? 0;
        }
    }
}
