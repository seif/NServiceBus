﻿/*
 Added reflection optimization techniques from Nate Kohari and Jimmy Bogard:

http://kohari.org/2009/03/06/fast-late-bound-invocation-with-expression-trees/

http://www.lostechies.com/blogs/jimmy_bogard/archive/2009/06/17/more-on-late-bound-invocations-with-expression-trees.aspx

http://www.lostechies.com/blogs/jimmy_bogard/archive/2009/08/05/late-bound-invocations-with-dynamicmethod.aspx 
 */

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace NServiceBus.Utils.Reflection
{
    /// <summary>
    /// Late Bound Method
    /// </summary>
    /// <param name="target">Target object</param>
    /// <param name="arguments">Arguments</param>
    /// <returns></returns>
    public delegate object LateBoundMethod(object target, object[] arguments);

    /// <summary>
    /// Late Bound Property
    /// </summary>
    /// <param name="target">Target Object</param>
    /// <returns></returns>
    public delegate object LateBoundProperty(object target);

    /// <summary>
    /// Late Bound Field
    /// </summary>
    /// <param name="target">Target Objects </param>
    /// <returns></returns>
    public delegate object LateBoundField(object target);

    /// <summary>
    /// Late Bound Field Set
    /// </summary>
    /// <param name="target">Target Object</param>
    /// <param name="value"></param>
    public delegate void LateBoundFieldSet(object target, object value);

    /// <summary>
    /// Late Bound Property Set
    /// </summary>
    /// <param name="target">Target Object</param>
    /// <param name="value"></param>
    public delegate void LateBoundPropertySet(object target, object value);

    /// <summary>
    /// Delegate Factory
    /// </summary>
	public static class DelegateFactory
	{
        /// <summary>
        /// Create Late Bound methods
        /// </summary>
        /// <param name="method">MethodInfo</param>
        /// <returns>LateBoundMethod</returns>
		public static LateBoundMethod Create(MethodInfo method)
		{
			ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");
			ParameterExpression argumentsParameter = Expression.Parameter(typeof(object[]), "arguments");

			MethodCallExpression call = Expression.Call(
				Expression.Convert(instanceParameter, method.DeclaringType),
				method,
				CreateParameterExpressions(method, argumentsParameter));

			Expression<LateBoundMethod> lambda = Expression.Lambda<LateBoundMethod>(
				Expression.Convert(call, typeof(object)),
				instanceParameter,
				argumentsParameter);

			return lambda.Compile();
		}

        /// <summary>
        /// Creates LateBoundProperty
        /// </summary>
        /// <param name="property">PropertyInfo</param>
        /// <returns>LateBoundProperty</returns>
        public static LateBoundProperty Create(PropertyInfo property)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");

            MemberExpression member = Expression.Property(Expression.Convert(instanceParameter, property.DeclaringType), property);

            Expression<LateBoundProperty> lambda = Expression.Lambda<LateBoundProperty>(
                Expression.Convert(member, typeof(object)),
                instanceParameter
                );

            return lambda.Compile();
        }

        /// <summary>
        /// LateBoundField
        /// </summary>
        /// <param name="field">FieldInfo</param>
        /// <returns></returns>
        public static LateBoundField Create(FieldInfo field)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");

            MemberExpression member = Expression.Field(Expression.Convert(instanceParameter, field.DeclaringType), field);

            Expression<LateBoundField> lambda = Expression.Lambda<LateBoundField>(
                Expression.Convert(member, typeof(object)),
                instanceParameter
                );

            return lambda.Compile();
        }

        /// <summary>
        /// Create filed set 
        /// </summary>
        /// <param name="field">FieldInfo</param>
        /// <returns>LateBoundFieldSet</returns>
        public static LateBoundFieldSet CreateSet(FieldInfo field)
        {
            var sourceType = field.DeclaringType;
            var method = new DynamicMethod("Set" + field.Name, null, new[] { typeof(object), typeof(object) }, true);
            var gen = method.GetILGenerator();
            
            gen.Emit(OpCodes.Ldarg_0); // Load input to stack
            gen.Emit(OpCodes.Castclass, sourceType); // Cast to source type
            gen.Emit(OpCodes.Ldarg_1); // Load value to stack
            gen.Emit(OpCodes.Unbox_Any, field.FieldType); // Unbox the value to its proper value type
            gen.Emit(OpCodes.Stfld, field); // Set the value to the input field
            gen.Emit(OpCodes.Ret);

            var callback = (LateBoundFieldSet)method.CreateDelegate(typeof(LateBoundFieldSet));

            return callback;
        }

        /// <summary>
        /// Creates Property Set 
        /// </summary>
        /// <param name="property">PropertyInfo</param>
        /// <returns>LateBoundPropertySet</returns>
        public static LateBoundPropertySet CreateSet(PropertyInfo property)
        {
            var method = new DynamicMethod("Set" + property.Name, null, new[] { typeof(object), typeof(object) }, true);
            var gen = method.GetILGenerator();

            var sourceType = property.DeclaringType;
            var setter = property.GetSetMethod(true);

            gen.Emit(OpCodes.Ldarg_0); // Load input to stack
            gen.Emit(OpCodes.Castclass, sourceType); // Cast to source type
            gen.Emit(OpCodes.Ldarg_1); // Load value to stack
            gen.Emit(OpCodes.Unbox_Any, property.PropertyType); // Unbox the value to its proper value type
            gen.Emit(OpCodes.Callvirt, setter); // Call the setter method
            gen.Emit(OpCodes.Ret);

            var result = (LateBoundPropertySet)method.CreateDelegate(typeof(LateBoundPropertySet));

            return result;
        }

        private static Expression[] CreateParameterExpressions(MethodInfo method, Expression argumentsParameter)
        {
            return method.GetParameters().Select((parameter, index) =>
                Expression.Convert(
                    Expression.ArrayIndex(argumentsParameter, Expression.Constant(index)),
                    parameter.ParameterType)).ToArray();
        }	
    }
}
