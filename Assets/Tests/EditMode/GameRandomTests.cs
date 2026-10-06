using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class GameRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new GameRandom(12345);
            var b = new GameRandom(12345);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new GameRandom(1);
            var b = new GameRandom(2);
            Assert.AreNotEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void Fork_IsDeterministicAndIndependentOfParentUsage()
        {
            var parent1 = new GameRandom(42);
            var parent2 = new GameRandom(42);
            parent2.NextULong(); // consuming the parent must not change the forked stream

            Assert.AreEqual(parent1.Fork(7).NextULong(), parent2.Fork(7).NextULong());
            Assert.AreNotEqual(parent1.Fork(7).NextULong(), parent1.Fork(8).NextULong());
        }

        [Test]
        public void IntRange_StaysInBoundsAndHitsEveryValue()
        {
            var rng = new GameRandom(99);
            var seen = new bool[5];
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.Range(-2, 3);
                Assert.That(v, Is.InRange(-2, 2));
                seen[v + 2] = true;
            }
            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void Value_IsInHalfOpenUnitInterval()
        {
            var rng = new GameRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                float v = rng.Value();
                Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }
    }
}
