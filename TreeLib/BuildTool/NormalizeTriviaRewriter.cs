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
    public class NormalizeTriviaRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitArgumentList(ArgumentListSyntax node)
        {
            node = NormalizeArgumentList(node);
            node = (ArgumentListSyntax)base.VisitArgumentList(node);
            return node;
        }

        public override SyntaxNode VisitTypeArgumentList(TypeArgumentListSyntax node)
        {
            node = NormalizeTypeArgumentList(node);
            node = (TypeArgumentListSyntax)base.VisitTypeArgumentList(node);
            return node;
        }

        public override SyntaxNode VisitParameterList(ParameterListSyntax node)
        {
            node = NormalizeParameterList(node);
            node = (ParameterListSyntax)base.VisitParameterList(node);
            return node;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            for (int i = 0; i < node.Members.Count - 1; i++)
            {
                while (true)
                {
                    int index = node.Members[i + 1].GetLeadingTrivia().IndexOf(SyntaxKind.EndIfDirectiveTrivia);
                    if (index < 0)
                    {
                        break;
                    }
                    node = node.WithMembers(
                        node.Members.RemoveAt(i).Insert(
                            i,
                            node.Members[i].WithTrailingTrivia(
                                node.Members[i].GetTrailingTrivia().Add(node.Members[i + 1].GetLeadingTrivia()[index]))));
                    node = node.WithMembers(
                        node.Members.RemoveAt(i + 1).Insert(
                            i + 1,
                            node.Members[i + 1].WithLeadingTrivia(
                                node.Members[i + 1].GetLeadingTrivia().RemoveAt(index))));
                }
                node = node.ReplaceNode(
                    node.Members[i],
                    RemoveOrComment(node.Members[i]));
            }

            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

            return node;
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            for (int i = 0; i < node.Members.Count - 1; i++)
            {
                while (true)
                {
                    int index = node.Members[i + 1].GetLeadingTrivia().IndexOf(SyntaxKind.EndIfDirectiveTrivia);
                    if (index < 0)
                    {
                        break;
                    }
                    node = node.WithMembers(
                        node.Members.RemoveAt(i).Insert(
                            i,
                            node.Members[i].WithTrailingTrivia(
                                node.Members[i].GetTrailingTrivia().Add(node.Members[i + 1].GetLeadingTrivia()[index]))));
                    node = node.WithMembers(
                        node.Members.RemoveAt(i + 1).Insert(
                            i + 1,
                            node.Members[i + 1].WithLeadingTrivia(
                                node.Members[i + 1].GetLeadingTrivia().RemoveAt(index))));
                }
                node = node.ReplaceNode(
                    node.Members[i],
                    RemoveOrComment(node.Members[i]));
            }

            node = (StructDeclarationSyntax)base.VisitStructDeclaration(node);

            return node;
        }

        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            for (int i = 0; i < node.Statements.Count; i++)
            {
                StatementSyntax statement = node.Statements[i];
                node = node.WithStatements(node.Statements.RemoveAt(i).Insert(i, RemoveOrComment(statement)));
            }

            node = node.WithStatements(RestructureComments(node.Statements));

            node = (BlockSyntax)base.VisitBlock(node);

            return node;
        }


        //
        // Helpers
        //

        // All this rigamarole is because Roslyn associates trivia in an actual argument list with the delimiters
        // rather than the arguments. It would make more sense to associate it with arguments, since a comment leading
        // or trailing the argument almost always applies to the argument [e.g. foo(a, b, true/*replace*/) or simulating
        // attributes, such as foo(a, /*[Special]*/ b) ] and almost never to the delimiter. To facilitate ease in
        // processing, this normalizes trivia by moving any trivia on delimiters to the appropriate argument.
        // (NOTE: Roslyn associates comments on multi-line argument lists with arguments, so it's behavior is actually
        // inconsistent! Normalization helps that too.)
        private static ArgumentListSyntax NormalizeArgumentList(ArgumentListSyntax node)
        {
            int count = node.Arguments.Count;

            SyntaxTriviaList[] argumentTriviasLeading = new SyntaxTriviaList[count];
            SyntaxTriviaList[] argumentTriviasTrailing = new SyntaxTriviaList[count];
            for (int j = 0; j < count; j++)
            {
                argumentTriviasLeading[j] = SyntaxTriviaList.Empty;
                argumentTriviasTrailing[j] = SyntaxTriviaList.Empty;
            }

            if (count > 0)
            {
                // save trivia after open paren
                SyntaxTriviaList trailing = SyntaxTriviaList.Empty.AddRange(node.OpenParenToken.TrailingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasLeading[0] = argumentTriviasLeading[0].AddRange(trailing);
            }
            for (int j = 0; j < count - 1; j++)
            {
                // save trivia on trailing edge of separator to next argument
                SyntaxTriviaList trailing = SyntaxTriviaList.Empty.AddRange(node.Arguments.GetSeparator(j).TrailingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasLeading[j + 1] = argumentTriviasLeading[j + 1].AddRange(trailing);
            }
            for (int j = 0; j < count; j++)
            {
                // save leading and trailing trivia from argument
                SyntaxTriviaList leading = node.Arguments[j].GetLeadingTrivia();
                SyntaxTriviaList trailing = node.Arguments[j].GetTrailingTrivia();
                argumentTriviasLeading[j] = argumentTriviasLeading[j].AddRange(leading);
                argumentTriviasTrailing[j] = argumentTriviasTrailing[j].AddRange(trailing);
            }
            for (int j = 0; j < count - 1; j++)
            {
                // save trivia on leading edge of separator to previous argument
                SyntaxTriviaList leading = SyntaxTriviaList.Empty.AddRange(node.Arguments.GetSeparator(j).LeadingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasTrailing[j] = argumentTriviasTrailing[j].AddRange(leading);
            }
            if (count > 0)
            {
                // save trivia before close paren
                SyntaxTriviaList leading = node.CloseParenToken.LeadingTrivia;
                argumentTriviasTrailing[count - 1] = argumentTriviasTrailing[count - 1].AddRange(leading);
            }

            bool multiline;
            int indent;
            DetectMultiLineArgumentList(node.Arguments, out multiline, out indent);

            // strip all trivia
            node = node.WithOpenParenToken(node.OpenParenToken.WithTrailingTrivia(SyntaxTriviaList.Empty));
            node = node.WithCloseParenToken(node.CloseParenToken.WithLeadingTrivia(SyntaxTriviaList.Empty));
            for (int j = 0; j < count - 1; j++)
            {
                node = node.WithArguments(
                    node.Arguments.ReplaceSeparator(
                        node.Arguments.GetSeparator(j),
                        node.Arguments.GetSeparator(j).WithLeadingTrivia(SyntaxTriviaList.Empty).WithTrailingTrivia(SyntaxTriviaList.Empty)));
            }

            // reformat
            SyntaxTriviaList delimiterTrailing = CreateDelimiterTrailer(multiline, indent);
            if (multiline)
            {
                node = node.WithOpenParenToken(node.OpenParenToken.WithTrailingTrivia(delimiterTrailing));
            }
            for (int j = 0; j < count - 1; j++)
            {
                node = node.WithArguments(
                    node.Arguments.ReplaceSeparator(
                        node.Arguments.GetSeparator(j),
                        node.Arguments.GetSeparator(j).WithTrailingTrivia(delimiterTrailing)));
            }

            // reassociate comment trivia to arguments
            for (int j = 0; j < count; j++)
            {
                node = node.WithArguments(
                    node.Arguments.Replace(
                        node.Arguments[j],
                        node.Arguments[j]
                            .WithoutTrivia()
                            .WithLeadingTrivia(argumentTriviasLeading[j])
                            .WithTrailingTrivia(argumentTriviasTrailing[j])));
            }

            return node;
        }

        // Since TypeArgumentListSyntax and ArgumentListSyntax do not share a common base class with argument
        // access, it's easiest to just write the code out again.
        private static TypeArgumentListSyntax NormalizeTypeArgumentList(TypeArgumentListSyntax node)
        {
            int count = node.Arguments.Count;

            SyntaxTriviaList[] argumentTriviasLeading = new SyntaxTriviaList[count];
            SyntaxTriviaList[] argumentTriviasTrailing = new SyntaxTriviaList[count];
            for (int j = 0; j < count; j++)
            {
                argumentTriviasLeading[j] = SyntaxTriviaList.Empty;
                argumentTriviasTrailing[j] = SyntaxTriviaList.Empty;
            }

            if (count > 0)
            {
                // save trivia after open paren
                SyntaxTriviaList trailing = SyntaxTriviaList.Empty.AddRange(node.LessThanToken.TrailingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasLeading[0] = argumentTriviasLeading[0].AddRange(trailing);
            }
            for (int j = 0; j < count - 1; j++)
            {
                // save trivia on trailing edge of separator to next argument
                SyntaxTriviaList trailing = SyntaxTriviaList.Empty.AddRange(node.Arguments.GetSeparator(j).TrailingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasLeading[j + 1] = argumentTriviasLeading[j + 1].AddRange(trailing);
            }
            for (int j = 0; j < count; j++)
            {
                // save leading and trailing trivia from argument
                SyntaxTriviaList leading = node.Arguments[j].GetLeadingTrivia();
                SyntaxTriviaList trailing = node.Arguments[j].GetTrailingTrivia();
                argumentTriviasLeading[j] = argumentTriviasLeading[j].AddRange(leading);
                argumentTriviasTrailing[j] = argumentTriviasTrailing[j].AddRange(trailing);
            }
            for (int j = 0; j < count - 1; j++)
            {
                // save trivia on leading edge of separator to previous argument
                SyntaxTriviaList leading = SyntaxTriviaList.Empty.AddRange(node.Arguments.GetSeparator(j).LeadingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasTrailing[j] = argumentTriviasTrailing[j].AddRange(leading);
            }
            if (count > 0)
            {
                // save trivia before close paren
                SyntaxTriviaList leading = node.GreaterThanToken.LeadingTrivia;
                argumentTriviasTrailing[count - 1] = argumentTriviasTrailing[count - 1].AddRange(leading);
            }

            bool multiline;
            int indent;
            DetectMultiLineArgumentList(node.Arguments, out multiline, out indent);

            // strip all trivia
            node = node.WithLessThanToken(node.LessThanToken.WithTrailingTrivia(SyntaxTriviaList.Empty));
            node = node.WithGreaterThanToken(node.GreaterThanToken.WithLeadingTrivia(SyntaxTriviaList.Empty));
            for (int j = 0; j < count - 1; j++)
            {
                node = node.WithArguments(
                    node.Arguments.ReplaceSeparator(
                        node.Arguments.GetSeparator(j),
                        node.Arguments.GetSeparator(j).WithLeadingTrivia(SyntaxTriviaList.Empty).WithTrailingTrivia(SyntaxTriviaList.Empty)));
            }

            // reformat
            SyntaxTriviaList delimiterTrailing = CreateDelimiterTrailer(multiline, indent);
            if (multiline)
            {
                node = node.WithLessThanToken(node.LessThanToken.WithTrailingTrivia(delimiterTrailing));
            }
            for (int j = 0; j < count - 1; j++)
            {
                node = node.WithArguments(
                    node.Arguments.ReplaceSeparator(
                        node.Arguments.GetSeparator(j),
                        node.Arguments.GetSeparator(j).WithTrailingTrivia(delimiterTrailing)));
            }

            // reassociate comment trivia to arguments
            for (int j = 0; j < count; j++)
            {
                node = node.WithArguments(
                    node.Arguments.Replace(
                        node.Arguments[j],
                        node.Arguments[j]
                            .WithoutTrivia()
                            .WithLeadingTrivia(argumentTriviasLeading[j])
                            .WithTrailingTrivia(argumentTriviasTrailing[j])));
            }

            return node;
        }

        private static void DetectMultiLineArgumentList<T>(SeparatedSyntaxList<T> arguments, out bool multiline, out int indent) where T : SyntaxNode
        {
            multiline = false;
            indent = 0;
            for (int j = 0; j < arguments.Count - 1; j++)
            {
                foreach (SyntaxTrivia trivia in arguments.GetSeparator(j).TrailingTrivia)
                {
                    if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        multiline = true;
                    }
                    else if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        int length = trivia.ToString().Length;
                        if (length >= 4) // kind of a hack
                        {
                            indent = Math.Max(indent, length);
                        }
                    }
                }
            }
        }

        private static bool MultiLineCommentPredicate(SyntaxTrivia candidate)
        {
            return candidate.IsKind(SyntaxKind.MultiLineCommentTrivia);
        }

        private static SyntaxTriviaList CreateDelimiterTrailer(bool multiline, int indent)
        {
            SyntaxTriviaList delimiterTrailing = multiline
                ? SyntaxTriviaList.Create(SyntaxFactory.EndOfLine(Environment.NewLine)).Add(SyntaxFactory.Whitespace(new string(' ', indent)))
                : SyntaxTriviaList.Create(SyntaxFactory.Space);
            return delimiterTrailing;
        }

        private static ParameterListSyntax NormalizeParameterList(ParameterListSyntax node)
        {
            int count = node.Parameters.Count;

            SyntaxTriviaList[] argumentTriviasLeading = new SyntaxTriviaList[count];
            SyntaxTriviaList[] argumentTriviasTrailing = new SyntaxTriviaList[count];
            for (int j = 0; j < count; j++)
            {
                argumentTriviasLeading[j] = SyntaxTriviaList.Empty;
                argumentTriviasTrailing[j] = SyntaxTriviaList.Empty;
            }

            if (count > 0)
            {
                // save trivia after open paren
                SyntaxTriviaList trailing = SyntaxTriviaList.Empty.AddRange(node.OpenParenToken.TrailingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasLeading[0] = argumentTriviasLeading[0].AddRange(trailing);
            }
            for (int j = 0; j < count - 1; j++)
            {
                // save trivia on trailing edge of separator to next argument
                SyntaxTriviaList trailing = SyntaxTriviaList.Empty.AddRange(node.Parameters.GetSeparator(j).TrailingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasLeading[j + 1] = argumentTriviasLeading[j + 1].AddRange(trailing);
            }
            for (int j = 0; j < count; j++)
            {
                // save leading and trailing trivia from argument
                SyntaxTriviaList leading = node.Parameters[j].GetLeadingTrivia();
                SyntaxTriviaList trailing = node.Parameters[j].GetTrailingTrivia();
                argumentTriviasLeading[j] = argumentTriviasLeading[j].AddRange(leading);
                argumentTriviasTrailing[j] = argumentTriviasTrailing[j].AddRange(trailing);
            }
            for (int j = 0; j < count - 1; j++)
            {
                // save trivia on leading edge of separator to previous argument
                SyntaxTriviaList leading = SyntaxTriviaList.Empty.AddRange(node.Parameters.GetSeparator(j).LeadingTrivia.Where(MultiLineCommentPredicate));
                argumentTriviasTrailing[j] = argumentTriviasTrailing[j].AddRange(leading);
            }
            if (count > 0)
            {
                // save trivia before close paren
                SyntaxTriviaList leading = node.CloseParenToken.LeadingTrivia;
                argumentTriviasTrailing[count - 1] = argumentTriviasTrailing[count - 1].AddRange(leading);
            }

            bool multiline;
            int indent;
            DetectMultiLineArgumentList(node.Parameters, out multiline, out indent);

            // strip all trivia
            node = node.WithOpenParenToken(node.OpenParenToken.WithTrailingTrivia(SyntaxTriviaList.Empty));
            node = node.WithCloseParenToken(node.CloseParenToken.WithLeadingTrivia(SyntaxTriviaList.Empty));
            for (int j = 0; j < count - 1; j++)
            {
                node = node.WithParameters(
                    node.Parameters.ReplaceSeparator(
                        node.Parameters.GetSeparator(j),
                        node.Parameters.GetSeparator(j).WithLeadingTrivia(SyntaxTriviaList.Empty).WithTrailingTrivia(SyntaxTriviaList.Empty)));
            }

            // reformat
            SyntaxTriviaList delimiterTrailing = CreateDelimiterTrailer(multiline, indent);
            if (multiline)
            {
                node = node.WithOpenParenToken(node.OpenParenToken.WithTrailingTrivia(delimiterTrailing));
            }
            for (int j = 0; j < count - 1; j++)
            {
                node = node.WithParameters(
                    node.Parameters.ReplaceSeparator(
                        node.Parameters.GetSeparator(j),
                        node.Parameters.GetSeparator(j).WithTrailingTrivia(delimiterTrailing)));
            }

            // reassociate comment trivia to arguments
            for (int j = 0; j < count; j++)
            {
                node = node.WithParameters(
                    node.Parameters.Replace(
                        node.Parameters[j],
                        node.Parameters[j]
                            .WithoutTrivia()
                            .WithLeadingTrivia(argumentTriviasLeading[j])
                            .WithTrailingTrivia(argumentTriviasTrailing[j])));
            }

            return node;
        }

        private static SyntaxTriviaList RemoveOrComment(SyntaxTriviaList leadingTrivia)
        {
            SyntaxTriviaList trivia = SyntaxTriviaList.Empty;
            foreach (SyntaxTrivia trivium in leadingTrivia)
            {
                if (!(trivium.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    && String.Equals(trivium.ToString().Replace("//", "").Trim(), "[OR]", StringComparison.OrdinalIgnoreCase)))
                {
                    trivia = trivia.Add(trivium);
                }
            }
            return trivia;
        }

        private static MemberDeclarationSyntax RemoveOrComment(MemberDeclarationSyntax member)
        {
            return member.WithLeadingTrivia(RemoveOrComment(member.GetLeadingTrivia()));
        }

        private static StatementSyntax RemoveOrComment(StatementSyntax member)
        {
            return member.WithLeadingTrivia(RemoveOrComment(member.GetLeadingTrivia()));
        }

        private static List<SyntaxTriviaList> RestructureComments(SyntaxTriviaList leadingTrivia)
        {
            List<SyntaxTriviaList> groups = new List<SyntaxTriviaList>();

            groups.Add(SyntaxTriviaList.Empty);
            bool newline = false;
            bool nonNewlineSeen = false;
            foreach (SyntaxTrivia trivium in leadingTrivia)
            {
                groups[groups.Count - 1] = groups[groups.Count - 1].Add(trivium);
                if (trivium.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    if (newline && nonNewlineSeen)
                    {
                        groups.Add(SyntaxTriviaList.Empty);
                        nonNewlineSeen = false;
                    }
                    newline = true;
                }
                else
                {
                    nonNewlineSeen = true;
                    newline = false;
                }
            }
            if (newline && !nonNewlineSeen && (groups.Count != 1))
            {
                groups.Add(SyntaxTriviaList.Empty);
            }

            return groups;
        }

        private static SyntaxList<StatementSyntax> RestructureComments(SyntaxList<StatementSyntax> statements)
        {
            SyntaxList<StatementSyntax> result = new SyntaxList<StatementSyntax>();

            foreach (StatementSyntax statement in statements)
            {
                List<SyntaxTriviaList> groups = RestructureComments(statement.GetLeadingTrivia());
                if (groups.Count == 1)
                {
                    result = result.Add(statement);
                }
                else
                {
                    for (int i = 0; i < groups.Count - 1; i++)
                    {
                        result = result.Add(
                            SyntaxFactory.EmptyStatement(SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                                .WithLeadingTrivia(groups[i]));
                    }
                    result = result.Add(
                        statement.WithLeadingTrivia(groups[groups.Count - 1])
                            .WithTrailingTrivia(statement.GetTrailingTrivia()));
                }
            }

            return result;
        }
    }
}
