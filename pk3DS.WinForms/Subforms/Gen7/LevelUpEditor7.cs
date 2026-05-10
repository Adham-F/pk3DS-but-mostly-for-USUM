using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;
using pk3DS.Core.Structures;
using pk3DS.Core;
using pk3DS.Core.Randomizers;
using System.Text.Json;
using pk3DS.WinForms.Properties;

namespace pk3DS.WinForms;

public partial class LevelUpEditor7 : Form
{
    public LevelUpEditor7(byte[][] infiles)
    {
        specieslist[0] = movelist[0] = "";
        InitializeComponent();
        var species = Main.Config.GetText(TextName.SpeciesNames);
        var altForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        var entryNames = Main.Config.Personal.GetPersonalEntryList(altForms, species, Main.Config.MaxSpeciesID, out baseForms, out formVal);
        files = infiles;
        RTB_Changelog.ReadOnly = true;
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
        
        Button b_genChangelog = new Button { Text = "Generate Diff vs Vanilla", Dock = DockStyle.Top, FlatStyle = FlatStyle.Flat, Height = 30 };
        b_genChangelog.Click += (s, e) => GenerateFullChangelog();
        TP_Changelog.Controls.Add(b_genChangelog);
        LoadVanillaLevelUp();
        LogChange("Level Up Editor initialized.");

        string[] sortedspecies = (string[])specieslist.Clone();
        Array.Resize(ref sortedspecies, Main.Config.MaxSpeciesID + 1); Array.Sort(sortedspecies);
        SetupDGV();

        var newlist = new List<ComboItem>();
        for (int i = 1; i < entryNames.Length; i++)
            newlist.Add(new ComboItem { Text = entryNames[i], Value = i });

        CB_Species.DisplayMember = "Text";
        CB_Species.ValueMember = "Value";
        CB_Species.DataSource = newlist;
        CB_Species.SelectedIndex = 0;
        RandSettings.GetFormSettings(this, groupBox1.Controls);
        dgv.CellValueChanged += UpdateCounters;
        dgv.RowsAdded += UpdateCounters;
        dgv.RowsRemoved += UpdateCounters;

        Shown += (sender, e) => {
            if (StartSpecies >= 0)
                CB_Species.SelectedValue = StartSpecies;
            UpdateCounters(null, null);
        };
    }

    public int StartSpecies { get; set; } = -1;

    private readonly byte[][] files;
    private int entry = -1;
    private static int[] ClipboardMoves;
    private static int[] ClipboardLevels;
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] specieslist = Main.Config.GetText(TextName.SpeciesNames);
    private readonly int[] baseForms, formVal;
    private byte[][] vanillaFiles;

    private void SetupDGV()
    {
        dgv.Columns.Clear();
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Lv", Width = 30, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Move", Width = 140, FlatStyle = FlatStyle.Flat, DataSource = movelist });
        
        dgv.RowHeadersVisible = false;
        dgv.ScrollBars = ScrollBars.Vertical;
        dgv.ColumnHeadersVisible = true;
        dgv.AllowUserToAddRows = false;
        dgv.EditMode = DataGridViewEditMode.EditOnEnter;
    }
   
private static Dictionary<string, int> formMappingCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly string mapFilePath = Path.Combine(Application.StartupPath, "custom_form_mappings.txt");

    private void LoadMappingCache()
    {
        if (!File.Exists(mapFilePath)) return;
        foreach (string line in File.ReadAllLines(mapFilePath))
        {
            var parts = line.Split('=');
            if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                formMappingCache[parts[0]] = id;
        }
    }

    private void SaveMappingCache()
    {
        var lines = formMappingCache.Select(kvp => $"{kvp.Key}={kvp.Value}");
        File.WriteAllLines(mapFilePath, lines);
    }

private int PromptFormMapping(string formName)
    {
        Form prompt = new Form()
        {
            Width = 420, Height = 210, FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Map Custom Form", StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false
        };
        
        Label textLabel = new Label() { 
            Left = 15, Top = 15, Width = 380, Height = 35, 
            Text = $"Unrecognized form '{formName}'.\nSelect the Pokémon this form corresponds to:" 
        };
        
        ComboBox cb = new ComboBox() { 
            Left = 15, Top = 55, Width = 230, 
            DropDownStyle = ComboBoxStyle.DropDown,
            DisplayMember = "Text", ValueMember = "Value",
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        
        // FIX: Add items directly to bypass deferred DataSource binding crashes
        foreach (ComboItem ci in CB_Species.Items) cb.Items.Add(ci);
        
        PictureBox pb = new PictureBox() { 
            Left = 260, Top = 55, Width = 120, Height = 100, 
            SizeMode = PictureBoxSizeMode.CenterImage, BorderStyle = BorderStyle.FixedSingle 
        };
        
        cb.SelectedIndexChanged += (sender, e) => {
            var ci = cb.SelectedItem as ComboItem;
            if (ci != null && (int)ci.Value > 0) {
                int id = (int)ci.Value;
                int s = id <= Main.Config.MaxSpeciesID ? id : baseForms[id];
                int f = id <= Main.Config.MaxSpeciesID ? 0 : formVal[id];
                
                var rawImg = WinFormsUtil.GetSprite(s, f, 0, 0, Main.Config);
                var bigImg = new Bitmap(rawImg.Width * 2, rawImg.Height * 2);
                for (int x = 0; x < rawImg.Width; x++)
                for (int y = 0; y < rawImg.Height; y++)
                {
                    Color c = rawImg.GetPixel(x, y);
                    bigImg.SetPixel(2 * x, 2 * y, c);
                    bigImg.SetPixel((2 * x) + 1, 2 * y, c);
                    bigImg.SetPixel(2 * x, (2 * y) + 1, c);
                    bigImg.SetPixel((2 * x) + 1, (2 * y) + 1, c);
                }
                pb.Image = bigImg;
            } else {
                pb.Image = null;
            }
        };
        
        Button confirmation = new Button() { Text = "Map Form", Left = 15, Width = 110, Top = 100, DialogResult = DialogResult.OK };
        Button cancel = new Button() { Text = "Skip", Left = 135, Width = 110, Top = 100, DialogResult = DialogResult.Cancel };
        
        prompt.Controls.Add(cb);
        prompt.Controls.Add(pb);
        prompt.Controls.Add(confirmation);
        prompt.Controls.Add(cancel);
        prompt.Controls.Add(textLabel);
        prompt.AcceptButton = confirmation;
        prompt.CancelButton = cancel;
        
        // Safety check using the natively populated cb.Items count
        if (cb.Items.Count > 0)
        {
            cb.SelectedIndex = cb.Items.Count > 1 ? 1 : 0; 
        }
        
        return prompt.ShowDialog() == DialogResult.OK && cb.SelectedItem != null ? (int)((ComboItem)cb.SelectedItem).Value : -1;
    }
    private Learnset6 pkm;

    private void GetList()
    {
        entry = WinFormsUtil.GetIndex(CB_Species);
        int s = baseForms[entry];
        int f = formVal[entry];
        if (entry <= Main.Config.MaxSpeciesID)
            s = entry;
        string filename = "_" + s + (entry > Main.Config.MaxSpeciesID ? "_" + (f + 1) : "");
        PB_MonSprite.Image = (Bitmap)Resources.ResourceManager.GetObject(filename);

        int dataIndex = entry < files.Length ? entry : baseForms[entry];
        dgv.Rows.Clear();
        byte[] input = files[dataIndex];
        if (input == null || input.Length <= 4) { files[dataIndex] = BitConverter.GetBytes(-1); return; }
        pkm = new Learnset6(input);

        for (int i = 0; i < pkm.Levels.Length; i++)
        {
            int move = pkm.Moves[i];
            int lv = pkm.Levels[i];
            dgv.Rows.Add(lv, movelist[move]);
        }
        L_TotalMoves.Text = $"Total Moves: {pkm.Moves.Length}";
        UpdateCounters(null, null);
        dgv.CancelEdit();
    }

    private void PopulateGenColumns()
    {
        if (dgv.Columns["Col_Gen8"] == null || dgv.Columns["Col_Gen9"] == null) return;
        if (pkm == null) return;

        // Build a move-name -> gen8/gen9 level map from the embedded learnsets database
        var gen8Map = ModernLearnsetImporter.GetMoveLevelMap(entry, specieslist, baseForms, 8);
        var gen9Map = ModernLearnsetImporter.GetMoveLevelMap(entry, specieslist, baseForms, 9);

        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            string moveName = dgv.Rows[i].Cells[1].Value as string ?? "";
            string normMove = moveName.ToLowerInvariant().Replace(" ", "").Replace("-", "");

            dgv.Rows[i].Cells["Col_Gen8"].Value = gen8Map.TryGetValue(normMove, out int lv8) ? lv8.ToString() : "—";
            dgv.Rows[i].Cells["Col_Gen9"].Value = gen9Map.TryGetValue(normMove, out int lv9) ? lv9.ToString() : "—";
        }
    }

    private void B_PSLearnsets_Click(object sender, EventArgs e)
    {
        var result = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Import Modern Learnsets for THIS species?", "Yes = Gen 9\nNo = Gen 8\nCancel = Abort");
        if (result == DialogResult.Cancel) return;
        int genId = result == DialogResult.Yes ? 9 : 8;

        var moveMap = ModernLearnsetImporter.GetMoveLevelMap(entry, specieslist, baseForms, genId);
        if (moveMap.Count == 0) { WinFormsUtil.Alert("No Gen 8/9 Level-Up moves found for this species."); return; }

        dgv.Rows.Clear();
        var sortedMoves = moveMap.OrderBy(kvp => kvp.Value).ToList();
        foreach (var kvp in sortedMoves)
        {
            // Find global index of move name
            int moveIdx = Array.IndexOf(movelist, movelist.FirstOrDefault(m => ModernLearnsetImporter.NormalizeName(m) == kvp.Key));
            if (moveIdx > 0) dgv.Rows.Add(kvp.Value, movelist[moveIdx]);
        }
        
        WinFormsUtil.Alert($"Imported {moveMap.Count} moves from the Gen {genId} database for {specieslist[entry]}!");
        PopulateGenColumns();
    }

    private void SetList()
    {
        if (entry < 1 || pkm == null) return;
        var levels = new List<int>();
        var moves = new List<int>();
        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            if (dgv.Rows[i].Cells[0].Value != null)
            {
                int moveIdx = Array.IndexOf(movelist, dgv.Rows[i].Cells[1].Value);
                if (moveIdx >= 0)
                {
                    int.TryParse(dgv.Rows[i].Cells[0].Value.ToString(), out int lv);
                    levels.Add(Math.Min(100, Math.Max(0, lv)));
                    moves.Add(moveIdx);
                }
            }
        }
        var sorted = levels.Select((l, i) => new { Level = l, Move = moves[i] })
                           .OrderBy(x => x.Level)
                           .ToList();
        pkm.Levels = sorted.Select(x => x.Level).ToArray();
        pkm.Moves = sorted.Select(x => x.Move).ToArray();
        int dataIndex = entry < files.Length ? entry : baseForms[entry];
        files[dataIndex] = pkm.Write();
        GenerateFullChangelog();
        LogChange($"Saved changes for {CB_Species.Text}");
    }

    private void LogChange(string text)
    {
        if (RTB_Changelog == null) return;
        RTB_Changelog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }

    private void ChangeEntry(object sender, EventArgs e)
    {
        SetList();
        GetList();
    }

    // Struggle, Hyperspace Fury, Dark Void
    private static readonly int[] usualBan = [165, 621, 464];

    // Metronome logic kept for fun/functionality
    private void B_Metronome_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Play using Metronome Mode?", "This will modify learnsets to only have Metronome.") != DialogResult.Yes)
            return;

        // clear all data, then only assign Metronome at Lv1
        for (int i = 0; i < CB_Species.Items.Count; i++)
        {
            CB_Species.SelectedIndex = i;
            dgv.Rows.Clear();
            dgv.Rows.Add();
            dgv.Rows[0].Cells[0].Value = 1;
            dgv.Rows[0].Cells[1].Value = movelist[118];
        }
        CB_Species.SelectedIndex = 0;
        WinFormsUtil.Alert("All Pokémon now only know the move Metronome!");
    }

    private void LoadVanillaLevelUp()
    {
        if (files == null || files.Length == 0) return;
        string path = Path.Combine(Application.StartupPath, "vanilla_levelup.txt");
        
        vanillaFiles = new byte[files.Length][];
        
        if (File.Exists(path))
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] parts = lines[i].Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out int idx)) continue;
                if (idx >= files.Length) continue;
                
                vanillaFiles[idx] = Convert.FromBase64String(parts[1]);
            }
        }
        else
        {
            try
            {
                List<string> lines = new List<string>();
                for (int i = 0; i < files.Length; i++)
                {
                    if (files[i] == null) continue;
                    vanillaFiles[i] = (byte[])files[i].Clone();
                    lines.Add($"{i}:{Convert.ToBase64String(vanillaFiles[i])}");
                }
                File.WriteAllLines(path, lines);
                WinFormsUtil.Alert("Created 'vanilla_levelup.txt' baseline successfully!");
            }
            catch { }
        }
    }

    private void GenerateFullChangelog()
    {
        if (vanillaFiles == null) { WinFormsUtil.Alert("Vanilla baseline missing."); return; }
        RTB_Changelog.Clear();
        RTB_Changelog.AppendText("=== LEVEL-UP CHANGELOG ===\n");
        int changes = 0;

        for (int i = 1; i < files.Length; i++)
        {
            if (files[i] == null || vanillaFiles[i] == null) continue;
            
            var cur = new Learnset6(files[i]);
            var old = new Learnset6(vanillaFiles[i]);
            
            // Reconstruct moves into string dictionaries for easy diffing
            List<string> curMoves = new List<string>();
            List<string> oldMoves = new List<string>();
            
            for (int j = 0; j < cur.Count; j++) curMoves.Add($"Lv{cur.Levels[j]} {movelist[cur.Moves[j]]}");
            for (int j = 0; j < old.Count; j++) oldMoves.Add($"Lv{old.Levels[j]} {movelist[old.Moves[j]]}");
            
            var added = curMoves.Except(oldMoves).ToList();
            var removed = oldMoves.Except(curMoves).ToList();
            
            if (added.Count > 0 || removed.Count > 0)
            {
                string speciesName = (CB_Species.Items.Count > i) ? CB_Species.Items[i].ToString() : $"Entry {i}";
                RTB_Changelog.AppendText($"\n[{i:000} - {speciesName}]\n");
                
                foreach (var move in added) RTB_Changelog.AppendText($"  + Added: {move}\n");
                foreach (var move in removed) RTB_Changelog.AppendText($"  - Removed: {move}\n");
                changes++;
            }
        }
        RTB_Changelog.AppendText($"\nTotal Modified Species: {changes}\n");
    }

    private void B_AddMove_Click(object sender, EventArgs e)
    {
        SetList(); // Commit current UI state
        if (pkm == null) return;
        
        var levels = pkm.Levels.ToList();
        var moves = pkm.Moves.ToList();
        levels.Add(1);
        moves.Add(1); // Default to Pound
        
        pkm.Levels = [.. levels];
        pkm.Moves = [.. moves];
        
        int dataIndex = entry < files.Length ? entry : baseForms[entry];
        files[dataIndex] = pkm.Write();
        GetList();
    }

    private void B_RemoveMove_Click(object sender, EventArgs e)
    {
        if (dgv.CurrentRow == null) return;
        int rowIdx = dgv.CurrentRow.Index;
        
        SetList(); // Commit current UI state
        if (pkm == null) return;
        
        var levels = pkm.Levels.ToList();
        var moves = pkm.Moves.ToList();
        
        if (rowIdx < levels.Count)
        {
            levels.RemoveAt(rowIdx);
            moves.RemoveAt(rowIdx);
            
            pkm.Levels = [.. levels];
            pkm.Moves = [.. moves];
            
            int dataIndex = entry < files.Length ? entry : baseForms[entry];
            files[dataIndex] = pkm.Write();
            GetList();
        }
    }

    private void UpdateCounters(object sender, EventArgs e)
    {
        if (entry < 0 || pkm == null) return;
        int moveCount = 0;
        int stabCount = 0;
        var pkmTypes = Main.SpeciesStat[entry].Types;
        var moveData = Main.Config.Moves;

        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            var cellVal = dgv.Rows[i].Cells[1].Value;
            if (cellVal == null) continue;
            int move = Array.IndexOf(movelist, cellVal);
            if (move > 0)
            {
                moveCount++;
                if (pkmTypes.Contains(moveData[move].Type))
                    stabCount++;
            }
        }
        if (L_TotalMoves != null) L_TotalMoves.Text = $"Total Moves: {moveCount}";
        if (L_STABCount != null) L_STABCount.Text = $"STAB Moves: {stabCount}";
    }


    private void B_Import_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog { Filter = "Supported Formats|*.txt;*.json;*.tsv;*.ts" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string ext = Path.GetExtension(ofd.FileName).ToLower();
        int count = 0;

        if (ext == ".json")
        {
            count = ImportJson(ofd.FileName);
        }
        else if (ext == ".ts" || ext == ".txt" || ext == ".tsv")
        {
            string text = File.ReadAllText(ofd.FileName);
            if (text.Contains("- Level")) // Showdown format
                count = ImportShowdown(text);
            else // Native Tab-separated
                count = ImportNative(File.ReadAllLines(ofd.FileName));
        }

        GetList();
        WinFormsUtil.Alert($"Imported learnsets for {count} Pokémon.");
    }

    private int ImportJson(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(json);
            if (data == null) return 0;
            int count = 0;
            foreach (var kvp in data)
            {
                int targetId = GetSpeciesIdFromText(kvp.Key);
                if (targetId <= 0) continue;
                var levels = kvp.Value.Keys.ToArray(); 
                var moves = kvp.Value.Values.Select(m => Array.IndexOf(movelist, m)).ToArray();
                ApplyToFiles(targetId, levels, moves);
                count++;
            }
            return count;
        }
        catch { return 0; }
    }

    private int ImportShowdown(string text)
    {
        int count = 0;
        string[] blocks = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            string[] lines = block.Split('\n');
            if (lines.Length < 2) continue;
            int targetId = GetSpeciesIdFromText(lines[0].Trim());
            if (targetId <= 0) continue;

            List<int> levels = [];
            List<int> moves = [];
            foreach (var line in lines.Skip(1))
            {
                if (!line.Contains("- Level")) continue;
                var match = System.Text.RegularExpressions.Regex.Match(line, @"- Level (\d+): (.+)");
                if (match.Success)
                {
                    levels.Add(int.Parse(match.Groups[1].Value));
                    moves.Add(Array.IndexOf(movelist, match.Groups[2].Value.Trim()));
                }
            }
            if (levels.Count > 0) {
                ApplyToFiles(targetId, [.. levels], [.. moves]);
                count++;
            }
        }
        return count;
    }

    private int ImportNative(string[] lines)
    {
        int count = 0;
        foreach (string line in lines)
        {
            string[] parts = line.Split('\t');
            if (parts.Length < 2) continue;
            if (int.TryParse(parts[0], out int targetId) && targetId > 0 && targetId < files.Length)
            {
                List<int> levels = [];
                List<int> moves = [];
                for (int i = 2; i < parts.Length - 1; i += 2)
                {
                    if (int.TryParse(parts[i], out int lvl))
                    {
                        int moveId = Array.IndexOf(movelist, parts[i+1].Trim());
                        if (moveId > 0) { levels.Add(lvl); moves.Add(moveId); }
                    }
                }
                ApplyToFiles(targetId, [.. levels], [.. moves]);
                count++;
            }
        }
        return count;
    }

    private int GetSpeciesIdFromText(string text)
    {
        if (int.TryParse(text, out int id)) return id;
        string clean = text.Trim();
        var items = CB_Species.Items.Cast<ComboItem>().ToArray();
        return Array.FindIndex(items, i => i.Text.Contains(clean, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyToFiles(int targetId, int[] levels, int[] moves)
    {
        if (targetId < 0 || targetId >= files.Length) return;
        byte[] input = files[targetId];
        if (input.Length < 4) return;
        
        var mod = new Learnset6(input) { Levels = levels, Moves = moves };
        files[targetId] = mod.Write();
    }

    private void B_ApplyModern_Click(object sender, EventArgs e)
    {
        var result = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Update ALL Pokémon to Modern Learnsets from Showdown Database?", 
            "Yes = Gen 9\nNo = Gen 8\nCancel = Abort");
            
        if (result == DialogResult.Cancel) return;
        int genId = result == DialogResult.Yes ? 9 : 8;

        if (genId == 8) ModernLearnsetImporter.ApplyModernLearnsets(files, movelist, specieslist, 8, baseForms);
        else if (genId == 9) ModernLearnsetImporter.ApplyModernLearnsets(files, movelist, specieslist, 9, baseForms);
        
        // Refresh the single current species
        GetList();
    }

    private void B_ModernGen8_Click(object sender, EventArgs e) { }
    private void B_ModernGen9_Click(object sender, EventArgs e) { }
    private void B_ImportJSON_Click(object sender, EventArgs e) => B_Import_Click(null, null);
    private void B_ImportTS_Click(object sender, EventArgs e) => B_Import_Click(null, null);

    private void B_RandAll_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize ALL Pokémon learnsets based on current options?") != DialogResult.Yes) return;
        var sets = files.Select(z => new Learnset6(z)).Cast<Learnset>().ToArray();
        var rand = new LearnsetRandomizer(Main.Config, sets);
        rand.Execute();
        for (int i = 0; i < files.Length; i++)
            files[i] = ((Learnset6)sets[i]).Write();
        GetList();
        WinFormsUtil.Alert("All learnsets randomized!");
    }

    private void B_Dump_Click(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "Level Up Learnsets.txt", Filter = "Text File|*.txt" };
        if (sfd.ShowDialog() != DialogResult.OK) return;
        var sb = new StringBuilder();
        for (int i = 0; i < files.Length; i++)
        {
            var p = new Learnset6(files[i]);
            sb.AppendLine($"{i:000} {specieslist[i]}");
            for (int j = 0; j < p.Count; j++)
                sb.AppendLine($"{p.Levels[j]} - {movelist[p.Moves[j]]}");
            sb.AppendLine();
        }
        File.WriteAllText(sfd.FileName, sb.ToString());
    }

    private void B_Goto_Click(object sender, EventArgs e)
    {
        int val = (int)NUD_FormTable.Value;
        if (val >= 0 && val < CB_Species.Items.Count)
            CB_Species.SelectedIndex = val;
    }

    private void B_Copy_Click(object sender, EventArgs e)
    {
        SetList(); // Forces the UI data to save to the pkm object first
        if (pkm == null) return;
        
        ClipboardMoves = (int[])pkm.Moves.Clone();
        ClipboardLevels = (int[])pkm.Levels.Clone();
    }

    private void B_Paste_Click(object sender, EventArgs e)
    {
        if (ClipboardMoves == null || ClipboardLevels == null) return;
        
        pkm.Moves = (int[])ClipboardMoves.Clone();
        pkm.Levels = (int[])ClipboardLevels.Clone();
        
        files[entry] = pkm.Write(); // Commit changes to the file array
        GetList(); // Refresh the DataGridView UI with the new data
    }
    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();
        RandSettings.SetFormSettings(this, groupBox1.Controls);
    }
    private void CHK_TypeBias_CheckedChanged(object sender, EventArgs e)
    {
        NUD_STAB.Enabled = CHK_STAB.Checked;
        NUD_STAB.Value = CHK_STAB.Checked ? 52 : NUD_STAB.Minimum;
    }

    public void CalcStats() // Debug Function
    {
        Move[] MoveData = Main.Config.Moves;
        int movectr = 0;
        int max = 0;
        int spec = 0;
        int stab = 0;
        for (int i = 0; i < Main.Config.MaxSpeciesID; i++)
        {
            byte[] movedata = files[i];
            int movecount = (movedata.Length - 4) / 4;
            if (movecount == 65535)
                continue;
            movectr += movecount; // Average Moves
            if (max < movecount) { max = movecount; spec = i; } // Max Moves (and species)
            for (int m = 0; m < movedata.Length / 4; m++)
            {
                int move = BitConverter.ToUInt16(movedata, m * 4);
                if (move == 65535)
                {
                    movectr--;
                    continue;
                }
                if (Main.SpeciesStat[i].Types.Contains(MoveData[move].Type))
                    stab++;
            }
        }
        WinFormsUtil.Alert($"Moves Learned: {movectr}\r\nMost Learned: {max} @ {spec}\r\nSTAB Count: {stab}");
    }
}