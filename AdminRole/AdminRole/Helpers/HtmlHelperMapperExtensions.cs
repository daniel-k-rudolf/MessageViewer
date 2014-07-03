using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using Microsoft.Practices.ServiceLocation;
using SolrNet;

namespace AdminRole.Helpers
{
    public static class HtmlHelperMapperExtensions
    {
        private static IReadOnlyMappingManager mapper
        {
            get { return ServiceLocator.Current.GetInstance<IReadOnlyMappingManager>(); }
        }

        public static string SolrFiledPropName<T>(this HtmlHelper helper, string fieldName)
        {
            return mapper.GetFields(typeof (T))[fieldName].Property.Name;
        }
    }
}