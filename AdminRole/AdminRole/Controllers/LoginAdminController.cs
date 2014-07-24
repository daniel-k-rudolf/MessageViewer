using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using AdminRole.Models;
using Microsoft.Practices.ServiceLocation;
using System.Data;
using PagedList;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.Exceptions;

namespace AdminRole.Controllers
{
    [Authorize, HandleError]
    public class LoginAdminController : Controller
    {
        private userDbEntities _entities = new userDbEntities();

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

                var seznam2 =
                    dbUs.CustomTypes.Where(m => seznam.Contains(m.Id_NewCustomerType)).Select(m => m.CustomerType);

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
                            new[] { new SolrQueryInList("destination", result), new SolrQueryInList("sender", result) },
                            "OR");
                }
                else
                {
                    queryInList = new SolrMultipleCriteriaQuery(
                            new[] { new SolrQueryInList("destination", parameters.Destination), new SolrQueryInList("sender", parameters.Destination) },
                            "OR");
                }

                var solrQueries = new List<ISolrQuery> { queryInList };

                #region OstalaPolja

                //if (!string.IsNullOrEmpty(parameters.FreeSearch))
                //    solrQueries.Add(new SolrQuery(parameters.FreeSearch));

                if (!string.IsNullOrEmpty(parameters.Sequ))
                    solrQueries.Add(new SolrQueryByField("sequentialid", parameters.Sequ));

                if (!string.IsNullOrEmpty(parameters.Agent))
                    solrQueries.Add(new SolrQueryByField("agent", parameters.Agent));

                if (!string.IsNullOrEmpty(parameters.Voyage))
                    solrQueries.Add(new SolrQueryByField("voyage", parameters.Voyage));

                if (!string.IsNullOrEmpty(parameters.Vessel))
                    solrQueries.Add(new SolrQueryByField("vessel", parameters.Vessel));

                if (!string.IsNullOrEmpty(parameters.MsgT))
                    solrQueries.Add(new SolrQueryByField("msgtype", parameters.MsgT));

                if (!string.IsNullOrEmpty(parameters.Order))
                    solrQueries.Add(new SolrQueryByField("order", parameters.Order));

                if (parameters.Exchangetimestamp.HasValue && parameters.Exchangetimestamp2.HasValue)
                {
                    solrQueries.Add(new SolrQueryByRange<DateTime>("exchangetimestamp",
                        parameters.Exchangetimestamp.Value, parameters.Exchangetimestamp2.Value));
                }

                if (!string.IsNullOrEmpty(parameters.MsgS))
                    solrQueries.Add(new SolrQueryByField("msg_state", parameters.MsgS));

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

        [Authorize]
        public ActionResult AdminIndex(SearchParameters parameters)
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

                        #region DropDawnList

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
                        //TotalCount = matchingSpremenljivke.NumFound,
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

        [Authorize]
        public ActionResult Registracija(string sortOrder, string searchString, string currentFilter, int? page)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.Active = true;
            ViewBag.NameSortParm = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            //ViewBag.Customer = sortOrder == "Customer" ? "cust_desc" : "Customer";

            try
            {
                using (var db = new userDbEntities())
                {
                    if (searchString != null)
                    {
                        page = 1;
                    }
                    else
                    {
                        searchString = currentFilter;
                    }

                    ViewBag.CurrentFilter = searchString;

                    var uporabniki = from u in db.UsersTables select u;
                    if (!String.IsNullOrEmpty(searchString))
                    {
                        uporabniki = uporabniki.Where(u => u.username.ToUpper().Contains(searchString.ToUpper()));
                    }

                    switch (sortOrder)
                    {
                        case "name_desc":
                            uporabniki = uporabniki.OrderByDescending(u => u.username);
                            break;
                        default:
                            uporabniki = uporabniki.OrderBy(u => u.userID);
                            break;
                    }
                    int pageSize = 10;
                    int pageNumber = (page ?? 1);
                    return View(uporabniki.ToPagedList(pageNumber, pageSize));
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        public ActionResult CreateNew(UserModel userModel, string act = null, int idx = 0) //UserModel za edit!!!!!
        {
            using (var dbContext = new userDbEntities())
            {
                if (userModel.User == null)
                {
                    userModel.User = new UsersTable();
                }
                var newUser = userModel.User.userID == 0;
                userModel.CustomTypes = dbContext.CustomTypes.ToList();

                # region VlogaUporabnika

                ViewBag.DropListVloge = new SelectList(dbContext.VlogaUporabnikas.Select(v => v.Naziv).ToList());

                ViewBag.SelectedVloga = userModel.User.VlogaUporabnika != null ? userModel.User.VlogaUporabnika.Naziv : userModel.VlogaNaziv;

                # endregion

                switch (act)
                {
                    case "addcustom":
                        userModel.User.CustomerTables.Add(new CustomerTable
                        {
                            CustomType = new CustomType(),
                            UsersTable = userModel.User
                        });
                        break;
                    case "deletecustom":
                        userModel.User.CustomerTables.RemoveAt(idx);
                        break;
                    case "save":
                        foreach (var customer in userModel.User.CustomerTables)
                        {
                            customer.CustomType = dbContext.CustomTypes.Find(customer.CustomType.Id_NewCustomerType);
                        }

                        if (newUser)
                        {
                            var crypto = new SimpleCrypto.PBKDF2();
                            if (userModel.User.password == null)
                            {
                                ModelState.AddModelError("Password", "You forgot to enter password for new user!");
                            }
                            else
                            {
                                if (userModel.User.username == null)
                                {
                                    ModelState.AddModelError("Username", "You forgot to enter username for new user!");
                                }
                                else
                                {
                                    userModel.User.password = crypto.Compute(userModel.User.password);
                                    userModel.User.passwordSalt = crypto.Salt;

                                    var vlogaUporabnika = dbContext.VlogaUporabnikas.FirstOrDefault(v => v.Naziv == userModel.VlogaNaziv);
                                    if (vlogaUporabnika != null)
                                    {
                                        userModel.User.VlogaUporabnika = vlogaUporabnika;
                                    }

                                    var count = dbContext.UsersTables.Count(u => u.username == userModel.User.username);
                                    if (count == 0)
                                    {
                                        dbContext.UsersTables.Add(userModel.User);
                                        dbContext.SaveChanges();
                                        return new ContentResult() { Content = "success" };
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("Username", "This username already exists!");
                                    }
                                }
                            }
                        }
                        else
                        {
                            var dbUser = dbContext.UsersTables.Find(userModel.User.userID);
                            dbUser.TimeZoneId = userModel.User.TimeZoneId;
                            foreach (var custom in userModel.User.CustomerTables)
                            {
                                if (custom.CustomerID == 0)
                                {
                                    dbUser.CustomerTables.Add(custom);
                                }

                            }
                            foreach (var custom in dbUser.CustomerTables.ToList())
                            {
                                if (userModel.User.CustomerTables.All(o => o.CustomerID != custom.CustomerID))
                                    dbUser.CustomerTables.Remove(custom);
                            }
                            dbContext.SaveChanges();
                        }
                        break;
                }
                return View("CreateNew", userModel);
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        public ActionResult CustomerTypeView(string sortOrderCust, string searchStringCust, string currentFilterCust, int? pageCust)
        {
            ViewBag.CurrentSortCust = sortOrderCust;
            ViewBag.Active = true;
            ViewBag.CustView = String.IsNullOrEmpty(sortOrderCust) ? "cust_desc" : "";

            try
            {
                using (var dbCust = new userDbEntities())
                {
                    if (searchStringCust != null)
                    {
                        pageCust = 1;
                    }
                    else
                    {
                        searchStringCust = currentFilterCust;
                    }

                    ViewBag.CurrentFilterCustomer = searchStringCust;
                    var customers = from c in dbCust.CustomTypes select c;
                    if (!String.IsNullOrEmpty(searchStringCust))
                    {
                        customers = customers.Where(c => c.CustomerType.ToUpper().Contains(searchStringCust.ToUpper()));
                    }
                    switch (sortOrderCust)
                    {
                        case "cust_desc":
                            customers = customers.OrderByDescending(c => c.CustomerType);
                            break;
                        default:
                            customers = customers.OrderBy(c => c.Id_NewCustomerType);
                            break;
                    }
                    int pageSize = 10;
                    int pageNumber = (pageCust ?? 1);
                    return View(customers.ToPagedList(pageNumber, pageSize));
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        public ActionResult CreateNewCustomerType(Models.CustomerType customerType)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var dataC = new userDbEntities())
                    {
                        var sysCustomerType = dataC.CustomTypes.Create();

                        if (sysCustomerType != null)
                        {
                            sysCustomerType.CustomerType = customerType.CustomType;

                            var count = dataC.CustomTypes.Count(c => c.CustomerType == customerType.CustomType);
                            if (count == 0)
                            {
                                dataC.CustomTypes.Add(sysCustomerType);
                                dataC.SaveChanges();
                                return RedirectToAction("CustomerTypeView", "LoginAdmin");
                            }
                            else
                            {
                                ModelState.AddModelError("CustomType", "This Customer already exists!");
                                //return View(customerType);
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("CustomType", "You forgot to enter customer type for new user!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    string error = ex.Message;
                }
            }
            return View(customerType);
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        public ActionResult Edit(int id = 0)
        {
            using (var dbVn = new userDbEntities())
            {
                var edit2 = dbVn.UsersTables.Where(o => o.userID == id)
                    .Include(o => o.CustomerTables.Select(c => c.CustomType))
                    .AsNoTracking()
                    .FirstOrDefault();

                if (edit2 == null)
                {
                    return HttpNotFound();
                }
                return Edit(new UserModel() { User = edit2 });
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(UserModel userModel, string act = null, int idx = 0)
        {
            using (var dbContext = new userDbEntities())
            {
                if (userModel.User == null)
                {
                    userModel.User = new UsersTable();
                }
                //var newUser = userModel.User.userID == 0;
                userModel.CustomTypes = dbContext.CustomTypes.ToList();

                # region VlogaUporabnika

                ViewBag.DropListVloge = new SelectList(dbContext.VlogaUporabnikas.Select(v => v.Naziv).ToList());

                ViewBag.SelectedVloga = userModel.User.VlogaUporabnika != null ? userModel.User.VlogaUporabnika.Naziv : userModel.VlogaNaziv;

                # endregion

                switch (act)
                {
                    case "addcustom":
                        userModel.User.CustomerTables.Add(new CustomerTable
                        {
                            CustomType = new CustomType(),
                            UsersTable = userModel.User
                        });
                        break;
                    case "deletecustom":
                        userModel.User.CustomerTables.RemoveAt(idx);
                        foreach (var customer in userModel.User.CustomerTables)
                        {
                            customer.CustomType = dbContext.CustomTypes.Find(customer.CustomType.Id_NewCustomerType);
                        }
                        var dbUser1 = dbContext.UsersTables.Find(userModel.User.userID);
                        dbUser1.TimeZoneId = userModel.User.TimeZoneId;
                        foreach (var custom in userModel.User.CustomerTables)
                        {
                            if (custom.CustomerID == 0)
                            {
                                dbUser1.CustomerTables.Add(custom);
                            }
                        }
                        foreach (var custom in dbUser1.CustomerTables.ToList())
                        {
                            var modelCustom =
                                userModel.User.CustomerTables.FirstOrDefault(o => o.CustomerID == custom.CustomerID);
                            if (modelCustom != null) //update it
                            {
                                custom.CustomType =
                                    dbContext.CustomTypes.Find(modelCustom.CustomType.Id_NewCustomerType);
                            }

                            if (userModel.User.CustomerTables.All(o => o.CustomerID != custom.CustomerID))
                                dbContext.Entry(custom).State = EntityState.Deleted;
                        }
                        dbContext.SaveChanges();
                        break;
                    case "save":
                        foreach (var customer in userModel.User.CustomerTables)
                        {
                            customer.CustomType = dbContext.CustomTypes.Find(customer.CustomType.Id_NewCustomerType);
                        }
                        var dbUser = dbContext.UsersTables.Find(userModel.User.userID);
                        dbUser.TimeZoneId = userModel.User.TimeZoneId;

                        var vlogaUporabnika = dbContext.VlogaUporabnikas.FirstOrDefault(v => v.Naziv == userModel.VlogaNaziv);
                        if (vlogaUporabnika != null)
                        {
                            dbUser.VlogaUporabnika = vlogaUporabnika;
                        }

                        // uredi uporabnikove stranke
                        foreach (var custom in userModel.User.CustomerTables)
                        {
                            if (custom.CustomerID == 0)
                            {
                                dbUser.CustomerTables.Add(custom);
                                custom.UsersTable = dbUser;
                                custom.Customer = custom.CustomType.Id_NewCustomerType;
                                custom.UserId = dbUser.userID;
                                dbContext.Entry(custom).State = EntityState.Added;
                            }
                        }

                        foreach (var custom in dbUser.CustomerTables.ToList())
                        {
                            var modelCustom =
                                userModel.User.CustomerTables.FirstOrDefault(o => o.CustomerID == custom.CustomerID);
                            if (modelCustom != null && custom.CustomerID > 0)//update it
                            {
                                custom.CustomType =
                                    dbContext.CustomTypes.Find(modelCustom.CustomType.Id_NewCustomerType);
                            }


                            if (userModel.User.CustomerTables.All(o => o.CustomerID != custom.CustomerID))
                                dbUser.CustomerTables.Remove(custom);
                        }

                        dbContext.SaveChanges();
                        break;
                }
                return View("Edit", userModel);
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        public ActionResult Delete(int id = 0)
        {
            using (var dbVn = new userDbEntities())
            {
                var delete = dbVn.UsersTables.Find(id);

                if (delete == null)
                {
                    return HttpNotFound();
                }
                return View(delete);
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            using (var dbContext = new userDbEntities())
            {
                var delete = dbContext.UsersTables.Find(id);

                var deleteCust = dbContext.CustomerTables.Where(m => m.UserId == id);

                dbContext.UsersTables.Remove(delete);

                foreach (var deleteCustomers in deleteCust)
                {
                    dbContext.CustomerTables.Remove(deleteCustomers);
                }

                dbContext.SaveChanges();
                return RedirectToAction("Registracija");

            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        public ActionResult DeleteCustomerType(int id = 0)
        {
            using (var dbCu = new userDbEntities())
            {
                var deleteCust = dbCu.CustomTypes.Find(id);
                if (deleteCust == null)
                {
                    return HttpNotFound();
                }
                return View(deleteCust);
            }
        }

        //NI CUSTOMER AMPAK DESTIONATION - UPORABI ZA SOLR!!!
        [HttpPost, ActionName("DeleteCustomerType")]
        public ActionResult DeleteConfirmedCust(int id)
        {
            using (var dbCu = new userDbEntities())
            {
                var deleteC = dbCu.CustomTypes.Find(id);
                dbCu.CustomTypes.Remove(deleteC);
                dbCu.SaveChanges();
                return RedirectToAction("CustomerTypeView");
            }
        }

        protected override void Dispose(bool disposing)
        {
            _entities.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult KraticeView(string sortOrder, string searchString, string currentFilter, int? pageKrat)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.Actice = true;
            ViewBag.KratView = String.IsNullOrEmpty(sortOrder) ? "krat_desc" : "";

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
                    return View(kraticeMsg.ToPagedList(pageNumber, pageSize));

                }

            }
            catch (Exception)
            {
                throw;
            }
        }

        public ActionResult CreateNewMessageType(Models.KraticeTable messageType)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var dataM = new userDbEntities())
                    {
                        var sysMessageType = dataM.KraticeTables.Create();

                        if (sysMessageType != null)
                        {
                            sysMessageType.Kratica = messageType.Kratica;
                            sysMessageType.OpisSlo = messageType.OpisSlo;
                            sysMessageType.OpisAng = messageType.OpisAng;

                            var count = dataM.KraticeTables.Count(c => c.Kratica == messageType.Kratica);
                            if (count == 0)
                            {
                                dataM.KraticeTables.Add(sysMessageType);
                                dataM.SaveChanges();
                                return RedirectToAction("KraticeView", "LoginAdmin");
                            }
                            else
                            {
                                ModelState.AddModelError("Message Type", "This Message already exists!");
                                //return View(customerType);
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("Message", "You forgot to enter Message type!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    string error = ex.Message;
                }
            }
            return View(messageType);
        }

        public ActionResult DeleteMessageType(int id = 0)
        {
            using (var dbMe = new userDbEntities())
            {
                var deleteMess = dbMe.KraticeTables.Find(id);
                if (deleteMess == null)
                {
                    return HttpNotFound();
                }
                return View(deleteMess);
            }
        }

        [HttpPost, ActionName("DeleteMessageType")]
        public ActionResult DeleteConfirmedMess(int id)
        {
            using (var dbMe = new userDbEntities())
            {
                var deleteM = dbMe.KraticeTables.Find(id);
                dbMe.KraticeTables.Remove(deleteM);
                dbMe.SaveChanges();
                return RedirectToAction("KraticeView");
            }
        }

        public ActionResult EditMessage(int id = 0)
        {
            using (var dbMs = new userDbEntities())
            {
                var editMsg = dbMs.KraticeTables.Find(id);
                if (editMsg == null)
                {
                    return HttpNotFound();
                }
                return View(editMsg);
            }
        }

        [HttpPost, ActionName("EditMessage")]
        public ActionResult EditMsg(KraticeTable vnEdit)
        {
            try
            {
                using (var dbEm = new userDbEntities())
                {
                    dbEm.Entry(vnEdit).State = EntityState.Modified;
                    dbEm.SaveChanges();
                    return RedirectToAction("KraticeView");
                }
            }
            catch (Exception ex)
            {
                string error = ex.Message;

            }
            return View(vnEdit);
        }
    }
}
