using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace AzureFunctions
{
    namespace HttpTriggers
    {
        public class StartOrchestration
        {
            private readonly ILogger<StartOrchestration> _logger;

            public StartOrchestration(ILogger<StartOrchestration> logger)
            {
                _logger = logger;
            }

            [Function($"{nameof(HttpTriggers)}{nameof(StartOrchestration)}")]
            public async Task<HttpResponseData> Run(
                [HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "StartOrchestration")] HttpRequestData request,
                [DurableClient] DurableTaskClient client
            )
            {
                _logger.LogInformation("StartOrchestration HTTP Trigger Started");

                var instanceId = await client.ScheduleNewOrchestrationInstanceExampleOrchestrationAsync(request.Url.ToString().Length);

                var response = await client.CreateCheckStatusResponseAsync(request, instanceId);

                _logger.LogInformation("StartOrchestration HTTP Trigger Finished");

                return response;
            }
        }
    }
}
