// using Microsoft.AspNetCore.Mvc;
// using System.Threading.Tasks;
// using FirefliesBackend.Services;
// using System;
// using System.Linq;

// namespace FirefliesBackend.Controllers
// {
//     [ApiController]
//     [Route("api/external")]
//     public class ExternalController : ControllerBase
//     {
//         private readonly IFirefliesClient _ff;
//         public ExternalController(IFirefliesClient ff) => _ff = ff;

//         [HttpGet("meetings")]
//         public async Task<IActionResult> GetMeetings(int limit = 25)
//         {
//             var query = @"query Transcripts($limit:Int){
//               transcripts(limit:$limit){
//                 id title date duration summary { overview short_summary }
//               }
//             }";

//             var doc = await _ff.QueryAsync(query, new { limit });
//             return Ok(doc.RootElement.GetProperty("data").GetProperty("transcripts"));
//         }

//         [HttpGet("meetings/{id}")]
//         public async Task<IActionResult> GetMeeting(string id)
//         {
//             var query = @"query Transcript($id:String!){
//               transcript(id:$id){
//                 id title date duration sentences { index text start_time end_time speaker_name }
//                 summary { overview short_summary bullet_gist }
//               }
//             }";
//             var doc = await _ff.QueryAsync(query, new { id });
//             return Ok(doc.RootElement.GetProperty("data").GetProperty("transcript"));
//         }
//     }
// }



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
            // still useful for showing recent from Fireflies — keep as is
            var query = @"query Transcripts($limit:Int){
              transcripts(limit:$limit){
                id title date duration summary { overview short_summary }
              }
            }";

            var doc = await _ff.QueryAsync(query, new { limit });
            return Ok(doc.RootElement.GetProperty("data").GetProperty("transcripts"));
        }

        [HttpGet("meetings/{id}")]
        public async Task<IActionResult> GetMeeting(string id)
        {
            // 1) Try DB first (by FirefliesId)
            var dbMeeting = await _db.Meetings.FirstOrDefaultAsync(m => m.FirefliesId == id);
            if (dbMeeting != null)
            {
                // map DB Meeting -> shape expected by frontend (similar to Fireflies transcript)
                var mapped = new
                {
                    id = dbMeeting.FirefliesId,
                    title = dbMeeting.Title,
                    date = dbMeeting.MeetingDate,
                    duration = dbMeeting.DurationSeconds,
                    // sentences should be JSON array, so parse transcriptJson
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

            // Map Fireflies JSON -> Meeting model and save to DB (safe conversion)
            try
            {
                var meeting = new Meeting
                {
                    FirefliesId = transcriptEl.GetProperty("id").GetString() ?? id,
                    Title = transcriptEl.GetProperty("title").GetString() ?? "",
                    MeetingDate = transcriptEl.TryGetProperty("date", out var d) && d.ValueKind != System.Text.Json.JsonValueKind.Null
                                  ? d.GetDateTime() // depends on shape; if string parse below
                                  : (DateTime?)null,
                    DurationSeconds = transcriptEl.TryGetProperty("duration", out var dur) && dur.ValueKind != System.Text.Json.JsonValueKind.Null
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
                // don't fail the response; return the Fireflies original payload
            }

            return Ok(transcriptEl);
        }
    }
}

