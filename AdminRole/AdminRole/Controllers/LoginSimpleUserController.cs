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
    public class LoginSimpleUserController : Controller
    {
        //
        // GET: /LoginSimpleUser/

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
                if (parameters.Destination != null)
                {
                    queryInList = new SolrMultipleCriteriaQuery(
                            new[]
                            {
                                new SolrQueryInList("destination", parameters.Destination), 
                                new SolrQueryInList("sender", parameters.Destination),
                                new SolrQueryInList("sender", "LUKA")
                            },
                            "OR");
                }


                //naredi listo (poizvedbo) po ostalih spremenlivkah in destination ali sender
                var solrQueries = new List<ISolrQuery> { queryInList };

                #region OstaleSpremenljivke

                //if (!string.IsNullOrEmpty(parameters.Order))
                //    solrQueries.Add(new SolrQueryByField("order", parameters.Order));
                    

                //if (parameters.Exchangetimestamp.HasValue && parameters.Exchangetimestamp2.HasValue)
                //{
                //    solrQueries.Add(new SolrQueryByRange<DateTime>("exchangetimestamp",
                //        parameters.Exchangetimestamp.Value, parameters.Exchangetimestamp2.Value));
                //}

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

        public ActionResult Index(SearchParameters parameters, UserModel userModel)
        {
            try
            {
                #region TimeZone
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
                #endregion

                var view = new SpremenljivkeView
                {
                    Spremenljivke = null,
                    Search = parameters,
                    TimeZone = timeZone,
                };

                if (parameters != null && (!String.IsNullOrEmpty(parameters.Order) || !String.IsNullOrEmpty(parameters.Internal)))
                {
                    //Izvedi SORL query
                    var order = new SortOrder[]
                    {
                        new SortOrder("sequentialid", Order.ASC), 
                        new SortOrder("exchangetimestamp", Order.ASC),
                    };
                    var solr = ServiceLocator.Current.GetInstance<ISolrOperations<SpremenljivkeSolr>>();
                    var start = (parameters.PageIndex - 1) * parameters.PageSize;
                    var matchingSpremenljivke = solr.Query(BuildQuery(parameters), new QueryOptions
                    {
                        Rows = parameters.PageSize,
                        Start = start,
                        OrderBy = order.Concat(GetSelectedSort(parameters)).ToList(),
                    });

                    #region Internal process
                    
                    var seznam = new List<SpremenljivkeSolr>();

                    if (matchingSpremenljivke != null && matchingSpremenljivke.Count > 0)
                    {

                        //foreach (var variable in matchingSpremenljivke)
                        //{
                        //    if (variable.MsgT.Contains("DISP.NP") ||
                        //        variable.MsgT.Contains("DISP.ND"))
                        //    {

                        //    }

                        //}

                        //Logika sortiranja

                        var grupe = matchingSpremenljivke.GroupBy(s => s.Version);
                        foreach (IGrouping<long, SpremenljivkeSolr> grupa in grupe)
                        {
                            List<SpremenljivkeSolr> tmpList = new List<SpremenljivkeSolr>();
                            foreach (SpremenljivkeSolr tmpSolr in grupa)
                            {
                                tmpList.Add(tmpSolr); 
                            }
                        }

                        if (matchingSpremenljivke[0].MsgT.Contains("DISP.NP") || 
                            matchingSpremenljivke[0].MsgT.Contains("DISP.ND"))
                        {

                            

                            SpremenljivkeSolr spremenljivkeSolr = new SpremenljivkeSolr();

                            spremenljivkeSolr.Internal = matchingSpremenljivke[0].Internal;
                            spremenljivkeSolr.Exchangetimestamp = matchingSpremenljivke[0].Exchangetimestamp;
                            spremenljivkeSolr.Order = matchingSpremenljivke[matchingSpremenljivke.Count - 1].Order;
                            spremenljivkeSolr.Exchangetimestamp2 = matchingSpremenljivke[matchingSpremenljivke.Count - 1].Exchangetimestamp;
                            spremenljivkeSolr.MsgT = matchingSpremenljivke[matchingSpremenljivke.Count - 1].MsgT;
                            spremenljivkeSolr.Sender = matchingSpremenljivke[0].Sender;

                            seznam.Add(spremenljivkeSolr);
                        }
                    }

                    #endregion

                    view = new SpremenljivkeView
                    {
                        Spremenljivke = seznam,
                        Search = parameters,
                        TimeZone = timeZone,
                    };
                }

                return View("../LoginUser/Index", view);
            }
            catch (InvalidFieldException)
            {
                return View("../LoginUser/Index", new SpremenljivkeView { QueryError = true });
                throw;
            }
        }

        private string GetSpellCheckingResult(SolrQueryResults<SpremenljivkeSolr> matchingSpremenljivke)
        {
            return string.Join(" ", matchingSpremenljivke.SpellChecking
                .Select(c => c.Suggestions.FirstOrDefault())
                .Where(c => !string.IsNullOrEmpty(c))
                    .ToArray());
        }

        public ActionResult KraticeView(string sortOrder, string searchString, string currentFilter, int? pageKrat)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.Actice = true;
            ViewBag.KratView = String.IsNullOrEmpty(sortOrder) ? "krat_desc" : "";
            //Test
            try
            {
                using (var dbKrat = new userDbEntities())
                {
                    if (searchString != null)
                    {
                        pageKrat = 1;
                    }
                    else
                    {
                        searchString = currentFilter;
                    }

                    ViewBag.CurrentFilter = searchString;
                    var kraticeMsg = from k in dbKrat.KraticeTables select k;
                    if (!String.IsNullOrEmpty(searchString))
                    {
                        kraticeMsg = kraticeMsg.Where(k => k.Kratica.ToUpper().Contains(searchString.ToUpper()));
                    }
                    switch (sortOrder)
                    {
                        case "krat_desc":
                            kraticeMsg = kraticeMsg.OrderByDescending(k => k.Kratica);
                            break;
                        default:
                            kraticeMsg = kraticeMsg.OrderBy(k => k.Id_K);
                            break;
                    }
                    int pageSize = 10;
                    int pageNumber = (pageKrat ?? 1);
                    return View("../LoginUser/KraticeView", kraticeMsg.ToPagedList(pageNumber, pageSize));

                }

            }
            catch (Exception)
            {

                throw;
            }
        }

    }
}
