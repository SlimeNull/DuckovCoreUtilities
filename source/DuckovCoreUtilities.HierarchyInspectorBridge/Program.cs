using System.IO.Pipes;

namespace SlimeNull.DuckovCoreUtilities.HierarchyInspectorBridge;

internal class Program
{
    private static async Task Main(string[] args)
    {
        const string PipeName = "SlimeNull.DuckovCoreUtilities.HierachyInspector";

        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync();

        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        var stdinToPipe = Task.Run(async () =>
        {
            await stdin.CopyToAsync(pipe);
            pipe.WaitForPipeDrain();
        });
        var pipeToStdout = pipe.CopyToAsync(stdout);

        await Task.WhenAny(stdinToPipe, pipeToStdout);
    }
}