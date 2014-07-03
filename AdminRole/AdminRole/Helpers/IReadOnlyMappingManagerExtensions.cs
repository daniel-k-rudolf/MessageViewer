using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using Microsoft.Ajax.Utilities;
using SolrNet;

namespace AdminRole.Helpers
{
    public static class ReadOnlyMappingManagerExtensions
    {
        public static string FieldName<T>(this IReadOnlyMappingManager mapper, Expression<Func<T, object>> property)
        {
            var propertyName = property.MemberName();
            return mapper.GetFields(typeof (T)).Values.First(p => p.Property.Name == propertyName).FieldName;
        }
    }
}