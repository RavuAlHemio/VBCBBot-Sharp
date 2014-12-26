using System.Text;
using VBCBBot;
using Xunit;

namespace BotTests
{
    public class UrlDecodingTests
    {
        private void Test(string unplussedResult, string plussedResult, string input)
        {
            var inputBytes = Util.Utf8NoBom.GetBytes(input);
            Assert.Equal(unplussedResult, Util.UrlDecodeToString(inputBytes, Util.Utf8NoBom, plusIsSpace: false));
            Assert.Equal(plussedResult, Util.UrlDecodeToString(inputBytes, Util.Utf8NoBom, plusIsSpace: true));
        }

        [Fact]
        public void EmptyString()
        {
            Test("", "", "");
        }

        [Fact]
        public void ShortString()
        {
            Test("abc", "abc", "abc");
        }

        [Fact]
        public void StringWithPluses()
        {
            Test("one+two+three", "one two three", "one+two+three");
        }
    }
}
