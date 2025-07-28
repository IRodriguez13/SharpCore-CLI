using System.CommandLine;
using ShpCore.Logging;
using System.Runtime.InteropServices;
using SharpCore.CLI.Env.FileManagement;
using System.Text.Json;
using Kernel.Boot.System;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using ShpCore.Kernel.VirtualMachineSubsystem;
using ShpCore.Kernel.RemoteLinuxConnection;
using SharpCore.CLI.Env.Helpers;

#if DEV_Kernel

using SharpCore.Kernel.Init;
using ShpCore.Launcher.Core.Factory;
using MSharp.Launcher.Core.Bridge;

#endif


// USO DE EJEMPLO: sharpcore run --protocol namedpipe --adapter forge --payload ./mods/axel.json
namespace SharpCore.CLI.Env;

public static class SharpCoreCLI
{
    public static async Task Run(string[] args)
    {
        // Si se inicializa correctamente, muestra un mensaje de éxito
        // Si hay un error al crear la estructura, muestra un mensaje de error
        Command initCommand = new("init", "Inicializa la estructura base de SharpCore");
        initCommand.AddAlias("--i");
        initCommand.AddAlias("--init");
        initCommand.SetHandler(() =>
        {

            SharpCoreFM.EnsureStructure();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[INFO] ✔ SharpCore inicializado correctamente. Estructura creada en: " + SharpCoreFM.Root);
            Console.ResetColor();


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
        Option<bool> forceOption = new(new[] { "--force", "-f" }, "Obliga el reinicio sin confirmación");
        resetCommand.AddOption(forceOption);
        resetCommand.SetHandler((bool force) =>
        {
            if (!force)
            {
                KernelLog.Warn("⚠ Este comando borra toda la configuración. Usá --force para confirmarlo.");
                return; // Esto ahora es válido porque está dentro de una lambda void
            }

            try
            {
                Directory.Delete(SharpCoreFM.Root, recursive: true);
                KernelLog.Info("✔ SharpCore CLI reseteado. Ejecutá `sharpcore init` para comenzar de nuevo.");
            }
            catch (Exception ex)
            {
                KernelLog.Panic("[RESET  commandenv line:91] No se pudo reiniciar SharpCore.", ex);
            }
        },
        forceOption); // Pasa el option como argumento al handler

        RootCommand root = new("CLI oficial de SharpCore") { Name = "shpcore" }; SharpCoreFM.EnsureStructure(); // <- Se asegura que todo exista

        Option<string> protocolOption = new Option<string>("--protocol", "Protocolo de transporte")
        {
            IsRequired = false
        }.FromAmong("namedpipe", "grpc", "unix", "remote-linux");

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
        neofetch.AddAlias("--cf");
        neofetch.AddAlias("--corefetch");
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

            Console.WriteLine($"Kernel version: {version}");
            Console.WriteLine("Author: Iván E. Rodriguez <ivanrwcm25@gmail.com>");
            Console.WriteLine($"Runtime: {os} {arch} / {runtime}");
            Console.WriteLine("GitHub: https://github.com/IRodriguez13/SharpCore-Kernel");
            Console.WriteLine("Adapter: No selected (use --adapter /path/to/adapter)");
            Console.WriteLine("ASCII font: Banner3 (logo), 3x5 (version)");
            Console.WriteLine("License: GPL-3.0");

            Console.ResetColor();
        });
        #region Comandos de la máquina virtual Linux

        var kernelCommand = new Command("kernel", "Acciones sobre kernels SharpCore");
        var remoteCommand = new Command("remote", "Habla con kernels Linux remotos por red");
        var vmCommand = new Command("vm", "Corre una máquina virtual y ejecuta comandos");

        kernelCommand.AddCommand(BuildKernelRunCommand());
        remoteCommand.AddCommand(BuildRemoteRunCommand());
        vmCommand.AddCommand(BuildVmStartCommand());

        Command vmInit = new("vm-init", "Inicializa una VM con una imagen disponible");
        Option<string> imageOption = new("--image", "Nombre de la imagen (debian-lite, fedora-core, etc)") { IsRequired = true };
        Option<string> qemuJsonOption = new("--qemu-options", "Ruta al archivo JSON con opciones de QEMU") { IsRequired = false };

        vmInit.AddOption(imageOption);
        vmInit.AddOption(qemuJsonOption);

        #region VM Init Handler 
        vmInit.SetHandler((string image, string qemuOptionsPath) =>
        {
            try
            {
                string imagesDir = Path.Combine(SharpCoreFM.GetVmImagesPath(), $"{image}.qcow2");

                if (!File.Exists(imagesDir))
                {
                    KernelLog.Panic($"[--IMAGE  commandenv line:319] No se encontró la imagen '{image}'. Buscada en: {imagesDir}");
                    return;
                }


                var options = QemuOptionsLoader.Load(qemuOptionsPath);
                options.ImagePath = imagesDir;

                var vm = new QemuBridgeConnection(options);
                vm.Start();

                KernelLog.Info($"[VM] {image} corriendo en {options.Port}");
            }
            catch (Exception ex)
            {
                KernelLog.Panic($"[VM INIT commandenv line:334] Falló el arranque de la VM", ex);
            }

        }, imageOption, qemuJsonOption);

        #endregion

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
        #region Run Command Handler
        Command runCommand = new("run", "Ejecuta un payload contra el núcleo");
        runCommand.AddOption(protocolOption);
        runCommand.AddOption(adapterOption);
        runCommand.AddOption(devFlag);
        Option<bool> vmFlag = new("--vm", "Usa una máquina virtual local como backend");
        Option<string> qemuOptionsPath = new("--qemu-options", "Ruta al archivo JSON con opciones de QEMU");
        Option<string> imagePathOption = new("--image-path", "Ruta a la imagen QCOW2");
        Option<string> commandOption = new("--cmd", "Comando directo para ejecución remota (en vez de pasar JSON)");
        Option<string> payloadOption = new("--payload", "Ruta al archivo JSON con el payload") { IsRequired = false };
        runCommand.AddOption(payloadOption);

        runCommand.SetHandler((string payloadPath, string protocol, string adapterPath, bool devMode, string commandInput, string imagePath, string qemuOptions, bool vmFlag) =>
        {

            bool usesFileBasedAdapter = protocol is "grpc" or "namedpipe" or "unix";

            if (vmFlag && protocol != "vm")
            {
                KernelLog.Panic("[--vm commandenv line:387] Flag activa pero protocolo no es 'vm'. Usá --protocol vm");
                return;
            }


            if (!SharpCoreFM.IsInitialized)
            {
                KernelLog.Panic("[--INIT commandenv line:394] SharpCore no está inicializado. Ejecutá primero `sharpcore init`.");
                return;
            }


            if (usesFileBasedAdapter && !File.Exists(payloadPath))
            {
                KernelLog.Panic($"[PAYLOAD commandenv line:401] El archivo {payloadPath} no existe.");
                return;
            }

            if (usesFileBasedAdapter && !Directory.Exists(adapterPath))
            {
                KernelLog.Panic($"[ADAPTER commandenv line:407] La ruta del adaptador '{adapterPath}' no existe. Asegurate de clonar el adaptador correspondiente.");
                return;
            }

            if (usesFileBasedAdapter && !File.Exists(payloadPath))
            {
                KernelLog.Panic($"[Kernel Loader commandenv line:413] El payload no existe en la ruta: {payloadPath}");
                return;
            }

            if (usesFileBasedAdapter && !Directory.Exists(adapterPath))
            {
                KernelLog.Panic($"[Kernel Loader commandenv line:419] La ruta del adaptador no existe: {adapterPath}");
                return;
            }

            if (!string.IsNullOrEmpty(commandInput) && !devMode)
            {
                KernelLog.Panic("[CLI: commandenv line:425]Comando directo (--cmd) solo puede utilizarse en modo desarrollador (--dev).");
                return;
            }

            if (string.IsNullOrEmpty(commandInput) && string.IsNullOrEmpty(payloadPath))
            {
                KernelLog.Panic("[ commandenv line:431] Debés pasar un payload (--payload) o un comando (--cmd).");
                return;
            }


            if (vmFlag)
            {
                try
                {
                    if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(qemuOptions))
                    {
                        KernelLog.Panic("[--vm commandenv line:442] Faltan opciones: asegurate de pasar --image-path y --qemu-options con un JSON válido.");
                        return;
                    }

                    string optionsJson = File.ReadAllText(qemuOptions);
                    var options = JsonSerializer.Deserialize<QemuOptions>(optionsJson);
                    options!.ImagePath = imagePath; // lo forzamos por CLI, opcional

                    var qemu = new QemuBridgeConnection(options);
                    qemu.Start();

                    if (!string.IsNullOrEmpty(commandInput)) qemu.Send(commandInput);

                    return;
                }
                catch (Exception ex)
                {
                    KernelLog.Panic("[QEMU commandenv line:459] Fallo al levantar la VM o ejecutar comando.", ex);
                    return;
                }
            }

            if (devMode)
            {

                KernelLog.Debug("[CLI MODE commandenv line:467] Modo de desarrollo activado. init con Kernel referenciado localmente.");
                try
                {

#if DEV_Kernel
                    // =========== Modo desarrollo: Ejecuta el kernel referenciado en el proyecto ===========
                    if (!string.IsNullOrEmpty(commandInput))
                    {
                        var json = SharpCoreFM.GenerateCommandPayload(commandInput);
                        var bridge = ProtocolFactory.Get(protocol).CreateBridge(adapterPath);
                        bridge.Start();
                        bridge.Send(json);
                        return;
                    }

                    // Requiere que el kernel esté referenciado en tiempo de dev

                    var devKernel = new SharpCoreKernel();
                    devKernel.Run(payloadPath, protocol, adapterPath, true);

#else

                    KernelLog.Panic("[DevMode commandenv line:489] Dev, No se puede ejecutar en modo desarrollo sin tu kernel referenciado en el csproj.");
                    return;
#endif

                }
                catch (Exception ex)
                {
                    KernelLog.Panic("[DevMode commandenv line:496] Fallo crítico al ejecutar el kernel en modo desarrollo.", ex);
                }

            }
            else // =========  FALLBACK al kernel compilado por defecto en el release del entorno de consola  ===========
            {

                if (!File.Exists(SharpCoreFM.ActiveKernelDll))
                {
                    KernelLog.Panic("[commandenv line:505]Dev, No se encontró el kernel activo. Ejecutá 'sharpcore kernel-update' o 'kernel-add-local' para registrar uno.");
                    return;
                }

                KernelLog.Info("[CLI MODE] Modo de producción activado. Ejecutando el kernel compilado.");

                // Si se pasa un comando directo, lo convertimos a JSON y lo guardamos en un archivo temporal
                // para que el kernel compilado lo pueda leer.
                // Si no se pasa un comando, se usa el payload como está.
                if (!string.IsNullOrEmpty(commandInput))
                {
                    var jsonPayload = SharpCoreFM.GenerateCommandPayload(commandInput);

                    string tempJsonPath = Path.Combine(Path.GetTempPath(), $"sharpcore_cmd_{Guid.NewGuid()}.json");
                    File.WriteAllText(tempJsonPath, jsonPayload);

                    // Overwrite del payloadPath para que el kernel compilado lo lea desde ahí
                    payloadPath = tempJsonPath;
                }

                try
                {
                    Boot_System.BootActiveKernel(SharpCoreFM.ActiveKernelDll, payloadPath, protocol, adapterPath);
                }
                catch (Exception ex)
                {
                    KernelLog.Panic("[Kernel Loader commandenv line:531] Fallo crítico al cargar el kernel compilado.", ex);
                }
            }

        }, payloadOption, protocolOption, adapterOption, devFlag, commandOption, imagePathOption, qemuOptionsPath, vmFlag);
        #endregion

        #region Comandos de CLI registrados
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
        root.AddCommand(vmInit);
        root.AddCommand(kernelCommand);
        root.AddCommand(remoteCommand);
        root.AddCommand(vmCommand);
        root.AddCommand(BuildDockerStartCommand());
        root.AddCommand(CheckKernelStatus());
        runCommand.AddOption(imagePathOption);
        runCommand.AddOption(qemuOptionsPath);
        runCommand.AddOption(vmFlag);
        root.AddGlobalOption(protocolOption);
        root.AddGlobalOption(adapterOption);
        root.AddGlobalOption(devFlag);
        root.AddGlobalOption(commandOption);

        await root.InvokeAsync(args);
    }
    #endregion
    // =========== Métodos de ayuda y utilidades ===========

    private static void ShowUtils()
    {
        Console.WriteLine(
        @"SharpCore CLI - Uso básico

        Comandos:
        --init                                     Inicializa la estructura base de SharpCore
        --run                                      Ejecuta un payload contra el núcleo
        reset --force                             Reinicia SharpCore CLI y borra su configuración
        --cmd                                     Comando directo para ejecución remota a Linux(en vez de pasar JSON)
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

        ==== Comandos Linux ====

        sharpcore vm init --image debian-lite --qemu-options ./qemu.json
        sharpcore vm cmd

        Ejemplo:
        sharpcore run --protocol namedpipe --adapter forge --payload ./mods/axel.json"
        );
    }

    #endregion

    #region Métodos de bienvenida y estado
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

        string installedKernels = string.Join(", ", SharpCoreFM.GetInstalledVersions());
        if (installedKernels == "")
        {
            Console.WriteLine("✔ Kernels SharpCore instalados: No se detectaron instalaciones compiladas. Compilá el tuyo en desde el repositorio remoto o referenciá uno en ShpCore.CLI.csproj.");
        }
        else
        {
            Console.WriteLine($"✔ Kernels instalados: {string.Join(", ", SharpCoreFM.GetInstalledVersions())}");

        }

        Console.WriteLine($"✔ Versión activa: {File.ReadAllText(SharpCoreFM.ActiveKernelFile).Trim()}");
    }

    #endregion

    #region Comandos de ejecución remota y máquina virtual
    public static Command BuildRemoteRunCommand()
    {
        var cmd = new Command("run", "Ejecuta un comando contra un kernel Linux remoto");

        var commandOption = new Option<string>("--cmd", "Comando a ejecutar (ej: 'ls -la')") { IsRequired = true };
        var adapterOption = new Option<string>("--adapter", "URL del adaptador HTTP remoto") { IsRequired = true };

        cmd.AddOption(commandOption);
        cmd.AddOption(adapterOption);

        cmd.SetHandler((string commandInput, string adapterUrl) =>
        {
            var json = JsonSerializer.Serialize(new { command = commandInput });

            var bridge = new RemoteLinuxBridgeConnection(adapterUrl);
            bridge.Start();
            bridge.Send(json);

        }, commandOption, adapterOption);

        return cmd;
    }

    public static Command BuildKernelRunCommand()
    {
        var cmd = new Command("kernel-run", "Ejecuta un payload usando el kernel local de SharpCore");

        var payloadOption = new Option<string>("--payload", "Ruta al payload JSON") { IsRequired = true };
        var protocolOption = new Option<string>("--protocol", "Protocolo a usar (ej: namedpipe, remote-linux, vm)") { IsRequired = true };
        var adapterOption = new Option<string>("--adapter", "Ruta o URL del adaptador correspondiente") { IsRequired = true };

        cmd.AddOption(payloadOption);
        cmd.AddOption(protocolOption);
        cmd.AddOption(adapterOption);

        cmd.SetHandler((string payloadPath, string protocol, string adapterPath) =>
        {
            if (!File.Exists(payloadPath))
            {
                KernelLog.Panic($"[kernel-run] Payload no encontrado en {payloadPath}");
                return;
            }

            var kernel = new SharpCoreKernel();
            kernel.Run(payloadPath, protocol, adapterPath, true);
        }, payloadOption, protocolOption, adapterOption);

        return cmd;
    }


    public static Command BuildVmStartCommand()
    {
        var cmd = new Command("vm-start", "Inicia una máquina virtual SharpCore con shell interactiva");

        var imageOption = new Option<string>("--image", "Ruta a la imagen .qcow2") { IsRequired = true };
        var qemuOptionsPath = new Option<string>("--qemu-options", "Ruta al JSON con opciones QEMU") { IsRequired = true };

        cmd.AddOption(imageOption);
        cmd.AddOption(qemuOptionsPath);

        cmd.SetHandler((string imagePath, string? optsPath) =>
        {
            try
            {
                #nullable disable
                var opts = QemuOptionsLoader.Load(optsPath); // loads qemu args from qemu-options-default.json. ignore the null cause Load() prevents this
                #nullable enable

                if (string.IsNullOrEmpty(optsPath))
                {
                    KernelLog.Panic("[vm-boot]: Dev, el json no contiene datos de config para QEMU (Commandenv, line 711)");
                    return;
                }

                KernelLog.Info("🧾 Opciones QEMU cargadas:");
                KernelLog.Info(JsonSerializer.Serialize(opts, new JsonSerializerOptions { WriteIndented = true }));

                opts.ImagePath = imagePath;

                if (opts.MemoryMb <= 0)
                {
                    KernelLog.Panic("[Json qemu-options commandenv line:723]: MemoryMb no puede ser menor o igual a 0");
                    return;
                }
                if (opts.Port < 0 || opts.Port > 65535)
                {
                    KernelLog.Panic("[Json qemu-options commandenv line:728]: Puerto inválido");
                    return;
                }
                if (string.IsNullOrWhiteSpace(opts.SharedFolder))
                {
                    KernelLog.Panic("[Json qemu-options commandenv line:733]: Falta SharedFolder");
                    return;
                }

                if (!File.Exists(opts.ImagePath))
                {
                    KernelLog.Panic("[vm-Boot commandenv line:739]: Dev, tenés pasar una imagen válida con --image");
                    return;
                }

                if (!opts.UseNographic) KernelLog.Info("[vm-Boot commandenv line:743] Iniciando VM en modo gráfico (no-nographic)\n");
                Console.WriteLine("\n======================================================================================================================================\n");
                CoreFecth();
                Console.WriteLine("\n======================================================================================================================================\n");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✔️ Verificando imagen");
                Console.WriteLine("✔️ Creando carpeta compartida");
                Console.WriteLine("✔️ Montando sistema de archivos");
                Console.WriteLine("✔️ Iniciando microkernel");
                Console.WriteLine("✔️ Iniciando Kernel Linux");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nIniciando SharpCore Subsystem for Linux 🌌\n");
                Console.ResetColor();



                var qemu = new QemuBridgeConnection(opts);
                qemu.Start();

            }
            catch (Exception ex)
            {
                KernelLog.Panic($"[vm-Boot commandenv line:767] Falló al iniciar VM: {ex}");
            }

        }, imageOption, qemuOptionsPath);

        return cmd;
    }

    #region Métodos de health check y utilidades
    public static Command CheckKernelStatus()
    {
        var cmd = new Command("--doctor", "Verifica el health check del núcleo y su entorno para portabilidad y soporte gráfico");
        cmd.AddAlias("--check");
        cmd.AddAlias("--health");
        cmd.AddAlias("--doc");

        cmd.SetHandler(() =>
        {
            var options = QemuOptionsLoader.Load();
            var doctor = new EnvironmentDoctor(options);
            doctor.RunFullCheck();
        });

        return cmd;
    }
    #endregion

    public static Command CheckGraphicSupport()
    {
        var cmd = new Command("check-graphic", "Verifica si el sistema soporta gráficos");
        var portOption = new Option<int>("--port", "Puerto donde escucha el bridge (default: 5000)") { IsRequired = true };
        cmd.SetHandler(() =>
        {
            var remote = new RemoteLinuxBridgeConnection($"http://127.0.0.1:{portOption}/exec");
            var checker = new GraphicsCheckService(remote);
            checker.RunGraphicsCheck();
        });

        return cmd;
    }



    public static Command BuildDockerStartCommand()
    {
        var cmd = new Command("docker-start", "Inicia el servicio Docker dentro de la VM");

        var portOpt = new Option<int>("--port", "Puerto donde escucha el bridge (default: 5000)") { IsRequired = false };
        portOpt.SetDefaultValue(5000);

        cmd.AddOption(portOpt);

        cmd.SetHandler((int port) =>
        {
            try
            {
                var bridge = new RemoteLinuxBridgeConnection($"http://127.0.0.1:{port}/exec");
                var json = JsonSerializer.Serialize(new { command = "service docker start" });

                bridge.Start();
                bridge.Send(json);

                KernelLog.Info($"[docker-start] Docker iniciado en la VM.");
            }
            catch (Exception ex)
            {
                KernelLog.Panic("[docker-start commandenv line:833] No se pudo iniciar Docker en la VM", ex);
            }

        }, portOpt);

        return cmd;
    }
    #endregion


}


