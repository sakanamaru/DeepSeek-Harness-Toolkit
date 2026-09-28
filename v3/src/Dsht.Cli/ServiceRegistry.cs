using System;
using System.Collections.Generic;

namespace Dsht.Cli
{
    /// <summary>极简依赖装配（自写，零第三方）：按类型注册单例并在组合根解析。
    /// 故意不做反射/自动装配——保持可读、可预测、零依赖。</summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        public ServiceRegistry Add<T>(T instance) where T : class
        {
            _map[typeof(T)] = instance;
            return this;
        }

        public T Get<T>() where T : class
        {
            object o;
            if (_map.TryGetValue(typeof(T), out o)) return (T)o;
            throw new InvalidOperationException("未注册的服务: " + typeof(T).Name);
        }

        public bool Has<T>() where T : class
        {
            return _map.ContainsKey(typeof(T));
        }
    }
}