using diplom2.Models;
using LoadTestingApp.Services;
using System.Text;
using System.Text.Json;

namespace diplom2.Services
{
    public class AIAnalysisService
    {
        private string _API_KEY = "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";
        private string _API_URL = "https://openrouter.ai/api/v1/chat/completions";
        private readonly HttpClient _httpClient;
        private ILogger<LoadTestService> _logger;
        public AIAnalysisService(HttpClient httpClient, ILogger<LoadTestService> logger)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_API_KEY}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _logger = logger;
        }
        public async Task<string> GetAnalysisAsync(string metricsJson)
        {
            var request = new
            {
                model = "openai/gpt-4o-mini",
                messages = new[] {
                new {
                    role = "user",
                    content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = $"Проанализируй метрики нагрузочного теста: {metricsJson}. " +
                              "Выяви аномалии, оцени стабильность системы и дай рекомендации."
                                }
                            }
                }
            }
            };
            try
            {
                var jsonRequest = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_API_URL, content);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseContent);

                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0)
                    throw new InvalidOperationException("AI-ответ не содержит ни одного выбора");

                var message = choices[0].GetProperty("message");
                var contentElem = message.GetProperty("content");

                // Если это массив сегментов, объединяем их тексты
                if (contentElem.ValueKind == JsonValueKind.Array)
                {
                    var texts = contentElem
                        .EnumerateArray()
                        .Where(el => el.TryGetProperty("text", out _))
                        .Select(el => el.GetProperty("text").GetString());

                    return string.Join("", texts);
                }
                // Если вдруг всё же строка
                else if (contentElem.ValueKind == JsonValueKind.String)
                {
                    return contentElem.GetString();
                }

                throw new InvalidOperationException("Не удалось найти поле message.content в ответе AI");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к OpenAI");
                return "Анализ временно недоступен.";
            }
        }
    }
}
