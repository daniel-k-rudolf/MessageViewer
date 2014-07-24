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
                        //Rows = parameters.PageSize,
                        //Start = start,
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

                            //izloči ven kdo je pošiljatelj iz glave sporočila
                            var tmp = m.Data.Split(' ', '\t')[0];
                            var tmp2 = m.Data.Split(' ', '\t')[54];

                            if (tmp == parameters.Destination + "LUKA")
                            {
                                destinacije.Add(m.Data.Split('#')[7]);
                            }
                            else if (tmp2 == parameters.Destination)
                            {
                                destinacije.Add(m.Data.Split(' ', '\t')[54]);
                            }

                        });

                        if (destinacije.Count != 0)
                        {
                            List<SpremenljivkeSolr> obdelaneMatchingSpremenljivke = new List<SpremenljivkeSolr>();

                            matchingSpremenljivke.ForEach(m =>
                            {
                                if (destinacije.Contains(m.Destination.FirstOrDefault()) ||
                                    destinacije.Contains(m.Sender.FirstOrDefault()))
                                {
                                    obdelaneMatchingSpremenljivke.Add(m);
                                }
                            });

                            matchingSpremenljivke.Clear();
                            obdelaneMatchingSpremenljivke.ForEach(matchingSpremenljivke.Add);

                            #region Koda Razvrščanja
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

                                        #region 2) ODIS.XX

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

                                        #endregion

                                    });

                                    if (slovarSolr.Keys.Count > 0)
                                    {
                                        int stDogodek = 1;

                                        foreach (int key in slovarSolr.Keys)
                                        {
                                            var seznamSolrSpre = slovarSolr[key];

                                            #region PodatkiZaPrikaz
                                            SpremenljivkeSolr spremenljivkeSolr = new SpremenljivkeSolr();

                                            spremenljivkeSolr.StevilkaDogodek = stDogodek;
                                            spremenljivkeSolr.Internal = seznamSolrSpre[0].Internal;
                                            spremenljivkeSolr.Exchangetimestamp = seznamSolrSpre[0].Exchangetimestamp;
                                            spremenljivkeSolr.Order = seznamSolrSpre[seznamSolrSpre.Count - 1].Order;
                                            spremenljivkeSolr.Exchangetimestamp2 = seznamSolrSpre[seznamSolrSpre.Count - 1].Exchangetimestamp;
                                            spremenljivkeSolr.MsgT = seznamSolrSpre[seznamSolrSpre.Count - 1].MsgT;
                                            spremenljivkeSolr.Sender = seznamSolrSpre[0].Sender;

                                            seznam.Add(spremenljivkeSolr);
                                            stDogodek++;

                                            #endregion
                                        }
                                    }
                                    #endregion
                                }

                            }
                            #endregion
                        }
                    }

                    #endregion

                    List<SpremenljivkeSolr> tmpSeznam = new List<SpremenljivkeSolr>();

                    for (int i = start; i < (start + parameters.PageSize); i++)
                    {
                        if (i < seznam.Count)
                        {
                            tmpSeznam.Add(seznam[i]);                            
                        }
                        else
                        {
                            break;
                        }
                    }

                    view = new SpremenljivkeView
                    {
                        Spremenljivke = tmpSeznam,
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
    }
}
