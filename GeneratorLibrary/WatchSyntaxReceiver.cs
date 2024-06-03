using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ObervableUnityComponentsGenerator
{
	internal class WatchSyntaxReceiver : ISyntaxReceiver
	{
		public List<VariableDeclaratorSyntax> Variables { get; } = new List<VariableDeclaratorSyntax>();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			// Test for a public field, or a private/protected field with at least one attribute

			if (!(syntaxNode is FieldDeclarationSyntax fieldDeclaration))
				return;

			var isPublic = fieldDeclaration.Modifiers.Any(x => x.IsKind(SyntaxKind.PublicKeyword));

			if (!isPublic && fieldDeclaration.AttributeLists.Count == 0)
				return;

			Variables.AddRange(fieldDeclaration.Declaration.Variables);
		}
	}
}
