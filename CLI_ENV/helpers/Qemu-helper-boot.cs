using System;
using System.IO;
using System.Text.Json;
using ShpCore.Logging;
using ShpCore.Kernel.VirtualMachineSubsystem;


namespace SharpCore.CLI.Env.Helpers;

public static class QemuOptionsLoader
{
    public static QemuOptions Load(string? path = null)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            KernelLog.Info($"[QEMU] Cargando opciones desde {path}");
            return JsonSerializer.Deserialize<QemuOptions>(File.ReadAllText(path)) ?? new QemuOptions();
        }

        // Ruta default
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string defaultPath = Path.Combine(home, ".sharpcore", "config", "qemu-options-default.json");

        if (File.Exists(defaultPath))
        {
            KernelLog.Info($"[QEMU] Usando configuración default desde {defaultPath}");
            return JsonSerializer.Deserialize<QemuOptions>(File.ReadAllText(defaultPath)) ?? new QemuOptions();
        }

        KernelLog.Info("[QEMU] Usando configuración embebida por defecto");
        return new QemuOptions(); // fallback hardcoded
    }
}
