using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Kdletters.EventSystem
{
    //TODO 委托优化成表达式树
    public static class KEventSystem
    {
        private static bool Initialized;
        private static readonly Dictionary<string, List<EventInfo>> InstanceEvents = new();
        private static readonly Dictionary<string, List<EventInfo>> StaticEvents = new();

        public static async Task InitAsync(Assembly assembly)
        {
            var tasks = new List<Task>();
            foreach (var type in assembly.GetTypes())
            {
                tasks.Add(Task.Run(() =>
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var attribute = method.GetCustomAttribute<KEventListenerAttribute>();
                        if (attribute is null) continue;
                        if (!attribute.EventFlag.IsSubclassOf(typeof(Delegate))) continue;
                        var             key = attribute.EventFlag.Name;
                        List<EventInfo> delegates;
                        if (method.IsStatic)
                        {
                            if (!StaticEvents.TryGetValue(key, out delegates))
                            {
                                delegates = StaticEvents[key] = new List<EventInfo>();
                            }
                        }
                        else
                        {
                            if (!InstanceEvents.TryGetValue(key, out delegates))
                            {
                                delegates = InstanceEvents[key] = new List<EventInfo>();
                            }
                        }

                        var parameterInfos = method.GetParameters();
                        var parameters     = new Type[parameterInfos.Length];
                        foreach (var parameter in parameterInfos)
                            parameters[parameter.Position] = parameter.ParameterType;

                        delegates.Add(new EventInfo(key, Delegate.CreateDelegate(attribute.EventFlag, method), parameters));
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Initialized = true;
        }

        public static void Dispatch<T>(params object[] parameters) where T : Delegate
        {
            if (!Initialized) throw new Exception("事件系统未初始化");
            var key = typeof(T).Name;
            if (StaticEvents.ContainsKey(key))
            {
                foreach (var eventInfo in StaticEvents[key])
                {
                    eventInfo.Delegate.Method.Invoke(null, parameters);
                }
            }

            if (InstanceEvents.ContainsKey(key))
            {
                foreach (var eventInfo in InstanceEvents[key])
                {
                    eventInfo.Delegate.Method.Invoke(parameters[0], parameters[1..]);
                }
            }
        }

        private class EventInfo
        {
            public string DelegateType;

            public Delegate Delegate;
            public Type[] Parameters;

            public EventInfo(string delegateType, Delegate @delegate, params Type[] parameters)
            {
                DelegateType = delegateType;
                Delegate     = @delegate;
                Parameters   = parameters;
            }
        }
    }
}