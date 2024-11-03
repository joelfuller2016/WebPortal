using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace WebAI
{
    public class OpenAIService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly string apiKey;
        private static readonly string apiUrl = "https://api.openai.com/v1/chat/completions";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static OpenAIService()
        {
            apiKey = ConfigurationManager.AppSettings["OpenAIApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                log.Error("OpenAI API key is missing from the configuration.");
                throw new ConfigurationErrorsException("OpenAI API key is not configured.");
            }
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<List<OutlineItem>> GenerateOutlineAsync(string conversationContext)
        {
            try
            {
                string outlinePrompt = $"Based on the following conversation, generate a project outline with 5-10 key points. Format the response as a numbered list.\n\nConversation context:\n{conversationContext}";
                string apiResponse = await GetAIResponseAsync(outlinePrompt);
                return ParseOutline(apiResponse);
            }
            catch (Exception ex)
            {
                log.Error("Error in GenerateOutlineAsync", ex);
                throw;
            }
        }

        public async Task<string> GetAIResponseAsync(string userMessage)
        {
            try
            {
                var messages = new List<object>
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = userMessage }
            };
                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = messages
                };
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic responseObject = JsonConvert.DeserializeObject(responseContent);
                    return responseObject.choices[0].message.content;
                }
                else
                {
                    log.Error($"Error from OpenAI API: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    throw new HttpRequestException($"Error from OpenAI API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                log.Error("Error in GetAIResponseAsync", ex);
                throw;
            }
        }

        private List<OutlineItem> ParseOutline(string apiResponse)
        {
            var outlineItems = new List<OutlineItem>();
            var lines = apiResponse.Split('\n', (char)StringSplitOptions.RemoveEmptyEntries);
            int id = 1;

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

                // Remove leading numbers, dots, or dashes
                trimmedLine = System.Text.RegularExpressions.Regex.Replace(trimmedLine, @"^\d+[\.\)]?\s*-?\s*", "");

                outlineItems.Add(new OutlineItem
                {
                    Id = id++,
                    ItemText = trimmedLine,
                    IsChecked = false
                });
            }


            return outlineItems;
        }
        
    }

    public class OutlineItem
    {
        public int Id { get; set; }
        public string ItemText { get; set; }
        public bool IsChecked { get; set; }
    }
}

