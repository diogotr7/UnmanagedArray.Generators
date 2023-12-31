using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DiagnosticDescriptor = Microsoft.CodeAnalysis.DiagnosticDescriptor;

namespace UnmanagedArray.Generators;

/// <summary>
///     Source generator used to expand structs into unmanaged-compatible ones.
///     This means instead of using an array internally, or a fixed buffer,
///     we stack X times the struct manually with fields, then add an operator to access them by index.
/// </summary>
[Generator]
public class UnmanagedArrayGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var input = context.SyntaxProvider.CreateSyntaxProvider(IsAttributePresent, GetStructInfo);
        var diagnostics = input.Where(diag => diag.Diagnostic is not null).Select((x, _) => x.Diagnostic!);
        var actualInfo = input.Where(diag => diag.Data is not null).Select((x, _) => x.Data!);

        context.RegisterSourceOutput(diagnostics, static (context, diagnostic) => { context.ReportDiagnostic(diagnostic); });

        context.RegisterSourceOutput(actualInfo,
            (spc, structInfo) => { spc.AddSource($"{structInfo.ParentStruct}.g.cs", Generate(structInfo)); });
    }

    private static bool IsAttributePresent(SyntaxNode syntaxNode, CancellationToken token)
    {
        if (syntaxNode is not AttributeSyntax attribute)
            return false;

        var name = attribute.Name switch
        {
            SimpleNameSyntax ins => ins.Identifier.Text,
            QualifiedNameSyntax qns => qns.Right.Identifier.Text,
            _ => null
        };

        return name is "UnmanagedArray" or "UnmanagedArrayAttribute";
    }

    private static DataOrDiagnostic<StructInfo> GetStructInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var structAttribute = (AttributeSyntax)context.Node;

        // "attribute.Parent" is "AttributeListSyntax"
        // "attribute.Parent.Parent" is a C# fragment the attributes are applied to
        var parentSyntax = structAttribute.Parent?.Parent;

        if (parentSyntax is not TypeDeclarationSyntax tds)
        {
            return Diagnostic.Create(new DiagnosticDescriptor(
                "UA001",
                "UnmanagedArrayAttribute can only be applied to structs or record structs",
                "",
                "RazerSdkReader.Generators",
                DiagnosticSeverity.Error,
                true
            ), parentSyntax?.GetLocation());
        }

        bool? isRecordStruct = tds switch
        {
            StructDeclarationSyntax => false,
            RecordDeclarationSyntax rds when rds.Kind() == SyntaxKind.RecordStructDeclaration => true,
            _ => null
        };

        // If this is null, we're not a struct or record struct
        if (isRecordStruct is null)
            return Diagnostic.Create(new DiagnosticDescriptor(
                "UA001",
                "UnmanagedArrayAttribute can only be applied to structs or record structs",
                "UnmanagedArrayAttribute can only be applied to structs or record structs",
                "RazerSdkReader.Generators",
                DiagnosticSeverity.Error,
                true
            ), parentSyntax.GetLocation());

        var parentType = context.SemanticModel.GetDeclaredSymbol(tds)!;

        //check if parentType has Fields. (Properties with a backing field are also considered fields here)
        var fields = parentType.GetMembers().OfType<IFieldSymbol>().ToImmutableArray();
        if (fields.Any(f => !f.IsConst && !f.IsStatic))
            return Diagnostic.Create(new DiagnosticDescriptor(
                "UA002",
                "UnmanagedArrayAttribute can only be applied to structs or record structs with no fields",
                "UnmanagedArrayAttribute can only be applied to structs or record structs with no fields",
                "RazerSdkReader.Generators",
                DiagnosticSeverity.Error,
                true
            ), parentSyntax.GetLocation());

        var structParameters = structAttribute.ArgumentList!.Arguments;
        if (structParameters.Count != 2)
            return Diagnostic.Create(new DiagnosticDescriptor(
                "UA003",
                "UnmanagedArrayAttribute must have exactly 2 parameters",
                "UnmanagedArrayAttribute must have exactly 2 parameters",
                "RazerSdkReader.Generators",
                DiagnosticSeverity.Error,
                true
            ), structAttribute.GetLocation());

        var childType = context.SemanticModel.GetTypeInfo((structParameters[0].Expression as TypeOfExpressionSyntax)!.Type);
        if (childType.Type is null || !childType.Type.IsUnmanagedType)
            return Diagnostic.Create(new DiagnosticDescriptor(
                "UA005",
                "UnmanagedArrayAttribute must have an unmanaged type as the first parameter",
                "UnmanagedArrayAttribute must have an unmanaged type as the first parameter",
                "RazerSdkReader.Generators",
                DiagnosticSeverity.Error,
                true
            ), structParameters[0].GetLocation());
        
        var childCountExpression = context.SemanticModel.GetConstantValue(structParameters[1].Expression);
        if (childCountExpression.Value is not (int childCount and > 0))
            return Diagnostic.Create(new DiagnosticDescriptor(
                "UA004",
                "UnmanagedArrayAttribute must have a constant positive integer as the second parameter",
                "UnmanagedArrayAttribute must have a constant positive integer as the second parameter",
                "RazerSdkReader.Generators",
                DiagnosticSeverity.Error,
                true
            ), structParameters[1].GetLocation());

        var format = new SymbolDisplayFormat(
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
        
        return new StructInfo
        {
            Namespace = parentType.ContainingNamespace.ToString(),
            ParentStruct = parentType.ToDisplayString(format),
            ChildStruct = childType.Type.ToDisplayString(format),
            Count = childCount,
            IsRecordStruct = isRecordStruct.Value,
            IsReadOnly = parentType.IsReadOnly
        };
    }
    
    public static string Generate(StructInfo structInfo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine($"namespace {structInfo.Namespace};");
        sb.AppendLine();
        sb.AppendLine("[StructLayout(LayoutKind.Sequential, Pack = 1)]");
        var type = structInfo.IsRecordStruct ? "record struct" : "struct";
        var readonlyModifier = structInfo.IsReadOnly ? " readonly" : "";
        sb.AppendLine($"public{readonlyModifier} partial {type} {structInfo.ParentStruct}");
        sb.AppendLine("{");
        
        var digits = structInfo.Count.ToString(CultureInfo.InvariantCulture).Length;
        for (int i = 0; i < structInfo.Count; i++)
        {
            sb.AppendLine($"    public{readonlyModifier} {structInfo.ChildStruct} {GetChildName(i, digits)};");
        }
        sb.AppendLine();
        sb.AppendLine($"    public int Length => {structInfo.Count};");
        sb.AppendLine();
        sb.AppendLine($"    public Span<{structInfo.ChildStruct}> AsSpan() => MemoryMarshal.CreateSpan(ref Unsafe.AsRef({GetChildName(0, digits)}), {structInfo.Count});");
        sb.AppendLine();
        sb.AppendLine($"    public static implicit operator Span<{structInfo.ChildStruct}>({structInfo.ParentStruct} {structInfo.ParentStruct.ToLowerInvariant()}) => {structInfo.ParentStruct.ToLowerInvariant()}.AsSpan();");
        sb.AppendLine("}");

        return sb.ToString();

        static string GetChildName(int index, int digits) => $"Child{index.ToString("D" + digits)}";
    }
}