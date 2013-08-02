using Appxery.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Appxery.Controllers
{
    public class homeController : Controller
    {
        //
        // GET: /home/

        public ActionResult Index()
        {
            ViewBag.AppX = AppXServer.AppXList;
            ViewBag.Bundle = AppXBundleServer.AppXBundleList;
            return View(ViewBag);
        }

        public ActionResult refresh()
        {
            AppXServer.ImportAppxs();
            AppXBundleServer.ImportAppxBundles();
            return RedirectToAction("Index");
        }
    }
}
