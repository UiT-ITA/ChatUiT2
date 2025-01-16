using MudBlazor;

namespace UiT.RagProjects.Services;

public class MyUIManager
{
    public MyUIManager(IConfiguration config)
    {
        try
        {
            AppTitle = config?.GetSection("AppVariables")?.GetValue<string>("AppTitle") ?? "Feil oppstod";
        }
        catch { }
        MyTheme = GetTheme;
    }

    public event EventHandler NotifyPersonSensitiveComponents;
        
    public string AppTitle { get; private set; } = "NOT SET";

    // Default theme: https://mudblazor.com/customization/default-theme#mudtheme
    public bool IsDarkTheme { get; set; } = false;
    public bool HidePersonSentive { get; set; } = false;

    public void SwitchTheme()
    {
        IsDarkTheme = !IsDarkTheme;        
    }
    public void SwitchPersonSensitiveMode()
    {
        HidePersonSentive = !HidePersonSentive;
        
        Console.WriteLine($"PersonSensitive: {HidePersonSentive}");
        NotifyPersonSensitiveComponents?.Invoke(this, EventArgs.Empty);
    }


    public MudTheme MyTheme { get; private set; }

    #region LightTheming
    private readonly MudTheme GetTheme = new()
    {
        PaletteLight = new PaletteLight()
        {
            //    Black                       = "#272c34ff",
            //    BackgroundGrey              = "#27272f",
            //    Surface                     = "#ffffff",
            //    Background                  = "#faf9f8",
            //    DrawerText                  = "rgba(44,43,42,255)",
            //    DrawerBackground            = "#f0f0f0",
            //    DrawerIcon                  = "rgba(0,0,0, 0.50)",
            LinesInputs         = "rgba(200,200,200, 0.32)",        // Linjer i tekstbokser
            AppbarBackground    = "#043348",                        // App bar
            AppbarText          = "rgba(255,255,255, 0.70)",        // App bar text
            TextPrimary         = "rgba(0,0,0, 0.90)",
            TextSecondary       = "rgba(190,190,190, 0.50)",
            //    TextSecondary               = "rgba(60,60,60, 0.50)",           // f.eks. inne i tekstbokser
            //    ActionDefault               = "#adadb1",
            //    ActionDisabled              = "rgba(255,255,255, 0.26)",
            //    ActionDisabledBackground    = "rgba(255,255,255, 0.12)"
            Success = "#4caf50"
        },

        PaletteDark = new PaletteDark()
        {
            //    Black                       = "#272c34ff",
            //    BackgroundGrey              = "#27272f",
            //    Surface                     = "#ffffff",
            //    Background                  = "#faf9f8",
            //    DrawerText                  = "rgba(44,43,42,255)",
            DrawerBackground            = "#373740",
            //    DrawerIcon                  = "rgba(0,0,0, 0.50)",
            LinesInputs                 = "rgba(20,20,20, 0.32)",        // Linjer i tekstbokser
            AppbarBackground            = "#333333",                        // App bar
            AppbarText                  = "rgba(50,50,50, 0.70)",        // App bar text
            TextPrimary                 = "rgba(170,170,170, 0.90)",
            TextSecondary               = "rgba(190,190,190, 0.50)",
            // App bar text
            //    ActionDefault               = "#adadb1",
            //    ActionDisabled              = "rgba(255,255,255, 0.26)",
            //    ActionDisabledBackground    = "rgba(255,255,255, 0.12)"
            Success                     = "#344234"
            
        },

        Shadows = new Shadow(),
        ZIndex = new ZIndex(),
        LayoutProperties = new LayoutProperties()
        {
            DrawerWidthLeft             = "250px",      // Størrelse på meny
            DrawerWidthRight            = "1100px",     // Størrelse på drawer til høyre
            AppbarHeight                = "48px"       // Høyden på logolinjen på topp
        },

        Typography = new Typography()
        {
            Default = new Default() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "0.875rem", FontWeight = 400, LineHeight = 1.430, LetterSpacing = ".01071em", TextTransform = "none" },
            H1 = new H1() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "2.0rem", FontWeight = 300, LineHeight = 1.15, LetterSpacing = "-.01562em", TextTransform = "none" },
            H2 = new H2() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1.7rem", FontWeight = 300, LineHeight = 1.15, LetterSpacing = "-.00833em", TextTransform = "none" },
            H3 = new H3() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1.5rem", FontWeight = 300, LineHeight = 1.15, LetterSpacing = "0", TextTransform = "none" },
            H4 = new H4() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1.3rem", FontWeight = 300, LineHeight = 1.15, LetterSpacing = ".0075em", TextTransform = "none" },
            H5 = new H5() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1.1rem", FontWeight = 300, LineHeight = 1.15, LetterSpacing = "0", TextTransform = "none" },
            H6 = new H6() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1.0rem", FontWeight = 300, LineHeight = 1.15, LetterSpacing = ".0075em", TextTransform = "none" },
            Subtitle1 = new Subtitle1() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1rem", FontWeight = 400, LineHeight = 1.750, LetterSpacing = ".00938em", TextTransform = "none" },
            Subtitle2 = new Subtitle2() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = ".875rem", FontWeight = 500, LineHeight = 1.570, LetterSpacing = ".01em", TextTransform = "none" },
            Body1 = new Body1() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "0.9rem", FontWeight = 400, LineHeight = 1.500, LetterSpacing = ".00938em", TextTransform = "none" },
            Body2 = new Body2() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = ".75rem", FontWeight = 400, LineHeight = 1.750, LetterSpacing = ".01071em", TextTransform = "none" },
            Button = new Button() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = "1rem", FontWeight = 500, LineHeight = 1.750, LetterSpacing = ".02857em", TextTransform = "none" },
            Caption = new Caption() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = ".75rem", FontWeight = 400, LineHeight = 1.660, LetterSpacing = ".03333em", TextTransform = "none" },
            Overline = new Overline() { FontFamily = ["Open Sans", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = ".75rem", FontWeight = 400, LineHeight = 2.660, LetterSpacing = ".08333em", TextTransform = "none" }
        }
    };
    #endregion    
}
