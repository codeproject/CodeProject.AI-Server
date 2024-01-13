
namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// Information on the performance of a route. This helps the Proxy controller choose the best
    /// server to send requests to.
    /// </summary>
    public class MeshServerRoutePerformance
    {
        /// <summary>
        /// Gets or sets the API route ("image/alpr") for a request, but not the full path 
        /// (eg v1/image/alpr).
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets the effective response time.
        /// </summary>
        public double EffectiveResponseTime { get; set; }

        /// <summary>
        /// Gets or sets the number of requests.
        /// </summary>
        public int NumberOfRequests { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MeshServerRoutePerformance"/> class.
        /// </summary>
        /// <param name="route">The API route ("image/alpr") for a request, but not the full path
        /// (v1/image/alpr).</param>
        /// <param name="effectiveResponseTime">The effective response time.</param>
        /// <param name="numberOfRequests">The number of requests.</param>
        /// <remarks>
        /// This class will probably change in the future to include more information.
        /// </remarks>
        public MeshServerRoutePerformance(string route, double effectiveResponseTime, int numberOfRequests)
        {
            Route                 = route;
            EffectiveResponseTime = effectiveResponseTime;
            NumberOfRequests      = numberOfRequests;
        }
    }
}