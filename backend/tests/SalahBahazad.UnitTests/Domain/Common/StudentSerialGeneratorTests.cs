using FluentAssertions;
using SalahBahazad.Domain.Common;

namespace SalahBahazad.UnitTests.Domain.Common;

public class StudentSerialGeneratorTests
{
    // STU- followed by exactly 6 Crockford base32 chars (digits + letters, no ambiguous I/L/O/U).
    private const string SerialRegex = "^STU-[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$";

    [Fact]
    public void Next_returns_STU_prefixed_crockford_serial_of_length_six()
    {
        // Many draws: asserts both the I/L/O/U exclusion and the length-6 pick never produce an out-of-alphabet char.
        for (var i = 0; i < 500; i++)
            StudentSerialGenerator.Next().Should().MatchRegex(SerialRegex);
    }

    [Fact]
    public void NextUnique_skips_values_already_in_the_set()
    {
        var taken = new HashSet<string>();

        var first = StudentSerialGenerator.NextUnique(taken);
        var second = StudentSerialGenerator.NextUnique(taken);

        first.Should().NotBe(second);
        taken.Should().Contain(first).And.Contain(second);
    }

    [Fact]
    public void NextUnique_throws_after_maxAttempts()
    {
        // maxAttempts: 0 → the loop body never runs, so it can never produce a serial → throws (the contract guard).
        var act = () => StudentSerialGenerator.NextUnique(new HashSet<string>(), maxAttempts: 0);

        act.Should().Throw<InvalidOperationException>();
    }
}
