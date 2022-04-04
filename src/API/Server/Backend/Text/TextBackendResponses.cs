namespace CodeProject.SenseAI.API.Server.Backend
{
#pragma warning disable IDE1006 // Naming Styles
    /// <summary>
    /// Text Summary Response
    /// </summary>
    public class BackendTextSummaryResponse : BackendSuccessResponse
    {
        /// <summary>
        /// Gets or sets the confidence in the recognition response
        /// </summary>
        public string? summary { get; set; }
    }
#pragma warning restore IDE1006 // Naming Styles
}
