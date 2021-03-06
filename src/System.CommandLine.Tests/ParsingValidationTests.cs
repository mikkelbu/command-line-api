﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace System.CommandLine.Tests
{
    public class ParsingValidationTests
    {
        private readonly ITestOutputHelper _output;

        public ParsingValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void
            When_an_option_accepts_only_specific_arguments_but_a_wrong_one_is_supplied_then_an_informative_error_is_returned()
        {
            var parser = new Parser(
                new Option(
                    "-x",
                    "",
                    new Argument
                        {
                            Arity = ArgumentArity.ExactlyOne 
                        }
                        .FromAmong("this", "that", "the-other-thing")));

            var result = parser.Parse("-x none-of-those");

            result.Errors
                  .Should()
                  .Contain(e => e.Message == "Required argument missing for option: -x");
        }

        [Fact]
        public void When_an_option_has_en_error_then_the_error_has_a_reference_to_the_option()
        {
            var option = new Option(
                "-x",
                "",
                new Argument
                {
                    Arity = ArgumentArity.ExactlyOne
                }.FromAmong("this", "that"));

            var parser = new Parser(option);

            var result = parser.Parse("-x something_else");

            result.Errors
                  .Where(e => e.SymbolResult != null)
                  .Should()
                  .Contain(e => e.SymbolResult.Name == option.Name);
        }

        [Fact]
        public void When_a_required_argument_is_not_supplied_then_an_error_is_returned()
        {
            var parser = new Parser(new Option(
                                        "-x",
                                        "",
                                        new Argument
                                        {
                                            Arity = ArgumentArity.ExactlyOne
                                        }));

            var result = parser.Parse("-x");

            result.Errors
                  .Should()
                  .Contain(e => e.Message == "Required argument missing for option: -x");
        }

        [Fact]
        public void When_no_option_accepts_arguments_but_one_is_supplied_then_an_error_is_returned()
        {
            var parser = new Parser(
                new Command("the-command", "",
                            new[]
                            {
                                new Option("-x", "")
                            }, Argument.None));

            var result = parser.Parse("the-command -x some-arg");

            _output.WriteLine(result.ToString());

            result.Errors
                  .Select(e => e.Message)
                  .Should()
                  .ContainSingle(e => e == "Unrecognized command or argument 'some-arg'");
        }

        [Fact]
        public void An_option_can_be_invalid_when_used_in_combination_with_another_option()
        {
            var argument = new Argument();
            argument.AddValidator(symbol => {
                if (symbol.Children.Contains("one") &&
                    symbol.Children.Contains("two"))
                {
                    return "Options '--one' and '--two' cannot be used together.";
                }

                return null;
            });

            var command = new Command("the-command", "", new[] {
                new Option("--one", ""),
                new Option("--two", "")
            }, argument);

            var result = command.Parse("the-command --one --two");

            result
                .Errors
                .Select(e => e.Message)
                .Should()
                .Contain("Options '--one' and '--two' cannot be used together.");
        }

        [Fact]
        public void LegalFilePathsOnly_rejects_arguments_containing_invalid_path_characters()
        {
            var command = new Command("the-command", "", 
                                      argument: new Argument
                                      {
                                          Arity = ArgumentArity.ZeroOrMore
                                      }.LegalFilePathsOnly());

            var invalidCharacters = $"|{Path.GetInvalidPathChars().First()}|";

            // Convert to ushort so the xUnit XML writer doesn't complain about invalid characters
            _output.WriteLine(string.Join("\n", Path.GetInvalidPathChars().Select(c => (ushort)c)));

            var result = command.Parse($"the-command {invalidCharacters}");

            result.UnmatchedTokens
                  .Should()
                  .BeEquivalentTo(invalidCharacters);
        }

        [Fact]
        public void LegalFilePathsOnly_accepts_arguments_containing_valid_path_characters()
        {
            var command = new Command("the-command", "",
                                      argument: new Argument
                                      {
                                          Arity = ArgumentArity.ZeroOrMore
                                      }.LegalFilePathsOnly());

            var validPathName = Directory.GetCurrentDirectory();
            var validNonExistingFileName = Path.Combine(validPathName, Guid.NewGuid().ToString());

            var result = command.Parse($"the-command {validPathName} {validNonExistingFileName}");

            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void An_argument_can_be_invalid_based_on_file_existence()
        {
            var command = new Command(
                "move",
                argument: new Argument
                          {
                              Arity = ArgumentArity.ExactlyOne
                          }.ExistingFilesOnly());
            command.AddOption(
                new Option(
                    "--to",
                    argument: new Argument
                              {
                                  Arity = ArgumentArity.ExactlyOne
                              }));

            var result =
                command.Parse(
                    $@"move ""{Guid.NewGuid()}.txt"" --to ""{Path.Combine(Directory.GetCurrentDirectory(), ".trash")}""");

            result.CommandResult
                  .Arguments
                  .Should()
                  .BeEmpty();
        }

        [Fact]
        public void An_argument_can_be_invalid_based_on_directory_existence()
        {
            var command = new Command(
                "move",
                argument: new Argument
                          {
                              Arity = ArgumentArity.ExactlyOne
                          }.ExistingFilesOnly());
            command.AddOption(
                new Option("--to",
                           argument: new Argument
                                     {
                                         Arity = ArgumentArity.ExactlyOne
                                     }));

            var currentDirectory = Directory.GetCurrentDirectory();
            var trash = Path.Combine(currentDirectory, ".trash");

            var commandLine = $@"move ""{currentDirectory}"" --to ""{trash}""";

            var result = command.Parse(commandLine);

            _output.WriteLine(result.Diagram());

            result.CommandResult
                  .Arguments
                  .Should()
                  .BeEquivalentTo(currentDirectory);
        }

        [Fact]
        public void A_command_with_subcommands_is_invalid_to_invoke_if_it_has_no_handler()
        {
            var outer = new Command("outer");
            var inner = new Command("inner");
            var innerer = new Command("inner-er");
            outer.AddCommand(inner);
            inner.AddCommand(innerer);

            var result = outer.Parse("outer inner arg");

            result.Errors
                  .Should()
                  .ContainSingle(
                      e => e.Message.Equals(ValidationMessages.Instance.RequiredCommandWasNotProvided()) &&
                           e.SymbolResult.Name.Equals("inner"));
        }

        [Fact]
        public void A_command_with_subcommands_is_valid_to_invoke_if_it_has_a_handler()
        {
            var outer = new Command("outer");
            var inner = new Command("inner")
                        {
                            Handler = CommandHandler.Create(() =>
                            {
                            })
                        };
            var innerer = new Command("inner-er");
            outer.AddCommand(inner);
            inner.AddCommand(innerer);

            var result = outer.Parse("outer inner");

            result.Errors.Should().BeEmpty();
            result.CommandResult.Command.Should().Be(inner);
        }

        [Fact]
        public void
            When_an_option_is_specified_more_than_once_but_only_allowed_once_then_an_informative_error_is_returned()
        {
            var parser = new Parser(
                new Option(
                    "-x",
                    "",
                    new Argument
                    {
                        Arity = ArgumentArity.ExactlyOne
                    }));

            var result = parser.Parse("-x 1 -x 2");

            result.Errors
                  .Select(e => e.Message)
                  .Should()
                  .Contain("Option '-x' expects a single argument but 2 were provided.");
        }

        [Fact]
        public void ParseArgumentsAs_with_arity_of_One_validates_against_extra_arguments()
        {
            var parser = new Parser(
                new Option(
                    "-x",
                    "",
                    new Argument<int>()));

            var result = parser.Parse("-x 1 -x 2");

            result.Errors
                  .Select(e => e.Message)
                  .Should()
                  .Contain("Option '-x' expects a single argument but 2 were provided.");
        }

        [Fact]
        public void When_an_option_has_a_default_value_it_is_not_valid_to_specify_the_option_without_an_argument()
        {
            var parser = new Parser(
                new Option(
                    "-x", "",
                    new Argument<int>(123)));

            var result = parser.Parse("-x");

            result.Errors
                  .Select(e => e.Message)
                  .Should()
                  .Contain("Required argument missing for option: -x");
        }

        [Fact]
        public void When_an_option_has_a_default_value_then_the_default_should_apply_if_not_specified()
        {
            var parser = new Parser(
                new Option(
                    "-x",
                    "",
                    new Argument<int>(123)),
                new Option(
                    "-y",
                    "",
                    new Argument<int>(456)));

            var result = parser.Parse("");

            result.Errors.Should().BeEmpty();
            result.RootCommandResult.ValueForOption("-x").Should().Be(123);
            result.RootCommandResult.ValueForOption("-y").Should().Be(456);
        }
    }
}
