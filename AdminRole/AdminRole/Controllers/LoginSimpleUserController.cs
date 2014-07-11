using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;
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
using ListExtensions = WebGrease.Css.Extensions.ListExtensions;

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
                                new SolrQueryInList("sender", "LUKA"),
                            },
                            "OR");
                }


                //naredi listo (poizvedbo) po ostalih spremenlivkah in destination ali sender
                var solrQueries = new List<ISolrQuery> { queryInList };

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
                        Grouping = new GroupingParameters()
                        {
                            Fields = new[] { "sequentialid" },
                            Format = GroupingFormat.Grouped,
                            Limit = 100,
                        }
                    }).Grouping["sequentialid"]
                                        .Groups.GroupBy(r => r.Documents)
                                        .Select(r => r.Key)
                                        .Select(r => r.FirstOrDefault()).ToList();
                    
                    #region Internal process
                    
                    var seznam = new List<SpremenljivkeSolr>();

                    if (matchingSpremenljivke != null && matchingSpremenljivke.Count > 0)
                    {
                        List<string> destinacije = new List<string>();

                        matchingSpremenljivke.ForEach(m =>
                        {
                            if (m.MsgT.Contains("DISP.NP") ||
                                m.MsgT.Contains("DISP.ND") || 
                                m.MsgT.Contains("DISP.LP"))
                            {
                                destinacije.Add(m.Data.Split('#')[7]);      
                            }
                            else if (m.MsgT.Contains("ODIS.LF") || 
                                     m.MsgT.Contains("ODIS.LE") ||
                                     m.MsgT.Contains("ODIS.LD") ||
                                     m.MsgT.Contains("ODIS.LV"))
                            {
                                destinacije.Add(m.Data.Split(' ', '\t')[54]);
                            }
                        }); 
 
                        if (destinacije[0] == parameters.Destination)
                        {

                            matchingSpremenljivke.ForEach(m =>
                            {
                                if (m.Sequ == 0)
                                {
                                    matchingSpremenljivke.Remove(m);
                                }
                            });

                            if (matchingSpremenljivke[0].MsgT.Contains("DISP.NP") ||
                                matchingSpremenljivke[0].MsgT.Contains("DISP.ND"))
                            {
                                #region LogikaRazvrscanja
                                //Tmp spremenljivke
                                int stevecDisp = 1;
                                int stevecOdis = 0;

                                Dictionary<int, List<SpremenljivkeSolr>> slovarSolr = new Dictionary<int, List<SpremenljivkeSolr>>();

                                matchingSpremenljivke.ForEach(s =>
                                 {
                                     #region 1) DISP.NP/ND
                                        if (s.MsgT.Contains("DISP.NP") ||
                                            s.MsgT.Contains("DISP.ND"))
                                        {
                                            if (slovarSolr.ContainsKey(stevecDisp))
                                            {
                                                var values = slovarSolr[stevecDisp];
                                                if (values != null)
                                                {
                                                    slovarSolr[stevecDisp].Add(s);
                                                }
                                                else
                                                {
                                                    List<SpremenljivkeSolr> list = new List<SpremenljivkeSolr>();
                                                    list.Add(s);
                                                    slovarSolr.Add(stevecDisp, list);
                                                }
                                            }
                                            else
                                            {
                                                List<SpremenljivkeSolr> list = new List<SpremenljivkeSolr>();
                                                list.Add(s);
                                                slovarSolr.Add(stevecDisp, list);
                                            }

                                            stevecDisp++;
                                            stevecOdis++;
                                        }
                                     #endregion

                                     #region 2) ODIS.LE/LF/LP/LD/LZ

                                     using (var dbKratica = new userDbEntities())
                                     {

                                         var seznamKrat =
                                             dbKratica.KraticeTables
                                             .Where(a => (a.Kratica != "DISP.ND") && (a.Kratica != "DISP.NP"))
                                             .Select(a => a.Kratica);

                                         foreach (var test in seznamKrat)
                                         {
                                             if (s.MsgT.Contains(test))
                                             {
                                                 if (slovarSolr.ContainsKey(stevecOdis))
                                                 {
                                                     var values = slovarSolr[stevecOdis];
                                                     if (values != null)
                                                     {
                                                         slovarSolr[stevecOdis].Add(s);
                                                     }
                                                     else
                                                     {
                                                         List<SpremenljivkeSolr> list = new List<SpremenljivkeSolr>();
                                                         list.Add(s);
                                                         slovarSolr.Add(stevecOdis, list);
                                                     }
                                                 }
                                                 else
                                                 {
                                                     List<SpremenljivkeSolr> list = new List<SpremenljivkeSolr>();
                                                     list.Add(s);
                                                     slovarSolr.Add(stevecOdis, list);
                                                 }
                                             }
                                         }
      
                                     }

                                    
                                        //if (s.MsgT.Contains("ODIS.LE") ||
                                        //    s.MsgT.Contains("ODIS.LF") ||
                                        //    s.MsgT.Contains("ODIS.LP") ||
                                        //    s.MsgT.Contains("ODIS.LD") ||
                                        //    s.MsgT.Contains("ODIS.LZ") ||
                                        //    s.MsgT.Contains("ODIS.LC") ||
                                        //    s.MsgT.Contains("DISP.LP")) 
                                        //{
                                        //    if (slovarSolr.ContainsKey(stevecOdis))
                                        //    {
                                        //        var values = slovarSolr[stevecOdis];
                                        //        if (values != null)
                                        //        {
                                        //            slovarSolr[stevecOdis].Add(s);
                                        //        }
                                        //        else
                                        //        {
                                        //            List<SpremenljivkeSolr> list = new List<SpremenljivkeSolr>();
                                        //            list.Add(s);
                                        //            slovarSolr.Add(stevecOdis, list);
                                        //        }
                                        //    }
                                        //    else
                                        //    {
                                        //        List<SpremenljivkeSolr> list = new List<SpremenljivkeSolr>();
                                        //        list.Add(s);
                                        //        slovarSolr.Add(stevecOdis, list);
                                        //    }
                                        //}
                                        #endregion

                                 });

                                if (slovarSolr.Keys.Count > 0)
                                {
                                    foreach (int key in slovarSolr.Keys)
                                    {
                                        var seznamSolrSpre = slovarSolr[key];

                                        #region PodatkiZaPrikaz
                                        SpremenljivkeSolr spremenljivkeSolr = new SpremenljivkeSolr();

                                        spremenljivkeSolr.Internal = seznamSolrSpre[0].Internal;
                                        spremenljivkeSolr.Exchangetimestamp = seznamSolrSpre[0].Exchangetimestamp;
                                        spremenljivkeSolr.Order = seznamSolrSpre[seznamSolrSpre.Count - 1].Order;
                                        spremenljivkeSolr.Exchangetimestamp2 = seznamSolrSpre[seznamSolrSpre.Count - 1].Exchangetimestamp;
                                        spremenljivkeSolr.MsgT = seznamSolrSpre[seznamSolrSpre.Count - 1].MsgT;
                                        spremenljivkeSolr.Sender = seznamSolrSpre[0].Sender;

                                        seznam.Add(spremenljivkeSolr);
                                        #endregion
                                    }
                                }
                                #endregion
                            }

                        }

                        #region ZaPregled
                        //foreach (var variable in matchingSpremenljivke)
                        //{
                        //    if (variable.MsgT.Contains("DISP.NP") ||
                        //        variable.MsgT.Contains("DISP.ND"))
                        //    {

                        //    }

                        //}
                        
                        //var grupe = matchingSpremenljivke.GroupBy(s => s.Version);
                        //foreach (IGrouping<long, SpremenljivkeSolr> grupa in grupe)
                        //{
                        //    List<SpremenljivkeSolr> tmpList = new List<SpremenljivkeSolr>();
                        //    foreach (SpremenljivkeSolr tmpSolr in grupa)
                        //    {
                        //        tmpList.Add(tmpSolr); 
                        //    }
                        //}
                        #endregion
                        
                    }

                    #endregion

                    view = new SpremenljivkeView
                    {
                        Spremenljivke = seznam,
                        Search = parameters,
                        TotalCount = seznam.Count,
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
