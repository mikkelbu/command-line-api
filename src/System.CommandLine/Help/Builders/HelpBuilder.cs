﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static System.CommandLine.DefaultHelpText;

namespace System.CommandLine
{
    public class HelpBuilder : IHelpBuilder
    {
        protected const int DefaultColumnGutter = 4;
        protected const int DefaultIndentationSize = 2;
        protected const int DefaultWindowWidth = 80;

        protected const int WindowMargin = 2;
        private int _indentationLevel;
        protected IConsole _console;

        public int ColumnGutter { get; } 
        public int IndentationSize { get; } 
        public int MaxWidth { get; } 

        /// <summary>
        /// Brokers the generation and output of help text of <see cref="Symbol"/>
        /// and the <see cref="IConsole"/>
        /// </summary>
        /// <param name="console"><see cref="IConsole"/> instance to write the help text output</param>
        /// <param name="columnGutter">
        /// Number of characters to pad invocation information from their descriptions
        /// </param>
        /// <param name="indentationSize">Number of characters to indent new lines</param>
        /// <param name="maxWidth">
        /// Maximum number of characters available for each line to write to the console
        /// </param>
        public HelpBuilder(
            IConsole console,
            int? columnGutter = null,
            int? indentationSize = null,
            int? maxWidth = null)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
            ColumnGutter = columnGutter ?? DefaultColumnGutter;
            IndentationSize = indentationSize ?? DefaultIndentationSize;
            MaxWidth = maxWidth ?? GetWindowWidth();
        }

        /// <inheritdoc />
        public void Write(ICommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            AddSynopsis(command);
            AddUsage(command);
            AddArguments(command);
            AddOptions(command);
            AddSubcommands(command);
            AddAdditionalArguments(command);
        }

        protected int CurrentIndentation => _indentationLevel * IndentationSize;

        /// <summary>
        /// Increases the current indentation level
        /// </summary>
        protected void Indent(int levels = 1)
        {
            _indentationLevel += levels;
        }

        /// <summary>
        /// Decreases the current indentation level
        /// </summary>
        protected void Outdent(int levels = 1)
        {
            if (_indentationLevel == 0)
            {
                throw new InvalidOperationException("Cannot outdent any further");
            }

            _indentationLevel -= levels;
        }

        /// <summary>
        /// Gets the currently available space based on the <see cref="MaxWidth"/> from the window
        /// and the current indentation level
        /// </summary>
        /// <returns>The number of characters available on the current line</returns>
        protected int GetAvailableWidth()
        {
            return MaxWidth - CurrentIndentation - WindowMargin;
        }

        /// <summary>
        /// Create a string of whitespace for the supplied number of characters
        /// </summary>
        /// <param name="width">The length of whitespace required</param>
        /// <returns>A string of <see cref="width"/> whitespace characters</returns>
        protected static string GetPadding(int width)
        {
            return new string(' ', width);
        }

        /// <summary>
        /// Writes a blank line to the console
        /// </summary>
        private void AppendBlankLine()
        {
            _console.Out.WriteLine();
        }


        /// <summary>
        /// Writes whitespace to the console based on the provided offset,
        /// defaulting to the <see cref="CurrentIndentation"/>
        /// </summary>
        /// <param name="offset">Number of characters to pad</param>
        private void AppendPadding(int? offset = null)
        {
            var padding = GetPadding(offset ?? CurrentIndentation);
            _console.Out.Write(padding);
        }

        /// <summary>
        /// Writes a new line of text to the console, padded with a supplied offset
        /// defaulting to the <see cref="CurrentIndentation"/>
        /// </summary>
        /// <param name="text">The text content to write to the console</param>
        /// <param name="offset">Number of characters to pad the text</param>
        private void AppendLine(string text, int? offset = null)
        {
            AppendPadding(offset);
            _console.Out.WriteLine(text ?? "");
        }

        /// <summary>
        /// Writes text to the console, padded with a supplied offset
        /// </summary>
        /// <param name="text">Text content to write to the console</param>
        /// <param name="offset">Number of characters to pad the text</param>
        private void AppendText(string text, int? offset = null)
        {
            AppendPadding(offset);
            _console.Out.Write(text ?? "");
        }

        /// <summary>
        /// Writes heading text to the console.
        /// </summary>
        /// <param name="heading">Heading text content to write to the console</param>
        /// <exception cref="ArgumentNullException"></exception>
        protected virtual void AppendHeading(string heading)
        {
            if (heading == null)
            {
                throw new ArgumentNullException(nameof(heading));
            }

            AppendLine(heading);
        }

        /// <summary>
        /// Writes a description block to the console
        /// </summary>
        /// <param name="description">Description text to write to the console</param>
        /// <exception cref="ArgumentNullException"></exception>
        protected virtual void AppendDescription(string description)
        {
            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            var availableWidth = GetAvailableWidth();
            var descriptionLines = SplitText(description, availableWidth);

            foreach (var descriptionLine in descriptionLines)
            {
                AppendLine(descriptionLine, CurrentIndentation);
            }
        }

        /// <summary>
        /// Adds columnar content for a <see cref="HelpItem"/> using the current indentation
        /// for the line, and adding the appropriate padding between the columns
        /// </summary>
        /// <param name="helpItem">
        /// Current <see cref="HelpItem" /> to write to the console
        /// </param>
        /// <param name="maxInvocationWidth">
        /// Maximum number of characters accross all <see cref="HelpItem">help items</see>
        /// occupied by the invocation text
        /// </param>
        protected void AppendHelpItem(HelpItem helpItem, int maxInvocationWidth)
        {
            if (helpItem == null)
            {
                throw new ArgumentNullException(nameof(helpItem));
            }

            AppendText(helpItem.Invocation, CurrentIndentation);

            var offset = maxInvocationWidth + ColumnGutter - helpItem.Invocation.Length;
            var availableWidth = GetAvailableWidth();
            var maxDescriptionWidth = availableWidth - maxInvocationWidth - ColumnGutter;

            var descriptionLines = SplitText(helpItem.Description, maxDescriptionWidth);
            var lineCount = descriptionLines.Count;

            AppendLine(descriptionLines.FirstOrDefault(), offset);

            if (lineCount == 1)
            {
                return;
            }

            offset = CurrentIndentation + maxInvocationWidth + ColumnGutter;

            foreach (var descriptionLine in descriptionLines.Skip(1))
            {
                AppendLine(descriptionLine, offset);
            }
        }

        /// <summary>
        /// Takes a string of text and breaks it into lines of <see cref="maxLength"/>
        /// characters. This does not preserve any formatting of the incoming text.
        /// </summary>
        /// <param name="text">Text content to split into writable lines</param>
        /// <param name="maxLength">
        /// Maximum number of characters allowed for writing the supplied <see cref="text"/>
        /// </param>
        /// <returns>
        /// Collection of lines of at most <see cref="maxLength"/> characters
        /// generated from the supplied <see cref="text"/>
        /// </returns>
        protected virtual IReadOnlyCollection<string> SplitText(string text, int maxLength)
        {
            var cleanText = Regex.Replace(text, "\\s+", " ");
            var textLength = cleanText.Length;

            if (string.IsNullOrWhiteSpace(cleanText) || textLength < maxLength)
            {
                return new[] {cleanText};
            }

            var lines = new List<string>();
            var builder = new StringBuilder();

            foreach (var item in cleanText.Split(new char[0], StringSplitOptions.RemoveEmptyEntries))
            {
                var length = item.Length + builder.Length;

                if (length >= maxLength)
                {
                    lines.Add(builder.ToString());
                    builder.Clear();
                }

                if (builder.Length > 0)
                {
                    builder.Append(" ");
                }

                builder.Append(item);
            }

            if (builder.Length > 0)
            {
                lines.Add(builder.ToString());
            }

            return lines;
        }

        /// <summary>
        /// Formats the help rows for a given argument
        /// </summary>
        /// <param name="commandDef"></param>
        /// <returns>A new <see cref="HelpItem"/></returns>
        protected virtual HelpItem ArgumentFormatter(ISymbol commandDef)
        {
            return new HelpItem {
                Invocation = $"<{commandDef?.Argument?.Name}>",
                Description = commandDef?.Argument?.Description ?? "",
            };
        }

        /// <summary>
        /// Formats the help rows for a given option
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns>A new <see cref="HelpItem"/></returns>
        protected virtual HelpItem OptionFormatter(ISymbol symbol)
        {
            var rawAliases = symbol.RawAliases
                .OrderBy(alias => alias.Length);

            var option = string.Join(", ", rawAliases);

            if (symbol?.ShouldShowHelp() == true && 
                !string.IsNullOrWhiteSpace(symbol.Argument?.Name))
            {
                option = $"{option} <{symbol.Argument?.Name}>";
            }

            return new HelpItem {
                Invocation = option,
                Description = symbol.Description ??  ""
            };
        }

        /// <summary>
        /// Writes a summary, if configured, for the supplied <see cref="command"/>
        /// </summary>
        /// <param name="command"></param>
        protected virtual void AddSynopsis(ICommand command)
        {
            if (!command.ShouldShowHelp())
            {
                return;
            }

            var title = $"{command.Name}:";
            HelpSection.Write(this, title, command.Description);
        }

        /// <summary>
        /// Writes the usage summary for the supplied <see cref="command"/>
        /// </summary>
        /// <param name="command"></param>
        protected virtual void AddUsage(ICommand command)
        {
            var usage = new List<string>();

            var subcommands = command
                .RecurseWhileNotNull(commandDef => commandDef.Parent)
                .Reverse();

            foreach (var subcommand in subcommands)
            {
                usage.Add(subcommand.Name);

                if (subcommand != command &&
                    ShouldDisplayArgumentHelp(subcommand, out var subcommandArgName))
                {
                    usage.Add($"<{subcommandArgName}>");
                }
            }

            var hasOptionHelp = command.Children
                .OfType<IOption>()
                .Any(symbolDef => symbolDef.ShouldShowHelp());

            if (hasOptionHelp)
            {
                usage.Add(Usage.Options);
            }

            if (ShouldDisplayArgumentHelp(command, out var commandArgName))
            {
                usage.Add($"<{commandArgName}>");
            }

            var hasCommandHelp = command.Children
                .OfType<ICommand>()
                .Any(f => f.ShouldShowHelp());

            if (hasCommandHelp)
            {
                usage.Add(Usage.Command);
            }

            if (!command.TreatUnmatchedTokensAsErrors)
            {
                usage.Add(Usage.AdditionalArguments);
            }

            HelpSection.Write(this, Usage.Title, string.Join(" ", usage));
        }

        /// <summary>
        /// Writes the arguments, if any, for the supplied <see cref="command"/>
        /// </summary>
        /// <param name="command"></param>
        protected virtual void AddArguments(ICommand command)
        {
            var commands = new List<ICommand>();

            if (ShouldDisplayArgumentHelp(command.Parent, out var _))
            {
                commands.Add(command.Parent);
            }

            if (ShouldDisplayArgumentHelp(command, out var _))
            {
                commands.Add(command);
            }

            HelpSection.Write(this, Arguments.Title, commands, ArgumentFormatter);
        }

        /// <summary>
        /// Writes the <see cref="Option"/> help content, if any,
        /// for the supplied <see cref="command"/>
        /// </summary>
        /// <param name="command"></param>
        protected virtual void AddOptions(ICommand command)
        {
            var options = command
                .Children
                .OfType<IOption>()
                .Where(opt => opt.ShouldShowHelp())
                .ToArray();

            HelpSection.Write(this, Options.Title, options, OptionFormatter);
        }

        /// <summary>
        /// Writes the help content of the <see cref="Command"/> subcommands, if any,
        /// for the supplied <see cref="command"/>
        /// </summary>
        /// <param name="command"></param>
        protected virtual void AddSubcommands(ICommand command)
        {
            var subcommands = command
                .Children
                .OfType<ICommand>()
                .Where(subCommand => subCommand.ShouldShowHelp())
                .ToArray();

            HelpSection.Write(this, Commands.Title, subcommands, OptionFormatter);
        }

        protected virtual void AddAdditionalArguments(ICommand command)
        {
            if (command.TreatUnmatchedTokensAsErrors)
            {
                return;
            }

            HelpSection.Write(this, AdditionalArguments.Title, AdditionalArguments.Description);
        }

        private static bool ShouldDisplayArgumentHelp(
            ISymbol symbol,
            out string name)
        {
            if (symbol?.Argument?.ShouldShowHelp() != true ||
                string.IsNullOrWhiteSpace(symbol?.Argument?.Name))
            {
                name = null;
                return false;
            }

            name =  symbol.Argument.Name;
            return true;
        }

        /// <summary>
        /// Gets the number of characters of the current <see cref="IConsole"/> window if necessary
        /// </summary>
        /// <returns>
        /// The current width (number of characters) of the configured <see cref="IConsole"/>,
        /// or the <see cref="DefaultWindowWidth"/> if unavailable
        /// </returns>
        private int GetWindowWidth()
        {
            try
            {
                if (_console is ITerminal terminal)
                {
                    return terminal.GetRegion().Width;
                }
                else
                {
                    return int.MaxValue;
                }
            }
            catch (Exception exception) when (exception is ArgumentOutOfRangeException || exception is IOException)
            {
                return DefaultWindowWidth;
            }
        }

        protected class HelpItem
        {
            public string Invocation { get; set; }

            public string Description { get; set; }
        }

        private static class HelpSection
        {
            public static void Write(
                HelpBuilder builder,
                string title,
                string description)
            {
                if (!ShouldWrite(description, null))
                {
                    return;
                }

                AppendHeading(builder, title);
                builder.Indent();
                AddDescription(builder, description);
                builder.Outdent();
                builder.AppendBlankLine();
            }

            public static void Write(
                HelpBuilder builder,
                string title,
                IReadOnlyCollection<ISymbol> usageItems = null,
                Func<ISymbol, HelpItem> formatter = null,
                string description = null)
            {
                if (!ShouldWrite(description, usageItems))
                {
                    return;
                }

                AppendHeading(builder, title);
                builder.Indent();
                AddDescription(builder, description);
                AddInvocation(builder, usageItems, formatter);
                builder.Outdent();
                builder.AppendBlankLine();
            }

            private static bool ShouldWrite(string description, IReadOnlyCollection<ISymbol> usageItems)
            {
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return true;
                }

                return usageItems?.Any() == true;
            }

            private static void AppendHeading(HelpBuilder builder, string title)
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    return;
                }

                builder.AppendHeading(title);
            }

            private static void AddDescription(HelpBuilder builder, string description)
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    return;
                }

                builder.AppendDescription(description);
            }

            private static void AddInvocation(
                HelpBuilder builder,
                IReadOnlyCollection<ISymbol> symbols,
                Func<ISymbol, HelpItem> formatter)
            {
                if (symbols?.Any() != true)
                {
                    return;
                }

                var helpItems = symbols
                    .Select(formatter).ToList();

                var maxWidth = helpItems
                    .Select(line => line.Invocation.Length)
                    .OrderByDescending(textLength => textLength)
                    .First();

                foreach (var helpItem in helpItems)
                {
                    builder.AppendHelpItem(helpItem, maxWidth);
                }
            }
        }
    }
}
