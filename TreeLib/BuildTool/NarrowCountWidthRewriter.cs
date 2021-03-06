﻿/*
 *  Copyright © 2016 Thomas R. Lawrence
 * 
 *  GNU Lesser General Public License
 * 
 *  This file is part of TreeLib
 * 
 *  TreeLib is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program. If not, see <http://www.gnu.org/licenses/>.
 * 
*/
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BuildTool
{
    public class NarrowCountWidthRewriter : CSharpSyntaxRewriter
    {
        private const string CountAttributeName = "Count";


        public NarrowCountWidthRewriter()
        {
        }

        private static TypeSyntax NarrowIntegerType(TypeSyntax type)
        {
            if ((type.Kind() == SyntaxKind.PredefinedType) && (((PredefinedTypeSyntax)type).Keyword.Kind() == SyntaxKind.ULongKeyword))
            {
                type = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UIntKeyword)).WithTrailingTrivia(SyntaxFactory.Space);
            }
            else if ((type.Kind() == SyntaxKind.PredefinedType) && (((PredefinedTypeSyntax)type).Keyword.Kind() == SyntaxKind.LongKeyword))
            {
                type = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)).WithTrailingTrivia(SyntaxFactory.Space);
            }
            else
            {
                throw new ArgumentException();
            }

            return type;
        }


        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            node = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node);

            if (AttributeMatchUtil.HasAttributeSimple(node.AttributeLists, CountAttributeName))
            {
                node = node.WithDeclaration(node.Declaration.WithType(NarrowIntegerType(node.Declaration.Type)));
            }

            return node;
        }

        public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            node = (LocalDeclarationStatementSyntax)base.VisitLocalDeclarationStatement(node);

            if (AttributeMatchUtil.HasTriviaAnnotationSimple(node.GetLeadingTrivia(), CountAttributeName))
            {
                node = node.WithDeclaration(node.Declaration.WithType(NarrowIntegerType(node.Declaration.Type)));
            }

            return node;
        }
    }
}
