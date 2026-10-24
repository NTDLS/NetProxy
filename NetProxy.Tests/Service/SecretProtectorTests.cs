using NetProxy.Service;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class SecretProtectorTests
    {
        [Fact]
        public void A_secret_is_protected_and_comes_back()
        {
            var protectedValue = SecretProtector.Protect("p@ssw0rd é中")!;

            Assert.NotEqual("p@ssw0rd é中", protectedValue);
            Assert.DoesNotContain("p@ssw0rd", protectedValue);
            Assert.True(SecretProtector.IsProtected(protectedValue));
            Assert.Equal("p@ssw0rd é中", SecretProtector.Unprotect(protectedValue, "test", out var plain));
            Assert.False(plain);
        }

        [Fact]
        public void Protecting_twice_gives_the_same_value_and_not_a_second_layer()
        {
            var once = SecretProtector.Protect("secret")!;
            Assert.Equal(once, SecretProtector.Protect(once));
        }

        [Fact]
        public void Each_protection_of_the_same_secret_can_differ_but_both_read_back()
        {
            var first = SecretProtector.Protect("secret")!;
            var second = SecretProtector.Protect("secret")!;

            Assert.Equal("secret", SecretProtector.Unprotect(first, "a", out _));
            Assert.Equal("secret", SecretProtector.Unprotect(second, "b", out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Nothing_is_left_as_nothing(string? value)
        {
            Assert.Equal(value, SecretProtector.Protect(value));
            Assert.Equal(value, SecretProtector.Unprotect(value, "test", out var plain));
            Assert.False(plain);
        }

        [Fact]
        public void A_value_that_is_not_protected_is_read_as_it_is_and_reported_so_it_can_be_saved_protected()
        {
            Assert.Equal("typed by hand", SecretProtector.Unprotect("typed by hand", "test", out var plain));
            Assert.True(plain);
        }

        [Fact]
        public void A_value_that_cannot_be_decrypted_is_dropped_never_turned_into_something_that_matches()
        {
            Assert.Null(SecretProtector.Unprotect("enc:aes:not-base64-at-all!!", "test", out _));
            Assert.Null(SecretProtector.Unprotect("enc:dpapi:AAAA", "test", out _));

            var tampered = SecretProtector.Protect("secret")!;
            tampered = tampered[..^6] + (tampered[^6] == 'A' ? "B" : "A") + tampered[^5..];
            Assert.Null(SecretProtector.Unprotect(tampered, "test", out _));
        }
    }
}
