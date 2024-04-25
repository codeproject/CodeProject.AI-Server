using Xunit;
using CodeProject.AI.SDK.Utils;
using System;

namespace CodeProject.AI.SDK.Utils.Tests
{
    public class ObjectPoolTests
    {
        private class PooledObject
        {
            public int Value { get; set; }
        }

        [Fact]
        public void GetTest()
        {
            var pool = new ObjectPool<PooledObject>(10, () => new PooledObject(), obj => obj.Value = 1);
            var obj = pool.Get();
            Assert.Equal(1, obj.Value);
        }

        [Fact]
        public void ReleaseTest()
        {
            var pool = new ObjectPool<PooledObject>(10, () => new PooledObject(), obj => obj.Value = 1);
            var obj = pool.Get();
            pool.Release(obj);
            var obj2 = pool.Get();
            Assert.Same(obj, obj2);
        }

        [Fact]
        public void ReleaseWhenPoolIsFullTest()
        {
            var pool = new ObjectPool<PooledObject>(1, () => new PooledObject(), obj => obj.Value = 1);
            var obj1 = pool.Get();
            var obj2 = pool.Get();
            pool.Release(obj1);
            pool.Release(obj2);
            var obj3 = pool.Get();
            Assert.NotSame(obj2, obj3);
            Assert.Same(obj1, obj3);
        }
    }
}
