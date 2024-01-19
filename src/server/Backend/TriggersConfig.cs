using System.Runtime.Serialization;

namespace CodeProject.AI.Server.Backend
{
    /// <summary>
    /// Defines the comparison to be checked between a a reported and expected property value
    /// </summary>
    public enum TriggerComparison
    {
        /// <summary>
        /// Value observed is equal to the value in the trigger
        /// </summary>
        [EnumMember(Value = "Equals")]
        Equals,

        /// <summary>
        /// Value observed is less than the value in the trigger
        /// </summary>
        [EnumMember(Value = "LessThan")]
        LessThan,

        /// <summary>
        /// Value observed is less than or equal to the value in the trigger
        /// </summary>
        [EnumMember(Value = "LessThanOrEquals")]
        LessThanOrEquals,

        /// <summary>
        /// Value observed is greater than the value in the trigger
        /// </summary>
        [EnumMember(Value = "Equals")]
        GreaterThan,

        /// <summary>
        /// Value observed is greater than or equal to the value in the trigger
        /// </summary>
        [EnumMember(Value = "GreaterThanOrEquals")]
        GreaterThanOrEquals,

        /// <summary>
        /// Value observed is not equal to the value in the trigger
        /// </summary>
        [EnumMember(Value = "NotEquals")]
        NotEquals
    }

    /// <summary>
    /// The type of task to be run when a trigger is triggered
    /// </summary>
    public enum TriggerTaskType
    {
        /// <summary>
        /// Execute a command locally
        /// </summary>
        Command
    }

    /// <summary>
    /// The type of task to be run when a trigger is triggered
    /// </summary>
    public class TriggerTask
    {
        /// <summary>
        /// Execute a shell comment locally
        /// </summary>
        public TriggerTaskType Type { get; set; }

        /// <summary>
        /// The command string
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// The command arguments
        /// </summary>
        public string? Args { get; set; }
    }

    /// <summary>
    /// The set of tasks for each platform that should be executed
    /// </summary>
    public class PlatformTasks
    {
        /// <summary>
        /// The task for Windows x64
        /// </summary>
        public TriggerTask? Windows { get; set; }

        /// <summary>
        /// The task for Windows arm64
        /// </summary>
        public TriggerTask? WindowsArm64 { get; set; }

        /// <summary>
        /// The task for Linux x64
        /// </summary>
        public TriggerTask? Linux { get; set; }

        /// <summary>
        /// The task for Linux arm64
        /// </summary>
        public TriggerTask? LinuxArm64 { get; set; }

        /// <summary>
        /// The task for macOS x64
        /// </summary>
        public TriggerTask? MacOS { get; set; }

        /// <summary>
        /// The task for macOS arm64
        /// </summary>
        public TriggerTask? MacOSArm64 { get; set; }
    }

    /// <summary>
    /// Defines a trigger event
    /// </summary>
    public class Trigger
    {
        /// <summary>
        /// Gets or sets the queue to be watched for this trigger
        /// </summary>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the name of the collection in the module's response that holds the
        /// collection of predictions. If this is null then it's assumed the response is just
        /// label/confidence for a single prediction. If there is a collection of predictions,
        /// each prediction will hold its own label/confidence
        /// </summary>
        public string? PredictionsCollectionName { get; set; }

        /// <summary>
        /// Gets or sets the property to be tested for this trigger
        /// </summary>
        public string? PropertyName { get; set; }

        /// <summary>
        /// Gets or sets the value to be checked for this trigger to be triggered
        /// </summary>
        public object? PropertyValue  { get; set; }

        /// <summary>
        /// Gets or sets how the value of this trigger is to be checked against the observed value
        /// </summary>
        public TriggerComparison? PropertyComparison { get; set; }

        /// <summary>
        /// The value of the inference confidence to test for this trigger. A value of null means
        /// do not check confidence
        /// </summary>
        public float? Confidence { get; set; }

        /// <summary>
        /// Gets or sets how the value of confidence (if provided) is to be checked against the 
        /// observed confidence
        /// </summary>
        public TriggerComparison? ConfidenceComparison { get; set; }

        /// <summary>
        /// Gets or sets the task for each platform to be run when triggered
        /// </summary>
        public PlatformTasks? PlatformTasks { get; set; }

        /// <summary>
        /// Gets the task for the given platform
        /// </summary>
        /// <param name="platform">The name of the platform</param>
        /// <returns>A task</returns>
        public TriggerTask? GetTask(string platform) => platform.ToLower() switch
        {
            "windows"      => PlatformTasks?.Windows,
            "windowsarm64" => PlatformTasks?.WindowsArm64,
            "linux"        => PlatformTasks?.Linux,
            "linuxarm64"   => PlatformTasks?.LinuxArm64,
            "macos"        => PlatformTasks?.MacOS,
            "macosarm64"   => PlatformTasks?.MacOSArm64,
            _ => null
        };

        /// <summary>
        /// Tests whether the given property value satisfies the trigger
        /// </summary>
        /// <param name="value">The property value</param>
        /// <param name="confidence">The confidence of this value</param>
        /// <returns>true if the test passes; false otherwise</returns>
        public bool Test(string? value, float confidence)
        {
            // Test must pass the confidence test first.
            if (Confidence is not null)
            {
                if (ConfidenceComparison == TriggerComparison.Equals)
                    if (confidence != Confidence) 
                        return false;

                if (ConfidenceComparison == TriggerComparison.GreaterThan)
                    if (confidence <= Confidence) 
                        return false;

                if (ConfidenceComparison == TriggerComparison.GreaterThanOrEquals)
                    if (confidence < Confidence) 
                        return false;

                if (ConfidenceComparison == TriggerComparison.LessThan)
                    if (confidence >= Confidence) 
                        return false;

                if (ConfidenceComparison == TriggerComparison.LessThanOrEquals)
                    if (confidence > Confidence) 
                        return false;

                if (ConfidenceComparison == TriggerComparison.NotEquals)
                    if (confidence == Confidence) 
                        return false;
            }

            string? propertyValue = PropertyValue?.ToString();

            if (value is null || propertyValue is null)
            {
                if (PropertyComparison == TriggerComparison.Equals)
                    return value is null && propertyValue is null;

                if (PropertyComparison == TriggerComparison.NotEquals)
                    return value is null && propertyValue is not null ||
                           value is not null && propertyValue is null;

                return false;
            }

            if (PropertyComparison == TriggerComparison.Equals)
                return value == propertyValue;

            if (PropertyComparison == TriggerComparison.GreaterThan)
                return value.CompareTo(propertyValue) > 0;

            if (PropertyComparison == TriggerComparison.GreaterThanOrEquals)
                return value.CompareTo(propertyValue) >= 0;

            if (PropertyComparison == TriggerComparison.LessThan)
                return value.CompareTo(propertyValue) < 0;

            if (PropertyComparison == TriggerComparison.LessThanOrEquals)
                return value.CompareTo(propertyValue) <= 0;

            if (PropertyComparison == TriggerComparison.NotEquals)
                return value != propertyValue;

            return false;
        }
    }

    /// <summary>
    /// Triggers config values.
    /// </summary>
    public class TriggersConfig
    {
        /// <summary>
        /// The name of the trigger config file
        /// </summary>
        public static string TriggersCfgFilename = "triggers.json";

        /// <summary>
        /// The name of the trigger config section within the trigger config file.
        /// </summary>
        public static string TriggersCfgSection  = "triggersSection";

        /// <summary>
        /// Gets or sets the version info
        /// </summary>
        public Trigger[]? Triggers { get; set; }
    }
}
