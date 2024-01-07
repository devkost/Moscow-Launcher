using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Effects;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics.Eventing.Reader;
using System.Threading;

namespace LauncherMoscowRp
{
    public static class Settings
    {
        public static string GamePath = @"C:\Games\MOSCOWRP";
    }

    public partial class MainWindow : Window
    {
        ServerInfo serverInfo = new ServerInfo();
        Download download = new Download();
        private List<List<UIElement>> ServersUI;
        public MainWindow()
        {
            InitializeComponent();

            HeaderMove.MouseDown += delegate (object Sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); };
            download.onUpdateDownload += Event_OnUpdateDownload;
            download.onUpdateCompleted += Event_OnUpdateCompleted;

            if (File.Exists("settings"))
            {
                string[] settingsInFile = File.ReadAllLines("settings");
                if (settingsInFile.Length > 0) Settings.GamePath = settingsInFile[0];
            }
            GamePath.Text = Settings.GamePath;

            ServersUI = new List<List<UIElement>>
            {
                new List<UIElement>() { Server1, ServerName1, ServerProgress1, ServerPlayers1, ServerPlay1 },
            };
            UpdateServer( this, EventArgs.Empty );

            var timer = new DispatcherTimer();
            timer.Tick += new EventHandler( UpdateServer );
            timer.Interval = new TimeSpan(0, 0, 15);
            timer.Start();
        }

        private async void onButtonClick(object sender, EventArgs e)
        {
            if (sender == BtnClose)
            {
                Close();
            }
            else if (sender == BtnSettings)
            {
                if (SettingsWindow.IsVisible) SettingsWindow.Visibility = Visibility.Hidden;
                else SettingsWindow.Visibility = Visibility.Visible;
            }
            else if (sender == BtnDownload)
            {
                int isUpdate = download.IsUpdate;
                if (isUpdate == 0)
                {
                    BtnDownload.Content = "Отменить";
                    FileDownload.Visibility = Visibility.Visible;
                    FileSizes.Visibility = Visibility.Visible;
                    await download.Update();
                }
                else if (isUpdate == 1)
                {
                    download.CancelDownload();
                    FileDownload.Visibility = Visibility.Hidden;
                    FileSizes.Visibility = Visibility.Hidden;
                    BtnDownload.Content = "Начать играть";
                }
            }
        }

        private void onSettingsButtonClick(object sender, EventArgs e)
        {
            if (sender == BtnCloseSettings)
            {
                if (Settings.GamePath != GamePath.Text) Settings.GamePath = GamePath.Text;
                SettingsWindow.Visibility = Visibility.Hidden;
                SaveSettings();
            }
            else if (sender == BtnPathSettings)
            {
                var dialog = new VistaFolderBrowserDialog();
                dialog.Description = "Выберете каталог где будет находиться MOSCOWRP";
                dialog.UseDescriptionForTitle = true;
                bool result = (bool)dialog.ShowDialog();
                if (result)
                {
                    Settings.GamePath = dialog.SelectedPath;
                    GamePath.Text = dialog.SelectedPath;
                    SaveSettings();
                }

            }
        }

        //Saves
        private void SaveSettings()
        {
            File.WriteAllText("settings", Settings.GamePath);
        }

        //Updates
        void UpdateServer(object sender, EventArgs e)
        {
            for (int i = 0; i < ServersUI.Count; i++)
            {
                List<string> ServerList = serverInfo.GetServerIPList();
                var info = serverInfo.httpRequestss(ServerList[i]);
                Border border = (Border)ServersUI[i][0];
                if (!string.IsNullOrEmpty(info.Item1) && !string.IsNullOrEmpty(info.Item2))
                {
                    TextBlock serverName = (TextBlock)ServersUI[i][1];
                    Rectangle progress = (Rectangle)ServersUI[i][2];
                    TextBlock serverPlayers = (TextBlock)ServersUI[i][3];

                    border.Effect = null;
                    border.IsHitTestVisible = true;

                    serverName.Text = info.Item1;
                    progress.Width = (info.Item4 / info.Item3) * 100;
                    serverPlayers.Text = info.Item3.ToString();
                }
                else
                {
                    BlurEffect blurEffect = new BlurEffect();
                    blurEffect.Radius = 10;
                    border.Effect = blurEffect;

                    border.IsHitTestVisible = false;
                }

            }
        }

        private void Event_OnUpdateDownload( object sender, UpdateFilesArgs e )
        {
            //Dispatcher.Invoke(() => {
            //    FileDownload.Text = e.filestatus == 1 ? "Проверка файла: " + e.downloadfile : "Загрузка файла: " + e.downloadfile;
            //});

            Dispatcher.Invoke(() => {
                Console.WriteLine($"Dispatcher thread ID: {Thread.CurrentThread.ManagedThreadId}");
                UpdateUI(e);
            });

            FileSizes.Text = $"Размер файла: {((double)e.filesize / (1024 * 1024)).ToString("F2")} МБ";

            Download_Progress.Width = (double)(526 / 100 * e.percents);
        }

        private void Event_OnUpdateCompleted(object sender, EventArgs e)
        {
            BtnDownload.IsEnabled = true;
            BtnDownload.Content = "Начать играть";
            FileDownload.Visibility = Visibility.Hidden;
            FileSizes.Visibility = Visibility.Hidden;
            Download_Progress.Width = 0;
        }

        private void UpdateUI(UpdateFilesArgs e)
        {
            FileDownload.Text = e.filestatus == 0 ? "Проверка файла: " + e.downloadfile : "Загрузка файла: " + e.downloadfile;
        }
    }
}
