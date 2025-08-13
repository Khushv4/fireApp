using System.Collections.Generic;
using System.Threading.Tasks;
using FirefliesBackend.Models;

namespace FirefliesBackend.Services
{
    public static class ChatGptService
    {
        public static async Task<List<FileResult>> GenerateFilesFromSummary(string summary)
        {
            // TODO: Integrate with OpenAI API
            // For now, return mock files
            await Task.Delay(1000);
            return new List<FileResult>
            {
                new FileResult { Name = "File1.txt", Content = $"File 1 based on: {summary}" },
                new FileResult { Name = "File2.txt", Content = $"File 2 based on: {summary}" },
                new FileResult { Name = "File3.txt", Content = $"File 3 based on: {summary}" }
            };
        }
    }
}
