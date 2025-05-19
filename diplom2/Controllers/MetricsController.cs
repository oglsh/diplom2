using Microsoft.AspNetCore.Mvc;
using Prometheus;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading.Tasks;

namespace LoadTestingApp.Controllers
{
    [ApiController]
    [Route("api/metrics")]
    public class MetricsController : ControllerBase
    {
        private readonly Counter _requestCounter;

        public MetricsController()
        {
            //_requestCounter = Metrics.CreateCounter("total_requests", "Total number of requests.");
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var stream = new MemoryStream();
            await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
            stream.Position = 0;
            return File(stream, "text/plain");
        }
    }
}