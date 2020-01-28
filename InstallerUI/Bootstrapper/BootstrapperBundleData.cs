using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;

/*
 * https://stackoverflow.com/questions/25855003/how-to-install-specific-msis-in-wix-burn
 * 
 * https://www.stacknoob.com/s/3vacynxVq87FRrmRzf8aLK
 * To find out which MSIs are missing from the client you can parse the "BootstrapperApplicationData.xml" file.
 * It should be located in the temp directory (%temp%{guid of the installer}\\ba1\\ you can press win+r and write %temp% to get to the tempfolder).


<?xml version="1.0" encoding="utf-16"?>
<BootstrapperApplicationData xmlns="http://schemas.microsoft.com/wix/2010/BootstrapperApplicationData">
  <WixBundleProperties DisplayName="Assist Bundle Installer Example" LogPathVariable="WixBundleLog" Compressed="no" Id="{0d638b2d-ad0b-4bf3-94c8-b0f140fb981c}" UpgradeCode="{C4858E43-3E12-4E25-9EC9-D7B393705A16}" PerMachine="yes" />
  <WixMbaPrereqInformation PackageId="" LicenseUrl="" />
  <WixPackageProperties Package="NetCore2_X64" Vital="yes" DisplayName="Microsoft .NET Core Runtime - 2.1.0 Preview 1 (x64)" Description="Microsoft .NET Core Runtime - 2.1.0 Preview 1 (x64)" DownloadSize="24036072" PackageSize="24036072" InstalledSize="24036072" PackageType="Exe" Permanent="yes" LogPathVariable="WixBundleLog_NetCore2_X64" RollbackLogPathVariable="WixBundleRollbackLog_NetCore2_X64" Compressed="yes" DisplayInternalUI="no" Version="2.1.0.26216" InstallCondition="VersionNT64" Cache="yes" />
  <WixPackageProperties Package="NetCore2_X86" Vital="yes" DisplayName="Microsoft .NET Core Runtime - 2.1.0 Preview 1 (x86)" Description="Microsoft .NET Core Runtime - 2.1.0 Preview 1 (x86)" DownloadSize="21744160" PackageSize="21744160" InstalledSize="21744160" PackageType="Exe" Permanent="yes" LogPathVariable="WixBundleLog_NetCore2_X86" RollbackLogPathVariable="WixBundleRollbackLog_NetCore2_X86" Compressed="yes" DisplayInternalUI="no" Version="2.1.0.26216" InstallCondition="NOT VersionNT64" Cache="yes" />
  <WixPackageProperties Package="SetupProject1" Vital="yes" DisplayName="Setup Project 1" DownloadSize="286819" PackageSize="286819" InstalledSize="8" PackageType="Msi" Permanent="no" LogPathVariable="WixBundleLog_SetupProject1" RollbackLogPathVariable="WixBundleRollbackLog_SetupProject1" Compressed="yes" DisplayInternalUI="yes" ProductCode="{5F680ED7-C5C3-4CDB-B30E-1670FBB16EDE}" UpgradeCode="{6784DFA7-7B23-4CDC-B0D9-17159E38FD67}" Version="1.0.0.0" Cache="yes" />
  <WixPackageProperties Package="SetupProject2" Vital="yes" DisplayName="Setup Project 2" DownloadSize="32867" PackageSize="32867" InstalledSize="8" PackageType="Msi" Permanent="no" LogPathVariable="WixBundleLog_SetupProject2" RollbackLogPathVariable="WixBundleRollbackLog_SetupProject2" Compressed="yes" DisplayInternalUI="no" ProductCode="{2E52859B-45D8-4246-8EBD-B677A9755812}" UpgradeCode="{4304B817-5912-4951-9A39-2B764D59A8DC}" Version="1.0.0.0" Cache="yes" />
  <WixPackageProperties Package="MySQL" Vital="yes" DisplayName="MySQL Community Installer" DownloadSize="506351616" PackageSize="506351616" InstalledSize="523134078" PackageType="Msi" Permanent="no" LogPathVariable="WixBundleLog_MySQL" RollbackLogPathVariable="WixBundleRollbackLog_MySQL" Compressed="yes" DisplayInternalUI="no" ProductCode="{69206AB7-253C-4B3F-93B9-F3F1629EDD8F}" UpgradeCode="{18B94B70-06F1-4AC0-B308-37280DB868C2}" Version="1.4.32.0" Cache="no" />
  <WixPayloadProperties Payload="NetCore2_X64" Package="NetCore2_X64" Container="WixAttachedContainer" Name="dotnet-runtime-2.1.0-preview1-26216-03-win-x64.exe" Size="24036072" LayoutOnly="no" />
  <WixPayloadProperties Payload="NetCore2_X86" Package="NetCore2_X86" Container="WixAttachedContainer" Name="dotnet-runtime-2.1.0-preview1-26216-03-win-x86.exe" Size="21744160" LayoutOnly="no" />
  <WixPayloadProperties Payload="SetupProject1" Package="SetupProject1" Container="WixAttachedContainer" Name="SetupProject1.msi" Size="286720" LayoutOnly="no" />
  <WixPayloadProperties Payload="cabCED2DE07683FAB197DE46FD49477F32D" Package="SetupProject1" Container="WixAttachedContainer" Name="cab1.cab" Size="99" LayoutOnly="no" />
  <WixPayloadProperties Payload="SetupProject2" Package="SetupProject2" Container="WixAttachedContainer" Name="SetupProject2.msi" Size="32768" LayoutOnly="no" />
  <WixPayloadProperties Payload="cab7C6C461597751EC8288DA4F30F4EF760" Package="SetupProject2" Container="WixAttachedContainer" Name="cab1.cab" Size="99" LayoutOnly="no" />
  <WixPayloadProperties Payload="MySQL" Package="MySQL" Container="WixAttachedContainer" Name="mysql-installer-community-5.7.29.0.msi" Size="506351616" LayoutOnly="no" />
</BootstrapperApplicationData> 
*/


namespace InstallerUI.Bootstrapper
{
    /// <summary>
    /// Class that parses the file "BootstrapperApplicationData.xml" which contains <WixPackageProperties /> nodes
    /// </summary>
    public class BootstrapperBundleData
    {
        public const string defaultFileName = "BootstrapperApplicationData.xml";
        public const string xmlNamespace = "http://schemas.microsoft.com/wix/2010/BootstrapperApplicationData";

        public FileInfo DataFile { get; protected set; }
        public Bundle Data { get; protected set; }

        private static string defaultFolder;
        public static string DefaultFolder
        {
            get
            {
                if (defaultFolder == null)
                {
                    defaultFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                }

                return defaultFolder;
            }
        }

        private static string defaultFile;
        public static string DefaultFile
        {
            get
            {
                if (defaultFile == null)
                {
                    defaultFile = Path.Combine(DefaultFolder, defaultFileName);
                }

                return defaultFile;
            }
        }

        

        public BootstrapperBundleData() : this(DefaultFile) { }

        public BootstrapperBundleData(string bootstrapperBundleDataFile)
        {
            using (FileStream fs = File.OpenRead(bootstrapperBundleDataFile))
            {
                Data = ParseBundleFromStream(fs);
            }
        }

        public static Bundle ParseBundleFromStream(Stream stream)
        {
            XPathDocument manifest = new XPathDocument(stream);
            XPathNavigator root = manifest.CreateNavigator();
            return ParseBundleFromXml(root);
        }

        public static Bundle ParseBundleFromXml(XPathNavigator root)
        {
            Bundle bundle = new Bundle();

            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(root.NameTable);
            namespaceManager.AddNamespace("p", xmlNamespace);
            XPathNavigator bundleNode = root.SelectSingleNode("/p:BootstrapperApplicationData/p:WixBundleProperties", namespaceManager);

            if (bundleNode == null)
            {
                throw new Exception("Failed to select bundle information");
            }


            bool? perMachine = GetBoolAttribute(bundleNode, "PerMachine");
            if (perMachine.HasValue)
            {
                bundle.PerMachine = perMachine.Value;
            }

            string name = GetStringAttribute(bundleNode, "DisplayName");
            if (name != null)
            {
                bundle.Name = name;
            }

            Package[] packages = ParsePackagesFromXml(root);
            bundle.Packages = packages;
            

            return bundle;
        }

        public static Package[] ParsePackagesFromXml(XPathNavigator root)
        {
            List<Package> packages = new List<Package>();

            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(root.NameTable);
            namespaceManager.AddNamespace("p", xmlNamespace);
            XPathNodeIterator nodes = root.Select("/p:BootstrapperApplicationData/p:WixPackageProperties", namespaceManager);

            foreach (XPathNavigator node in nodes)
            {
                Package package = new Package();

                string id = GetStringAttribute(node, "Package");
                package.Id = id ?? throw new Exception("Failed to get package identifier for package");

                string displayName = GetStringAttribute(node, "DisplayName");
                if (displayName != null)
                {
                    package.DisplayName = displayName;
                }

                string description = GetStringAttribute(node, "Description");
                if (description != null)
                {
                    package.Description = description;
                }

                bool? displayInternalUI = GetBoolAttribute(node, "DisplayInternalUI");
                if (!displayInternalUI.HasValue)
                {
                    throw new Exception("Failed to get DisplayInternalUI setting for package");
                }
                package.DisplayInternalUI = displayInternalUI.Value;
                packages.Add(package);
            }

            return packages.ToArray();
        }

        public static string GetStringAttribute(XPathNavigator node, string attributeName)
        {
            XPathNavigator attribute = node.SelectSingleNode("@" + attributeName);

            if (attribute == null)
            {
                return null;
            }

            return attribute.Value;
        }

        public static bool? GetBoolAttribute(XPathNavigator node, string attributeName)
        {
            string attributeValue = GetStringAttribute(node, attributeName);

            if (attributeValue == null)
            {
                return null;
            }

            return attributeValue.Equals("yes", StringComparison.InvariantCulture);
        }
    }
}
