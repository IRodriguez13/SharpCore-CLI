using ShpCore.Logging;
using SharpCore.CLI.Env.FileManagement;
using System.Reflection;

namespace Kernel.Boot.System;

public static class Boot_System
{
    public static void BootActiveKernel(string kernelPath, string payload, string protocol, string adapter)
    {


        if (!File.Exists(kernelPath))
        {
            KernelLog.Panic($"[CLI] El kernel activo no se encuentra en la ruta esperada: {kernelPath}");
            return;
        }
        
        try
        {

            KernelLog.Info($"[CLI MODE] Modo producción activado: Ejecutando kernel compilado desde: {kernelPath}");

            if (!File.Exists(kernelPath))
            {
                KernelLog.Panic($"[Kernel Loader] No se encontró el kernel activo en: {kernelPath}");
                return;
            }

            var asm = Assembly.LoadFrom(kernelPath);

            var type = asm.GetType("SharpCore.Kernel.Init.SharpCoreKernel");
            var method = type?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            if (method == null)
            {
                KernelLog.Panic("[Kernel Loader] No se encontró el método 'Run' dentro del kernel.");
                return;
            }

            if (type == null)
            {
                KernelLog.Panic("[Kernel Loader] No se pudo crear una instancia del kernel.");
                return;
            }

            var instance = Activator.CreateInstance(type);
            method.Invoke(null, new object[] { payload, protocol, adapter});
        }
        catch (Exception ex)
        {
            KernelLog.Panic("[Kernel Loader] Fallo al cargar el kernel compilado.", ex);
        }
    }
}