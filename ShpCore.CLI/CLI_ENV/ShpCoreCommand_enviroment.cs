using System.CommandLine;
using ShpCore.Logging;
using System.Runtime.InteropServices;
using SharpCore.CLI.Env.FileManagement;
using System.Text.Json;
using Kernel.Boot.System;
#if DEV_Kernel
using SharpCore.Abstractions;
using SharpCore.Kernel.Init;
#endif


// USO DE EJEMPLO: sharpcore run --protocol namedpipe --adapter forge --payload ./mods/axel.json

namespace SharpCore.CLI.Env;

public static class SharpCoreCLI
{
    public static async Task Run(string[] args)
    {
        SharpCoreHome.EnsureStructure(); // <- Se asegura que todo exista

        RootCommand root = new("CLI oficial de SharpCore") { Name = "shpcore" }; SharpCoreHome.EnsureStructure(); // <- Se asegura que todo exista

        // Muestra el banner y la información del núcleo ni bien se inicia el CLI en color blanco y amarillo
        Console.ForegroundColor = ConsoleColor.White;
        Welcome();
        Console.ResetColor();


        Option<string> protocolOption = new Option<string>("--protocol", "Protocolo de transporte")
        {
            IsRequired = false
        }.FromAmong("namedpipe", "grpc", "unix");

        Option<string> adapterOption = new("--adapter", "Ruta al adaptador")
        {
            IsRequired = false
        };

        var devFlag = new Option<bool>("--dev", () => false, "Modo desarrollador (usa el kernel referenciado en lugar del compilado)")
        {
            IsRequired = false
        };


        Command kernelCheck = new("-kernel-check", "Consulta si hay una nueva versión disponible");

        kernelCheck.SetHandler(() =>
        {
            // Simula lectura desde metadata
            var localVersions = SharpCoreHome.GetInstalledVersions();
            var availableJson = File.ReadAllText(SharpCoreHome.VersionMetadataFile);
            var availableVersions = JsonSerializer.Deserialize<List<string>>(availableJson);

            if (availableVersions == null || availableVersions.Count == 0)
            {
                KernelLog.Warn("No hay versiones remotas disponibles registradas.");
                return;
            }

            var latest = availableVersions.OrderByDescending(v => v).FirstOrDefault();
            var installed = localVersions.Contains(latest);

            if (!installed)
            {
                KernelLog.Warn($"🔔 Hay una nueva versión disponible: {latest}");
                return;
            }

            KernelLog.Info($"✔ Ya tenés la última versión ({latest}) instalada.");
        });



        Command kernelRollback = new("-kernel-rollback", "Vuelve a la versión anterior del kernel (si existe)");

        kernelRollback.SetHandler(() =>
        {
            string activePath = SharpCoreHome.ActiveKernelFile.Trim();
            string currentVersion = File.ReadAllText(activePath).Trim();
            var versionPath = Path.Combine(SharpCoreHome.KernelsDir, currentVersion);
            string[] installedVersions = SharpCoreHome.GetInstalledVersions();

            var currentIndex = Array.IndexOf(installedVersions, currentVersion);
            if (currentIndex == -1 || currentIndex + 1 >= installedVersions.Length)
            {
                KernelLog.Warn("No hay versiones anteriores disponibles.");
                return;
            }
            string previous = installedVersions[currentIndex + 1];
            File.WriteAllText(activePath, previous);
            KernelLog.Info($"✔ Volviste a la versión anterior: {previous}");



        });

        Option<string> localPathOption = new("--path", "Ruta a un kernel local ya compilado") { IsRequired = true };
        Option<string> localVersionOption = new("--version", "Versión a registrar") { IsRequired = true };

        Command kernelAddLocal = new("kernel-add-local", "Registra un kernel compilado localmente");
        kernelAddLocal.AddOption(localPathOption);
        kernelAddLocal.AddOption(localVersionOption);

        kernelAddLocal.SetHandler((string path, string version) =>
        {
            string kernelsDir = Path.Combine(SharpCoreHome.ActiveKernelDll, "kernels");
            string targetDir = Path.Combine(kernelsDir, version);

            if (!Directory.Exists(path))
            {
                KernelLog.Warn($"❌ El path {path} no existe.");
                return;
            }

            if (Directory.Exists(targetDir))
            {
                KernelLog.Warn($"❌ Ya existe un kernel registrado como {version}.");
                return;
            }

            Directory.CreateDirectory(targetDir);
            File.Copy(Path.Combine(path, "sharpcore.kernel.dll"), Path.Combine(targetDir, "sharpcore.kernel.dll"));

            KernelLog.Info($"✔ Kernel local registrado como versión {version}.");
        }, localPathOption, localVersionOption);


        Command kernelLogPath = new("kernel-log-path", "Muestra la ubicación del archivo de logs del núcleo");
        kernelLogPath.SetHandler(() =>
        {
            string logPath = Path.Combine(SharpCoreHome.LogsDir, "kernel.log");
            KernelLog.Info($"Ruta actual de logs: {logPath}");
        });



        Command kernelList = new("kernel-list", "Lista los kernels instalados localmente");
        kernelList.SetHandler(() =>
        {
            string kernelsPath = Path.Combine(SharpCoreHome.ActiveKernelFile, "kernels");
            string active = File.ReadAllText(Path.Combine(kernelsPath, "active.txt")).Trim();

            foreach (var dir in Directory.GetDirectories(kernelsPath))
            {
                string version = new DirectoryInfo(dir).Name;
                string label = version == active ? " (active)" : "";
                KernelLog.Info($"✔ {version}{label}");
            }
        });


        Option<string> switchVersionOption = new("--version", "Versión a activar") { IsRequired = true };

        Command kernelSwitch = new("kernel-switch", "Activa otra versión del kernel");
        kernelSwitch.AddOption(switchVersionOption);
        kernelSwitch.SetHandler((string version) =>
        {
            string versionPath = Path.Combine(SharpCoreHome.KernelsDir, "kernels", version);
            if (!Directory.Exists(versionPath))
            {
                KernelLog.Warn($"La versión {version} no está instalada.");
                return;
            }

            File.WriteAllText(Path.Combine(SharpCoreHome.MetadataDir, "kernels", "active.txt"), version);
            KernelLog.Info($"✔ Versión activa cambiada a {version}");
        }, switchVersionOption);

        Command kernelUpdate = new("kernel-update", "Descarga la última versión del kernel");
        kernelUpdate.SetHandler(() =>
        {
            KernelLog.Info("✔ Simulando descarga desde remoto...");
            // Lógica real: descargar zip, extraer a ~/.sharpcore/kernels/vX.Y.Z/
        });


        Command neofetch = new("corefetch", "Muestra información del núcleo SharpCore");

        neofetch.SetHandler(() =>
        {
            CoreFecth();

            string version = File.ReadAllText("VERSION.txt").Trim();
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown";

            var arch = RuntimeInformation.OSArchitecture;
            string runtime = RuntimeInformation.FrameworkDescription;

            Console.ForegroundColor = ConsoleColor.Yellow;

            KernelLog.Info($"Kernel version: {version}");
            KernelLog.Info("Author: Iván Rodriguez (ivanr013) <ivanrwcm25@gmail.com>");
            KernelLog.Info($"Runtime: {os} {arch} / {runtime}");
            KernelLog.Info("GitHub: https://github.com/IRodriguez13/SharpCore_forge");
            KernelLog.Info("Adapter: No selected (use --adapter /path/to/adapter)");
            KernelLog.Info("ASCII font: Banner3 (logo), 3x5 (version)");
            KernelLog.Info("License: GPL-3.0");

            Console.ResetColor();
        });


        Command HelpCommand = new("--utils", "Muestra la ayuda del CLI");
        HelpCommand.AddAlias("--u");
        HelpCommand.AddAlias("--U");

        HelpCommand.SetHandler(() =>
        {
            KernelLog.Info("[CLI] Uso de SharpCore CLI:");
            ShowUtils();
        });

        Command runCommand = new("run", "Ejecuta un payload contra el núcleo");
        runCommand.AddOption(protocolOption);
        runCommand.AddOption(adapterOption);
        runCommand.AddOption(devFlag);

        Option<string> payloadOption = new("--payload", "Ruta al archivo JSON con el payload") { IsRequired = true };
        runCommand.AddOption(payloadOption);

        runCommand.SetHandler((string payloadPath, string protocol, string adapterPath, bool devMode) =>
        {
            if (!File.Exists(payloadPath))
            {
                KernelLog.Panic($"(PAYLOAD) El archivo {payloadPath} no existe.");
                return;
            }

            if (!Directory.Exists(adapterPath))
            {
                KernelLog.Panic($"(ADAPTER) La ruta del adaptador '{adapterPath}' no existe. Asegurate de clonar el adaptador.");
                return;
            }

            if (!File.Exists(payloadPath))
            {
                KernelLog.Panic($"[Kernel Loader] El payload no existe en la ruta: {payloadPath}");
                return;
            }

            if (!Directory.Exists(adapterPath))
            {
                KernelLog.Panic($"[Kernel Loader] La ruta del adaptador no existe: {adapterPath}");
                return;
            }

            if (devMode)
            {

                KernelLog.Debug("[CLI MODE] Modo de desarrollo activado. init con Kernel referenciado localmente.");


                try
                {

#if DEV_Kernel

                    // Requiere que el kernel esté referenciado en tiempo de desarrollo
                    IKernelEntryPoint devKernel = new SharpCore.Kernel.Init.SharpCoreKernel();
                    devKernel.Run(payloadPath, protocol, adapterPath, true);

#else

                    KernelLog.Panic("[DevMode] No se puede ejecutar en modo desarrollo sin el kernel referenciado.");

#endif

                }
                catch (Exception ex)
                {
                    KernelLog.Panic("[DevMode] Fallo al ejecutar el kernel en modo desarrollo.", ex);
                }

            }
            else
            {
                try
                {
                    Boot_System.BootActiveKernel(SharpCoreHome.ActiveKernelDll, payloadPath, protocol, adapterPath);
                }
                catch (Exception ex)
                {

                    KernelLog.Panic("[Kernel Loader] Fallo al cargar el kernel compilado.", ex);

                }
            }

        }, payloadOption, protocolOption, adapterOption, devFlag);


        // =========== Comandos del CLI registrados ===========

        root.AddCommand(runCommand);
        root.AddCommand(HelpCommand);
        root.AddCommand(neofetch);
        root.AddCommand(kernelList);
        root.AddCommand(kernelSwitch);
        root.AddCommand(kernelUpdate);
        root.AddCommand(kernelCheck);
        root.AddCommand(kernelRollback);
        root.AddCommand(kernelAddLocal);
        root.AddCommand(kernelLogPath);


        await root.InvokeAsync(args);
    }

    private static void ShowUtils()
    {
        Console.WriteLine(
        @"SharpCore CLI - Uso básico

        Comandos:
        --protocol    [namedpipe|grpc|unix]        Protocolo de transporte
        --adapter     [forge|gba|ps2]              Adaptador (consola/juego destino)
        --payload     path/to/payload.json         Ruta del archivo de instrucción
        --dev         [true|false]                 Modo desarrollador (usa el kernel referenciado en lugar del compilado)
        --utils       Muestra esta ayuda
        --kernel-check                              Consulta si hay una nueva versión del kernel disponible
        --kernel-rollback                           Vuelve a la versión anterior del kernel (si existe)
        --kernel-add-local path/to/kernel --version X.Y.Z  Registra un kernel compilado localmente
        --kernel-list                               Lista los kernels instalados localmente
        --kernel-switch --version X.Y.Z             Activa otra versión del kernel
        --kernel-update                             Descarga la última versión del kernel
        --kernel-log-path                           Muestra la ubicación del archivo de logs del núcleo
        --corefetch                                 Muestra información del núcleo SharpCore y del usuario
        --help                                      Muestra esta ayuda

        Ejemplo:
        sharpcore run --protocol namedpipe --adapter forge --payload ./mods/axel.json"
        );
    }

    private static void CoreFecth()
    {
        string banner = File.ReadAllText("Short_Banner.txt");
        Console.WriteLine(banner);
    }

    private static void Welcome()
    {
        string welcome = File.ReadAllText("Banner.txt");
        Console.WriteLine(welcome);
    }

}


