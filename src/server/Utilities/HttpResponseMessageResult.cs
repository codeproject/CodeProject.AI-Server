using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

// Mostly from a Bing Chat sample that was wrong.
// Probably based on https://stackoverflow.com/questions/54136488/correct-way-to-return-httpresponsemessage-as-iactionresult-in-net-core-2-2

namespace CodeProject.AI.Server.Utilities
{
    /// <summary>
    /// A class the will return a HttpResponseMessage as an IActionResult
    /// </summary>
    public class HttpResponseMessageResult : IActionResult
    {
        private readonly HttpResponseMessage _responseMessage;
        /// <summary>
        /// Create a new HttpResponseMessageResult.
        /// </summary>
        /// <param name="responseMessage"></param>
        public HttpResponseMessageResult(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage; // could add throw if null
        }

        /// <summary>
        /// Execute the result.
        /// </summary>
        /// <param name="context">THe Context.</param>
        /// <returns>The Task.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task ExecuteResultAsync(ActionContext context)
        {
            HttpResponse response = context.HttpContext.Response;

            if (_responseMessage == null)
            {
                string message = "Response message cannot be null";
                throw new InvalidOperationException(message);
            }

            using (_responseMessage)
            {
                response.StatusCode = (int)_responseMessage.StatusCode;
                IHttpResponseFeature? responseFeature = context.HttpContext.Features.Get<IHttpResponseFeature>();
                if (responseFeature != null)
                {
                    responseFeature.ReasonPhrase = _responseMessage.ReasonPhrase;
                }

                HttpResponseHeaders responseHeaders = _responseMessage.Headers;

                // Ignore the Transfer-Encoding header if it is just "chunked".
                // We let the host decide about whether the response should be chunked or not.
                if (responseHeaders.TransferEncodingChunked == true &&
                    responseHeaders.TransferEncoding.Count == 1)
                {
                    responseHeaders.TransferEncoding.Clear();
                }

                foreach (KeyValuePair<string, IEnumerable<string>> header in responseHeaders)
                {
                    response.Headers.Add(header.Key, new StringValues(header.Value.ToArray()));
                }

                if (_responseMessage.Content != null)
                {
                    HttpContentHeaders contentHeaders = _responseMessage.Content.Headers;

                    // Copy the response content headers only after ensuring they are complete.
                    // We ask for Content-Length first so that ComputeLengthAsync is forced
                    // to run and set the value of the content headers.
                    long? unused = contentHeaders.ContentLength;

                    foreach (KeyValuePair<string, IEnumerable<string>> header in contentHeaders)
                    {
                        response.Headers.Add(header.Key, new StringValues(header.Value.ToArray()));
                    }

                    await _responseMessage.Content.CopyToAsync(response.Body);
                }
            }
        }
    }
}
