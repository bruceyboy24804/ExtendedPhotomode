// ─────────────────────────────────────────────────────────────────────────────
// ExtendedPhotomode.Codegen
//
// Scans the mod's C# for enums whose members carry [EnumOption], and emits the
// TypeScript the tool panel builds its button rows from: the member values, and
// an options list carrying each one's icon and tooltip.
//
// Why generate rather than hand-write: the panel keeps a table per enum, and
// nothing connects the two. Add a member and the table is silently short one
// entry; renumber one and every icon after it selects the wrong thing. Both are
// invisible until someone clicks the button and gets another mode.
//
// Parsed with Roslyn syntax trees rather than regex — the attribute arguments
// are ordinary C# expressions, and tooltips are prose full of the punctuation a
// regex would have to be taught about.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length < 2) {
    Console.Error.WriteLine("Usage: ExtendedPhotomode.Codegen <sourceDir> <outputFile>");
    return 1;
}

var sourceDir = Path.GetFullPath(args[0]);
var outputFile = Path.GetFullPath(args[1]);

if (!Directory.Exists(sourceDir)) {
    Console.Error.WriteLine($"Source directory not found: {sourceDir}");
    return 1;
}

var found = new List<EnumDef>();

// Skipping the submodule and build output: Common is shared code with its own UI, and obj/bin hold
// generated copies of the very files being read, which would produce every enum twice.
var files = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}Common{Path.DirectorySeparatorChar}"));

foreach (var file in files) {
    var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();

    foreach (var declaration in root.DescendantNodes().OfType<EnumDeclarationSyntax>()) {
        var options = new List<OptionDef>();

        // Enum members may omit their value, in which case C# continues from the previous one. The
        // generated TypeScript must carry the SAME numbers the C# compiler assigns, because the two
        // sides talk in raw ints over the binding — so the implicit sequence is tracked here rather
        // than requiring every member to be written out explicitly.
        var next = 0;

        foreach (var member in declaration.Members) {
            var value = next;

            if (member.EqualsValue?.Value is LiteralExpressionSyntax literal
                && literal.Token.Value is int explicitValue) {
                value = explicitValue;
            }

            next = value + 1;

            var attribute = member.AttributeLists
                .SelectMany(list => list.Attributes)
                // Matched on the last dotted segment, so it is found however it is written: bare,
                // namespace-qualified, or with the Attribute suffix C# allows to be dropped. Syntax
                // trees carry no symbol resolution, so the name here is exactly the text in the file.
                .FirstOrDefault(a => {
                    var name = a.Name.ToString();
                    var tail = name[(name.LastIndexOf('.') + 1)..];

                    return tail is "EnumOption" or "EnumOptionAttribute";
                });

            if (attribute is null) {
                continue;
            }

            var positional = attribute.ArgumentList?.Arguments
                .Where(a => a.NameEquals is null)
                .Select(a => (a.Expression as LiteralExpressionSyntax)?.Token.ValueText ?? string.Empty)
                .ToList() ?? [];

            var visible = attribute.ArgumentList?.Arguments
                .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "Visible")
                ?.Expression.ToString() != "false";

            if (!visible) {
                continue;
            }

            options.Add(new OptionDef(
                member.Identifier.Text,
                value,
                positional.ElementAtOrDefault(0) ?? string.Empty,
                positional.ElementAtOrDefault(1) ?? string.Empty));
        }

        if (options.Count > 0) {
            found.Add(new EnumDef(declaration.Identifier.Text, options));
        }
    }
}

if (found.Count == 0) {
    Console.Error.WriteLine("No enums carrying [EnumOption] were found; refusing to write an empty file.");
    return 1;
}

var output = new StringBuilder();

output.AppendLine("// GENERATED FILE — do not edit.");
output.AppendLine("//");
output.AppendLine("// Produced by Tools/ExtendedPhotomode.Codegen from the [EnumOption] attributes on the");
output.AppendLine("// mod's own enums. Change an icon or a tooltip in the C# and re-run it; editing this file");
output.AppendLine("// instead means the next run silently discards your change.");
output.AppendLine("//");
output.AppendLine("//   dotnet run --project Tools/ExtendedPhotomode.Codegen -- \\");
output.AppendLine("//       ExtendedPhotomode ExtendedPhotomode/UI/src/mods/generated/enum-options.ts");
output.AppendLine();
output.AppendLine("/** One selectable option, as the panel's button rows consume it. */");
output.AppendLine("export type EnumOption = {");
output.AppendLine("    readonly mode: number;");
output.AppendLine("    readonly src: string;");
output.AppendLine("    readonly tooltip: string;");
output.AppendLine("};");

foreach (var definition in found.OrderBy(e => e.Name, StringComparer.Ordinal)) {
    output.AppendLine();
    output.Append("export const ").Append(definition.Name).AppendLine(" = {");

    foreach (var option in definition.Options) {
        output.Append("    ").Append(option.Name).Append(": ").Append(option.Value).AppendLine(",");
    }

    output.AppendLine("} as const;");
    output.AppendLine();
    output.Append("export const ").Append(definition.Name).AppendLine("Options: readonly EnumOption[] = [");

    foreach (var option in definition.Options) {
        output.Append("    { mode: ").Append(definition.Name).Append('.').Append(option.Name)
              .Append(", src: \"").Append(Escape(option.Icon))
              .Append("\", tooltip: \"").Append(Escape(option.Tooltip)).AppendLine("\" },");
    }

    output.AppendLine("];");
}

Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
File.WriteAllText(outputFile, output.ToString());

Console.WriteLine($"Wrote {found.Count} enum(s), "
                + $"{found.Sum(e => e.Options.Count)} option(s) to {outputFile}");

return 0;

// Tooltips are prose: they carry quotes, backslashes and the odd newline, and any of the three would
// otherwise end the generated string literal early and produce TypeScript that does not parse.
static string Escape(string value) =>
    value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", string.Empty).Replace("\n", "\\n");

internal record OptionDef(string Name, int Value, string Icon, string Tooltip);

internal record EnumDef(string Name, List<OptionDef> Options);
