﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Certify.Config.Migration;
using Certify.Models;
using Certify.UI.ViewModel;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Certify.UI.Windows
{
    /// <summary>
    /// Interaction logic for ImportExport.xaml
    /// </summary>
    public partial class ImportExport
    {
        public Certify.UI.ViewModel.AppViewModel MainViewModel => ViewModel.AppViewModel.Current;

        public class ImportExportModel : BindableBase
        {
            public bool IsImportReady { get; set; } = false;
            public ManagedCertificateFilter Filter { get; set; } = new ManagedCertificateFilter { };
            public ImportSettings ImportSettings { get; set; } = new ImportSettings { };
            public ExportSettings ExportSettings { get; set; } = new ExportSettings { };
            public ImportExportPackage Package { get; set; } = null;

        }

        public ImportExportModel Model { get; set; } = new ImportExportModel();
        public ImportExport()
        {
            InitializeComponent();

            this.DataContext = Model;
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {

            var dialog = new OpenFileDialog();
            bool isPreview = true;

            // prompt user for save file location and perform export to json file

            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;

                var json = System.IO.File.ReadAllText(filePath);
                Model.Package = JsonConvert.DeserializeObject<ImportExportPackage>(json);

                var results = await MainViewModel.PerformSettingsImport(Model.Package, Model.ImportSettings, isPreview);
                PrepareImportSummary(isPreview, results);
            }
        }

        private void PrepareImportSummary(bool isPreview, List<ActionStep> results)
        {
            if (!isPreview && results.All(r => r.HasError == false))
            {
                (App.Current as App).ShowNotification("Import completed OK", App.NotificationType.Success);
            }

            this.PrepareImportPreview(Model.Package, results, isPreview ? "Import Preview" : "Import Results");

            if (results.All(r => r.HasError == false))
            {
                Model.IsImportReady = true;
            }
            else
            {
                Model.IsImportReady = false;
            }

        }

        private async void CompleteImport_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you wish to perform the import as shown in the preview? The import cannot be reverted once complete.", "Perform Import?", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
            {
                var results = await MainViewModel.PerformSettingsImport(Model.Package, Model.ImportSettings, false);

                PrepareImportSummary(false, results);
            }
        }

        private void PrepareImportPreview(ImportExportPackage package, List<ActionStep> steps, string title)
        {
            Markdig.MarkdownPipeline _markdownPipeline;
            string _css = "";


            var _markdownPipelineBuilder = new Markdig.MarkdownPipelineBuilder();
            _markdownPipelineBuilder.Extensions.Add(new Markdig.Extensions.Tables.PipeTableExtension());
            _markdownPipeline = _markdownPipelineBuilder.Build();

            try
            {
                var cssPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "CSS", "markdown.css");
                _css = System.IO.File.ReadAllText(cssPath);

                if (MainViewModel.UISettings?.UITheme?.ToLower() == "dark")
                {
                    cssPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "CSS", "dark-mode.css");
                    _css += System.IO.File.ReadAllText(cssPath);
                }
            }
            catch
            {

            }
            var intro = $"Importing from source: {package.SourceName} exported {package.ExportDate.ToLongDateString()} \r\n______";
            var markdown = GetStepsAsMarkdown(steps, title, intro);

            var result = Markdig.Markdown.ToHtml(markdown, _markdownPipeline);
            result = "<html><head><meta http-equiv='Content-Type' content='text/html;charset=UTF-8'><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />" +
                    "<style>" + _css + "</style></head><body>" + result + "</body></html>";


            MarkdownView.NavigateToString(result);
        }

        private string GetStepsAsMarkdown(IEnumerable<ActionStep> steps, string title, string intro)
        {
            //TODO: deduplicate this vs. Preview version
            var newLine = "\r\n";

            var sb = new StringBuilder();

            if (title != null)
            {
                sb.AppendLine("# " + title + newLine + "_______");
            }

            if (intro != null)
            {
                sb.AppendLine(intro);
            }

            foreach (var s in steps)
            {
                sb.AppendLine(newLine + "## " + s.Title);
                if (!string.IsNullOrEmpty(s.Description))
                {
                    sb.AppendLine(s.Description);
                }

                if (s.Substeps != null)
                {
                    foreach (var sub in s.Substeps)
                    {
                        if (!string.IsNullOrEmpty(sub.Description))
                        {
                            if (sub.Description.Contains("|"))
                            {
                                // table items
                                sb.AppendLine(sub.Description);
                            }
                            else if (sub.Description.StartsWith("\r\n"))
                            {
                                sb.AppendLine(sub.Description);
                            }
                            else
                            {
                                // list items
                                sb.AppendLine(" - " + sub.Description);
                            }
                        }
                        else
                        {
                            sb.AppendLine(" - " + sub.Title);
                        }
                    }
                }
            }
            return sb.ToString();
        }


        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var filter = new ManagedCertificateFilter { };
            var settings = new ExportSettings { };

            var dialog = new SaveFileDialog();

            // prompt user for save file location and perform export to json file
            dialog.FileName = $"certifytheweb_export_{DateTime.Now.ToString("yyyyMMdd")}.json";

            if (dialog.ShowDialog() == true)
            {
                var savePath = dialog.FileName;

                var export = await MainViewModel.GetSettingsExport(filter, settings, false);

                var json = JsonConvert.SerializeObject(export);
                System.IO.File.WriteAllText(savePath, json);

                (App.Current as App).ShowNotification("Export completed OK", App.NotificationType.Success);
            }

        }


    }
}