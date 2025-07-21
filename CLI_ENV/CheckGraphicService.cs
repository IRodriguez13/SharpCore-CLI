using System;
using System.Runtime.InteropServices;
using ShpCore.Kernel.RemoteLinuxConnection; 
using ShpCore.Logging;
using System.Diagnostics;

namespace ShpCore.Kernel.VirtualMachineSubsystem;

public class GraphicsCheckService
{
    private readonly RemoteLinuxBridgeConnection _remote;

    public GraphicsCheckService(RemoteLinuxBridgeConnection remote)
    {
        _remote = remote;
    }

    public void RunGraphicsCheck()
    {
        KernelLog.Info("[GraphicsCheck] Iniciando validación de entorno gráfico...");

        if (!IsX11ServerRunning())
        {
            KernelLog.Warn("[GraphicsCheck] No se detectó un servidor X en el host.");
            KernelLog.Warn("[GraphicsCheck] Instalá y ejecutá XQuartz (Mac), VcXsrv (Windows) o asegurate de tener Xorg corriendo (Linux).");
            return;
        }

        KernelLog.Info("[GraphicsCheck] Servidor X detectado en el host.");

        string display = GetRecommendedDisplay();

        KernelLog.Info($"[GraphicsCheck] Usando DISPLAY={display} dentro de la VM...");

        // Ejecutamos xclock como test
        var setupDisplay = _remote.SendAndReceive($"export DISPLAY={display} && xclock");

        if (string.IsNullOrWhiteSpace(setupDisplay))
        {
            KernelLog.Warn("[GraphicsCheck] No se pudo ejecutar xclock. Puede que el DISPLAY no sea correcto o que el servidor X esté bloqueando conexiones.");
        }
        else
        {
            KernelLog.Info("[GraphicsCheck] xclock ejecutado exitosamente. El entorno gráfico funciona.");
        }
    }

    private bool IsX11ServerRunning()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Process.GetProcessesByName("vcxsrv").Length > 0;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Process.GetProcessesByName("XQuartz").Length > 0;
        }

        return false;
    }

    private string GetRecommendedDisplay()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ":0";
        }

        // host.docker.internal funciona bien en Mac y Windows
        return "host.docker.internal:0.0";
    }
}
