using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using pk3DS.Core;
using pk3DS.Core.Modding;
using pk3DS.Core.CTR;

namespace pk3DS.WinForms;

public partial class TMEditor7 : Form
{
    public TMEditor7()
    {
        InitializeComponent();
        if (Main.ExeFSPath == null) { WinFormsUtil.Alert("No exeFS code to load."); Close(); }
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) { WinFormsUtil.Alert("No .code.bin detected."); Close(); }
        data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) { WinFormsUtil.Alert(".code.bin not decompressed. Aborting."); Close(); }

        // Universal TM Table Detection
        // TM01: Work Up (526), TM02: Dragon Claw (337), TM03: Psyshock (473)
        // Little-endian ushorts: [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01]
        byte[] tmSig = [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01];
        int foundOfs = Util.IndexOfBytes(data, tmSig, 0x100000, 0);
        if (foundOfs >= 0)
        {
            offset = foundOfs;
        }
        else
        {
            // Fallback to standard signature search
            offset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + Signature.Length;
            if (Main.Config.USUM) offset += 0x22;
        }
        codebin = files[0];
        movelist[0] = "";

        // Auto-detect expansion start ID
        DetectExpansionStartID();

        SetupDGV();
        
        // Auto-detect TM count from code.bin once on load
        if (File.Exists(codebin))
        {
            byte[] codeData = File.ReadAllBytes(codebin);
            int detectedCount = DetectTMCount(codeData);
            if (detectedCount > 0)
            {
                skipUpdate = true;
                NUD_TMCount.Value = Math.Min(detectedCount, NUD_TMCount.Maximum);
                skipUpdate = false;
            }
        }
        
        GetList();
        TB_Offset.Text = offset.ToString("X");

        // Show TM expansion info when patch is detected
        int tmCount = (int)NUD_TMCount.Value;
        if (tmCount > 100)
        {
            string msg = $"TM/HM Expansion Patch Detected — {tmCount} TMs\n\n"
                + "Slot Configuration:\n"
                + "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
                + "• TM01–TM107: Uses standard/HM slots.\n"
                + "• TM108+: Automatically mapped to Item ID " + expandedTMStartID + ".\n\n"
                + "(If it says ID 328, it is just a placeholder. It means you haven't expanded anything yet!)\n\n"
                + "The editor has automatically detected your custom Item ID range from code.bin.";
            WinFormsUtil.Alert(msg);
        }
    }

    private int expandedTMStartID = 960;
    private void DetectExpansionStartID()
    {
        try {
            if (File.Exists(codebin)) {
                byte[] d = File.ReadAllBytes(codebin);
                // TM Item Table for 108+ is usually at 0x4BB794 in patched USUM
                if (d.Length > 0x4BB794 + 2) {
                    int id = BitConverter.ToUInt16(d, 0x4BB794);
                    if (id >= 100 && id < 2000) expandedTMStartID = id;
                }
            }
        } catch { expandedTMStartID = 960; }
    }

    private static readonly byte[] Signature = [0x03, 0x40, 0x03, 0x41, 0x03, 0x42, 0x03, 0x43, 0x03]; // tail end of item::ITEM_CheckBeads
    private readonly string codebin;
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private bool skipUpdate = false;
    private int offset = 0x0059795A; // Default
    private readonly byte[] data;
    private int dataoffset;

    private void GetDataOffset()
    {
        dataoffset = offset; // reset
    }

    private int GetTMOffset(int index)
    {
        // TM01 to TM100 are always contiguous from the detected base
        if (index < 100) return offset + (2 * index);

        // For expanded TMs (101+), we check if there's a second table (sandbox)
        // or if they are contiguous. Most expansion patches jump at 108.
        if (index >= 107)
        {
             // If a known sandbox offset is provided in the textbox or research, use it.
             // Otherwise, check for the 108+ sandbox (0x4BB794) if it contains a move ID.
             if (offset < 0x100000 && data.Length > 0x4BB794 + 2)
                 return 0x4BB794 + (2 * (index - 107));
        }
        return offset + (2 * index);
    }

    private void SetupDGV()
    {
        dgvTM.Columns.Clear();
        var dgvIndex = new DataGridViewTextBoxColumn();
        {
            dgvIndex.HeaderText = "Index";
            dgvIndex.DisplayIndex = 0;
            dgvIndex.Width = 45;
            dgvIndex.ReadOnly = true;
            dgvIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvIndex.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        var dgvItemID = new DataGridViewTextBoxColumn();
        {
            dgvItemID.HeaderText = "Item ID";
            dgvItemID.DisplayIndex = 1;
            dgvItemID.Width = 60;
            dgvItemID.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvItemID.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        var dgvMove = new DataGridViewComboBoxColumn();
        {
            dgvMove.HeaderText = "Move";
            dgvMove.DisplayIndex = 2;
            dgvMove.Items.AddRange(movelist); // add only the Names
            dgvMove.Width = 133;
            dgvMove.FlatStyle = FlatStyle.Flat;
            dgvMove.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        dgvTM.Columns.Add(dgvIndex);
        dgvTM.Columns.Add(dgvItemID);
        dgvTM.Columns.Add(dgvMove);
    }

    private List<ushort> tms = [];

    private void GetList()
    {
        dgvTM.Rows.Clear();
        tms = [];

        // Dynamic Repointing: Parse the offset box
        if (!int.TryParse(TB_Offset.Text, System.Globalization.NumberStyles.HexNumber, null, out int currentOffset))
            currentOffset = offset;

        int count = (int)NUD_TMCount.Value;
        if (currentOffset + (count * 2) > data.Length)
        {
             WinFormsUtil.Alert("Offset is out of bounds for the current code.bin.");
             return;
        }

        for (int i = 0; i < count; i++) 
            tms.Add(BitConverter.ToUInt16(data, GetTMOffset(i)));

        ushort[] defaultItems = GetDefaultTMItems();
        ushort[] itemIDs = ResearchEngine.GetTMItemArray(data, count, defaultItems);

        ushort[] tmlist = [.. tms];
        for (int i = 0; i < tmlist.Length; i++)
        { 
            dgvTM.Rows.Add(); 
            dgvTM.Rows[i].Cells[0].Value = (i + 1).ToString(); 
            
            ushort itemID = i < itemIDs.Length ? itemIDs[i] : (ushort)0;
            dgvTM.Rows[i].Cells[1].Value = itemID.ToString();
            // Lock vanilla item IDs (TMs 1-100 and HMs 101-107) to prevent breaking standard compatibility
            if (i < 107) dgvTM.Rows[i].Cells[1].ReadOnly = true;
            
            ushort moveId = tmlist[i];
            if (moveId >= movelist.Length) moveId = 0; 
            
            dgvTM.Rows[i].Cells[2].Value = movelist[moveId]; 
        }
    }
    
    private ushort[] GetDefaultTMItems()
    {
        ushort[] items = new ushort[107];
        for (int i = 0; i < 92; i++) items[i] = (ushort)(328 + i);
        for (int i = 0; i < 3; i++) items[92 + i] = (ushort)(618 + i);
        for (int i = 0; i < 5; i++) items[95 + i] = (ushort)(690 + i);
        for (int i = 0; i < 6; i++) items[100 + i] = (ushort)(420 + i);
        items[106] = 737; // HM07
        return items;
    }

    /// <summary>
    /// Scans code.bin for the CMP instruction that originally checked #100 (0x64) for TM count.
    /// Only searches near the TM table offset to avoid false positives from unrelated CMP instructions.
    /// Decodes ARM rotated immediates to return the actual patched value.
    /// </summary>
    private int DetectTMCount(byte[] codeData)
    {
        // 1. Search for our custom patch signature: push {r4, lr}; cmp r0, #LIMIT; ldrlo r4, [pc, #8]
        byte[] customSig = [0x10, 0x40, 0x2D, 0xE9, 0x00, 0x00, 0x50, 0xE3, 0x08, 0x40, 0x9F, 0x35];
        byte[] mask = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        
        for (int i = 0x100000; i < codeData.Length - 12; i += 4)
        {
            bool match = true;
            for (int j = 0; j < customSig.Length; j++)
            {
                if (mask[j] == 0xFF && codeData[i + j] != customSig[j]) { match = false; break; }
            }
            if (match)
            {
                uint word = BitConverter.ToUInt32(codeData, i + 4);
                return (int)(word & 0xFF);
            }
        }

        // 2. Fallback: Search for vanilla Order->Move signature
        byte[] vanillaSig = [0x10, 0x40, 0x2D, 0xE9, 0x6B, 0x00, 0x50, 0xE3, 0x00, 0x40, 0xA0, 0xE1];
        int vanillaOfs = Util.IndexOfBytes(codeData, vanillaSig, 0x100000, 0);
        if (vanillaOfs >= 0)
        {
            // Vanilla uses CMP R4, #0x64 at offset 0x24 (0x276444)
            uint word = BitConverter.ToUInt32(codeData, vanillaOfs + 0x24);
            if ((word & 0xFFFFF000) == 0xE3540000) // CMP R4, #IMM
                return (int)(word & 0xFF);
        }

        return 100;
    }

    private void SetList()
    {
        tms = [];
        List<ushort> items = [];
        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            if (ushort.TryParse(dgvTM.Rows[i].Cells[1].Value?.ToString(), out ushort itemID))
                items.Add(itemID);
            else
                items.Add(0);

            var val = dgvTM.Rows[i].Cells[2].Value;
            if (val == null) tms.Add(0);
            else tms.Add((ushort)Array.IndexOf(movelist, val.ToString()));
        }

        ushort[] tmlist = [.. tms];
        ushort[] itemlist = [.. items];

        if (!int.TryParse(TB_Offset.Text, System.Globalization.NumberStyles.HexNumber, null, out int currentOffset))
            currentOffset = offset;

        int count = Math.Min(tmlist.Length, (int)NUD_TMCount.Value);
        
        // Pass the expansion to ResearchEngine which handles Assembly patching
        ResearchEngine.ApplyExpandedTMCodePatch(data, tmlist, itemlist);

        // Update descriptions
        string[] itemDescriptions = Main.Config.GetText(TextName.ItemFlavor);
        string[] moveDescriptions = Main.Config.GetText(TextName.MoveFlavor);
        
        // TM01-TM92
        for (int i = 0; i < 92 && i < tmlist.Length; i++) 
            itemDescriptions[328 + i] = moveDescriptions[tmlist[i]];
        // TM93-TM95
        for (int i = 92; i < 95 && i < tmlist.Length; i++) 
            itemDescriptions[618 + i - 92] = moveDescriptions[tmlist[i]];
        // TM96-TM100
        for (int i = 95; i < 100 && i < tmlist.Length; i++) 
            itemDescriptions[690 + i - 95] = moveDescriptions[tmlist[i]];
            
        // Extra TMs (101-107) - Item IDs 721-727
        for (int i = 100; i < 107 && i < tmlist.Length; i++)
            itemDescriptions[721 + (i - 100)] = moveDescriptions[tmlist[i]];

        // Extra TMs (108+) - Start from expandedTMStartID (default 960)
        if (itemDescriptions.Length > expandedTMStartID)
        {
            for (int i = 107; i < tmlist.Length; i++)
            {
                int target = expandedTMStartID + (i - 107);
                if (target < itemDescriptions.Length) 
                    itemDescriptions[target] = moveDescriptions[tmlist[i]];
            }
        }
        
        Main.Config.SetText(TextName.ItemFlavor, itemDescriptions);
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();
        File.WriteAllBytes(codebin, data);
    }

    private void B_RandomTM_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize TMs?", "Move compatibility will be the same as the base TMs.") != DialogResult.Yes) return;

        int[] randomMoves = Enumerable.Range(1, movelist.Length - 1).Select(i => i).ToArray();
        Util.Shuffle(randomMoves);

        int[] banned = [.. Legal.Z_Moves, .. new[] { 165, 464, 621 }];
        int ctr = 0;

        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            int val = Array.IndexOf(movelist, dgvTM.Rows[i].Cells[2].Value);
            if (banned.Contains(val)) continue;
            while (banned.Contains(randomMoves[ctr])) ctr++;

            dgvTM.Rows[i].Cells[2].Value = movelist[randomMoves[ctr++]];
        }
        WinFormsUtil.Alert("Randomized!");
    }

    internal static ushort[] GetTMHMList()
    {
        if (Main.ExeFSPath == null) return [];
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) return [];
        
        byte[] data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) return [];

        // Use universal TM table signature detection
        byte[] tmSig = [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01];
        int dataoffset = Util.IndexOfBytes(data, tmSig, 0x100000, 0);
        if (dataoffset < 0)
        {
            dataoffset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + Signature.Length;
            if (Main.Config.USUM) dataoffset += 0x22;
        }

        // Static callers always get the base 100 TMs — expansion detection
        // requires the instance context (offset) to avoid false positives.
        int count = 100;

        List<ushort> tms = [];
        for (int i = 0; i < count; i++) 
            tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        return [.. tms];
    }

    private void NUD_TMCount_ValueChanged(object sender, EventArgs e)
    {
        if (skipUpdate) return;
        GetList();
    }

    private void B_ExportTxt_Click(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "TMs.txt", Filter = "Text File|*.txt" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var lines = new List<string>();
        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            string moveName = dgvTM.Rows[i].Cells[2].Value?.ToString() ?? "";
            lines.Add($"TM{i + 1:00}: {moveName}");
        }
        File.WriteAllLines(sfd.FileName, lines);
        WinFormsUtil.Alert("TM data exported!");
    }

    private void B_ImportTxt_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string[] lines = File.ReadAllLines(ofd.FileName);
        int updated = 0;
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

            // Parse lines like "TM01: Work Up" or "TM01: 526"
            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            string tmPart = line.Substring(0, colonIdx).Trim();
            string movePart = line.Substring(colonIdx + 1).Trim();

            // Extract TM number
            if (!tmPart.StartsWith("TM", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(tmPart.Substring(2), out int tmNum)) continue;
            int rowIdx = tmNum - 1;
            if (rowIdx < 0 || rowIdx >= dgvTM.Rows.Count) continue;

            // Try to match move by name first, then by index
            int moveIdx = Array.IndexOf(movelist, movePart);
            if (moveIdx < 0 && int.TryParse(movePart, out int moveId) && moveId >= 0 && moveId < movelist.Length)
                moveIdx = moveId;
            if (moveIdx < 0) continue;

            dgvTM.Rows[rowIdx].Cells[2].Value = movelist[moveIdx];
            updated++;
        }
        WinFormsUtil.Alert($"Imported {updated} TM entries!");
    }

    private void B_UpdateDesc_Click(object sender, EventArgs e)
    {
        const string disclaimer = "Warning: This will overwrite ALL TM item descriptions in the game text with the descriptions of the moves they currently teach.\n\n" +
                                   "This action cannot be undone. Are you sure you want to proceed?";
        
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, disclaimer) != DialogResult.Yes)
            return;

        // Build current TM list from the grid
        List<ushort> currentTMs = [];
        for (int i = 0; i < dgvTM.Rows.Count; i++)
            currentTMs.Add((ushort)Array.IndexOf(movelist, dgvTM.Rows[i].Cells[2].Value));

        ushort[] tmlist = [.. currentTMs];

        // Sync move descriptions into item descriptions (same logic as SetList)
        string[] itemDescriptions = Main.Config.GetText(TextName.ItemFlavor);
        string[] moveDescriptions = Main.Config.GetText(TextName.MoveFlavor);
        for (int i = 1 - 1; i <= 92 - 1; i++) // TM01 - TM92
            itemDescriptions[328 + i] = moveDescriptions[tmlist[i]];
        for (int i = 93 - 1; i <= 95 - 1; i++) // TM93 - TM95
            itemDescriptions[618 + i - 92] = moveDescriptions[tmlist[i]];
        for (int i = 96 - 1; i <= 100 - 1; i++) // TM96 - TM100
            itemDescriptions[690 + i - 95] = moveDescriptions[tmlist[i]];
        Main.Config.SetText(TextName.ItemFlavor, itemDescriptions);

        WinFormsUtil.Alert("TM item descriptions updated to match current moves!");
    }

    private void TB_Offset_TextChanged(object sender, EventArgs e)
    {
        if (uint.TryParse(TB_Offset.Text, System.Globalization.NumberStyles.HexNumber, null, out uint res))
        {
            offset = (int)res;
            GetList();
        }
    }
}