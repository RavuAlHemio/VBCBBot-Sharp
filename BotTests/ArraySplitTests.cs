using System.Linq;
using VBCBBot;
using Xunit;

namespace BotTests
{
    public class ArraySplitTests
    {
        [Fact]
        public void SplitEmptyArray()
        {
            var arr = new byte[0];
            var ret = arr.Split(new [] { (byte)0 }).ToList();

            Assert.Equal(1, ret.Count);
            Assert.Equal(0, ret[0].Length);
        }

        [Fact]
        public void SplitIntoTwo()
        {
            var arr = new byte[] { 1, 2, 3 };
            var ret = arr.Split(new [] { (byte)2 }).ToList();

            Assert.Equal(2, ret.Count);
            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[0][0]);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(3, ret[1][0]);
        }

        [Fact]
        public void SingleElementArrays()
        {
            var arr = new byte[] { 1, 2, 3, 2, 4, 2, 5 };
            var ret = arr.Split(new [] { (byte)2 }).ToList();

            Assert.Equal(4, ret.Count);
            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[0][0]);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(3, ret[1][0]);
            Assert.Equal(1, ret[2].Length);
            Assert.Equal(4, ret[2][0]);
            Assert.Equal(1, ret[3].Length);
            Assert.Equal(5, ret[3][0]);
        }

        [Fact]
        public void BeginsWithSplitter()
        {
            var arr = new byte[] { 2, 1, 2, 3 };
            var ret = arr.Split(new [] { (byte)2 }).ToList();

            Assert.Equal(3, ret.Count);
            Assert.Equal(0, ret[0].Length);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(1, ret[1][0]);
            Assert.Equal(1, ret[2].Length);
            Assert.Equal(3, ret[2][0]);
        }

        [Fact]
        public void EndsWithSplitter()
        {
            var arr = new byte[] { 1, 2, 3, 2 };
            var ret = arr.Split(new [] { (byte)2 }).ToList();

            Assert.Equal(3, ret.Count);
            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[0][0]);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(3, ret[1][0]);
            Assert.Equal(0, ret[2].Length);
        }

        [Fact]
        public void BeginsAndEndsWithSplitter()
        {
            var arr = new byte[] { 2, 1, 2, 3, 2 };
            var ret = arr.Split(new [] { (byte)2 }).ToList();

            Assert.Equal(4, ret.Count);
            Assert.Equal(0, ret[0].Length);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(1, ret[1][0]);
            Assert.Equal(1, ret[2].Length);
            Assert.Equal(3, ret[2][0]);
            Assert.Equal(0, ret[3].Length);
        }

        [Fact]
        public void ExactMaxCount()
        {
            var arr = new byte[] { 1, 2, 3, 2, 4 };
            var ret = arr.Split(new [] { (byte)2 }, 3).ToList();

            Assert.Equal(3, ret.Count);
            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[0][0]);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(3, ret[1][0]);
            Assert.Equal(1, ret[2].Length);
            Assert.Equal(4, ret[2][0]);
        }

        [Fact]
        public void LowerMaxCount()
        {
            var arr = new byte[] { 1, 2, 3, 2, 4 };
            var ret = arr.Split(new [] { (byte)2 }, 2).ToList();

            Assert.Equal(2, ret.Count);
            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[0][0]);
            Assert.Equal(3, ret[1].Length);
            Assert.Equal(3, ret[1][0]);
            Assert.Equal(2, ret[1][1]);
            Assert.Equal(4, ret[1][2]);
        }

        [Fact]
        public void GreaterMaxCount()
        {
            var arr = new byte[] { 1, 2, 3, 2, 4 };
            var ret = arr.Split(new [] { (byte)2 }, 4).ToList();

            Assert.Equal(3, ret.Count);
            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[0][0]);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(3, ret[1][0]);
            Assert.Equal(1, ret[2].Length);
            Assert.Equal(4, ret[2][0]);
        }

        [Fact]
        public void ZeroMaxCount()
        {
            var arr = new byte[] { 1, 2, 3, 2, 4 };
            var ret = arr.Split(new [] { (byte)2 }, 0).ToList();

            Assert.Equal(0, ret.Count);
        }

        [Fact]
        public void EmptySplitter()
        {
            var arr = new byte[] { 1, 2, 3, 2, 4 };
            var ret = arr.Split(new byte[0]).ToList();

            Assert.Equal(5, ret.Count);

            Assert.Equal(1, ret[0].Length);
            Assert.Equal(1, ret[1].Length);
            Assert.Equal(1, ret[2].Length);
            Assert.Equal(1, ret[3].Length);
            Assert.Equal(1, ret[4].Length);

            Assert.Equal(1, ret[0][0]);
            Assert.Equal(2, ret[1][0]);
            Assert.Equal(3, ret[2][0]);
            Assert.Equal(2, ret[3][0]);
            Assert.Equal(4, ret[4][0]);
        }
    }
}
