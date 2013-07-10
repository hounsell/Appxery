using Appxery.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Appxery.Controllers
{
    public class appxController : Controller
    {
        //
        // GET: /appx/

        public ActionResult Index()
        {
            return View(AppXServer.AppXList.OrderBy(a => a.Name));
        }

        //
        // GET: /appx/details/5

        public ActionResult details(Guid id)
        {
            return View(AppXServer.AppXList.Single(s => s.AppxId == id));
        }

        //
        // GET: /appx/download/5

        public ActionResult download(Guid id)
        {
            AppX app = AppXServer.AppXList.Single(s => s.AppxId == id);
            Response.ContentType = "application/vnd.ms-appx";
            Response.AddHeader("Content-Disposition", string.Format("attachment; filename=\"{0}.appx\"", app.Name));
            string storePath = Server.MapPath("~/App_Data/AppX-Store");
            Response.WriteFile(Path.Combine(storePath, app.Path));
            Response.Flush();
            return new EmptyResult();
        }

        //
        // GET: /appx/refresh/
        public ActionResult refresh()
        {
            AppXServer.ImportAppxs();
            return RedirectToAction("Index");
        }

        ////
        //// GET: /appx/Create

        //public ActionResult Create()
        //{
        //    return View();
        //}

        ////
        //// POST: /appx/Create

        //[HttpPost]
        //public ActionResult Create(FormCollection collection)
        //{
        //    try
        //    {
        //        // TODO: Add insert logic here

        //        return RedirectToAction("Index");
        //    }
        //    catch
        //    {
        //        return View();
        //    }
        //}

        ////
        //// GET: /appx/Edit/5

        //public ActionResult Edit(int id)
        //{
        //    return View();
        //}

        ////
        //// POST: /appx/Edit/5

        //[HttpPost]
        //public ActionResult Edit(int id, FormCollection collection)
        //{
        //    try
        //    {
        //        // TODO: Add update logic here

        //        return RedirectToAction("Index");
        //    }
        //    catch
        //    {
        //        return View();
        //    }
        //}

        ////
        //// GET: /appx/Delete/5

        //public ActionResult Delete(int id)
        //{
        //    return View();
        //}

        ////
        //// POST: /appx/Delete/5

        //[HttpPost]
        //public ActionResult Delete(int id, FormCollection collection)
        //{
        //    try
        //    {
        //        // TODO: Add delete logic here

        //        return RedirectToAction("Index");
        //    }
        //    catch
        //    {
        //        return View();
        //    }
        //}
    }
}
