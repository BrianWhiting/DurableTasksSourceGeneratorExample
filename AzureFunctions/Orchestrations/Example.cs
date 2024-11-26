using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.Orchestrations
{
    namespace Example
    {
        public class Orchestration
        {
            private readonly ILogger<Orchestration> _logger;

            public Orchestration(ILogger<Orchestration> logger)
            {
                _logger = logger;
            }

            [Function($"{nameof(Example)}{nameof(Orchestration)}")]
            public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context, int parameter)
            {
                _logger.LogInformation("Example Orchestration Started");
                _logger.LogInformation("Parameter: {parameter}", parameter);

                var activityParameter = new ComplexActivityParameter
                {
                    DelayInSeconds = 5,
                    MessageToDisplay = "Hello World!",
                    ReturnValue = 42,
                };

                var result = await context.CallActivityExampleActivityAsync(activityParameter);

                _logger.LogInformation("Activity Result: {result}", result);

                _logger.LogInformation("Example Orchestration Finished");
            }
        }

        public class Activity
        {
            private readonly ILogger<Activity> _logger;

            public Activity(ILogger<Activity> logger)
            {
                _logger = logger;
            }

            [Function($"{nameof(Example)}{nameof(Activity)}")]
            public async Task<int> RunAsync([ActivityTrigger] ComplexActivityParameter parameter)
            {
                _logger.LogInformation("Example Activity Started");

                _logger.LogInformation("DelayInSeconds: {DelayInSeconds}", parameter.DelayInSeconds);
                _logger.LogInformation("MessageToDisplay: {MessageToDisplay}", parameter.MessageToDisplay);

                await Task.Delay(parameter.DelayInSeconds * 1000);

                _logger.LogInformation("Example Activity Finished");

                return parameter.ReturnValue;
            }
        }

        public class ComplexActivityParameter
        {
            public int DelayInSeconds { get; set; }
            public required string MessageToDisplay { get; set; }
            public int ReturnValue { get; set; }
        }
    }
}
