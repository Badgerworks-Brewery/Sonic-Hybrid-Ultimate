using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SonicHybridUltimate.Engines
{
    /// <summary>
    /// Handles native library resolution for all game engines (RSDKv4, OxygenEngine).
    /// Only one resolver can be registered per assembly, so this unified resolver handles all.
    /// </summary>
    public static class NativeLibraryResolver
    {
        private static bool _isRegistered = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Registers the native library resolver for the assembly.
        /// Call this once at application startup before using any engine.
        /// </summary>
        public static void Register()
        {
            lock (_lock)
            {
                if (_isRegistered)
                    return;

                NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, DllImportResolver);
                _isRegistered = true;
            }
        }

        /// <summary>
        /// Gets the DLL file names for a given library name.
        /// Returns empty array if the library name is not recognized.
        /// </summary>
        private static string[] GetDllNames(string libraryName)
        {
            if (libraryName == "RSDKv4")
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { "RSDKv4.dll" }
                    : new[] { "libRSDKv4.so", "RSDKv4.so" };
            }

            if (libraryName == "OxygenEngine")
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { "OxygenEngine.dll" }
                    : new[] { "libOxygenEngine.so", "OxygenEngine.so" };
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Gets the search paths for native libraries.
        /// </summary>
        private static string[] GetSearchPaths()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            return new[]
            {
                // Same directory as the executable
                exeDir,
                // Current directory
                Directory.GetCurrentDirectory(),
                // Hybrid-RSDK-Main build output (relative to exe)
                Path.Combine(exeDir, "..", "Hybrid-RSDK-Main", "build", "lib"),
                Path.Combine(exeDir, "..", "Hybrid-RSDK-Main", "build", "bin"),
                Path.Combine(exeDir, "..", "Hybrid-RSDK-Main", "build", "bin", "Release"),
            };
        }

        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            var dllNames = GetDllNames(libraryName);
            if (dllNames.Length == 0)
            {
                // Let other libraries be resolved by the default resolver
                return IntPtr.Zero;
            }

            return TryLoadLibrary(libraryName, dllNames);
        }

        private static IntPtr TryLoadLibrary(string libraryName, string[] dllNames)
        {
            var searchPaths = GetSearchPaths();

            foreach (var basePath in searchPaths)
            {
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                    continue;

                foreach (var dllName in dllNames)
                {
                    var fullPath = Path.Combine(basePath, dllName);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var handle = NativeLibrary.Load(fullPath);
                            Console.WriteLine($"Successfully loaded {libraryName} from: {fullPath}");
                            return handle;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load {fullPath}: {ex.Message}");
                        }
                    }
                }
            }

            Console.WriteLine($"Could not find {libraryName} library in any of the search paths:");
            foreach (var path in searchPaths)
            {
                Console.WriteLine($"  - {path}");
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Checks if a native library exists in the search paths.
        /// </summary>
        public static bool IsLibraryAvailable(string libraryName)
        {
            var dllNames = GetDllNames(libraryName);
            if (dllNames.Length == 0)
            {
                return false;
            }

            var searchPaths = GetSearchPaths();

            foreach (var basePath in searchPaths)
            {
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                    continue;

                foreach (var dllName in dllNames)
                {
                    var fullPath = Path.Combine(basePath, dllName);
                    if (File.Exists(fullPath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
