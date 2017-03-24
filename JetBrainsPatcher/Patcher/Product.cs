using Microsoft.Win32;
using System;
using System.IO;

namespace JetBrainsPatcher.Patcher
{
    public class Product
    {
        private string _fileName;
        private string _filePath;

        public string Name { get; set; }

        public string FilePath
        {
            get { return string.IsNullOrEmpty(_filePath) ? _filePath : Environment.ExpandEnvironmentVariables(_filePath); }
            set { _filePath = value; }
        }

        public string FileName
        {
            get
            {
                return _fileName;
            }
            set
            {
                _fileName = value;
                using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    using (RegistryKey registryKey2 = registryKey.OpenSubKey(string.Format("SOFTWARE\\JetBrains\\{0}", Name)))
                    {
                        if (registryKey2 != null)
                        {
                            var subKeyNames = registryKey2.GetSubKeyNames();
                            if (subKeyNames.Length != 0)
                            {
                                Array.Sort(subKeyNames);
                                Array.Reverse(subKeyNames);
                                using (RegistryKey registryKey3 = registryKey2.OpenSubKey(subKeyNames[0]))
                                {
                                    if (registryKey3 != null)
                                    {
                                        FilePath = Path.Combine((string)registryKey3.GetValue(null), "lib", _fileName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
