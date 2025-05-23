using diplom2.Models;
using LoadTestingApp.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

                var contentElem = choices[0]
       .GetProperty("message")
       .GetProperty("content");

                string rawText;
                if (contentElem.ValueKind == JsonValueKind.Array)
                {
                    var segments = contentElem
                        .EnumerateArray()
                        .Where(el => el.TryGetProperty("text", out _))
                        .Select(el => el.GetProperty("text").GetString().Trim());

                    rawText = string.Join(" ", segments);
                }
                else if (contentElem.ValueKind == JsonValueKind.String)
                {
                    rawText = contentElem.GetString();
                }
                else
                {
                    throw new InvalidOperationException("Не удалось извлечь текст из message.content");
                }

                // Убираем лишние пробелы и единичные переносы строк
                rawText = Regex.Replace(rawText, @"\r?\n[ \t]*", "\n");       // нормализуем переходы
                rawText = Regex.Replace(rawText, @"[ \t]{2,}", " ");         // сводим подряд идущие пробелы к одному

                // Разбиваем на абзацы по двойным переносам строк (\n\n)
                var paragraphs = rawText
                    .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim());

                // Формируем финальный HTML
                var sb = new StringBuilder();
                foreach (var para in paragraphs)
                {
                    // внутри абзаца одиночные \n превращаем в <br/>
                    var htmlPara = Regex.Replace(
                        para,
                        @"\r?\n",
                        "<br/>"
                    );
                    sb.Append("<p>");
                    sb.Append(htmlPara);
                    sb.Append("</p>");
                }

                return sb.ToString();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к OpenAI");
                return "Анализ временно недоступен.";
            }
        }
    }
}
