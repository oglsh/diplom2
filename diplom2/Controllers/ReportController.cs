﻿using diplom2.Services;
using LoadTestingApp.Services;
using Microsoft.AspNetCore.Mvc;
using NReco.PdfGenerator;
using System;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace LoadTestingApp.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportController : ControllerBase
    {
        private readonly LoadTestService _loadTestService;
        private readonly AIAnalysisService _aiService;

        public ReportController(LoadTestService loadTestService, AIAnalysisService aiService)
        {
            _loadTestService = loadTestService;
            _aiService = aiService;
        }

        [HttpGet("generate/{testId}")]
        public async Task<IActionResult> GenerateReportAsync(string testId)
        {
            try
            {               
                var test = _loadTestService.GetCompletedTests()
                    .FirstOrDefault(t => t.TestId == testId);

                if (test == null)
                    return NotFound(new { Message = "Test not found" });

                var metricsForAI = new
                {
                    totalRequests = test.Result.TotalRequests,
                    successRate = ((double)test.Result.SuccessfulRequests / test.Result.TotalRequests) * 100,
                    avgResponseTime = test.Result.AverageDuration,
                    errorCodes = test.Result.StatusCodesDistribution,
                    metr = test.Result.MetricsSummary
                };
                string metricsJson = JsonSerializer.Serialize(metricsForAI);
                var aiAnalysis = await _aiService.GetAnalysisAsync(metricsJson);

                // Генерируем HTML контент
                var htmlContent = $@"
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <style>
                        body {{ font-family: Arial; padding: 20px; }}
                        .header {{ text-align: center; }}
                        .section {{ margin-bottom: 30px; }}
                        table {{ width: 100%; border-collapse: collapse; }}
                        td, th {{ border: 1px solid #ddd; padding: 8px; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Отчет по тесту: {test.Name}</h1>
                        <p>ID теста: {test.TestId}</p>
                        <p>Время начала: {test.StartTime:yyyy-MM-dd HH:mm:ss}</p>
                        <p>Время окончания: {test.EndTime:yyyy-MM-dd HH:mm:ss}</p>
                    </div>

                    <div class='section'>
                        <h2>Основные метрики</h2>
                        <table>
                            <tr>
                                <th>Всего запросов</th>
                                <th>Успешных</th>
                                <th>Ошибок</th>
                                <th>Среднее время (мс)</th>
                            </tr>
                            <tr>
                                <td>{test.Result.TotalRequests}</td>
                                <td>{test.Result.SuccessfulRequests}</td>
                                <td>{test.Result.FailedRequests}</td>
                                <td>{test.Result.AverageDuration:F2}</td>
                            </tr>
                        </table>
                    </div>

                    <div class='section'>
                        <h2>Распределение статус-кодов</h2>
                        <table>
                            <tr>
                                <th>Статус код</th>
                                <th>Количество</th>
                            </tr>
                            {string.Join("", test.Result.StatusCodesDistribution.Select(kv =>
                                $"<tr><td>{kv.Key}</td><td>{kv.Value}</td></tr>"))}
                        </table>
                    </div>
                    </div>
                    <div class='section'>
                        <h2>Анализ ИИ</h2>
                        <div class='ai-comment'>{aiAnalysis}</div>
                    </div>
                </body>
                </html>";

                // Генерируем PDF
                var htmlToPdf = new HtmlToPdfConverter();
                htmlToPdf.CustomWkHtmlArgs = "--encoding utf-8";
                var pdfBytes = htmlToPdf.GeneratePdf(htmlContent);

                return File(pdfBytes, "application/pdf", $"report_{test.Name} - ${test.EndTime:yyyy-MM-dd HH:mm}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Ошибка генерации отчета",
                    Detail = ex.Message
                });
            }
        }
    }
}