using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Assertions;

namespace BossRoom.Infrastructure
{
    [AttributeUsage( AttributeTargets.Method )]
	public sealed class Inject : Attribute
	{
	}

	public class NoInstanceToInjectException : Exception
	{
		public NoInstanceToInjectException( string message ) : base( message )
		{
		}
	}

    public interface IInstanceResolver
    {
        public T Resolve<T>()
            where T : class;

        public void Inject<T>(T obj)
            where T : class;
    }

	public sealed class DIScope : IInstanceResolver, IDisposable
	{
        private static class CachedReflectionUtility
        {
            private static readonly Dictionary<Type, MethodBase> k_CachedInjectableMethods = new Dictionary<Type, MethodBase>();
            private static readonly Dictionary<MethodBase, ParameterInfo[]> k_CachedMethodParameters = new Dictionary<MethodBase, ParameterInfo[]>();
            private static readonly Type k_InjectAttributeType = typeof(Inject);
            private static readonly HashSet<Type> k_TypesToSkip = new HashSet<Type>();

            public static bool TryGetInjectableMethod(Type type, out MethodBase method)
            {
                if (k_TypesToSkip.Contains(type))
                {
                    method = null;
                    return false;
                }

                if (!k_CachedInjectableMethods.TryGetValue(type, out method))
                {
                    bool foundInjectionSite = false;

                    var constructors = type.GetConstructors(BindingFlags.Default);
                    foreach (var constructorInfo in constructors)
                    {
                        foundInjectionSite = constructorInfo.IsDefined(k_InjectAttributeType);
                        if (foundInjectionSite)
                        {
                            k_CachedInjectableMethods[type] = constructorInfo;
                            break;
                        }
                    }

                    if (!foundInjectionSite)
                    {
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        foreach (var methodInfo in methods)
                        {
                            foundInjectionSite = methodInfo.IsDefined(k_InjectAttributeType);
                            if (foundInjectionSite)
                            {
                                k_CachedInjectableMethods[type] = methodInfo;
                                break;
                            }
                        }
                    }

                    if (!foundInjectionSite)
                    {
                        k_TypesToSkip.Add(type);
                    }
                }

                return k_CachedInjectableMethods.TryGetValue(type, out method);
            }

            public static ParameterInfo[] GetMethodParameters(MethodBase injectionMethod)
            {
                if (!k_CachedMethodParameters.TryGetValue(injectionMethod, out var methodParameters))
                {
                    methodParameters = injectionMethod.GetParameters();
                    k_CachedMethodParameters[injectionMethod] = methodParameters;
                }

                return methodParameters;
            }
        }

        private readonly DIScope m_Parent;
        private readonly Dictionary<Type, object> m_TypesToInstances = new Dictionary<Type, object>();

        private bool m_ScopeConstructionComplete = false;
        public DIScope(DIScope parent = null)
        {
            m_Parent = parent;
            BindInstanceAsSingle<IInstanceResolver>( this );
        }

        ~DIScope()
        {
            Dispose();
        }

        public void Dispose()
        {
            m_TypesToInstances.Clear();
        }

        private void EnsureScopeIsNotFinalized()
        {
            Assert.IsFalse(m_ScopeConstructionComplete, "Trying to bind dependencies to a scope after it has been finalized by either being accessed or by explicit call of CompleteScopeConstruction");
        }

        public void BindInstanceAsSingle<T>(T implementation) where T : class
        {
            EnsureScopeIsNotFinalized();
            m_TypesToInstances[typeof(T)] = implementation;
        }

        public void BindInstanceAsSingle<TInterface, TImplementation>(TImplementation implementation)
            where TImplementation : class, TInterface
            where TInterface : class
        {
            BindInstanceAsSingle<TInterface>(implementation);
            BindInstanceAsSingle<TImplementation>(implementation);
        }

        public void BindAsSingle<TInterface, TImplementation>()
            where TImplementation : class, TInterface,  new()
            where TInterface : class
        {
            var instance = new TImplementation();
            BindInstanceAsSingle<TInterface>(instance);
        }

        public T Resolve<T>()
            where T : class
        {
            FinalizeScopeConstruction();

            if (!m_TypesToInstances.TryGetValue(typeof(T), out var value))
            {
                if (m_Parent != null)
                {
                    return m_Parent.Resolve<T>();
                }

                throw new NoInstanceToInjectException($"Injection of type {typeof(T)} failed.");
            }

            return (T)value;
        }

        /// <summary>
        /// This method forces the finalization of construction of DI Scope. It would inject all the instances passed to it and afterwards would not be available for modification.
        /// </summary>
        public void FinalizeScopeConstruction()
        {
            if (m_ScopeConstructionComplete)
            {
                return;
            }

            foreach (var typesToInstance in m_TypesToInstances)
            {
                Inject(typesToInstance.Value);
            }

            m_ScopeConstructionComplete = true;
        }

        public void Inject<T>( T obj )
            where T : class
		{
            if(CachedReflectionUtility.TryGetInjectableMethod(typeof(T), out var injectionMethod))
            {
                var parameters = CachedReflectionUtility.GetMethodParameters(injectionMethod);

                var paramColleciton = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];

                    var genericResolveMethod = typeof(DIScope).GetMethod("Resolve").MakeGenericMethod(parameter.ParameterType);
                    var resolved = genericResolveMethod.Invoke(this, null);
                    paramColleciton[i] = resolved;
                }

                injectionMethod.Invoke(obj, paramColleciton);
            }
        }
	}
}
