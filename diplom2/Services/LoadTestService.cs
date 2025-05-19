using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using diplom2.Models;
using LoadTestingApp.Models;
using Microsoft.Extensions.Logging;
using Prometheus;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LoadTestingApp.Services
{
    public class LoadTestService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private ILogger<LoadTestService> _logger;
        private static readonly Counter _requestCounter = Metrics.CreateCounter(
            "http_requests_total",
            "Total HTTP requests",
            new CounterConfiguration { LabelNames = new[] { "status_code" } });
        private readonly Histogram _responseTimes = Metrics.CreateHistogram(
            "http_response_time_ms",
            "Request duration in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0, width: 100, count: 10)
            });

        private static readonly Meter _meter = new("LoadTesting");
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTests = new();
        private readonly ConcurrentDictionary<string, CompletedTestInfo> _completedTests = new();

        public LoadTestService(IHttpClientFactory httpClientFactory, ILogger<LoadTestService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }


        //public async Task<TestResult> RunTest(TestScenario scenario)
        //{
        //    var results = new List<RequestResult>();
        //    var stopwatch = Stopwatch.StartNew();
        //    var requestIndex = 0;

        //    for (int i = 0; i < scenario.apiCalls.Count; i++)
        //    {
        //        var apiCall = scenario.apiCalls[requestIndex % scenario.apiCalls.Count];
        //        var requestStopwatch = Stopwatch.StartNew();
        //        try
        //        {
        //            var request = new HttpRequestMessage(
        //                new HttpMethod(apiCall.Method),
        //                apiCall.Url);

        //            if (!string.IsNullOrEmpty(apiCall.Body))
        //            {
        //                request.Content = new StringContent(
        //                    apiCall.Body,
        //                    Encoding.UTF8,
        //                    "application/json");
        //            }

        //            var response = await _httpClient.SendAsync(request);
        //            requestStopwatch.Stop();

        //            results.Add(new RequestResult
        //            {
        //                StatusCode = response.StatusCode,
        //                Duration = requestStopwatch.Elapsed,
        //                IsSuccess = response.IsSuccessStatusCode
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            requestStopwatch.Stop();
        //            _logger.LogError(ex, "Request {RequestNumber} failed", i + 1);
        //            results.Add(new RequestResult
        //            {
        //                Error = ex.Message,
        //                Duration = requestStopwatch.Elapsed,
        //                IsSuccess = false
        //            });
        //        }

        //        if (scenario.DelayBetweenRequests > TimeSpan.Zero)
        //        {
        //            await Task.Delay(scenario.DelayBetweenRequests);
        //        }
        //    }

        //    stopwatch.Stop();

        //    return new TestResult
        //    {
        //        TotalRequests = scenario.apiCalls.Count,
        //        SuccessfulRequests = results.Count(r => r.IsSuccess),
        //        FailedRequests = results.Count(r => !r.IsSuccess),
        //        AverageDuration = results.Where(r => r.IsSuccess).Average(r => r.Duration.TotalMilliseconds),
        //        TotalDuration = stopwatch.Elapsed,
        //        IndividualResults = results
        //    };
        //}


        //private HttpRequestMessage CreateRequestMessage(TestConfiguration config)
        //{
        //    return new HttpRequestMessage(new HttpMethod(config.Method), config.TargetUrl)
        //    {
        //        Content = config.Method == "GET" ? null : new StringContent(config.Body ?? "")
        //    };
        //}

        public async Task<TestResult> RunTest(string testId, TestScenario scenario)
        {
            var results = new ConcurrentBag<RequestResult>();
            var cts = new CancellationTokenSource();
            var random = new Random();
            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;

            if (!_runningTests.TryAdd(testId, cts))
            {
                throw new InvalidOperationException("Test with this ID already exists");
            }


            try
            {

                //cts.CancelAfter(TimeSpan.FromSeconds(scenario.Duration));

                // 3. Настройка параллелизма
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                    CancellationToken = cts.Token
                };

                // 4. Запуск теста
                await Parallel.ForEachAsync(
                    Enumerable.Repeat(0, scenario.UserCount),
                    parallelOptions,
                    async (_, ct) =>
                    {
                        var localRandom = new Random(Guid.NewGuid().GetHashCode());
                        var delayMs = scenario.DelayBetweenRequests.TotalMilliseconds > 0
                            ? scenario.DelayBetweenRequests.TotalMilliseconds
                            : 1000.0 / scenario.RequestRate;

                        int requestsSent = 0;

                        try
                        {
                            while //(!ct.IsCancellationRequested &&
                               // (scenario.RequestCount == 0 || 
                                (requestsSent < (scenario.Duration))
                            {
                                var apiCall = scenario.apiCalls[localRandom.Next(scenario.apiCalls.Count)];
                                var requestResult = await ExecuteRequestAsync(apiCall, ct).ConfigureAwait(false);
                                results.Add(requestResult);
                                requestsSent++;

                                await Task.Delay((int)Math.Max(1, delayMs), ct).ConfigureAwait(false);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // корректная отмена
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "User thread error");
                        }
                    });
            }
            finally
            {
                var endTime = DateTime.UtcNow;
                _completedTests.TryAdd(testId, new CompletedTestInfo
                {
                    TestId = testId,
                    Name = scenario.Name,
                    Duration = scenario.Duration,
                    UserCount = scenario.UserCount,
                    StartTime = startTime,
                    EndTime = endTime,
                    Result = CompileResults(results, stopwatch.Elapsed)
                });
                stopwatch.Stop();
            }
            return CompileResults(results, stopwatch.Elapsed);
        }

        private async Task<RequestResult> ExecuteRequestAsync(ApiCall apiCall, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(
                    GetHttpMethod(apiCall.Method),
                    apiCall.Url
                );

                if (apiCall.Body != null)
                {
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(apiCall.Body),
                        Encoding.UTF8,
                        "application/json");
                }
                using var client = _httpClientFactory.CreateClient("LoadTestClient");
                using var response = await client.SendAsync(request, ct);

                // Запись метрик
                _requestCounter.WithLabels(((int)response.StatusCode).ToString()).Inc();
                _responseTimes.Observe(sw.Elapsed.TotalMilliseconds);

                // Обработка 500 ошибки
                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return new RequestResult
                    {
                        StatusCode = response.StatusCode,
                        Error = $"HTTP Error: {(int)response.StatusCode}",
                        ErrorDetails = content, // Сохраняем тело ошибки
                        Duration = sw.Elapsed,
                        IsSuccess = false
                    };
                }

                return new RequestResult
                {
                    StatusCode = response.StatusCode,
                    Duration = sw.Elapsed,
                    IsSuccess = response.IsSuccessStatusCode
                };
            }
            catch (TaskCanceledException ex) when (ct.IsCancellationRequested)
            {
                return new RequestResult
                {
                    Error = "Request canceled",
                    Duration = sw.Elapsed,
                    IsSuccess = false
                };
            }
            catch (Exception ex)
            {
                return new RequestResult
                {
                    Error = ex.GetType().Name,
                    Duration = sw.Elapsed,
                    ErrorDetails = ex.Message,
                    IsSuccess = false
                };
            }
            finally
            {
                sw.Stop();
            }
        }

        private TestResult CompileResults(ConcurrentBag<RequestResult> results, TimeSpan totalDuration)
        {
            var successful = results.Where(r => r.IsSuccess).ToList();
            var failed = results.Where(r => !r.IsSuccess).ToList();

            var result = new TestResult
            {
                TotalRequests = results.Count,
                SuccessfulRequests = successful.Count,
                FailedRequests = failed.Count,
                ServerErrors500 = results.Count(r => r.StatusCode == HttpStatusCode.InternalServerError),
                AverageDuration = successful.Any() ? successful.Average(r => r.Duration.TotalMilliseconds) : 0,
                MaxDuration = successful.Any() ? successful.Max(r => r.Duration.TotalMilliseconds) : 0,
                MinDuration = successful.Any() ? successful.Min(r => r.Duration.TotalMilliseconds) : 0,
                TotalDuration = totalDuration,
                RequestsPerSecond = totalDuration.TotalSeconds > 0 ? results.Count / totalDuration.TotalSeconds : 0,
                IndividualResults = results.ToList(),
                        Errors = results
            .Where(r => !string.IsNullOrEmpty(r.Error))
            .Select(r => $"[{r.StatusCode}] {r.Error}: {r.ErrorDetails}")
            .ToList(),
            };


            // Группировка статус-кодов
            result.StatusCodesDistribution = results
                .GroupBy(r => r.StatusCode?.ToString() ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            return result;
        }

        public bool StopTest(string testId)
        {
            if (_runningTests.TryRemove(testId,out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                return true;
            }
            return false;
        }

        private static HttpMethod GetHttpMethod(string method)
        {
            return method.ToUpper() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                _ => throw new ArgumentException($"Unsupported method: {method}")
            };
        }


        public IEnumerable<CompletedTestInfo> GetCompletedTests()
        {
            return _completedTests.Values.OrderByDescending(t => t.EndTime);
        }

        public CompletedTestInfo GetCompletedTest(string testId)
        {
            if (_completedTests.TryGetValue(testId, out var test))
            {
                return test;
            }
            return null;
        }

        public int GetCompletedTestCount()
        { 
            return _completedTests.Count;
        }

        public int GetActiveTestsCount()
        {
            return _runningTests.Count;
        }
    }
}

