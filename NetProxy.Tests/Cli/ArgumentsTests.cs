using NetProxy.Cli;
using Xunit;

namespace NetProxy.Tests.Cli
{
    public class ArgumentsTests
    {
        [Fact]
        public void Words_and_options_are_told_apart()
        {
            var arguments = new Arguments(["proxy", "apply", "-f", "web.json", "--dry-run", "name"]);

            Assert.Equal(["proxy", "apply", "name"], arguments.Words);
            Assert.Equal("web.json", arguments.Get("file"));
            Assert.True(arguments.Has("dry-run"));
        }

        [Fact]
        public void An_option_can_be_written_with_an_equals_sign_and_the_value_can_be_empty()
        {
            var arguments = new Arguments(["login", "--user=admin", "--password="]);

            Assert.Equal("admin", arguments.Get("user"));
            Assert.Equal("", arguments.Get("password"));
        }

        [Fact]
        public void Short_names_stand_for_long_ones()
        {
            var arguments = new Arguments(["logs", "-n", "20", "-o", "out.json", "-s", "host", "-u", "me"]);

            Assert.Equal("20", arguments.Get("lines"));
            Assert.Equal("out.json", arguments.Get("output"));
            Assert.Equal("host", arguments.Get("server"));
            Assert.Equal("me", arguments.Get("user"));
        }

        [Fact]
        public void A_single_dash_is_the_standard_input_and_not_an_option()
        {
            var arguments = new Arguments(["proxy", "apply", "-f", "-"]);

            Assert.Equal("-", arguments.Get("file"));
            Assert.Equal(["proxy", "apply"], arguments.Words);
        }

        [Fact]
        public void Option_names_are_not_case_sensitive()
        {
            var arguments = new Arguments(["--DRY-RUN", "--Server", "h"]);

            Assert.True(arguments.Has("dry-run"));
            Assert.Equal("h", arguments.Get("server"));
        }

        [Fact]
        public void An_option_that_needs_a_value_and_has_none_is_a_usage_error()
            => Assert.Throws<UsageException>(() => new Arguments(["proxy", "apply", "-f"]));

        [Fact]
        public void A_missing_word_is_a_usage_error_that_says_what_is_missing()
        {
            var arguments = new Arguments(["proxy"]);

            var error = Assert.Throws<UsageException>(() => arguments.Word(1, "proxy command"));
            Assert.Contains("proxy command", error.Message);
        }

        [Fact]
        public void An_option_that_is_not_there_is_null_and_a_flag_that_is_not_there_is_false()
        {
            var arguments = new Arguments(["status"]);

            Assert.Null(arguments.Get("server"));
            Assert.False(arguments.Has("json"));
        }
    }
}
