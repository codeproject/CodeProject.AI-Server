using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Threading.Tasks;
using System.Threading;

using CodeProject.SenseAI.API.Server.Backend;
using CodeProject.SenseAI.API.Common;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// The Vision Operations
    /// </summary>
    [Route("v1/text")]
    [ApiController]
    // [DisableResponseChunking]
    public class TextController : ControllerBase
    {
        private readonly TextCommandDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the VisionController class.
        /// </summary>
        /// <param name="dispatcher">The Command Dispatcher instance.</param>
        public TextController(TextCommandDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Summarizes text.
        /// </summary>
        /// <param name="text">The Form file object.</param>
        /// <param name="num_sentences">The number of sentences to produce for the summary.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A Response containing the summary of the text.</returns>
        /// <response code="200">Returns text summary, if any.</response>
        /// <response code="400">If the no text provided.</response>            
        [HttpPost("summarize", Name = "SummarizeText")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> SummarizeText([FromForm] string? text,
                                                      [FromForm] int? num_sentences,
                                                      CancellationToken token)
        {
            var backendResponse = await _dispatcher.SummarizeText(text, num_sentences ?? 2, token);

            if (backendResponse is BackendTextSummaryResponse summaryResponse)
            {
                var response = new TextSummaryResponse
                {
                    summary = summaryResponse.summary
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        private static ErrorResponse HandleErrorResponse(BackendResponseBase backendResponse)
        {
            if (backendResponse is BackendErrorResponse errorResponse)
                return new ErrorResponse(errorResponse.error, errorResponse.code);

            return new ErrorResponse("unexpected response", -1);
        }
    }
}
