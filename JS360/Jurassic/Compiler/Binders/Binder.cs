using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jurassic.Compiler
{

    /// <summary>
    /// Represents a generic delegate that all method calls pass through.  For internal use only.
    /// </summary>
    /// <param name="engine"> The associated script engine. </param>
    /// <param name="thisObject"> The value of the <c>this</c> keyword. </param>
    /// <param name="arguments"> The arguments that were passed to the function. </param>
    /// <returns> The result of calling the method. </returns>
    internal delegate object BinderDelegate(ScriptEngine engine, object thisObject, params object[] arguments);

    /// <summary>
    /// Selects a method from a list of candidates and performs type conversion from actual
    /// argument type to formal argument type.
    /// </summary>
    [Serializable]
    internal abstract class Binder
    {
        [NonSerialized]
        private BinderDelegate[] delegateCache;
        public static Dictionary<string, BinderDelegate[]> globalDelegateCache;
        private const int MaximumCachedParameterCount = 10;
        protected bool HasParamArray = false;



        //     PROPERTIES
        //_________________________________________________________________________________________

        /// <summary>
        /// Gets the name of the target methods.
        /// </summary>
        public abstract string Name
        {
            get;
        }

        /// <summary>
        /// Gets the full name of the target methods, including the type name.
        /// </summary>
        public abstract string FullName
        {
            get;
        }

        /// <summary>
        /// Gets the maximum number of arguments of any of the target methods.  Used to set the
        /// length property on the function.
        /// </summary>
        public virtual int FunctionLength
        {
            get { return 0; }
        }




        //     METHODS
        //_________________________________________________________________________________________

        /// <summary>
        /// Calls the bound method.
        /// </summary>
        /// <param name="engine"> The associated script engine. </param>
        /// <param name="thisObject"> The value of the <c>this</c> keyword. </param>
        /// <param name="arguments"> The arguments to pass to the function. </param>
        /// <returns> The result of calling the method. </returns>
        public object Call(ScriptEngine engine, object thisObject, params object[] arguments)
        {
            var binderDelegate = CreateDelegate(arguments.Length);
            return binderDelegate(engine, thisObject, arguments);
        }

        /// <summary>
        /// Creates a delegate that does type conversion and calls the bound method.
        /// </summary>
        /// <param name="argumentCount"> The number of arguments that will be passed to the delegate. </param>
        /// <returns> A delegate that does type conversion and calls the method represented by this
        /// object. </returns>
        /// <remarks> This method caches the result so calling CreateDelegate a second time with
        /// the same parameter count will be markedly quicker. </remarks>
        public BinderDelegate CreateDelegate(int argumentCount)
        {
            // Early out
            if (this.delegateCache != null && argumentCount <= MaximumCachedParameterCount)
                return this.delegateCache[argumentCount];

            // If there are too many arguments, don't cache the delegate.
            if (argumentCount > MaximumCachedParameterCount)
                return CreateDelegateCore(argumentCount);

            // No cache found? Try the global cache first
            if (this.delegateCache == null && Binder.globalDelegateCache != null)
                this.delegateCache = Binder.globalDelegateCache[this.FullName];

            // Save the delegate that is created into a cache so it doesn't have to be created again.
            if (this.delegateCache == null)
                this.delegateCache = new BinderDelegate[MaximumCachedParameterCount + 1];

            var binderDelegate = this.delegateCache[argumentCount];
            if (binderDelegate == null)
            {
                // Create a binding method.
                binderDelegate = CreateDelegateCore(argumentCount);

                // Store it in the cache.
                this.delegateCache[argumentCount] = binderDelegate;
            }
            return binderDelegate;
        }

        /// <summary>
        /// Creates a delegate that does type conversion and calls the bound method.
        /// </summary>
        /// <param name="argumentCount"> The number of arguments that will be passed to the delegate. </param>
        /// <returns> A delegate that does type conversion and calls the method represented by this
        /// object. </returns>
        /// <remarks> No caching of the result occurs. </remarks>
        private BinderDelegate CreateDelegateCore(int argumentCount)
        {
#if !XBOX
            // Create a new dynamic method.
            System.Reflection.Emit.DynamicMethod dm;
            ILGenerator generator;
#if !SILVERLIGHT
            if (ScriptEngine.LowPrivilegeEnvironment == false)
            {
                // Full trust only - skips visibility checks.
                dm = new System.Reflection.Emit.DynamicMethod(
                    string.Format("binder_for_{0}", this.FullName),                                                 // Name of the generated method.
                    typeof(object),                                                                                 // Return type of the generated method.
                    new Type[] { typeof(ScriptEngine), typeof(object), typeof(object[]) },                          // Parameter types of the generated method.
                    typeof(JSBinder),                                                                               // Owner type.
                    true);                                                                                          // Skips visibility checks.
                generator = new DynamicILGenerator(dm);
            }
            else
            {
#endif
                // Partial trust / silverlight.
                dm = new System.Reflection.Emit.DynamicMethod(
                    string.Format("binder_for_{0}", this.FullName),                                                 // Name of the generated method.
                    typeof(object),                                                                                 // Return type of the generated method.
                    new Type[] { typeof(ScriptEngine), typeof(object), typeof(object[]) });                         // Parameter types of the generated method.
                generator = new ReflectionEmitILGenerator(dm.GetILGenerator());
#if !SILVERLIGHT
            }
#endif

            // Generate the body of the method.
            GenerateStub(generator, argumentCount);

            // Convert the DynamicMethod to a delegate.
            return (BinderDelegate)dm.CreateDelegate(typeof(BinderDelegate));
#else
            return null;
#endif //XBOX
        }

        /// <summary>
        /// Generates a method that does type conversion and calls the bound method.
        /// </summary>
        /// <param name="generator"> The ILGenerator used to output the body of the method. </param>
        /// <param name="argumentCount"> The number of arguments that will be passed to the delegate. </param>
        /// <returns> A delegate that does type conversion and calls the method represented by this
        /// object. </returns>
        protected abstract void GenerateStub(ILGenerator generator, int argumentCount);




        //     OBJECT OVERRIDES
        //_________________________________________________________________________________________

        /// <summary>
        /// Returns a textual representation of this object.
        /// </summary>
        /// <returns> A textual representation of this object. </returns>
        public override string ToString()
        {
            return this.FullName;
        }


        public string GetCompiledName(int argumentCount)
        {
            return string.Format("{0}.{1}", this.FullName, argumentCount);
        }

#if !XBOX
        /// <summary>
        /// Compile the Binder methods
        /// </summary>
        /// <param name="engine">ScriptEngine</param>
        /// <param name="typeBuilder">The TypeBuilder to attach the compiled Binder methods to</param>
        public void Compile(ScriptEngine engine, System.Reflection.Emit.TypeBuilder typeBuilder)
        {

            int length = this.HasParamArray ? Binder.MaximumCachedParameterCount : this.FunctionLength;
            for (int argumentCount = 0; argumentCount <= length; argumentCount++)
            {
                string compiledName = GetCompiledName(argumentCount);
                if (!engine.compiledBinders.Contains(compiledName))
                {
                    engine.compiledBinders.Add(compiledName);
                    var methodBuilder = typeBuilder.DefineMethod(
                        compiledName,
                        System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.Public,
                        typeof(object),
                        new Type[] { typeof(ScriptEngine), typeof(object), typeof(object[]) });

                    // Generate the IL for the method.
                    ILGenerator generator = new ReflectionEmitILGenerator(methodBuilder.GetILGenerator());
                    GenerateStub(generator, argumentCount);
                }
            }
        }
#endif

        /// <summary>
        /// Load the Binder methods from the given Type
        /// </summary>
        /// <param name="type">Type to load the Binder methods from</param>
        public void LoadFromType(Type type)
        {
            if (this.delegateCache == null)
                this.delegateCache = new BinderDelegate[MaximumCachedParameterCount + 1];

            for (int argumentCount = 0; argumentCount <= MaximumCachedParameterCount; argumentCount++)
            {
                int length = this.HasParamArray ? Binder.MaximumCachedParameterCount : this.FunctionLength;
                string methodName = GetCompiledName(Math.Min(length, argumentCount));
                MethodInfo method = type.GetMethod(methodName);
                if (method == null)
                    throw new NotImplementedException(String.Format("Method {0} not found in Type", methodName));

                this.delegateCache[argumentCount] = (BinderDelegate)Delegate.CreateDelegate(typeof(BinderDelegate), method);
            }

            // Add into the global cache
            if (Binder.globalDelegateCache == null)
                Binder.globalDelegateCache = new Dictionary<string,BinderDelegate[]>();

            if (!Binder.globalDelegateCache.ContainsKey(this.FullName))
                Binder.globalDelegateCache.Add(this.FullName, this.delegateCache);
        }
    }
}
