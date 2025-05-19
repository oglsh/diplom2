using Microsoft.AspNetCore.Mvc;
using LoadTestingApp.Models;
using LoadTestingApp.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace LoadTestingApp.Controllers
{
    [ApiController]
    [Route("api/tests")]
    public class TestController : ControllerBase
    {
        private readonly LoadTestService _loadTestService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TestController> _logger;

        public TestController(LoadTestService loadTestService, IMemoryCache cache, ILogger<TestController> logger)
        {
            _loadTestService = loadTestService;
            _cache = cache;
            _logger = logger;
        }



        [HttpGet]
        ActionResult test()
        {
            var df = "Мы тут";
            return Ok(new { TestId = Guid.NewGuid() });
        }



        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] TestConfiguration config)
        {
            try
            {
                var scenario = config.ToScenario();
                var testId = Guid.NewGuid().ToString();

                _cache.Set(testId, scenario, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1)
                });

                return Ok(new { TestId = testId });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/run")]
        public async Task<IActionResult> RunTest(string id, [FromBody] TestConfiguration config)
        {
            try
            {
                if (!_cache.TryGetValue(id, out TestScenario scenario))
                    return NotFound(new ProblemDetails
                    {
                        Title = "Test scenario not found",
                        Status = 404
                    });

                scenario.RequestRate = config.RequestRate;
                scenario.Duration = config.Duration;

                var result = await _loadTestService.RunTest(id, scenario);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid request parameters",
                    Detail = ex.Message,
                    Status = 400
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running test {TestId}", id);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }


        [HttpPost("{id}/stop")]
        public IActionResult StopTest(string id)
        {
            try
            {
                bool stopped = _loadTestService.StopTest(id);
                if (!stopped)
                    return NotFound(new { message = "Тест с таким ID не найден или уже остановлен" });

                return Ok(new { message = "Тест успешно остановлен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при остановке теста {TestId}", id);
                return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
            }
        }

        [HttpGet("completed")]
        public IActionResult GetCompletedTests()
        {
            try
            {
                var completedTests = _loadTestService.GetCompletedTests();
                return Ok(completedTests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting completed tests");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        [HttpGet("{id}/completedTest")]
        public IActionResult GetCompletedTest(string id)
        {
            try
            {
                var completedTests = _loadTestService.GetCompletedTest(id);
                return Ok(completedTests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting completed tests");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

    }
}
