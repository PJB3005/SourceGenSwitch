﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestSourceGen;

[Generator]
public sealed class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var valuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            "TestProject.FooAttribute",
            (node, token) => true,
            (syntaxContext, token) =>
            {
                return new Value(syntaxContext.TargetSymbol.Name, syntaxContext.TargetSymbol.ContainingNamespace.Name);
            });

        var additionalFiles = context.AdditionalTextsProvider.Select((text, token) =>
            {
                var readText = text.GetText(token);
                if (readText == null)
                    return null;

                return new TextValue(Path.GetFileNameWithoutExtension(text.Path), readText.ToString());
            })
            .Where(x => x != null)
            .Collect()
            .Select((values, _) => values.ToDictionary(i => i!.Name, i => i!.Value));

        var combinedValues = valuesProvider.Combine(additionalFiles);

        context.RegisterSourceOutput(combinedValues, (productionContext, combinedValue) =>
        {
            var value = combinedValue.Left;
            if (!combinedValue.Right.TryGetValue(value.Name, out var content))
                return;

            var str = SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(content));

            var code = $$"""
                // <auto-generated />
                namespace {{value.Namespace}};

                public partial class {{value.Name}}
                {
                    private void Foo()
                    {
                        System.Console.WriteLine({{str.ToFullString()}});
                    }
                }
                """;

            productionContext.AddSource($"{value.Name}.cs", code);
        });
    }

    private sealed record Value(string Name, string Namespace);

    private sealed record TextValue(string Name, string Value);
}