using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Linq;
using System.IO;
//using System.Windows.Forms;
using Form = System.Windows.Forms.Form;

using Microsoft.Win32;

namespace Interop
{
    class Interop
    {
        // To support flashing.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        //Flash both the window caption and taskbar button.
        //This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags. 
        public const UInt32 FLASHW_ALL = 3;

        // Flash continuously until the window comes to the foreground. 
        public const UInt32 FLASHW_TIMERNOFG = 12;

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }

        public static bool FlashWindowEx()
        {
            IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;//form.Handle;
            FLASHWINFO fInfo = new FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = hWnd;
            fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
            fInfo.uCount = UInt32.MaxValue;
            fInfo.dwTimeout = 0;

            return FlashWindowEx(ref fInfo);
        }
    }
}

namespace CurseforgeMirrorer
{
    public class Image
    {
        public string Filepath = string.Empty;
        public string URL = string.Empty;
        public bool FileExists { get => File.Exists(Filepath); }
    }

    public class DownloadFile
    {
        public string Filename;
        public int Id;
        public bool Downloaded;
        [JsonIgnore]
        public bool UseURL { get => Url != null && Url.Length > 0; }
        public string Url;
        public bool DownloadedFilePage;
    }

    public class Mod
    {
        public string ModIdentifier = string.Empty;
        public string ModName = string.Empty;
        public Image CoverImage
            = new Image();
        //Purge
        [JsonIgnore]
        public List<DownloadFile> _Files
            = new List<DownloadFile>();
        public List<int> Files
            = new List<int>();
        public DateTime CreationDate = DateTime.UnixEpoch;
        public DateTime UpdateDate = DateTime.UnixEpoch;
        public string Synopsis = string.Empty;
        public string Description = string.Empty;
        public string DescriptionHTML = string.Empty;
        public string Author = string.Empty;
        public HashSet<string> CompleteAuthorList
            = new HashSet<string>();
        public List<int> ParsedPages
            = new List<int>();
        public int PageCount = 0;
        public int DownloadCount = 0;
        public HashSet<string> Images
            = new HashSet<string>();
        public HashSet<string> Categories
            = new HashSet<string>();
        public int ProjectId = 0;
        public bool HasSource { get => SourceURL.Length > 0; }
        public bool HasImage { get => CoverImage.URL.Length > 0; }

        public string SourceURL = string.Empty;
        public bool SourceTar = false;
        public bool FailedToRetrieveSource = false;
    }

    public class Program
    {
        [JsonIgnore]
        public static StreamWriter logFile
            = new StreamWriter(File.Open("files.txt", FileMode.Append, FileAccess.Write, FileShare.Read));

        [JsonIgnore]
        public static StreamWriter failFile
            = new StreamWriter(File.Open("fails.txt", FileMode.Append, FileAccess.Write, FileShare.Read));

        [JsonIgnore]
        public static WebClient ProxyClient
            = new WebClient();

        [JsonIgnore]
        public static WebClient CDNClient
            = new WebClient();

        public static Dictionary<string, Mod> Mods
            = new Dictionary<string, Mod>();

        /// <summary>
        /// Add mods to scrape here
        /// </summary>
        public static Queue<Mod> ModsToScrape
            = new Queue<Mod>();
        public static Queue<string> ModsToCaptureInfoPage
            = new Queue<string>();

        //Purge
        public static Queue<KeyValuePair<Mod, DownloadFile>> _FilesToDownload
            = new Queue<KeyValuePair<Mod, DownloadFile>>();
        public static Queue<KeyValuePair<string, int>> FilesToDownload
            = new Queue<KeyValuePair<string, int>>();

        //Purge
        public static Queue<KeyValuePair<Mod, DownloadFile>> _FilesToCaptureDownloadPage
            = new Queue<KeyValuePair<Mod, DownloadFile>>();
        public static Queue<KeyValuePair<string, int>> FilesToCaptureDownloadPage
            = new Queue<KeyValuePair<string, int>>();

        public static Dictionary<string, HashSet<int>> FilesCouldNotDownload
            = new Dictionary<string, HashSet<int>>();

        public static Dictionary<int, DownloadFile> DownloadRegistry
            = new Dictionary<int, DownloadFile>();

        [JsonIgnore]
        public static List<Func<bool>> MirrorActions
            = new List<Func<bool>>();

        /// <summary>
        /// Add parsed mod identifers from the main page here
        /// </summary>
        public static List<string> ParsedModIdentifiers
            = new List<string>();
        public static HashSet<string> CompletedMods
            = new HashSet<string>();
        public static List<int> ParsedPages
            = new List<int>();
        public static HashSet<string> CompletedModInfo
            = new HashSet<string>();
        public static HashSet<string> FailedToCaptureInfoPage
            = new HashSet<string>();

        [JsonIgnore]
        public static int PageCount = 964; //941 Total pages ?page=

        [JsonIgnore]
        public static Random gRandom
            = new Random();

        public static DateTime lastRequest = DateTime.Now.AddSeconds(-60);

        public static int getRandomWaitTime()
        {
            /*
            float x = (float)gRandom.NextDouble() * 2.0f;
            x -= 1.8f;
            float v = -((x * x * x) + (x * x) - x - 1);
            v /= 2.0f;
            int mx = 75 - 15;
            v *= mx;
            v += 15;
            return (int)v;
            */
            float x = (float)gRandom.NextDouble();
            x = -((x - 0.5f) * (x - 0.5f));
            x *= 4;
            int mx = 0 - 10;
            x *= mx;
            x += 10;
            return (int)x;
        }

        public static HtmlDocument GetHtmlDocument(string url, int tries = 0)
        {
            //Console.Write("Sleeping...");
            //while (DateTime.Now.Subtract(lastRequest).TotalSeconds < 60)
            //    Thread.Sleep(60000 - (int)DateTime.Now.Subtract(lastRequest).TotalMilliseconds);
            int randomWaitTime = getRandomWaitTime();//gRandom.Next(15, 75);
            Console.Write("Sleeping for {0}s...", randomWaitTime);
            while (DateTime.Now.Subtract(lastRequest).TotalSeconds < randomWaitTime)
                Thread.Sleep((randomWaitTime * 1000) - (int)DateTime.Now.Subtract(lastRequest).TotalMilliseconds);
            Console.Write("Making request to {0}...", url);
            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.Load(ProxyClient.OpenRead(url));
            Console.WriteLine("Complete.");
            lastRequest = DateTime.Now;

            if (htmlDocument.DocumentNode.ChildNodes.Count < 1)
            {
                if (tries > 2)
                {
                    Console.WriteLine($"Could not download page: {url} after 3 tries");
                    return htmlDocument;
                }
                else
                {
                    //Console.WriteLine("Would you like to retry this request? It could have been a captcha.");
                    //Console.WriteLine("Press any key to continue, ^C to terminate program");
                    //Console.ReadKey(true);
                    NeedUserAttention(1, "Would you like to retry this request? It could have been a captcha.");
                    Console.WriteLine($"Retrying...");

                    return GetHtmlDocument(url, tries + 1);
                }
            }

            return htmlDocument;
        }
                
        public static void TimerTest()
        {
            Console.Write("Sleeping...");
            while (DateTime.Now.Subtract(lastRequest).TotalSeconds < 60)
            {
                Thread.Sleep(60000 - (int)DateTime.Now.Subtract(lastRequest).TotalMilliseconds);

            }
            Console.Write("Making request to {0}...", "http://iansweb.org");
            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.Load(CDNClient.OpenRead("http://iansweb.org"));
            Console.WriteLine("Complete.");
            lastRequest = DateTime.Now;
        }

        /// <summary>
        /// Obtain mod information such as the description, friendly name, developer names, creation date, last update, and total downloads
        /// Will even clone repositories
        /// </summary>
        /// <returns>false if there are no more mods left to obtain info from</returns>
        public static bool Mirror_LoadModInfoPage()
        {
            if (ModsToCaptureInfoPage.Count < 1)
                return false;

            var modId = ModsToCaptureInfoPage.Peek();
            
            if (!Mods.ContainsKey(modId))
            {
                Console.WriteLine("Mirrorer wished to load mod info, but has yet to create an instance of the mod. Aborting for now.");
                return false;
            }

            var mod = Mods[modId];

            string url = $"http://www.curseforge.com/minecraft/mc-mods/{modId}";

            HtmlDocument doc;
            try
            {
                doc = GetHtmlDocument(url);
            }
            catch
            {
                Console.WriteLine("Failed to retrieve document");
                return false;
            }

            try
            {
                //Mod description
                mod.Description = (doc.DocumentNode.SelectSingleNode("//div[@class='box p-4 pb-2 project-detail__content']") ?? HtmlNode.CreateNode("")).InnerText.HtmlDecode();
                mod.DescriptionHTML = (doc.DocumentNode.SelectSingleNode("//div[@class='box p-4 pb-2 project-detail__content']") ?? HtmlNode.CreateNode("")).InnerHtml;
                //Members
                foreach (var authorNode in doc.DocumentNode.SelectNodes("//div[@class='flex mb-2']/div[@class='flex flex-col flex-grow']/p[@class='text-sm text-primary-500 flex']/a/span").Select(n => n.InnerText))
                {
                    string author = authorNode.Trim('\r', '\n', ' ', '\0', '\t');
                    if (!mod.CompleteAuthorList.Contains(author))
                        mod.CompleteAuthorList.Add(author);
                }
                //Source
                var srcNodes = (doc.DocumentNode.SelectNodes("//li[@class=' b-list-item p-nav-item px-2 pb-1/10 -mb-1/10 text-gray-500']/a[@class='text-gray-500 hover:no-underline']"));
                if (srcNodes != null)
                    if (srcNodes.Any(n => n.InnerText.Contains("Source")))
                        if (srcNodes.First(n => n.InnerText.Contains("Source")).Attributes.Contains("href"))
                            mod.SourceURL = srcNodes.First(n => n.InnerText.Contains("Source")).Attributes["href"].Value;

                //Download Count
                mod.DownloadCount = Convert.ToInt32(doc.DocumentNode.SelectSingleNode("//div[@class='flex flex-col mt-auto mb-auto']/div[@class='flex']/span[@class='mr-2 text-sm text-gray-500']").InnerText.Split(' ')[0].Replace(",", ""));

                //Project ID
                mod.ProjectId = Convert.ToInt32(doc.DocumentNode.SelectSingleNode("//div[@class='pb-4 border-b border-gray--100']/div[@class='flex flex-col mb-3']/div[@class='w-full flex justify-between']/span[2]").InnerText);

                var node = (doc.DocumentNode.SelectSingleNode("//div[@class='project-avatar project-avatar-64']/a[@class='bg-white']/img[@class='mx-auto']"));
                if (node != null && node.HasAttributes && node.Attributes.Contains("src"))
                    mod.CoverImage.URL = node.Attributes["src"].Value;

                //Print out results
                Console.WriteLine("{0}: {1}", mod.ModIdentifier, mod.ModName);
                Console.WriteLine("Description: {0}", mod.Description);
                Console.WriteLine("Description HTML: {0}", mod.DescriptionHTML);
                Console.WriteLine("Authors: {0}", mod.CompleteAuthorList.Aggregate((a, b) => $"{a}, {b}"));
                Console.WriteLine("Source: {0}", mod.SourceURL);
                Console.WriteLine("Icon: {0}", mod.CoverImage.URL);
                Console.WriteLine("Downloads: {0}", mod.DownloadCount);
                Console.WriteLine("ProjectID: {0}", mod.ProjectId);

                if (!CompletedModInfo.Contains(modId))
                    CompletedModInfo.Add(modId);
                
                if (mod.HasSource)
                {
                    Console.WriteLine("Source preset for {0} ({1})", mod.ModIdentifier, mod.SourceURL);

                    using (Process gitProcess = new Process())
                    {
                        if (!Directory.Exists(mod.ModIdentifier))
                            Directory.CreateDirectory(mod.ModIdentifier);
                        gitProcess.StartInfo.UseShellExecute = false;
                        gitProcess.StartInfo.FileName = "git.exe";
                        gitProcess.StartInfo.WorkingDirectory = mod.ModIdentifier;
                        gitProcess.StartInfo.Arguments = $"clone {mod.SourceURL} --recursive";                        
                        gitProcess.Start();
                        Console.WriteLine("Times out in 120 seconds");
                        if (!gitProcess.WaitForExit(120000))
                            if (NeedUserAttention("Program failed to exit after wait period. Wait anyway?"))
                                gitProcess.WaitForExit();
                        if (!gitProcess.HasExited || gitProcess.ExitCode != 0)
                        {
                            Console.WriteLine("Failed to clone {0} ({1}) ({2})", mod.ModIdentifier, mod.SourceURL, gitProcess.ExitCode);
                            mod.FailedToRetrieveSource = true;
                        }
                        else
                        {
                            Console.WriteLine("Cloned {0} ({1}) successfully", mod.ModIdentifier, mod.SourceURL);
                        }
                    }

                    //Add to tar
                    if (!mod.FailedToRetrieveSource)
                    using (Process _7zProcess = new Process()) {
                            var path = Directory.GetDirectories(mod.ModIdentifier).First() ?? throw new Exception();
                            var folderName = Path.GetFileName(path);
                            _7zProcess.StartInfo = new ProcessStartInfo()
                            {
                                UseShellExecute = false,
                                FileName = "7z.exe",
                                WorkingDirectory = mod.ModIdentifier,
                                Arguments = $"a {folderName}-source.tar {folderName} -ttar -y -sdel"
                            };
                            _7zProcess.Start();
                            Console.WriteLine("Times out in 480 seconds");
                            if (!_7zProcess.WaitForExit(480000))
                                if (NeedUserAttention("Program failed to exit after wait period. Wait anyway?"))
                                    _7zProcess.WaitForExit();
                            if (!_7zProcess.HasExited || _7zProcess.ExitCode != 0)
                            {
                                Console.WriteLine("Failed to archive repository {0} marking as failed", mod.ModIdentifier);
                                mod.FailedToRetrieveSource = true;
                            }
                            else
                            {
                                Console.WriteLine("Archived {0} successfully", mod.ModIdentifier);
                                mod.FailedToRetrieveSource = false;
                            }
                    }
                }

                //Get and create the icon
                if (mod.HasImage)
                {
                    Console.WriteLine("Avatar preset for {0} ({1})", mod.ModIdentifier, mod.CoverImage.URL);
                    var uri = new Uri(mod.CoverImage.URL);
                    mod.CoverImage.Filepath = mod.ModIdentifier + "/" + mod.ModIdentifier + ".png";
                    //Capture image
                    try
                    {
                        CDNClient.DownloadFile(mod.CoverImage.URL, mod.CoverImage.Filepath);
                    }
                    catch
                    {
                        Console.WriteLine("Could not download avatar for {0}", mod.ModIdentifier);
                        mod.CoverImage.URL = string.Empty;
                        goto a;                        
                    }

                    using (Process magickProcess = new Process())
                    {
                        if (!Directory.Exists(mod.ModIdentifier))
                            Directory.CreateDirectory(mod.ModIdentifier);
                        magickProcess.StartInfo.UseShellExecute = false;
                        magickProcess.StartInfo.FileName = "magick.exe";
                        magickProcess.StartInfo.WorkingDirectory = mod.ModIdentifier;
                        magickProcess.StartInfo.Arguments = $"convert -verbose {mod.ModIdentifier + ".png"} {mod.ModIdentifier + ".ico"}";
                        magickProcess.Start();
                        Console.WriteLine("Times out in 30 seconds");
                        magickProcess.WaitForExit(30000);
                        if (magickProcess.ExitCode != 0)
                        {
                            Console.WriteLine("Failed to capture image or create icon");
                        }
                        else
                        {
                            Console.WriteLine("Captured image and created icon successfully");
                            var iniPath = mod.ModIdentifier + "/desktop.ini";
                            if (File.Exists(iniPath))
                                File.SetAttributes(iniPath, File.GetAttributes(iniPath) & ~(FileAttributes.Hidden | FileAttributes.System));                            
                            using (StreamWriter stw = new StreamWriter(iniPath, false))
                            {
                                //stw.WriteLine("[{0}]", mod.ModIdentifier);
                                //stw.WriteLine("Icon={0}", mod.ModIdentifier + ".ico");
                                stw.WriteLine("[.ShellClassInfo]");
                                stw.WriteLine("IconResource={0},0", mod.ModIdentifier + ".ico");
                                stw.WriteLine("IconFile={0}", mod.ModIdentifier + ".ico");
                                stw.WriteLine("IconIndex=0");
                                stw.WriteLine("[ViewState]");
                                stw.WriteLine("Mode=");
                                stw.WriteLine("Vid=");
                                stw.WriteLine("FolderType=Generic");
                            }
                            File.SetAttributes(iniPath, File.GetAttributes(iniPath) | FileAttributes.Hidden | FileAttributes.System);
                            //set the folder as system
                            File.SetAttributes(mod.ModIdentifier, File.GetAttributes(mod.ModIdentifier) | FileAttributes.System);
                        }
                    }
                }

                a:;

                ModsToCaptureInfoPage.Dequeue();

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}\r\n{1}", e.Message, e.StackTrace);
                NeedUserAttention(1);
                if (!FailedToCaptureInfoPage.Contains(mod.ModIdentifier))
                    FailedToCaptureInfoPage.Add(mod.ModIdentifier);
                ModsToCaptureInfoPage.Dequeue();
                return false;
            }

        }

        public static bool NeedUserAttention(string message = "Program ran into an error, would you like to continue?")
        {
            Console.WriteLine(message + "\a\a\a");
            Interop.Interop.FlashWindowEx();
            while (true)
            {
                Console.WriteLine("[Y/N]? ");
                var key = Console.ReadKey(false);
                Console.WriteLine();
                if (key.Key == ConsoleKey.Y)
                    return true;
                if (key.Key == ConsoleKey.N)
                    return false;
            }
        }

        public static void NeedUserAttention(int exitCode, string message = "Program ran into an error, would you like to continue?")
        {
            Console.WriteLine(message + "\a\a\a");
            Interop.Interop.FlashWindowEx();
            while (true)
            {
                Console.Write("[Y/N]? ");
                var key = Console.ReadKey(true);
                Console.WriteLine(key.KeyChar);
                if (key.Key == ConsoleKey.Y)
                    return;
                if (key.Key == ConsoleKey.N)
                {
                    Console.WriteLine("Exit requested.");
                    Environment.Exit(exitCode);
                    return;
                }
            }
        }

        /// <summary>
        /// Load a new page from curseforge
        /// Never repeats a download
        /// </summary>
        /// <returns>false if there are no pages left to mirror</returns>
        public static bool Mirror_LoadPage()
        {            
            //Select next page to parse
            int nextPage = 0;
            while (true)
            {
                nextPage = (new Random()).Next(1, PageCount + 1);
                if (!ParsedPages.Contains(nextPage))
                    break;
                if (ParsedPages.Count > PageCount - 1)
                    return false;
            }

            /*
            string url = "http://www.curseforge.com/minecraft/mc-mods";
            if (nextPage > 1)
                url += $"?page={nextPage}";
            */
            string url = "http://www.curseforge.com/minecraft/mc-mods?filter-sort=-1";
            if (nextPage > 1)
                url += $"&page={nextPage}";

            //Obtain page
            HtmlDocument doc;
            try
            {
                doc = GetHtmlDocument(url);
            }
            catch
            {
                //Most likely captcha
                Console.WriteLine("Failed to retrieve document");
                return false;
            }            

            //HtmlDocument doc = new HtmlDocument();
            //doc.Load("technology.1");

            //Parse page to obtain mods
            string selector = "//div[@class='project-listing-row box py-3 px-4 flex flex-col lg:flex-row lg:items-center']/div[@class='flex flex-col']";
            foreach (var node in doc.DocumentNode.SelectNodes(selector))
            {
                Mod modInit = new Mod();
                modInit.ModName = node.SelectNodes(".//h3[@class='text-primary-500 font-bold text-lg']")[0].InnerText.HtmlDecode();
                modInit.ModIdentifier = node.SelectNodes(".//a[@class='my-auto']")[0].Attributes["href"].DeEntitizeValue.Split('/').Last();
                modInit.Author = node.SelectNodes(".//a[@class='text-base leading-normal font-bold hover:no-underline my-auto']")[0].InnerText.HtmlDecode();
                modInit.CreationDate = DateTime.UnixEpoch.AddSeconds(Convert.ToInt64(node.SelectNodes(".//span[@class='text-xs text-gray-500']/abbr")[0].Attributes["data-epoch"].Value));
                var updateDateNodes = node.SelectNodes(".//span[@class='mr-2 text-xs text-gray-500']/abbr");
                if (updateDateNodes != null && updateDateNodes.Count > 0)
                {
                    if (updateDateNodes[0] != null && updateDateNodes[0].Attributes.Contains("data-epoch") && updateDateNodes[0].Attributes["data-epoch"].Value != null)
                        modInit.UpdateDate = DateTime.UnixEpoch.AddSeconds(Convert.ToInt64(updateDateNodes[0].Attributes["data-epoch"].Value));
                }
                modInit.Synopsis = node.SelectNodes(".//p[@class='text-sm leading-snug']")[0].InnerText.HtmlDecode().Trim('\r','\n',' ', '\t'); //This is the synopsis, we do not need leading tabs

                if (!ParsedModIdentifiers.Contains(modInit.ModIdentifier))
                {
                    Console.WriteLine("Enqueued: {0}", modInit.ModIdentifier);
                    ModsToScrape.Enqueue(modInit);
                    ModsToCaptureInfoPage.Enqueue(modInit.ModIdentifier);
                    ParsedModIdentifiers.Add(modInit.ModIdentifier);
                }
                else
                {
                    Console.WriteLine("Duplicate identifer: {0}", modInit.ModIdentifier);
                }
            }

            //Tell future instances
            ParsedPages.Add(nextPage);

            return true;
        }

        public static string CreateCDNUrl(DownloadFile file)
        {
            return "";
        }

        /// <summary>
        /// Scrapes the download page of mods that have differing download names
        /// This may be considered a waste of time
        /// </summary>
        /// <returns></returns>
        public static bool Mirror_DownloadModPage()
        {
            //Check if any files need to be captured
            if (FilesToCaptureDownloadPage.Count < 1)
                return false;

            var dlFileId = FilesToCaptureDownloadPage.Peek();
            var dlFile = new KeyValuePair<Mod, DownloadFile>(Mods[dlFileId.Key], DownloadRegistry[dlFileId.Value]);

            try
            {
                Console.WriteLine($"Capturing download page of {dlFile.Key.ModIdentifier}:{dlFile.Value.Id}");
                string dlPageURL = $"http://www.curseforge.com/minecraft/mc-mods/{dlFile.Key.ModIdentifier}/files/{dlFile.Value.Id}";
                var doc = GetHtmlDocument(dlPageURL);
                string pageXPath = "//div[@class='flex flex-col md:flex-row justify-between border-b border-gray--100 mb-2 pb-4']/div/span[@class='text-sm']";
                var filename = doc.DocumentNode.SelectNodes(pageXPath)[0];
                dlFile.Value.Filename = filename.InnerText.HtmlDecode();
                logFile.WriteLine($"{dlFile.Value.Id}:{dlFile.Value.Filename}");
                logFile.Flush();
                dlFile.Value.DownloadedFilePage = true;
                FilesToCaptureDownloadPage.Dequeue();
                FilesToDownload.Enqueue(dlFileId);
            }
            catch (Exception e)
            {
                string er = $"Failed to capture download page {dlFile.Key.ModIdentifier}:{dlFile.Value.Id}";
                dlFile.Value.DownloadedFilePage = true;
                Console.WriteLine(er);
                failFile.WriteLine(er);
                failFile.Flush();
                NeedUserAttention(1);
                FilesToCaptureDownloadPage.Dequeue();
                if (!FilesCouldNotDownload.ContainsKey(dlFileId.Key))
                    FilesCouldNotDownload.Add(dlFileId.Key, new HashSet<int>());
                if (!FilesCouldNotDownload[dlFileId.Key].Contains(dlFileId.Value))
                    FilesCouldNotDownload[dlFileId.Key].Add(dlFileId.Value);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Load a new page from the list of mod downloads
        /// Selects a random mod
        /// Never repeats a download
        /// </summary>
        /// <returns>false if all the pages from each mod have been mirrored</returns>
        public static bool Mirror_LoadModPage()
        {
            //50/50 chance to either collect a new mod or continue scraping a random mod
            //The point of this function is to collect mods to download. All things considered, this function is performing 2 functions.
            if (ModsToScrape.Count > 0 && ((new Random()).Next(0,2) == 0 || Mods.Count == 0))
            {
                //Collect a new mod. This means capture the page count and files on the first page
                Mod collect = ModsToScrape.Peek();
                Console.WriteLine("Collecting: {0}", collect.ModIdentifier);


                HtmlDocument doc;
                try
                {
                    doc = GetHtmlDocument($"http://www.curseforge.com/minecraft/mc-mods/{collect.ModIdentifier}/files/all");
                }
                catch
                {
                    //Most likely captcha
                    Console.WriteLine("Failed to retrieve document");
                    return false;
                }

                //Capture page count
                var pages = doc.DocumentNode.SelectNodes("//div[@class='pagination pagination-top flex items-center']");
                if (pages is null)
                {
                    collect.PageCount = 1;
                }
                else
                {
                    pages = pages[0].SelectNodes("./a");
                    collect.PageCount = Convert.ToInt32(pages.Last().InnerText);
                }

                //Capture files
                var files = doc.DocumentNode.SelectNodes("//tr/td/a[@data-action='file-link']");
                if (files != null && files.Count > 0) //Some projects have 0 files, just learned about that
                foreach (var file in files)
                {
                    DownloadFile dlFile = new DownloadFile();
                    dlFile.Id = Convert.ToInt32(file.Attributes["href"].Value.Split('/').Last());
                    dlFile.Filename = file.InnerText.HtmlDecode();
                    Console.WriteLine("{0} : {1}", dlFile.Id, dlFile.Filename);
                    logFile.WriteLine("{0}:{1}", dlFile.Id, dlFile.Filename);
                    if (!DownloadRegistry.ContainsKey(dlFile.Id))
                        DownloadRegistry.Add(dlFile.Id, dlFile);
                    else
                        DownloadRegistry[dlFile.Id] = dlFile;
                    collect.Files.Add(dlFile.Id);
                    //FilesToDownload.Enqueue(new KeyValuePair<Mod, DownloadFile>(collect, dlFile));
                    FilesToDownload.Enqueue(new KeyValuePair<string, int>(collect.ModIdentifier, dlFile.Id));
                }

                logFile.Flush();

                //Dequeue if success
                ModsToScrape.Dequeue();
                //Add to master list
                Mods.Add(collect.ModIdentifier, collect);
                //Mark page 1 as scraped
                collect.ParsedPages.Add(1);
                return true;
            }
            else
            {
                //Scrape a random mod. Capture a random page
                var rnd = new Random();
                int pageToScrape = 0;
                Mod mod;

                //Identify a mod that has pages to be scraped
                while (true)
                {
                    if (CompletedMods.Count >= Mods.Count)
                    {
                        Console.WriteLine("Completed download of all mods (or pages have yet to be parsed). Sleeping for 1 second");
                        Thread.Sleep(1000);
                        return false;
                    }

                    //mod = Mods[ParsedModIdentifiers[(new Random()).Next(0, ParsedModIdentifiers.Count)]];
                    //mod = Mods[Mods.Keys.ElementAt((new Random()).Next(0, Mods.Count))];
                    mod = Mods.ElementAt((new Random()).Next(0, Mods.Count)).Value;

                    if (mod.ParsedPages.Count > mod.PageCount - 1)
                    {
                        //Console.WriteLine("Downloaded all pages of {0} : {1}/{2}", mod.ModIdentifier, mod.ParsedPages.Count, mod.PageCount);
                        if (!CompletedMods.Contains(mod.ModIdentifier))
                        {
                            CompletedMods.Add(mod.ModIdentifier);
                            Console.WriteLine($"Completed: {mod.ModIdentifier}");
                        }
                        continue;
                    }

                    break;
                }

                //Pick a page that can be scraped
                int nextPage = 0;
                while (true)
                {
                    nextPage = rnd.Next(1, mod.PageCount + 1);
                    if (!mod.ParsedPages.Contains(nextPage))
                        break;
                    if (mod.ParsedPages.Count >= PageCount - 2)
                        return false;
                }

                string url = $"http://www.curseforge.com/minecraft/mc-mods/{mod.ModIdentifier}/files/all";
                if (nextPage > 1)
                    url += $"?page={nextPage}";

                //Obtain page
                HtmlDocument doc;
                try
                {
                    doc = GetHtmlDocument(url);
                }
                catch
                {
                    //Most likely captcha
                    Console.WriteLine("Failed to retrieve document");
                    return false;
                }

                //Capture files
                var files = doc.DocumentNode.SelectNodes("//tr/td/a[@data-action='file-link']");
                foreach (var file in files)
                {
                    DownloadFile dlFile = new DownloadFile();
                    dlFile.Id = Convert.ToInt32(file.Attributes["href"].Value.Split('/').Last());
                    dlFile.Filename = file.InnerText.HtmlDecode();
                    Console.WriteLine("{0} : {1}", dlFile.Id, dlFile.Filename);
                    logFile.WriteLine("{0}:{1}", dlFile.Id, dlFile.Filename);
                    if (!DownloadRegistry.ContainsKey(dlFile.Id))
                        DownloadRegistry.Add(dlFile.Id, dlFile);
                    else
                        DownloadRegistry[dlFile.Id] = dlFile;
                    mod.Files.Add(dlFile.Id);
                    FilesToDownload.Enqueue(new KeyValuePair<string, int>(mod.ModIdentifier, dlFile.Id));
                }

                logFile.Flush();

                if (!mod.ParsedPages.Contains(nextPage))
                    mod.ParsedPages.Add(nextPage);

                Console.WriteLine("Scraping: {0}", mod.ModIdentifier);

                return true;
            }
        }

        public static void PerformRandomMirrorAction()
        {
            Random rnd = new Random();
            int nextAction;
            bool[] returnValue = new bool[MirrorActions.Count];
            do //Keep trying to perform a mirror action until one returns true or succeeds
            {
                nextAction = rnd.Next(0, MirrorActions.Count);
                DownloadFiles();
            } while (!MirrorActions[nextAction]());
        }

        public static void DownloadFiles()
        {
            while (FilesToDownload.Count > 0)
            {
                var pair_ref = FilesToDownload.Peek();
                KeyValuePair<Mod, DownloadFile> pair
                    = new KeyValuePair<Mod, DownloadFile>(Mods[pair_ref.Key], DownloadRegistry[pair_ref.Value]);
                var file = pair.Value;
                string filename = file.Filename.EndsWith(".jar") ? file.Filename : file.Filename + ".jar";
                string filenameLink = filename;
                filenameLink.Replace("+", "%2B");
                filename = filename.Replace(' ', '+');//4,3 Substring(0,4) Remove(0,4)
                filenameLink = filename.Replace(' ', '+');                
                string link = $"https://media.forgecdn.net/files/{file.Id.ToString().Substring(0, file.Id.ToString().Length - 3)}/{file.Id.ToString().Remove(0, file.Id.ToString().Length - 3).TrimStart('0')}/{filenameLink}";
                filename = pair.Key.ModIdentifier.WindowsEncode() + "\\" + pair.Value.Id + "-" + filename;
                Directory.CreateDirectory(pair.Key.ModIdentifier.WindowsEncode());
                try
                {
                    CDNClient.DownloadFile(link, filename);
                    Console.WriteLine("Downloaded {0} -> {1}", link, filename);
                    file.Downloaded = true;
                    FilesToDownload.Dequeue();
                }
                catch (Exception e)
                {
                    if (pair.Value.DownloadedFilePage)
                    {
                        string er = $"Could not download {link}: {e.Message}";
                        Console.WriteLine(er);
                        failFile.WriteLine(er);
                        failFile.Flush();
                        //NeedUserAttention();
                        Console.WriteLine("Will not requeue");

                        if (!FilesCouldNotDownload.ContainsKey(pair.Key.ModIdentifier))
                            FilesCouldNotDownload.Add(pair.Key.ModIdentifier, new HashSet<int>());
                        if (!FilesCouldNotDownload[pair.Key.ModIdentifier].Contains(pair.Value.Id))
                            FilesCouldNotDownload[pair.Key.ModIdentifier].Add(pair.Value.Id);

                        FilesToDownload.Dequeue();
                    }
                    else
                    {
                        string er = $"Error with {link}: {e.Message}";
                        Console.WriteLine(er);
                        failFile.WriteLine(er);
                        failFile.Flush();
                        //NeedUserAttention();
                        Console.WriteLine("Adding to list");
                        FilesToCaptureDownloadPage.Enqueue(pair_ref);
                        FilesToDownload.Dequeue();
                    }
                }
                Console.WriteLine("Sleeping...");
                Thread.Sleep(1000);
            }
        }

        public static void TestXPath(string path)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.Load(path);
            //HtmlDocument doc = GetHtmlDocument(path);
            /*
            string pageXPath = "//div[@class='flex flex-col md:flex-row justify-between border-b border-gray--100 mb-2 pb-4']/div/span[@class='text-sm']";
            var filename = doc.DocumentNode.SelectNodes(pageXPath)[0];
            Console.WriteLine(filename.InnerText.HtmlDecode());
            */
            Mod mod = new Mod();
            //Mod description
            mod.Description = (doc.DocumentNode.SelectSingleNode("//div[@class='box p-4 pb-2 project-detail__content']") ?? HtmlNode.CreateNode("")).InnerText.HtmlDecode();
            //Members
            foreach (var authorNode in doc.DocumentNode.SelectNodes("//div[@class='flex mb-2']/div[@class='flex flex-col flex-grow']/p[@class='text-sm text-primary-500 flex']/a/span").Select(n => n.InnerText))
            {
                string author = authorNode.Trim('\r', '\n', ' ', '\0', '\t');
                if (!mod.CompleteAuthorList.Contains(author))
                    mod.CompleteAuthorList.Add(author);
            }
            //Source
            var srcNodes = (doc.DocumentNode.SelectNodes("//li[@class=' b-list-item p-nav-item px-2 pb-1/10 -mb-1/10 text-gray-500']/a[@class='text-gray-500 hover:no-underline']"));
            if (srcNodes != null)
                if (srcNodes.Any(n => n.InnerText.Contains("Source")))
                    if (srcNodes.First(n => n.InnerText.Contains("Source")).Attributes.Contains("href"))
                        mod.SourceURL = srcNodes.First(n => n.InnerText.Contains("Source")).Attributes["href"].Value;

            //Download Count
            mod.DownloadCount = Convert.ToInt32(doc.DocumentNode.SelectSingleNode("//div[@class='flex flex-col mt-auto mb-auto']/div[@class='flex']/span[@class='mr-2 text-sm text-gray-500']").InnerText.Split(' ')[0].Replace(",", ""));

            mod.ProjectId = Convert.ToInt32(doc.DocumentNode.SelectSingleNode("//div[@class='pb-4 border-b border-gray--100']/div[@class='flex flex-col mb-3']/div[@class='w-full flex justify-between']/span[2]").InnerText);
            

            //Print out results
            Console.WriteLine("{0}: {1}", mod.ModIdentifier, mod.ModName);
            Console.WriteLine("Description: {0}", mod.Description);
            Console.WriteLine("Authors: {0}", mod.CompleteAuthorList.Aggregate((a, b) => $"{a}, {b}"));
            Console.WriteLine("Source: {0}", mod.SourceURL);
            Console.WriteLine("Downloads: {0}", mod.DownloadCount);
            Console.WriteLine("ProjectID: {0}", mod.ProjectId);

            //foreach (var a in filename)
            //{
            //    Console.WriteLine(a.InnerText);
            //}
            /*
            string pageXPath = "//div[@class='pagination pagination-top flex items-center']";
            var pages = doc.DocumentNode.SelectNodes(pageXPath)[0].SelectNodes("./a");
            int pageCount = Convert.ToInt32(pages.Last().InnerText);
            Console.WriteLine("Pages: {0}", pageCount);
            string filesXPath = "//tr/td/a[@data-action='file-link']";
            var files = doc.DocumentNode.SelectNodes(filesXPath);
            foreach (var file in files)
            {
                DownloadFile dlFile = new DownloadFile();
                dlFile.Id = Convert.ToInt32(file.Attributes["href"].Value.Split('/').Last());
                dlFile.Filename = file.InnerText.HtmlDecode();
                Console.WriteLine("{0} : {1}", dlFile.Id, dlFile.Filename);
                logFile.WriteLine("{0}:{1}", dlFile.Id, dlFile.Filename);
            }
            logFile.Flush();
            */
            /*
            string o = "//div[@class='project-listing-row box py-3 px-4 flex flex-col lg:flex-row lg:items-center']/div[@class='flex flex-col']";
            var nodes = doc.DocumentNode.SelectNodes(o);
            var d = doc.DocumentNode;
            string p = o + "/div[@class='lg:flex items-end hidden']";
            string q = o + "/div[@class='flex my-1']";
            foreach (var node in nodes)
            {
                //var a = node.SelectNodes(p);
                //var b = node.SelectNodes(q);
                //Title
                //Console.WriteLine(d.SelectNodes(p + "/a[@class='my-auto']/h3[@class='text-primary-500 font-bold text-lg']")[0].InnerText);                
                Console.WriteLine(node.SelectNodes(".//h3[@class='text-primary-500 font-bold text-lg']")[0].InnerText.HtmlDecode());
                //Identifier
                Console.WriteLine(node.SelectNodes(".//a[@class='my-auto']")[0].Attributes["href"].DeEntitizeValue.Split('/').Last());
                //Project manager
                Console.WriteLine(node.SelectNodes(".//a[@class='text-base leading-normal font-bold hover:no-underline my-auto']")[0].InnerText.HtmlDecode());
                //Downloads
                Console.WriteLine(node.SelectNodes(".//span[@class='mr-2 text-xs text-gray-500']")[0].InnerText.HtmlDecode());
                //Updated
                Console.WriteLine(node.SelectNodes(".//span[@class='mr-2 text-xs text-gray-500']")[1].InnerText.HtmlDecode());
                //Created
                Console.WriteLine(node.SelectNodes(".//span[@class='text-xs text-gray-500']")[0].InnerText.HtmlDecode());
                //Synopsis
                Console.WriteLine(node.SelectNodes(".//p[@class='text-sm leading-snug']")[0].InnerText.HtmlDecode());
                Console.WriteLine();
                //Console.WriteLine(node.InnerText);
                Console.ReadKey(true);
            }
            */
        }

        [JsonIgnore]
        public static NewContainer data = new NewContainer();

        public static void SaveThread()
        {
            Console.WriteLine("Autosaving enabled.");
            int current = 0;
            while (true)
            {
                try
                {

                    Thread.Sleep(1800000);
                    Console.Write("Saving..."); //Just hope a property isn't being iterated over
                    SaveState($"savedState.new.auto.{current++ % 10}.json");
                    //Console_CancelKeyPress(null, null);
                    Console.WriteLine("Saved.");
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// See if downloaded mods are present
        /// </summary>
        public static void Verify()
        {

        }

        public static void Main(string[] args)
        {
            Environment.CurrentDirectory = "D:\\curseforge\\";
            //DownloadFile file = new DownloadFile();
            //file.Id = 3333444;
            //file.Filename = "unity.jar";
            //string link = $"https://media.forgecdn.net/files/{file.Id.ToString().Substring(0, 4)}/{file.Id.ToString().Substring(4, 3)}/{file.Filename}";
            //Console.ReadKey(true);
            //return;
            //TestXPath("technology.1");
            //Mirror_LoadPage();
            //TestXPath("all");
            //TestXPath("jei");
            //NeedUserAttention();
            //ProxyClient.Proxy = new WebProxy("192.168.128.17", 9097);
            //TestXPath("http://www.curseforge.com/minecraft/mc-mods/nether-mod/files/2916574");
            //Console.WriteLine("Done.");
            //Console.ReadKey(true);
            //return;
            
            //Load saved state
            if (File.Exists("savedState.new.json"))
            {
                using (FileStream fstream = new FileStream("savedState.new.json", FileMode.Open, FileAccess.Read, FileShare.None, 1048576))
                using (StreamReader sr = new StreamReader(fstream))
                using (JsonReader jr = new JsonTextReader(sr))
                {
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    data = jsonSerializer.Deserialize<NewContainer>(jr);
                    Mods = data.Mods;
                    ModsToScrape = data.ModsToScrape;
                    ModsToCaptureInfoPage = data.ModsToCaptureInfoPage;
                    CompletedModInfo = data.CompletedModInfo;
                    FilesToDownload = data.FilesToDownload;
                    FilesToCaptureDownloadPage = data.FilesToCaptureDownloadPage;
                    FilesCouldNotDownload = data.FilesCouldNotDownload;
                    FailedToCaptureInfoPage = data.FailedToCaptureInfoPage;
                    DownloadRegistry = data.DownloadRegistry;
                    ParsedModIdentifiers = data.ParsedModIdentifiers;
                    CompletedMods = data.CompletedMods;
                    ParsedPages = data.ParsedPages;
                    lastRequest = data.lastRequest;
                    /*
                    Mods = jsonSerializer.Deserialize<Dictionary<string, Mod>>(jr);
                    ModsToScrape = jsonSerializer.Deserialize<Queue<Mod>>(jr);
                    FilesToDownload = jsonSerializer.Deserialize<Queue<KeyValuePair<Mod, DownloadFile>>>(jr);
                    FilesToCaptureDownloadPage = jsonSerializer.Deserialize<Queue<KeyValuePair<Mod, DownloadFile>>>(jr);
                    ParsedModIdentifiers = jsonSerializer.Deserialize<List<string>>(jr);
                    CompletedMods = jsonSerializer.Deserialize<HashSet<string>>(jr);
                    ParsedPages = jsonSerializer.Deserialize<List<int>>(jr);
                    lastRequest = jsonSerializer.Deserialize<DateTime>(jr);
                    */
                }
            }

            
            data.Mods = Mods;
            data.ModsToScrape = ModsToScrape;
            data.ModsToCaptureInfoPage = ModsToCaptureInfoPage;
            data.CompletedModInfo = CompletedModInfo;
            data.FilesToDownload = FilesToDownload;
            data.FilesToCaptureDownloadPage = FilesToCaptureDownloadPage;
            data.FilesCouldNotDownload = FilesCouldNotDownload;
            data.FailedToCaptureInfoPage = FailedToCaptureInfoPage;
            data.ParsedModIdentifiers = ParsedModIdentifiers;
            data.DownloadRegistry = DownloadRegistry;
            data.CompletedMods = CompletedMods;
            data.ParsedPages = ParsedPages;
            data.lastRequest = lastRequest;
            
            /*
            var copy = data.FilesToCaptureDownloadPage.ToArray();
            data.FilesToCaptureDownloadPage.Clear();

            foreach (var a in copy)
            {
                if (!data.FilesToCaptureDownloadPage.Any(n => n.Value == a.Value && n.Key == a.Key))
                    data.FilesToCaptureDownloadPage.Enqueue(a);                    
            }

            SaveState("savedState.new.fix.json");
            return;
            */

            /*
            //Convert
            NewContainer newData = new NewContainer();
            foreach (var mod in data.Mods)
                foreach (var file in mod.Value.Files)
                {
                    mod.Value._Files.Add(file.Id);
                    if (!newData.DownloadRegistry.ContainsKey(file.Id))
                        newData.DownloadRegistry.Add(file.Id, file);
                }
            newData.Mods = data.Mods;
            foreach (var mod in data.ModsToScrape)
                foreach (var file in mod.Files)
                {
                    mod._Files.Add(file.Id);
                    if (!newData.DownloadRegistry.ContainsKey(file.Id))
                        newData.DownloadRegistry.Add(file.Id, file);
                }
            newData.ModsToScrape = data.ModsToScrape;
            if (data.FilesToDownload.Count > 0)
                data.FilesToDownload.Select(n => new KeyValuePair<string, int>(n.Key.ModIdentifier, n.Value.Id)).ToList().ForEach(newData.FilesToDownload.Enqueue);
            if (data.FilesToCaptureDownloadPage.Count > 0)
                data.FilesToCaptureDownloadPage.Select(n => new KeyValuePair<string, int>(n.Key.ModIdentifier, n.Value.Id)).ToList().ForEach(newData.FilesToCaptureDownloadPage.Enqueue);
            newData.ParsedModIdentifiers = data.ParsedModIdentifiers;
            newData.CompletedMods = data.CompletedMods;
            newData.ParsedPages = data.ParsedPages;
            newData.lastRequest = data.lastRequest;

            using (FileStream fstream = new FileStream("newFormat.json", FileMode.Create, FileAccess.Write, FileShare.None, 1048576))
            using (StreamWriter sw = new StreamWriter(fstream))
            using (JsonWriter jw = new JsonTextWriter(sw))
            {
                JsonSerializer jsonSerializer = new JsonSerializer();

                jsonSerializer.Serialize(jw, newData);
            }
            return;
            */

            Console.CancelKeyPress += Console_CancelKeyPress;

            if (args.Any("--verify".Equals)) Verify();

            MirrorActions.Add(Mirror_LoadPage);
            MirrorActions.Add(Mirror_LoadModPage);
            MirrorActions.Add(Mirror_DownloadModPage);
            MirrorActions.Add(Mirror_LoadModInfoPage);
            var savingThread = new Thread(SaveThread);
            savingThread.Start();

            ProxyClient.Proxy = new WebProxy("192.168.128.17", 9097); //Cloudscrape request system hosted on a seperate machine

            try
            {
                while (true)
                {
                    PerformRandomMirrorAction();
                    if (Console.KeyAvailable)
                        if (Console.ReadKey(true).Key == ConsoleKey.Spacebar)
                            Console.WriteLine($"STATUS: Pages: {ParsedPages.Count}/{PageCount} Mods: {{Σ{DownloadRegistry.Count}:{Mods.Count}:{ModsToScrape.Count}:{CompletedMods.Count}:{ParsedModIdentifiers.Count}}} Files: {{+{DownloadRegistry.Sum(n => n.Value.Downloaded ? 1 : 0)}:{FilesToDownload.Count}:{FilesToCaptureDownloadPage.Count}:!{FilesCouldNotDownload.Sum(n => n.Value.Count)}}} Info: {{+{CompletedModInfo.Count}:!{Mods.Sum(n => n.Value.FailedToRetrieveSource ? 1 : 0)}:-{Mods.Sum(n => n.Value.HasImage ? 1 : 0)}:{Mods.Sum(n => n.Value.HasSource ? 1 : 0)}}}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Program crash: {e.Message}");
                Console.WriteLine(e.StackTrace);
                //Save
                SaveState("savedState.new.json");
                //Console_CancelKeyPress(null, null);
                NeedUserAttention(1);
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        public class NewContainer
        {
            public Dictionary<string, Mod> Mods
                = new Dictionary<string, Mod>();
            public Queue<Mod> ModsToScrape
                = new Queue<Mod>();
            public Queue<KeyValuePair<string, int>> FilesToDownload
                = new Queue<KeyValuePair<string, int>>();
            public Queue<KeyValuePair<string, int>> FilesToCaptureDownloadPage
                = new Queue<KeyValuePair<string, int>>();
            public Dictionary<string, HashSet<int>> FilesCouldNotDownload
                = new Dictionary<string, HashSet<int>>();
            public HashSet<string> FailedToCaptureInfoPage
                = new HashSet<string>();
            public Dictionary<int, DownloadFile> DownloadRegistry
                = new Dictionary<int, DownloadFile>();
            public List<string> ParsedModIdentifiers
                = new List<string>();
            public HashSet<string> CompletedMods
                = new HashSet<string>();
            public Queue<string> ModsToCaptureInfoPage
                = new Queue<string>();
            public HashSet<string> CompletedModInfo
                = new HashSet<string>();
            public List<int> ParsedPages
                = new List<int>();
            public DateTime lastRequest = DateTime.Now.AddSeconds(-60);
        }

        public class Container
        {
            public Dictionary<string, Mod> Mods;
            public Queue<Mod> ModsToScrape;
            public Queue<KeyValuePair<Mod, DownloadFile>> FilesToDownload;
            public Queue<KeyValuePair<Mod, DownloadFile>> FilesToCaptureDownloadPage;
            public List<string> ParsedModIdentifiers;
            public HashSet<string> CompletedMods;
            public List<int> ParsedPages;
            public DateTime lastRequest = DateTime.Now.AddSeconds(-60);
        }

        private static void SaveState(string path)
        {
            using (FileStream fstream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1048576))
            using (StreamWriter sw = new StreamWriter(fstream))
            using (JsonWriter jw = new JsonTextWriter(sw))
            {
                JsonSerializer jsonSerializer = new JsonSerializer();

                jsonSerializer.Serialize(jw, data);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            SaveState("savedState.new.json");
            //Program exit
            Environment.Exit(0);
        }
    }

    public static class Extensions
    {
        public static string HtmlDecode(this string str)
        {
            return WebUtility.HtmlDecode(str);
        }

        public static string WindowsEncode(this string str)
        {
            return str.Replace("<", "")
                      .Replace(">", "")
                      .Replace(":", "")
                      .Replace("\"", "")
                      .Replace("/", "")
                      .Replace("\\", "")
                      .Replace("|", "")
                      .Replace("?", "")
                      .Replace("*", "");
        }
    }
}
