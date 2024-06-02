using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ObervableUnityComponentsGenerator
{
	internal class WatchSyntaxReceiver : ISyntaxReceiver
	{
		public List<VariableDeclaratorSyntax> Variables { get; } = new List<VariableDeclaratorSyntax>();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			// A field with at least one attribute
			if (!(syntaxNode is FieldDeclarationSyntax fieldDeclaration) || fieldDeclaration.AttributeLists.Count == 0)
				return;

			Variables.AddRange(fieldDeclaration.Declaration.Variables);
		}
	}
}
