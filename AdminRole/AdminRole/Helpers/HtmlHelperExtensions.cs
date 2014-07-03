using System;
using System.Web.Mvc;

namespace AdminRole.Helpers {
    public static class HtmlHelperExtensions {
        private static readonly Random rnd = new Random();

        public static int RandomNumber(this HtmlHelper helper) {
            return rnd.Next();
        }
    }
}