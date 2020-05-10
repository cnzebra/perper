using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Perper.WebJobs.Extensions.Config;
using Perper.WebJobs.Extensions.Model;

namespace DotNet.FunctionApp
{
    public static class Launcher
    {
        [FunctionName("Launcher")]
        public static async Task RunAsync([PerperStreamTrigger(RunOnStartup = true)]
            PerperStreamContext context,
            CancellationToken cancellationToken)
        {
            await using var cyclicGenerator = context.DeclareStream(typeof(CyclicGenerator));
            await using var firstGenerator =
                await context.StreamFunctionAsync(typeof(Generator), new {count = 10, tag = "first"});
            await using var secondGenerator =
                await context.StreamFunctionAsync(typeof(Generator).GetMethod("Run"), new {count = 10, tag = "second"});
            await using var processor =
                await context.StreamFunctionAsync(typeof(Processor), new
                {
                    generator = new[] {firstGenerator, secondGenerator, cyclicGenerator},
                    multiplier = 10
                });
            await context.StreamFunctionAsync(cyclicGenerator, new {processor});

            await using var consumer =
                await context.StreamActionAsync(typeof(Consumer), new {processor});

            await using var dataFrameGenerator = await context.StreamFunctionAsync(typeof(DataFrameGenerator),
                new {indices = new[] {1, 2, 3, 4, 5, 6}});
            await using var dataFrameConsumer =
                await context.StreamActionAsync(typeof(Consumer), new {dataFrameGenerator});

            await context.BindOutput(cancellationToken);
        }
    }
}