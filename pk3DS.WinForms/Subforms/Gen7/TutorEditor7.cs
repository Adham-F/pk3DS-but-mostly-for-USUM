using System.Collections.Generic;
using pk3DS.Core;
using pk3DS.Core.Modding;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.CTR;

namespace pk3DS.WinForms;

public partial class TutorEditor7 : Form
{
    private readonly string CROPath = Path.Combine(Main.RomFSPath, "Shop.cro");

    private byte[] data;
    private int ofs_counts;
    private byte[] len_BPTutor;

    private const int SafeZoneBase = 0x5800; 
    private const int SafeZoneSize = 2048;

    public TutorEditor7()
    {
        if (!File.Exists(CROPath))
        {
            WinFormsUtil.Error("CRO does not exist! Closing.", CROPath);
            Close();
            return;
        }
        InitializeComponent();

        data = File.ReadAllBytes(CROPath);
        LoadShopOffsets();

        SetupDGV();
        CB_LocationBPMove.Items.AddRange(locationsTutor);
        CB_LocationBPMove.SelectedIndex = 0;
        B_AddMove.Enabled = B_DelMove.Enabled = true;
    }

    private void LoadShopOffsets()
    {
        // Use verified USUM RPT key for tutor counts
        ofs_counts = ResearchEngine.GetRelocationPatchTarget(data, 0x03C);
        if (ofs_counts != -1)
        {
            len_BPTutor = data.Skip(ofs_counts).Take(4).ToArray();
            Text = "Tutor Editor (RPT Mode)";
            return;
        }

        ofs_counts = ProjectState.Instance.GetOffset("TutorCountsOffset", 0);
        if (ofs_counts <= 0) ScanForSignatures();
        Text = "Tutor Editor (Legacy Mode)";
        len_BPTutor = data.Skip(ofs_counts).Take(4).ToArray();
    }

    private void ScanForSignatures()
    {
        byte[] sig_counts = [0x0F, 0x11, 0x11, 0x0F];
        int idx_c = Util.IndexOfBytes(data, sig_counts, 0, data.Length - sig_counts.Length);
        if (idx_c >= 0) ofs_counts = idx_c; else ofs_counts = 0x52D2;
        SaveShopOffsets();
    }

    private void SaveShopOffsets()
    {
        pk3DS.Core.Modding.ProjectState.Instance.SetOffset("TutorCountsOffset", ofs_counts);
    }

    private static readonly uint[] TutorPatchAddrs = {
        0x4C8, // Melemele
        0x4D4, // Akala
        0x4E0, // Ula'ula
        0x4EC, // Battle Tree
    };

    private int GetTutorOffset(int index)
    {
        if (index < TutorPatchAddrs.Length)
        {
            int rptOfs = ResearchEngine.GetRelocationPatchTarget(data, TutorPatchAddrs[index]);
            if (rptOfs != -1) return rptOfs;
        }
        // Fallback: Legacy contiguous logic
        int baseOfs = 0x54DE;
        return baseOfs + (len_BPTutor.Take(index).Sum(z => z) * 4);
    }

    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private static readonly int[] Tutors_USUM =
    [
        450, 343, 162, 530, 324, 442, 402, 529, 340, 067, 441, 253, 009, 007, 008, // 0-14
        277, 335, 414, 492, 356, 393, 334, 387, 276, 527, 196, 401, 428, 406, 304, 231, // 15-30
        020, 173, 282, 235, 257, 272, 215, 366, 143, 220, 202, 409, 264, 351, 352, // 31-45
        380, 388, 180, 495, 270, 271, 478, 472, 283, 200, 278, 289, 446, 285, // 46-59
        477, 502, 432, 710, 707, 675, 673 // 60-66
    ];

    private readonly string[] locationsTutor =
    [
        "Big Wave Beach",
        "Heahea Beach",
        "Ula'ula Beach",
        "Battle Tree",
    ];

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entryBPMove > -1) SetListBPMove();
        SyncTutorsToCodeBin();
        File.WriteAllBytes(CROPath, data);
        SaveShopOffsets();
        Close();
    }

    private void SyncTutorsToCodeBin()
    {
        if (string.IsNullOrEmpty(Main.ExeFSPath)) return;
        string binName = File.Exists(Path.Combine(Main.ExeFSPath, ".code.bin")) ? ".code.bin" : "code.bin";
        string fullCodePath = Path.Combine(Main.ExeFSPath, binName);
        if (!File.Exists(fullCodePath)) return;

        byte[] codeBin = File.ReadAllBytes(fullCodePath);
        int offset = ProjectState.Instance.TutorCodeOffset;

        // Anchor search for USUM Tutor Move Table
        byte[] onOffSig = [0x5F, 0x6F, 0x6E, 0x5F, 0x6F, 0x66, 0x66, 0xFF];
        int onOffIdx = Util.IndexOfBytes(codeBin, onOffSig, 0, codeBin.Length);
        if (onOffIdx >= 0)
        {
            // The move ID table is usually located shortly after the _on_off string in USUM
            // We can use this as a reference point if the length table signature fails.
        }

        if (offset <= 0)
        {
            byte[] sig = Main.Config.USUM 
                ? new byte[] { 0x0F, 0x00, 0x11, 0x00, 0x0F, 0x00, 0x14, 0x00 } // USUM: 15, 17, 15, 20
                : new byte[] { 0x0F, 0x00, 0x11, 0x00, 0x11, 0x00, 0x0F, 0x00 }; // SM: 15, 17, 17, 15
            int sigIdx = pk3DS.Core.Util.IndexOfBytes(codeBin, sig, 0x100000, 0);
            if (sigIdx >= 0)
            {
                offset = sigIdx; 
                ProjectState.Instance.TutorCodeOffset = offset;
                ProjectState.Instance.Save();
            }
        }

        if (offset > 0)
        {
            int groups = Main.Config.USUM ? 5 : 4;
            for (int t = 0; t < groups; t++)
            {
                int destOfs = offset + (t * 2);
                if (destOfs + 1 < codeBin.Length && t < len_BPTutor.Length)
                {
                    codeBin[destOfs] = (byte)len_BPTutor[t];
                    codeBin[destOfs + 1] = 0;
                }
            }

            // 2. Sync Move ID Table
            // The move ID table is a list of ushorts (2 bytes each) for every tutorable move.
            byte[] moveSig = [0xC2, 0x01, 0x57, 0x01, 0xA2, 0x00, 0x12, 0x02]; // First 4 Beach 1 moves
            int moveTableOfs = Util.IndexOfBytes(codeBin, moveSig, 0, codeBin.Length);
            
            if (moveTableOfs < 0 && onOffIdx >= 0)
            {
                // Fallback: Search near _on_off anchor
                // The table is usually within 0x400 bytes of _on_off
                int searchStart = Math.Max(0, onOffIdx - 0x400);
                int searchEnd = Math.Min(codeBin.Length, onOffIdx + 0x400);
                moveTableOfs = Util.IndexOfBytes(codeBin, moveSig, searchStart, searchEnd - searchStart);
                
                if (moveTableOfs < 0)
                {
                    // More aggressive fallback: Search for just the first two moves
                    byte[] shortSig = [0xC2, 0x01, 0x57, 0x01];
                    moveTableOfs = Util.IndexOfBytes(codeBin, shortSig, searchStart, searchEnd - searchStart);
                }
            }

            if (moveTableOfs >= 0)
            {
                // We found the table! Now sync all moves from the shop.
                var tutorData = GetUSUMTutorData(CROPath, Tutors_USUM);
                int[] shopMoves = tutorData.moves;
                
                for (int i = 0; i < shopMoves.Length; i++)
                {
                    int m_ofs = moveTableOfs + (i * 2);
                    if (m_ofs + 2 <= codeBin.Length)
                    {
                        BitConverter.GetBytes((ushort)shopMoves[i]).CopyTo(codeBin, m_ofs);
                    }
                }
            }

            // 3. Patch hardcoded limit checks in code.bin (e.g., CMP R?, #67)
            if (Main.Config.USUM && len_BPTutor.Sum(z => z) > 60)
            {
                ResearchEngine.PatchLimitCheck(codeBin, 67, 127);
            }
            
            File.WriteAllBytes(fullCodePath, codeBin);
        }
    }

    private void B_Cancel_Click(object sender, EventArgs e) => Close();

    private void SetupDGV()
    {
        dgvmvMove.Items.AddRange(movelist); // add only the Names
    }

    private int entryBPMove = -1;

    private void ChangeIndexBPMove(object sender, EventArgs e)
    {
        if (entryBPMove > -1) SetListBPMove();
        entryBPMove = CB_LocationBPMove.SelectedIndex;
        GetListBPMove();
    }

    private int FindSafeSpace(int bytes)
    {
        // Simple allocator: find first block of 0xFFFFs (unallocated/padding) in the safe zone
        for (int i = 0; i <= SafeZoneSize - bytes; i += 2)
        {
            bool free = true;
            for (int j = 0; j < bytes; j += 2)
            {
                ushort val = BitConverter.ToUInt16(data, SafeZoneBase + i + j);
                if (val != 0xFFFF && val != 0x0000)
                {
                    free = false;
                    break;
                }
            }
            if (free) return SafeZoneBase + i;
        }
        return -1;
    }

    private void GetListBPMove()
    {
        if (entryBPMove < 0 || entryBPMove >= len_BPTutor.Length) return;
        dgvmv.Rows.Clear();
        int count = len_BPTutor[entryBPMove];
        if (count > 0)
            dgvmv.Rows.Add(count);
        int ofs = GetTutorOffset(entryBPMove);
        if (ofs == -1) return;

        for (int i = 0; i < count; i++)
        {
            dgvmv.Rows[i].Cells[0].Value = i.ToString();
            int m_ofs = ofs + (4 * i);
            int p_ofs = m_ofs + 2;
            if (p_ofs + 2 > data.Length) break;

            int moveID = BitConverter.ToUInt16(data, m_ofs);
            if (moveID >= movelist.Length) moveID = 0;

            dgvmv.Rows[i].Cells[1].Value = movelist[moveID];
            dgvmv.Rows[i].Cells[2].Value = BitConverter.ToUInt16(data, p_ofs).ToString();
        }
    }

    private void SetListBPMove()
    {
        if (entryBPMove < 0 || entryBPMove >= len_BPTutor.Length) return;
        int count = dgvmv.Rows.Count;
        int tutorOfs = GetTutorOffset(entryBPMove);
        if (tutorOfs <= 0) return;

        if (count > len_BPTutor[entryBPMove])
        {
            // Expansion into Mega Safe Zone
            int required = count * 4; 
            int freeOfs = FindSafeSpace(required);
            if (freeOfs == -1)
            {
                WinFormsUtil.Alert("No space left in Safe Zone for expansion.");
                return;
            }
            
            // Wipe the rest of the zone with markers on first expansion if needed
            if (BitConverter.ToUInt16(data, SafeZoneBase) == 0)
            {
                for (int i = 0; i < SafeZoneSize; i += 2)
                    BitConverter.GetBytes((ushort)0xFFFF).CopyTo(data, SafeZoneBase + i);
            }

            ResearchEngine.RepointRelocationByOffset(data, TutorPatchAddrs[entryBPMove], (uint)freeOfs);
            tutorOfs = freeOfs;
        }

        for (int i = 0; i < count; i++)
        {
            int move = Array.IndexOf(movelist, dgvmv.Rows[i].Cells[1].Value);
            uint price = 4; uint.TryParse(dgvmv.Rows[i].Cells[2].Value.ToString(), out price);

            int m_ofs = tutorOfs + (i * 4);
            BitConverter.GetBytes((ushort)move).CopyTo(data, m_ofs);
            BitConverter.GetBytes((ushort)price).CopyTo(data, m_ofs + 2);
        }

        data[ofs_counts + entryBPMove] = (byte)count;
        len_BPTutor[entryBPMove] = (byte)count;

        // Apply automatic expansion patch to Shop.cro if moves > 67
        // We check if the total count exceeds vanilla limits or if any group is expanded.
        if (len_BPTutor.Sum(z => z) > 60 || count > 15)
        {
            ResearchEngine.PatchLimitCheck(data, 67, 127);
        }
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        WinFormsUtil.Alert("Not currently implemented.");
    }

    private void B_AddMove_Click(object sender, EventArgs e)
    {
        if (entryBPMove < 0) return;
        dgvmv.Rows.Add(1);
        int idx = dgvmv.Rows.Count - 1;
        dgvmv.Rows[idx].Cells[0].Value = idx.ToString();
        dgvmv.Rows[idx].Cells[1].Value = movelist[1];
        dgvmv.Rows[idx].Cells[2].Value = "4";
    }

    private void B_DelMove_Click(object sender, EventArgs e)
    {
        if (entryBPMove < 0 || dgvmv.Rows.Count == 0) return;
        dgvmv.Rows.RemoveAt(dgvmv.Rows.Count - 1);
    }

    public static (int[] moves, int[] lengths) GetUSUMTutorData(string croPath, int[] defaultMoves)
    {
        int[] defaultLengths = [15, 16, 15, 14, 7];
        if (!File.Exists(croPath)) return (defaultMoves, defaultLengths);
        byte[] d = File.ReadAllBytes(croPath);
        
        // 1. Try to find the counts offset
        int c_ofs = ResearchEngine.GetRelocationPatchTarget(d, 0x03C);
        if (c_ofs == -1)
        {
            byte[] sig_counts = [0x0F, 0x11, 0x11, 0x0F];
            int idx_c = Util.IndexOfBytes(d, sig_counts, 0, d.Length - sig_counts.Length);
            if (idx_c >= 0) c_ofs = idx_c;
        }

        // 2. Read lengths
        int[] lengths;
        if (c_ofs != -1)
        {
            lengths = d.Skip(c_ofs).Take(16).Select(b => (int)b).ToArray();
            // Filter out 0 lengths or stop if we hit 0 after some valid ones
            List<int> validLengths = [];
            foreach (var l in lengths)
            {
                if (l > 0 && l < 64) validLengths.Add(l);
                else if (validLengths.Count > 0) break;
            }
            lengths = validLengths.Count > 0 ? validLengths.ToArray() : defaultLengths;
        }
        else
        {
            lengths = defaultLengths;
        }

        // 3. Read moves
        List<int> moves = [];
        for (int i = 0; i < lengths.Length; i++)
        {
            int tutorOfs = -1;
            if (i < TutorPatchAddrs.Length)
                tutorOfs = ResearchEngine.GetRelocationPatchTarget(d, TutorPatchAddrs[i]);
            
            if (tutorOfs == -1 || tutorOfs + (lengths[i] * 4) > d.Length)
            {
                // Fallback to defaults for this group if we can't find it
                int start = defaultLengths.Take(i).Sum();
                int count = Math.Min(lengths[i], defaultMoves.Length - start);
                if (count > 0)
                    moves.AddRange(defaultMoves.Skip(start).Take(count).Select(z => (int)z));
                continue;
            }

            for (int m = 0; m < lengths[i]; m++)
            {
                int m_ofs = tutorOfs + (m * 4);
                int mID = BitConverter.ToUInt16(d, m_ofs);
                if (mID > 0 && mID < 1000) moves.Add(mID);
                else moves.Add(0); // Placeholder for corrupted entry
            }
        }

        return (moves.Count > 0 ? moves.ToArray() : defaultMoves, lengths);
    }
}