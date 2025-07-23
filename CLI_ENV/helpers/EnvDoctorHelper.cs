using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ShpCore.Kernel.VirtualMachineSubsystem;
using ShpCore.Kernel.RemoteLinuxConnection;
using ShpCore.Logging;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;

namespace SharpCore.CLI.Env.Helpers;

public class EnvironmentDoctor
{
    private readonly QemuOptions _options;

    public EnvironmentDoctor(QemuOptions options)
    {
        _options = options;
    }

    public void RunFullCheck()
    {
        Console.WriteLine("🩺 SharpCore Doctor - Diagnostic Tool");
        Console.WriteLine("------------------------------------");

        CheckOS();
        CheckDisplay();
        CheckMount();
        CheckXClock();

        Console.WriteLine("\n✅ Diagnóstico finalizado.");
    }

    private void CheckOS()
    {
        KernelLog.Info("[QEMU] Verificando sistema operativo...");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            KernelLog.Info("👉 Sugerido: instalar VcXsrv → https://sourceforge.net/projects/vcxsrv/");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            KernelLog.Info("[QEMU] Entorno macOS detectado, no se requiere instalación adicional de X11.");
            KernelLog.Info("👉 Sugerido: instalar XQuartz → https://www.xquartz.org/");
            KernelLog.Info("Nota: Asegúrate de que XQuartz esté configurado para permitir conexiones de red.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            KernelLog.Info("[QEMU] Entorno Linux detectado, no se requiere instalación adicional de X11.");
        }
    }

    private void CheckDisplay()
    {
        var displayEnv = Environment.GetEnvironmentVariable("DISPLAY") ?? "(no seteado)";
        KernelLog.Info($"[QEMU] Variable de entorno DISPLAY: {displayEnv}");
    }

    private void CheckMount()
    {
        KernelLog.Info("[QEMU] Verificando montaje de carpeta compartida...");

        try
        {
            var client = new HttpClient();
            var response = client.GetAsync($"http://127.0.0.1:{_options.Port}/mountcheck").Result;

            if (!response.IsSuccessStatusCode)
            {
                KernelLog.Warn($"[MountCheck] Falló el chequeo HTTP: {response.StatusCode}");
                return;
            }

            var resultJson = response.Content.ReadAsStringAsync().Result;
            var result = JsonSerializer.Deserialize<MountCheckResult>(resultJson);

            if (result?.Mounted == true)
            {
                KernelLog.Info("[MountCheck] Montaje de carpeta hostshare OK.");
            }
            else
            {
                KernelLog.Warn("[MountCheck] Montaje no detectado en /mnt/hostshare.");
                if (!string.IsNullOrEmpty(result?.Error))
                    KernelLog.Panic($"[MountCheck doc-helper line:86] Error del lado de la VM: {result?.Error}");
            }
        }
        catch (Exception ex)
        {
            KernelLog.Panic("[MountCheck doc-helper line:91] Excepción: " + ex.Message);
            KernelLog.Panic("[MountCheck doc-helper line:92] También puede ser que nunca hayas iniciado la VM");
        }
    }

    private async void CheckXClock()
    {
        KernelLog.Info("[QEMU] Verificando si la GUI funciona correctamente...");

        var cmd = "xclock";
        var startInfo = new ProcessStartInfo
        {
            FileName = "sh",
            Arguments = $"-c \"{cmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        try
        {
            var process = Process.Start(startInfo);
            await Task.Delay(3000);

            if (process == null)
            {
                KernelLog.Panic("[QEMU doc-helper line:117] No se pudo iniciar el proceso xclock.");
                return;
            }

            if (!process.HasExited)
            {
                KernelLog.Info("[Doctor] GUI lanzada correctamente (xclock)");
                process.Kill();
                KernelLog.Info("[Doctor] xclock finalizado correctamente.");
            }
            else
            {
                KernelLog.Warn("[Doctor] xclock terminó inesperadamente.");
            }
        }

        catch (Exception ex)
        {
            KernelLog.Panic($"[QEMU doc-helper line:135] Error al ejecutar xclock: {ex.Message}");
        }
    }
}


public class MountCheckResult
{
    public bool Mounted { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}
