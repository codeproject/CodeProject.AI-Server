using System.Diagnostics;

using CodeProject.AI.SDK;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeProject.AI.Modules.SentimentAnalysis
{
    class SentimentAnalysisResponse : BackendSuccessResponse
    {
        /// <summary>
        /// Gets or set a value indicating whether the text is positive.
        /// </summary>
        public bool? is_positive { get; set; }

        /// <summary>
        /// Gets or sets the probablity of being positive.
        /// </summary>
        public float? positive_probability { get; set; }
    }

    public class SentimentAnalysisWorker : ModuleWorkerBase
    {
        private readonly TextClassifier _textClassifier;

        /// <summary>
        /// Initializes a new instance of the SentimentAnalysisWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="textClassifier">The TextClassifier.</param>
        /// <param name="configuration">The app configuration values.</param>
        public SentimentAnalysisWorker(ILogger<SentimentAnalysisWorker> logger,
                                       TextClassifier textClassifier,  
                                       IConfiguration configuration)
            : base(logger, configuration)
        {
            _textClassifier   = textClassifier;
        }

        /// <summary>
        /// Called before the main processing loops are started
        /// </summary>
        protected override void InitModule()
        {
            HardwareType      = _textClassifier.HardwareType;
            ExecutionProvider = _textClassifier.ExecutionProvider;
            CanUseGPU         = _textClassifier.CanUseGPU;
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        protected override BackendResponseBase ProcessRequest(BackendRequest request)
        {
            string? text = request?.payload?.GetValue("text");
            if (text is null)
                return new BackendErrorResponse($"{ModuleName} missing 'text' parameter.");

            Stopwatch sw = Stopwatch.StartNew();
            var result = _textClassifier.PredictSentiment(text);
            long inferenceMs = sw.ElapsedMilliseconds;

            if (result is null)
                return new BackendErrorResponse($"{ModuleName} PredictSentiment returned null. Try reducing the length of the input text.");

            var response = new SentimentAnalysisResponse
            {
                is_positive          = result?.Prediction?[1] > 0.5f,
                positive_probability = result?.Prediction?[1],
                processMs            = inferenceMs,
                inferenceMs          = inferenceMs
            };

            return response;
        }
    }
}