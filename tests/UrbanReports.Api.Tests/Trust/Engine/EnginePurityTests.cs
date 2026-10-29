using System.Reflection;
using System.Runtime.CompilerServices;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Tests.Trust.Engine;

public sealed class EnginePurityTests
{
    [Fact]
    public void Engine_uses_only_bcl()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "UrbanReports.Api", "Feature", "Trust", "Engine");
        Directory.Exists(folder).Should().BeTrue(folder);

        var files = Directory.GetFiles(folder, "*.cs");
        files.Should().HaveCountGreaterThanOrEqualTo(5);

        var usings = files
            .SelectMany(File.ReadLines)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("using ", StringComparison.Ordinal) && l.EndsWith(';'))
            .Select(l => l["using ".Length..^1].Trim())
            .Distinct();

        usings.Should().OnlyContain(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) || u == "UrbanReports.Api.Common");
    }

    [Fact]
    public void Engine_signatures_use_only_bcl_engine_types_and_enums()
    {
        var engineNamespace = typeof(TrustEngine).Namespace!;
        var engineTypes = typeof(TrustEngine).Assembly.GetTypes()
            .Where(t => t.Namespace == engineNamespace && !t.IsDefined(typeof(CompilerGeneratedAttribute)))
            .ToList();
        engineTypes.Should().HaveCountGreaterThanOrEqualTo(5);

        var referenced = engineTypes.SelectMany(ReferencedTypes).SelectMany(Flatten).Distinct().ToList();

        referenced.Should().OnlyContain(
            t => t.Namespace == engineNamespace
                || t.Namespace == "System" || t.Namespace!.StartsWith("System.", StringComparison.Ordinal)
                || (t.IsEnum && t.Namespace == "UrbanReports.Api.Common"),
            "o motor de confiança é puro: só BCL, os próprios tipos e os enums de Common");
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }
        foreach (var i in type.GetInterfaces()) yield return i;
        foreach (var f in type.GetFields(all)) yield return f.FieldType;
        foreach (var p in type.GetProperties(all)) yield return p.PropertyType;
        foreach (var m in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            if (m is MethodInfo mi) yield return mi.ReturnType;
            foreach (var p in m.GetParameters()) yield return p.ParameterType;
            foreach (var l in m.GetMethodBody()?.LocalVariables ?? []) yield return l.LocalType;
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
        {
            foreach (var t in Flatten(type.GetElementType()!)) yield return t;
            yield break;
        }
        if (type.IsGenericParameter)
        {
            yield break;
        }
        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        foreach (var arg in type.GetGenericArguments().SelectMany(Flatten)) yield return arg;
    }

    private static string RepositoryRoot([CallerFilePath] string file = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(file)!);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0)
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
