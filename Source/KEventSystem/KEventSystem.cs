using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Kdletters.EventSystem
{
    //TODO 委托优化成表达式树
    public static class KEventSystem
    {
        private static bool initialized;
        private static readonly Dictionary<Type, Delegate> Events = new();

        /// <summary>
        /// 自动注册添加<see cref="KEventListenerAttribute"/>所标记的静态函数
        /// </summary>
        /// <param name="assemblies">需要注册的程序集</param>
        public static async Task InitAsync(params Assembly[] assemblies)
        {
            var tasks = new List<Task>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    tasks.Add(Task.Run(() =>
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            var attribute = method.GetCustomAttribute<KEventListenerAttribute>();
                            if (attribute is null) continue;
                            var key = attribute.EventFlag;
                            var eventType = typeof(Action<>).MakeGenericType(key);

                            var parameters = Expression.Parameter(key);
                            var methodCallExpression = Expression.Call(method, parameters);
                            var lambda = Expression.Lambda(eventType, methodCallExpression, parameters).Compile();

                            if (!Events.ContainsKey(key))
                                Events[key] = lambda;
                            else
                                Events[key] = Delegate.Combine(Events[key], lambda);
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            initialized = true;
        }

        public static void Subscribe<T>(Action<T> method)
        {
            var key = typeof(T);

            if (!Events.ContainsKey(key))
                Events[key] = method;
            else
                Events[key] = Delegate.Combine(Events[key], method);
        }

        public static void Unsubscribe<T>(Action<T> method)
        {
            var key = typeof(T);

            if (Events.ContainsKey(key))
                Events[key] = Delegate.Remove(Events[key], method);
        }

        public static void Dispatch<T>(T parameter)
        {
            if (!initialized) throw new Exception("事件系统未初始化");
            if (parameter is null) throw new Exception("参数不能为空");

            if (Events.TryGetValue(typeof(T), out var tempEvent))
            {
                var temp = tempEvent as Action<T>;
                if (temp is null)
                    Console.WriteLine($"未找到相应事件-[{typeof(T)}]");
                else
                    temp.Invoke(parameter);
            }
            else
                Console.WriteLine($"未找到相应事件-[{typeof(T)}]");
        }
    }
}