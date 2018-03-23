using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VAGtoSCD
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                //setup
                if (!Directory.Exists("wav"))
                    Directory.CreateDirectory("wav");
                if (!Directory.Exists("at9"))
                    Directory.CreateDirectory("at9");
                if (!Directory.Exists("output"))
                    Directory.CreateDirectory("output");

                //Check for vgmstream and at9tool first
                if (!File.Exists("tools\\vgmstream.exe"))
                {
                    Console.WriteLine("Please put vgmstream.exe in the tools folder!");
                    return;
                }

                if (!File.Exists("tools\\at9tool.exe"))
                {
                    Console.WriteLine("Please put at9tool.exe in the tools folder!");
                    return;
                }

                FileAttributes attr = File.GetAttributes(args[0]);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    Console.WriteLine("Folder mode");
                    foreach (var file in Directory.GetFiles(args[0], "*.vag"))
                    {
                        ConvertFile(file);
                    }
                }
                else
                    ConvertFile(args[0]);

                Directory.Delete("wav");
                Directory.Delete("at9");
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("VAGtoSCD <file/dir>");
            }
        }

        static void ConvertFile(string file)
        {
            if (Path.GetExtension(file) != "vag")
            {
                Console.WriteLine("No valid vag file!");
            }
            else
            {
                Console.WriteLine($"Current file: {file}");
                var pureFileName = file.Substring(file.LastIndexOf("\\") + 1);
                pureFileName = pureFileName.Replace(".vag", string.Empty);

                var wavPath = Path.Combine(Environment.CurrentDirectory, $"wav\\{pureFileName}.wav");
                var at9Path = Path.Combine(Environment.CurrentDirectory, $"at9\\{pureFileName}.at9");
                var scdPath = Path.Combine(Environment.CurrentDirectory, $"output\\{pureFileName}.ps4.scd");

                //convert vag to wav
                Process p = new Process();
                p.StartInfo.FileName = "tools\\vgmstream.exe";
                p.StartInfo.Arguments = $"-o \"{wavPath}\" \"{file}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = false;
                p.Start();
                p.WaitForExit();

                //convert wav to at9
                p.StartInfo.FileName = "tools\\at9tool.exe";
                p.StartInfo.Arguments = $"-e -br 96 \"{wavPath}\" \"{at9Path}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = false;
                p.Start();
                p.WaitForExit();

                //read data part from at9
                byte[] at9Data = File.ReadAllBytes(at9Path);
                at9Data = at9Data.Skip(0x64).ToArray();

                //read dummy scd and write informations

                byte[] dummyScd = File.ReadAllBytes("dummy.scd");
                using (var writer = new BinaryWriter(new MemoryStream(dummyScd)))
                {
                    //1.5+2.5 header only contains file size (minus itself [16 byte])
                    var finalFileLength = dummyScd.Length + at9Data.Length - 16;

                    //file length
                    writer.Write(finalFileLength);
                    writer.BaseStream.Position = 0x20;
                    writer.Write(finalFileLength);

                    //filename
                    writer.BaseStream.Position = 0x160;
                    writer.Write(Encoding.UTF8.GetBytes(pureFileName));

                    //at9 length
                    writer.BaseStream.Position = 0x260;
                    writer.Write(at9Data.Length);

                    //length of audio (size of wav / 2)
                    //this would be the streamsize, but this works, so meh
                    writer.BaseStream.Position = 0x290;
                    writer.Write(new FileInfo(wavPath).Length / 2);
                }

                //merge arrays
                var finalScd = new byte[dummyScd.Length + at9Data.Length];
                Array.Copy(dummyScd, finalScd, dummyScd.Length);
                Array.Copy(at9Data, 0, finalScd, dummyScd.Length, at9Data.Length);

                File.WriteAllBytes(scdPath, finalScd);

                //clean up
                File.Delete(wavPath);
                File.Delete(at9Path);
            }
        }
    }
}
