using Polly;
using Polly.CircuitBreaker;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// HTTP message handler that implements retry and circuit breaker policies
    /// </summary>
    public class HttpClientPolicyHandler : DelegatingHandler
    {
        private readonly ILogger _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;

        /// <summary>
        /// Initializes a new instance of the HttpClientPolicyHandler class
        /// </summary>
        public HttpClientPolicyHandler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure retry policy
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    3, // Number of retries
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Warning(
                            "Request failed with {ExceptionType}. Retry attempt {RetryCount} after {RetryInterval}s",
                            exception.GetType().Name,
                            retryCount,
                            timeSpan.TotalSeconds);
                    });

            // Configure circuit breaker policy
            _circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // Break on 50% failure rate
                    samplingDuration: TimeSpan.FromSeconds(30),
                    minimumThroughput: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, duration) =>
                    {
                        _logger.Warning(
                            "Circuit broken due to {ExceptionType} for {Duration}s",
                            ex.Exception?.GetType().Name ?? "unknown",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.Information("Circuit reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.Information("Circuit half-open: testing connectivity");
                    });
        }

        /// <summary>
        /// Sends an HTTP request with retry and circuit breaker policies
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy)
                .ExecuteAsync(async () =>
                {
                    var response = await base.SendAsync(request, cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.Warning(
                            "Request to {Url} failed with status code {StatusCode}",
                            request.RequestUri,
                            response.StatusCode);
                    }
                    else
                    {
                        _logger.Information(
                            "Request to {Url} succeeded with status code {StatusCode}",
                            request.RequestUri,
                            response.StatusCode);
                    }

                    return response;
                });
        }
    }
}
