using System;
using System.IO;
using System.Text.Json;
using ShpCore.Logging;
using ShpCore.Kernel.VirtualMachineSubsystem;


namespace SharpCore.CLI.Env.Helpers;

public static class QemuOptionsLoader
{
    public static QemuOptions Load(string path = "qemu-options-default.json")
    {
        if (path is null)
        {
            KernelLog.Panic("[QEMU QemuHelperBoot line:16] Path cannot be NULL");

            throw new FileNotFoundException("El archivo de configuración QEMU no fue encontrado", path);
        }
        
        if (!File.Exists(path)) throw new FileNotFoundException("El archivo de configuración QEMU no fue encontrado", path);

        string json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<QemuOptions>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Exception("Error deserializando opciones de QEMU");
    }
}

