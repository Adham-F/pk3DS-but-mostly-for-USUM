using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;
using System.Text.Json;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures.PersonalInfo;
using pk3DS.Core.Randomizers;

namespace pk3DS.WinForms;

public partial class PersonalEditor7 : Form
{
    private int[][] vanillaStats;
    
    public byte[][] Learnsets => learnsets;
    public byte[][] EggMoves => eggmoves;
    public byte[][] EvolutionFiles => evolutionFiles;
    private byte[][] learnsets;
    private byte[][] eggmoves;
    private byte[][] evolutionFiles;
    public PersonalEditor7(byte[][] infiles, byte[][] learnsets, byte[][] eggmoves, byte[][] evolutionFiles)
    {
        InitializeComponent();
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
        this.learnsets = learnsets;
        this.eggmoves = eggmoves;
        this.evolutionFiles = evolutionFiles;
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Closing);
        RTB_Changelog.ReadOnly = true;
        RTB_Changelog.Dock = DockStyle.Fill;
        helditem_boxes = [CB_HeldItem1, CB_HeldItem2, CB_HeldItem3];
        ability_boxes = [CB_Ability1, CB_Ability2, CB_Ability3];
        typing_boxes = [CB_Type1, CB_Type2];
        eggGroup_boxes = [CB_EggGroup1, CB_EggGroup2];
        byte_boxes = [TB_BaseHP, TB_BaseATK, TB_BaseDEF, TB_BaseSPA, TB_BaseSPD, TB_BaseSPE, TB_Gender, TB_HatchCycles, TB_Friendship, TB_CatchRate, TB_CallRate,
        ];
        ev_boxes = [TB_HPEVs, TB_ATKEVs, TB_DEFEVs, TB_SPEEVs, TB_SPAEVs, TB_SPDEVs];
        rstat_boxes = [CHK_rHP, CHK_rATK, CHK_rDEF, CHK_rSPA, CHK_rSPD, CHK_rSPE];
        files = (byte[][])infiles.Clone();
        if (Main.Config.Generation == 7 && files.Length > 0)
        {
            // Strip the Master Table (last file) so 'files' only contains Pokemon data
            Array.Resize(ref files, files.Length - 1);
        }
        originalFiles = files.Select(a => (byte[])a.Clone()).ToArray(); // Snapshots YOUR custom ROM data
        
        foreach (var tb in byte_boxes) tb.TextAlign = HorizontalAlignment.Center;
        foreach (var tb in ev_boxes) tb.TextAlign = HorizontalAlignment.Center;
        TB_BST.TextAlign = TB_BaseExp.TextAlign = TB_Height.TextAlign = TB_Weight.TextAlign = 
        TB_FormeCount.TextAlign = TB_FormeSprite.TextAlign = TB_Stage.TextAlign = HorizontalAlignment.Center;

        // Bind dynamic updates for the Stat Diff calculator
        TB_BaseHP.TextChanged += UpdateDynamicDiff;
        TB_BaseATK.TextChanged += UpdateDynamicDiff;
        TB_BaseDEF.TextChanged += UpdateDynamicDiff;
        TB_BaseSPA.TextChanged += UpdateDynamicDiff;
        TB_BaseSPD.TextChanged += UpdateDynamicDiff;
        TB_BaseSPE.TextChanged += UpdateDynamicDiff;

        species[0] = "---";
        abilities[0] = items[0] = moves[0] = "";
        var altForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        entryNames = Main.Config.Personal.GetPersonalEntryList(altForms, species, Main.Config.MaxSpeciesID, out baseForms, out formVal);
        TMs = TMEditor7.GetTMHMList();

        B_GenerateDiff.Click += (s, e) => GenerateFullChangelog();
        B_CopyPage1.Click += B_CopyPage1_Click;
        B_PastePage1.Click += B_PastePage1_Click;
        B_MaxCatch.Click += B_MaxCatch_Click;
        B_MaxCatchAll.Click += B_MaxCatchAll_Click;
        B_ZeroHatch.Click += B_ZeroHatch_Click;
        B_ZeroHatchAll.Click += B_ZeroHatchAll_Click;
        B_JumpLevelUp.Click += (s, e) => {
            SaveEntry();
            var ed = new LevelUpEditor7(learnsets) { StartSpecies = CB_Species.SelectedIndex };
            WinFormsUtil.ApplyTheme(ed);
            ed.ShowDialog();
            ReadEntry();
        };
        B_JumpEggMoves.Click += (s, e) => {
            SaveEntry();
            var ed = new EggMoveEditor7(eggmoves) { StartSpecies = CB_Species.SelectedIndex };
            WinFormsUtil.ApplyTheme(ed);
            ed.ShowDialog();
            ReadEntry();
        };
        TC_Pokemon.SelectedIndexChanged += (s, e) =>
        {
            string tab = TC_Pokemon.SelectedTab.Text;
            PB_MonSprite.Visible = TC_Pokemon.SelectedIndex == 0 || tab.Contains("Tutor");
            if (tab == "Changelog")
            {
                SaveEntry();
                GenerateFullChangelog();
            }
        };
        CB_ZItem.SelectedIndexChanged += (s, e) => { if (!reading) SaveEntry(); };
        CB_ZBaseMove.SelectedIndexChanged += (s, e) => { if (!reading) SaveEntry(); };
        CB_ZMove.SelectedIndexChanged += (s, e) => { if (!reading) SaveEntry(); };
        B_SaveCurrent.Click += (s, e) => { SaveEntry(); WinFormsUtil.Alert("Current Pokémon saved to internal buffer."); };

        RegisterAutosave(TP_General.Controls);
        NUD_TutorBase = new NumericUpDown
        {
            Minimum = 0x28, Maximum = 0x2F, Hexadecimal = true, 
            Value = 0x29, Location = new Point(370, 370), Size = new Size(50, 20),
            Enabled = false // Locked — ASM patch expects base 0x29
        };
        L_TutorBase = new Label { Text = "Base:", Location = new Point(300, 373), Size = new Size(70, 20) };
        B_AlignTutors = new Button { Text = "Align", Location = new Point(430, 368), Size = new Size(50, 24) };
        L_ContinuousTutors = new CheckBox { Text = "Continuous Mapping (ASM)", Location = new Point(490, 370), Size = new Size(200, 20), Checked = true, Enabled = false };

        TP_MoveTutors.Controls.Add(L_TutorBase);
        TP_MoveTutors.Controls.Add(NUD_TutorBase);
        TP_MoveTutors.Controls.Add(B_AlignTutors);
        TP_MoveTutors.Controls.Add(L_ContinuousTutors);
        B_AlignTutors.Click += B_AlignTutors_Click;
        L_NewTutors.Visible = false;
        L_Special.Visible = false; // Get rid of Special Tutors text
        CLB_NewTutors.Visible = false;
        CLB_BeachTutors.Location = new Point(300, 35);
        CLB_BeachTutors.Width = 490;
        CLB_BeachTutors.Height = 275;
        CLB_BeachTutors.MultiColumn = true;
        CLB_BeachTutors.ColumnWidth = 150;

        L_DebugTutors = new RichTextBox 
        { 
            ReadOnly = true, 
            BackColor = Color.Black, 
            ForeColor = Color.Yellow, 
            Font = new Font("Consolas", 8),
            Location = new Point(300, 315), 
            Size = new Size(490, 45),
            BorderStyle = BorderStyle.None
        };
        TP_MoveTutors.Controls.Add(L_DebugTutors);

        Setup();
        LoadVanillaStats();
        if (CB_Species.Items.Count > 1) CB_Species.SelectedIndex = 1;
        RandSettings.GetFormSettings(this, TP_Randomizer.Controls);
    }



    private void RegisterAutosave(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            if (c is ComboBox cb) cb.SelectedIndexChanged += (s, e) => { if (!reading) SaveEntry(false); };
            else if (c is CheckBox chk) chk.CheckedChanged += (s, e) => { if (!reading) SaveEntry(false); };
            else if (c is NumericUpDown nud) nud.ValueChanged += (s, e) => { if (!reading) SaveEntry(false); };
            
            if (c.HasChildren) RegisterAutosave(c.Controls);
        }
    }
    // Removed redundant handler
    #region Global Variables
    public byte[][] Files => files;
    private byte[][] files;

    private readonly string[] items = Main.Config.GetText(TextName.ItemNames);
    private readonly string[] moves = Main.Config.GetText(TextName.MoveNames);
    private string[] species = Main.Config.GetText(TextName.SpeciesNames);
    private readonly string[] abilities = Main.Config.GetText(TextName.AbilityNames);
    //private readonly string[] forms = Main.Config.GetText(TextName.Forms);
    private readonly string[] types = Main.Config.GetText(TextName.Types);

    private readonly ComboBox[] helditem_boxes;
    private readonly ComboBox[] ability_boxes;
    private readonly ComboBox[] typing_boxes;
    private readonly ComboBox[] eggGroup_boxes;

    private readonly MaskedTextBox[] byte_boxes;
    private readonly MaskedTextBox[] ev_boxes;
    private readonly CheckBox[] rstat_boxes;

    private readonly string[] eggGroups = ["---", "Monster", "Water 1", "Bug", "Flying", "Field", "Fairy", "Grass", "Human-Like", "Water 3", "Mineral", "Amorphous", "Water 2", "Ditto", "Dragon", "Undiscovered",
    ];
    private readonly string[] EXPGroups = ["Medium-Fast", "Erratic", "Fluctuating", "Medium-Slow", "Fast", "Slow"];
    private readonly string[] colors = ["Red", "Blue", "Yellow", "Green", "Black", "Brown", "Purple", "Gray", "White", "Pink",
    ];
    private readonly ushort[] tutormoves = [520, 519, 518, 338, 307, 308, 434, 620];

    internal static readonly int[] Tutors_USUM =
    [
        450, 343, 162, 530, 324, 442, 402, 529, 340, 067, 441, 253, 009, 007, 008,
        277, 335, 414, 492, 356, 393, 334, 387, 276, 527, 196, 401, 428, 406, 304, 231,
        020, 173, 282, 235, 257, 272, 215, 366, 143, 220, 202, 409, 264, 351, 352,
        380, 388, 180, 495, 270, 271, 478, 472, 283, 200, 278, 289, 446, 285,
        477, 502, 432, 710, 707, 675, 673,
    ];

    // Canonical USUM Vanilla Bit Order (Bits 0-66) - Matches Legacy pk3DS Mapping
    private static readonly int[] Tutors_USUM_Vanilla_Bits =
    [
        450, 343, 162, 530, 324, 442, 402, 529, 340, 067, 441, 253, 009, 007, 008, // 0-14
        277, 335, 414, 492, 356, 393, 334, 387, 276, 527, 196, 401, 428, 406, 304, 231, // 15-30
        020, 173, 282, 235, 257, 272, 215, 366, 143, 220, 202, 409, 264, 351, 352, // 31-45
        380, 388, 180, 495, 270, 271, 478, 472, 283, 200, 278, 289, 446, 285, // 46-59
        477, 502, 432, 710, 707, 675, 673 // 60-66
    ];


    private int[] Tutors_USUM_Lengths = [15, 16, 15, 14, 7];

    private int[] baseForms, formVal;
    private string[] entryNames;
    private readonly ushort[] TMs;
    private int entry = -1;
    private NumericUpDown NUD_TutorBase;
    private Button B_AlignTutors;
    private Label L_TutorBase;
    private CheckBox L_ContinuousTutors;
    private RichTextBox L_DebugTutors;
    private int[] Tutors_Beach_Map;
    private static bool[] ClipboardTMs;
    private static bool[] ClipboardTutors;
    private static bool[] ClipboardBeachTutors;
    private readonly byte[][] originalFiles;
    #endregion
    private bool reading;
    private int tutorBase = 0x29;
    private void Setup()
    {
        reading = true;
        if (Main.Config.USUM)
        {
            tutorBase = 0x29;
            NUD_TutorBase.Value = tutorBase;
        }

        CLB_TM.Items.Clear();
        CB_Species.Items.Clear();
        CLB_MoveTutors.Items.Clear();
        CLB_BeachTutors.Items.Clear();

        if (TMs.Length == 0) // No ExeFS to grab TMs from.
        {
            for (int i = 1; i <= 128; i++)
                CLB_TM.Items.Add($"TM{i:00}{(i > 100 ? " (Extra Toggle)" : "")}");
        }
        else // Use TM moves.
        {
            for (int i = 1; i <= 128; i++)
            {
                string name = (i - 1 < TMs.Length && TMs[i - 1] < moves.Length) ? moves[TMs[i - 1]] : "---";
                CLB_TM.Items.Add($"TM{i:00} {name}");
            }
        }
        foreach (ushort m in tutormoves)
            CLB_MoveTutors.Items.Add(moves[m]);

        for (int i = 0; i < entryNames.Length; i++)
            CB_Species.Items.Add($"{entryNames[i]} - {i:000}");

        CB_Species.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        CB_Species.AutoCompleteSource = AutoCompleteSource.ListItems;

        foreach (ComboBox cb in helditem_boxes)
        {
            cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cb.AutoCompleteSource = AutoCompleteSource.ListItems;
            cb.Items.AddRange(items);
        }

        CB_ZItem.Items.AddRange(items);
        CB_ZItem.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        CB_ZItem.AutoCompleteSource = AutoCompleteSource.ListItems;

        CB_ZBaseMove.Items.AddRange(moves);
        CB_ZBaseMove.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        CB_ZBaseMove.AutoCompleteSource = AutoCompleteSource.ListItems;

        CB_ZMove.Items.AddRange(moves);
        CB_ZMove.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        CB_ZMove.AutoCompleteSource = AutoCompleteSource.ListItems;

        foreach (ComboBox cb in ability_boxes)
        {
            cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cb.AutoCompleteSource = AutoCompleteSource.ListItems;
            cb.Items.AddRange(abilities);
        }

        foreach (ComboBox cb in typing_boxes)
        {
            cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cb.AutoCompleteSource = AutoCompleteSource.ListItems;
            cb.Items.AddRange(types);
        }

        foreach (ComboBox cb in eggGroup_boxes)
        {
            cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cb.AutoCompleteSource = AutoCompleteSource.ListItems;
            cb.Items.AddRange(eggGroups);
        }

        CB_Color.Items.AddRange(colors);
        CB_Color.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        CB_Color.AutoCompleteSource = AutoCompleteSource.ListItems;

        CB_EXPGroup.Items.AddRange(EXPGroups);
        CB_EXPGroup.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        CB_EXPGroup.AutoCompleteSource = AutoCompleteSource.ListItems;
        if (Main.Config.USUM)
        {
            string croPath = Path.Combine(Main.RomFSPath, "Shop.cro");
            var tutorData = TutorEditor7.GetUSUMTutorData(croPath, Tutors_USUM);
            
            CLB_BeachTutors.Items.Clear();
            List<int> beachMap = [];
            bool unified = L_ContinuousTutors is CheckBox { Checked: true };
            int baseOfs = (int)NUD_TutorBase.Value;
            int startBit = (baseOfs - 0x28) * 32;

            // 1. Add Special Tutors (Always Bits 0-7 / Word 28)
            if (unified)
            {
                for (int i = 0; i < tutormoves.Length; i++)
                {
                    int moveID = tutormoves[i];
                    string name = moveID < moves.Length ? moves[moveID] : $"Move {moveID}";
                    CLB_BeachTutors.Items.Add($"+ [{moveID:000}] {name}");
                    beachMap.Add(i); // Absolute bits 0-7
                }
            }

            // 2. Add Vanilla Beach Tutors (Standard Mapping)
            for (int i = 0; i < Tutors_USUM.Length; i++)
            {
                int moveID = Tutors_USUM[i];
                string name = moveID < moves.Length ? moves[moveID] : $"Move {moveID}";
                CLB_BeachTutors.Items.Add($"[{moveID:000}] {name}");
                
                // Absolute bit calculation: startBit + i
                beachMap.Add(startBit + i);
            }

            // 3. Add Expansion Moves (from Bits 67+ relative to startBit)
            var vanillaSet = new HashSet<int>(Tutors_USUM);
            var shopTutors = tutorData.moves;
            int nextExpIndex = 67;
            for (int i = 0; i < shopTutors.Length; i++)
            {
                int moveID = shopTutors[i];
                if (!vanillaSet.Contains(moveID))
                {
                    string name = moveID < moves.Length ? moves[moveID] : $"Move {moveID}";
                    CLB_BeachTutors.Items.Add($"* [{moveID:000}] {name}");
                    beachMap.Add(startBit + nextExpIndex++);
                }
            }
            Tutors_Beach_Map = beachMap.ToArray();
        }

        CLB_BeachTutors.ItemCheck += (s, e) => { if (!reading) BeginInvoke(new Action(() => SaveEntry(false))); };
        CLB_NewTutors.ItemCheck += (s, e) => { if (!reading) BeginInvoke(new Action(() => SaveEntry(false))); };
        CLB_MoveTutors.ItemCheck += (s, e) => { if (!reading) BeginInvoke(new Action(() => SaveEntry(false))); };
        CLB_TM.ItemCheck += (s, e) => { if (!reading) BeginInvoke(new Action(() => SaveEntry(false))); };

        // toggle usum content
        CHK_BeachTutors.Checked = CHK_BeachTutors.Visible =
            CLB_BeachTutors.Visible = CLB_BeachTutors.Enabled = L_BeachTutors.Visible = Main.Config.USUM;
        
        reading = false;
        if (entry > -1 && entry < CB_Species.Items.Count) CB_Species.SelectedIndex = entry;
    }

    private void CB_Species_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (entry > -1 && !dumping) SaveEntry();
        entry = CB_Species.SelectedIndex;
        ReadEntry();
    }

    private void B_InsertForm_Click(object sender, EventArgs e)
    {
        if (entry < 1) return;
        if (entry > -1) SaveEntry(); // save current entry before insertion
        
        using var form = new FormInsertion(files, evolutionFiles ?? new byte[files.Length][], learnsets ?? new byte[files.Length][], eggmoves ?? new byte[files.Length][], species, entryNames, baseForms, formVal);
        WinFormsUtil.ApplyTheme(form);
        if (form.ShowDialog() != DialogResult.OK)
            return;

        // ── Phase 1: Update in-memory arrays ──
        files = form.ResultPersonal;
        evolutionFiles = form.ResultEvolution;
        learnsets = form.ResultLevelUp;
        eggmoves = form.ResultEggMoves;

        // ── Phase 2: Persist to GARCs immediately ──
        try
        {
            // Save Personal GARC (with reconstructed Master Table)
            byte[] masterTable = RebuildMasterTable(files);
            byte[][] personalWithTable = new byte[files.Length + 1][];
            for (int i = 0; i < files.Length; i++) personalWithTable[i] = files[i];
            personalWithTable[files.Length] = masterTable;
            
            Main.Config.GARCPersonal.Files = personalWithTable;
            Main.Config.GARCPersonal.Save();

            // Save Evolution GARC
            var evoGarc = Main.Config.GetGARCData("evolution");
            evoGarc.Files = evolutionFiles;
            evoGarc.Save();

            // Save Learnset GARC
            Main.Config.GARCLearnsets.Files = learnsets;
            Main.Config.GARCLearnsets.Save();

            // Save EggMove GARC
            var eggGarc = Main.Config.GetGARCData("eggmove");
            if (eggGarc != null)
            {
                eggGarc.Files = eggmoves;
                eggGarc.Save();
            }

            // Memory cleanup after heavy GARC operations
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Failed to save GARCs after insertion.", ex.Message);
        }

        // ── Phase 3: Rebuild internal tables (like reference tool's update_species_list) ──
        Main.Config.InitializePersonal();
        Main.Config.InitializeLearnset();
        
        // Refresh names reference from config (it was expanded in FormInsertion)
        species = Main.Config.GetText(TextName.SpeciesNames);

        // Rebuild the global species stat table from the updated personal files
        Main.Config.Personal.Table = files.Select(f => PersonalTable.GetInfo(f, Main.Config.Version)).ToArray();
        var altForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        entryNames = Main.Config.Personal.GetPersonalEntryList(altForms, species, Main.Config.MaxSpeciesID, out baseForms, out formVal);

        // ── Phase 4: Resort if requested ──
        if (form.ShouldResort) ResortFileStructure();

        // ── Phase 5: Refresh the UI ──
        Setup();
        WinFormsUtil.Alert("Form insertion complete!", "All GARCs saved. Internal tables refreshed.");
    }

    private void RefreshSpeciesList()
    {
        // Re-calculate the list based on current game info
        var altForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        var res = Main.Config.Personal.GetPersonalEntryList(altForms, species, Main.Config.MaxSpeciesID, out var bf, out var fv);
        
        CB_Species.Items.Clear();
        CB_Species.Items.AddRange(res.Select((n, i) => $"{n} - {i:000}").ToArray());
        CB_Species.SelectedIndex = entry;
    }

    private void InsertNewForm(int index, int sourceIndex)
    {
        // Expand all relevant data arrays
        ExpandArray(ref files, index, sourceIndex);
        ExpandArray(ref learnsets, index, sourceIndex);
        ExpandArray(ref eggmoves, index, sourceIndex);
        ExpandArray(ref evolutionFiles, index, sourceIndex);
        
        // Mark for saving
        // This would normally involve writing back to the GARCs
    }

    private static void ExpandArray<T>(ref T[][] array, int index, int sourceIndex)
    {
        var list = array.ToList();
        list.Insert(index, (T[])array[sourceIndex].Clone());
        array = list.ToArray();
    }

    private void B_SortForms_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Perform a global species-contiguous resort? This will shift file pointers for Models, Animations, Cries, and Sprites.") != DialogResult.Yes)
            return;

        ResortFileStructure();
    }

    private void ResortFileStructure()
    {
        if (Main.Config.Version is not (GameVersion.US or GameVersion.UM or GameVersion.USUM))
        {
            WinFormsUtil.Alert("Resort logic currently only optimized for USUM.");
            return;
        }

        // 1. Calculate Permutation
        var order = Enumerable.Range(0, entryNames.Length)
            .OrderBy(i => baseForms[i])
            .ThenBy(i => i)
            .ToArray();

        int[] oldToNew = new int[entryNames.Length];
        for (int i = 0; i < order.Length; i++) oldToNew[order[i]] = i;

        // 2. Update Main Tables in Memory
        files = Reorder(files, order);
        learnsets = Reorder(learnsets, order);
        eggmoves = Reorder(eggmoves, order);
        evolutionFiles = Reorder(evolutionFiles, order);

        // 3. Realignment: All form pointers must be updated to their new indices
        for (int i = 0; i < files.Length; i++)
        {
            int oldPtr = BitConverter.ToUInt16(files[i], 0x1C);
            if (oldPtr == 0) continue;
            
            // Map old pointer to new pointer using oldToNew table
            int newPtr = oldToNew[oldPtr];
            byte[] ptrBytes = BitConverter.GetBytes((ushort)newPtr);
            files[i][0x1C] = ptrBytes[0];
            files[i][0x1D] = ptrBytes[1];
        }

        // 4. Update Global Config and Refresh Info Objects
        Main.Config.Personal.Table = files.Select(f => PersonalTable.GetInfo(f, Main.Config.Version)).ToArray();
        
        // Refresh names and base forms logic
        var altForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        entryNames = Main.Config.Personal.GetPersonalEntryList(altForms, species, Main.Config.MaxSpeciesID, out baseForms, out formVal);

        // 5. Persist resorted data to disk
        try
        {
            byte[][] personalWithTable = [.. files, RebuildMasterTable(files)];
            Main.Config.GARCPersonal.Files = personalWithTable;
            Main.Config.GARCPersonal.Save();

            Main.Config.GARCLearnsets.Files = learnsets;
            Main.Config.GARCLearnsets.Save();

            var eggGarc = Main.Config.GetGARCData("eggmove");
            if (eggGarc != null) { eggGarc.Files = eggmoves; eggGarc.Save(); }

            var evoGarc = Main.Config.GetGARCData("evolution");
            if (evoGarc != null) { evoGarc.Files = evolutionFiles; evoGarc.Save(); }
            
            // Memory cleanup after heavy resort
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Failed to save resorted GARCs.", ex.Message);
        }

        Setup(); // Refresh UI with new order
        WinFormsUtil.Alert("Global resort and repointing complete. All GARCs have been synchronized.");
    }

    private T[] Reorder<T>(T[] array, int[] order)
    {
        if (array == null) return null;
        var newArray = new T[array.Length];
        for (int i = 0; i < Math.Min(order.Length, array.Length); i++) 
        {
            int idx = order[i];
            if (idx >= 0 && idx < array.Length)
                newArray[i] = array[idx];
        }
        return newArray;
    }

    private void SyncGARC(string path, int[] order)
    {
        if (!File.Exists(path)) return;
        var memgarc = new GARC.MemGARC(File.ReadAllBytes(path));
        var oldFiles = memgarc.Files;
        var newFiles = new byte[oldFiles.Length][];
        
        // Only reorder within bounds of both order array and oldFiles
        for (int i = 0; i < oldFiles.Length; i++)
        {
            if (i < order.Length && order[i] >= 0 && order[i] < oldFiles.Length) 
                newFiles[i] = oldFiles[order[i]];
            else 
                newFiles[i] = oldFiles[i];
        }
        
        memgarc.Files = newFiles;
        File.WriteAllBytes(path, memgarc.Data);
    }

    private void B_Import_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "JSON File|*.json" };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        
        try
        {
            string json = File.ReadAllText(ofd.FileName);
            var imported = JsonSerializer.Deserialize<PersonalInfoSM[]>(json);
            if (imported != null && imported.Length > 0)
            {
                for (int i = 1; i < files.Length && i < imported.Length; i++)
                {
                    if (imported[i] != null) 
                    {
                        Main.SpeciesStat[i] = imported[i];
                        files[i] = imported[i].Write();
                    }
                }
                ReadEntry();
                WinFormsUtil.Alert($"Successfully imported {imported.Length} personal entries from JSON.");
            }
        }
        catch (Exception ex) { WinFormsUtil.Error("Failed to import JSON:", ex.Message); }
    }

    private void ByteLimiter(object sender, EventArgs e)
    {
        if (sender is not MaskedTextBox mtb)
            return;
        _ = int.TryParse(mtb.Text, out int val);
        if (Array.IndexOf(byte_boxes, mtb) > -1 && val > 255)
            mtb.Text = "255";
        else if (Array.IndexOf(ev_boxes, mtb) > -1 && val > 3)
            mtb.Text = "3";
    }

    private PersonalInfo pkm;

    private void ReadInfo()
    {
        reading = true;
        pkm = Main.SpeciesStat[entry];

        TB_BaseHP.Text = pkm.HP.ToString("000");
        TB_BaseATK.Text = pkm.ATK.ToString("000");
        TB_BaseDEF.Text = pkm.DEF.ToString("000");
        TB_BaseSPE.Text = pkm.SPE.ToString("000");
        TB_BaseSPA.Text = pkm.SPA.ToString("000");
        TB_BaseSPD.Text = pkm.SPD.ToString("000");
        TB_HPEVs.Text = pkm.EV_HP.ToString("0");
        TB_ATKEVs.Text = pkm.EV_ATK.ToString("0");
        TB_DEFEVs.Text = pkm.EV_DEF.ToString("0");
        TB_SPEEVs.Text = pkm.EV_SPE.ToString("0");
        TB_SPAEVs.Text = pkm.EV_SPA.ToString("0");
        TB_SPDEVs.Text = pkm.EV_SPD.ToString("0");

        CB_Type1.SelectedIndex = pkm.Types[0];
        CB_Type2.SelectedIndex = pkm.Types[1];

        TB_CatchRate.Text = pkm.CatchRate.ToString("000");
        TB_Stage.Text = pkm.EvoStage.ToString("0");

        CB_HeldItem1.SelectedIndex = pkm.Items[0];
        CB_HeldItem2.SelectedIndex = pkm.Items[1];
        CB_HeldItem3.SelectedIndex = pkm.Items[2];

        TB_Gender.Text = pkm.Gender.ToString("000");
        TB_HatchCycles.Text = pkm.HatchCycles.ToString("000");
        TB_Friendship.Text = pkm.BaseFriendship.ToString("000");

        CB_EXPGroup.SelectedIndex = pkm.EXPGrowth;

        CB_EggGroup1.SelectedIndex = pkm.EggGroups[0];
        CB_EggGroup2.SelectedIndex = pkm.EggGroups[1];

        CB_Ability1.SelectedIndex = pkm.Abilities[0];
        CB_Ability2.SelectedIndex = pkm.Abilities[1];
        CB_Ability3.SelectedIndex = pkm.Abilities[2];

        TB_FormeCount.Text = pkm.FormeCount.ToString("000");
        TB_FormeSprite.Text = pkm.FormeSprite.ToString("000");

        TB_RawColor.Text = pkm.Color.ToString("000");
        CB_Color.SelectedIndex = pkm.Color & 0xF;

        TB_BaseExp.Text = pkm.BaseEXP.ToString("000");
        TB_BST.Text = pkm.BST.ToString("000");

        TB_Height.Text = ((decimal)pkm.Height / 100).ToString("00.00");
        TB_Weight.Text = ((decimal)pkm.Weight / 10).ToString("000.0");

        for (int i = 0; i < CLB_TM.Items.Count && i < pkm.TMHM.Length; i++)
            CLB_TM.SetItemChecked(i, pkm.TMHM[i]);

        PersonalInfoSM sm = (PersonalInfoSM)pkm;
        TB_CallRate.Text = sm.EscapeRate.ToString("000");
        TB_CallRate.TextAlign = HorizontalAlignment.Center;
        CB_ZItem.SelectedIndex = sm.SpecialZ_Item == 65535 || sm.SpecialZ_Item >= CB_ZItem.Items.Count ? 0 : sm.SpecialZ_Item;
        CB_ZBaseMove.SelectedIndex = sm.SpecialZ_BaseMove == 65535 || sm.SpecialZ_BaseMove >= CB_ZBaseMove.Items.Count ? 0 : sm.SpecialZ_BaseMove;
        CB_ZMove.SelectedIndex = sm.SpecialZ_ZMove == 65535 || sm.SpecialZ_ZMove >= CB_ZMove.Items.Count ? 0 : sm.SpecialZ_ZMove;
        CHK_Variant.Checked = sm.LocalVariant;

        CLB_MoveTutors.Visible = L_NewTutors.Visible = L_Special.Visible = !(L_ContinuousTutors is CheckBox { Checked: true });
        for (int i = 0; i < CLB_MoveTutors.Items.Count && i < 32; i++)
            CLB_MoveTutors.SetItemChecked(i, sm.TutorFlags[i]);

        // Beach Tutors (Standard USUM Island-based mapping)
        for (int i = 0; i < CLB_BeachTutors.Items.Count; i++)
        {
            int bitPos = GetUSUMBitPos(i);
            CLB_BeachTutors.SetItemChecked(i, bitPos >= 0 && bitPos < sm.TutorFlags.Length && sm.TutorFlags[bitPos]);
        }
        ReadDexEntry();
        UpdateDebugLabel(sm);
        reading = false;
    }

    private void UpdateDebugLabel(PersonalInfoSM sm)
    {
        if (L_DebugTutors == null) return;
        byte[] data = sm.Write();
        byte[] tutorData = data.Skip(0x38).Take(20).ToArray();
        string hex = "0x38:\n" + BitConverter.ToString(tutorData).Replace("-", " ");
        L_DebugTutors.Text = hex;
    }

    private int GetUSUMBitPos(int index)
    {
        // Use the absolute map calculated during Setup()
        if (Tutors_Beach_Map != null && index < Tutors_Beach_Map.Length)
            return Tutors_Beach_Map[index];
            
        return -1;
    }

    private void ReadDexEntry()
    {
        var dexBox = RTB_DexEntry;
        if (dexBox != null)
        {
            dexBox.Text = "";
            if (Main.Config.USUM || Main.Config.SM)
            {
                var dex1 = Main.Config.GetText(TextName.PokedexEntry1);
                var dex2 = Main.Config.GetText(TextName.PokedexEntry2);
                int dexIdx = entry;
                if (entry > Main.Config.MaxSpeciesID) dexIdx = baseForms[entry];

                if (dexIdx < dex1.Length)
                {
                    dexBox.Text = dex1[dexIdx].Replace("\\n", "\n").Replace("\\r", "\n");
                    if (dexIdx < dex2.Length && !string.IsNullOrWhiteSpace(dex2[dexIdx]) && dex2[dexIdx] != dex1[dexIdx])
                        dexBox.Text += "\n\n" + dex2[dexIdx].Replace("\\n", "\n").Replace("\\r", "\n");
                }
            }
        }
        reading = false;
    }

    private void ReadEntry()
    {
        ReadInfo();

        if (dumping) return;
        int s = baseForms[entry];
        int f = formVal[entry];
        if (entry <= Main.Config.MaxSpeciesID)
            s = entry;
        var rawImg = WinFormsUtil.GetSprite(s, f, 0, 0, Main.Config);
        var bigImg = new Bitmap(rawImg.Width * 2, rawImg.Height * 2);
        for (int x = 0; x < rawImg.Width; x++)
        {
            for (int y = 0; y < rawImg.Height; y++)
            {
                Color c = rawImg.GetPixel(x, y);
                bigImg.SetPixel(2 * x, 2 * y, c);
                bigImg.SetPixel((2 * x) + 1, 2 * y, c);
                bigImg.SetPixel(2 * x, (2 * y) + 1, c);
                bigImg.SetPixel((2 * x) + 1, (2 * y) + 1, c);
            }
        }
        PB_MonSprite.Image = bigImg;
    }

    private void SavePersonal()
    {
        pkm.HP = Convert.ToByte(TB_BaseHP.Text);
        pkm.ATK = Convert.ToByte(TB_BaseATK.Text);
        pkm.DEF = Convert.ToByte(TB_BaseDEF.Text);
        pkm.SPE = Convert.ToByte(TB_BaseSPE.Text);
        pkm.SPA = Convert.ToByte(TB_BaseSPA.Text);
        pkm.SPD = Convert.ToByte(TB_BaseSPD.Text);

        pkm.EV_HP = Convert.ToByte(TB_HPEVs.Text);
        pkm.EV_ATK = Convert.ToByte(TB_ATKEVs.Text);
        pkm.EV_DEF = Convert.ToByte(TB_DEFEVs.Text);
        pkm.EV_SPE = Convert.ToByte(TB_SPEEVs.Text);
        pkm.EV_SPA = Convert.ToByte(TB_SPAEVs.Text);
        pkm.EV_SPD = Convert.ToByte(TB_SPDEVs.Text);

        pkm.CatchRate = Convert.ToByte(TB_CatchRate.Text);
        pkm.EvoStage = Convert.ToByte(TB_Stage.Text);

        pkm.Types = [CB_Type1.SelectedIndex, CB_Type2.SelectedIndex];
        pkm.Items = [CB_HeldItem1.SelectedIndex, CB_HeldItem2.SelectedIndex, CB_HeldItem3.SelectedIndex];

        pkm.Gender = Convert.ToByte(TB_Gender.Text);
        pkm.HatchCycles = Convert.ToByte(TB_HatchCycles.Text);
        pkm.BaseFriendship = Convert.ToByte(TB_Friendship.Text);
        pkm.EXPGrowth = (byte)CB_EXPGroup.SelectedIndex;
        pkm.EggGroups = [CB_EggGroup1.SelectedIndex, CB_EggGroup2.SelectedIndex];
        pkm.Abilities = [CB_Ability1.SelectedIndex, CB_Ability2.SelectedIndex, CB_Ability3.SelectedIndex];

        pkm.FormeSprite = Convert.ToUInt16(TB_FormeSprite.Text);
        pkm.FormeCount = Convert.ToByte(TB_FormeCount.Text);
        pkm.Color = (byte)(Convert.ToByte(CB_Color.SelectedIndex) | (Convert.ToByte(TB_RawColor.Text) & 0xF0));
        pkm.BaseEXP = Convert.ToUInt16(TB_BaseExp.Text);

        _ = decimal.TryParse(TB_Height.Text, out var h);
        _ = decimal.TryParse(TB_Weight.Text, out var w);
        pkm.Height = (int)(h * 100);
        pkm.Weight = (int)(w * 10);

        for (int i = 0; i < CLB_TM.Items.Count && i < pkm.TMHM.Length; i++)
            pkm.TMHM[i] = CLB_TM.GetItemChecked(i);

        PersonalInfoSM sm = (PersonalInfoSM)pkm;
        for (int i = 0; i < CLB_MoveTutors.Items.Count && i < 32; i++)
            sm.TutorFlags[i] = CLB_MoveTutors.GetItemChecked(i);

        for (int i = 0; i < CLB_BeachTutors.Items.Count; i++)
        {
            int bitPos = GetUSUMBitPos(i);
            if (bitPos < sm.TutorFlags.Length) sm.TutorFlags[bitPos] = CLB_BeachTutors.GetItemChecked(i);
        }
        UpdateDebugLabel(sm);

        // Log significant changes
        LogChange($"Saved changes for {CB_Species.Text}");
    }

    private void B_AlignTutors_Click(object sender, EventArgs e)
    {
        int newBase = (int)NUD_TutorBase.Value;
        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, 
            "Shift all Pokémon compatibility bits to align with the current Base?\n\n" +
            "YES: Shift bits from 29 -> 28 (Corrects expansion mismatch)\n" +
            "NO: Shift bits from 28 -> 29 (Reverts to vanilla display)\n" +
            "CANCEL: Do nothing.");

        if (dr == DialogResult.Cancel) return;
        int shift = (dr == DialogResult.Yes) ? -32 : 32;

        int count = 0;
        for (int i = 0; i < files.Length && i < Main.SpeciesStat.Length; i++)
        {
            if (files[i] == null || files[i].Length < 0x50) continue;
            var sm = new PersonalInfoSM(files[i]);
            bool[] newBits = new bool[sm.TutorFlags.Length];
            for (int b = 0; b < sm.TutorFlags.Length; b++)
            {
                int oldIdx = b - shift;
                if (oldIdx >= 0 && oldIdx < sm.TutorFlags.Length)
                    newBits[b] = sm.TutorFlags[oldIdx];
            }
            sm.TutorFlags = newBits;
            files[i] = sm.Write();
            Main.SpeciesStat[i] = sm;
            count++;
        }
        tutorBase = newBase;
        ReadInfo();
        WinFormsUtil.Alert("Tutor bits shifted and cache refreshed.", $"Successfully aligned {count} Pokémon.");
    }

    private void LogChange(string text)
    {
        if (RTB_Changelog == null) return;
        RTB_Changelog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }


    private void SaveEntry(bool updateChangelog = true)
    {
        if (entry < 0 || entry >= files.Length) return;
        SavePersonal();
        byte[] edits = pkm.Write();
        files[entry] = edits;
        if (updateChangelog) GenerateFullChangelog();
    }

    private void B_RandomizeCurrent_Click(object sender, EventArgs e)
    {
        if (entry < 0) return;
        var rnd = new PersonalRandomizer(Main.SpeciesStat, Main.Config)
        {
            TypeCount = CB_Type1.Items.Count,
            ModifyCatchRate = CHK_CatchRate.Checked,
            ModifyEggGroup = CHK_EggGroup.Checked,
            ModifyStats = CHK_Stats.Checked,
            ShuffleStats = CHK_Shuffle.Checked,
            StatsToRandomize = rstat_boxes.Select(g => g.Checked).ToArray(),
            ModifyAbilities = CHK_Ability.Checked,
            ModifyLearnsetTM = CHK_TM.Checked,
            ModifyLearnsetHM = false,
            ModifyLearnsetTypeTutors = CHK_Tutors.Checked,
            ModifyLearnsetMoveTutors = Main.Config.USUM && CHK_BeachTutors.Checked,
            ModifyTypes = CHK_Type.Checked,
            ModifyHeldItems = CHK_Item.Checked,
            SameTypeChance = NUD_TypePercent.Value,
            SameEggGroupChance = NUD_Egg.Value,
            StatDeviation = NUD_StatDev.Value,
            AllowWonderGuard = CHK_WGuard.Checked,
        };

        rnd.Randomize(Main.SpeciesStat[entry], entry);
        files[entry] = Main.SpeciesStat[entry].Write();
        ReadEntry();
    }

    private void B_RandomizeAll_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all? Cannot undo.", "Double check Randomization settings in the Enhancements tab.") != DialogResult.Yes)
            return;
        if (entry > -1) SaveEntry();
        // input settings
        var rnd = new PersonalRandomizer(Main.SpeciesStat, Main.Config)
        {
            TypeCount = CB_Type1.Items.Count,
            ModifyCatchRate = CHK_CatchRate.Checked,
            ModifyEggGroup = CHK_EggGroup.Checked,
            ModifyStats = CHK_Stats.Checked,
            ShuffleStats = CHK_Shuffle.Checked,
            StatsToRandomize = rstat_boxes.Select(g => g.Checked).ToArray(),
            ModifyAbilities = CHK_Ability.Checked,
            ModifyLearnsetTM = CHK_TM.Checked,
            ModifyLearnsetHM = false, // no HMs in Gen 7
            ModifyLearnsetTypeTutors = CHK_Tutors.Checked,
            ModifyLearnsetMoveTutors = Main.Config.USUM && CHK_BeachTutors.Checked,
            ModifyTypes = CHK_Type.Checked,
            ModifyHeldItems = CHK_Item.Checked,
            SameTypeChance = NUD_TypePercent.Value,
            SameEggGroupChance = NUD_Egg.Value,
            StatDeviation = NUD_StatDev.Value,
            AllowWonderGuard = CHK_WGuard.Checked,
        };

        rnd.Execute();
        Main.SpeciesStat.Select(z => z.Write()).ToArray().CopyTo(files, 0);

        ReadEntry();
        WinFormsUtil.Alert("Randomized all Pokémon Personal data entries according to specification!", "Press the Export All button to view the new Personal data!");
    }

    private void B_ModifyAll(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Modify all? Cannot undo.", "Double check Modification settings in the Enhancements tab.") != DialogResult.Yes) return;
        if (entry > -1) SaveEntry();

        for (int i = 1; i < CB_Species.Items.Count; i++)
        {
            CB_Species.SelectedIndex = i; // Get new Species

            if (CHK_NoEV.Checked)
            {
                for (int z = 0; z < 6; z++)
                    ev_boxes[z].Text = 0.ToString();
            }

            if (CHK_Growth.Checked)
                CB_EXPGroup.SelectedIndex = 5;
            if (CHK_EXP.Checked)
                TB_BaseExp.Text = ((float)NUD_EXP.Value * (Convert.ToUInt16(TB_BaseExp.Text) / 100f)).ToString("000");

            if (CHK_NoTutor.Checked)
            {
                foreach (int tm in CLB_TM.CheckedIndices)
                    CLB_TM.SetItemCheckState(tm, CheckState.Unchecked);
                foreach (int mt in CLB_MoveTutors.CheckedIndices)
                    CLB_MoveTutors.SetItemCheckState(mt, CheckState.Unchecked);
                foreach (int ao in CLB_BeachTutors.CheckedIndices)
                    CLB_BeachTutors.SetItemCheckState(ao, CheckState.Unchecked);
            }

            if (CHK_FullTMCompatibility.Checked)
            {
                for (int t = 0; t < CLB_TM.Items.Count; t++)
                    CLB_TM.SetItemCheckState(t, CheckState.Checked);
            }

            if (CHK_FullMoveTutorCompatibility.Checked)
            {
                for (int m = 0; m < CLB_MoveTutors.Items.Count; m++)
                    CLB_MoveTutors.SetItemCheckState(m, CheckState.Checked);
            }

            if (CHK_FullBeachTutorCompatibility.Checked)
            {
                for (int m = 0; m < CLB_BeachTutors.Items.Count; m++)
                    CLB_BeachTutors.SetItemCheckState(m, CheckState.Checked);
            }

            if (CHK_QuickHatch.Checked)
                TB_HatchCycles.Text = 1.ToString();
            if (CHK_CallRate.Checked)
                TB_CallRate.Text = ((int)NUD_CallRate.Value).ToString();
            if (CHK_CatchRateMod.Checked)
                TB_CatchRate.Text = ((int)NUD_CatchRateMod.Value).ToString();
        }
        CB_Species.SelectedIndex = 1;
        WinFormsUtil.Alert("Modified all Pokémon Personal data entries according to specification!", "Press the Export All button to view the new Personal data!");
    }

    private bool dumping;

    private void B_Export_Click(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "Personal Entries", Filter = "Text File|*.txt|JSON File|*.json" };
        if (sfd.ShowDialog() != DialogResult.OK)
            return;

        SystemSounds.Asterisk.Play();

        if (sfd.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Main.SpeciesStat, options);
                File.WriteAllText(sfd.FileName, json);
                WinFormsUtil.Alert($"Exported all personal data to JSON!");
            }
            catch (Exception ex) { WinFormsUtil.Error("JSON Export Failed:", ex.Message); }
            return;
        }

        if (entry > -1) SaveEntry();
        dumping = true;
        List<string> lines = [];
        for (int i = 0; i < CB_Species.Items.Count; i++)
        {
            CB_Species.SelectedIndex = i; // Get new Species
            lines.Add("======");
            lines.Add($"{entry} - {CB_Species.Text} (Stage: {TB_Stage.Text})");
            lines.Add("======");
            lines.Add($"Base Stats: {TB_BaseHP.Text}.{TB_BaseATK.Text}.{TB_BaseDEF.Text}.{TB_BaseSPA.Text}.{TB_BaseSPD.Text}.{TB_BaseSPE.Text} (BST: {pkm.BST})");
            lines.Add($"EV Yield: {TB_HPEVs.Text}.{TB_ATKEVs.Text}.{TB_DEFEVs.Text}.{TB_SPAEVs.Text}.{TB_SPDEVs.Text}.{TB_SPEEVs.Text}");
            lines.Add($"Abilities: {CB_Ability1.Text} (1) | {CB_Ability2.Text} (2) | {CB_Ability3.Text} (H)");
            lines.Add(string.Format(CB_Type1.SelectedIndex != CB_Type2.SelectedIndex
                ? "Type: {0} / {1}"
                : "Type: {0}", CB_Type1.Text, CB_Type2.Text));

            lines.Add($"Item 1 (50%): {CB_HeldItem1.Text}");
            lines.Add($"Item 2 (5%): {CB_HeldItem2.Text}");
            lines.Add($"Item 3 (1%): {CB_HeldItem3.Text}");

            lines.Add($"EXP Group: {CB_EXPGroup.Text}");
            lines.Add(string.Format(CB_EggGroup1.SelectedIndex != CB_EggGroup2.SelectedIndex
                ? "Egg Group: {0} / {1}"
                : "Egg Group: {0}", CB_EggGroup1.Text, CB_EggGroup2.Text));
            lines.Add($"Hatch Cycles: {TB_HatchCycles.Text}");
            lines.Add($"Height: {TB_Height.Text} m, Weight: {TB_Weight.Text} kg, Color: {CB_Color.Text}");

            if (CB_ZBaseMove.SelectedIndex > 0)
                lines.Add($"{CB_ZBaseMove.Text} + {CB_ZItem.Text} => {CB_ZMove.Text}");
            lines.Add("");
        }
        string path = sfd.FileName;
        File.WriteAllLines(path, lines, Encoding.Unicode);
        dumping = false;
    }
    private void B_GenerateDiff_Click(object sender, EventArgs e) => GenerateFullChangelog();


    private void CHK_Stats_CheckedChanged(object sender, EventArgs e)
    {
        L_StatDev.Enabled = NUD_StatDev.Enabled = CHK_Stats.Checked;
        CHK_rHP.Enabled = CHK_rATK.Enabled = CHK_rDEF.Enabled = CHK_rSPA.Enabled = CHK_rSPD.Enabled = CHK_rSPE.Enabled = CHK_Stats.Checked;
    }

    private void CHK_Ability_CheckedChanged(object sender, EventArgs e)
    {
        CHK_WGuard.Enabled = CHK_Ability.Checked;
        if (!CHK_WGuard.Enabled)
            CHK_WGuard.Checked = false;
    }
    private void UpdateDynamicDiff(object sender, EventArgs e)
    {
        if (pkm == null || vanillaStats == null || entry < 0 || entry >= vanillaStats.Length) return;
        
        int.TryParse(TB_BaseHP.Text, out int hp);
        int.TryParse(TB_BaseATK.Text, out int atk);
        int.TryParse(TB_BaseDEF.Text, out int def);
        int.TryParse(TB_BaseSPA.Text, out int spa);
        int.TryParse(TB_BaseSPD.Text, out int spd);
        int.TryParse(TB_BaseSPE.Text, out int spe);

        bool isAlt = entry > Main.Config.MaxSpeciesID;
        int[] origValues = new int[6];
        string prefix = isAlt ? "Base Form" : "Vanilla";

        if (isAlt)
        {
            int baseID = baseForms[entry];
            var bPkm = Main.SpeciesStat[baseID];
            origValues[0] = bPkm.HP;
            origValues[1] = bPkm.ATK;
            origValues[2] = bPkm.DEF;
            origValues[3] = bPkm.SPA;
            origValues[4] = bPkm.SPD;
            origValues[5] = bPkm.SPE;
        }
        else
        {
            if (entry >= vanillaStats.Length || vanillaStats[entry] == null) return;
            origValues = vanillaStats[entry];
        }

        SetDiffLabel(L_DiffHP, hp, origValues[0]);
        SetDiffLabel(L_DiffATK, atk, origValues[1]);
        SetDiffLabel(L_DiffDEF, def, origValues[2]);
        SetDiffLabel(L_DiffSPA, spa, origValues[3]); 
        SetDiffLabel(L_DiffSPD, spd, origValues[4]); 
        SetDiffLabel(L_DiffSPE, spe, origValues[5]); 

        int origBST = origValues.Sum();
        int curBST = hp + atk + def + spa + spd + spe;
        int diff = curBST - origBST;

        if (diff > 0)
        {
            L_StatDiff.Text = $"{prefix} BST Diff: {curBST} ({diff} more than {origBST})";
            L_StatDiff.ForeColor = Color.Green;
        }
        else if (diff < 0)
        {
            L_StatDiff.Text = $"{prefix} BST Diff: {curBST} ({Math.Abs(diff)} less than {origBST})";
            L_StatDiff.ForeColor = Color.Red;
        }
        else
        {
            L_StatDiff.Text = $"{prefix} BST Diff: {curBST} (0 more than {origBST})";
            L_StatDiff.ForeColor = Color.Gray; 
        }
    }

    // Missing method restored
    private void SetDiffLabel(Label l, int current, int original)
    {
        int diff = current - original;
        if (diff > 0) { l.Text = $"+{diff}"; l.ForeColor = Color.Green; }
        else if (diff < 0) { l.Text = $"{diff}"; l.ForeColor = Color.Red; }
        else { l.Text = "0"; l.ForeColor = Color.Gray; } 
    }

    private void GenerateFullChangelog()
    {
        if (vanillaStats == null) { WinFormsUtil.Alert("Vanilla stats missing."); return; }
        RTB_Changelog.Clear();
        RTB_Changelog.AppendText("=== COMPREHENSIVE CHANGELOG ===\n");
        int changes = 0;
        var pkmTypeNames = Main.Config.GetText(TextName.Types);
        var abilityNames = Main.Config.GetText(TextName.AbilityNames);

        for (int i = 1; i < files.Length; i++)
        {
            if (files[i] == null || files[i].Length != PersonalInfoSM.SIZE) continue;
            var cur = new PersonalInfoSM(files[i]);
            
            int[] old = null;
            if (i > Main.Config.MaxSpeciesID && baseForms != null && i < baseForms.Length)
            {
                int baseID = baseForms[i];
                if (baseID < vanillaStats.Length && vanillaStats[baseID] != null) old = vanillaStats[baseID];
            }
            else if (i < vanillaStats.Length && vanillaStats[i] != null)
            {
                old = vanillaStats[i];
            }
            
            if (old == null) continue;

            List<string> specDiffs = new List<string>();
            if (cur.HP != old[0]) specDiffs.Add($"HP changed: {old[0]} -> {cur.HP}");
            if (cur.ATK != old[1]) specDiffs.Add($"ATK changed: {old[1]} -> {cur.ATK}");
            if (cur.DEF != old[2]) specDiffs.Add($"DEF changed: {old[2]} -> {cur.DEF}");
            if (cur.SPA != old[3]) specDiffs.Add($"SPA changed: {old[3]} -> {cur.SPA}");
            if (cur.SPD != old[4]) specDiffs.Add($"SPD changed: {old[4]} -> {cur.SPD}");
            if (cur.SPE != old[5]) specDiffs.Add($"SPE changed: {old[5]} -> {cur.SPE}");
            
            if (specDiffs.Count > 0)
            {
                string speciesName = (CB_Species.Items.Count > i) ? CB_Species.Items[i].ToString() : $"Entry {i}";
                RTB_Changelog.AppendText($"\n[{i:000} - {speciesName}]\n");
                foreach (var d in specDiffs) RTB_Changelog.AppendText($"  • {d}\n");
                changes++;
            }
        }
        RTB_Changelog.AppendText($"\nTotal Changed Species: {changes}\n");
    }

    private void B_CopyPage1_Click(object sender, EventArgs e)
    {
        // -- General Info --
        string[] page1Data = {
            TB_BaseHP.Text, TB_BaseATK.Text, TB_BaseDEF.Text, TB_BaseSPA.Text, TB_BaseSPD.Text, TB_BaseSPE.Text,
            CB_Type1.SelectedIndex.ToString(), CB_Type2.SelectedIndex.ToString(),
            TB_CatchRate.Text, TB_HatchCycles.Text, TB_Friendship.Text, TB_Gender.Text,
            CB_Ability1.SelectedIndex.ToString(), CB_Ability2.SelectedIndex.ToString(), CB_Ability3.SelectedIndex.ToString(),
            CB_EggGroup1.SelectedIndex.ToString(), CB_EggGroup2.SelectedIndex.ToString(),
            TB_Height.Text, TB_Weight.Text, CB_Color.SelectedIndex.ToString(),
            TB_BaseExp.Text, CB_EXPGroup.SelectedIndex.ToString(), TB_CallRate.Text,
            TB_HPEVs.Text, TB_ATKEVs.Text, TB_DEFEVs.Text, TB_SPAEVs.Text, TB_SPDEVs.Text, TB_SPEEVs.Text,
            CB_HeldItem1.SelectedIndex.ToString(), CB_HeldItem2.SelectedIndex.ToString(), CB_HeldItem3.SelectedIndex.ToString()
        };

        // Also copy Move Tutor/TM data into in-memory clipboard
        ClipboardTMs = new bool[CLB_TM.Items.Count];
        for (int i = 0; i < CLB_TM.Items.Count; i++) ClipboardTMs[i] = CLB_TM.GetItemChecked(i);

        ClipboardTutors = new bool[CLB_MoveTutors.Items.Count];
        for (int i = 0; i < CLB_MoveTutors.Items.Count; i++) ClipboardTutors[i] = CLB_MoveTutors.GetItemChecked(i);

        if (CLB_BeachTutors.Visible)
        {
            ClipboardBeachTutors = new bool[CLB_BeachTutors.Items.Count];
            for (int i = 0; i < CLB_BeachTutors.Items.Count; i++) ClipboardBeachTutors[i] = CLB_BeachTutors.GetItemChecked(i);
        }

        Clipboard.SetText(string.Join(",", page1Data));
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_PastePage1_Click(object sender, EventArgs e)
    {
        if (!Clipboard.ContainsText()) return;
        string[] p = Clipboard.GetText().Split(',');
        
        if (p.Length < 29) { WinFormsUtil.Error("Invalid clipboard data. Ensure you copied a full set."); return; }

        TB_BaseHP.Text = p[0]; TB_BaseATK.Text = p[1]; TB_BaseDEF.Text = p[2]; 
        TB_BaseSPA.Text = p[3]; TB_BaseSPD.Text = p[4]; TB_BaseSPE.Text = p[5];
        CB_Type1.SelectedIndex = int.Parse(p[6]); CB_Type2.SelectedIndex = int.Parse(p[7]);
        TB_CatchRate.Text = p[8]; TB_HatchCycles.Text = p[9]; TB_Friendship.Text = p[10]; TB_Gender.Text = p[11];
        CB_Ability1.SelectedIndex = int.Parse(p[12]); CB_Ability2.SelectedIndex = int.Parse(p[13]); CB_Ability3.SelectedIndex = int.Parse(p[14]);
        CB_EggGroup1.SelectedIndex = int.Parse(p[15]); CB_EggGroup2.SelectedIndex = int.Parse(p[16]);
        
        TB_Height.Text = p[17]; 
        TB_Weight.Text = p[18]; 
        if (int.TryParse(p[19], out int colIdx) && colIdx > -1 && colIdx < CB_Color.Items.Count) CB_Color.SelectedIndex = colIdx;
        TB_BaseExp.Text = p[20];
        if (int.TryParse(p[21], out int expIdx) && expIdx > -1 && expIdx < CB_EXPGroup.Items.Count) CB_EXPGroup.SelectedIndex = expIdx;
        TB_CallRate.Text = p[22];
        
        TB_HPEVs.Text = p[23]; TB_ATKEVs.Text = p[24]; TB_DEFEVs.Text = p[25];
        TB_SPAEVs.Text = p[26]; TB_SPDEVs.Text = p[27]; TB_SPEEVs.Text = p[28];

        if (p.Length >= 32)
        {
            CB_HeldItem1.SelectedIndex = int.Parse(p[29]);
            CB_HeldItem2.SelectedIndex = int.Parse(p[30]);
            CB_HeldItem3.SelectedIndex = int.Parse(p[31]);
        }

        // Restore TM/Tutor selections if they were copied
        if (ClipboardTMs != null)
        {
            for (int i = 0; i < Math.Min(CLB_TM.Items.Count, ClipboardTMs.Length); i++)
                CLB_TM.SetItemChecked(i, ClipboardTMs[i]);
        }
        if (ClipboardTutors != null)
            for (int i = 0; i < CLB_MoveTutors.Items.Count && i < ClipboardTutors.Length; i++)
                CLB_MoveTutors.SetItemChecked(i, ClipboardTutors[i]);

        if (ClipboardBeachTutors != null && CLB_BeachTutors.Visible)
            for (int i = 0; i < CLB_BeachTutors.Items.Count && i < ClipboardBeachTutors.Length; i++)
                CLB_BeachTutors.SetItemChecked(i, ClipboardBeachTutors[i]);

        SaveEntry();
        System.Media.SystemSounds.Asterisk.Play();
    }
    private void B_CopyTMs_Click(object sender, EventArgs e)
    {
        ClipboardTMs = new bool[CLB_TM.Items.Count];
        for (int i = 0; i < CLB_TM.Items.Count; i++)
            ClipboardTMs[i] = CLB_TM.GetItemChecked(i);
        System.Media.SystemSounds.Asterisk.Play();
    }
    private void B_PasteTMs_Click(object sender, EventArgs e)
    {
        if (ClipboardTMs == null) return;
        for (int i = 0; i < Math.Min(CLB_TM.Items.Count, ClipboardTMs.Length); i++)
            CLB_TM.SetItemChecked(i, ClipboardTMs[i]);
        SaveEntry();
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_MaxCatch_Click(object sender, EventArgs e) { TB_CatchRate.Text = "255"; SaveEntry(); }
    private void B_ZeroHatch_Click(object sender, EventArgs e) { TB_HatchCycles.Text = "0"; SaveEntry(); }

    private void B_MaxCatchAll_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Apply Max Catch Rate (255) to ALL Pokémon?") != DialogResult.Yes) return;
        for (int i = 1; i < files.Length; i++) {
            Main.SpeciesStat[i].CatchRate = 255;
            files[i] = Main.SpeciesStat[i].Write();
        }
        TB_CatchRate.Text = "255";
        SaveEntry();
        WinFormsUtil.Alert("All Pokémon Catch Rates set to 255.");
    }

    private void B_ZeroHatchAll_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Apply 0 Hatch Cycles to ALL Pokémon?") != DialogResult.Yes) return;
        for (int i = 1; i < files.Length; i++) {
            Main.SpeciesStat[i].HatchCycles = 0;
            files[i] = Main.SpeciesStat[i].Write();
        }
        TB_HatchCycles.Text = "0";
        SaveEntry();
        WinFormsUtil.Alert("All Pokémon Hatch Cycles set to 0.");
    }

    private void B_JumpLevelUp_Click(object sender, EventArgs e)
    {
        if (learnsets == null) return;
        SaveEntry();
        var editor = new LevelUpEditor7(learnsets) { StartSpecies = entry };
        editor.ShowDialog();
        ReadEntry(); // Refresh in case anything changed (though unlikely for stats)
    }
    private void B_JumpEggMoves_Click(object sender, EventArgs e)
    {
        if (eggmoves == null) return;
        SaveEntry();
        var editor = new EggMoveEditor7(eggmoves) { StartSpecies = entry };
        editor.ShowDialog();
        ReadEntry();
    }
    private void LoadVanillaStats() 
    {
        if (files == null || files.Length == 0) return; // Safety check

        vanillaStats = new int[files.Length][];
        string path = Path.Combine(Application.StartupPath, "vanilla_stats.txt");

        if (File.Exists(path))
        {
            // Read from the permanent backup
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i >= files.Length) break;
                string[] parts = lines[i].Split(',');
                if (parts.Length == 6)
                {
                    vanillaStats[i] = new int[6];
                    for (int j = 0; j < 6; j++)
                        int.TryParse(parts[j], out vanillaStats[i][j]);
                }
            }
        }
        else
        {
            try
            {
                // First run: Create the permanent backup file using ALL entries in files
                List<string> lines = new List<string>();
                for (int i = 0; i < files.Length; i++)
                {
                    vanillaStats[i] = new int[6];
                    
                    if (i > 0 && files[i] != null && files[i].Length >= 6)
                    {
                        vanillaStats[i][0] = files[i][0]; // HP
                        vanillaStats[i][1] = files[i][1]; // ATK
                        vanillaStats[i][2] = files[i][2]; // DEF
                        vanillaStats[i][3] = files[i][4]; // SPA 
                        vanillaStats[i][4] = files[i][5]; // SPD 
                        vanillaStats[i][5] = files[i][3]; // SPE 
                    }
                    
                    lines.Add($"{vanillaStats[i][0]},{vanillaStats[i][1]},{vanillaStats[i][2]},{vanillaStats[i][3]},{vanillaStats[i][4]},{vanillaStats[i][5]}");
                }
                
                File.WriteAllLines(path, lines);
                WinFormsUtil.Alert("Created 'vanilla_stats.txt' successfully!", 
                                   $"It is located at:\n{path}");
            }
            catch (Exception ex)
            {
                WinFormsUtil.Alert("Failed to create vanilla_stats.txt.", "Error details:", ex.Message);
            }
        }
    }
    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        if (entry > -1) SaveEntry();
        RandSettings.SetFormSettings(this, TP_Randomizer.Controls);
    }

    private byte[] RebuildMasterTable(byte[][] entries)
    {
        if (entries == null || entries.Length == 0) return [];
        int len = entries[0].Length;
        byte[] table = new byte[entries.Length * len];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i].CopyTo(table, i * len);
        }
        return table;
    }
}
