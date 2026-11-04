using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

const string PluginGuid = "arikm.cotl.followertalk";
const string ApiGuid = "io.github.xhayper.COTL_API";
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
string pluginSource = File.ReadAllText(Path.Combine(root, "Plugin.cs"));
string manifestPath = Path.Combine(root, "thunderstore", "manifest.json");
string projectPath = Path.Combine(root, "FollowerTalk.csproj");

using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
XDocument project = XDocument.Load(projectPath);
string version = manifest.RootElement.GetProperty("version_number").GetString() ?? "";
string[] dependencies = manifest.RootElement.GetProperty("dependencies")
    .EnumerateArray().Select(item => item.GetString() ?? "").ToArray();

Check(Version.TryParse(version, out _), "manifest version is numeric");
Check(dependencies.Any(item => item.StartsWith("BepInEx-BepInExPack_CultOfTheLamb-", StringComparison.Ordinal)),
    "manifest declares the game's BepInEx pack");
Check(dependencies.Any(item => item.StartsWith("xhayper-COTL_API-", StringComparison.Ordinal)),
    "manifest declares COTL_API");
Check(project.Descendants("TargetFramework").Single().Value == "netstandard2.1",
    "plugin targets the BepInEx-compatible framework");
Check(project.Descendants("AssemblyName").Single().Value == "FollowerTalk",
    "plugin output assembly name");
Check(project.Descendants("EnableDefaultCompileItems").All(item => item.Value != "false") &&
    !project.Descendants("Compile").Any(item => item.Attribute("Remove")?.Value == "Plugin.cs"),
    "plugin source is included in the production assembly");
foreach (string path in Directory.EnumerateFiles(root, "*.cs").Where(path => Path.GetFileName(path) != "Plugin.cs"))
{
    CompilationUnitSyntax unit = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();
    Check(!unit.DescendantNodes().OfType<ClassDeclarationSyntax>()
        .Any(type => Attributes(type, "BepInPlugin").Any()),
        "unexpected second BepInEx plugin in " + Path.GetFileName(path));
}

string[] errors = Inspect(pluginSource, version);
Check(errors.Length == 0, string.Join("; ", errors));
Check(Inspect(pluginSource.Replace("public sealed class Plugin", "internal sealed class Plugin", StringComparison.Ordinal), version)
    .Any(error => error.Contains("public", StringComparison.Ordinal)), "reject non-public plugin");
Check(Inspect(pluginSource.Replace("BaseUnityPlugin", "object", StringComparison.Ordinal), version)
    .Any(error => error.Contains("BaseUnityPlugin", StringComparison.Ordinal)), "reject wrong base type");
Check(Inspect(pluginSource.Replace(ApiGuid, "wrong.api.guid", StringComparison.Ordinal), version)
    .Any(error => error.Contains("dependency", StringComparison.Ordinal)), "reject wrong dependency GUID");
Check(Inspect(pluginSource.Replace(version + "\")", "0.0.0\")", StringComparison.Ordinal), version)
    .Any(error => error.Contains("version", StringComparison.Ordinal)), "reject package version drift");
Check(Inspect(pluginSource.Replace("private void Awake()", "private static void Awake()", StringComparison.Ordinal), version)
    .Any(error => error.Contains("Awake", StringComparison.Ordinal)), "reject static Awake");

Console.WriteLine("BepInEx source metadata and discovery prerequisites passed (game DLL loading not tested).");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("Failed: " + message);
}

static string[] Inspect(string source, string expectedVersion)
{
    var errors = new List<string>();
    SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
    errors.AddRange(tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(d => "C# syntax: " + d));
    CompilationUnitSyntax unit = tree.GetCompilationUnitRoot();
    ClassDeclarationSyntax[] attributed = unit.DescendantNodes().OfType<ClassDeclarationSyntax>()
        .Where(type => Attributes(type, "BepInPlugin").Any()).ToArray();
    if (attributed.Length != 1)
    {
        errors.Add("expected exactly one BepInPlugin class, found " + attributed.Length);
        return errors.ToArray();
    }

    ClassDeclarationSyntax plugin = attributed[0];
    string? ns = plugin.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
    if (ns != "FollowerTalk" || plugin.Identifier.ValueText != "Plugin" ||
        plugin.Ancestors().OfType<TypeDeclarationSyntax>().Any())
        errors.Add("plugin must be top-level FollowerTalk.Plugin");
    if (!plugin.Modifiers.Any(SyntaxKind.PublicKeyword) || plugin.Modifiers.Any(SyntaxKind.AbstractKeyword) ||
        plugin.Modifiers.Any(SyntaxKind.StaticKeyword) ||
        plugin.TypeParameterList != null)
        errors.Add("plugin must be public, concrete, and non-generic");
    if (plugin.BaseList?.Types.Count != 1 || plugin.BaseList.Types[0].Type.ToString() != "BaseUnityPlugin")
        errors.Add("plugin must directly inherit BaseUnityPlugin");

    AttributeSyntax[] identities = Attributes(plugin, "BepInPlugin").ToArray();
    if (identities.Length != 1 || !Arguments(identities[0]).SequenceEqual(new[] { PluginGuid, "Follower Talk", expectedVersion }))
        errors.Add("BepInPlugin GUID, name, or version does not match the package");
    AttributeSyntax[] api = Attributes(plugin, "BepInDependency").ToArray();
    if (api.Length != 1 || !Arguments(api[0]).SequenceEqual(new[] { ApiGuid }))
        errors.Add("required COTL_API dependency GUID is missing or incorrect");
    if (Attributes(plugin, "BepInProcess").Any() || Attributes(plugin, "BepInIncompatibility").Any())
        errors.Add("unexpected BepInEx process or incompatibility restriction");
    if (plugin.Members.OfType<ConstructorDeclarationSyntax>().Any())
        errors.Add("Unity plugin must not declare a custom constructor");
    if (!plugin.Members.OfType<MethodDeclarationSyntax>().Any(method =>
        method.Identifier.ValueText == "Awake" && method.ReturnType.ToString() == "void" &&
        method.ParameterList.Parameters.Count == 0 && !method.Modifiers.Any(SyntaxKind.StaticKeyword)))
        errors.Add("Unity Awake entry point is missing");
    return errors.ToArray();
}

static IEnumerable<AttributeSyntax> Attributes(ClassDeclarationSyntax type, string name) =>
    type.AttributeLists.SelectMany(list => list.Attributes).Where(attribute =>
        attribute.Name.ToString().Split('.').Last() is string shortName &&
        (shortName == name || shortName == name + "Attribute"));

static IEnumerable<string?> Arguments(AttributeSyntax attribute) =>
    attribute.ArgumentList?.Arguments.Select(argument =>
        (argument.Expression as LiteralExpressionSyntax)?.Token.ValueText) ?? Enumerable.Empty<string?>();
