﻿using Microsoft.Win32;
using PdfiumViewer.Demo.Annotations;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PdfiumViewer.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Process CurrentProcess { get; }
        private CancellationTokenSource Cts { get; }
        private PdfMatches SearchMatches { get; set; }
        private System.Windows.Threading.DispatcherTimer MemoryChecker { get; }
        public string InfoText { get; set; }
        public string SearchTerm { get; set; }

        public double ZoomPercent
        {
            get => Renderer.Zoom * 100;
            set => Renderer.SetZoom(value / 100);
        }
        public bool IsSearchOpen { get; set; }
        public int SearchMatchItemNo { get; set; }
        public int SearchMatchesCount { get; set; }
        public int Page
        {
            get => Renderer.PageNo + 1;
            set => Renderer.PageNo = Math.Min(Math.Max(value - 1, 0), Renderer.PageCount - 1);
        }


        public MainWindow()
        {
            InitializeComponent();

            CurrentProcess = Process.GetCurrentProcess();
            Cts = new CancellationTokenSource();
            DataContext = this;
            Renderer.PropertyChanged += delegate { OnPropertyChanged(nameof(Page)); };

            MemoryChecker = new System.Windows.Threading.DispatcherTimer();
            MemoryChecker.Tick += OnMemoryChecker;
            MemoryChecker.Interval = new TimeSpan(0, 0, 1);
            MemoryChecker.Start();
        }

        private void OnMemoryChecker(object sender, EventArgs e)
        {
            CurrentProcess.Refresh();
            InfoText = $"Memory: {CurrentProcess.PrivateMemorySize64 / 1024 / 1024} MB";
            OnPropertyChanged(nameof(InfoText));
        }


        private async void RenderToMemory(object sender, RoutedEventArgs e)
        {
            try
            {
                var pageStep = Renderer.PagesDisplayMode == PdfViewerPagesDisplayMode.BookMode ? 2 : 1;
                Dispatcher.Invoke(() => Renderer.PageNo = 0);
                while (Renderer.PageNo < Renderer.PageCount - pageStep)
                {
                    Dispatcher.Invoke(() => Renderer.NextPage());
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                Cts.Cancel();
                Debug.Fail(ex.Message);
                MessageBox.Show(this, ex.Message, "Error!");
            }
        }

        private void OpenPdf(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Open PDF File"
            };

            if (dialog.ShowDialog() == true)
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                var mem = new MemoryStream(bytes);
                Renderer.OpenPdf(mem);
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            MemoryChecker?.Stop();
            Renderer?.Dispose();
        }

        private void OnPrevPageClick(object sender, RoutedEventArgs e)
        {
            Renderer.PreviousPage();
        }
        private void OnNextPageClick(object sender, RoutedEventArgs e)
        {
            Renderer.NextPage();
        }

        private void OnFitWidth(object sender, RoutedEventArgs e)
        {
            Renderer.SetZoomMode(PdfViewerZoomMode.FitWidth);
        }
        private void OnFitHeight(object sender, RoutedEventArgs e)
        {
            Renderer.SetZoomMode(PdfViewerZoomMode.FitHeight);
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            Renderer.ZoomIn();
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            Renderer.ZoomOut();
        }

        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            Renderer.Counterclockwise();
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            Renderer.ClockwiseRotate();
        }

        private void OnInfo(object sender, RoutedEventArgs e)
        {
            var info = Renderer.GetInformation();
            var sb = new StringBuilder();
            sb.AppendLine($"Author: {info.Author}");
            sb.AppendLine($"Creator: {info.Creator}");
            sb.AppendLine($"Keywords: {info.Keywords}");
            sb.AppendLine($"Producer: {info.Producer}");
            sb.AppendLine($"Subject: {info.Subject}");
            sb.AppendLine($"Title: {info.Title}");
            sb.AppendLine($"Create Date: {info.CreationDate}");
            sb.AppendLine($"Modified Date: {info.ModificationDate}");

            MessageBox.Show(sb.ToString(), "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnGetText(object sender, RoutedEventArgs e)
        {
            var txtViewer = new TextViewer();
            var page = Renderer.PageNo;
            txtViewer.Body = Renderer.GetPdfText(page);
            txtViewer.Caption = $"Page {page + 1} contains {txtViewer.Body.Length} character(s):";
            txtViewer.ShowDialog();
        }

        private void OnDisplayBookmarks(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnContinuousModeClick(object sender, RoutedEventArgs e)
        {
            Renderer.PagesDisplayMode = PdfViewerPagesDisplayMode.ContinuousMode;
        }

        private void OnBookModeClick(object sender, RoutedEventArgs e)
        {
            Renderer.PagesDisplayMode = PdfViewerPagesDisplayMode.BookMode;
        }

        private void OnSinglePageModeClick(object sender, RoutedEventArgs e)
        {
            Renderer.PagesDisplayMode = PdfViewerPagesDisplayMode.SinglePageMode;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private void OnTransparent(object sender, RoutedEventArgs e)
        {
            if ((Renderer.Flags & PdfRenderFlags.Transparent) != 0)
            {
                Renderer.Flags &= ~PdfRenderFlags.Transparent;
            }
            else
            {
                Renderer.Flags |= PdfRenderFlags.Transparent;
            }
        }

        private void OpenCloseSearch(object sender, RoutedEventArgs e)
        {
            IsSearchOpen = !IsSearchOpen;
            OnPropertyChanged(nameof(IsSearchOpen));
        }

        private void OnSearchTermKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search();
            }
        }

        private void Search()
        {
            var matchCase = MatchCaseCheckBox.IsChecked.GetValueOrDefault();
            var wholeWordOnly = WholeWordOnlyCheckBox.IsChecked.GetValueOrDefault();


            SearchMatches = Renderer.Search(SearchTerm, matchCase, wholeWordOnly);
            SearchMatchesCount = SearchMatches.Items.Count;
            SearchMatchesTextBlock.Visibility = Visibility.Visible;
            if (SearchMatches.Items.Any())
                DisplayTextSpan(SearchMatches.Items.First().TextSpan);
        }

        private void DisplayTextSpan(PdfTextSpan span)
        {
            Renderer.GotoPage(span.Page);
            Renderer.ScrollToVerticalOffset(span.Offset);
        }

        private void SaveAsImages(object sender, RoutedEventArgs e)
        {
            // Create a "Save As" dialog for selecting a directory (HACK)
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select a Directory",
                Filter = "Directory|*.this.directory",
                FileName = "select"
            };
            // instead of default "Save As"
            // Prevents displaying files
            // Filename will then be "select.this.directory"
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                // Remove fake filename from resulting path
                path = path.Replace("\\select.this.directory", "");
                path = path.Replace(".this.directory", "");
                // If user has changed the filename, create the new directory
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }
                // Our final value is in path
                SaveAsImages(path);
            }
        }

        private void SaveAsImages(string path)
        {
            try
            {
                for (var i = 0; i < Renderer.PageCount; i++)
                {
                    var size = Renderer.Document.PageSizes[i];
                    var image = Renderer.Document.Render(i, (int)size.Width * 5, (int)size.Height * 5, 300, 300, false);
                    image.Save(Path.Combine(path, $"img{i}.png"));
                }
            }
            catch (Exception ex)
            {
                Cts.Cancel();
                Debug.Fail(ex.Message);
                MessageBox.Show(this, ex.Message, "Error!");
            }
        }
    }
}
