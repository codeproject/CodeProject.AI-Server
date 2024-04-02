using System.Text.Json.Serialization;

namespace CodeProject.AI.SDK.API
{
    public delegate Task<ModuleResponse> LongProcessMethod(BackendRequest request,
                                                           CancellationToken cancellationToken);

    /// <summary>
    /// The common data for responses from analysis modules.
    /// </summary>
    public class ModuleResponse : BaseResponse
    {
        /// <summary>
        /// Gets or sets the Id of the Module handling this request
        /// </summary>
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the name of the Module handling this request
        /// </summary>
        public string? ModuleName { get; set; }
        
        /// <summary>
        /// Gets or sets the optional command associated with this request
        /// </summary>
        public string? Command { get; set; }
       
        /// <summary>
        /// Gets or sets the ID of the request being serviced
        /// </summary>
        public string? RequestId { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the device that performed the inference operation for this 
        /// request. eg CPU, GPU, TPU, NPU etc
        /// </summary>
        public string? InferenceDevice { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds required to perform the AI inference operation(s)
        /// for this response.
        /// </summary>
        public long InferenceMs { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds required to perform the AI processing for this
        /// response. This includes the inference, as well as any pre- and post-processing.
        /// </summary>
        public long ProcessMs { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds required to run the full task in processing this
        /// response.
        /// </summary>
        public long AnalysisRoundTripMs { get; set; }

        /// <summary>
        /// The long process delegate to be executed. Long process modules will return this value in
        /// the <see cref="ProcessRequest"/> method to indicate to the module runner that the module
        /// has a long process, and that this method should be run in the background.
        /// </summary>
        [JsonIgnore]
        public LongProcessMethod? LongProcessMethod { get; set; }
    }
   
    /// <summary>
    /// Represents a failed response from a module.
    /// </summary>
    public class ModuleErrorResponse : ModuleResponse
    {
        /// <summary>
        /// Gets or sets the error message, if any. May be null if no error.
        /// </summary>
        public string? Error { get; set; }

        public ModuleErrorResponse()
        {
            Success = false;
        }

        public ModuleErrorResponse(string? error)
        {
            Success = false;
            Error   = error;
        }
    }

    public class ModuleLongProcessCancelResponse : ModuleResponse
    {
        /// <summary>
        /// Gets or sets the message, if any.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the CommandId of the long process being cancelled.
        /// </summary>
        public string? CommandId { get; set; }

        /// <summary>
        /// Gets or sets the current command status
        /// </summary>
        public string? CommandStatus { get; set; }
    }
}
