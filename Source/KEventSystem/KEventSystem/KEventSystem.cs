using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Kdletters.EventSystem
{
    public delegate void KEvent<T>(in T arg);

    public delegate Task KTask<T>(in T arg);

    [AttributeUsage(AttributeTargets.Method)]
    public class KEventListenerAttribute : Attribute
    {
        public Type EventFlag { get; }
        public string EventName { get; }

        public KEventListenerAttribute(Type flag)
        {
            EventFlag = flag;
        }

        public KEventListenerAttribute(string eventName)
        {
            EventName = eventName;
        }
    }

    public static class KEventSystem
    {
        private static bool initializing;
        private static bool initialized;
        private static TaskCompletionSource<bool> _initializationTcs;

        private static readonly Dictionary<Type, Delegate> Events = new Dictionary<Type, Delegate>();
        private static readonly Dictionary<Type, Delegate> Tasks = new Dictionary<Type, Delegate>();
        private static readonly Dictionary<string, Action> NonParamEvents = new Dictionary<string, Action>();
        private static readonly Dictionary<string, Func<Task>> NonParamTasks = new Dictionary<string, Func<Task>>();

        public static event Action<string> Log;

        static KEventSystem()
        {
            Log += Console.WriteLine;
        }

        public static Task WaitForInitialization()
        {
            if (_initializationTcs != null)
            {
                return _initializationTcs.Task;
            }

            return Task.CompletedTask;
            // throw new Exception("Does not start initialization.");
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
                Log?.Invoke("Is initialing or has initialized.");
                return;
            }

            initializing = true;
            _initializationTcs = new TaskCompletionSource<bool>();

            if (assemblies != null && assemblies.Length > 0)
            {
                foreach (var assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        ProcessMethod(type);
                    }
                }
            }

            initializing = false;
            initialized = true;
            _initializationTcs.TrySetResult(true);
        }

        /// <summary>
        /// Automatic register all static method been mark with attribute <see cref="KEventListenerAttribute"/> in appdomain
        /// </summary>
        public static Task InitAsync()
        {
            return InitAsync(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <summary>
        /// Automatic register all static method been mark with attribute <see cref="KEventListenerAttribute"/>
        /// </summary>
        /// <param name="assemblies">The assemblies need to register</param>
        public static async Task InitAsync(params Assembly[] assemblies)
        {
            if (initializing || initialized)
            {
                Log?.Invoke("Is initialing or has initialized.");
                return;
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
                        tasks.Add(System.Threading.Tasks.Task.Run(() => { ProcessMethod(type); }));
                    }
                }

                await Task.WhenAll(tasks);
            }

            initializing = false;
            initialized = true;
            _initializationTcs.TrySetResult(true);
        }

        private static void ProcessMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var attribute = method.GetCustomAttribute<KEventListenerAttribute>();
                if (attribute is null) continue;
                var argType = attribute.EventFlag;
                var param = method.GetParameters();
                var ret = method.ReturnType;
                if (argType == null)
                {
                    if (string.IsNullOrEmpty(attribute.EventName) || param.Length > 0)
                    {
                        Log?.Invoke($"[{type.FullName}.{method.Name}] The event name is null or empty.");
                        continue;
                    }

                    if (ret == typeof(void))
                    {
                        Subscribe(attribute.EventName, (Action) method.CreateDelegate(typeof(Action)));
                    }
                    else if (ret == typeof(Task))
                    {
                        Subscribe(attribute.EventName, (Func<Task>) method.CreateDelegate(typeof(Func<Task>)));
                    }
                    else
                    {
                        Log?.Invoke($"[{type.FullName}.{method.Name}] The event return type is illegal.");
                    }
                }
                else
                {
                    if (param.Length != 1)
                    {
                        Log?.Invoke($"[{type.FullName}.{method.Name}] The method format is incorrect, please check.\n Incorrect param count.");
                        continue;
                    }

                    if (param[0].ParameterType != argType.MakeByRefType())
                    {
                        Log?.Invoke($"[{type.FullName}.{method.Name}] The method format is incorrect, please check.\n{param[0].ParameterType} | {argType.MakeByRefType()}");
                        continue;
                    }

                    if (ret == typeof(void))
                    {
                        var eventType = typeof(KEvent<>).MakeGenericType(argType);

                        var parameters = Expression.Parameter(argType.MakeByRefType(), "arg");
                        var methodCallExpression = Expression.Call(method, parameters);
                        var lambda = Expression.Lambda(eventType, methodCallExpression, parameters).Compile();

                        if (!Events.ContainsKey(argType))
                            Events[argType] = lambda;
                        else
                            Events[argType] = Delegate.Combine(Events[argType], lambda);
                    }
                    else if (ret == typeof(Task))
                    {
                        var eventType = typeof(KTask<>).MakeGenericType(argType);

                        var parameters = Expression.Parameter(argType.MakeByRefType(), "arg");
                        var methodCallExpression = Expression.Call(method, parameters);
                        var lambda = Expression.Lambda(eventType, methodCallExpression, parameters).Compile();

                        if (!Events.ContainsKey(argType))
                            Events[argType] = lambda;
                        else
                            Events[argType] = Delegate.Combine(Events[argType], lambda);
                    }
                    else
                    {
                        Log?.Invoke($"[{type.FullName}.{method.Name}] The event return type is illegal.");
                    }
                }
            }
        }

        public static void Subscribe<T>(KEvent<T> method)
        {
            if (method is null)
            {
                return;
            }

            var key = typeof(T);

            if (!Events.ContainsKey(key))
                Events[key] = method;
            else
                Events[key] = Delegate.Combine(Events[key], method);
        }

        public static void Unsubscribe<T>(KEvent<T> method)
        {
            if (method is null)
            {
                return;
            }

            var key = typeof(T);

            if (Events.ContainsKey(key))
            {
                var temp = Delegate.Remove(Events[key], method);
                if (temp is null)
                {
                    Events.Remove(key);
                }
                else
                {
                    Events[key] = temp;
                }
            }
        }

        public static void Dispatch<T>(in T argument)
        {
            if (initializing)
            {
                Log?.Invoke("EventSystem is initializing.");
                return;
            }

            if (argument is null)
            {
                Log?.Invoke("The parameter cannot be empty");
                return;
            }

            if (Events.TryGetValue(typeof(T), out var tempEvent))
            {
                if (!(tempEvent is KEvent<T> temp))
                {
                    return;
                }
                else
                {
                    temp.Invoke(argument);
                }
            }
            else
            {
                Log?.Invoke($"Can not find correct event-[{typeof(T)}]");
            }
        }

        public static bool Has<T>() => Events.ContainsKey(typeof(T));

        #region 无参部分

        /// <summary>
        /// 注册无参事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="method"></param>
        public static void Subscribe(string eventName, Action method)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (method == null) return;

            if (NonParamEvents.ContainsKey(eventName))
            {
                NonParamEvents[eventName] += method;
            }
            else
            {
                NonParamEvents[eventName] = method;
            }
        }

        /// <summary>
        /// 注销无参事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="method"></param>
        public static void Unsubscribe(string eventName, Action method)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (method == null) return;

            if (NonParamEvents.ContainsKey(eventName))
            {
                NonParamEvents[eventName] -= method;
            }
        }

        /// <summary>
        /// 触发无参事件
        /// </summary>
        /// <param name="eventName"></param>
        public static void Dispatch(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            if (NonParamEvents.TryGetValue(eventName, out var action))
            {
                action?.Invoke();
            }
            else if (NonParamTasks.TryGetValue(eventName, out var task))
            {
                Call(task);
            }

            return;

            async void Call(Func<Task> task)
            {
                await (task?.Invoke() ?? Task.CompletedTask);
            }
        }

        /// <summary>
        /// 注册无参事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="method"></param>
        public static void Subscribe(string eventName, Func<Task> method)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (method is null) return;

            if (NonParamTasks.ContainsKey(eventName))
            {
                NonParamTasks[eventName] += method;
            }
            else
            {
                NonParamTasks[eventName] = method;
            }
        }

        /// <summary>
        /// 注销无参事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="method"></param>
        public static void Unsubscribe(string eventName, Func<Task> method)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (method is null) return;

            if (NonParamTasks.ContainsKey(eventName))
            {
                NonParamTasks[eventName] -= method;
            }
        }

        /// <summary>
        /// 触发无参事件
        /// </summary>
        /// <param name="eventName"></param>
        public static Task DispatchTask(string eventName)
        {
            if (!string.IsNullOrEmpty(eventName))
            {
                if (NonParamTasks.TryGetValue(eventName, out var action))
                {
                    return action?.Invoke() ?? Task.CompletedTask;
                }

                Dispatch(eventName);
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}