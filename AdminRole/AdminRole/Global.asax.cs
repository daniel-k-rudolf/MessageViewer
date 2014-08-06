using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using AdminRole.Models;
using AdminRole.Models.Binders;
using SolrNet;

namespace AdminRole
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}", // URL with parameters
                new { controller = "User", action = "Index" } // Parameter defaults
                );
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            //HibernatingRhinos.Profiler.Appender.EntityFramework.EntityFrameworkProfiler.Initialize();
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            ModelBinders.Binders[typeof (SearchParameters)] = new SearchParametersBinder();
            Startup.Init<SpremenljivkeSolr>(System.Configuration.ConfigurationManager.AppSettings["SolrUrl"]);

        }

        public IController GetContainerRegistration(IServiceProvider container, Type t)
        {
            var constructor = t.GetConstructors()[0];
            var dependencies = constructor.GetParameters().Select(p => container.GetService(p.ParameterType)).ToArray();
            return (IController)constructor.Invoke(dependencies);
        }

        public string GetControllerName(Type t)
        {
            return Regex.Replace(t.Name, "controller$", "", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Registers controllers in the DI container
        /// </summary>
        public void RegisterAllControllers()
        {
            var controllers = typeof(MvcApplication).Assembly.GetTypes().Where(t => typeof(IController).IsAssignableFrom(t));
            foreach (var controller in controllers)
            {
                Type controllerType = controller;
                Startup.Container.Register(GetControllerName(controller), controller, c => GetContainerRegistration(c, controllerType));
            }
        }


    }
}