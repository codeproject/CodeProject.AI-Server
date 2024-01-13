using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// The metrics for a Route. Currently, this only provides an Effective Response Time for the
    /// route.
    /// </summary>
    /// <remarks>
    /// Future metrics might include the Size, Accuracy, Precision and/or Recall of the model used
    /// by the route if speed is not the only consideration for node selection.
    ///</remarks>
    public class RouteMetrics : IComparable<RouteMetrics>
    {
        private const int NumResponseTimeSamples = 10;

        // A NOTE ON IMPLEMENTATION:
        // We need to know how fast each server in the mesh is, so we can choose the best one. We
        // can do this however we want as long as it's consistent. The main points we must follow,
        // however, are:
        //
        // - We're judging based on full round-trip timing. Not on hardware, GPU, inference time, or
        //   anything else that's implementation specific. The question we're asking is: "who can 
        //   get the processing done fastest".
        // - We need to be aware that a server may fail or be removed from the mesh without telling
        //   us, so we need to have a means to remove them from the selection criteria as soon as 
        //   possible.
        // - We need to allow a server to recover. Maybe it timed out, maybe it had indigestion,
        //   maybe a network error. We should let it back in the mesh as soon as we can
        // - We need to be polite to the network as a whole. Specifically, we need a threshold below
        //   which a server will *not* offload requests to other servers. For instance, we have a
        //   Raspberry Pi handling the occasional object detection task. It's slow. It can, 100% of
        //   the time, get a better response from the main work computer next to it. Except someone
        //   is using that work computer and having the CPU spin up every second or so while the Pi
        //   kicks back and scrolls through the 'gram is not polite. We need a "Only offload when I
        //   am overloaded" mode.
        //
        // WHAT WE CURRENTLY DO:
        // 1. Record the timing of each request to a given server and store these in a circular 
        //    buffer. Everytime a request is made to a server, we record the response time. Only the
        //    last 10 response times are stored. NOTE: they are stored without a timestamp. 
        // 2. We have a loop that fires on a timer interval 'RouteActivityCheckInterval' which will
        //    update the route metrics for each route of each server regardless of the route/server
        //    status.
        // 3. This 'update' will check how long it's been since the last response time was stored.
        //    If it has been over 'RouteInactivityTimeSpan' then we will push a 0 time onto the 
        //    queue. This effectively deletes the oldest (but we don't know how old) record.
        // 4. We calculate the EffectiveResponseTime as the average of all values in the circular
        //    buffer
        //
        // The decay of older timings is dependent on the activity of the server so varies with 
        // load. There is no way to set the decay timings to say "anything older than 5 minutes is
        // to be ignored. It's debatable whether this is good, bad, or neither. It's just not
        // deterministic.
        //
        // SCENARIOS
        //
        // 1. Two servers, server A with a response time 3 seconds, server B times-out at 30 seconds.
        //    30 seconds after B times out, the average for both A and B will be 3 seconds. B will
        //    now be consider again for inclusion in the mesh
        // 2. A server gets hammered. Lots of responses are added and the buffer is constantly having
        //    old entries overwritten with new entries. The Effective response time will be based on
        //    a very small time period, which may hide regular spikes of slowness that happened 5
        //    seconds ago.
        // 3. A server times out on first request. It starts off with an effective response time of
        //    3 seconds. If it's chosen again and times out, it will be 6 second response. Timeouts
        //    are treated very (maybe too) kindly

        private int[]    _responseTimes     = new int[NumResponseTimeSamples];
        private int      _responseTimeIndex = 0;
        private DateTime _lastRequestTime   = DateTime.UtcNow;

        /// <summary>
        /// The Effective Response Time for this Route. This is an arbitrary value that is used to
        /// determine which node to use for a request. The lower the value, the more likely the node
        /// is to be selected. 
        /// </summary>
        public double EffectiveResponseTime => _responseTimes.Average();

        /// <summary>
        /// Gets or sets the Number of Requests sent to this Route by this server since the last
        /// time the metrics were reset, such as when the server became active.
        /// </summary>
        public int NumberOfRequests { get; internal set; }

        /// <summary>
        /// Records information about a request sent to this route.
        /// </summary>
        /// <remarks>
        /// In practice this adds a response time to the Route Metrics in order to update the 
        /// effective response time, as well as incrementing the counter on the number of responses
        /// sent to this route. These are internal details that are subject to change.
        /// </remarks>
        /// <param name="responseTime">How long the request took.</param>
        public void RecordRequest(int responseTime)
        {
            // response times are stored in a circular buffer. The oldest sample gets overwritten.
            _responseTimes[_responseTimeIndex] = responseTime;

            _responseTimeIndex = (_responseTimeIndex + 1) % NumResponseTimeSamples;
            _lastRequestTime   = DateTime.UtcNow;

            // HACK: We record a '0' request when there has been no activity for
            // a while in order to remove older request timings, which it turn
            // provides a natural decay for the effective response time. However,
            // this '0' response time isn't an actual request, so we assume there
            // will never actually be a 0 response time request and use the 0 as
            // a signal that this isn't a real request we're recording
            if (responseTime > 0)
                NumberOfRequests++;
        }

        /// <summary>
        /// Compare this Route Metrics to another Route Metrics.
        /// </summary>
        /// <param name="other">The instance to compare to.</param>
        /// <returns>-1 if less than, 0 if equal, 1 if greater.</returns>
        /// <exception cref="ArgumentNullException">Thrown if other is null.</exception>
        public int CompareTo(RouteMetrics? other)
        {
           // add guard for null and throw exception
           if (other == null)
                throw new ArgumentNullException(nameof(other));

           return EffectiveResponseTime.CompareTo(other.EffectiveResponseTime);
        }

        /// <summary>
        /// This determines if the route has not been used for a while, and if so performs some 
        /// action to adjust Effective Response Time so that all servers will occasionally be used.
        /// </summary>
        /// <param name="routeInactivityTimeSpan">How long since the last request was sent to this
        /// path to consider the response time outdated.</param>
        public void Update(TimeSpan routeInactivityTimeSpan)
        {
            TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest > routeInactivityTimeSpan)
                RecordRequest(0);
        }
    }

    /// <summary>
    /// The collection of <see cref="RouteMetrics"/> objects.
    /// </summary>
    public class RouteMetricsCollection
    {
        private readonly ConcurrentDictionary<string, RouteMetrics> _metrics = 
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the Route Metrics.
        /// </summary>
        public IEnumerable<RouteMetrics> RouteMetrics => _metrics.Values;

        /// <summary>
        /// Get the Route Metrics for the given route.
        /// </summary>
        /// <param name="route">The route to get the metrics for.</param>
        /// <returns>The Route Metrics for the given path.</returns>
        public RouteMetrics GetMetricsFromRoute(string route)
        {
            return _metrics.GetOrAdd(route, _ => new RouteMetrics());
        }
    }
}
