using CodeProject.AI.SDK.API;

namespace CodeProject.AI.SDK.Client
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
