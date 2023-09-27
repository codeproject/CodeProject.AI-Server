using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System.Text.Json;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// Disables response chunking on an Action or Controller.
    /// </summary>
    /// <remarks>Some Http clients do not handle 'Transfer-Encoding: chunked' properly or at all.
    /// This results in failure to process the API responses.
    /// </remarks>
    public class DisableResponseChunkingAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Sets the content length to prevent chunking.  Assumes ContentType is json.
        /// </summary>
        /// <param name="context">THe context.</param>
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            var httpContext = context.HttpContext;
            if (httpContext.Response.ContentLength is null)
            {
                if (context.Result is ObjectResult result)
                {
                    // Making 2 whopper assumptions here
                    // 1. The response is json encoded (which we've specified in the controller)
                    // 2. The json encoding that's produced by the controller is the same length as
                    //    what we generate here. This is justifiable since we're using the standard
                    //    .NET json encoder. The one that replaced the previous "standard" json
                    //    encoder. Standards are great: there are so many to choose from.
                    // The goal is to set the content length header in order to stop chunking. We
                    // could use buffering but there's a risk that buffering is all or nothing for
                    // the application. This needs to be checked.
                    var json = JsonSerializer.Serialize(result.Value);
                    httpContext.Response.ContentLength = json.Length;
                }
            }

            base.OnActionExecuted(context);
        }
    }
}
