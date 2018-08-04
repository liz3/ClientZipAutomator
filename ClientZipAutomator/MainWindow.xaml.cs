using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Threading;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace ClientZipAutomator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Manager _manager;
        public readonly ProgressBar Bar;
        public readonly TextBlock Block;

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            InitializeComponent();


            this._manager = new Manager(this);



            this.Bar = progressBar;
            this.Block = updateStr;

            entriesGrid.ItemsSource = _manager.Entries;

            if (!_manager.LolPathValid && !_manager.findLol())
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog()
                {
                    ShowNewFolderButton = false
                };
                dialog.Description = "Select League Folder";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _manager.updateLolPath(dialog.SelectedPath);
                }

                dialog.Dispose();
            }


            if (_manager.LolPathValid)
                _manager.addExternalZips();
            else
            {
                MessageBox.Show(
                    "League path not Valid, ClientZipAutomator will exit now, restart and verify you select the select League Folder.");
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Error: " + ((Exception)e.ExceptionObject).ToString());
        }

        private void onDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dropPath = e.Data.GetData(DataFormats.FileDrop, true) as string[];

                disableButtons();

                BackgroundWorker backgroundWorker = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };

                backgroundWorker.ProgressChanged += (xxxx, args) =>
                {
                    progressBar.Value = args.ProgressPercentage;
                    if (args.UserState is string)
                    {
                        Block.Text = (string)args.UserState;
                    }
                };

                backgroundWorker.DoWork += (xx, xxxxxx) =>
                {
                    var progress = 0;
                    var am = 100 / dropPath.Length;

                    foreach (var s in dropPath)
                    {
                        if (Directory.Exists(s))
                        {
                            backgroundWorker.ReportProgress(progress, "Adding Folder: " + s);
                            _manager.addEntry(s);
                            progress += am;
                            backgroundWorker.ReportProgress(progress, "Adding Folder: " + s);
                        }
                        else if (File.Exists(s) && s.ToLower().EndsWith(".zip"))
                        {
                            backgroundWorker.ReportProgress(progress, "Adding Zip File: " + s);

                            _manager.addManagedZip(s, new FileInfo(s).Name);
                            progress += am;
                            backgroundWorker.ReportProgress(progress, "Adding Zip File: " + s);
                            
                        }
                    }
                };
                backgroundWorker.RunWorkerCompleted += (ssssss, args) =>
                {
                    _manager.writeConf();
                    Block.Text = "Ready";
                    progressBar.Value = 0;
                    enableButtons();
                    updateGrid();
                };
                backgroundWorker.RunWorkerAsync();
            }
        }

        private void entriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }


        private void addFolder_Click(object sender, RoutedEventArgs e)
        {
            disableButtons();

            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(
                () =>
                {
                    FolderBrowserDialog dialog = new FolderBrowserDialog()
                    {
                        ShowNewFolderButton = false
                    };
                    dialog.Description = "Select Folder to Add";
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _manager.addEntry(dialog.SelectedPath);
                    }

                    dialog.Dispose();
                    updateGrid();
                    enableButtons();
                })).Wait();
        }

        private void setLolPath_Click(object sender, RoutedEventArgs e)
        {
            disableButtons();


            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(
                () =>
                {
                    FolderBrowserDialog dialog = new FolderBrowserDialog()
                    {
                        ShowNewFolderButton = false
                    };
                    dialog.Description = "Select League Folder";
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _manager.updateLolPath(dialog.SelectedPath);
                    }

                    dialog.Dispose();
                    enableButtons();
                })).Wait();
        }

        public void updateGrid()
        {
            CollectionViewSource.GetDefaultView(entriesGrid.ItemsSource).Refresh();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var items = entriesGrid.SelectedItems.Cast<Entry>();

            if (items.Count() == 0) return;
            disableButtons();


            BackgroundWorker backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            backgroundWorker.ProgressChanged += (xxxx, args) =>
            {
                progressBar.Value = args.ProgressPercentage;
                if (args.UserState is string)
                {
                    Block.Text = (string)args.UserState;
                }
            };

            backgroundWorker.DoWork += (xx, xxxxxx) =>
            {
                var endPath = _manager.LolPath.EndsWith("/") ? _manager.LolPath : _manager.LolPath + "/";


                var progress = 0;
                var am = 100 / items.Count();
                foreach (var entry in items)
                {
                    backgroundWorker.ReportProgress(progress, "Deleting: " + entry.ZipName);
                    _manager.Entries.Remove(entry);
                    if (_manager.LolPathValid)
                    {
                        _manager.removeFromClientZip(endPath, entry.ZipName + "\n");
                        if (File.Exists(endPath + entry.ZipName))
                        {
                            backgroundWorker.ReportProgress(progress, "Deleting zip file: " + entry.ZipName);

                            File.Delete(endPath + entry.ZipName);
                        }
                    }

                    progress += am;
                    backgroundWorker.ReportProgress(progress, "Deleting: " + entry.ZipName);
                }

                _manager.writeConf();
            };

            backgroundWorker.RunWorkerCompleted += (ssssss, args) =>
            {
                if (items.Count() != 0) MessageBox.Show("Removed " + items.Count() + " Entries");

                Block.Text = "Ready";
                progressBar.Value = 0;
                enableButtons();
                updateGrid();
            };
            backgroundWorker.RunWorkerAsync();
        }

        public void disableButtons()
        {
            applyBtn.Content = "Loading...";
            clearBtn.Content = "Loading...";
            deleteBtn.Content = "Loading...";

            applyBtn.IsEnabled = false;
            clearBtn.IsEnabled = false;
            deleteBtn.IsEnabled = false;
            entriesGrid.IsEnabled = false;
        }

        public void enableButtons()
        {
            applyBtn.Content = "Apply";
            clearBtn.Content = "Clear Zips";
            deleteBtn.Content = "Delete";

            applyBtn.IsEnabled = true;
            clearBtn.IsEnabled = true;
            deleteBtn.IsEnabled = true;
            entriesGrid.IsEnabled = true;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _manager.applyEntries();
        }


        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            disableButtons();
            if (!_manager.LolPathValid) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(
                () =>
                {
                    var endPath = _manager.LolPath.EndsWith("/") ? _manager.LolPath : _manager.LolPath + "/";

                    foreach (var entry in _manager.Entries)
                    {
                        _manager.removeFromClientZip(endPath, entry.ZipName + "\n");
                        if (File.Exists(endPath + entry.ZipName))
                        {
                            File.Delete(endPath + entry.ZipName);
                        }
                    }

                    enableButtons();
                })).Wait();
        }

        private void open_league_dir(object sender, RoutedEventArgs e)
        {
            if (_manager.LolPathValid) Process.Start(_manager.LolPath);
        }

        private void open_managed(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists("managed")) Process.Start(new DirectoryInfo("managed").FullName);
            
        }
    }
}
