using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Kdletters.EventSystem
{
    public delegate void KEvent<T>(in T arg);

    public static class KEventSystem
    {
        private static bool initializing;
        private static bool initialized;
        private static TaskCompletionSource<bool> _initializationTcs;

        private static readonly Dictionary<Type, Delegate> Events = new();

        public static Task WaitForInitialization()
        {
            if (_initializationTcs != null)
            {
                return _initializationTcs.Task;
            }

            throw new Exception("Does not start initialization.");
        }

        /// <summary>
        /// Automatic register all static method been mark with attribute <see cref="KEventListenerAttribute"/> in appdomain
        /// </summary>
        public static void Init()
        {
            Init(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <summary>
        /// Automatic register all static method been mark with attribute <see cref="KEventListenerAttribute"/>
        /// </summary>
        /// <param name="assemblies">The assemblies need to register</param>
        public static void Init(params Assembly[] assemblies)
        {
            if (initializing || initialized)
            {
                throw new Exception("Is initialing or has initialized.");
            }

            initializing = true;
            _initializationTcs = new TaskCompletionSource<bool>();
            
            if (assemblies != null && assemblies.Length > 0)
            {
                foreach (var assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            var attribute = method.GetCustomAttribute<KEventListenerAttribute>();
                            if (attribute is null) continue;
                            var key = attribute.EventFlag;
                            var eventType = typeof(KEvent<>).MakeGenericType(key);

                            var parameters = Expression.Parameter(key.MakeByRefType(), "arg");
                            var methodCallExpression = Expression.Call(method, parameters);
                            var lambda = Expression.Lambda(eventType, methodCallExpression, parameters).Compile();

                            if (!Events.ContainsKey(key))
                                Events[key] = lambda;
                            else
                                Events[key] = Delegate.Combine(Events[key], lambda);
                        }
                    }
                }
            }

            initializing = false;
            initialized = true;
            _initializationTcs.SetResult(true);
        }

        /// <summary>
        /// Automatic register all static method been mark with attribute <see cref="KEventListenerAttribute"/>
        /// </summary>
        /// <param name="assemblies">The assemblies need to register</param>
        public static async Task InitAsync(params Assembly[] assemblies)
        {
            if (initializing || initialized)
            {
                throw new Exception("Is initialing or has initialized.");
            }

            initializing = true;
            _initializationTcs = new TaskCompletionSource<bool>();

            if (assemblies != null && assemblies.Length > 0)
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
                                var eventType = typeof(KEvent<>).MakeGenericType(key);

                                var parameters = Expression.Parameter(key.MakeByRefType(), "arg");
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
            }

            initializing = false;
            initialized = true;
            _initializationTcs.SetResult(true);
        }

        public static void Subscribe<T>(KEvent<T> method)
        {
            var key = typeof(T);

            if (!Events.ContainsKey(key))
                Events[key] = method;
            else
                Events[key] = Delegate.Combine(Events[key], method);
        }

        public static void Unsubscribe<T>(KEvent<T> method)
        {
            var key = typeof(T);

            if (Events.ContainsKey(key))
                Events[key] = Delegate.Remove(Events[key], method);
        }

        public static void Dispatch<T>(in T argument)
        {
            if (initializing) throw new Exception("EventSystem is initializing.");
            if (!initialized) throw new Exception("EventSystem has not initialized.");
            //if (argument is null) throw new Exception("参数不能为空");

            if (Events.TryGetValue(typeof(T), out var tempEvent))
            {
                if (tempEvent is not KEvent<T> temp)
                    throw new Exception($"Can not find correct event-[{typeof(T)}]");
                else
                    temp.Invoke(argument);
            }
            else
                throw new Exception($"Can not find correct event-[{typeof(T)}]");
        }
    }
}