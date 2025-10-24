using System;
using System.Linq;
using System.Reflection;

public static class Program
{
    public static void Main()
    {
        var hbAssembly = typeof(HarfBuzzSharp.Face).Assembly;
        Console.WriteLine($"Assembly: {hbAssembly.FullName}");

        Console.WriteLine("First 50 HarfBuzzSharp types:");
        foreach (var type in hbAssembly.GetTypes().Take(50))
        {
            Console.WriteLine($" - {type.FullName}");
        }

        var skAssembly = typeof(SkiaSharp.SKTypeface).Assembly;
        Console.WriteLine($"\nSkiaSharp assembly: {skAssembly.FullName}");
        var subsetMethods = typeof(SkiaSharp.SKTypeface).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Subset", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Console.WriteLine(subsetMethods.Count == 0
            ? "SKTypeface has no public methods containing 'Subset'."
            : "SKTypeface subset-related methods:");

        foreach (var method in subsetMethods)
        {
            Console.WriteLine($" - {method}");
        }

        var apiType = hbAssembly.GetType("HarfBuzzSharp.HarfBuzzApi", throwOnError: false);
        Console.WriteLine("\nHarfBuzzApi subset methods:");
        if (apiType == null)
        {
            Console.WriteLine(" (HarfBuzzApi not found)");
        }
        else
        {
            var methods = apiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).ToList();
            var subsetApi = methods
                .Where(m => m.Name.Contains("Subset", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (subsetApi.Count == 0)
                Console.WriteLine(" (none)");
            else
                subsetApi.ForEach(name => Console.WriteLine($" - {name}"));

            var sampleMethod = methods.FirstOrDefault();
            if (sampleMethod != null)
            {
                var dllImport = sampleMethod.GetCustomAttributesData()
                    .FirstOrDefault(a => a.AttributeType.FullName == typeof(System.Runtime.InteropServices.DllImportAttribute).FullName);
                if (dllImport != null)
                {
                    var dllName = dllImport.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    Console.WriteLine($"\nDllImport target: {dllName}");
                }
            }
        }

        var blobType = hbAssembly.GetType("HarfBuzzSharp.Blob");
        if (blobType != null)
        {
            Console.WriteLine("\nBlob constructors:");
            foreach (var ctor in blobType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Console.WriteLine($" - {ctor}");
            }

            Console.WriteLine("Blob static methods:");
            foreach (var method in blobType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
                    method.Name.Contains("From", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($" - {method}");
                }
            }
        }
    }
}
