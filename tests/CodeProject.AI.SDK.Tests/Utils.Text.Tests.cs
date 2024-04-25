using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.SDK.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void EqualsIgnoreCaseTest()
        {
            Assert.True("test".EqualsIgnoreCase("TEST"));
            Assert.False("test".EqualsIgnoreCase("TEST1"));
            Assert.True(((string?)null).EqualsIgnoreCase(null));
            Assert.False("test".EqualsIgnoreCase(null));
        }

        [Fact]
        public void ContainsIgnoreCaseTest()
        {
            Assert.True("test".ContainsIgnoreCase("ES"));
            Assert.False("test".ContainsIgnoreCase("ES1"));
            Assert.True(((string?)null).ContainsIgnoreCase(null));
            Assert.False("test".ContainsIgnoreCase(null));
        }

        [Fact]
        public void StartsWithIgnoreCaseTest()
        {
            Assert.True("test".StartsWithIgnoreCase("TE"));
            Assert.False("test".StartsWithIgnoreCase("TE1"));
            Assert.True(((string?)null).StartsWithIgnoreCase(null));
            Assert.False("test".StartsWithIgnoreCase(null));
        }
    }

    public class TextTests
    {
        [Fact]
        public void StripXTermColorsTest()
        {
            string text = "\u001b[31mHello World\u001b[0m";
            Assert.Equal("Hello World", Text.StripXTermColors(text));
        }

        [Fact]
        public void StripSpinnerCharsTest()
        {
            string text = "-\bHello World";
            Assert.Equal("Hello World", Text.StripSpinnerChars(text));
            text = "/\bHello World";
            Assert.Equal("Hello World", Text.StripSpinnerChars(text)); 
            text = "-\bHello World";
            Assert.Equal("Hello World", Text.StripSpinnerChars(text));
            text = "\\\bHello World";
            Assert.Equal("Hello World", Text.StripSpinnerChars(text));
        }

        [Fact]
        public void FixSlashesTest()
        {
            string path = "test\\path";
            Assert.Equal("test" + Path.DirectorySeparatorChar + "path", Text.FixSlashes(path));
            path = "test/path";
            Assert.Equal("test" + Path.DirectorySeparatorChar + "path", Text.FixSlashes(path));
        }

        [Fact]
        public void ShrinkPathTest()
        {
            string path = "test/path/to/some/file.txt";
            Assert.Equal("test" + Path.DirectorySeparatorChar + "p...le.txt", Text.ShrinkPath(path, 15));
            Assert.Equal("test" + Path.DirectorySeparatorChar + "path...file.txt", Text.ShrinkPath(path, 20));
        }
    }
}