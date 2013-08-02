using Elmah;
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
    public class AppXBundle
    {
        public Guid AppxBundleId { get; set; }

        public long Size { get; set; }
        public string Path { get; set; }
        public string Sha256Hash { get; set; }

        public string Name { get; set; }
        public Version Version { get; set; }
        public string Publisher { get; set; }

        public List<AppXFromBundle> Packages { get; set; }
    }

    [Serializable]
    public class AppXFromBundle : AppX
    {
        public string ResourceId { get; set; }
        public bool IsResourcePackage { get; set; }
        public Dictionary<string, string> ResourcesProvided { get; set; }
    }


    public static class AppXBundleServer
    {
        static AppXBundleServer()
        {
            ImportAppxBundles();
        }

        public static void ImportAppxBundles()
        {
            AppXBundleList = new List<AppXBundle>();

            string importPath = HttpContext.Current.Server.MapPath("~/App_Data/AppX-Import");
            string storePath = HttpContext.Current.Server.MapPath("~/App_Data/AppXBundle-Store");

            DirectoryInfo importDir = new DirectoryInfo(importPath);

            FileInfo[] bundles = importDir.GetFiles("*.appxbundle");

            foreach (FileInfo bundle in bundles)
            {
                ZipArchive za = ZipFile.OpenRead(bundle.FullName);
                var bundleManifest = za.Entries.First(f => f.FullName == "AppxMetadata/AppxBundleManifest.xml");
                string xmlPath = Path.Combine(importPath, bundle.Name.Substring(0, bundle.Name.Length - bundle.Extension.Length) + ".xml");

                if (!File.Exists(xmlPath))
                {
                    bundleManifest.ExtractToFile(xmlPath);
                }

                za.Dispose();
                FileStream bundleManifestStr = new FileStream(xmlPath, FileMode.Open);
                XDocument bundleManifestXml = XDocument.Load(bundleManifestStr);

                SHA256 hash = SHA256.Create();
                FileStream bundleStr = bundle.OpenRead();
                byte[] hashBytes = hash.ComputeHash(bundleStr);
                bundleStr.Close();
                bundleStr.Dispose();

                StringBuilder hashString = new StringBuilder();

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hashString.Append(hashBytes[i].ToString("X2"));
                }

                try
                {
                    AppXBundle item = new AppXBundle()
                    {
                        AppxBundleId = Guid.NewGuid(),

                        Size = bundle.Length,
                        Sha256Hash = hashString.ToString(),

                        Name = bundleManifestXml.Root.LocalElement("Identity").Attribute("Name").Value,
                        Version = Version.Parse(bundleManifestXml.Root.LocalElement("Identity").Attribute("Version").Value),
                        Publisher = bundleManifestXml.Root.LocalElement("Identity").Attribute("Publisher").Value
                    };

                    bundleManifestStr.Close();

                    File.Delete(xmlPath);

                    // Import and Store details
                    DirectoryInfo storeDir = Directory.CreateDirectory(Path.Combine(storePath, item.AppxBundleId.ToString()));
                    File.Move(bundle.FullName, Path.Combine(storeDir.FullName, bundle.Name));

                    item.Path = Path.Combine(storeDir.FullName, bundle.Name).Substring(storePath.Length + 1);
                    FileStream fStr = new FileStream(Path.Combine(storeDir.FullName, "data.bin"), FileMode.CreateNew);

                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fStr, item);

                    fStr.Flush();
                    fStr.Close();

                    ImportPackagesFromBundle(item, storeDir);
                }
                catch (Exception e)
                {
                    Error elmahErr = new Error(e, HttpContext.Current);
                    elmahErr.ServerVariables.Add("Bundle Path", bundle.FullName);
                    ErrorLog.GetDefault(HttpContext.Current).Log(elmahErr);

                    bundleManifestStr.Close();

                    File.Delete(xmlPath);
                    continue;
                }
            }

            foreach (DirectoryInfo storedAppx in new DirectoryInfo(storePath).GetDirectories())
            {
                FileInfo storeBin = new FileInfo(Path.Combine(storedAppx.FullName, "data.bin"));

                FileStream storeBinStr = storeBin.OpenRead();
                BinaryFormatter bf = new BinaryFormatter();
                AppXBundle item = bf.Deserialize(storeBinStr) as AppXBundle;

                if (item != null)
                {
                    if (AppXBundleList.SingleOrDefault(a => a.Sha256Hash == item.Sha256Hash) != null)
                    {
                        Error elmahErr = new Error(new Exception("Duplicate AppXBundle"));
                        elmahErr.ServerVariables.Add("AppXBundle Path 1", AppXBundleList.SingleOrDefault(a => a.Sha256Hash == item.Sha256Hash).Path);
                        elmahErr.ServerVariables.Add("AppXBundle Path 2", item.Path);

                        ErrorLog.GetDefault(HttpContext.Current).Log(elmahErr);
                    }
                    else
                    {
                        item.Packages = new List<AppXFromBundle>();
                        FileInfo[] packageBins = storedAppx.GetDirectories("Packages").First().GetFiles("*.bin");
                        foreach (FileInfo packageBin in packageBins)
                        {
                            FileStream packageBinStr = packageBin.OpenRead();
                            AppXFromBundle package = bf.Deserialize(packageBinStr) as AppXFromBundle;
                            item.Packages.Add(package);
                            packageBinStr.Close();
                        }
                        AppXBundleList.Add(item);
                    }
                }

                storeBinStr.Close();
            }
        }

        private static void ImportPackagesFromBundle(AppXBundle item, DirectoryInfo storeDir)
        {
            // time to bring out the packages
            ZipArchive za = ZipFile.OpenRead(Path.Combine(storeDir.Parent.FullName, item.Path));
            DirectoryInfo packagesDir = Directory.CreateDirectory(Path.Combine(storeDir.FullName, "Packages"));

            var packages = za.Entries.Where(f => f.Name.EndsWith(".appx"));
            foreach (var package in packages)
            {
                package.ExtractToFile(Path.Combine(packagesDir.FullName, package.Name));
            }

            za.Dispose();

            FileInfo[] packageInfos = packagesDir.GetFiles("*.appx");

            foreach (FileInfo package in packageInfos)
            {
                ZipArchive pza = ZipFile.OpenRead(package.FullName);
                var appxManifest = pza.Entries.First(f => f.Name == "AppxManifest.xml");
                string xmlPath = Path.Combine(packagesDir.FullName, package.Name.Substring(0, package.Name.Length - package.Extension.Length) + ".xml");

                if (!File.Exists(xmlPath))
                {
                    appxManifest.ExtractToFile(xmlPath);
                }

                pza.Dispose();
                FileStream appxManifestStr = new FileStream(xmlPath, FileMode.Open);
                XDocument appxManifestXml = XDocument.Load(appxManifestStr);

                SHA256 hash = SHA256.Create();
                FileStream appxStr = package.OpenRead();
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
                    AppXFromBundle packageItem = new AppXFromBundle()
                    {
                        AppxId = Guid.NewGuid(),

                        Size = package.Length,
                        Sha256Hash = hashString.ToString(),

                        Name = appxManifestXml.Root.LocalElement("Identity").Attribute("Name").Value,
                        Version = Version.Parse(appxManifestXml.Root.LocalElement("Identity").Attribute("Version").Value),
                        Publisher = appxManifestXml.Root.LocalElement("Identity").Attribute("Publisher").Value,
                        ProcessorArchitecture = (Architecture)Enum.Parse(typeof(Architecture), appxManifestXml.Root.LocalElement("Identity").Attribute("ProcessorArchitecture") == null ? "neutral" : appxManifestXml.Root.LocalElement("Identity").Attribute("ProcessorArchitecture").Value),

                        DisplayName = appxManifestXml.Root.LocalElement("Properties").LocalElement("DisplayName").Value,
                        Description = appxManifestXml.Root.LocalElement("Properties").LocalElement("Description") != null ? appxManifestXml.Root.LocalElement("Properties").LocalElement("Description").Value : "",
                        PublisherDisplayName = appxManifestXml.Root.LocalElement("Properties").LocalElement("PublisherDisplayName").Value,

                        OSMinVersion = Version.Parse(appxManifestXml.Root.LocalElement("Prerequisites").LocalElement("OSMinVersion").Value),
                        OSMaxVersionTested = Version.Parse(appxManifestXml.Root.LocalElement("Prerequisites").LocalElement("OSMaxVersionTested").Value),

                        IsResourcePackage = appxManifestXml.Root.LocalElement("Properties").LocalElement("ResourcePackage") != null ? bool.Parse(appxManifestXml.Root.LocalElement("Properties").LocalElement("ResourcePackage").Value) : false,
                        ResourceId = appxManifestXml.Root.LocalElement("Identity").Attribute("ResourceId") != null ? appxManifestXml.Root.LocalElement("Identity").Attribute("ResourceId").Value : "",
                        ResourcesProvided = (from res in appxManifestXml.Root.LocalElement("Identity").Elements()
                                            where res.Name.LocalName == "Resource"
                                            select new
                                            {
                                                resourceType = res.Attributes().First().Name.LocalName,
                                                resourceValue = res.Attributes().First().Value
                                            }).ToDictionary(o => o.resourceType, o => o.resourceValue)
                    };
                    appxManifestStr.Close();

                    File.Delete(xmlPath);

                    // Import and Store details
                    packageItem.Path = Path.Combine(storeDir.FullName, "Packages", package.Name).Substring(storeDir.Parent.FullName.Length + 1);
                    FileStream fStr = new FileStream(Path.Combine(packagesDir.FullName, string.Format("{0}.bin", package.Name.Substring(0, package.Name.Length - package.Extension.Length))), FileMode.CreateNew);

                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(fStr, packageItem);

                    fStr.Flush();
                    fStr.Close();
                }
                catch (Exception e)
                {
                    Error elmahErr = new Error(e, HttpContext.Current);
                    elmahErr.ServerVariables.Add("AppX Path", package.FullName);
                    ErrorLog.GetDefault(HttpContext.Current).Log(elmahErr);

                    appxManifestStr.Close();

                    File.Delete(xmlPath);
                    continue;
                }
            }
        }

        public static List<AppXBundle> AppXBundleList { get; set; }
    }
}