using Appxery.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Appxery.Controllers
{
    public class bundleController : Controller
    {
        //
        // GET: /bundle/

        public ActionResult Index()
        {
            return View(AppXBundleServer.AppXBundleList.OrderBy(a => a.Name));
        }

        //
        // GET: /bundle/details/5

        public ActionResult details(Guid id)
        {
            return View(AppXBundleServer.AppXBundleList.Single(s => s.AppxBundleId == id));
        }

        //
        // GET: /bundle/packageDetails/5?appxId=6

        public ActionResult packageDetails(Guid id, Guid appxId)
        {
            AppXFromBundle package;
            AppXBundle bundle = AppXBundleServer.AppXBundleList.Single(s => s.AppxBundleId == id);
            if (bundle != null)
            {
                package = bundle.Packages.Single(s => s.AppxId == appxId);
                ViewBag.BundleId = id;
                return View(package);
            }
            return RedirectToAction("Index", new { controller = "home" });
        }

        //
        // GET: /bundle/download/5

        public ActionResult download(Guid id)
        {
            AppXBundle app = AppXBundleServer.AppXBundleList.Single(s => s.AppxBundleId == id);
            Response.ContentType = "application/vnd.ms-appx";
            Response.AddHeader("Content-Disposition", string.Format("attachment; filename=\"{0}.appxbundle\"", app.Name));
            string storePath = Server.MapPath("~/App_Data/AppXBundle-Store");
            Response.WriteFile(Path.Combine(storePath, app.Path));
            Response.Flush();
            return new EmptyResult();
        }

        //
        // GET: /bundle/packageDownload/5?appxId=6

        public ActionResult packageDownload(Guid id, Guid appxId)
        {
            AppXFromBundle package;
            AppXBundle bundle = AppXBundleServer.AppXBundleList.Single(s => s.AppxBundleId == id);
            if (bundle != null)
            {
                package = bundle.Packages.Single(s => s.AppxId == appxId);
                Response.ContentType = "application/vnd.ms-appx";
                Response.AddHeader("Content-Disposition", string.Format("attachment; filename=\"{0}.appx\"", package.Name));
                string storePath = Server.MapPath("~/App_Data/AppXBundle-Store");
                Response.WriteFile(Path.Combine(storePath, package.Path));
                Response.Flush();
                return new EmptyResult();
            }
            return RedirectToAction("Index", new { controller = "home" });
        }

        //
        // GET: /bundle/refresh/
        public ActionResult refresh()
        {
            AppXBundleServer.ImportAppxBundles();
            return RedirectToAction("Index");
        }

    }
}
