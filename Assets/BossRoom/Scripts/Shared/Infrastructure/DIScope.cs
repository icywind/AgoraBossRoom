using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

namespace BossRoom.Scripts.Shared.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
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
            private static readonly Dictionary<Type, ConstructorInfo> k_CachedInjectableConstructors = new Dictionary<Type, ConstructorInfo>();
            private static readonly Dictionary<MethodBase, ParameterInfo[]> k_CachedMethodParameters = new Dictionary<MethodBase, ParameterInfo[]>();
            private static readonly Dictionary<Type, MethodInfo> k_CachedResolveMethods = new Dictionary<Type, MethodInfo>();
            private static readonly Type k_InjectAttributeType = typeof(Inject);
            private static readonly HashSet<Type> k_ProcessedTypes = new HashSet<Type>();
            private static MethodInfo k_ResolveMethod;

            public static bool TryGetInjectableConstructor(Type type, out ConstructorInfo method)
            {
                CacheTypeMethods(type);
                return k_CachedInjectableConstructors.TryGetValue(type, out method);
            }

            private static void CacheTypeMethods(Type type)
            {
                if (k_ProcessedTypes.Contains(type))
                {
                    return;
                }

                bool foundConstructorInjector = false;

                var constructors = type.GetConstructors(BindingFlags.Default);
                foreach (var constructorInfo in constructors)
                {
                    bool foundInjectionSite = constructorInfo.IsDefined(k_InjectAttributeType);
                    if (foundInjectionSite)
                    {
                        k_CachedInjectableConstructors[type] = constructorInfo;
                        var methodParameters = constructorInfo.GetParameters();
                        k_CachedMethodParameters[constructorInfo] = methodParameters;
                        foundConstructorInjector = true;
                        break;
                    }
                }

                if (!foundConstructorInjector)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var methodInfo in methods)
                    {
                        bool foundInjectionSite = methodInfo.IsDefined(k_InjectAttributeType);
                        if (foundInjectionSite)
                        {
                            k_CachedInjectableMethods[type] = methodInfo;
                            var methodParameters = methodInfo.GetParameters();
                            k_CachedMethodParameters[methodInfo] = methodParameters;
                            break;
                        }
                    }
                }

                k_ProcessedTypes.Add(type);

            }

            public static bool TryGetInjectableMethod(Type type, out MethodBase method)
            {
                CacheTypeMethods(type);
                return k_CachedInjectableMethods.TryGetValue(type, out method);
            }

            public static ParameterInfo[] GetMethodParameters(MethodBase injectionMethod)
            {
                return k_CachedMethodParameters[injectionMethod];
            }

            public static MethodInfo GetTypedResolveMethod(Type parameterType)
            {
                if (!k_CachedResolveMethods.TryGetValue(parameterType, out var resolveMethod))
                {
                    if (k_ResolveMethod == null)
                    {
                        k_ResolveMethod = typeof(DIScope).GetMethod("Resolve");
                    }
                    resolveMethod = k_ResolveMethod.MakeGenericMethod(parameterType);
                    k_CachedResolveMethods[parameterType] = resolveMethod;
                }

                return resolveMethod;
            }
        }

        private struct LazyBindDescriptor
        {
            public Type Type;
            public Type[] InterfaceTypes;

            public LazyBindDescriptor(Type type, Type[] interfaceTypes)
            {
                Type = type;
                InterfaceTypes = interfaceTypes;
            }
        }

        private readonly DIScope m_Parent;
        private readonly Dictionary<Type, LazyBindDescriptor> m_LazyBindDescriptors = new Dictionary<Type, LazyBindDescriptor>();
        private readonly Dictionary<Type, object> m_TypesToInstances = new Dictionary<Type, object>();

        private bool m_ScopeConstructionComplete = false;

        private readonly DisposableGroup m_DisposableGroup = new DisposableGroup();

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
            m_DisposableGroup.Dispose();
        }

        public void BindInstanceAsSingle<T>(T instance) where T : class
        {
            BindInstanceToType(instance, typeof(T));
        }

        public void BindInstanceAsSingle<TInterface, TImplementation>(TImplementation instance)
            where TImplementation : class, TInterface
            where TInterface : class
        {
            BindInstanceAsSingle<TInterface>(instance);
            BindInstanceAsSingle<TImplementation>(instance);
        }

        private void BindInstanceToType(object instance, Type type)
        {
            m_TypesToInstances[type] = instance;
        }

        public void BindAsSingle<TImplementation, TInterface>()
            where TImplementation : class, TInterface
            where TInterface : class
        {
            LazyBind(typeof(TImplementation), typeof(TInterface));
        }

        public void BindAsSingle<TImplementation, TInterface, TInterface2>()
            where TImplementation : class, TInterface, TInterface2
            where TInterface : class
            where TInterface2 : class
        {
            LazyBind(typeof(TImplementation), typeof(TInterface), typeof(TInterface2));
        }

        public void BindAsSingle<T>()
            where T : class
        {
            LazyBind(typeof(T));
        }

        private void LazyBind(Type type, params Type[] typeAliases)
        {
            var descriptor = new LazyBindDescriptor(type, typeAliases);
            m_LazyBindDescriptors[type] = descriptor;
        }

        private object InstantiateLazyBoundObject(LazyBindDescriptor descriptor)
        {
            object instance;
            if (CachedReflectionUtility.TryGetInjectableConstructor(descriptor.Type, out var constructor))
            {
                var parameters = GetResolvedInjectionMethodParameters(constructor);
                instance = constructor.Invoke(parameters);
            }
            else
            {
                instance = Activator.CreateInstance(descriptor.Type);
            }

            AddToDisposableGroupIfDisposable(instance);

            BindInstanceToType(instance, descriptor.Type);

            if (descriptor.InterfaceTypes != null)
            {
                foreach (var interfaceType in descriptor.InterfaceTypes)
                {
                    BindInstanceToType(instance, interfaceType);
                }
            }

            return instance;
        }

        private void AddToDisposableGroupIfDisposable(object instance)
        {
            if (instance is IDisposable disposable)
            {
                m_DisposableGroup.Add(disposable);
            }
        }

        public T Resolve<T>()
            where T : class
        {
            FinalizeScopeConstruction();

            //if we have this type as lazy-bound instance - we are going to instantiate it now
            if (m_LazyBindDescriptors.TryGetValue(typeof(T), out var lazyBindDescriptor))
            {
                var instance = (T)InstantiateLazyBoundObject(lazyBindDescriptor);
                m_LazyBindDescriptors.Remove(typeof(T));
                return instance;
            }

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
        /// This method forces the finalization of construction of DI Scope. It would inject all the instances passed to it directly.
        /// Objects that were bound by just type will be instantiated on their first use.
        /// </summary>
        public void FinalizeScopeConstruction()
        {
            if (m_ScopeConstructionComplete)
            {
                return;
            }

            var uniqueObjects = new HashSet<object>(m_TypesToInstances.Values);

            foreach (var objectToInject in uniqueObjects)
            {
                Inject(objectToInject);
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

                    var genericResolveMethod = CachedReflectionUtility.GetTypedResolveMethod(parameter.ParameterType);
                    var resolved = genericResolveMethod.Invoke(this, null);
                    paramColleciton[i] = resolved;
                }

                injectionMethod.Invoke(obj, paramColleciton);
            }
        }

        public void Inject(GameObject go)
        {
            var components = go.GetComponentsInChildren<Component>();

            foreach (var component in components)
            {
                Inject(component);
            }
        }

        private object[] GetResolvedInjectionMethodParameters(MethodBase injectionMethod)
        {
            var parameters = CachedReflectionUtility.GetMethodParameters(injectionMethod);

            var paramColleciton = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                var genericResolveMethod = CachedReflectionUtility.GetTypedResolveMethod(parameter.ParameterType);
                var resolved = genericResolveMethod.Invoke(this, null);
                paramColleciton[i] = resolved;
            }

            return paramColleciton;
        }
	}
}
