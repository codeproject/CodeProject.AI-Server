using System.Text.Json.Serialization;

namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// Represents an option in a dropdown menu in the dashboard
    /// </summary>
    public class DashboardMenuOption
    {
        /// <summary>
        /// Gets or sets the label for this menu option
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Gets or sets the setting to be modified by this menu option
        /// </summary>
        public string? Setting { get; set; }

        /// <summary>
        /// Gets or sets the value to be set by this menu option
        /// </summary>
        public string? Value { get; set; }
    }

    /// <summary>
    /// Represents a dropdown menu in the dashboard. This will be used to construct a dropdown menu
    /// in the CodeProject.AI Server dashboard for the given module.
    /// </summary>
    public class DashboardMenu
    {
        /// <summary>
        /// Gets or sets the label for this menu
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Gets or sets the options for this menu
        /// </summary>
        public DashboardMenuOption[]? Options { get; set; }
    }

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