using ShpCore.Logging;
using SharpCore.CLI.Env;
using System.Threading.Tasks;

namespace ShpCore.Launcher.CLI;

public class Program
{
    public static async Task Main(string[] args)
    {
        await SharpCoreCLI.Run(args);
    }

}
