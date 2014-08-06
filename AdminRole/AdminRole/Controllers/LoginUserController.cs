using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Providers.Entities;
using System.Web.UI.WebControls;
using System.Web.WebPages;
using Microsoft.Ajax.Utilities;
using Microsoft.Practices.ServiceLocation;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.DSL;
using AdminRole.Models;
using SolrNet.Exceptions;
using SolrNet.Impl.FieldSerializers;
using System.Linq;
using PagedList;

namespace AdminRole.Controllers
{
    [HandleError]
    [Authorize]
    public class LoginUserController : Controller
    {
        //
        // GET: /LoginUser/

        public ISolrQuery BuildQuery(SearchParameters parameters)
        {

            //identificiraj uporabnika
            string tmpUser = System.Web.HttpContext.Current.User.Identity.Name;

            //najdi userja v bazi in primerjaj njegovo uporabniško ime z Identity name
            using (var dbUs = new userDbEntities())
            {
                var kdoseprijavlja = (from user in dbUs.UsersTables
                                      where user.username == tmpUser
                                      select user.userID).FirstOrDefault();

                //get all destinations for user userid
                var seznam = dbUs.CustomerTables.Where(m => m.UserId == kdoseprijavlja).Select(m => m.Customer);

                var seznam2 = dbUs.CustomTypes.Where(m => seznam.Contains(m.Id_NewCustomerType)).Select(m => m.CustomerType);

                //Lista za dodajanje katerih customerjev ima posamezni uporabnik
                List<string> result = new List<string>();

                foreach (var customer in seznam2)
                {
                    result.Add(customer);
                }

                //naredi glavno listo (poizvedbo) po Solr: Destination ali Sender
                SolrMultipleCriteriaQuery queryInList = null;
                if (parameters.Destination == null)
                {
                    queryInList =
                        new SolrMultipleCriteriaQuery(
                            new[]
                            {
                                new SolrQueryInList("destination", result), 
                                new SolrQueryInList("sender", result)
                            },
                            "OR");
                }
                else
                {
                    queryInList = new SolrMultipleCriteriaQuery(
                            new[]
                            {
                                new SolrQueryInList("destination", parameters.Destination), 
                                new SolrQueryInList("sender", parameters.Destination),
                            },
                            "OR");
                }

                //naredi listo (poizvedbo) po ostalih spremenlivkah in destination ali sender
                var solrQueries = new List<ISolrQuery> { queryInList };

                #region OstaleSpremenljivke

                if (!string.IsNullOrEmpty(parameters.Order))
                    solrQueries.Add(new SolrQueryByField("order", parameters.Order));

                if (parameters.Exchangetimestamp.HasValue && parameters.Exchangetimestamp2.HasValue)
                {
                    solrQueries.Add(new SolrQueryByRange<DateTime>("exchangetimestamp",
                        parameters.Exchangetimestamp.Value, parameters.Exchangetimestamp2.Value));
                }

                #region IntrernalSearch

                if (!string.IsNullOrEmpty(parameters.Internal))
                {
                    string str = parameters.Internal;
                    if (str.Length >= 6)
                    {
                        string s = str;
                        string no_start_zeros = s.TrimStart('0');
                        var valueNoZero = no_start_zeros;
                        solrQueries.Add(new SolrMultipleCriteriaQuery(new[]
                        {
                            new SolrQueryByField("internal", parameters.Internal),
                            new SolrQueryByField("internal", valueNoZero)
                        }, "OR"));

                    }

                    else
                    {

                        var nule = str.Length + (6 - str.Length);
                        solrQueries.Add(new SolrMultipleCriteriaQuery(new[]
                        {
                            new SolrQueryByField("internal", parameters.Internal.PadLeft(nule, '0')),
                            new SolrQueryByField("internal", parameters.Internal)
                        }, "OR"));
                    }

                }

                #endregion

                #endregion;

                return new SolrMultipleCriteriaQuery(solrQueries, "AND");
            }
        }

        public SortOrder[] GetSelectedSort(SearchParameters parameters)
        {
            return new[] { SortOrder.Parse(parameters.Sort) }.Where(o => o != null).ToArray();
        }

        [Authorize]
        public ActionResult Index(SearchParameters parameters, UserModel userModel)
        {
            {
                try
                {
                    TimeZoneInfo timeZone;
                    using (var dbContext = new userDbEntities())
                    {
                        var user = dbContext.UsersTables.FirstOrDefault(o => o.username == User.Identity.Name);
                        if (user == null)
                            throw new NullReferenceException("user");
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId);

                        #region DropDownList

                        //identificiraj uporabnika
                        string tmpUser = System.Web.HttpContext.Current.User.Identity.Name;

                        //najdi userja v bazi in primerjaj njegovo uporabniško ime z Identity name
                        var kdoseprijavlja = (from dllist in dbContext.UsersTables
                                              where dllist.username == tmpUser
                                              select dllist.userID).FirstOrDefault();

                        //get all destinations for user userid
                        var seznam = dbContext.CustomerTables.Where(m => m.UserId == kdoseprijavlja).Select(m => m.Customer);

                        var seznam2 = dbContext.CustomTypes.Where(m => seznam.Contains(m.Id_NewCustomerType)).Select(m => m.CustomerType);

                        List<string> result = new List<string>();

                        foreach (var customer in seznam2)
                        {
                            result.Add(customer);
                        }

                        ViewBag.DropList = new SelectList(result);
                        ViewBag.Destination = parameters.Destination;
                        
                        #endregion
                    }

                    var order = new SortOrder[] { new SortOrder("sequentialid", Order.DESC), new SortOrder("exchangetimestamp", Order.DESC) };
                    var solr = ServiceLocator.Current.GetInstance<ISolrOperations<SpremenljivkeSolr>>();
                    var start = (parameters.PageIndex - 1) * parameters.PageSize;

                    var matchingSpremenljivke = solr.Query(BuildQuery(parameters), new QueryOptions
                    {
                        Rows = parameters.PageSize,
                        Start = start,
                        OrderBy = order.Concat(GetSelectedSort(parameters)).ToList(),
                        //SpellCheck = new SpellCheckingParameters(),
                        Grouping = new GroupingParameters()
                        {
                            Fields = new[] { "breadcrumbid" },
                            Format = GroupingFormat.Grouped,
                            Limit = 100
                        },
                    });

                    var view = new SpremenljivkeView
                    {
                        Spremenljivke = matchingSpremenljivke
                                        .Grouping["breadcrumbid"]
                                        .Groups.GroupBy(r => r.Documents)
                                        .Select(r => r.Key)
                                        .Select(r => r.FirstOrDefault()).ToList(),
                        Search = parameters,
                        TotalCount = matchingSpremenljivke.Grouping["breadcrumbid"].Matches,
                        //AliSiMislil = GetSpellCheckingResult(matchingSpremenljivke),
                        TimeZone = timeZone,
                    };

                    return View(view);
                }
                catch (InvalidFieldException)
                {
                    return View(new SpremenljivkeView { QueryError = true });
                    throw;
                }
            }
        }

        private string GetSpellCheckingResult(SolrQueryResults<SpremenljivkeSolr> matchingSpremenljivke)
        {
            return string.Join(" ", matchingSpremenljivke.SpellChecking
                .Select(c => c.Suggestions.FirstOrDefault())
                .Where(c => !string.IsNullOrEmpty(c))
                    .ToArray());
        }
 
    }
}
