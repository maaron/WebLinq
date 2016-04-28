using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Linq;
using WebLinq;

namespace UnitTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var source = new WebLinqSource<int>(() => new[] { 1, 2, 3, 4 , 3, 2, 1});

            var query =
                from i in source.Data
                where i > 2
                where i >= 3
                select i;

            var result = query.ToArray();
            Assert.IsTrue(result.SequenceEqual(new[] { 3, 4, 3 }));
        }

        [TestMethod]
        public void TestMethod2()
        {
            var source = new WebLinqSource<int>(() => new[] { 1, 2, 3, 4, 3, 2, 1 });

            var query =
                from i in source.Data
                from i2 in Enumerable.Range(1, i)
                where i2 <= i
                select i2;

            var result = query.ToArray();
            Assert.IsTrue(result.SequenceEqual(new[] {
                1,
                1, 2,
                1, 2, 3,
                1, 2, 3, 4,
                1, 2, 3,
                1, 2,
                1
            }));
        }

        [TestMethod]
        public void TestMethod3()
        {
            var source = new[] {
                new WebLinqSource<int>(() => new[] { 1, 2, 3 }),
                new WebLinqSource<int>(() => new[] { 4, 5, 6 })
            };

            var query =
                from src in source
                from i in src.Data
                where i % 2 == 0
                select i;

            var result = query.ToArray();
            Assert.IsTrue(result.SequenceEqual(new[] {
                2, 4, 6
            }));
        }
    }
}
