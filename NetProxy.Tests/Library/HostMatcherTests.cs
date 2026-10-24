using NetProxy.Library.Utilities;
using Xunit;

namespace NetProxy.Tests.Library
{
    public class HostMatcherTests
    {
        [Theory]
        [InlineData("www.example.com", "www.example.com")]
        [InlineData("  WWW.Example.COM  ", "www.example.com")]
        [InlineData("www.example.com.", "www.example.com")]
        [InlineData("example.com:8443", "example.com")]
        [InlineData("[::1]:443", "[::1]")]
        [InlineData("[2001:db8::1]", "[2001:db8::1]")]
        [InlineData("::1", "::1")]
        public void Normalize_gives_one_form_for_a_name(string input, string expected)
            => Assert.Equal(expected, NpHostMatcher.Normalize(input));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".")]
        public void Normalize_gives_null_when_there_is_no_name(string? input)
            => Assert.Null(NpHostMatcher.Normalize(input));

        [Theory]
        [InlineData("example.com")]
        [InlineData("www.example.com")]
        [InlineData("*.example.com")]
        [InlineData("a-b.example.com")]
        [InlineData("under_score.example.com")]
        [InlineData("localhost")]
        [InlineData("xn--bcher-kva.example")]
        public void IsValidPattern_accepts_names_and_leading_wildcards(string pattern)
            => Assert.True(NpHostMatcher.IsValidPattern(pattern));

        [Theory]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("*.")]
        [InlineData("a*.example.com")]
        [InlineData("www.*.com")]
        [InlineData("*.*.example.com")]
        [InlineData("-a.example.com")]
        [InlineData("a-.example.com")]
        [InlineData("a..example.com")]
        [InlineData("a b.example.com")]
        [InlineData("a/b.example.com")]
        public void IsValidPattern_rejects_the_rest(string pattern)
            => Assert.False(NpHostMatcher.IsValidPattern(pattern));

        [Fact]
        public void IsValidPattern_rejects_labels_and_names_that_are_too_long()
        {
            Assert.False(NpHostMatcher.IsValidPattern(new string('a', 64) + ".com"));
            Assert.True(NpHostMatcher.IsValidPattern(new string('a', 63) + ".com"));
            Assert.False(NpHostMatcher.IsValidPattern(string.Join(".", Enumerable.Repeat(new string('a', 60), 5))));
        }

        [Theory]
        [InlineData("example.com", "example.com", true)]
        [InlineData("example.com", "www.example.com", false)]
        [InlineData("*.example.com", "www.example.com", true)]
        [InlineData("*.example.com", "a.b.example.com", true)]
        [InlineData("*.example.com", "example.com", false)]
        [InlineData("*.example.com", "badexample.com", false)]
        [InlineData("*.example.com", "www.example.org", false)]
        public void Matches_follows_the_rules_of_exact_names_and_wildcards(string pattern, string host, bool expected)
            => Assert.Equal(expected, NpHostMatcher.Matches(pattern, host));

        [Fact]
        public void An_exact_name_is_more_specific_than_any_wildcard_and_a_longer_wildcard_than_a_shorter()
        {
            Assert.True(NpHostMatcher.Specificity("www.example.com") > NpHostMatcher.Specificity("*.example.com"));
            Assert.True(NpHostMatcher.Specificity("*.b.example.com") > NpHostMatcher.Specificity("*.example.com"));
        }

        [Fact]
        public void FindBest_picks_the_most_specific_match()
        {
            string[] patterns = ["*.example.com", "*.b.example.com", "www.b.example.com", "other.com"];

            Assert.Equal("www.b.example.com", NpHostMatcher.FindBest(patterns, "www.b.example.com"));
            Assert.Equal("*.b.example.com", NpHostMatcher.FindBest(patterns, "x.b.example.com"));
            Assert.Equal("*.example.com", NpHostMatcher.FindBest(patterns, "x.example.com"));
            Assert.Null(NpHostMatcher.FindBest(patterns, "example.net"));
        }
    }
}
