using System;
using System.IO;
using NUnit.Framework;

namespace _Tests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Convert()
        {
            var assy = new string[]
            {
                "{057ff31d-43e4-47cd-a174-100129c69e9a}.zll",
                "{6ed49711-7ada-4097-b64f-deb2aa104670}.zll",
                "{dce4ca12-f077-4077-afc4-97118e97cbae}.zll",
                "{e6ee3088-2be6-4207-9f3a-6ad434cf83df}.zll",
            };

            foreach(var file in assy)
            {
                var fileSource = new FileInfo(
                    Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    file));
                var fileDest = new FileInfo(fileSource.FullName + ".dll");
                if(fileSource.Exists)
                {
                    using(var stream = new FileStream(fileSource.FullName, FileMode.Open))
                    {
                        var bufferOriginal = new byte[stream.Length];
                        stream.Read(bufferOriginal, 0, bufferOriginal.Length);
                        using(var memoryStream = new FileStream(fileDest.FullName, FileMode.Create))
                        {
                            var bufferDecoded = SimpleZip.Unzip(bufferOriginal);
                            memoryStream.Write(bufferDecoded, 0, bufferDecoded.Length);
                        }
                    }
                }
            }
        }
    }
}
