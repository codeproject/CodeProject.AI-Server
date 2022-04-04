namespace CodeProject.SenseAI.API.Server.Backend
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// For Text Summary requests
    /// </summary>
    public class BackendTextSummaryRequest : BackendRequestBase
    {
        /// <summary>
        /// The text to summarise. 
        /// </summary>
        public string? text { get; set; }

        /// <summary>
        /// The number of sentences to generate.
        /// </summary>
        public float? numsentences { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendTextSummaryRequest()
        {
            reqtype = "textsummary";
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendTextSummaryRequest(string? text, int? numsentences) : this()
        {
            this.text         = text;
            this.numsentences = numsentences;
        }
    }
#pragma warning restore IDE1006 // Naming Styles
}