using CodeProject.SenseAI.AnalysisLayer.SDK;

namespace SentimentAnalysis
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

    public class SentimentAnalysisWorker : CommandQueueWorker
    {
        private const string _defaultModuleId  = "sentiment-analysis";
        private const string _defaultQueueName = "sentimentanalysis_queue";
        private const string _moduleName       = "Sentiment Analysis";

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
            : base(logger, configuration, _moduleName, _defaultQueueName, _defaultModuleId)
        {
            _textClassifier  = textClassifier;
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public override BackendResponseBase ProcessRequest(BackendRequest request)
        {
            string text = request.payload.GetValue("text");
            if (text is null)
                return new BackendErrorResponse(-1, $"{ModuleName} missing 'text' parameter.");

            var result = _textClassifier.PredictSentiment(text);

            if (result is null)
                return new BackendErrorResponse(-1, $"{ModuleName} PredictSentiment returned null.");

            var response = new SentimentAnalysisResponse
            {
                is_positive          = result?.Prediction?[1] > 0.5f,
                positive_probability = result?.Prediction?[1]
            };

            return response;
        }
    }
}