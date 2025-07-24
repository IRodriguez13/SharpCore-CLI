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
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n------------------------------------");

        Console.WriteLine("SSL Doctor Diagnostics Tool");
        Console.WriteLine("------------------------------------");
        Console.ResetColor();

        CheckOS();
        CheckDisplay();
        CheckMount();
        CheckXClock().Wait(); // yep, the clocx process normally async

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n Diagnóstico finalizado.\n");
        Console.ResetColor();
    }

    private void CheckOS()
    {
        KernelLog.Info("[QEMU] Verificando sistema operativo...");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            KernelLog.Info("- Sugerido: instalar VcXsrv → https://sourceforge.net/projects/vcxsrv/");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            KernelLog.Info("[QEMU] Entorno macOS detectado, no se requiere instalación adicional de X11.");
            KernelLog.Info("- Sugerido: instalar XQuartz → https://www.xquartz.org/");
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


            if (result?.mounted == true)
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

    private async Task CheckXClock()
    {
        KernelLog.Info("[QEMU] Verificando si la GUI funciona correctamente...");

        var cmd = "xclock -update 1"; // sin `&`, así lo podemos trackear

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
            if (process == null)
            {
                KernelLog.Panic("[QEMU doc-helper line:119] No se pudo iniciar el proceso xclock.");
                return;
            }

            await Task.Delay(1200); // esperamos a que levante la GUI

            if (!process.HasExited)
            {
                KernelLog.Info("[QEMU doc-helper] GUI lanzada correctamente (xclock)");
                process.Kill();
                KernelLog.Info("[QEMU doc-helper] xclock finalizado correctamente.");
            }
            else
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                KernelLog.Warn($"[QEMU doc-helper] xclock terminó inesperadamente. ExitCode: {process.ExitCode}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    KernelLog.Warn($"[QEMU doc-helper] Error de xclock: {stderr}");
            }
        }
        catch (Exception ex)
        {
            KernelLog.Panic($"[QEMU doc-helper line:141] Error al ejecutar xclock: {ex.Message}");
        }
    }

}


public class MountCheckResult
{
    public bool mounted { get; set; } // el endpoint devuelve literalmente mounted con minusculas.
    public string? Output { get; set; }
    public string? Error { get; set; }
}
