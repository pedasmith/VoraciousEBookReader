using SimpleEpubReader.Controls;
using SimpleEpubReader.Database;
using SimpleEpubReader.FileWizards;
using SimpleEpubReader.Searching;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace SimpleEpubReader
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {

        // // // Task WarmUpDataBase = null;
        public UserCustomization Customization { get; } = new UserCustomization();
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Logger.Log($"App:constructor:called");
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.UnhandledException += App_UnhandledException;
            Logger.Log($"App:constructor:done");
        }

        private async void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            var message = $"ERROR: something went wrong.\n{e.Message}";
            var md = new MessageDialog(message, "Error in Voracious Reader");
            await md.ShowAsync();
        }

        public static void Error (string str)
        {
            Logger.Log(str);
        }


        protected override async void OnFileActivated(FileActivatedEventArgs args)
        {
            Application.Current.Resources["UserCustomization"] = Customization;
            var mustCopy = InitializeFilesToGet.GetMustCopyInitialDatabase();
            var mainPageType = mustCopy ? typeof(MainInitializationPage) : typeof(MainPage);

            Frame rootFrame = CreateRootFrame();
            if (rootFrame.Content == null)
            {
                if (!rootFrame.Navigate(mainPageType))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            if (!mustCopy)
            {
                var p = rootFrame.Content as MainPage;
                if (p != null)
                {
                    await p.DoFilesActivated(args);
                }
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        private Frame CreateRootFrame()
        {
            Logger.Log($"App:CreateRootFrame:called");

            Frame rootFrame = Window.Current.Content as Frame;
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame.NavigationFailed += OnNavigationFailed;
                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            Logger.Log($"App:CreateRootFrame:done");
            return rootFrame;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            Logger.Log($"App:OnActivated:called");
            switch (args.Kind)
            {
                case ActivationKind.Protocol:
                    var paarg = args as ProtocolActivatedEventArgs;
                    var (bookId, bookLocation) = ProjectRomeActivity.ParseUrl(paarg.Uri);
                    var nav = Navigator.Get();
                    var bookdb = BookDataContext.Get();
                    var bookData = CommonQueries.BookGet(bookdb, bookId);
                    SavedActivatedBookData = bookData;
                    SavedActivatedBookLocation = bookLocation;
                    DoLaunch();
                    break;
                default:
                    base.OnActivated(args);
                    break;
            }
            Logger.Log($"App:OnActivated:done");
        }

        public static BookData SavedActivatedBookData = null;
        public static BookLocation SavedActivatedBookLocation = null;

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            DoLaunch(e);
        }

        private void DoLaunch(LaunchActivatedEventArgs e = null)
        {
            int nerror = 0;
            Logger.Log($"App:DoLaunch:about to test parser");
            nerror += SearchParserTest.Test_Parser(); // Run some tests
            Logger.Log($"App:DoLaunch:about to test html");
            nerror += EpubWizard.Test_HtmlStringIdIndexOf();
            nerror += BookMarkFile.TestAsValidFilename();

            Logger.Log($"App:DoLaunch:about to add to resources");

            // SLOW: this is 2 seconds of startup
            Application.Current.Resources["UserCustomization"] = Customization;

            Logger.Log($"App:DoLaunch:about to copy initial database");
            var mustCopy = InitializeFilesToGet.GetMustCopyInitialDatabase();
            var mainPageType = mustCopy ? typeof(MainInitializationPage) : typeof(MainPage);

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                Logger.Log($"App:DoLaunch:about to make frame");
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e != null && e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e == null || e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    Logger.Log($"App:DoLaunch:about to navigate");
                    rootFrame.Navigate(mainPageType, e?.Arguments);
                }
                // Ensure the current window is active
                Logger.Log($"App:DoLaunch:about to activate");
                Window.Current.Activate();
                Logger.Log($"App:DoLaunch:done to navigate and activate");
            }

            //if (WarmUpDataBase == null && !mustCopy)
            //{
            //    WarmUpDataBase = CommonQueries.FirstSearchToWarmUpDatabase();
            //}
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
