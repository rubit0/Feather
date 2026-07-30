using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Feather.Editor
{
    /// <summary>UPM package that can be opted into Feather JS AllowClr + type generation.</summary>
    public readonly struct DiscoverableApiPackage
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Version;
        public readonly string[] AssemblyNames;

        public DiscoverableApiPackage(string id, string displayName, string version, string[] assemblyNames)
        {
            Id = id;
            DisplayName = displayName;
            Version = version;
            AssemblyNames = assemblyNames ?? Array.Empty<string>();
        }

        public string Label => string.IsNullOrEmpty(DisplayName) || DisplayName == Id
            ? Id
            : $"{DisplayName} ({Id})";
    }

    public static class ApiPackageDiscovery
    {
        private static readonly HashSet<string> ExcludedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "com.unity.test-framework",
            "com.unity.ext.nunit",
            "com.unity.collab-proxy",
            "com.unity.ide.rider",
            "com.unity.ide.visualstudio",
            "com.unity.ide.vscode",
            "com.unity.multiplayer.center",
            "com.coplaydev.unity-mcp",
        };

        private static readonly string[] ExcludedPackagePrefixes =
        {
            "com.unity.modules.",
            "com.unity.ide.",
        };

        /// <summary>Assemblies Feather always exposes — not listed as optional packages.</summary>
        public static HashSet<string> GetCoreAssemblyNames()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in UnityApiSurface.GetCoreAssemblies())
                set.Add(asm.GetName().Name);
            return set;
        }

        public static bool IsExcludedAssemblyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("TestRunner", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("InternalAPI", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static DiscoverableApiPackage[] Discover()
        {
            var core = GetCoreAssemblyNames();
            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var result = new List<DiscoverableApiPackage>();

            foreach (var package in PackageInfo.GetAllRegisteredPackages().OrderBy(p => p.displayName))
            {
                if (IsExcludedPackage(package.name))
                    continue;

                var assemblies = playerAssemblies
                    .Where(a => a.sourceFiles != null && a.sourceFiles.Any(f => IsUnderPackage(f, package)))
                    .Select(a => a.name)
                    .Where(n => !core.Contains(n) && !IsExcludedAssemblyName(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToArray();

                if (assemblies.Length == 0)
                    continue;

                result.Add(new DiscoverableApiPackage(
                    package.name,
                    package.displayName,
                    package.version,
                    assemblies));
            }

            return result.ToArray();
        }

        /// <summary>Union of assembly names for the given package IDs (unknown IDs skipped).</summary>
        public static string[] ResolveAssembliesForPackages(IEnumerable<string> packageIds)
        {
            if (packageIds == null) return Array.Empty<string>();
            var idSet = new HashSet<string>(packageIds.Where(id => !string.IsNullOrEmpty(id)),
                StringComparer.OrdinalIgnoreCase);
            if (idSet.Count == 0) return Array.Empty<string>();

            return Discover()
                .Where(p => idSet.Contains(p.Id))
                .SelectMany(p => p.AssemblyNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToArray();
        }

        private static bool IsExcludedPackage(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return true;
            if (ExcludedPackageIds.Contains(packageId)) return true;
            foreach (var prefix in ExcludedPackagePrefixes)
            {
                if (packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string NormalizePath(string path) =>
            string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').TrimEnd('/');

        /// <summary>
        /// CompilationPipeline source paths are often <c>Packages/com.unity.x/...</c>,
        /// while <see cref="PackageInfo.resolvedPath"/> points at PackageCache on disk.
        /// </summary>
        private static bool IsUnderPackage(string filePath, PackageInfo package)
        {
            var file = NormalizePath(filePath);
            if (string.IsNullOrEmpty(file)) return false;

            var virtualRoot = "Packages/" + package.name;
            if (file.StartsWith(virtualRoot + "/", StringComparison.OrdinalIgnoreCase))
                return true;

            var resolved = NormalizePath(package.resolvedPath);
            if (!string.IsNullOrEmpty(resolved) &&
                file.StartsWith(resolved + "/", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
