namespace CodeProject.AI.API.Common
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// The Response for a Text Summary request.
    /// </summary>
    public class TextSummaryResponse : SuccessResponse
    {
        /// <summary>
        /// Gets or sets the text summary.
        /// </summary>
        public string? summary { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
