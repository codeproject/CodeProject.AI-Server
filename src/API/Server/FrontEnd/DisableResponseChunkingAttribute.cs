using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System.Text.Json;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// Disables response chunking on an Action or Controller.
    /// </summary>
    /// <remarks>Some Http clients do not handle 'Transfer-Endcoding: chunked' properly or at all.
    ///     This results in failure to process the webapi reponses.
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
                if (context.Result is ObjectResult result)
                {
                    var json = JsonSerializer.Serialize(result.Value);
                    httpContext.Response.ContentLength = json.Length;
                }
            base.OnActionExecuted(context);
        }
    }
}
