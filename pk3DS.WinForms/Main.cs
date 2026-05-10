/*----------------------------------------------------------------------------*/
/*--  This program is free software: you can redistribute it and/or modify  --*/
/*--  it under the terms of the GNU General Public License as published by  --*/
/*--  the Free Software Foundation, either version 3 of the License, or     --*/
/*--  (at your option) any later version.                                   --*/
/*--                                                                        --*/
/*--  This program is distributed in the hope that it will be useful,       --*/
/*--  but WITHOUT ANY WARRANTY; without even the implied warranty of        --*/
/*--  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the          --*/
/*--  GNU General Public License for more details.                          --*/
/*--                                                                        --*/
/*--  You should have received a copy of the GNU General Public License     --*/
/*--  along with this program. If not, see <http://www.gnu.org/licenses/>.  --*/
/*----------------------------------------------------------------------------*/

using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures.PersonalInfo;
using System.Drawing;
using System.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace pk3DS.WinForms;

    public sealed partial class Main : Form
{
    private System.Windows.Forms.PictureBox PB_Sprite = new System.Windows.Forms.PictureBox();
    private System.Windows.Forms.Label L_MascotQuote = new System.Windows.Forms.Label();
    private System.Windows.Forms.PictureBox PB_GameIcon = new System.Windows.Forms.PictureBox();
    private System.Windows.Forms.Label L_Version = new System.Windows.Forms.Label();
    private System.Windows.Forms.Label L_MascotThought = new System.Windows.Forms.Label();
    private System.Windows.Forms.PictureBox PB_Friendship = new System.Windows.Forms.PictureBox();
    private System.Windows.Forms.Panel PNL_MascotGlass = new System.Windows.Forms.Panel();
    private System.Windows.Forms.Panel PNL_Sidebar = new System.Windows.Forms.Panel();
    private System.Windows.Forms.Button B_Store = new System.Windows.Forms.Button();
    private Color GradientStart = Color.FromArgb(45, 25, 60);
    private Color GradientEnd = Color.FromArgb(30, 30, 30);
    public static Main Instance;
    public Main()
    {
        Instance = this;
        // Initialize the Main Form
        InitializeComponent();
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.Paint += new PaintEventHandler(Main_Paint);
        ShowdownSetManager.Load();

        // Prepare DragDrop Functionality
        AllowDrop = TB_Path.AllowDrop = true;
        DragEnter += TabMain_DragEnter;
        DragDrop += TabMain_DragDrop;
        TB_Path.DragEnter += TabMain_DragEnter;
        TB_Path.DragDrop += TabMain_DragDrop;
        foreach (var t in TC_RomFS.TabPages.OfType<TabPage>())
        {
            t.AllowDrop = true;
            t.DragEnter += TabMain_DragEnter;
            t.DragDrop += TabMain_DragDrop;
        }

        // Reload Previous Editing Files if the file exists
        var settings = Properties.Settings.Default;
        if (CB_Lang.Items.Count == 0)
        {
            CB_Lang.Items.AddRange(new object[] { "日本語", "English", "Français", "Italiano", "Deutsch", "Español", "中文", "한국어", "Dutch", "Portuguese", "Russian", "Traditional Chinese" });
        }
        if (settings.Language >= 0 && settings.Language < CB_Lang.Items.Count)
            CB_Lang.SelectedIndex = settings.Language;

        var path = settings.GamePath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                OpenQuick(path);
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error($"Unable to automatically load the previously opened ROM dump located at -- {path}.", ex.Message);
                ResetStatus();
            }
        }

        string[] args = Environment.GetCommandLineArgs();
        string filename = args.Length > 0 ? Path.GetFileNameWithoutExtension(args[0]).ToLower() : "";
        skipBoth = filename.Contains("3DSkip");

        const string randset = RandSettings.FileName;
        if (File.Exists(randset))
            RandSettings.Load(File.ReadAllLines(randset));
        else
        {
            string defaultRand = WinFormsUtil.GetInternalText("randsettings.txt");
            if (!string.IsNullOrEmpty(defaultRand))
                RandSettings.Load(defaultRand.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
        }

        WinFormsUtil.ApplyTheme(this);
        WinFormsUtil.SetDoubleBuffered(TC_RomFS);
        
        // Add Toggle to Options
        var themeToggle = new ToolStripMenuItem("Visual Mode");
        var darkItem = new ToolStripMenuItem("Dark") { CheckOnClick = true, Checked = WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.Dark };
        var greyItem = new ToolStripMenuItem("Grey") { CheckOnClick = true, Checked = WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.Grey };
        
        darkItem.Click += (s, e) => { WinFormsUtil.CurrentTheme = WinFormsUtil.VisualTheme.Dark; greyItem.Checked = false; WinFormsUtil.RefreshAllThemes(); };
        greyItem.Click += (s, e) => { WinFormsUtil.CurrentTheme = WinFormsUtil.VisualTheme.Grey; darkItem.Checked = false; WinFormsUtil.RefreshAllThemes(); };
        
        themeToggle.DropDownItems.Add(darkItem);
        themeToggle.DropDownItems.Add(greyItem);
        Menu_Options.DropDownItems.Add(themeToggle);

        LoadQuotes();
        InitializeMascotUI();
        UpdateMascot();
        AddThemeMenu();
    }

    private void InitializeMascotUI()
    {
        // Sidebar Panel - Narrower and Transparent
        this.PNL_Sidebar.Size = new System.Drawing.Size(180, 420);
        this.PNL_Sidebar.Dock = System.Windows.Forms.DockStyle.Right;
        this.PNL_Sidebar.BackColor = System.Drawing.Color.Transparent; this.PNL_Sidebar.Name = "PNL_Sidebar";

        // ----- MASCOT PictureBox — parented to PNL_Sidebar -----
        this.PB_Sprite.Size = new System.Drawing.Size(160, 160);
        this.PB_Sprite.Location = new System.Drawing.Point(10, 10);
        this.PB_Sprite.SizeMode = PictureBoxSizeMode.Zoom;
        this.PB_Sprite.BackColor = Color.Transparent;
        this.PB_Sprite.TabIndex = 0;
        this.PB_Sprite.Visible = true;
        this.PB_Sprite.Click += new System.EventHandler(this.PB_Sprite_Click);
        this.PNL_Sidebar.Controls.Add(this.PB_Sprite);

        // Load a default mascot sprite immediately
        try { 
            object obj = pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject("_800");
            if (obj is Bitmap img) 
            {
                this.PB_Sprite.Image = WinFormsUtil.ScaleImage(img, 2);
                this.PB_Sprite.SizeMode = PictureBoxSizeMode.Zoom;
            }
        }
        catch { }

        // Thought bubble (top of sidebar, above mascot) - HIDDEN
        this.PNL_MascotGlass.Visible = false;
        this.PNL_MascotGlass.Location = new System.Drawing.Point(5, 5);
        this.PNL_MascotGlass.Size = new System.Drawing.Size(170, 36);
        this.PNL_Sidebar.Controls.Add(this.PNL_MascotGlass);

        this.L_MascotThought.Dock = System.Windows.Forms.DockStyle.Fill;
        this.L_MascotThought.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
        this.L_MascotThought.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this.L_MascotThought.ForeColor = System.Drawing.Color.White;
        this.L_MascotThought.BackColor = System.Drawing.Color.Transparent;

        // Path label for the top area
        var L_PathLabel = new Label { Text = "Path:", Location = new Point(12, 30), AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent };
        this.Controls.Add(L_PathLabel);
        L_PathLabel.BringToFront();

        // Friendship heart - moved to left as requested
        this.PB_Friendship.Location = new System.Drawing.Point(10, 182);
        this.PB_Friendship.Size = new System.Drawing.Size(24, 24);
        this.PB_Friendship.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_Friendship.BackColor = System.Drawing.Color.Transparent;
        this.PNL_Sidebar.Controls.Add(this.PB_Friendship);
        this.PB_Friendship.BringToFront();
        
        // Quote below the mascot - moved UP
        this.L_MascotQuote.Location = new System.Drawing.Point(5, 210);
        this.L_MascotQuote.Size = new System.Drawing.Size(170, 85);
        this.L_MascotQuote.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular);
        this.L_MascotQuote.TextAlign = System.Drawing.ContentAlignment.TopCenter;
        this.L_MascotQuote.ForeColor = System.Drawing.Color.White;
        this.L_MascotQuote.BackColor = System.Drawing.Color.FromArgb(120, 0, 0, 0); // Semi-transparent black rectangle
        this.L_MascotQuote.Padding = new Padding(5);
        this.L_MascotQuote.BorderStyle = BorderStyle.FixedSingle;
        this.PNL_Sidebar.Controls.Add(this.L_MascotQuote);

        // Game icon + version labels (anchored to bottom of sidebar)
        this.PB_GameIcon.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.PB_GameIcon.Location = new System.Drawing.Point(8, 315);
        this.PB_GameIcon.Size = new System.Drawing.Size(32, 32);
        this.PB_GameIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.PB_GameIcon.BackColor = System.Drawing.Color.Transparent;
        this.PNL_Sidebar.Controls.Add(this.PB_GameIcon);
 
        this.L_Version.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.L_Version.AutoSize = true;
        this.L_Version.Font = new System.Drawing.Font("Segoe UI", 8F);
        this.L_Version.ForeColor = System.Drawing.Color.LightGray;
        this.L_Version.BackColor = System.Drawing.Color.Transparent;
        this.L_Version.Location = new System.Drawing.Point(45, 319);
        this.L_Version.Text = "";
        this.PNL_Sidebar.Controls.Add(this.L_Version);
 
        this.L_Game.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.L_Game.AutoSize = true;
        this.L_Game.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.L_Game.Location = new System.Drawing.Point(45, 334);
        this.L_Game.Size = new System.Drawing.Size(150, 20);
        this.L_Game.ForeColor = System.Drawing.Color.White;
        this.L_Game.BackColor = System.Drawing.Color.Transparent;
        this.PNL_Sidebar.Controls.Add(this.L_Game);

        // Store button - moved UP
        this.B_Store.AutoSize = false;
        this.B_Store.Text = "Store";
        this.B_Store.Location = new System.Drawing.Point(85, 180);
        this.B_Store.Size = new System.Drawing.Size(80, 26);
        this.B_Store.FlatStyle = FlatStyle.Flat;
        this.B_Store.ForeColor = Color.White;
        this.B_Store.BackColor = Color.FromArgb(80, 0, 0, 0);
        this.B_Store.FlatAppearance.BorderSize = 1;
        this.B_Store.Click += new System.EventHandler(this.B_Store_Click);
        this.PNL_Sidebar.Controls.Add(this.B_Store);

        // Add sidebar to form — must come LAST so Dock=Right is calculated correctly
        if (!this.Controls.Contains(this.PNL_Sidebar)) this.Controls.Add(this.PNL_Sidebar);
        this.PNL_Sidebar.Visible = true;
        this.PNL_Sidebar.BringToFront();
        this.PB_Sprite.BringToFront();
        this.PB_Sprite.Click += Mascot_Click;
        SetMascotQuote();

        // Expand window to fit content + sidebar
        this.ClientSize = new System.Drawing.Size(800, 450);
        this.MinimumSize = new System.Drawing.Size(800, 490);
        this.MinimizeBox = true;
        this.MaximizeBox = false;
    }

    private void Main_Paint(object sender, PaintEventArgs e)
    {
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(this.ClientRectangle, GradientStart, GradientEnd, 90F))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
    }

    private void AddThemeMenu()
    {
        var themeMenu = new ToolStripMenuItem("Visual Theme");
        foreach (var theme in new[] { "Xerneas", "Yveltal", "Groudon", "Kyogre", "Solgaleo", "Lunala", "Necrozma", "Dusk Mane Necrozma", "Dawn Wings Necrozma", "Ultra Necrozma", "Rayquaza", "Deoxys", "Zygarde", "Magearna", "Zeraora", "Marshadow", "Incineroar", "Decidueye", "Primarina" })
        {
            var item = new ToolStripMenuItem(theme);
            item.Click += (s, e) => {
                Properties.Settings.Default.CustomTheme = theme;
                Properties.Settings.Default.Save();
                UpdateMascot(); // This will now update GradientStart and Sprite
                this.Invalidate();
            };
            themeMenu.DropDownItems.Add(item);
        }
        Menu_Options.DropDownItems.Add(themeMenu);
        
        string savedTheme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(savedTheme))
            UpdateMascot();
    }

    private string[] Quotes;
    private void LoadQuotes()
    {
        try {
            string data = WinFormsUtil.GetInternalText("quotes.txt");
            if (string.IsNullOrEmpty(data)) throw new Exception();
            Quotes = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        } catch {
            Quotes = ["Keep up the good work!", "You're doing great!", "Let's make a great game!"];
        }
    }

    private int Friendship
    {
        get => Properties.Settings.Default.MascotFriendship;
        set { Properties.Settings.Default.MascotFriendship = value; Properties.Settings.Default.Save(); UpdateFriendshipUI(); }
    }

    private void Mascot_Click(object sender, EventArgs e)
    {
        Friendship += 1;
        SetMascotQuote();
    }

    private void SetMascotQuote()
    {
        if (Quotes == null || Quotes.Length == 0) LoadQuotes();
        string quote = Quotes[new Random().Next(Quotes.Length)];
        string name = GetMascotName();
        L_MascotQuote.Text = quote.Replace("[PokemonName]", name);
    }

    private string GetMascotName()
    {
        string theme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(theme))
        {
            if (theme == "Ultra Necrozma") return "Ultra Necrozma";
            return theme;
        }

        if (Config == null) return "Necrozma";
        if (Config.X) return "Xerneas";
        if (Config.Y) return "Yveltal";
        if (Config.AS) return "Kyogre";
        if (Config.OR) return "Groudon";
        if (Config.Sun) return "Solgaleo";
        if (Config.Moon) return "Lunala";
        if (Config.UltraSun) return "Necrozma";
        if (Config.UltraMoon) return "Necrozma";
        return "Mascot";
    }

    private void UpdateFriendshipUI()
    {
        if (Friendship >= 255) PB_Friendship.Image = WinFormsUtil.GetFriendshipIcon(3); // Max
        else if (Friendship >= 150) PB_Friendship.Image = WinFormsUtil.GetFriendshipIcon(2); // Mid
        else if (Friendship >= 50) PB_Friendship.Image = WinFormsUtil.GetFriendshipIcon(1); // Low
        else PB_Friendship.Image = WinFormsUtil.GetFriendshipIcon(0); // None
    }

    public void HandleFriendship(int points)
    {
        int multiplier = 1;
        var items = Properties.Settings.Default.MascotItems ?? "";
        if (items.Contains("Big Root") || items.Contains("Leftovers"))
            multiplier = 2;

        Friendship = Math.Min(255, Friendship + (points * multiplier));
        UpdateFriendshipUI();
    }

    private string currentPath;
    private void OnPathChanged()
    {
        if (currentPath == TB_Path.Text) return;
        currentPath = TB_Path.Text;
        // Reset session-based perks if game path changes
        Properties.Settings.Default.MascotItems = ""; 
        Properties.Settings.Default.Save();
        UpdateMascot();
    }

    private void UpdateMascot()
    {
        // Default Gradient
        GradientStart = Color.FromArgb(45, 25, 60);
        GradientEnd = Color.FromArgb(30, 30, 30);
        int species = 800;
        int form = 0;

        // Apply Custom Theme Override if present
        string customTheme = Properties.Settings.Default.CustomTheme;
        if (!string.IsNullOrEmpty(customTheme))
        {
            switch (customTheme)
            {
                case "Xerneas": species = 716; GradientStart = ColorTranslator.FromHtml("#4C69A2"); break;
                case "Yveltal": species = 717; GradientStart = ColorTranslator.FromHtml("#A4101B"); break;
                case "Groudon": species = 383; GradientStart = ColorTranslator.FromHtml("#DA1B22"); break;
                case "Kyogre": species = 382; GradientStart = ColorTranslator.FromHtml("#3A16A9"); break;
                case "Solgaleo": species = 791; GradientStart = ColorTranslator.FromHtml("#FF6A01"); break;
                case "Lunala": species = 792; GradientStart = ColorTranslator.FromHtml("#6B30C9"); break;
                case "Necrozma": species = 800; form = 0; GradientStart = ColorTranslator.FromHtml("#4A4B56"); break;
                case "Dusk Mane Necrozma": species = 800; form = 1; GradientStart = ColorTranslator.FromHtml("#F5E9D0"); break;
                case "Dawn Wings Necrozma": species = 800; form = 2; GradientStart = ColorTranslator.FromHtml("#B2DAE2"); break;
                case "Ultra Necrozma": species = 800; form = 3; GradientStart = ColorTranslator.FromHtml("#FFF79F"); break;
                case "Rayquaza": species = 384; GradientStart = ColorTranslator.FromHtml("#1F8464"); break;
                case "Deoxys": species = 386; GradientStart = ColorTranslator.FromHtml("#E7935D"); break;
                case "Zygarde": species = 718; form = 1; GradientStart = ColorTranslator.FromHtml("#353535"); break;
                case "Magearna": species = 801; GradientStart = ColorTranslator.FromHtml("#D3B6B9"); break;
                case "Zeraora": species = 807; GradientStart = ColorTranslator.FromHtml("#F6D035"); break;
                case "Marshadow": species = 802; GradientStart = ColorTranslator.FromHtml("#4A4B56"); break;
                case "Incineroar": species = 727; GradientStart = ColorTranslator.FromHtml("#CC2121"); break;
                case "Decidueye": species = 724; GradientStart = ColorTranslator.FromHtml("#155C41"); break;
                case "Primarina": species = 730; GradientStart = ColorTranslator.FromHtml("#54B3D4"); break;
            }
        }
        else // Fallback to Game-based detection
        {
            string path = (RomFSPath ?? TB_Path.Text).ToLower();
            if (path.Contains("ultra sun") || path.Contains("ultrasun") || path.Contains("usum")) { species = 800; form = 1; GradientStart = ColorTranslator.FromHtml("#F5E9D0"); }
            else if (path.Contains("ultra moon") || path.Contains("ultramoon")) { species = 800; form = 2; GradientStart = ColorTranslator.FromHtml("#B2DAE2"); }
            else if (path.Contains("omega ruby") || path.Contains("oras")) { species = 383; GradientStart = ColorTranslator.FromHtml("#DA1B22"); }
            else if (path.Contains("alpha sapphire")) { species = 382; GradientStart = ColorTranslator.FromHtml("#3A16A9"); }
            else if (path.Contains("sun")) { species = 791; GradientStart = ColorTranslator.FromHtml("#FF6A01"); }
            else if (path.Contains("moon")) { species = 792; GradientStart = ColorTranslator.FromHtml("#6B30C9"); }
            else if (path.Contains("pokemon x") || path.EndsWith(" x")) { species = 716; GradientStart = ColorTranslator.FromHtml("#4C69A2"); }
            else if (path.Contains("pokemon y") || path.EndsWith(" y")) { species = 717; GradientStart = ColorTranslator.FromHtml("#A4101B"); }
            else if (Config != null)
            {
                if (Config.X) { species = 716; GradientStart = ColorTranslator.FromHtml("#4C69A2"); }
                else if (Config.Y) { species = 717; GradientStart = ColorTranslator.FromHtml("#A4101B"); }
                else if (Config.AS) { species = 382; GradientStart = ColorTranslator.FromHtml("#3A16A9"); }
                else if (Config.OR) { species = 383; GradientStart = ColorTranslator.FromHtml("#DA1B22"); }
                else if (Config.Sun) { species = 791; GradientStart = ColorTranslator.FromHtml("#FF6A01"); }
                else if (Config.Moon) { species = 792; GradientStart = ColorTranslator.FromHtml("#6B30C9"); }
                else if (Config.UltraSun) { species = 800; form = 1; GradientStart = ColorTranslator.FromHtml("#F5E9D0"); }
                else if (Config.UltraMoon) { species = 800; form = 2; GradientStart = ColorTranslator.FromHtml("#B2DAE2"); }
                else { species = 800; }
            }
            else { species = 800; }
        }

        // Item-based Form Overrides
        var ownedItemsList = (Properties.Settings.Default.MascotItems ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (species == 383 && ownedItemsList.Contains("Red Orb")) form = 1; // Primal Groudon
        if (species == 382 && ownedItemsList.Contains("Blue Orb")) form = 1; // Primal Kyogre
        if (species == 384 && ownedItemsList.Contains("Meteorite")) form = 1; // Mega Rayquaza
        if (species == 718 && ownedItemsList.Contains("Leftovers")) form = 4; // Zygarde Complete
        if (species == 386) form = DeoxysForm % 4; // Use persistent Deoxys form counter


        // Special fallback for alternate forms of Necrozma if they don't load from GARC
        Bitmap sprite = (Bitmap)WinFormsUtil.GetSprite(species, form, 0, 0, Config);
        if (sprite == null)
        {
            string resName = $"_{species}";
            if (form > 0) resName += $"_{form}";
            object obj = pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject(resName);
            if (obj is Bitmap resImg) sprite = resImg;
            else if (obj == null) { // Definitive fallback
                obj = pk3DS.WinForms.Properties.Resources.ResourceManager.GetObject("_800");
                if (obj is Bitmap defImg) sprite = defImg;
            }
        }

        if (sprite != null)
            PB_Sprite.Image = WinFormsUtil.ScaleImage(sprite, 2);
        
        this.Invalidate(); // Redraw with new gradient
        
        PB_Sprite.Visible = true;
        PB_Sprite.BringToFront();
        PNL_Sidebar.Visible = true;
        PNL_Sidebar.BringToFront();
        
        // Add quote text randomly
        SetMascotQuote();
        UpdateFriendshipUI();
        
        string themeName = GetThemeForGame();
        if (species == 800 && form == 3) themeName = "Ultra Necrozma";
        WinFormsUtil.ApplyGradient(this, themeName);
        
        // Update Game Info
        if (Config != null)
        {
            L_Game.Text = Config.X ? "Pokémon X" : Config.Y ? "Pokémon Y" : Config.OR ? "Pokémon Omega Ruby" : Config.AS ? "Pokémon Alpha Sapphire" : Config.Sun ? "Pokémon Sun" : Config.Moon ? "Pokémon Moon" : Config.UltraSun ? "Pokémon Ultra Sun" : Config.UltraMoon ? "Pokémon Ultra Moon" : "Pokémon Game";
            L_Version.Text = $"v{Config.Version}";
            if (SMDH != null) PB_GameIcon.Image = SMDH.LargeIcon.Icon;
        }
        else
        {
            L_Game.Text = "No Game Loaded";
            L_Version.Text = "";
            PB_GameIcon.Image = null;
        }
    }

    private string GetThemeForGame()
    {
        string path = (RomFSPath ?? TB_Path.Text).ToLower();
        if (path.Contains("ultra sun") || path.Contains("ultrasun") || path.Contains("usum")) return "Dusk Mane Necrozma";
        if (path.Contains("ultra moon") || path.Contains("ultramoon")) return "Dawn Wings Necrozma";
        if (path.Contains("sun")) return "Solgaleo";
        if (path.Contains("moon")) return "Lunala";
        if (path.Contains("omega ruby") || path.Contains("oras")) return "Groudon";
        if (path.Contains("alpha sapphire")) return "Kyogre";
        if (path.Contains("pokemon x") || path.EndsWith(" x")) return "Xerneas";
        if (path.Contains("pokemon y") || path.EndsWith(" y")) return "Yveltal";

        if (Config != null)
        {
            if (Config.X) return "Xerneas";
            if (Config.Y) return "Yveltal";
            if (Config.AS) return "Kyogre";
            if (Config.OR) return "Groudon";
            if (Config.Sun) return "Solgaleo";
            if (Config.Moon) return "Lunala";
            if (Config.UltraSun) return "Dusk Mane Necrozma";
            if (Config.UltraMoon) return "Dawn Wings Necrozma";
        }
        return "Necrozma";
    }

    private static int DeoxysForm = 0;
    private void PB_Sprite_Click(object sender, EventArgs e)
    {
        var items = Properties.Settings.Default.MascotItems ?? "";
        if (items.Contains("Meteorite") && PB_Sprite.Image != null)
        {
            // Specifically handling Deoxys form loop
            string customTheme = Properties.Settings.Default.CustomTheme;
            if (customTheme == "Deoxys")
            {
                DeoxysForm++;
                UpdateMascot();
            }
        }
        HandleFriendship(2);
        if (Quotes == null || Quotes.Length == 0) return;
        string quote = Quotes[new Random().Next(Quotes.Length)];
        
        // Contextual Thoughts
        if (TC_RomFS.SelectedTab == Tab_RomFS)
        {
            var active = FLP_RomFS.Controls.OfType<Button>().FirstOrDefault(b => b.Focused);
            if (active == B_Personal) quote = "It dreams about what it would be if it got buffed.";
            else if (active == B_LevelUp) quote = "Maybe it could use a new move or two!";
        }

        L_MascotThought.Text = quote;
        
        // Visual feedback
        PNL_MascotGlass.Visible = true;
        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += (s, ev) => { PNL_MascotGlass.Visible = false; timer.Stop(); };
        timer.Start();
    }

    private void B_Store_Click(object sender, EventArgs e)
    {
        if (Friendship < 100)
        {
            WinFormsUtil.Alert("You need at least 100 Friendship points to open the Store!");
            return;
        }

        var storeItems = new (string Name, int ID)[] { 
            ("Red Orb", 534), ("Blue Orb", 535), ("Meteorite", 729), ("Big Root", 296), 
            ("Black Glasses", 240), ("Leftovers", 234), ("Solganium Z", 927), 
            ("Lunalium Z", 928), ("Ultranecrozium Z", 929) 
        };
        var ownedItems = (Properties.Settings.Default.MascotItems ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        using (var f = new Form { Text = "Mascot Store", Size = new Size(400, 500), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog })
        {
            var flp = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            foreach (var item in storeItems)
            {
                var pnl = new Panel { Size = new Size(350, 60), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 5) };
                var pb = new PictureBox { Size = new Size(48, 48), Location = new Point(5, 5), SizeMode = PictureBoxSizeMode.Zoom };
                pb.Image = (Bitmap)Properties.Resources.ResourceManager.GetObject($"item_{item.ID}") ?? Properties.Resources.helditem;
                var lbl = new Label { Text = item.Name, Location = new Point(60, 10), Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true };
                var cost = new Label { Text = ownedItems.Contains(item.Name) ? "OWNED" : "50 PTS", Location = new Point(60, 30), AutoSize = true, ForeColor = Color.Gold };
                
                var btnBuy = new Button { Text = "BUY", Size = new Size(60, 30), Location = new Point(280, 15), Enabled = !ownedItems.Contains(item.Name) };
                btnBuy.Click += (s, ev) => {
                    if (Friendship < 50) { WinFormsUtil.Alert("Not enough points!"); return; }
                    Friendship -= 50;
                    ownedItems.Add(item.Name);
                    Properties.Settings.Default.MascotItems = string.Join(",", ownedItems);
                    Properties.Settings.Default.Save();
                    WinFormsUtil.Alert($"Purchased {item.Name}!");
                    UpdateMascot();
                    f.Close();
                };

                pnl.Controls.AddRange(new Control[] { pb, lbl, cost, btnBuy });
                flp.Controls.Add(pnl);
            }
            f.Controls.Add(flp);
            WinFormsUtil.ApplyTheme(f);
            f.ShowDialog();
        }
    }

    internal static GameConfig Config;
    public static string RomFSPath;
    public static string ExeFSPath;
    public static string ExHeaderPath;
    private volatile int threads;
    internal static volatile int Language;
    internal static SMDH SMDH;
    private uint HANSgameID; // for exporting RomFS/ExeFS with correct X8 gameID
    private readonly bool skipBoth;
    public static PersonalInfo[] SpeciesStat => Config.Personal.Table;

    // Main Form Methods
    private void L_About_Click(object sender, EventArgs e)
    {
        new About().ShowDialog();
    }

    private void L_GARCInfo_Click(object sender, EventArgs e)
    {
        if (RomFSPath == null)
            return;

        string s = "Game Type: " + Config.Version + Environment.NewLine;
        s = Config.Files.Select(file => file.Name).Aggregate(s, (current, t) => current + string.Format(Environment.NewLine + "{0} - {1}", t, Config.GetGARCFileName(t)));

        var copyPrompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, s, "Copy to Clipboard?");
        if (copyPrompt != DialogResult.Yes)
            return;

        try { Clipboard.SetText(s); }
        catch { WinFormsUtil.Alert("Unable to copy to Clipboard."); }
    }

    private void L_Game_Click(object sender, EventArgs e) { var ed = new EnhancedRestore(Config); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); }

    private void B_Open_Click(object sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            OpenQuick(fbd.SelectedPath);
            OnPathChanged();
        }
    }

    private void ChangeLanguage(object sender, EventArgs e)
    {
        if (InvokeRequired)
            Invoke((MethodInvoker)delegate { Language = CB_Lang.SelectedIndex; });
        else Language = CB_Lang.SelectedIndex;
        if (Config != null)
            Config.Language = Language;
        Menu_Options.DropDown.Close();
        if (!Tab_RomFS.Enabled || Config == null)
            return;

        if ((Config.XY || Config.ORAS) && Language > 7)
        {
            WinFormsUtil.Alert("Language not available for games. Defaulting to English.");
            if (InvokeRequired)
                Invoke((MethodInvoker)delegate { CB_Lang.SelectedIndex = 2; });
            else CB_Lang.SelectedIndex = 2;
            return; // set event re-triggers this method
        }

        UpdateProgramTitle();
        Config.InitializeGameText();
        Properties.Settings.Default.Language = Language;
        Properties.Settings.Default.Save();
    }

    private void Menu_Exit_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CloseForm(object sender, FormClosingEventArgs e)
    {
        if (Config == null)
            return;
        var g = Config.GARCGameText;
        string[][] files = Config.GameTextStrings;
        g.Files = files.Select(x => TextFile.GetBytes(Config, x)).ToArray();
        g.Save();

        try
        {
            var text = RandSettings.Save();
            File.WriteAllLines(RandSettings.FileName, text, Encoding.Unicode);
        }
        catch
        {
            // ignored
        }
    }

    private void OpenQuick(string path)
    {
        if (ThreadActive())
            return;

        try
        {
            if (!Directory.Exists(path)) // File
                OpenFile(path);
            else // Directory
                OpenDirectory(path);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error($"Failed to open -- {path}", ex.Message);
            ResetStatus();
        }
    }

    private void OpenFile(string path)
    {
        if (!File.Exists(path))
            return;

        var fi = new FileInfo(path);
        if (fi.Name.Contains("code.bin")) // Compress/Decompress .code.bin
        {
            OpenExeFSCodeBinary(path, fi);
        }
        else if (fi.Name.Contains("exe", StringComparison.OrdinalIgnoreCase)) // Unpack exefs
        {
            OpenExeFSCombined(path, fi);
        }
        else if (fi.Name.Contains("rom", StringComparison.OrdinalIgnoreCase))
        {
            WinFormsUtil.Alert("RomFS unpacking not implemented.");
        }
        else
        {
            var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Unpack sub-files?", "Cancel: Abort");
            if (dr == DialogResult.Cancel)
                return;
            bool recurse = dr == DialogResult.Yes;
            ToolsUI.OpenARC(path, pBar1, recurse);
        }
    }

    private void OpenExeFSCombined(string path, FileInfo fi)
    {
        if (fi.Length % 0x200 != 0)
            return;
        var dir = Path.GetDirectoryName(path);
        if (dir is null)
            return;

        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected ExeFS.bin.", "Unpack?");
        if (prompt != DialogResult.Yes)
            return;

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            ExeFS.UnpackExeFS(path, dir);
            Interlocked.Decrement(ref threads);
            WinFormsUtil.Alert("Unpacked!");
        }).Start();
    }

    private void OpenExeFSCodeBinary(string path, FileInfo fi)
    {
        if (fi.Length % 0x200 == 0)
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Decompressed code.bin.", "Compress? File will be replaced.");
            if (prompt != DialogResult.Yes)
                return;
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                new BLZCoder(["-en", path], pBar1);
                Interlocked.Decrement(ref threads);
                WinFormsUtil.Alert("Compressed!");
            }).Start();
        }
        else
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed code.bin.", "Decompress? File will be replaced.");
            if (prompt != DialogResult.Yes)
                return;
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                new BLZCoder(["-d", path], pBar1);
                Interlocked.Decrement(ref threads);
                WinFormsUtil.Alert("Decompressed!");
            }).Start();
        }
    }

    private void OpenDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        // Check for ROMFS/EXEFS/EXHEADER
        RomFSPath = ExeFSPath = null; // Reset
        Config = null;

        string[] folders = Directory.GetDirectories(path);
        int count = folders.Length;

        // Find RomFS folder
        foreach (string f in folders.Where(f => new DirectoryInfo(f).Name.Contains("rom", StringComparison.OrdinalIgnoreCase) && Directory.Exists(f)))
            CheckIfRomFS(f);
        // Find ExeFS folder
        foreach (string f in folders.Where(f => new DirectoryInfo(f).Name.Contains("exe", StringComparison.OrdinalIgnoreCase) && Directory.Exists(f)))
            CheckIfExeFS(f);

        if (count > 3)
            WinFormsUtil.Alert("pk3DS will function best if you keep your Game Files folder clean and free of unnecessary folders.");

        // Enable buttons if applicable
        Tab_RomFS.Enabled = Menu_Restore.Enabled = Tab_CRO.Enabled = Menu_CRO.Enabled = Menu_Shuffler.Enabled = RomFSPath != null;
        Tab_ExeFS.Enabled = RomFSPath != null && ExeFSPath != null;
        if (RomFSPath != null && Config != null)
        {
            ToggleSubEditors();
            L_Version.Text = $"v{Config.Version}";
            L_Version.Visible = true;
            
            if (SMDH != null) PB_GameIcon.Image = SMDH.LargeIcon.Icon;
            else PB_GameIcon.Image = WinFormsUtil.GetSprite(Config.Sun ? 791 : 716, 0, 0, 0, Config); // Default mascot icon
            
            if (Directory.Exists("personal"))
                Directory.Delete("personal", true); // Force reloading of personal data if the game is switched.

            TB_Path.Text = path;
        }
        else if (ExeFSPath != null)
        {
            L_Game.Text = "ExeFS loaded - no RomFS";
            L_Version.Visible = false;
            PB_GameIcon.Image = null;
            TB_Path.Text = path;
        }
        else
        {
            L_Game.Text = "No Game Loaded";
            L_Version.Visible = false;
            PB_GameIcon.Image = null;
            TB_Path.Text = "";
        }

        if (RomFSPath != null)
        {
            HandleFriendship(1); // Editor open
            UpdateMascot();
            
            // Extract SMDH info
            if (SMDH != null)
            {
                PB_GameIcon.Image = SMDH.LargeIcon.Icon;
                L_Version.Text = $"v{SMDH.Version}";
                L_Version.Visible = true;
            }
            // Trigger Data Loading
            if (RTB_Status.Text.Length > 0)
                RTB_Status.Clear();

            UpdateStatus("Data found! Loading persistent data for subforms...", false);
            try
            {
                if (Config is not null)
                {
                    if (ExeFSPath is not null)
                        Config.Initialize(RomFSPath, ExeFSPath, Language);
                }
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error("Failed to initialize game logic.", ex.Message);
                ResetStatus();
                return;
            }
        }

        UpdateProgramTitle();

        // Enable Rebuilding options if all files have been found
        CheckIfExHeader(path);
        Menu_ExeFS.Enabled = ExeFSPath != null;
        Menu_RomFS.Enabled = Menu_Restore.Enabled = Menu_GARCs.Enabled = RomFSPath != null;
        Menu_Patch.Enabled = RomFSPath != null && ExeFSPath != null;
        Menu_3DS.Enabled = RomFSPath != null && ExeFSPath != null && ExHeaderPath != null;
        Menu_Trimmed3DS.Enabled = RomFSPath != null && ExeFSPath != null && ExHeaderPath != null;

        UpdateMascot();
        string gradientName = Config.Version.ToString();
        if (gradientName.Contains("Sun") || gradientName.Contains("Moon")) gradientName = "Solgaleo"; // Default to Solgaleo/Lunala
        if (Config.X) gradientName = "Xerneas";
        if (Config.Y) gradientName = "Yveltal";
        if (Config.OR) gradientName = "Primal Groudon";
        if (Config.AS) gradientName = "Primal Kyogre";
        
        if (Properties.Settings.Default.SelectedGradient != "Default")
            gradientName = Properties.Settings.Default.SelectedGradient;
            
        WinFormsUtil.ApplyGradient(this, gradientName);

        // Change L_Game if RomFS and ExeFS exists to a better descriptor
        SMDH = ExeFSPath != null
            ? File.Exists(Path.Combine(ExeFSPath, "icon.bin")) ? new SMDH(Path.Combine(ExeFSPath, "icon.bin")) : null
            : null;
        HANSgameID = SMDH != null ? (SMDH.AppSettings?.StreetPassID ?? 0) : 0;
        L_Game.Visible = SMDH == null && RomFSPath != null;
        TB_Path.Select(TB_Path.TextLength, 0);
        // Method finished.
        System.Media.SystemSounds.Asterisk.Play();
        ResetStatus();
        Properties.Settings.Default.GamePath = path;
        Properties.Settings.Default.Save();
    }

    private void B_ExtractCXI_Click(object sender, EventArgs e)
    {
        const string l1 = "Extracting a CXI requires multiple GB of disc space and takes some time to complete.";
        const string l2 = "If you want to continue, press OK to select your CXI and then select your output directory. For best results, make sure the output directory is an empty directory.";
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, l1, l2);
        if (prompt != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog { Title = "Select CXI", Filter = "CXI files (*.cxi)|*.cxi" };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        using var fbd = new FolderBrowserDialog();
        DialogResult result = fbd.ShowDialog();
        if (result != DialogResult.OK)
            return;

        var inputCXI = ofd.FileName;
        ExtractNCCH(inputCXI, fbd.SelectedPath);
    }

    private void B_Extract3DS_Click(object sender, EventArgs e)
    {
        const string l1 = "Extracting a 3DS file requires multiple GB of disc space and takes some time to complete.";
        const string l2 = "If you want to continue, press OK to select your CXI and then select your output directory. For best results, make sure the output directory is an empty directory.";
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, l1, l2);
        if (prompt != DialogResult.OK)
            return;

        using var ofd = new OpenFileDialog { Title = "Select 3DS", Filter = "3DS files (*.3ds)|*.3ds" };
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        using var fbd = new FolderBrowserDialog();
        DialogResult result = fbd.ShowDialog();
        if (result != DialogResult.OK)
            return;

        var input3DS = ofd.FileName;
        ExtractNCSD(input3DS, fbd.SelectedPath);
    }

    private void ExtractNCCH(string ncchPath, string outputDirectory)
    {
        if (!File.Exists(ncchPath))
            return;

        var ncch = new NCCH();

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            ncch.ExtractNCCHFromFile(ncchPath, outputDirectory, RTB_Status, pBar1);
            Interlocked.Decrement(ref threads);
            WinFormsUtil.Alert("Extraction complete!");
        }).Start();
    }

    private void ExtractNCSD(string ncsdPath, string outputDirectory)
    {
        if (!File.Exists(ncsdPath))
            return;

        var ncsd = new NCSD();
        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            ncsd.ExtractFilesFromNCSD(ncsdPath, outputDirectory, RTB_Status, pBar1);
            Interlocked.Decrement(ref threads);
            WinFormsUtil.Alert("Extraction complete!");
        }).Start();
    }

    private void ToggleSubEditors()
    {
        // Hide all buttons
        foreach (var f in from TabPage t in TC_RomFS.TabPages from f in t.Controls.OfType<FlowLayoutPanel>() select f)
        {
            for (int i = f.Controls.Count - 1; i >= 0; i--)
                f.Controls.Remove(f.Controls[i]);
        }

        B_MoveTutor.Visible = Config.ORAS; // Default false unless loaded

        Control[] romfs, exefs, cro;

        switch (Config.Generation)
        {
            case 6:
                romfs = [B_GameText, B_StoryText, B_Personal, B_Evolution, B_LevelUp, B_Wild, B_MegaEvo, B_EggMove, B_Trainer, B_Item, B_Move, B_Maison, B_TitleScreen, B_OWSE,
                ];
                exefs = [B_MoveTutor, B_TMHM, B_Mart, B_Pickup, B_OPower, B_ShinyRate];
                cro = [B_TypeChart, B_Starter, B_Gift, B_Static, B_CROExpander];
                B_MoveTutor.Visible = Config.ORAS; // Default false unless loaded
                break;
            case 7:
                romfs = [B_GameText, B_StoryText, B_Personal, B_Evolution, B_LevelUp, B_Wild, B_MegaEvo, B_EggMove, B_Trainer, B_Item, B_Move, B_Royal, B_Pickup, B_OWSE,
                ];
                exefs = [B_TM, B_TypeChart, B_ShinyRate];
                cro = [B_Mart, B_MoveTutor, B_CROExpander, B_ResearchCenter];
                B_MoveTutor.Visible = Config.USUM;

                if (Config.Version != GameVersion.SMDEMO)
                    romfs = [.. romfs, .. new[] { B_Static }];
                break;
            default:
                romfs = exefs = cro = [new Label { Text = "No editors available." }];
                break;
        }

        FLP_RomFS.Controls.AddRange(romfs);
        FLP_ExeFS.Controls.AddRange(exefs);
        FLP_CRO.Controls.AddRange(cro);
    }

    private void UpdateProgramTitle() => Text = GetProgramTitle();

    private static string GetProgramTitle()
    {
        // 0 - JP
        // 1 - EN
        // 2 - FR
        // 3 - DE
        // 4 - IT
        // 5 - ES
        // 6 - CHS
        // 7 - KO
        // 8 -
        // 11 - CHT
        if (SMDH?.AppSettings == null)
            return "pk3DS";
        int[] AILang = [0, 0, 1, 2, 4, 3, 5, 7, 8, 9, 6, 11];
        return "pk3DS - " + SMDH.AppInfo[AILang[Language]].ShortDescription;
    }

    private static GameConfig CheckGameType(string[] files)
    {
        try
        {
            if (files.Length > 1000)
                return null;
            var parent = Directory.GetParent(files[0]);
            if (parent is null)
                return null;

            string[] fileArr = Directory.GetFiles(Path.Combine(parent.FullName, "a"), "*", SearchOption.AllDirectories);
            int fileCount = fileArr.Count(file => Path.GetFileName(file).Length == 1);
            return new GameConfig(fileCount);
        }
        catch { }
        return null;
    }

    private static bool CheckIfRomFS(string path)
    {
        string[] top = Directory.GetDirectories(path);
        var fi = new FileInfo(top[top.Length > 1 ? 1 : 0]);
        // Check to see if the folder is romfs
        if (fi.Name == "a")
        {
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            var cfg = CheckGameType(files);

            if (cfg == null)
            {
                RomFSPath = null;
                Config = null;
                WinFormsUtil.Error("File count does not match expected game count.", "Files: " + files.Length);
                return false;
            }

            RomFSPath = path;
            Config = cfg;
            pk3DS.Core.Modding.ProjectState.SetRomFS(path);
            return true;
        }
        WinFormsUtil.Error("Folder does not contain an 'a' folder in the top level.");
        RomFSPath = null;
        return false;
    }

    private bool CheckIfExeFS(string path)
    {
        string[] files = Directory.GetFiles(path);
        if (files.Length == 1 && string.Equals(Path.GetFileName(files[0]), "exefs.bin", StringComparison.OrdinalIgnoreCase))
        {
            // Prompt if the user wants to unpack the ExeFS.
            if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected ExeFS binary.", "Unpack?"))
                return false;

            // User wanted to unpack. Unpack.
            if (!ExeFS.UnpackExeFS(files[0], path))
                return false; // on unpack fail

            // Remove ExeFS binary after unpacking
            File.Delete(files[0]);

            files = Directory.GetFiles(path);
            // unpack successful, continue onward!
        }

        if (files.Length != 3 && files.Length != 4)
            return false;

        var fi = new FileInfo(files[0]);
        if (!fi.Name.Contains("code"))
        {
            if (new FileInfo(files[1]).Name != "code.bin")
                return false;

            File.Move(files[1], Path.Combine(Path.GetDirectoryName(files[1]), ".code.bin"));
            files = Directory.GetFiles(path);
            fi = new FileInfo(files[0]);
        }
        if (fi.Length % 0x200 != 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed code binary.", "Decompress? File will be replaced.") == DialogResult.Yes)
            new Thread(() => { Interlocked.Increment(ref threads); new BLZCoder(["-d", files[0]], pBar1); Interlocked.Decrement(ref threads); WinFormsUtil.Alert("Decompressed!"); }).Start();

        ExeFSPath = path;
        return true;
    }

    private static bool CheckIfExHeader(string path)
    {
        ExHeaderPath = null;
        // Input folder path should contain the ExHeader.
        string[] files = Directory.GetFiles(path);
        foreach (string fp in from s in files let f = new FileInfo(s) where (f.Name.StartsWith("exh", StringComparison.OrdinalIgnoreCase) || f.Name.StartsWith("decryptedexh", StringComparison.OrdinalIgnoreCase)) && f.Length == 0x800 select s)
            ExHeaderPath = fp;

        return ExHeaderPath != null;
    }

    private bool ThreadActive()
    {
        if (threads <= 0)
            return false;
        WinFormsUtil.Alert("Please wait for all operations to finish first."); return true;
    }

    private void TabMain_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) is true)
            e.Effect = DragDropEffects.Copy;
    }

    private void TabMain_DragDrop(object sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: not 0 } files)
            return;
        string path = files[0]; // open first D&D
        OpenQuick(path);
    }

    // RomFS Subform Items
    private void RebuildRomFS(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (RomFSPath == null)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuild RomFS?") != DialogResult.Yes)
            return;

        var sfd = new SaveFileDialog
        {
            FileName = HANSgameID != 0 ? HANSgameID.ToString("X8") + ".romfs" : "romfs.bin",
            Filter = "HANS RomFS|*.romfs|Binary File|*.bin|All Files|*.*",
        };
        sfd.FilterIndex = HANSgameID != 0 ? 0 : sfd.Filter.Length - 1;

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            new Thread(() =>
            {
                UpdateStatus(Environment.NewLine + "Building RomFS binary. Please wait until the program finishes.");

                Interlocked.Increment(ref threads);
                RomFS.BuildRomFS(RomFSPath, sfd.FileName, RTB_Status, pBar1);
                Interlocked.Decrement(ref threads);

                HandleFriendship(10); // Rebuild gain
                UpdateStatus("RomFS binary saved." + Environment.NewLine);
                WinFormsUtil.Alert("Wrote RomFS binary:", sfd.FileName);
            }).Start();
        }
    }

    private void B_GameText_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GARCGameText;
            string[][] files = Config.GameTextStrings;
            Invoke(() => { var ed = new TextEditor(files, "gametext"); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
            g.Files = TryWriteText(files, g);
            g.Save();
        }).Start();
    }

    private void B_StoryText_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("storytext");
            string[][] files = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
            Invoke(() => { var ed = new TextEditor(files, "storytext"); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
            g.Files = TryWriteText(files, g);
            g.Save();
        }).Start();
    }

    private static byte[][] TryWriteText(string[][] files, GARCFile g)
    {
        byte[][] data = new byte[files.Length][];
        var errata = new List<string>();
        for (int i = 0; i < data.Length; i++)
        {
            try
            {
                data[i] = TextFile.GetBytes(Config, files[i]);
            }
            catch (Exception ex)
            {
                errata.Add($"File {i:000} | {ex.Message}");
                // revert changes
                data[i] = g.GetFile(i);
            }
        }
        if (errata.Count == 0)
            return data;

        string[] options =
        [
            "Cancel: Discard all changes",
            "Yes: Save changes, dump errata/failed text",
            "No: Save changes, don't dump errata/failed text",
        ];
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Errors found while attempting to save text."
                                                                    + Environment.NewLine + "Example: " + errata[0],
            string.Join(Environment.NewLine, options));
        if (dr == DialogResult.Cancel)
            return g.Files; // discard
        if (dr == DialogResult.No)
            return data;

        const string txt_errata = "text_errata.txt";
        const string txt_failed = "text_failed.txt";
        File.WriteAllLines(txt_errata, errata);
        TextEditor.ExportTextFile(txt_failed, true, files);

        WinFormsUtil.Alert("Saved text files to path: " + Application.StartupPath,
            txt_errata + Environment.NewLine + txt_failed);

        return data;
    }

    private void B_Maison_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        DialogResult dr;
        switch (Config.Generation)
        {
            case 6:
                dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Edit Super Maison instead of Normal Maison?", "Yes = Super, No = Normal, Cancel = Abort");
                break;
            case 7:
                dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Edit Battle Royal instead of Battle Tree?", "Yes = Royal, No = Tree, Cancel = Abort");
                break;
            default:
                return;
        }
        if (dr == DialogResult.Cancel)
            return;

        new Thread(() =>
        {
            bool super = dr == DialogResult.Yes;
            string c = super ? "S" : "N";
            var trdata = Config.GetGARCData("maisontr" + c);
            var trpoke = Config.GetGARCData("maisonpk" + c);
            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new MaisonEditor6(trd, trp, super); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new MaisonEditor7(trd, trp, super); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            trdata.Files = trd;
            trpoke.Files = trp;
            trdata.Save();
            trpoke.Save();
        }).Start();
    }

    private void B_Personal_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            byte[][] d = Config.GARCPersonal.Files;
            var gl = Config.GARCLearnsets;
            var ge = Config.GetGARCData("eggmove");
            byte[][] l = gl.Files;
            byte[][] eg = ge.Files;
            byte[][] ev = Config.GetGARCData("evolution").Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new PersonalEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    PersonalEditor7 ped = null;
                    Invoke(() => { ped = new PersonalEditor7(d, l, eg, ev); WinFormsUtil.ApplyTheme(ped); HandleFriendship(1); ped.ShowDialog(); });
                    if (ped != null)
                    {
                        d = ped.Files;
                        l = ped.Learnsets;
                        eg = ped.EggMoves;
                        ev = ped.EvolutionFiles;
                    }
                    break;
            }

            // Set Master Table back (Safely resize if needed)
            if (d.Length > 1 && Config.Generation == 7)
            {
                int len = d[0].Length;
                // If the last entry is still a Pokemon entry, we need to append a new slot for the Master Table
                if (d[^1].Length == len)
                {
                    Array.Resize(ref d, d.Length + 1);
                }

                int tableSize = (d.Length - 1) * len;
                if (d[^1] == null || d[^1].Length != tableSize)
                {
                    d[^1] = new byte[tableSize];
                }
                for (int i = 0; i < d.Length - 1; i++)
                    d[i].CopyTo(d[^1], i * len);
            }

            Config.GARCPersonal.Files = d;
            Config.GARCPersonal.Save();
            Config.InitializePersonal();

            // Save any changes from jumps
            gl.Files = l;
            gl.Save();
            Config.InitializeLearnset();
            
            var ge_new = Config.GetGARCData("eggmove");
            if (ge_new != null)
            {
                ge_new.Files = eg;
                ge_new.Save();
            }

            var g_evo = Config.GetGARCData("evolution");
            if (g_evo != null)
            {
                g_evo.Files = ev;
                g_evo.Save();
                Config.InitializeEvos();
            }
        }).Start();
    }

    private void B_Trainer_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var trclass = Config.GetGARCData("trclass");
            var trdata = Config.GetGARCData("trdata");
            var trpoke = Config.GetGARCData("trpoke");
            byte[][] trc = trclass.Files;
            byte[][] trd = trdata.Files;
            byte[][] trp = trpoke.Files;

            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new RSTE(trd, trp); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { 
                        try {
                            var ed = new SMTE(trd, trp); 
                            WinFormsUtil.ApplyTheme(ed); 
                            HandleFriendship(1); 
                            ed.ShowDialog(); 
                        } catch (Exception ex) {
                            WinFormsUtil.Error("Failed to open Trainer Editor (SMTE):\n" + ex.Message + "\n" + ex.StackTrace);
                        }
                    });
                    break;
            }
            trclass.Files = trc;
            trdata.Files = trd;
            trpoke.Files = trp;
            trclass.Save();
            trdata.Save();
            trpoke.Save();
        }).Start();
    }

    private void B_Wild_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            string[] files;
            Action action;
            switch (Config.Generation)
            {
                case 6:
                    files = ["encdata"];
                    if (Config.ORAS)
                        action = () => { var ed = new RSWE(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); };
                    else if (Config.XY)
                        action = () => { var ed = new XYWE(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); };
                    else return;

                    Invoke((MethodInvoker)delegate { Enabled = false; });
                    FileGet(files, false);
                    Invoke(action);
                    FileSet(files);
                    Invoke((MethodInvoker)delegate { Enabled = true; });
                    break;
                case 7:
                    Invoke((MethodInvoker)delegate { Enabled = false; });
                    Interlocked.Increment(ref threads);

                    files = ["encdata", "zonedata", "worlddata"];
                    UpdateStatus($"GARC Get: {files[0]}... ");
                    var ed = Config.GetlzGARCData(files[0]);
                    UpdateStatus($"GARC Get: {files[1]}... ");
                    var zd = Config.GetlzGARCData(files[1]);
                    UpdateStatus($"GARC Get: {files[2]}... ");
                    var wd = Config.GetlzGARCData(files[2]);
                    UpdateStatus("Running SMWE... ");
                    action = () => { var editor = new SMWE(ed, zd, wd); WinFormsUtil.ApplyTheme(editor); HandleFriendship(1); editor.ShowDialog(); };
                    Invoke(action);

                    UpdateStatus($"GARC Set: {files[0]}... ");
                    ed.Save();
                    ResetStatus();
                    Interlocked.Decrement(ref threads);
                    Invoke((MethodInvoker)delegate { Enabled = true; });
                    break;
                default:
                    return;
            }
        }).Start();
    }

    private void B_OWSE_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "The OverWorld/Script Editor is not recommended for most users and is still a work-in-progress.", "Continue anyway?"))
            return;
        switch (Config.Generation)
        {
            case 6:
                RunOWSE6();
                return;
            case 7:
                RunOWSE7();
                return;
        }
    }

    private void RunOWSE6()
    {
        Enabled = false;
        new Thread(() =>
        {
            bool reload = ModifierKeys is Keys.Control or (Keys.Alt | Keys.Control);
            string[] files = ["encdata", "storytext", "mapGR", "mapMatrix"];
            if (reload || files.Sum(t => Directory.Exists(t) ? 0 : 1) != 0) // Dev bypass if all exist already
                FileGet(files, false);

            // Don't set any data back. Just view.
            {
                var g = Config.GetGARCData("storytext");
                string[][] tfiles = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
                Invoke(() => new OWSE().Show());
                Invoke(() => new TextEditor(tfiles, "storytext").Show());
                while (Application.OpenForms.Count > 1)
                    Thread.Sleep(200);
            }
            Invoke((MethodInvoker)delegate { Enabled = true; });
            FileSet(files);
        }).Start();
    }

    private void RunOWSE7()
    {
        Enabled = false;
        new Thread(() =>
        {
            var files = new[] { "encdata", "zonedata", "worlddata" };
            UpdateStatus($"GARC Get: {files[0]}... ");
            var ed = Config.GetlzGARCData(files[0]);
            UpdateStatus($"GARC Get: {files[1]}... ");
            var zd = Config.GetlzGARCData(files[1]);
            UpdateStatus($"GARC Get: {files[2]}... ");
            var wd = Config.GetlzGARCData(files[2]);

            var g = Config.GetGARCData("storytext");
            string[][] tfiles = g.Files.Select(file => new TextFile(Config, file).Lines).ToArray();
            Invoke(() => new TextEditor(tfiles, "storytext").Show());
            Invoke(() => new OWSE7(ed, zd, wd).Show());
            while (Application.OpenForms.Count > 1)
                Thread.Sleep(200);
            Invoke((MethodInvoker)delegate { Enabled = true; });
        }).Start();
    }

    private void B_Evolution_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("evolution");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new EvolutionEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new EvolutionEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            g.Files = d;
            Config.InitializeEvos();
            g.Save();
        }).Start();
    }

    private void B_MegaEvo_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("megaevo");
            byte[][] d = g.Files;

            // Auto-expand megaevo GARC to match personal GARC for custom forms
            if (Config.Generation == 7)
            {
                int personalCount = Config.Personal.Table.Length;
                if (d.Length < personalCount)
                {
                    int oldLen = d.Length;
                    Array.Resize(ref d, personalCount);
                    for (int i = oldLen; i < personalCount; i++)
                        d[i] = new byte[16]; // Empty mega evo entry
                }
            }

            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new MegaEvoEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new MegaEvoEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            g.Files = d;
            g.Save();
        }).Start();
    }

    private void B_Item_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("item");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new ItemEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    ItemEditor7 editor7 = null;
                    Invoke(() => { editor7 = new ItemEditor7(d); WinFormsUtil.ApplyTheme(editor7); HandleFriendship(1); editor7.ShowDialog(); });
                    if (editor7 != null) d = editor7.Files; // Array.Resize in ChangeEntry may create a new array; recapture it
                    break;
            }
            g.Files = d;
            g.Save();

            // Persist any game text changes made by the item editor (item names/flavor)
            var gt = Config.GARCGameText;
            gt.Files = TryWriteText(Config.GameTextStrings, gt);
            gt.Save();
            Config.InitializeGameText();
        }).Start();
    }

    private void B_Move_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GARCMoves;
            byte[][] Moves;
            switch (Config.Generation)
            {
                case 6:
                    bool isMini = Config.ORAS;
                    Moves = isMini ? Mini.UnpackMini(g.GetFile(0), "WD") : g.Files;
                    Invoke(() => new MoveEditor6(Moves).ShowDialog());
                    g.Files = isMini ? [Mini.PackMini(Moves, "WD")] : Moves;
                    break;
                case 7:
                    Moves = Mini.UnpackMini(g.GetFile(0), "WD");
                    Invoke(() => 
                    {
                        var editor = new MoveEditor7(Moves);
                        WinFormsUtil.ApplyTheme(editor);
                        HandleFriendship(1);
                        editor.ShowDialog();
                        Moves = editor.Files;
                    });
                    g.Files = [Mini.PackMini(Moves, "WD")];
                    break;
            }
            g.Save();
            Config.InitializeMoves();
        }).Start();
    }

    private void B_LevelUp_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            byte[][] d = Config.GARCLearnsets.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new LevelUpEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new LevelUpEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            Config.GARCLearnsets.Files = d;
            Config.GARCLearnsets.Save();
            Config.InitializeLearnset();
        }).Start();
    }

    private void B_EggMove_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            var g = Config.GetGARCData("eggmove");
            byte[][] d = g.Files;
            switch (Config.Generation)
            {
                case 6:
                    Invoke(() => { var ed = new EggMoveEditor6(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
                case 7:
                    Invoke(() => { var ed = new EggMoveEditor7(d); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
                    break;
            }
            g.Files = d;
            g.Save();
        }).Start();
    }

    private void B_TitleScreen_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        new Thread(() =>
        {
            string[] files = ["titlescreen"];
            FileGet(files); // Compressed files exist, handled in the other form since there's so many
            Invoke(() => { var ed = new TitleScreenEditor6(); WinFormsUtil.ApplyTheme(ed); HandleFriendship(1); ed.ShowDialog(); });
            FileSet(files);
        }).Start();
    }
    // RomFS File Requesting Method Wrapper
    private void FileGet(string[] files, bool skipDecompression = true, bool skipGet = false)
    {
        if (skipGet || skipBoth)
            return;
        foreach (string toEdit in files)
        {
            string GARC = Config.GetGARCFileName(toEdit);
            UpdateStatus($"GARC Get: {toEdit} @ {GARC}... ");
            ThreadGet(Path.Combine(RomFSPath, GARC), toEdit, true, skipDecompression);
            while (threads > 0) Thread.Sleep(50);
            ResetStatus();
        }
    }

    private void FileSet(IEnumerable<string> files, bool keep = false)
    {
        if (skipBoth)
            return;
        foreach (string toEdit in files)
        {
            string GARC = Config.GetGARCFileName(toEdit);
            UpdateStatus($"GARC Set: {toEdit} @ {GARC}... ");
            ThreadSet(Path.Combine(RomFSPath, GARC), toEdit, 4); // 4 bytes for Gen6
            while (threads > 0) Thread.Sleep(50);
            if (!keep && Directory.Exists(toEdit)) Directory.Delete(toEdit, true);
            ResetStatus();
        }
    }

    // ExeFS Subform Items
    private void RebuildExeFS(object sender, EventArgs e)
    {
        if (ExeFSPath == null)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuild ExeFS?") != DialogResult.Yes)
            return;

        string[] files = Directory.GetFiles(ExeFSPath);
        int file = 0;
        if (files[1].Contains("code"))
            file = 1;

        var sfd = new SaveFileDialog
        {
            FileName = HANSgameID != 0 ? HANSgameID.ToString("X8") + ".exefs" : "exefs.bin",
            Filter = "HANS ExeFS|*.exefs|Binary File|*.bin|All Files|*.*",
        };
        sfd.FilterIndex = HANSgameID != 0 ? 0 : sfd.Filter.Length - 1;

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            new Thread(() =>
            {
                Interlocked.Increment(ref threads);
                new BLZCoder(["-en", files[file]], pBar1);
                WinFormsUtil.Alert("Compressed!");
                ExeFS.PackExeFS(Directory.GetFiles(ExeFSPath), sfd.FileName);
                HandleFriendship(10);
                Interlocked.Decrement(ref threads);
            }).Start();
        }
    }

    private void B_Pickup_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new PickupEditor6().Show();
                break;
            case 7:
                var pickup = Config.GetlzGARCData("pickup");
                Invoke(() => { var ed = new PickupEditor7(pickup); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); });
                break;
        }
    }

    private void B_TMHM_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath == null)
            return;
        switch (Config.Generation)
        {
            case 6: new TMHMEditor6().Show(); break;
            case 7: { var ed = new TMEditor7(); WinFormsUtil.ApplyTheme(ed); ed.Show(); } break;
        }
    }

    private void B_Mart_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new MartEditor6().Show();
                break;

            case 7:
                if (ThreadActive())
                    return;
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                if (RomFSPath != null) { var ed = Config.USUM ? new MartEditor7UU() : (Form)new MartEditor7(); WinFormsUtil.ApplyTheme(ed); ed.Show(); }
                break;
        }
    }

    private void B_MoveTutor_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        switch (Config.Generation)
        {
            case 6:
                if (ExeFSPath != null) new TutorEditor6().Show();
                break;
            case 7:
                if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                    return;
                if (RomFSPath != null) { var ed = new TutorEditor7(); WinFormsUtil.ApplyTheme(ed); ed.Show(); }
                break;
        }
    }

    private void B_OPower_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath != null) new OPower().Show();
    }

    private void B_ShinyRate_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (ExeFSPath != null) { var ed = new ShinyRate(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); }
    }

    private void B_ResearchCenter_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (Config.Version == GameVersion.USUM)
            new ResearchCenter7().ShowDialog();
        else
            WinFormsUtil.Error("Compatibility Error", "Research Center is currently only optimized for Ultra Sun and Ultra Moon.");
    }

    // CRO Subform Items
    private void PatchCRO_CRR(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (RomFSPath == null)
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Rebuilding CRO/CRR is not necessary if you patch the RO module.", "Continue?"))
            return;
        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            CRO.E_HashCRR(Path.Combine(RomFSPath, ".crr", "static.crr"), RomFSPath, true, /* true // don't patch crr for now */ false, RTB_Status, pBar1);
            Interlocked.Decrement(ref threads);

            WinFormsUtil.Alert("CRO's and CRR have been updated.",
                "If you have made any modifications, it is required that the RSA Verification check be patched on the system in order for the modified CROs to load (ie, no file redirection like NTR's layeredFS).");
        }).Start();
    }

    private void B_Starter_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllPoke3Select.cro");
        string CRO2 = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllPoke3Select.cro was not found in your RomFS folder!");
            return;
        }
        if (!File.Exists(CRO2))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        var ed = new StarterEditor6(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }

    private void B_TypeChart_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        switch (Config.Generation)
        {
            case 6:
                {
                    if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
                        return;
                    string CRO = Path.Combine(RomFSPath, "DllBattle.cro");
                    if (!File.Exists(CRO))
                    {
                        WinFormsUtil.Error("File Missing!", "DllBattle.cro was not found in your RomFS folder!");
                        return;
                    }
                    var ed6 = new TypeChart6(); 
                    WinFormsUtil.ApplyTheme(ed6); 
                    ed6.ShowDialog();
                }
                break;
            case 7:
                {
                    var ed7 = new TypeChart7(); 
                    WinFormsUtil.ApplyTheme(ed7); 
                    ed7.ShowDialog();
                }
                break;
        }
    }

    private void B_Gift_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        var ed = new GiftEditor6(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }

    private void B_Static_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        if (Config.Generation == 7)
        {
            new Thread(() =>
            {
                var esg = Config.GetGARCData("encounterstatic");
                byte[][] es = esg.Files;

                Invoke(() => { var ed = new StaticEncounterEditor7(es); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog(); });
                esg.Files = es;
                esg.Save();
            }).Start();
            return;
        }

        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "CRO Editing causes crashes if you do not patch the RO module.", "In order to patch the RO module, your device must be running Custom Firmware (for example, Luma3DS).", "Continue anyway?"))
            return;
        string CRO = Path.Combine(RomFSPath, "DllField.cro");
        if (!File.Exists(CRO))
        {
            WinFormsUtil.Error("File Missing!", "DllField.cro was not found in your RomFS folder!");
            return;
        }
        var ed = new StaticEncounterEditor6(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }  

    private void B_CROExpander_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;
        var ed = new CROExpander(); WinFormsUtil.ApplyTheme(ed); ed.ShowDialog();
    }

    // CXI Building
    private void B_RebuildTrimmed3DS_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        var sfd = new SaveFileDialog
        {
            FileName = "newROM.3ds",
            Filter = "Binary File|*.*",
        };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        string path = sfd.FileName;

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            var exh = new Exheader(ExHeaderPath);
            CTRUtil.BuildROM(true, "Nintendo", ExeFSPath, RomFSPath, ExHeaderPath, exh.GetSerial(), path,
                true, pBar1, RTB_Status);
            Interlocked.Decrement(ref threads);
        }).Start();
    }

    // 3DS Building
    private void B_Rebuild3DS_Click(object sender, EventArgs e)
    {
        if (ThreadActive())
            return;

        var sfd = new SaveFileDialog
        {
            FileName = "newROM.3ds",
            Filter = "Binary File|*.*",
        };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;
        string path = sfd.FileName;

        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            var exh = new Exheader(ExHeaderPath);
            CTRUtil.BuildROM(true, "Nintendo", ExeFSPath, RomFSPath, ExHeaderPath, exh.GetSerial(), path,
                false, pBar1, RTB_Status);
            Interlocked.Decrement(ref threads);
        }).Start();
    }

    // Extra Tools
    private void L_SubTools_Click(object sender, EventArgs e)
    {
        new ToolsUI().ShowDialog();
    }

    private void B_Patch_Click(object sender, EventArgs e)
    {
        new Patch().ShowDialog();
    }

    private void Menu_BLZ_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog();
        if (DialogResult.OK != ofd.ShowDialog())
            return;

        string path = ofd.FileName;
        var fi = new FileInfo(path);
        if (fi.Length > 15 * 1024 * 1024) // 15MB
        { WinFormsUtil.Error("File too big!", fi.Length + " bytes."); return; }

        if (ModifierKeys != Keys.Control && fi.Length % 0x200 == 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Decompressed Binary.", "Compress? File will be replaced.") == DialogResult.Yes)
            new Thread(() => { Interlocked.Increment(ref threads); new BLZCoder(["-en", path], pBar1); Interlocked.Decrement(ref threads); WinFormsUtil.Alert("Compressed!"); }).Start();
        else if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Detected Compressed Binary", "Decompress? File will be replaced.") == DialogResult.Yes)
            new Thread(() => { Interlocked.Increment(ref threads); new BLZCoder(["-d", path], pBar1); Interlocked.Decrement(ref threads); WinFormsUtil.Alert("Decompressed!"); }).Start();
    }

    private void Menu_LZ11_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog();
        if (DialogResult.OK != ofd.ShowDialog())
            return;

        string path = ofd.FileName;
        var fi = new FileInfo(path);
        if (fi.Length > 15 * 1024 * 1024) // 15MB
        { WinFormsUtil.Error("File too big!", fi.Length + " bytes."); return; }

        byte[] data = File.ReadAllBytes(path);
        string predict = data[0] == 0x11 ? "compressed" : "decompressed";
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, $"Detected {predict} file. Do what?",
            "Yes = Decompress\nNo = Compress\nCancel = Abort");
        new Thread(() =>
        {
            Interlocked.Increment(ref threads);
            if (dr == DialogResult.Yes)
            {
                try
                {
                    LZSS.Decompress(path, Path.Combine(Directory.GetParent(path).FullName, "dec_" + Path.GetFileNameWithoutExtension(path) + ".bin"));
                }
                catch (Exception err) { WinFormsUtil.Alert("Tried decompression, may have worked:", err.ToString()); }
                WinFormsUtil.Alert("File Decompressed!", path);
            }
            if (dr == DialogResult.No)
            {
                LZSS.Compress(path, Path.Combine(Directory.GetParent(path).FullName, Path.GetFileNameWithoutExtension(path).Replace("_dec", "") + ".lz"));
                WinFormsUtil.Alert("File Compressed!", path);
            }
            Interlocked.Decrement(ref threads);
        }).Start();
    }

    private void Menu_SMDH_Click(object sender, EventArgs e)
    {
        new Icon().ShowDialog();
    }

    private void Menu_Shuffler_Click(object sender, EventArgs e)
    {
        new Shuffler().ShowDialog();
    }

    // GARC Requests
    internal static string GetGARCFileName(string requestedGARC, int lang)
    {
        var garc = Config.GetGARCReference(requestedGARC);
        if (garc.LanguageVariant)
            garc = garc.GetRelativeGARC(lang);

        return garc.Reference;
    }

    private bool GetGARC(string infile, string outfolder, bool PB, bool bypassExt = false)
    {
        if (skipBoth && Directory.Exists(outfolder))
        {
            UpdateStatus("Skipped - Exists!", false);
            Interlocked.Decrement(ref threads);
            return true;
        }
        try
        {
            bool success = GarcUtil.UnpackGARC(infile, outfolder, bypassExt, PB ? pBar1 : null, L_Status, true);
            UpdateStatus(string.Format(success ? "Success!" : "Failed!"), false);
            Interlocked.Decrement(ref threads);
            return success;
        }
        catch (Exception e) { WinFormsUtil.Error("Could not get the GARC:", e.ToString()); Interlocked.Decrement(ref threads); return false; }
    }

    private bool SetGARC(string outfile, string infolder, int padBytes, bool PB)
    {
        if (skipBoth || (ModifierKeys == Keys.Control && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Cancel writing data back to GARC?") == DialogResult.Yes))
        { Interlocked.Decrement(ref threads); UpdateStatus("Aborted!", false); return false; }

        try
        {
            bool success = GarcUtil.PackGARC(infolder, outfile, Config.GARCVersion, padBytes, PB ? pBar1 : null, L_Status, true);
            Interlocked.Decrement(ref threads);
            UpdateStatus(string.Format(success ? "Success!" : "Failed!"), false);
            return success;
        }
        catch (Exception e) { WinFormsUtil.Error("Could not set the GARC back:", e.ToString()); Interlocked.Decrement(ref threads); return false; }
    }

    private void ThreadGet(string infile, string outfolder, bool PB = true, bool bypassExt = false)
    {
        Interlocked.Increment(ref threads);
        if (Directory.Exists(outfolder))
        {
            try { Directory.Delete(outfolder, true); }
            catch { }
        }

        new Thread(() => GetGARC(infile, outfolder, PB, bypassExt)).Start();
    }

    private void ThreadSet(string outfile, string infolder, int padBytes, bool PB = true)
    {
        Interlocked.Increment(ref threads);
        new Thread(() => SetGARC(outfile, infolder, padBytes, PB)).Start();
    }

    // Update RichTextBox
    private void UpdateStatus(string status, bool preBreak = true)
    {
        string newtext = (preBreak ? Environment.NewLine : "") + status;
        try
        {
            if (RTB_Status.InvokeRequired)
            {
                RTB_Status.Invoke((MethodInvoker)delegate
                {
                    RTB_Status.AppendText(newtext);
                    RTB_Status.SelectionStart = RTB_Status.Text.Length;
                    RTB_Status.ScrollToCaret();
                    L_Status.Text = RTB_Status.Lines[^1].Split([" @"], StringSplitOptions.None)[0];
                });
            }
            else
            {
                RTB_Status.AppendText(newtext);
                RTB_Status.SelectionStart = RTB_Status.Text.Length;
                RTB_Status.ScrollToCaret();
                L_Status.Text = RTB_Status.Lines[^1].Split([" @"], StringSplitOptions.None)[0];
            }
        }
        catch { }
    }

    private void ResetStatus()
    {
        try
        {
            if (L_Status.InvokeRequired)
            {
                L_Status.Invoke((MethodInvoker)(() => L_Status.Text = ""));
            }
            else
            {
                L_Status.Text = "";
            }
        }
        catch { }
    }

    private void SetInt32SeedToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Reseed RNG?", "If yes, copy the 32 bit (not hex) integer seed to the clipboard before hitting Yes."))
            return;

        string val = string.Empty;
        try { val = Clipboard.GetText(); }
        catch { }
        if (int.TryParse(val, out int seed))
        {
            Util.ReseedRand(seed);
            WinFormsUtil.Alert($"Reseeded RNG to seed: {seed}");
            return;
        }
        WinFormsUtil.Alert("Unable to set seed.");
    }
}
