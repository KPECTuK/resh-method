using JetBrainsPatcher.Patcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Xceed.FileSystem;
using Xceed.Zip;

namespace JetBrainsPatcher
{
    class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("www.nummerdev.ru\nBY NUMMER\n\n(c) 2016 www.nummerdev.ru\ntwitter.com/nummerok\n\n==================================\n");
            Xceed.Zip.Licenser.LicenseKey = "ZIN58-WFSR0-ERS0W-8RGA";
            Xceed.FileSystem.Licenser.LicenseKey = "ZIN58-WFSR0-ERS0W-8RGA";
            string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Products.json");
            if(!File.Exists(text))
            {
                Console.WriteLine("ERROR: \"{0}\" not found.", text);
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
                return;
            }
            try
            {
                Product[] settings = JsonConvert.DeserializeObject<Product[]>(File.ReadAllText(text));
                string jbCertificate = GetCertString("jb.cacert.crt");
                string Certificate = GetCertString("cacert.crt");
                byte[] jbCertificateBytes = Encoding.UTF8.GetBytes(jbCertificate);
                byte[] certificateBytes = Encoding.UTF8.GetBytes(Certificate);
                byte[] jbCertificateBytesW = Encoding.UTF8.GetBytes(jbCertificate.Replace("\n", "\r\n"));
                byte[] certificateBytesW = Encoding.UTF8.GetBytes(Certificate.Replace("\n", "\r\n"));
                Product[] settingsCopy = settings;
                for(int i = 0; i < settingsCopy.Length; i++)
                {
                    Product product = settingsCopy[i];
                    string filePath = product.FilePath;
                    if(string.IsNullOrEmpty(filePath))
                    {
                        Console.WriteLine("ERROR: [{0}] File \"{1}\" path corruption.", product.Name, filePath);
                        continue;
                    }
                    if(!File.Exists(filePath))
                    {
                        Console.WriteLine("ERROR: [{0}] File \"{1}\" not found.", product.Name, filePath);
                        continue;
                    }

                    byte[] buffer = File.ReadAllBytes(filePath);
                    MemoryFile memoryFile = new MemoryFile();
                    using(Stream stream = memoryFile.CreateWrite())
                    {
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    bool flag = false;
                    string fileExt = Path.GetExtension(filePath).ToLower();
                    if(fileExt == ".jar")
                    {
                        flag = PatchJarFile(memoryFile, jbCertificateBytes, certificateBytes);
                    }
                    else if(fileExt == ".dll")
                    {
                        flag = PatchDllFile(memoryFile, jbCertificateBytesW, certificateBytesW);
                    }
                    else
                    {
                        // error
                    }

                    if(flag)
                    {
                        File.WriteAllBytes(filePath, memoryFile.ToArray());
                        Console.WriteLine("INFO: [{0}] File \"{1}\" patched", product.Name, filePath);
                    }
                    else
                    {
                        Console.WriteLine("ERROR: [{0}] File \"{1}\" not patched.", product.Name, filePath);
                    }
                }
            }
            catch(Exception arg)
            {
                Console.WriteLine("ERROR: {0}", arg);
            }

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }

        private static string GetCertString(string name)
        {
            string result;
            using(Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program), name))
            {
                if(manifestResourceStream == null)
                {
                    result = string.Empty;
                }
                else
                {
                    using(StreamReader streamReader = new StreamReader(manifestResourceStream))
                    {
                        result = streamReader.ReadToEnd().Replace("\r\n", "\n");
                    }
                }
            }
            return result;
        }

        private static bool PatchDllFile(AbstractFile file, byte[] jbcacertBytes, byte[] cacertBytes)
        {
            bool flag;
            var newBytes = ReplaceBytes(ReadAllBytes(file), jbcacertBytes, cacertBytes, out flag);
            if(flag)
            {
                WriteAllBytes(file, newBytes);
            }
            return flag;
        }

        private static bool PatchJarFile(AbstractFile absfile, byte[] jbcacertBytes, byte[] cacertBytes)
        {
            var buffer = new ZipArchive(absfile).GetFiles(true, new object[] { "*.class" });
            bool result = false;
            for(int i = 0; i < buffer.Length; i++)
            {
                var file = buffer[i];
                bool flag;
                var newBytes = ReplaceBytes(ReadAllBytes(file), jbcacertBytes, cacertBytes, out flag);
                if(flag)
                {
                    WriteAllBytes(file, newBytes);
                    result = true;
                }
            }
            return result;
        }

        private static byte[] ReadAllBytes(AbstractFile f)
        {
            byte[] result;
            using(MemoryStream memoryStream = new MemoryStream())
            {
                using(Stream stream = f.OpenRead())
                {
                    byte[] array = new byte[1024];
                    int count;
                    while((count = stream.Read(array, 0, array.Length)) != 0)
                    {
                        memoryStream.Write(array, 0, count);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }

        private static byte[] ReplaceBytes(byte[] buffer, byte[] oldBytes, byte[] newBytes, out bool replaced)
        {
            if(buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if(oldBytes == null)
            {
                throw new ArgumentNullException("oldBytes");
            }
            if(newBytes == null)
            {
                throw new ArgumentNullException("newBytes");
            }

            replaced = false;
            int num = buffer.Length;
            int num2 = oldBytes.Length;
            if(num2 == 0)
            {
                return buffer;
            }
            List<byte> list = new List<byte>();
            for(int i = 0; i < num; i++)
            {
                if(i + oldBytes.Length > num)
                {
                    list.Add(buffer[i]);
                }
                else
                {
                    bool flag = true;
                    for(int j = 0; j < num2; j++)
                    {
                        if(buffer[i + j] != oldBytes[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if(!flag)
                    {
                        list.Add(buffer[i]);
                    }
                    else
                    {
                        replaced = true;
                        list.AddRange(newBytes);
                        i += num2 - 1;
                    }
                }
            }
            return list.ToArray();
        }

        private static void WriteAllBytes(AbstractFile f, byte[] newBytes)
        {
            using(Stream stream = f.OpenWrite(true))
            {
                stream.Write(newBytes, 0, newBytes.Length);
            }
        }
    }
}
