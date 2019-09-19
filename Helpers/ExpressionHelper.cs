using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace GenericRestApi.Helpers
{
    public abstract class ExpressionHelper<T>
    {
        public static Expression<Func<T, bool>> CreateSearchExpression(IQueryable<T> query, List<string> filterProps, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filterProps.Count == 0)
                throw new Exception("No es posible crear el arbol de expresiones");

            var parameter = Expression.Parameter(typeof(T), "e");
            var members = GenerateFilterableMembers(filterProps, parameter);
            var constant = Expression.Constant(filter.ToLower());
            var searchExp = GenerateSearchExpression(members, constant);

            return Expression.Lambda<Func<T, bool>>(searchExp, parameter);
        }

        public static Expression<Func<T, bool>> CreateSoftDeleteExpression(IQueryable<T> query)
        {
            var parameter = Expression.Parameter(typeof(T), "e");
            var member = Expression.Property(parameter, "Activo");
            var constant = Expression.Constant(true);
            // e => e.Activo == true
            var activeExp = Expression.Equal(member, constant);

            return Expression.Lambda<Func<T, bool>>(activeExp, parameter);
        }

        public static MethodCallExpression CreateOrderByExpression(IQueryable<T> source, string sortProperty, string sortOrder)
        {
            if (string.IsNullOrEmpty(sortProperty) || string.IsNullOrEmpty(sortOrder))
                throw new Exception();

            var type = typeof(T);
            var parameter = Expression.Parameter(type, "p");
            var member = sortProperty.Split('.')
                .Aggregate((Expression)parameter, Expression.PropertyOrField);
            var selector = Expression.Lambda(member, parameter);
            var typeArguments = new Type[] { type, member.Type };
            var methodName = sortOrder.ToLower() == "desc" ? "OrderByDescending" : "OrderBy";
            var orderCallExp = Expression.Call(typeof(Queryable), methodName, typeArguments,
                source.Expression, Expression.Quote(selector));

            return orderCallExp;
        }

        // convierte un string a capitalize
        private static string FirstCharToUpper(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return char.ToUpper(value[0]) + value.Substring(1);
        }

        private static MemberExpression[] GenerateFilterableMembers(List<string> filterProps, ParameterExpression parameter)
        {
            var members = new MemberExpression[filterProps.Count()];

            for (int i = 0; i < filterProps.Count(); i++)
            {
                if (filterProps[i].Contains('.'))
                {   // el filtro es una propiedad de una entidad anidada
                    // ej. u => u.Rol.Nombre
                    Expression nestedMember = parameter;
                    foreach (var prop in filterProps[i].Split('.'))
                    {
                        nestedMember = Expression.PropertyOrField(nestedMember, prop);
                    }
                    members[i] = (MemberExpression)nestedMember;
                }
                else
                {
                    // el filtro es una propiedad de la entidad
                    // ej. u => u.Username
                    members[i] = Expression.Property(parameter, filterProps[i]);
                }
            }

            return members;
        }

        private static Expression GenerateSearchExpression(MemberExpression[] members, ConstantExpression constant)
        {
            var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            var toLowerMethod = typeof(string).GetMethod("ToLower", System.Type.EmptyTypes);
            Expression searchExp = null;

            foreach (var member in members)
            {
                // e => e.Member != null
                var notNullExp = Expression.NotEqual(member, Expression.Constant(null));
                // e => e.Member.ToLower() 
                var toLowerExp = Expression.Call(member, toLowerMethod);
                // e => e.Member.Contains(value)
                var containsExp = Expression.Call(toLowerExp, containsMethod, constant);
                // e => e.Member != null && e.Member.Contains(value)
                var filterExpression = Expression.AndAlso(notNullExp, containsExp);

                searchExp = searchExp == null ? (Expression)filterExpression : Expression.OrElse(searchExp, filterExpression);
            }

            return searchExp;
        }
    }
}