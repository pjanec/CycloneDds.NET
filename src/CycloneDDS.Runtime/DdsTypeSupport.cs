using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CycloneDDS.Runtime
{
    public delegate void NativeUnmarshalDelegate<T>(IntPtr nativeData, out T managedData);

    /// <summary>
    /// Internal helper for extracting type metadata via reflection.
    /// Caches delegates to amortize reflection overhead.
    /// </summary>
    public static class DdsTypeSupport
    {
        // Cache: Type -> GetDescriptorOps delegate
        private static readonly ConcurrentDictionary<Type, Func<uint[]>> _opsCache = new();
        
        /// <summary>
        /// Get descriptor ops array for type T using reflection.
        /// Throws if T doesn't have GetDescriptorOps() method (not a DDS type).
        /// </summary>
        public static uint[] GetDescriptorOps<T>()
        {
            var func = _opsCache.GetOrAdd(typeof(T), type =>
            {
                // Look for: public static uint[] GetDescriptorOps()
                var method = type.GetMethod("GetDescriptorOps", 
                    BindingFlags.Static | BindingFlags.Public, 
                    null, 
                    Type.EmptyTypes, 
                    null);
                
                if (method == null || method.ReturnType != typeof(uint[]))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.Name}' does not have a public static GetDescriptorOps() method. " +
                        "Did you forget to add [DdsTopic] or [DdsStruct] attribute?");
                }
                
                // Create delegate for zero-overhead invocation
                return (Func<uint[]>)Delegate.CreateDelegate(typeof(Func<uint[]>), method);
            });
            
            return func();
        }
        
        private static readonly ConcurrentDictionary<Type, Func<DdsKeyDescriptor[]>> _keysCache = new();

        public static DdsKeyDescriptor[] GetKeyDescriptors<T>()
        {
            var func = _keysCache.GetOrAdd(typeof(T), type =>
            {
                var method = type.GetMethod("GetKeyDescriptors",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                
                if (method == null)
                {
                    // For backward compatibility or partially generated types, return null func
                    //return () => null;
                    throw new InvalidOperationException(
                        $"Type '{type.Name}' does not have a public static GetKeyDescriptors() method. " +
                        "Did you forget to add [DdsTopic] or [DdsStruct] attribute?");
				}
                
                return (Func<DdsKeyDescriptor[]>)Delegate.CreateDelegate(typeof(Func<DdsKeyDescriptor[]>), method);
            });
            
            return func();
        }
        
        /// <summary>
        /// Get type name for DDS topic registration.
        /// </summary>
        public static string GetTypeName<T>()
        {
            var name = typeof(T).FullName;
            if (string.IsNullOrEmpty(name))
                return typeof(T).Name;
            return name.Replace(".", "::");
        }

        private static readonly ConcurrentDictionary<Type, Delegate> _unmarshallerCache = new();

        public static T FromNative<T>(IntPtr nativePtr)
        {
             var unmarshaller = (NativeUnmarshalDelegate<T>)_unmarshallerCache.GetOrAdd(typeof(T), t =>
             {
                 var method = t.GetMethod("MarshalFromNative", 
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, 
                     null,
                     new[] { typeof(IntPtr), typeof(T).MakeByRefType() },
                     null);
                 
                 if (method == null)
                 {
                    throw new InvalidOperationException(
                        $"Type '{t.Name}' does not have a public MarshalFromNative(IntPtr, out T) method. " +
                        "Did you forget to add [DdsTopic] or [DdsStruct] attribute?");
                 }

                 return (NativeUnmarshalDelegate<T>)Delegate.CreateDelegate(typeof(NativeUnmarshalDelegate<T>), method);
             });
             
             T managedData;
             unmarshaller(nativePtr, out managedData);
             return managedData;
        }
    }
}
