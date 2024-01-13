namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// The Response for a Text Summary request.
    /// </summary>
    public class TextSummaryResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the text summary.
        /// </summary>
        public string? Summary { get; set; }
    }
}
