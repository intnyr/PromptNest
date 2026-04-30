using FluentAssertions;

using PromptNest.Core.Variables;

namespace PromptNest.Core.Tests;

public sealed class VariableParserTests
{
    [Fact]
    public void ParseReturnsRequiredAndDefaultVariables()
    {
        var parser = new VariableParser();

        var variables = parser.Parse("Tone: {{tone|friendly}} Audience: {{audience}} Again: {{audience}}");

        variables.Should().HaveCount(2);
        variables.Should().Contain(variable => variable.Name == "tone" && variable.DefaultValue == "friendly");
        variables.Should().Contain(variable => variable.Name == "audience" && variable.IsRequired);
    }

    [Fact]
    public void ParseIgnoresInvalidPlaceholdersWithoutThrowing()
    {
        var parser = new VariableParser();

        var variables = parser.Parse("{{1bad}} {{missing {{valid_name}} {{also-bad}}");

        variables.Should().ContainSingle(variable => variable.Name == "valid_name");
    }

    [Fact]
    public void ParseHandlesMixedPromptBodiesAndDuplicateVariablesCaseInsensitively()
    {
        var parser = new VariableParser();

        var variables = parser.Parse("Intro {{Industry}} then {{industry|SaaS}} and {{tone|friendly}}.");

        variables.Should().HaveCount(2);
        variables.Should().Contain(variable => variable.Name == "Industry");
        variables.Should().Contain(variable => variable.Name == "tone" && variable.DefaultValue == "friendly");
    }
}