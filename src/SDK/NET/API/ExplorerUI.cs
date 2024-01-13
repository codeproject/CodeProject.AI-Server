using System.Text.Json.Serialization;

namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// A structure containing the HTML, CSS and Javascript that are to be injected into the 
    /// Explorer web app to allow the user to explore and test a module.
    /// </summary>
    public class ExplorerUI
    {
        /// <summary>
        /// The HTML for this UI element
        /// </summary>
        public string? Html   { get; set; }

        /// <summary>
        /// The CSS for this UI element
        /// </summary>
        public string? Css    { get; set; }

        /// <summary>
        /// The Javascript for this UI element
        /// </summary>
        public string? Script { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not this UI element is empty
        /// </summary>
        [JsonIgnore]
        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(Html)   && 
                       string.IsNullOrWhiteSpace(Script) &&
                       string.IsNullOrWhiteSpace(Css);
            }
        }
    }
}