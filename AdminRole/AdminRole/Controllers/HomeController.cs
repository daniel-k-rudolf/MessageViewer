using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using AdminRole.Helpers;
using AdminRole.Models;

namespace AdminRole.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        //[HttpGet]
        public ActionResult Index()
        {
            string userName = System.Web.HttpContext.Current.User.Identity.Name;
            using (var dbUs = new userDbEntities())
            {
                var currentUser = dbUs.UsersTables.FirstOrDefault(u => u.username == userName);
                if (currentUser != null)
                {
                    //return view for admin
                    if (currentUser.VlogaUporabnika.Id_Vloga == VlogaEnum.Administrator)
                    {
                        //return view for admin
                        return RedirectToAction("AdminIndex", "LoginAdmin");
                    }
                    else if (currentUser.VlogaUporabnika.Id_Vloga == VlogaEnum.Spediter)
                    {
                        //return view for spediter
                        return RedirectToAction("Index", "LoginUser");
                    }
                    else if (currentUser.VlogaUporabnika.Id_Vloga == VlogaEnum.SpediterOsnovni)
                    {
                        //return view for Osnovnispediter
                        return RedirectToAction("Index", "LoginSimpleUser");
                    }
                    //..
                    else
                    {
                        //vloga does not exists
                        return View("Index");
                    }
                }
                else
                {
                    return View("Index");
                }
            }
        }

        [HttpPost]
        public ActionResult Login(Models.Users uporabnik)
        {
            if (ModelState.IsValid)
            {
                if (IsValid(uporabnik.Username, uporabnik.Password))
                {
                    using (var dbs = new userDbEntities())
                    {
                        var currentUser = dbs.UsersTables.FirstOrDefault(u => u.username == uporabnik.Username);

                        if (currentUser != null)
                        {
                            if (currentUser.VlogaUporabnika.Id_Vloga == VlogaEnum.Administrator)
                            {
                                FormsAuthentication.SetAuthCookie(uporabnik.Username, false);
                                return RedirectToAction("AdminIndex", "LoginAdmin");
                            }
                            else if (currentUser.VlogaUporabnika.Id_Vloga == VlogaEnum.Spediter)
                            {
                                FormsAuthentication.SetAuthCookie(uporabnik.Username, false);
                                return RedirectToAction("Index", "LoginUser");
                            }
                            else if (currentUser.VlogaUporabnika.Id_Vloga == VlogaEnum.SpediterOsnovni)
                            {
                                FormsAuthentication.SetAuthCookie(uporabnik.Username, false);
                                return RedirectToAction("Index", "LoginSimpleUser");
                            }
                            else
                            {
                                ModelState.AddModelError("", "");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("", "");
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("", "");
                    //return View("Index", uporabnik);
                }
            }
            return View("Index", uporabnik);
        }

        public ActionResult LogOut()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        private bool IsValid(string userSol, string passSol)
        {
            var crypto = new SimpleCrypto.PBKDF2();
            bool isValid = false;
            using (var dbs = new userDbEntities())
            {
                var user = dbs.UsersTables.FirstOrDefault(u => u.username == userSol);
                if (user != null)
                {
                    if (user.password == crypto.Compute(passSol, user.passwordSalt))
                    {
                        isValid = true;
                    }
                }
            }
            return isValid;
        }
    }
}
