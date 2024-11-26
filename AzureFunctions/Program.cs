using Microsoft.Extensions.Hosting;

namespace AzureFunctions
{
    public class Program
    {
        public static async Task Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .Build();

            await host.RunAsync();
        }
    }
}
