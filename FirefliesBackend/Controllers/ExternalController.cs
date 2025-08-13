using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using FirefliesBackend.Services;
using System;
using System.Linq;
using FirefliesBackend.Data; // add
using FirefliesBackend.Models; // optional for mapping
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FirefliesBackend.Controllers
{
    [ApiController]
    [Route("api/external")]
    public class ExternalController : ControllerBase
    {
        private readonly IFirefliesClient _ff;
        private readonly AppDbContext _db;

        public ExternalController(IFirefliesClient ff, AppDbContext db)
        {
            _ff = ff;
            _db = db;
        }

        [HttpGet("meetings")]
        public async Task<IActionResult> GetMeetings(int limit = 25)
        {
            var query = @"query Transcripts($limit:Int){
              transcripts(limit:$limit){
                id title date duration summary { overview short_summary }
              }
            }";

            var doc = await _ff.QueryAsync(query, new { limit });

            var meetings = doc.RootElement.GetProperty("data").GetProperty("transcripts")
                .EnumerateArray()
                .Select(m =>
                {
                    DateTime? parsedDate = null;
                    if (m.TryGetProperty("date", out var d) && d.ValueKind != JsonValueKind.Null)
                    {
                        if (d.ValueKind == JsonValueKind.Number)
                        {
                            // Assuming 'date' is a Unix timestamp in milliseconds
                            long unixTimestampMillis = d.GetInt64();
                            parsedDate = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMillis).DateTime;
                        }
                        else if (d.ValueKind == JsonValueKind.String)
                        {
                            // Fallback for if it might sometimes be a string
                            if (DateTime.TryParse(d.GetString(), out DateTime dt))
                            {
                                parsedDate = dt;
                            }
                        }
                    }

                    return new
                    {
                        id = m.GetProperty("id").GetString(),
                        title = m.GetProperty("title").GetString(),
                        date = parsedDate, // Use the parsed date
                        duration = m.GetProperty("duration").GetDouble(),
                        summary = m.GetProperty("summary")
                    };
                });

            return Ok(meetings);
        }

        [HttpGet("meetings/{id}")]
        public async Task<IActionResult> GetMeeting(string id)
        {
            // 1) Try DB first
            var dbMeeting = await _db.Meetings.FirstOrDefaultAsync(m => m.FirefliesId == id);
            if (dbMeeting != null)
            {
                var mapped = new
                {
                    id = dbMeeting.FirefliesId,
                    title = dbMeeting.Title,
                    date = dbMeeting.MeetingDate,
                    duration = dbMeeting.DurationSeconds,
                    sentences = string.IsNullOrWhiteSpace(dbMeeting.TranscriptJson)
                                 ? Array.Empty<object>()
                                 : JsonSerializer.Deserialize<object[]>(dbMeeting.TranscriptJson),
                    summary = new
                    {
                        overview = dbMeeting.Summary,
                        short_summary = dbMeeting.Summary
                    }
                };
                return Ok(mapped);
            }

            // 2) Not in DB -> fetch from Fireflies, persist, then return Fireflies response
            var query = @"query Transcript($id:String!){
              transcript(id:$id){
                id title date duration sentences { index text start_time end_time speaker_name }
                summary { overview short_summary bullet_gist }
              }
            }";
            var doc = await _ff.QueryAsync(query, new { id });
            var transcriptEl = doc.RootElement.GetProperty("data").GetProperty("transcript");

            try
            {
                DateTime? meetingDate = null;
                if (transcriptEl.TryGetProperty("date", out var d) && d.ValueKind != JsonValueKind.Null)
                {
                    if (d.ValueKind == JsonValueKind.Number)
                    {
                        // Assuming 'date' is a Unix timestamp in milliseconds
                        long unixTimestampMillis = d.GetInt64();
                        meetingDate = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMillis).DateTime;
                    }
                    else if (d.ValueKind == JsonValueKind.String)
                    {
                        // Fallback for if it might sometimes be a string
                        if (DateTime.TryParse(d.GetString(), out DateTime dt))
                        {
                            meetingDate = dt;
                        }
                    }
                }

                var meeting = new Meeting
                {
                    FirefliesId = transcriptEl.GetProperty("id").GetString() ?? id,
                    Title = transcriptEl.GetProperty("title").GetString() ?? "",
                    MeetingDate = meetingDate, // Use the parsed date
                    DurationSeconds = transcriptEl.TryGetProperty("duration", out var dur) && dur.ValueKind != JsonValueKind.Null
                                          ? Convert.ToInt32(Math.Round(dur.GetDouble()))
                                          : 0,
                    TranscriptJson = transcriptEl.TryGetProperty("sentences", out var s) ? s.ToString() : "[]",
                    Summary = transcriptEl.TryGetProperty("summary", out var su) && su.TryGetProperty("overview", out var ov) ? ov.GetString() ?? "" : ""
                };

                _db.Meetings.Add(meeting);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed writing new meeting to DB: " + ex);
            }

            // Also ensure the returned mappedTranscript handles the number as a date
            DateTime? mappedTranscriptDate = null;
            if (transcriptEl.TryGetProperty("date", out var dateProp) && dateProp.ValueKind != JsonValueKind.Null)
            {
                if (dateProp.ValueKind == JsonValueKind.Number)
                {
                    long unixTimestampMillis = dateProp.GetInt64();
                    mappedTranscriptDate = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMillis).DateTime;
                }
                else if (dateProp.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(dateProp.GetString(), out DateTime dt))
                    {
                        mappedTranscriptDate = dt;
                    }
                }
            }


            var mappedTranscript = new
            {
                id = transcriptEl.GetProperty("id").GetString(),
                title = transcriptEl.GetProperty("title").GetString(),
                date = mappedTranscriptDate, // Use the parsed date
                duration = transcriptEl.GetProperty("duration").GetDouble(),
                sentences = transcriptEl.GetProperty("sentences"),
                summary = transcriptEl.GetProperty("summary")
            };

            return Ok(mappedTranscript);
        }

        [HttpPost("generate-files")]
        public async Task<IActionResult> GenerateFiles([FromBody] GenerateFilesRequest req)
        {
            var files = await ChatGptService.GenerateFilesFromSummary(req.Summary);
            return Ok(files);
        }

        [HttpPost("save-files")]
        public async Task<IActionResult> SaveFiles([FromBody] SaveFilesRequest req)
        {
            var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == req.MeetingId);
            if (meeting == null) return NotFound();

            foreach (var file in req.Files)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file.Name).ToLower();
                if (name == "markdown")
                    meeting.Markdown = file.Content;
                else if (name == "functionaldoc")
                    meeting.FunctionalDoc = file.Content;
                else if (name == "mockups")
                    meeting.Mockups = file.Content;
            }

            meeting.GeneratedFilesJson = JsonSerializer.Serialize(req.Files);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}