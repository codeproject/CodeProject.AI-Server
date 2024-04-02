using System;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Modules.SentimentAnalysis
{
    class SentimentAnalysisResponse : ModuleResponse
    {
        /// <summary>
        /// Gets or set a value indicating whether the text is positive.
        /// </summary>
        public bool? Is_positive { get; set; }

        /// <summary>
        /// Gets or sets the probability of being positive.
        /// </summary>
        public float? Positive_probability { get; set; }
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
        /// <param name="hostApplicationLifetime">The applicationLifetime object</param>
        public SentimentAnalysisWorker(ILogger<SentimentAnalysisWorker> logger,
                                       TextClassifier textClassifier,  
                                       IConfiguration configuration,
                                       IHostApplicationLifetime hostApplicationLifetime)
            : base(logger, configuration, hostApplicationLifetime)
        {
            _textClassifier   = textClassifier;
        }

        /// <summary>
        /// Called before the main processing loops are started
        /// </summary>
        protected override void Initialize()
        {
            InferenceDevice  = _textClassifier.InferenceDevice;
            InferenceLibrary = _textClassifier.InferenceLibrary;
            CanUseGPU        = _textClassifier.CanUseGPU;
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        protected override ModuleResponse Process(BackendRequest request)
        {
            string? text = request?.payload?.GetValue("text");
            if (text is null)
                return new ModuleErrorResponse($"{ModuleName} missing 'text' parameter.");

            Stopwatch sw = Stopwatch.StartNew();
            var result = _textClassifier.PredictSentiment(text);
            long inferenceMs = sw.ElapsedMilliseconds;

            if (result is null)
                return new ModuleErrorResponse($"{ModuleName} ProcessRequest returned null. Try reducing the length of the input text.");

            var response = new SentimentAnalysisResponse
            {
                Is_positive          = result?.Prediction?[1] > 0.5f,
                Positive_probability = result?.Prediction?[1],
                ProcessMs            = inferenceMs,
                InferenceMs          = inferenceMs,
                InferenceDevice      = _textClassifier.InferenceDevice
            };

            return response;
        }

        /// <summary>
        /// Called when the module is asked to execute a self-test to ensure it install and runs
        /// correctly
        /// </summary>
        /// <returns>An exit code for the test. 0 = no error.</returns>
        protected override int SelfTest()
        {
            Environment.SetEnvironmentVariable("TF_CPP_MIN_LOG_LEVEL", "3"); 

            RequestPayload payload = new RequestPayload("analyse");
            payload.SetValue("text", "This is a shiny happy wonderful sentence");

            var request = new BackendRequest(payload);
            ModuleResponse response = Process(request);

            if (response.Success)
                return 0;

            return 1;
        }
    }
}