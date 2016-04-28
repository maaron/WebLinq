using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using IQToolkit;
using Serialize.Linq.Serializers;

namespace WebLinq
{
    public static class ExpressionExtensions
    {
        public static bool HasArgImplementing(this MethodCallExpression expression, Type type)
        {
            return expression.Arguments.FirstOrDefault(a => 
                a.Type.Implements(type)) != null;
        }
    }

    public static class TypeExtensions
    {
        public static bool Implements(this Type type, Type iface)
        {
            var ifaceDef = iface.IsGenericType ? 
                iface.GetGenericTypeDefinition() 
                : iface;

            return (type.IsGenericType ? type.GetGenericTypeDefinition() : type) == ifaceDef ||
                type.GetInterfaces().FirstOrDefault(i => 
                    (i.IsGenericType ? i.GetGenericTypeDefinition() : i) == ifaceDef)
                != default(Type);
        }
    }

    public class WebLinqSource<T>
    {
        public WebLinqProvider Provider { get; private set; }
        public Query<T> Data { get; private set; }

        public WebLinqSource(Expression<Func<IEnumerable<T>>> sourceExpression)
        {
            Provider = new WebLinqProvider(sourceExpression);
            Data = new Query<T>(Provider);
        }
    }

    public class WebLinqVisitor : System.Linq.Expressions.ExpressionVisitor
    {
        public Expression SourceExpression { get; private set; }

        public WebLinqVisitor(Expression sourceExpression)
        {
            SourceExpression = sourceExpression;
        }

        private static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
                e = ((UnaryExpression)e).Operand;

            return e;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.HasArgImplementing(typeof(IQueryable)))
            {
                var newArgs = node.Arguments.Select(a => StripQuotes(base.Visit(a))).ToArray();

                // TODO: This is an imperfect shortcut to find an Enumerable 
                // method that corresponds to the given Queryable method.  
                // Should work for most (if not all) LINQ operators, though, 
                // which is primarily what we are after.  To do this 
                // "correctly", i.e., the same as what the compiler does, 
                // we'd need to effectively reproduce extension method lookup, 
                // generic overload resolution, etc.  This is not trivial.
                var newMethod = typeof(Enumerable).GetMethods().FirstOrDefault(
                    m => m.Name == node.Method.Name 
                    && newArgs.Length == m.GetParameters().Length
                    && newArgs.Zip(m.GetParameters().Select(p => p.ParameterType), 
                        (arg, type) => arg.Type.Implements(type.GetGenericTypeDefinition())).All(b => b == true));

                if (newMethod == null)
                    throw new Exception("Couldn't find IEnumerable<> replacement for method " + node.Method.Name);

                if (newMethod.IsGenericMethod) newMethod = newMethod.MakeGenericMethod(node.Method.GetGenericArguments().Select(
                    t => t.Implements(typeof(IQueryable<>)) ? 
                        typeof(IEnumerable<>).MakeGenericType(t.GetGenericArguments()[0])
                        : t).ToArray());

                return Expression.Call(newMethod, newArgs);
            }
            else return base.VisitMethodCall(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            return node.Type.Implements(typeof(IQueryable)) ?
                Expression.Invoke(SourceExpression)
                : base.VisitConstant(node);
        }
    }

    public class WebLinqProvider : QueryProvider
    {
        public Expression SourceExpression { get; private set; }

        public WebLinqProvider(Expression sourceExpression)
        {
            SourceExpression = sourceExpression;
        }

        public override object Execute(Expression expression)
        {
            var command = GetQueryText(expression);

            var deserializer = new ExpressionSerializer(new JsonSerializer());
            var deserialized = deserializer.DeserializeText(command);

            return Expression.Lambda(deserialized).Compile().DynamicInvoke();
        }

        public override string GetQueryText(Expression expression)
        {
            var partial = PartialEvaluator.Eval(expression);

            return Serialize(new WebLinqVisitor(SourceExpression)
                .Visit(partial));
        }

        private string Serialize(Expression expression)
        {
            var serializer = new ExpressionSerializer(new JsonSerializer());
            return serializer.SerializeText(expression);
        }
    }
}
