namespace ILAQA.DTOs
{
    // Optional body of POST /api/runs/{id}/end. Older apps send no body.
    // The server checks both values against the stored GPS before use.
    public class EndRunRequest
    {
        // When the runner pressed Finish on the phone (UTC). Lets a Finish
        // that was synced later keep its real end time.
        public DateTime? EndedAt { get; set; }

        // Running time measured on the phone, pauses excluded.
        public int? ActiveSeconds { get; set; }
    }
}
