using System;

namespace FirefliesBackend.Models
{
    public class Meeting
    {
        public int Id { get; set; }
        public string FirefliesId { get; set; } = string.Empty;
        public string Title { get; set; }
        public DateTime? MeetingDate { get; set; }
        public int DurationSeconds { get; set; }
        public string TranscriptJson { get; set; }
        public string Summary { get; set; }
    public string SummaryFileContent { get; set; }=string.Empty;
      public string FunctionalDoc { get; set; } = string.Empty; // Initialize - This is the primary culprit!
        public string Mockups { get; set; } = string.Empty; // Initialize
        public string Markdown { get; set; } = string.Empty; // Initialize
        public string GeneratedFilesJson { get; set; } = string.Empty; // Stores generated files from ChatGPT
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

