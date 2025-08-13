using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using FirefliesBackend.Data;
using FirefliesBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FirefliesBackend.Controllers
{
    [ApiController]
    [Route("api/meetings")]
    public class MeetingsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _openAiApiKey;

        public MeetingsController(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _openAiApiKey = config["OpenAI:ApiKey"];
        }

        // ✅ Cross-platform UTC → IST conversion
        

        [HttpGet]
        public async Task<IActionResult> GetAllMeetings()
        {
            var meetings = await _db.Meetings
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    id = m.Id,
                    firefliesId = m.FirefliesId,
                    title = m.Title,
                    createdAt = m.CreatedAt,
                    meetingDate = m.MeetingDate, // ✅ Now returns IST
                    summary = m.Summary
                })
                .ToListAsync();

            return Ok(meetings);
        }

        [HttpGet("{id}/download-summary")]
        public async Task<IActionResult> DownloadSummary(int id)
        {
            var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null)
                return NotFound("Meeting not found");

            var textContent = $"Meeting Title: {meeting.Title}\nDate: {meeting.MeetingDate}\n\nSummary:\n{meeting.Summary}";
            var bytes = Encoding.UTF8.GetBytes(textContent);
            return File(bytes, "text/plain", "summary.txt");
        }

        [HttpPost("{id}/send-to-openai")]
        public async Task<IActionResult> SendSummaryToOpenAi(int id)
        {
            var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null)
                return NotFound("Meeting not found");

            var textContent = $"Meeting Title: {meeting.Title}\nDate: {meeting.MeetingDate}\n\nSummary:\n{meeting.Summary}";

            var httpClient = _httpClientFactory.CreateClient("OpenAI");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that turns meeting summaries into functional docs, mockups, and markdown." },
                    new { role = "user", content = $"Here is the meeting summary:\n\n{textContent}\n\nPlease generate:\n1. A functional document\n2. Mockup descriptions\n3. A markdown version" }
                }
            };

            var response = await httpClient.PostAsJsonAsync("v1/chat/completions", requestBody);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return Ok(new { generatedContent = content });
        }

        [HttpPost("upsert")]
public async Task<IActionResult> UpsertMeeting([FromBody] SaveMeetingDto dto)
{
    if (dto == null || string.IsNullOrWhiteSpace(dto.FirefliesId))
        return BadRequest("Invalid payload - FirefliesId required.");

    try
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.FirefliesId == dto.FirefliesId);
        
        // Use a local variable to safely handle the date
        DateTime meetingDateToSave = dto.MeetingDate.HasValue ? dto.MeetingDate.Value : DateTime.UtcNow;

        if (meeting == null)
        {
            meeting = new Meeting
            {
                FirefliesId = dto.FirefliesId,
                Title = dto.Title ?? "",
                MeetingDate = meetingDateToSave, // Use the non-nullable variable
                DurationSeconds = (int)Math.Round(dto.DurationSeconds),
                TranscriptJson = dto.TranscriptJson ?? "",
                Summary = dto.Summary ?? ""
            };
            _db.Meetings.Add(meeting);
        }
        else
        {
            meeting.Title = dto.Title ?? meeting.Title;
            meeting.MeetingDate = meetingDateToSave; // Use the non-nullable variable
            meeting.DurationSeconds = (int)Math.Round(dto.DurationSeconds);
            meeting.TranscriptJson = dto.TranscriptJson ?? meeting.TranscriptJson;
            meeting.Summary = dto.Summary ?? meeting.Summary;
        }

        await _db.SaveChangesAsync();
        return Ok(new { id = meeting.Id, firefliesId = meeting.FirefliesId });
    }
    catch (DbUpdateException dbEx) // Catch specific EF Core exceptions
    {
        Console.Error.WriteLine($"DbUpdateException: {dbEx.Message}");
        if (dbEx.InnerException != null)
        {
            Console.Error.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
        }
        return StatusCode(500, "Database save failed. Check server logs.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB Upsert failed: " + ex);
        return StatusCode(500, "Database save failed. Check server logs.");
    }
}

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var m = await _db.Meetings.FindAsync(id);
            if (m == null) return NotFound();

            return Ok(new
            {
                m.Id,
                m.FirefliesId,
                m.Title,
                meetingDate = m.MeetingDate,
                m.DurationSeconds,
                m.Summary,
                m.TranscriptJson
            });
        }

        [HttpPost("{id}/generate-summary")]
        public async Task<IActionResult> GenerateAISummary(int id)
        {
            var meeting = await _db.Meetings.FindAsync(id);
            if (meeting == null)
                return NotFound("Meeting not found.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
            var prompt = $"Summarize the following meeting:\n{meeting.Summary}";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var aiSummary = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return Ok(new { aiSummary });
        }

        [HttpPut("{id}/summary")]
        public async Task<IActionResult> UpdateSummary(int id, [FromBody] UpdateSummaryDto dto)
        {
            var m = await _db.Meetings.FindAsync(id);
            if (m == null) return NotFound();
            m.Summary = dto.Summary;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    public record SaveMeetingDto(
        string FirefliesId,
        string Title,
        DateTime? MeetingDate,
        double DurationSeconds,
        string TranscriptJson,
        string Summary
    );

    public record UpdateSummaryDto(string Summary);
}
