// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MonoGame.Effect
{
    internal sealed class EffectPreprocessor
    {
        private readonly Dictionary<string, Macro> _macros = new Dictionary<string, Macro>();
        private readonly List<string> _dependencies;
        private readonly IEffectCompilerOutput _output;
        private readonly string _rootDirectory;
        private readonly Stack<ConditionalState> _conditionals = new Stack<ConditionalState>();

        public EffectPreprocessor(IDictionary<string, string> defines, List<string> dependencies, IEffectCompilerOutput output, string rootFilePath)
        {
            _dependencies = dependencies;
            _output = output;
            _rootDirectory = Path.GetDirectoryName(Path.GetFullPath(rootFilePath)) ?? Directory.GetCurrentDirectory();

            foreach (var define in defines)
                DefineObjectMacro(define.Key, define.Value ?? string.Empty);
        }

        public string Process(string source, string filePath)
        {
            return ProcessSource(AppendNewlineIfNonePresent(NormalizeNewlines(source)), Path.GetFullPath(filePath), false);
        }

        private string ProcessSource(string source, string filePath, bool isInclude)
        {
            if (isInclude && !_dependencies.Contains(filePath))
                _dependencies.Add(filePath);

            source = StripCommentsPreservingNewlines(source);

            var output = new StringBuilder();
            var lines = ReadLogicalLines(source);
            var directory = Path.GetDirectoryName(filePath) ?? _rootDirectory;

            foreach (var line in lines)
            {
                var trimmed = line.Text.TrimStart();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    HandleDirective(line, trimmed.Substring(1).TrimStart(), directory, output);
                    continue;
                }

                if (!IsActive)
                    continue;

                output.Append(ExpandMacros(line.Text, new HashSet<string>()));
                if (!line.Text.EndsWith("\n", StringComparison.Ordinal))
                    output.Append('\n');
            }

            return output.ToString();
        }

        private void HandleDirective(LogicalLine line, string directiveText, string directory, StringBuilder output)
        {
            var name = ReadIdentifier(directiveText, 0, out var index);
            var rest = directiveText.Substring(index).TrimStart();

            switch (name)
            {
                case "include":
                    if (IsActive)
                        Include(rest, directory, output, line.LineNumber);
                    break;
                case "define":
                    if (IsActive)
                        Define(rest);
                    break;
                case "undef":
                    if (IsActive)
                    {
                        var macroName = ReadIdentifier(rest, 0, out _);
                        if (macroName.Length > 0)
                            _macros.Remove(macroName);
                    }
                    break;
                case "if":
                    PushConditional(EvaluateExpression(rest));
                    break;
                case "ifdef":
                    PushConditional(_macros.ContainsKey(ReadIdentifier(rest, 0, out _)));
                    break;
                case "ifndef":
                    PushConditional(!_macros.ContainsKey(ReadIdentifier(rest, 0, out _)));
                    break;
                case "elif":
                    Elif(EvaluateExpression(rest));
                    break;
                case "else":
                    Else();
                    break;
                case "endif":
                    if (_conditionals.Count > 0)
                        _conditionals.Pop();
                    break;
                case "line":
                case "pragma":
                    if (IsActive)
                        output.Append(line.Text);
                    break;
                case "warning":
                    if (IsActive)
                        _output.WriteWarning(directory, line.LineNumber, 1, rest);
                    break;
                case "error":
                    if (IsActive)
                        _output.WriteError(directory, line.LineNumber, 1, rest);
                    break;
                default:
                    if (IsActive)
                        output.Append(line.Text);
                    break;
            }
        }

        private void Include(string rest, string directory, StringBuilder output, int lineNumber)
        {
            rest = ExpandMacros(rest, new HashSet<string>()).Trim();
            if (rest.Length < 2 || rest[0] != '"')
            {
                _output.WriteError(directory, lineNumber, 1, "Expected quoted include path");
                return;
            }

            var end = rest.IndexOf('"', 1);
            if (end < 0)
            {
                _output.WriteError(directory, lineNumber, 1, "Unterminated include path");
                return;
            }

            var includeName = rest.Substring(1, end - 1);
            var includePath = ResolveInclude(directory, includeName);
            if (includePath == null)
            {
                _output.WriteError(directory, lineNumber, 1, "Could not find include file '" + includeName + "'");
                return;
            }

            output.Append(ProcessSource(AppendNewlineIfNonePresent(NormalizeNewlines(File.ReadAllText(includePath))), includePath, true));
        }

        private string? ResolveInclude(string directory, string includeName)
        {
            var localPath = Path.GetFullPath(Path.Combine(directory, includeName));
            if (File.Exists(localPath))
                return localPath;

            var rootPath = Path.GetFullPath(Path.Combine(_rootDirectory, includeName));
            if (File.Exists(rootPath))
                return rootPath;

            return null;
        }

        private void Define(string text)
        {
            var name = ReadIdentifier(text, 0, out var index);
            if (name.Length == 0)
                return;

            if (index < text.Length && text[index] == '(')
            {
                var parametersEnd = FindMatchingParen(text, index);
                if (parametersEnd < 0)
                    return;

                var parametersText = text.Substring(index + 1, parametersEnd - index - 1);
                var parameters = SplitArguments(parametersText);
                var body = parametersEnd + 1 < text.Length ? TrimDirectiveBody(text.Substring(parametersEnd + 1).TrimStart()) : string.Empty;
                _macros[name] = new Macro(name, parameters, body);
            }
            else
            {
                var body = index < text.Length ? TrimDirectiveBody(text.Substring(index).TrimStart()) : string.Empty;
                DefineObjectMacro(name, body);
            }
        }

        private static string TrimDirectiveBody(string body)
        {
            return body.TrimEnd('\n', '\r');
        }

        private void DefineObjectMacro(string name, string body)
        {
            if (name.Length > 0)
                _macros[name] = new Macro(name, null, body);
        }

        private string ExpandMacros(string text, HashSet<string> expanding)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < text.Length;)
            {
                var ch = text[index];
                if (IsIdentifierStart(ch))
                {
                    var identifier = ReadIdentifier(text, index, out var next);
                    if (_macros.TryGetValue(identifier, out var macro) && !expanding.Contains(identifier))
                    {
                        if (macro.IsFunctionLike)
                        {
                            var callIndex = SkipHorizontalWhitespace(text, next);
                            if (callIndex < text.Length && text[callIndex] == '(' && TryReadMacroArguments(text, callIndex, out var arguments, out var afterCall))
                            {
                                expanding.Add(identifier);
                                builder.Append(ExpandMacros(ApplyFunctionMacro(macro, arguments), expanding));
                                expanding.Remove(identifier);
                                index = afterCall;
                                continue;
                            }
                        }
                        else
                        {
                            expanding.Add(identifier);
                            builder.Append(ExpandMacros(macro.Body, expanding));
                            expanding.Remove(identifier);
                            index = next;
                            continue;
                        }
                    }

                    builder.Append(identifier);
                    index = next;
                    continue;
                }

                builder.Append(ch);
                index++;
            }

            return builder.ToString();
        }

        private string ApplyFunctionMacro(Macro macro, List<string> arguments)
        {
            var parameters = macro.Parameters
                ?? throw new InvalidOperationException($"Macro '{macro.Name}' does not define parameters.");

            var body = macro.Body;
            for (var i = 0; i < parameters.Count; i++)
            {
                var argument = i < arguments.Count ? arguments[i].Trim() : string.Empty;
                body = ReplaceIdentifier(body, parameters[i], argument);
            }

            return PasteTokens(body);
        }

        private static string ReplaceIdentifier(string text, string identifier, string replacement)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < text.Length;)
            {
                if (IsIdentifierStart(text[index]))
                {
                    var current = ReadIdentifier(text, index, out var next);
                    builder.Append(current == identifier ? replacement : current);
                    index = next;
                }
                else
                {
                    builder.Append(text[index]);
                    index++;
                }
            }

            return builder.ToString();
        }

        private static string PasteTokens(string text)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < text.Length;)
            {
                if (index + 1 < text.Length && text[index] == '#' && text[index + 1] == '#')
                {
                    while (builder.Length > 0 && char.IsWhiteSpace(builder[builder.Length - 1]))
                        builder.Length--;
                    index += 2;
                    while (index < text.Length && char.IsWhiteSpace(text[index]))
                        index++;
                    continue;
                }

                builder.Append(text[index]);
                index++;
            }

            return builder.ToString();
        }

        private bool EvaluateExpression(string expression)
        {
            var parser = new ExpressionParser(expression, _macros);
            return parser.Parse() != 0;
        }

        private void PushConditional(bool condition)
        {
            var parentActive = IsActive;
            _conditionals.Push(new ConditionalState(parentActive, parentActive && condition, parentActive && condition));
        }

        private void Elif(bool condition)
        {
            if (_conditionals.Count == 0)
                return;

            var current = _conditionals.Pop();
            var active = current.ParentActive && !current.BranchTaken && condition;
            _conditionals.Push(new ConditionalState(current.ParentActive, active, current.BranchTaken || active));
        }

        private void Else()
        {
            if (_conditionals.Count == 0)
                return;

            var current = _conditionals.Pop();
            var active = current.ParentActive && !current.BranchTaken;
            _conditionals.Push(new ConditionalState(current.ParentActive, active, true));
        }

        private bool IsActive
        {
            get
            {
                foreach (var conditional in _conditionals)
                    if (!conditional.Active)
                        return false;
                return true;
            }
        }

        private static string StripCommentsPreservingNewlines(string text)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < text.Length;)
            {
                if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '/')
                {
                    while (index < text.Length && text[index] != '\n')
                        index++;
                    continue;
                }

                if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/'))
                    {
                        if (text[index] == '\n')
                            builder.Append('\n');
                        index++;
                    }
                    index = Math.Min(index + 2, text.Length);
                    continue;
                }

                builder.Append(text[index]);
                index++;
            }

            return builder.ToString();
        }

        private static List<LogicalLine> ReadLogicalLines(string source)
        {
            var result = new List<LogicalLine>();
            var physicalLines = source.Split('\n');

            for (var i = 0; i < physicalLines.Length; i++)
            {
                var lineNumber = i + 1;
                var line = physicalLines[i];
                var builder = new StringBuilder();
                builder.Append(line);

                while (EndsWithContinuation(builder))
                {
                    builder.Length--;
                    builder.Append('\n');
                    if (++i >= physicalLines.Length)
                        break;
                    builder.Append(physicalLines[i]);
                }

                builder.Append('\n');
                result.Add(new LogicalLine(builder.ToString(), lineNumber));
            }

            return result;
        }

        private static bool EndsWithContinuation(StringBuilder builder)
        {
            var index = builder.Length - 1;
            while (index >= 0 && (builder[index] == ' ' || builder[index] == '\t'))
                index--;
            return index >= 0 && builder[index] == '\\';
        }

        private static bool TryReadMacroArguments(string text, int openParen, out List<string> arguments, out int endIndex)
        {
            arguments = new List<string>();
            endIndex = openParen;
            var depth = 0;
            var start = openParen + 1;

            for (var index = openParen; index < text.Length; index++)
            {
                var ch = text[index];
                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        arguments.Add(text.Substring(start, index - start));
                        endIndex = index + 1;
                        return true;
                    }
                    continue;
                }

                if (ch == ',' && depth == 1)
                {
                    arguments.Add(text.Substring(start, index - start));
                    start = index + 1;
                }
            }

            return false;
        }

        private static List<string> SplitArguments(string text)
        {
            var result = new List<string>();
            foreach (var value in text.Split(','))
            {
                var parameter = value.Trim();
                if (parameter.Length > 0)
                    result.Add(parameter);
            }
            return result;
        }

        private static int FindMatchingParen(string text, int openParen)
        {
            var depth = 0;
            for (var index = openParen; index < text.Length; index++)
            {
                if (text[index] == '(')
                    depth++;
                else if (text[index] == ')')
                {
                    depth--;
                    if (depth == 0)
                        return index;
                }
            }
            return -1;
        }

        private static int SkipHorizontalWhitespace(string text, int index)
        {
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
                index++;
            return index;
        }

        private static string ReadIdentifier(string text, int index, out int next)
        {
            next = index;
            if (index >= text.Length || !IsIdentifierStart(text[index]))
                return string.Empty;

            next++;
            while (next < text.Length && IsIdentifierPart(text[next]))
                next++;
            return text.Substring(index, next - index);
        }

        private static bool IsIdentifierStart(char ch)
        {
            return ch == '_' || char.IsLetter(ch);
        }

        private static bool IsIdentifierPart(char ch)
        {
            return ch == '_' || char.IsLetterOrDigit(ch);
        }

        private static string NormalizeNewlines(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string AppendNewlineIfNonePresent(string text)
        {
            return text.EndsWith("\n", StringComparison.Ordinal) ? text : text + "\n";
        }

        private sealed class Macro
        {
            public readonly string Name;
            public readonly List<string>? Parameters;
            public readonly string Body;

            public Macro(string name, List<string>? parameters, string body)
            {
                Name = name;
                Parameters = parameters;
                Body = body;
            }

            public bool IsFunctionLike => Parameters != null;
        }

        private struct ConditionalState
        {
            public readonly bool ParentActive;
            public readonly bool Active;
            public readonly bool BranchTaken;

            public ConditionalState(bool parentActive, bool active, bool branchTaken)
            {
                ParentActive = parentActive;
                Active = active;
                BranchTaken = branchTaken;
            }
        }

        private struct LogicalLine
        {
            public readonly string Text;
            public readonly int LineNumber;

            public LogicalLine(string text, int lineNumber)
            {
                Text = text;
                LineNumber = lineNumber;
            }
        }

        private sealed class ExpressionParser
        {
            private readonly string _text;
            private readonly Dictionary<string, Macro> _macros;
            private int _index;

            public ExpressionParser(string text, Dictionary<string, Macro> macros)
            {
                _text = text;
                _macros = macros;
            }

            public long Parse()
            {
                return ParseOr();
            }

            private long ParseOr()
            {
                var left = ParseAnd();
                while (Match("||"))
                    left = ParseAnd() != 0 || left != 0 ? 1 : 0;
                return left;
            }

            private long ParseAnd()
            {
                var left = ParseEquality();
                while (Match("&&"))
                    left = ParseEquality() != 0 && left != 0 ? 1 : 0;
                return left;
            }

            private long ParseEquality()
            {
                var left = ParseRelational();
                for (;;)
                {
                    if (Match("=="))
                        left = left == ParseRelational() ? 1 : 0;
                    else if (Match("!="))
                        left = left != ParseRelational() ? 1 : 0;
                    else
                        return left;
                }
            }

            private long ParseRelational()
            {
                var left = ParseAdditive();
                for (;;)
                {
                    if (Match("<="))
                        left = left <= ParseAdditive() ? 1 : 0;
                    else if (Match(">="))
                        left = left >= ParseAdditive() ? 1 : 0;
                    else if (Match("<"))
                        left = left < ParseAdditive() ? 1 : 0;
                    else if (Match(">"))
                        left = left > ParseAdditive() ? 1 : 0;
                    else
                        return left;
                }
            }

            private long ParseAdditive()
            {
                var left = ParseMultiplicative();
                for (;;)
                {
                    if (Match("+"))
                        left += ParseMultiplicative();
                    else if (Match("-"))
                        left -= ParseMultiplicative();
                    else
                        return left;
                }
            }

            private long ParseMultiplicative()
            {
                var left = ParseUnary();
                for (;;)
                {
                    if (Match("*"))
                        left *= ParseUnary();
                    else if (Match("/"))
                    {
                        var right = ParseUnary();
                        left = right == 0 ? 0 : left / right;
                    }
                    else if (Match("%"))
                    {
                        var right = ParseUnary();
                        left = right == 0 ? 0 : left % right;
                    }
                    else
                        return left;
                }
            }

            private long ParseUnary()
            {
                if (Match("!"))
                    return ParseUnary() == 0 ? 1 : 0;
                if (Match("-"))
                    return -ParseUnary();
                if (Match("+"))
                    return ParseUnary();
                return ParsePrimary();
            }

            private long ParsePrimary()
            {
                SkipWhitespace();
                if (Match("("))
                {
                    var value = ParseOr();
                    Match(")");
                    return value;
                }

                if (_index < _text.Length && char.IsDigit(_text[_index]))
                    return ReadNumber();

                var identifier = ReadIdentifier(_text, _index, out var next);
                if (identifier.Length == 0)
                    return 0;

                _index = next;
                if (identifier == "defined")
                    return ReadDefined() ? 1 : 0;

                if (_macros.TryGetValue(identifier, out var macro) && !macro.IsFunctionLike)
                {
                    if (long.TryParse(macro.Body.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        return value;
                    if (macro.Body.Trim().Length == 0)
                        return 1;
                }

                return 0;
            }

            private bool ReadDefined()
            {
                SkipWhitespace();
                if (Match("("))
                {
                    var name = ReadIdentifier(_text, _index, out _index);
                    Match(")");
                    return _macros.ContainsKey(name);
                }

                var identifier = ReadIdentifier(_text, _index, out _index);
                return _macros.ContainsKey(identifier);
            }

            private long ReadNumber()
            {
                var start = _index;
                while (_index < _text.Length && char.IsDigit(_text[_index]))
                    _index++;
                return long.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
            }

            private bool Match(string token)
            {
                SkipWhitespace();
                if (_index + token.Length > _text.Length)
                    return false;
                if (string.CompareOrdinal(_text, _index, token, 0, token.Length) != 0)
                    return false;
                _index += token.Length;
                return true;
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                    _index++;
            }
        }
    }
}
