﻿using Elmah;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml.Linq;

namespace Appxery.Models
{
    [Serializable]
    public class AppX
    {
        public Guid AppxId { get; set; }

        public long Size { get; set; }
        public string Path { get; set; }
        public string Sha256Hash { get; set; }

        public string Name { get; set; }
        public Version Version { get; set; }
        public string Publisher { get; set; }
        public Architecture ProcessorArchitecture { get; set; }

        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string PublisherDisplayName { get; set; }

        public Version OSMinVersion { get; set; }
        public Version OSMaxVersionTested { get; set; }
    }

    public enum Architecture
    {
        x86,
        x64,
        arm,
        neutral
    }


    public static class AppXServer
    {
        static AppXServer()
        {
            ImportAppxs();
        }

        public static void ImportAppxs()
        {
            AppXList = new List<AppX>();

            string importPath = HttpContext.Current.Server.MapPath("~/App_Data/AppX-Import");
            string storePath = HttpContext.Current.Server.MapPath("~/App_Data/AppX-Store");

            DirectoryInfo importDir = new DirectoryInfo(importPath);

            FileInfo[] appxs = importDir.GetFiles("*.appx");

            foreach (FileInfo appx in appxs)
            {
                ZipArchive za = ZipFile.OpenRead(appx.FullName);
                var appxManifest = za.Entries.First(f => f.Name == "AppxManifest.xml");
                string xmlPath = Path.Combine(importPath, appx.Name.Substring(0, appx.Name.Length - appx.Extension.Length) + ".xml");

                if (!File.Exists(xmlPath))
                {
                    appxManifest.ExtractToFile(xmlPath);
                }

                za.Dispose();
                FileStream appxManifestStr = new FileStream(xmlPath, FileMode.Open);
                XDocument appxManifestXml = XDocument.Load(appxManifestStr);

                SHA256 hash = SHA256.Create();
                FileStream appxStr = appx.OpenRead();
                byte[] hashBytes = hash.ComputeHash(appxStr);
                appxStr.Close();
                appxStr.Dispose();

                StringBuilder hashString = new StringBuilder();

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hashString.Append(hashBytes[i].ToString("X2"));
                }

                try
                {
                    AppX item = new AppX()
                    {
                        AppxId = Guid.NewGuid(),

                        Size = appx.Length,
                        Sha256Hash = hashString.ToString(),

                        Name = appxManifestXml.Root.LocalElement("Identity").Attribute("Name").Value,
                        Version = Version.Parse(appxManifestXml.Root.LocalElement("Identity").Attribute("Version").Value),
                        Publisher = appxManifestXml.Root.LocalElement("Identity").Attribute("Publisher").Value,
                        ProcessorArchitecture = (Architecture)Enum.Parse(typeof(Architecture), appxManifestXml.Root.LocalElement("Identity").Attribute("ProcessorArchitecture") == null ? "neutral" : appxManifestXml.Root.LocalElement("Identity").Attribute("ProcessorArchitecture").Value),

                        DisplayName = appxManifestXml.Root.LocalElement("Properties").LocalElement("DisplayName").Value,
                        Description = appxManifestXml.Root.LocalElement("Properties").LocalElement("Description") != null ? appxManifestXml.Root.LocalElement("Properties").LocalElement("Description").Value : "",
                        PublisherDisplayName = appxManifestXml.Root.LocalElement("Properties").LocalElement("PublisherDisplayName").Value,

                        OSMinVersion = Version.Parse(appxManifestXml.Root.LocalElement("Prerequisites").LocalElement("OSMinVersion").Value),
                        OSMaxVersionTested = Version.Parse(appxManifestXml.Root.LocalElement("Prerequisites").LocalElement("OSMaxVersionTested").Value)
                    };
                    appxManifestStr.Close();

                    File.Delete(xmlPath);

                    // Import and Store details
                    DirectoryInfo storeDir = Directory.CreateDirectory(Path.Combine(storePath, item.AppxId.ToString()));
                    File.Move(appx.FullName, Path.Combine(storeDir.FullName, appx.Name));

                    item.Path = Path.Combine(storeDir.FullName, appx.Name).Substring(storePath.Length + 1);
                    FileStream fStr = new FileStream(Path.Combine(storeDir.FullName, "data.bin"), FileMode.CreateNew);

                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fStr, item);

                    fStr.Flush();
                    fStr.Close();
                }
                catch (Exception e)
                {
                    Error elmahErr = new Error(e, HttpContext.Current);
                    elmahErr.ServerVariables.Add("AppX Path", appx.FullName);
                    ErrorLog.GetDefault(HttpContext.Current).Log(elmahErr);

                    appxManifestStr.Close();

                    File.Delete(xmlPath);
                    continue;
                }

            }

            foreach (DirectoryInfo storedAppx in new DirectoryInfo(storePath).GetDirectories())
            {
                FileInfo storeBin = new FileInfo(Path.Combine(storedAppx.FullName, "data.bin"));

                FileStream storeBinStr = storeBin.OpenRead();
                BinaryFormatter bf = new BinaryFormatter();
                AppX item = bf.Deserialize(storeBinStr) as AppX;

                if (item != null)
                {
                    if (AppXList.SingleOrDefault(a => a.Sha256Hash == item.Sha256Hash) != null)
                    {
                        Error elmahErr = new Error(new Exception("Duplicate AppX"));
                        elmahErr.ServerVariables.Add("AppX Path 1", AppXList.SingleOrDefault(a => a.Sha256Hash == item.Sha256Hash).Path);
                        elmahErr.ServerVariables.Add("AppX Path 2", item.Path);

                        ErrorLog.GetDefault(HttpContext.Current).Log(elmahErr);
                    }
                    else
                    {
                        AppXList.Add(item);
                    }
                }

                storeBinStr.Close();
            }
        }

        public static List<AppX> AppXList { get; set; }

        public static XElement LocalElement(this XElement element, XName elementName)
        {
            return element.Elements().SingleOrDefault(s => s.Name.LocalName == elementName);
        }
    }
}