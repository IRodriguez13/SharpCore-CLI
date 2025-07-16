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

        if (!File.Exists(SharpCoreFM.InitStateFile))
        {
            KernelLog.Panic("[CLI] SharpCore no está inicializado. Ejecutá 'sharpcore init' primero.");
            return;
        }

        // Comando para inicializar la estructura base de SharpCore
        // Crea los directorios necesarios y el archivo de estado
        // Si ya está inicializado, muestra un mensaje de advertencia
        // Si no, crea la estructura y el archivo de estado
        // Ejemplo: sharpcore init
        // Si se inicializa correctamente, muestra un mensaje de éxito
        // Si hay un error al crear la estructura, muestra un mensaje de error
        Command initCommand = new("init", "Inicializa la estructura base de SharpCore");
        initCommand.SetHandler(() =>
        {

            SharpCoreFM.EnsureStructure();

            KernelLog.Info("✔ SharpCore inicializado correctamente. Estructura creada en: " + SharpCoreFM.Root);

            SharpCoreFM.Initialize();

            Console.ForegroundColor = ConsoleColor.Green;

            KernelLog.Info("Para mas información, ejecutá 'sharpcore --utils' o '--help'");

            Console.ResetColor();
            // Muestra el banner y la información del kernel ni bien se inicia el CLI en color blanco y amarillo
            Console.ForegroundColor = ConsoleColor.White;
            Welcome();
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Status();
            Console.ResetColor();
        });

        // Comando para reiniciar SharpCore CLI y borrar su configuración
        // Elimina el directorio raíz y todos sus contenidos
        // Si se usa --force, no pide confirmación
        // Si no se usa --force, muestra un mensaje de advertencia y no hace nada
        // Si se usa --force, borra el directorio raíz y muestra un mensaje de éxito
        // Si hay un error al borrar el directorio, muestra un mensaje de error
        // Si se borra correctamente, muestra un mensaje de éxito
        // Ejemplo: sharpcore reset --force
        // Si no se usa --force, muestra un mensaje de advertencia y no hace nada
        // Ejemplo: sharpcore reset
        Command resetCommand = new("reset", "Reinicia SharpCore CLI y borra su configuración");
        Option<bool> forceOption = new("--force", "Obliga el reinicio sin confirmación");
        resetCommand.AddOption(forceOption);

        resetCommand.SetHandler((bool force) =>
        {
            if (!force)
            {
                KernelLog.Warn("⚠ Este comando borra toda la configuración. Usá --force para confirmarlo.");
                return;
            }

            try
            {
                Directory.Delete(SharpCoreFM.Root, recursive: true);
                KernelLog.Info("✔ SharpCore CLI reseteado. Ejecutá `sharpcore init` para comenzar de nuevo.");
            }
            catch (Exception ex)
            {
                KernelLog.Panic("❌ No se pudo reiniciar SharpCore.", ex);
            }
        });

        RootCommand root = new("CLI oficial de SharpCore") { Name = "shpcore" }; SharpCoreFM.EnsureStructure(); // <- Se asegura que todo exista

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

        // Comando para consultar si hay una nueva versión del kernel disponible
        // Lee el archivo metadata/available.json y compara con las versiones instaladas
        // Si hay una nueva versión, muestra un mensaje de advertencia
        // Si no hay versiones disponibles, muestra un mensaje de advertencia
        // Si hay una versión instalada, muestra un mensaje de éxito
        // Si hay una versión instalada y es la última, muestra un mensaje de éxito
        // Si hay una versión instalada y no es la última, muestra un mensaje de advertencia
        // Si hay una versión instalada y es la última, muestra un mensaje de éxito
        Command kernelCheck = new("-kernel-check", "Consulta si hay una nueva versión disponible");

        kernelCheck.SetHandler(() =>
        {
            // Simula lectura desde metadata
            var localVersions = SharpCoreFM.GetInstalledVersions();
            var availableJson = File.ReadAllText(SharpCoreFM.VersionMetadataFile);
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


        // Comando para volver a la versión anterior del kernel (si existe)
        // Lee el archivo active.txt, obtiene la versión actual y busca la anterior en la lista
        Command kernelRollback = new("-kernel-rollback", "Vuelve a la versión anterior del kernel (si existe)");

        kernelRollback.SetHandler(() =>
        {
            string activePath = SharpCoreFM.ActiveKernelFile.Trim();
            string currentVersion = File.ReadAllText(activePath).Trim();
            var versionPath = Path.Combine(SharpCoreFM.KernelsDir, currentVersion);
            string[] installedVersions = SharpCoreFM.GetInstalledVersions();

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
            string kernelsDir = Path.Combine(SharpCoreFM.ActiveKernelDll, "kernels");
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

        // Comando para mostrar la ruta del archivo de logs del kernel
        // Muestra la ruta del archivo de logs del kernel
        // Si el archivo no existe, muestra un mensaje de advertencia
        // Si el archivo existe, muestra la ruta
        Command kernelLogPath = new("kernel-log-path", "Muestra la ubicación del archivo de logs del núcleo");
        kernelLogPath.SetHandler(() =>
        {
            string logPath = Path.Combine(SharpCoreFM.LogsDir, "kernel.log");
            KernelLog.Info($"Ruta actual de logs: {logPath}");
        });


        // Comando para listar los kernels instalados
        // Muestra los kernels instalados y cuál es el activo
        // Si no hay kernels instalados, muestra un mensaje de advertencia
        // Si hay kernels instalados, muestra la lista y cuál es el activo
        Command kernelList = new("kernel-list", "Lista los kernels instalados localmente");
        kernelList.SetHandler(() =>
        {
            string kernelsPath = Path.Combine(SharpCoreFM.ActiveKernelFile, "kernels");
            string active = File.ReadAllText(Path.Combine(kernelsPath, "active.txt")).Trim();

            foreach (var dir in Directory.GetDirectories(kernelsPath))
            {
                string version = new DirectoryInfo(dir).Name;
                string label = version == active ? " (active)" : "";
                KernelLog.Info($"✔ {version}{label}");
            }
        });


        // Comando para activar otra versión del kernel
        // Cambia la versión activa del kernel a la especificada
        // Si la versión no está instalada, muestra un mensaje de advertencia
        // Si la versión está instalada, cambia la versión activa y muestra un mensaje de éxito
        // Ejemplo: sharpcore kernel-switch --version X.Y.Z
        // Si la versión no está instalada, muestra un mensaje de advertencia
        // Si la versión está instalada, cambia la versión activa y muestra un mensaje de éxito
        Option<string> switchVersionOption = new("--version", "Versión a activar") { IsRequired = true };
        Command kernelSwitch = new("kernel-switch", "Activa otra versión del kernel");
        kernelSwitch.AddOption(switchVersionOption);
        kernelSwitch.SetHandler((string version) =>
        {
            string versionPath = Path.Combine(SharpCoreFM.KernelsDir, "kernels", version);
            if (!Directory.Exists(versionPath))
            {
                KernelLog.Warn($"La versión {version} no está instalada.");
                return;
            }

            File.WriteAllText(Path.Combine(SharpCoreFM.MetadataDir, "kernels", "active.txt"), version);
            KernelLog.Info($"✔ Versión activa cambiada a {version}");
        }, switchVersionOption);

        Command kernelUpdate = new("kernel-update", "Descarga la última versión del kernel");
        kernelUpdate.SetHandler(() =>
        {
            KernelLog.Info("✔ Simulando descarga desde remoto...");
            // Lógica real: descargar zip, extraer a ~/.sharpcore/kernels/vX.Y.Z/
        });

        // Comando para mostrar información del kernel. mi versión de neofetch
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

        // Comando de ayuda del CLI
        // Muestra la ayuda del CLI y los comandos disponibles
        // Si se usa --utils, muestra la ayuda de utilidades
        // Si se usa --help, muestra la ayuda completa del CLI
        // Ejemplo: sharpcore --utils o sharpcore --help
        // Si se usa --utils, muestra la ayuda de utilidades
        // Si se usa --help, muestra la ayuda completa del CLI
        // Si se usa --utils, muestra la ayuda de utilidades
        // Si se usa --help, muestra la ayuda completa del CLI
        // Si se usa --utils, muestra la ayuda de utilidades    
        Command HelpCommand = new("--utils", "Muestra la ayuda del CLI");
        HelpCommand.AddAlias("--u");
        HelpCommand.AddAlias("--U");

        HelpCommand.SetHandler(() =>
        {
            KernelLog.Info("[CLI] Uso de SharpCore CLI:");
            ShowUtils();
        });

        // Comando para ejecutar un payload contra el núcleo
        // Requiere --payload, --protocol y --adapter
        // Si se usa --dev, usa el kernel referenciado en lugar del compilado
        // Si no se usa --dev, usa el kernel compilado
        // Si se usa --dev, muestra un mensaje de depuración
        // Si no se usa --dev, ejecuta el kernel compilado
        // Ejemplo: sharpcore run --payload /path/to/payload.json --protocol namedpipe --adapter forge --dev true
        Command runCommand = new("run", "Ejecuta un payload contra el núcleo");
        runCommand.AddOption(protocolOption);
        runCommand.AddOption(adapterOption);
        runCommand.AddOption(devFlag);

        Option<string> payloadOption = new("--payload", "Ruta al archivo JSON con el payload") { IsRequired = true };
        runCommand.AddOption(payloadOption);

        runCommand.SetHandler((string payloadPath, string protocol, string adapterPath, bool devMode) =>
        {
            if (!SharpCoreFM.IsInitialized)
            {
                KernelLog.Panic("SharpCore no está inicializado. Ejecutá primero `sharpcore init`.");
                return;
            }

            if (!File.Exists(SharpCoreFM.ActiveKernelDll))
            {
                KernelLog.Panic("Dev, No se encontró el kernel activo. Ejecutá 'sharpcore kernel-update' o 'kernel-add-local' para registrar uno.");
                return;
            }


            if (!File.Exists(payloadPath))
            {
                KernelLog.Panic($"(PAYLOAD) El archivo {payloadPath} no existe.");
                return;
            }

            if (!Directory.Exists(adapterPath))
            {
                KernelLog.Panic($"(ADAPTER) La ruta del adaptador '{adapterPath}' no existe. Asegurate de clonar el adaptador correspondiente.");
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

                    // Requiere que el kernel esté referenciado en tiempo de dev
                    IKernelEntryPoint devKernel = new SharpCore.Kernel.Init.SharpCoreKernel();
                    devKernel.Run(payloadPath, protocol, adapterPath, true);

                #else

                    KernelLog.Panic("[DevMode] Dev, No se puede ejecutar en modo desarrollo sin tu kernel referenciado en el csproj.");
                    return;
                #endif

                }
                catch (Exception ex)
                {
                    KernelLog.Panic("[DevMode] Fallo crítico al ejecutar el kernel en modo desarrollo.", ex);
                }

            }
            else
            {
                try
                {
                    Boot_System.BootActiveKernel(SharpCoreFM.ActiveKernelDll, payloadPath, protocol, adapterPath);
                }
                catch (Exception ex)
                {

                    KernelLog.Panic("[Kernel Loader] Fallo crítico al cargar el kernel compilado.", ex);

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
        root.AddCommand(initCommand);
        root.AddCommand(resetCommand);


        await root.InvokeAsync(args);
    }

    // =========== Métodos de ayuda y utilidades ===========

    private static void ShowUtils()
    {
        Console.WriteLine(
        @"SharpCore CLI - Uso básico

        Comandos:
        --init                                     Inicializa la estructura base de SharpCore
        --run                                      Ejecuta un payload contra el núcleo
         reset --force                            Reinicia SharpCore CLI y borra su configuración
        --status                                   Muestra el estado actual del CLI y del núcleo
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

    // =========== Métodos de bienvenida y estado ===========

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

    private static void Status()
    {
        Console.WriteLine($"✔ Directorio raíz: {SharpCoreFM.Root}");
        Console.WriteLine($"✔ Kernels instalados: {string.Join(", ", SharpCoreFM.GetInstalledVersions())}");
        Console.WriteLine($"✔ Versión activa: {File.ReadAllText(SharpCoreFM.ActiveKernelFile).Trim()}");
    }

}


