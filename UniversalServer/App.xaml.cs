using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace UniversalServer
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                // Versuch, MaterialDesign-Ressourcen zur Laufzeit zu laden. Falls das Paket fehlt,
                // wird die Anwendung mit den eigenen Fallback-Styles weiter gestartet.
                var mdLight = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml", UriKind.Absolute)
                };
                var mdDefaults = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml", UriKind.Absolute)
                };

                // MaterialDesign-Ressourcen vorne einfügen, damit sie Fallbacks überschreiben
                this.Resources.MergedDictionaries.Insert(0, mdDefaults);
                this.Resources.MergedDictionaries.Insert(0, mdLight);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MaterialDesign resource load failed: " + ex.Message);
            }
        }
    }
}
