namespace SharpCore.Abstractions;

public interface IKernelEntryPoint
{
    void Run(string payloadPath, string protocol, string adapter, bool devMode);
    
}
