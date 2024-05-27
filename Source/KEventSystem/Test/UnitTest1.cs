using System;
using System.Threading.Tasks;
using EventKey;
using Kdletters.EventSystem;
using NUnit.Framework;

namespace Test
{
    public class SuccessException : Exception
    {
    }

    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            KEventSystem.Init();
        }

        [Test]
        public void NonParamStaticEvent()
        {
            KEventSystem.Subscribe("Add", Test.StaticEvent.Add);
            KEventSystem.Subscribe("Add", Test.StaticEvent.AddTask);
            Check(() => KEventSystem.Dispatch("Add"));
            Check(() => KEventSystem.DispatchTask("Add"));
            Check(() => KEventSystem.Dispatch("Foo"));
            Check(() => KEventSystem.DispatchTask("Foo"));
            KEventSystem.Unsubscribe("Add", Test.StaticEvent.Add);
        }

        [Test]
        public void StaticEvent()
        {
            KEventSystem.Subscribe<AddArg>(Test.StaticEvent.Add);
            Check(() => KEventSystem.Dispatch(new AddArg(1)));
            Check(() => KEventSystem.Dispatch(new FooArg()));
            KEventSystem.Unsubscribe<AddArg>(Test.StaticEvent.Add);
        }

        [Test]
        public void InstanceEvent()
        {
            var obj = new InstanceEvent(10);
            KEventSystem.Subscribe<AddArg>(obj.Add);
            Check(() => KEventSystem.Dispatch(new AddArg(1)));
            KEventSystem.Unsubscribe<AddArg>(obj.Add);
        }

        private void Check(Action action)
        {
            var b = false;
            try
            {
                action?.Invoke();
            }
            catch (SuccessException e)
            {
                b = true;
            }

            Assert.IsTrue(b);
        }
    }

    public class StaticEvent
    {
        [KEventListener("Foo")]
        public static void Foo()
        {
            Console.WriteLine("non-param static event Foo has been called.");
            throw new SuccessException();
        }
        [KEventListener("Foo")]
        public static Task FooTask()
        {
            Console.WriteLine("non-param static event FooTask has been called.");
            throw new SuccessException();
        }

        [KEventListener(typeof(FooArg))]
        public static void Foo(in FooArg arg)
        {
            Console.WriteLine("static event Foo has been called.");
            throw new SuccessException();
        }

        public static void Add()
        {
            Console.WriteLine("non-param static event Add has been called.");
            throw new SuccessException();
        }

        public static Task AddTask()
        {
            Console.WriteLine("non-param static event AddTask has been called.");
            throw new SuccessException();
        }

        public static void Add(in AddArg arg)
        {
            Console.WriteLine("static event Add has been called.");
            Console.WriteLine(arg.a);
            throw new SuccessException();
        }
    }

    public class InstanceEvent
    {
        public int v;

        public InstanceEvent(int v)
        {
            this.v = v;
        }

        public void Add(in AddArg arg)
        {
            Console.WriteLine("instance event Add has been called.");
            Console.WriteLine(v + arg.a);
            throw new SuccessException();
        }

        public Task AddTask(in AddArg arg)
        {
            Console.WriteLine("instance event AddTask has been called.");
            Console.WriteLine(v + arg.a);
            throw new SuccessException();
        }
    }
}