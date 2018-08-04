using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace ClientZipAutomator
{
    class Entry
    {
        private readonly Manager _manger;

        private string _zipName;
        private bool _active;
        public String FolderName { get; }


        public String ZipName
        {
            get => _zipName;
            set
            {
                var save = _zipName;
                if (_manger.LolPathValid && value != _zipName)
                {
                    var endPath = _manger.LolPath.EndsWith("/") ? _manger.LolPath : _manger.LolPath + "/";

                    if (File.Exists(endPath + _zipName))
                    {
                        _manger._mainWindow.disableButtons();

                        BackgroundWorker backgroundWorker = new BackgroundWorker
                        {
                            WorkerReportsProgress = true
                        };

                        backgroundWorker.ProgressChanged += (xxxx, args) =>
                        {
                            _manger._mainWindow.progressBar.Value = args.ProgressPercentage;
                            if (args.UserState is string)
                            {
                                _manger._mainWindow.Block.Text = (string)args.UserState;
                            }
                        };
                        backgroundWorker.DoWork += (sender, args) =>
                        {
                            backgroundWorker.ReportProgress(25,
                                "Deleting old zip file: " + save + " has been renamed to: " + _zipName);
                            File.Delete(endPath + save);
                            _manger.removeFromClientZip(endPath, save + "\n");
                            Thread.Sleep(3800);
                        };
                        backgroundWorker.RunWorkerCompleted += (ssssss, args) =>
                        {
                            _manger._mainWindow.Block.Text = "Ready";
                            _manger._mainWindow.progressBar.Value = 0;
                            _manger._mainWindow.enableButtons();
                            _manger._mainWindow.updateGrid();
                        };
                        backgroundWorker.RunWorkerAsync();
                    }
                }

                _zipName = value;
                _manger.writeConf();
            }
        }


        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                _manger.writeConf();
            }
        }

        public Entry(Manager manger, string zipName, string folderName, bool active)
        {
            _manger = manger;
            _zipName = zipName;
            FolderName = folderName;
            _active = active;
        }
    }

    class Manager
    {
        public readonly MainWindow _mainWindow;
        public List<Entry> Entries { get; }
        public bool LolPathValid { get; private set; }
        public string LolPath { get; private set; }

        public Manager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;

            Entries = new List<Entry>();
            setup();
        }


        public void applyEntries()
        {
            if (LolPathValid)
            {
                BackgroundWorker backgroundWorker = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };

                backgroundWorker.ProgressChanged += (sender, args) =>
                {
                    _mainWindow.progressBar.Value = args.ProgressPercentage;
                    if (args.UserState is string)
                    {
                        _mainWindow.Block.Text = (string)args.UserState;
                    }
                };


                backgroundWorker.DoWork += (sender, e) =>
                {
                    var progress = 0;
                    double am = 0;
                    foreach (var entry in Entries)
                    {
                        if (entry.Active) am++;
                    }

                    backgroundWorker.ReportProgress(0);

                    Console.WriteLine(am);

                    am = 100 / am;

                    var endPath = LolPath.EndsWith("/") ? LolPath : LolPath + "/";
                    var toRemove = new List<Entry>();
                    foreach (var entry in Entries)
                    {
                        backgroundWorker.ReportProgress(progress, "Processing: " + entry.ZipName);

                        if (File.Exists(endPath + entry.ZipName))
                        {
                            File.Delete(endPath + entry.ZipName);
                        }

                        if (!Directory.Exists(entry.FolderName))
                        {
                            continue;
                        }

                        if (!entry.Active)
                        {
                            removeFromClientZip(endPath, entry.ZipName + "\n");
                            continue;
                        }

                        var tempDir = getTemporaryDirectory();
                        var tempPath = Directory.CreateDirectory(tempDir + "/ASSETS").FullName;
                        copyFiles(entry.FolderName, tempPath);
                        ZipFile.CreateFromDirectory(tempDir, endPath + entry.ZipName);
                        processInfoFile(endPath, entry.ZipName);
                        Directory.Delete(tempDir, true);
                        progress += (int)am;
                        backgroundWorker.ReportProgress(progress);
                    }

                    foreach (var entry in toRemove)
                    {
                        Entries.Remove(entry);
                    }

                    _mainWindow.updateGrid();
                };
                backgroundWorker.RunWorkerCompleted += (sender, args) =>
                {
                    _mainWindow.Block.Text = "Ready";
                    _mainWindow.progressBar.Value = 0;
                    _mainWindow.enableButtons();
                    MessageBox.Show("Done!");
                };
                _mainWindow.disableButtons();
                backgroundWorker.RunWorkerAsync();
            }
        }

        public void removeFromClientZip(string path, string toReplace)
        {
            var fPath = path + "ClientZips.txt";

            if (File.Exists(fPath))
            {
                var content = readFile(fPath).Replace("\r", "").Replace(toReplace, "");
                File.WriteAllText(fPath, content);
            }
        }

        public void addEntry(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var entry in Entries)
            {
                if (entry.FolderName == path)
                {
                    MessageBox.Show("This Folder was already added");
                    return;
                }
            }

            string name;
            var directoryInfo = new DirectoryInfo(path).Parent;
            if (directoryInfo != null)
            {
                if (directoryInfo.Parent == null)
                {
                    Console.Out.WriteLine(
                        "WARNING: Putting the ASSETS Folder in the top level of a drive is not recommended!");
                    name = directoryInfo.Name.Replace(" ", "").Replace("\\", "").Replace(":", "") + ".zip";
                }
                else
                {
                    name = directoryInfo.Name.Replace(" ", "_") + ".zip";
                }
            }
            else
            {
                Console.Out.WriteLine("WARNING: the selected folder has no parent, using the date as name!");
                name = DateTime.Today.ToString(CultureInfo.InvariantCulture).Replace(" ", "_").Replace(".", "_") +
                       ".zip";
            }

            foreach (var entry in Entries)
            {
                if (entry.ZipName == name)
                {
                    MessageBox.Show("The zipname: " + name + ", is already contained in a entry");
                    return;
                }
            }

            Entries.Add(new Entry(this, name, path, true));
            writeConf();
        }

        private void parseConf()
        {
            var obj = JObject.Parse(File.ReadAllText("data.json"));
            string lolPath = (string)obj["path"];

            if (pathValid(lolPath))
            {
                this.LolPath = lolPath;
                this.LolPathValid = true;
            }
            else
            {
                this.LolPath = "";
                this.LolPathValid = false;
            }

            Entries.Clear();
            var arr = (JArray)obj["entries"];

            foreach (JObject entry in arr)
            {
                if (!Directory.Exists((string)entry["path"])) continue;
                this.Entries.Add(
                    new Entry(this, (string)entry["name"], (string)entry["path"], (bool)entry["active"]));
            }

           
        }

        public void addExternalZips()
        {
            if (LolPathValid)
            {
                BackgroundWorker backgroundWorker = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };

                backgroundWorker.ProgressChanged += (sender, args) =>
                {
                    _mainWindow.progressBar.Value = args.ProgressPercentage;
                    if (args.UserState is string)
                    {
                        _mainWindow.Block.Text = (string)args.UserState;
                    }
                };


                backgroundWorker.DoWork += (sender, e) =>
                {
                    var endPath = LolPath.EndsWith("/") ? LolPath : LolPath + "/";
                    if (File.Exists(endPath + "ClientZips.txt"))
                    {
                        var am = 0;
                        var progress = 0;
                        foreach (var s in readFile(endPath + "ClientZips.txt").Replace("\r", "").Split('\n'))
                        {
                            if (s == "") continue;
                            var found = false;
                            foreach (var entry in Entries)
                            {
                                if (entry.ZipName == s)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                am++;
                            }
                        }

                        if (am != 0) am = 100 / am;
                        foreach (var s in readFile(endPath + "ClientZips.txt").Replace("\r", "").Split('\n'))
                        {
                            if (s == "") continue;
                            var found = false;
                            foreach (var entry in Entries)
                            {
                                if (entry.ZipName == s)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                if (File.Exists(endPath + s))
                                {
                                    backgroundWorker.ReportProgress(progress, "Adding zip from League Folder: " + s);
                                    addManagedZip(endPath + s, s);
                                    progress += am;
                                    backgroundWorker.ReportProgress(progress, "Adding zip from League Folder: " + s);
                                }
                            }
                        }
                    }
                };
                backgroundWorker.RunWorkerCompleted += (sender, args) =>
                {
                    writeConf();

                    _mainWindow.Block.Text = "Ready";
                    _mainWindow.progressBar.Value = 0;
                    _mainWindow.enableButtons();
                    _mainWindow.updateGrid();

                };
                _mainWindow.disableButtons();
                backgroundWorker.RunWorkerAsync();
            }
        }

        public void addManagedZip(string path, string name)
        {
            var folderName = name.Replace(".zip", "");

            foreach (var entry in Entries)
            {
                if (entry.ZipName.ToLower() == name.ToLower())
                {
                    MessageBox.Show("The zipname: " + name + ", is already contained in a entry");

                    return;
                }
            }


            if (!Directory.Exists("managed")) Directory.CreateDirectory("managed");
            if (Directory.Exists("managed/" + folderName)) Directory.Delete("managed/" + folderName, true);
            Directory.CreateDirectory("managed/" + folderName);
            ZipFile.ExtractToDirectory(new FileInfo(path).FullName, "managed/" + folderName);
            Entries.Add(new Entry(this, name,
                new DirectoryInfo("managed/" + folderName).GetDirectories().First().FullName, true));
        }

        public void updateLolPath(string path)
        {
            if (pathValid(path))
            {
                this.LolPath = path;
                this.LolPathValid = true;
                writeConf();
                addExternalZips();
                MessageBox.Show("Updated League path sucessfully.");
            }
            else
            {
                MessageBox.Show("The Given path is not valid");
            }
        }

        private bool pathValid(string raw)
        {
            if (raw == null) return false;
            var path = "";
            if (raw.EndsWith("/"))
            {
                path = raw;
            }
            else
            {
                path = raw + "/";
            }

            return Directory.Exists(path) && File.Exists(path + "League of Legends.exe") &&
                   File.Exists(path + "stub.dll");
        }

        public void writeConf()
        {
            var obj = new JObject();
            obj["path"] = LolPath;
            var arr = new JArray();
            foreach (var entry in Entries)
            {
                if (!entry.ZipName.EndsWith(".zip"))
                {
                    entry.ZipName = entry.ZipName + ".zip";
                }

                var jEntry = new JObject();
                jEntry["name"] = entry.ZipName;
                jEntry["path"] = entry.FolderName;
                jEntry["active"] = entry.Active;
                arr.Add(jEntry);
            }

            obj["entries"] = arr;

            File.WriteAllText("data.json", obj.ToString());
        }

        private void setup()
        {
            if (File.Exists("data.json"))

                parseConf();
            else
                writeConf();
        }

        private void processInfoFile(string path, string name)
        {
            Console.Out.WriteLine("Processing ClientZips.txt");
            var fPath = path + "ClientZips.txt";
            if (!File.Exists(fPath))
            {
                Console.Out.WriteLine("Creating ClientZips.txt file");
                File.AppendAllText(fPath, name + "\n");
                Console.Out.WriteLine("Added " + name + " To the ClientZips.txt");
                return;
            }
            else
            {
                var content = readFile(fPath);
                if (!content.Contains(name))
                {
                    Console.Out.WriteLine("ClientZips.txt exists!");
                    Console.Out.WriteLine(name + ", is not in the ClientZips.txt, adding it");
                    File.AppendAllText(fPath, name + "\n");
                }
                else
                {
                    Console.Out.WriteLine(name + ", is already in the ClientZips.txt");
                }
            }

            return;
        }

        private string getTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private string readFile(string path)
        {
            try
            {
                var reader = new StreamReader(path);
                var content = reader.ReadToEnd();
                reader.Close();
                return content;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to read: " + path);

                throw;
            }
        }

        private void copyFiles(string inPath, string outPath)
        {
            foreach (string dirPath in Directory.GetDirectories(inPath, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(inPath, outPath));
            foreach (string newPath in Directory.GetFiles(inPath, "*.*",
                SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(inPath, outPath), true);
        }

        public bool findLol()
        {
            var isGarena = checkGarena();
            if (isGarena)
                Console.Out.WriteLine("INFO: Using Garena directory Patterns!");
            else
                Console.Out.WriteLine("INFO: Using Default directory Patterns!");


            var basePath = isGarena
                ? "C:/Garena/Games/32771/Game/"
                : "C:/Riot Games/League of Legends/RADS/solutions/lol_game_client_sln/releases/";
            Console.Out.WriteLine("Searching League of Legends");

            if (!isGarena &&
                File.Exists(
                    "C:ProgramData/Microsoft/Windows/Start Menu/Programs/League of Legends/League of Legends.lnk"))
            {
                var process = new System.Diagnostics.Process();
                var startInfo = new System.Diagnostics.ProcessStartInfo();

                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "Shortcut.exe";
                startInfo.RedirectStandardOutput = true;
                startInfo.Arguments =
                    "/A:Q /F:\"C:/ProgramData/Microsoft/Windows/Start Menu/Programs/League of Legends/League of Legends.lnk\"";
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
                string content = process.StandardOutput.ReadToEnd();

                var contained = false;
                foreach (var s in content.Split('\n'))
                {
                    if (s.Contains("WorkingDirectory"))
                    {
                        contained = true;
                        basePath = s.Split('=')[1].Replace("\n", "").Replace("\r", "").Replace("\\", "/") +
                                   "/RADS/solutions/lol_game_client_sln/releases/";
                        break;
                    }
                }
            }

            if (!isGarena)
            {
                if (!Directory.Exists(basePath))
                {
                    Console.Out.WriteLine("ERROR lol: Calculated Directory: " + basePath.Replace("\\", "/") +
                                          " does not exists, cant continue.");
                    return false;
                }

                DateTime lastEdited = DateTime.MinValue;
                DirectoryInfo final = null;
                foreach (var directory in Directory.GetDirectories(basePath))
                {
                    var info = new DirectoryInfo(directory);
                    if (lastEdited == DateTime.MinValue)
                    {
                        lastEdited = info.LastWriteTime;
                        final = info;
                    }
                    else
                    {
                        if (DateTime.Compare(lastEdited, info.LastWriteTime) < 0)
                        {
                            lastEdited = info.LastWriteTime;
                            final = info;
                        }
                    }
                }

                var finalPath = final.FullName.Replace("\\", "/") + "/deploy/";
                if (!Directory.Exists(finalPath))
                {
                    Console.Out.WriteLine("ERROR: Calculated Directory: " + finalPath.Replace("\\", "/") +
                                          " does not exists, cant continue.");

                    return false;
                }

                if (!File.Exists(finalPath + "League of Legends.exe") || !File.Exists(finalPath + "stub.dll"))
                {
                    Console.Out.WriteLine("1 ERROR: Calculated Directory: " + finalPath.Replace("\\", "/") +
                                          " is not a valid League Game Directory, cant continue.");

                    return false;
                }

                MessageBox.Show(
                    "Found League at: " + finalPath + ", if the path is wrong, edit it with File -> Set League path");
                this.LolPathValid = true;
                this.LolPath = finalPath;
                writeConf();
                return true;
            }


            if (!Directory.Exists(basePath))
            {
                Console.Out.WriteLine("ERROR: Calculated Directory: " + basePath.Replace("\\", "/") +
                                      " does not exists, cant continue.");
                return false;
            }

            if (!File.Exists(basePath + "/League of Legends.exe"))
            {
                Console.Out.WriteLine("2 ERROR: Calculated Directory: " + basePath.Replace("\\", "/") +
                                      " is not a valid League Game Directory, cant continue.");
                return false;
            }

            MessageBox.Show(
                "Found League at: " + basePath + ", if the path is wrong, edit it with File -> Set League path");
            this.LolPathValid = true;
            this.LolPath = basePath;
            writeConf();
            return true;
        }

        private bool checkGarena()
        {
            if (!File.Exists("garena.txt"))
            {
                File.WriteAllText("garena.txt", "false");
                return false;
            }

            var content = readFile("garena.txt");
            return content.Replace("\n", "").Trim() == "true";
        }
    }
}
