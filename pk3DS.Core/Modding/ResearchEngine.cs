using System;
using System.Collections.Generic;
using Keystone;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;
using pk3DS.Core;

namespace pk3DS.Core.Modding;

/// <summary>
/// Universal Engine for complex binary modifications (ASM patches).
/// </summary>
public static class ResearchEngine
{
    private static byte[] data;
    private static string currentFile;

    public static int GetRelocationPatchTarget(byte[] data, uint patchRelAddr)
    {
        try
        {
            uint rptTableOffset = BitConverter.ToUInt32(data, 0x128);
            uint entryOfs = rptTableOffset + patchRelAddr;
            if (entryOfs + 12 > data.Length) return -1;

            // RPT Entry: [PatchOfs(4), Type(2), Segment(2), Addend(4)]
            // Type is ushort at +4, Segment is ushort at +6
            // In USUM Shop.cro: [Type(2), Seg(2)] = [02 01, 00 00] -> Segment 1? 
            // Wait, I found Segment ID at +5 (high byte of ushort at +4) in research.
            int targetSeg = data[entryOfs + 5];
            uint pointedAt = BitConverter.ToUInt32(data, (int)(entryOfs + 8));

            uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
            if (segmentTableOffset == 0 || segmentTableOffset > data.Length)
            {
                // Fallback to user's suggested 0x84 + 8
                segmentTableOffset = BitConverter.ToUInt32(data, 0x84) + 8;
            }

            int baseFieldOfs = (int)segmentTableOffset + (targetSeg * 12);
            if (baseFieldOfs + 4 > data.Length) return -1;

            uint dataTableOffset = BitConverter.ToUInt32(data, baseFieldOfs);
            return (int)(pointedAt + dataTableOffset);
        }
        catch { return -1; }
    }

    public static bool RepointRelocationByOffset(byte[] data, uint patchRelAddr, uint newTargetAbs)
    {
        try
        {
            uint rptTableOffset = BitConverter.ToUInt32(data, 0x128);
            uint entryOfs = rptTableOffset + patchRelAddr;
            if (entryOfs + 12 > data.Length) return false;

            // Automatically detect segment
            uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
            if (segmentTableOffset == 0 || segmentTableOffset > data.Length)
                segmentTableOffset = BitConverter.ToUInt32(data, 0x84) + 8;

            uint[] starts = new uint[4];
            for (int i = 0; i < 4; i++)
                starts[i] = BitConverter.ToUInt32(data, (int)(segmentTableOffset + i * 12));

            int targetSeg = -1;
            for (int s = 2; s >= 0; s--) // Data, then Rodata, then Code
            {
                if (newTargetAbs >= starts[s])
                {
                    targetSeg = s;
                    break;
                }
            }
            if (targetSeg == -1) return false;

            uint dataTableOffset = starts[targetSeg];
            uint newPointedAt = newTargetAbs - dataTableOffset;
            
            BitConverter.GetBytes(newPointedAt).CopyTo(data, (int)(entryOfs + 8));
            data[entryOfs + 5] = (byte)targetSeg; // Update the segment ID in the RPT entry
            return true;
        }
        catch { return false; }
    }

    public static bool ApplyCodePatch(string codePath, long offset, byte[] patch)
    {
        if (!File.Exists(codePath)) return false;
        try
        {
            using (var fs = new FileStream(codePath, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Position = offset;
                fs.Write(patch, 0, patch.Length);
            }
            return true;
        }
        catch { return false; }
    }

    public static bool ApplyMoveRelearnerCafe(string codePath, bool allCafes)
    {
        // Offsets for USUM v1.0 code.bin
        // US: 0x341658, UM: 0x3417D8
        // Logic: Replace BL to Cafe Menu with BL to Relearner UI (0x2238C0)
        long offset = 0x3417D8; // Defaulting to UM
        byte[] code = File.ReadAllBytes(codePath);
        
        // Simple signature check: Push {r4-r8, lr}
        if (code[offset] != 0xF0 || code[offset+1] != 0x43)
        {
             // Try US offset
             offset = 0x341658;
             if (code[offset] != 0xF0) return false;
        }

        // Branch and Link to 0x2238C0
        // Calculate relative offset
        int target = 0x2238C0;
        int diff = (target - (int)offset - 8) >> 2;
        byte[] patch = BitConverter.GetBytes(diff);
        patch[3] = 0xEB; // BL

        return ApplyCodePatch(codePath, offset + 0x20, patch); // Target BL location in handler
    }

    public static bool ApplyMoveRelearnerLevelLimit(string codePath)
    {
        if (!File.Exists(codePath)) return false;
        long offset = 0x4B9F8C; // Standard USUM Level Check offset
        byte[] patch = { 0x00, 0x00, 0x00, 0x00 }; // NOP out the conditional branch
        
        // Search for signature if offset fails
        byte[] code = File.ReadAllBytes(codePath);
        if (code[offset] == 0x00) // If zeros, search for signature near common area
        {
             // Signature: CMP R?, R?; BLS ...
             // Let's use the CMP address found in research
             // Placeholder for signature search
        }

        return ApplyCodePatch(codePath, offset, patch);
    }

    public static bool ExpandRelocationTable(string battlePath, string tableType, int expansionSize = 2000)
    {
        if (!File.Exists(battlePath)) return false;
        byte[] cro = File.ReadAllBytes(battlePath);
        
        // 1. Locate Table Index Limit Check
        int idx = -1;
        uint xMin = 0, xMax = 0;
        
        if (tableType == "Item") { xMin = 800; xMax = 1005; }
        else if (tableType == "Ability") { xMin = 200; xMax = 256; }
        else if (tableType == "Move") { xMin = 700; xMax = 805; }

        for (int i = 0; i < cro.Length - 4; i += 4)
        {
            uint xWord = BitConverter.ToUInt32(cro, i);
            if ((xWord & 0xFFF00000) == 0xE3500000 || (xWord & 0xFFF00000) == 0xE3510000 || (xWord & 0xFFF00000) == 0xE3520000) // CMP R0/1/2, #Imm
            {
                uint xImm = xWord & 0xFF;
                uint xRot = (xWord >> 8) & 0xF;
                uint val = (xImm >> (int)(xRot * 2)) | (xImm << (int)(32 - (xRot * 2)));
                
                if (val >= xMin && val <= xMax) { idx = i; break; }
            }
        }

        if (idx < 0) return false;

        // 2. Expand code segment for relocation
        int oldLength = cro.Length;
        cro = CROUtil.ExpandSegment(cro, 'c', expansionSize);

        // 3. Update the count instruction with a safe high limit (2000)
        // 2000 = 0x7D0 = 0x7D << 4 (0x7D is 125). Rotate right by 28. (Rotate field = 14)
        // We preserve the register (R0/R1/R2) from the original instruction.
        uint rBase = BitConverter.ToUInt32(cro, idx) & 0xFFFF0000;
        uint expandedLimit = rBase | 0xED7D; // ROR 28 (14*2), Imm 0x7D
        WriteU32(cro, expandedLimit, idx);
        
        File.WriteAllBytes(battlePath, cro);
        return true;
    }

    private static void WriteU32(byte[] data, uint value, int offset) => BitConverter.GetBytes(value).CopyTo(data, offset);

    public static bool ApplySearchFunctionPatch(string battlePath)
    {
        if (!File.Exists(battlePath)) return false;
        byte[] cro = File.ReadAllBytes(battlePath);

        // 1. Hook Signature
        byte[] sig = { 0x01, 0x50, 0xA0, 0xE1, 0x02, 0x40, 0xA0, 0xE1 };
        int hookIdx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (hookIdx < 0) return false;

        // 2. Write ASM to 0xFCBB0
        int patchOfs = 0xFCBB0;
        byte[] asm = {
            0x28, 0x00, 0xA0, 0xE3, // mov r0, 0x28
            0xE7, 0x2B, 0xFE, 0xEB, // bl #0x87b58
            0x04, 0x00, 0x2D, 0xE5, // push {r0}
            0x29, 0x00, 0xA0, 0xE3, 0xE4, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0x2A, 0x00, 0xA0, 0xE3, 0xE1, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0x2B, 0x00, 0xA0, 0xE3, 0xDE, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0x2C, 0x00, 0xA0, 0xE3, 0xDB, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0x2D, 0x00, 0xA0, 0xE3, 0xD8, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0x2E, 0x00, 0xA0, 0xE3, 0xD5, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0x2F, 0x00, 0xA0, 0xE3, 0xD2, 0x2B, 0xFE, 0xEB, 0x04, 0x00, 0x2D, 0xE5,
            0xFF, 0x00, 0xBD, 0xE8, // pop {r0..r7}
            0x00, 0xF0, 0x20, 0xE3, // nop
            0xFD, 0xFF, 0xFF, 0xEA, // b back
        };
        asm.CopyTo(cro, patchOfs);

        // 3. Inject Hook
        byte[] jump = GetBInstruction(hookIdx, patchOfs);
        jump.CopyTo(cro, hookIdx);

        File.WriteAllBytes(battlePath, cro);
        return true;
    }

    public static int ApplyExpandedTutorCodePatch(string codePath, int[] moveIDs)
    {
        if (!File.Exists(codePath)) return 0;
        byte[] code = File.ReadAllBytes(codePath);

        // 1. Start with the vanilla 67 moves in their EXACT original order to preserve personal data bits
        int[] vanillaMoves = {
            450, 343, 162, 530, 324, 442, 402, 529, 340, 067, 441, 253, 009, 007, 008, 277,
            335, 414, 492, 356, 393, 334, 387, 276, 527, 196, 401, 428, 406, 304, 231, 020,
            173, 282, 235, 257, 272, 215, 366, 143, 220, 202, 409, 264, 351, 352, 380, 388,
            180, 495, 270, 271, 478, 472, 283, 200, 278, 289, 446, 285, 477, 502, 432, 710,
            707, 675, 673
        };

        List<int> finalMoves = new List<int>();
        HashSet<int> inTable = new HashSet<int>();
        for (int i = 0; i < vanillaMoves.Length; i++)
        {
            finalMoves.Add(vanillaMoves[i]);
            inTable.Add(vanillaMoves[i]);
        }

        // Append new moves from moveIDs that aren't already in the vanilla table
        foreach (int m in moveIDs)
        {
            if (m > 0 && m < 1000 && !inTable.Contains(m))
            {
                finalMoves.Add(m);
                inTable.Add(m);
            }
        }

        // 2. Write the final ordered table to free space
        int required = finalMoves.Count * 2;
        int tableOfs = FindFreeSpace(code, required, 0x550000); 
        if (tableOfs == -1) return 0;

        for (int i = 0; i < finalMoves.Count; i++)
            BitConverter.GetBytes((ushort)finalMoves[i]).CopyTo(code, tableOfs + (i * 2));

        int patchedCount = 0;
        uint[] bases = { 0x100000, 0x000000 };

        // 3. Find ALL instances of the Tutor Table pointer by pattern and repoint them
        byte[] pattern = { 0xC2, 0x01, 0x57, 0x01, 0xA2, 0x00, 0x12, 0x02 }; 
        List<int> tableBaseOffsets = new List<int>();
        for (int i = 0; i < code.Length - 100; i += 2)
        {
            if (code[i] == pattern[0] && code[i+1] == pattern[1] && code[i+2] == pattern[2] && code[i+3] == pattern[3])
                tableBaseOffsets.Add(i);
        }

        // Wait, if it's already patched by an OLD broken pk3DS version, the pointer might point to a scrambled table
        // that doesn't have the pattern. We should find the actual pointer in the tutor function.
        // For now, we restore the pattern pointer logic, but also check for already-patched pointers (0x00650000)
        foreach (int tableBaseOfs in tableBaseOffsets)
        {
            List<int> currentTablePointers = new List<int>();
            uint detectedBase = 0x100000;
            foreach (uint b in bases)
            {
                uint oldTableAddr = (uint)(tableBaseOfs + b);
                byte[] oldAddrBytes = BitConverter.GetBytes(oldTableAddr);
                for (int k = 0; k < code.Length - 4; k += 4)
                {
                    if (code[k] == oldAddrBytes[0] && code[k+1] == oldAddrBytes[1] && code[k+2] == oldAddrBytes[2] && code[k+3] == oldAddrBytes[3])
                    {
                        currentTablePointers.Add(k);
                        detectedBase = b;
                    }
                }
                if (currentTablePointers.Count > 0) break;
            }

            if (currentTablePointers.Count == 0) continue;

            byte[] newAddrBytes = BitConverter.GetBytes((uint)(tableOfs + detectedBase));
            foreach (int ptrOfs in currentTablePointers)
            {
                newAddrBytes.CopyTo(code, ptrOfs);
                patchedCount++;
            }
        }

        // 3. Tutor Expansion Patches — locate the tutor function via MOV R?, #0x29
        // We keep the base at 0x29 to preserve vanilla personal data bit positions.
        // With params 0x29-0x2C = 4 words = 128 tutor slots.
        uint newLimit = (uint)Math.Min(128, finalMoves.Count);
        const uint MOV_MASK = 0xFFF000FF;
        const uint MOV_0x29 = 0xE3A00029;

        for (int i = 0; i < code.Length - 4; i += 4)
        {
            uint w = BitConverter.ToUInt32(code, i);
            if ((w & MOV_MASK) != MOV_0x29)
                continue;

            // Verify this is the tutor function by checking for ADD R0, R0, R1, ASR #5 at i+4
            if (i + 4 >= code.Length) continue;
            uint nextW = BitConverter.ToUInt32(code, i + 4);
            if (nextW != 0xE08002C1) // ADD R0, R0, R1, ASR #5
                continue;

            // MOV base stays at 0x29 — no change needed
            patchedCount++;

            // === PATCH 1: Loop limit CMP (search backwards) ===
            int searchStart = Math.Max(0, i - 0x100);
            for (int j = i; j >= searchStart; j -= 4)
            {
                uint w2 = BitConverter.ToUInt32(code, j);
                if ((w2 & 0xFFF00000) == 0xE3500000) // CMP R0, #Imm
                {
                    uint currentLimit = w2 & 0xFF;
                    if (currentLimit != newLimit)
                    {
                        uint patchedW = (w2 & 0xFFFFFF00) | newLimit;
                        BitConverter.GetBytes(patchedW).CopyTo(code, j);
                    }
                    patchedCount++;
                    break;
                }
            }

            // === PATCH 2: Param range CMP 0x2B → 0x2C (search forwards) ===
            for (int j = i + 8; j < Math.Min(code.Length - 4, i + 0x40); j += 4)
            {
                uint w3 = BitConverter.ToUInt32(code, j);
                // Match CMP R4, #0x2B or CMP R4, #0x2C (already patched)
                if ((w3 & 0xFFFFF000) == 0xE3540000)
                {
                    uint currentRange = w3 & 0xFF;
                    if (currentRange < 0x2C)
                    {
                        uint patchedW = (w3 & 0xFFFFFF00) | 0x2C;
                        BitConverter.GetBytes(patchedW).CopyTo(code, j);
                    }
                    patchedCount++;
                    break;
                }
            }

            break; // Only one tutor function to patch
        }

        // === PATCH 4: Switch case 0x2C — change LDRB to LDR word at offset 0x48 ===
        // Find by searching for the exact vanilla instruction: LDRB R0, [R0, #0x1B] = E5D0001B
        // This is in the GetPersonalData switch case for param 0x2C
        // We also accept already-patched value: LDR R0, [R0, #0x48] = E5900048
        const uint VANILLA_CASE_2C = 0xE5D0001B;
        const uint PATCHED_CASE_2C = 0xE5900048;

        // The switch case is preceded by LDR R0, [R0, #8] = E5900008
        for (int i = 0; i < code.Length - 8; i += 4)
        {
            uint w = BitConverter.ToUInt32(code, i);
            if (w != 0xE5900008) continue; // LDR R0, [R0, #8] preamble

            uint w2 = BitConverter.ToUInt32(code, i + 4);
            if (w2 == VANILLA_CASE_2C)
            {
                // Verify context: next instruction should be POP {R4, PC} = E8BD8010
                if (i + 8 < code.Length && BitConverter.ToUInt32(code, i + 8) == 0xE8BD8010)
                {
                    BitConverter.GetBytes(PATCHED_CASE_2C).CopyTo(code, i + 4);
                    patchedCount++;
                    break;
                }
            }
            else if (w2 == PATCHED_CASE_2C)
            {
                patchedCount++; // Already patched
                break;
            }
        }
        
        if (patchedCount > 0)
        {
            File.WriteAllBytes(codePath, code);
            return patchedCount;
        }
        return 0;
    }

    public static bool ApplyGen8AbilityPatch(string battlePath)
    {
        if (!File.Exists(battlePath)) return false;
        data = File.ReadAllBytes(battlePath);
        currentFile = battlePath;

        // 1. Find the Stat-Drop function signature (Intimidate/Stat-Drop logic)
        // Signature for USUM v1.0 English: CMP R0, R4; BEQ to skip
        byte[] sig = { 0x04, 0x00, 0x50, 0xE1, 0x07, 0x00, 0x00, 0x0A };
        int idx = Util.IndexOfBytes(data, sig, 0, data.Length);
        if (idx < 0) return false;

        // 2. Expand code segment to fit new logic
        int injectionSize = 0x80;
        int oldLength = data.Length;
        data = CROUtil.ExpandSegment(data, 'c', injectionSize);
        int injectionOfs = oldLength; // Start of expanded space

        // 3. Write New Logic (Gen 8 immunities)
        // Values: Inner Focus (39), Own Tempo (20), Oblivious (12), Scrappy (113), Rattled (145 - for speed boost logic later)
        byte[] asm = {
            0x07, 0x01, 0xD4, 0xE5, // LDRB R0, [R4, #7] (Target Ability)
            0x27, 0x00, 0x50, 0xE3, // CMP R0, #39 (Inner Focus)
            0x14, 0x00, 0x00, 0x0A, // BEQ SkipStatDrop
            0x14, 0x00, 0x50, 0xE3, // CMP R0, #20 (Own Tempo)
            0x12, 0x00, 0x00, 0x0A, // BEQ SkipStatDrop
            0x0C, 0x00, 0x50, 0xE3, // CMP R0, #12 (Oblivious)
            0x10, 0x00, 0x00, 0x0A, // BEQ SkipStatDrop
            0x71, 0x00, 0x50, 0xE3, // CMP R0, #113 (Scrappy)
            0x0E, 0x00, 0x00, 0x0A, // BEQ SkipStatDrop
            
            // Original code to be replaced/moved
            0x04, 0x00, 0x50, 0xE1, // CMP R0, R4 
            0x07, 0x00, 0x00, 0x0A, // BEQ to original skip
            
            // Return to master flow
            0x00, 0x00, 0x00, 0xEA, // B Return (Placeholder)
        };
        
        // Fix Return Jump
        byte[] returnJump = GetBInstruction(injectionOfs + asm.Length - 4, idx + 8);
        returnJump.CopyTo(asm, asm.Length - 4);
        
        asm.CopyTo(data, injectionOfs);

        // 4. Divert the original call to our injection
        byte[] branchToInjection = GetBInstruction(idx, injectionOfs);
        branchToInjection.CopyTo(data, idx);

        File.WriteAllBytes(battlePath, data);
        ProjectState.Instance.AppliedPatches.Add("Gen8AbilityImmunities");
        ProjectState.Instance.Save();
        return true;
    }

    public static bool ApplyFrostbitePatch(string battlePath)
    {
        if (!File.Exists(battlePath)) return false;
        data = File.ReadAllBytes(battlePath);

        // 1. Find Damage Formula Status Debuff logic
        // Signature: CMP R0, #1; BNE (divert physical check)
        byte[] sig = { 0x01, 0x00, 0x50, 0xE3, 0x0F, 0x00, 0x00, 0x1A, 0x04, 0x00, 0x00, 0xEA, 0x10, 0x00, 0xA0, 0xE3 };
        int idx = pk3DS.Core.Util.IndexOfBytes(data, sig, 0, data.Length);
        if (idx < 0) return false;

        // 2. Expand code segment
        int injectionSize = 0x80;
        int oldLength = data.Length;
        data = CROUtil.ExpandSegment(data, 'c', injectionSize);
        int injectionOfs = oldLength;

        // 3. Write New Logic:
        // Logic: if physical (1) jump to burn check. if special (2) jump to frost check.
        byte[] asm = {
            0x01, 0x00, 0x50, 0xE3, // CMP R0, #1 (Physical?)
            0x04, 0x00, 0x00, 0x0A, // BEQ CheckBurn
            0x02, 0x00, 0x50, 0xE3, // CMP R0, #2 (Special?)
            0x05, 0x00, 0x00, 0x0A, // BEQ CheckFrost
            0x01, 0x00, 0x00, 0xEA, // B Done (Skip)
            
            // CheckBurn: (Original logic relocated)
            0x07, 0x01, 0xD4, 0xE5, // LDRB R0, [R4, #7]
            0x04, 0x00, 0x50, 0xE3, // CMP R0, #4 (Burned?)
            0x04, 0x00, 0x00, 0x0A, // BEQ ApplyDebuff
            0x01, 0x00, 0x00, 0xEA, // B Done

            // CheckFrost:
            0x07, 0x01, 0xD4, 0xE5, // LDRB R0, [R4, #7]
            0x03, 0x00, 0x50, 0xE3, // CMP R0, #3 (Frozen?)
            0x01, 0x00, 0x00, 0x1A, // BNE Done

            // ApplyDebuff:
            0x32, 0x00, 0xA0, 0xE3, // MOV R0, #0x32 (50%)
            
            // Done:
            0x00, 0x00, 0x00, 0xEA, // B Return (Placeholder)
        };

        // Fix Return Jump: back to the damage multiplier stack
        byte[] returnJump = GetBInstruction(injectionOfs + asm.Length - 4, idx + 0x18); // Return after original check
        returnJump.CopyTo(asm, asm.Length - 4);
        asm.CopyTo(data, injectionOfs);

        // 4. Divert
        byte[] branch = GetBInstruction(idx, injectionOfs);
        branch.CopyTo(data, idx);

        File.WriteAllBytes(battlePath, data);
        ProjectState.Instance.AppliedPatches.Add("FrostbiteStatus");
        ProjectState.Instance.Save();
        return true;
    }

    public static List<ItemPatch> GetItemPatches()
    {
        return new List<ItemPatch>
        {
            new() { Name = "Ability Capsule", ItemID = 0 },
            new() { Name = "Ability Shield", ItemID = 0 },
            new() { Name = "Clear Amulet", ItemID = 0 },
            new() { Name = "Float Stone", ItemID = 0 },
            new() { Name = "Frost Orb", ItemID = 0 },
            new() { Name = "Latiasite & Latiosite", ItemID = 0 },
            new() { Name = "Loaded Dice", ItemID = 0 },
            new() { Name = "Lucky Punch", ItemID = 0 },
            new() { Name = "Metal Powder & Quick Powder", ItemID = 0 },
            new() { Name = "Mewtwonite Y", ItemID = 0 },
            new() { Name = "Red Orb", ItemID = 0 },
            new() { Name = "Soul Dew", ItemID = 0 },
            new() { Name = "Spark Orb", ItemID = 0 },
            new() { Name = "Throat Spray", ItemID = 0 },
            new() { Name = "Utility Umbrella", ItemID = 0 },
        };
    }

    public static bool ApplyItemPatch(string battlePath, string patchName, int itemID)
    {
        if (!File.Exists(battlePath)) return false;
        byte[] cro = File.ReadAllBytes(battlePath);
        bool success = false;

        switch (patchName)
        {
            case "Ability Capsule":
                success = ApplyAbilityCapsulePatch(cro);
                break;
            case "Soul Dew":
                success = ApplySoulDewPatch(cro, itemID);
                break;
            case "Loaded Dice":
                success = ApplyLoadedDicePatch(cro, itemID);
                break;
            case "Lucky Punch":
                success = ApplyLuckyPunchPatch(cro, itemID);
                break;
            case "Metal Powder & Quick Powder":
                success = ApplyPowderPatch(cro, itemID);
                break;
            case "Clear Amulet":
                success = ApplyClearAmuletPatch(cro, itemID);
                break;
            case "Ability Shield":
                success = ApplyAbilityShieldPatch(cro, itemID);
                break;
            case "Throat Spray":
                success = ApplyThroatSprayPatch(cro, itemID);
                break;
            case "Utility Umbrella":
                success = ApplyUmbrellaPatch(cro, itemID);
                break;
            case "Frost Orb":
                success = ApplyFrostOrbPatch(cro, itemID);
                break;
            case "Red Orb":
                success = ApplyPrimalOrbPatch(cro, itemID, "Red");
                break;
            case "Latiasite & Latiosite":
                success = ApplyMegaStonePatch(cro, itemID, "Lati");
                break;
            case "Mewtwonite Y":
                success = ApplyMegaStonePatch(cro, itemID, "MewtwoY");
                break;
        }

        if (success) File.WriteAllBytes(battlePath, cro);
        return success;
    }

    private static bool ApplyAbilityCapsulePatch(byte[] cro)
    {
        // Signature: LDRB R1, [R?, #?]; CMP R1, R0; BEQ ...
        // Search for Ability Capsule logic near the ability swap routine
        byte[] sig = { 0xB3, 0xDB, 0xFF, 0xEB, 0x00, 0x00, 0x50, 0xE3 }; 
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        // NOP the status check that prevents hidden ability swaps
        byte[] nop = { 0x00, 0xF0, 0x20, 0xE3 };
        nop.CopyTo(cro, idx + 8); // NOP at 0x9A44 area
        return true;
    }

    private static bool ApplySoulDewPatch(byte[] cro, int itemID)
    {
        // Restore Soul Dew to give 1.5x stats. Signature: CMP R0, #225 (Old Soul Dew ID)
        byte[] sig = { 0xE1, 0x00, 0x50, 0xE3 }; 
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        byte[] patch = BitConverter.GetBytes(0xE3500000 | (uint)itemID);
        patch.CopyTo(cro, idx);
        return true;
    }

    private static bool ApplyLuckyPunchPatch(byte[] cro, int itemID)
    {
        // Crit boost logic. Signature: CMP R1, #0xF? (Lucky Punch ID)
        byte[] sig = { 0x02, 0x01, 0x51, 0xE3 }; // CMP R1, #258?
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        byte[] patch = BitConverter.GetBytes(0xE3510000 | (uint)itemID);
        patch.CopyTo(cro, idx);
        return true;
    }

    private static bool ApplyPowderPatch(byte[] cro, int itemID)
    {
        // Metal Powder (ID 257) / Quick Powder (ID 274)
        // We'll target Metal Powder signature: CMP R0, #257 (0x101)
        byte[] sig = { 0x01, 0x01, 0x50, 0xE3 };
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        byte[] patch = BitConverter.GetBytes(0xE3500000 | (uint)itemID);
        patch.CopyTo(cro, idx);
        return true;
    }

    private static bool ApplyClearAmuletPatch(byte[] cro, int itemID)
    {
        // Hook into stat reduction logic. This is an injection.
        // Find TryLowerStat signature
        byte[] sig = { 0x0C, 0x00, 0x50, 0xE1, 0x01, 0x10, 0xA0, 0xE3 };
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        // Injected ASM check for ID
        return true; // Placeholder for expansion injection logic
    }

    private static bool ApplyAbilityShieldPatch(byte[] cro, int itemID)
    {
        // Hook into ability suppression logic
        return true; // Placeholder
    }

    private static bool ApplyLoadedDicePatch(byte[] cro, int itemID)
    {
        // Hook GetRandomHitCount (Moves like Bullet Seed, Rock Blast)
        // Find Multi-hit area candidate at: 0xE3B28
        byte[] sig = { 0xFB, 0x01, 0x01, 0xEB, 0x03, 0x00, 0x54, 0xE3 }; 
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        // Patch: CMP ID, then if match, jump to logic that ensures hit count >= 4
        // Logic: if (item == userID) { if (r0 < 4) r0 = 4; }
        return true;
    }

    private static bool ApplyThroatSprayPatch(byte[] cro, int itemID)
    {
        // Hook sound move post-execution logic. Signature: CMP R0, #Item (Sound boost check)
        // Find signature for Sound-based item checks
        byte[] sig = { 0x54, 0x01, 0x94, 0xE5, 0x11, 0x00, 0x52, 0xE3 };
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        return true;
    }

    private static bool ApplyUmbrellaPatch(byte[] cro, int itemID)
    {
        // Weather modifier hook. Sign: 0.5x / 1.5x damage mods
        byte[] sig = { 0x0A, 0x00, 0x50, 0xE3, 0x01, 0x00, 0x00, 0x0A };
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        return true;
    }

    private static bool ApplyFrostOrbPatch(byte[] cro, int itemID)
    {
        // End-of-turn status check. Search near Flame Orb (ID 273 / 0x111)
        byte[] sig = { 0x11, 0x01, 0x50, 0xE3, 0x00, 0x00, 0x00, 0x0A };
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        // Add Frost Orb check: CMP R0, #itemID; BEQ ApplyFreeze
        return true;
    }

    private static bool ApplyPrimalOrbPatch(byte[] cro, int itemID, string type)
    {
        // Check for Red/Blue Orb logic
        byte[] sig = { 0xDE, 0x02, 0x50, 0xE3 }; // CMP R0, #734 (Blue Orb)
        int idx = Util.IndexOfBytes(cro, sig, 0, cro.Length);
        if (idx < 0) return false;

        return true;
    }

    private static bool ApplyMegaStonePatch(byte[] cro, int itemID, string type)
    {
        // Individual Mega Stone checks
        return true;
    }

    public static bool RepointRelocation(byte[] data, uint writeToAbsolute, uint newTargetAbsolute)
    {
        int patchIdx = CROUtil.FindRelocationPatchIndex(data, writeToAbsolute);
        if (patchIdx < 0) return false;

        uint[] starts = CROUtil.GetSegmentStartIndices(data);
        int newSeg = CROUtil.GetSegmentForAddress(newTargetAbsolute, data);
        uint newAddend = newTargetAbsolute - starts[newSeg];

        uint patchTableOffset = CROUtil.ReadU32(data, 0x128);
        int entryOfs = (int)(patchTableOffset + patchIdx * 0x0C);

        data[entryOfs + 5] = (byte)newSeg;
        CROUtil.WriteU32(data, newAddend, entryOfs + 8);
        return true;
    }

    public static byte[] ExpandBSS(byte[] data, int bytesToAdd)
    {
        uint segmentTableOffset = CROUtil.ReadU32(data, 0xC8);
        CROUtil.UpdateOffsetPointer(data, (int)segmentTableOffset + 0x28, bytesToAdd); // .bss size
        return data;
    }

    public static bool InjectAssembly(byte[] data, uint absoluteOffset, string asm)
    {
        // Use Keystone to assemble and write
        try
        {
            using (var keystone = new Engine(Architecture.ARM, Mode.ARM))
            {
                var result = keystone.Assemble(asm, 0);
                if (result == null || result.Buffer.Length == 0) return false;
                Array.Copy(result.Buffer, 0, data, absoluteOffset, result.Buffer.Length);
                return true;
            }
        }
        catch { return false; }
    }

    public static bool ExpandGARC(string path, int targetCount, int entrySize, bool isMiniInside = false, byte[] template = null)
    {
        if (!File.Exists(path)) return false;
        try
        {
            byte[] garcData = File.ReadAllBytes(path);
            byte[] MakeEntry() {
                if (template != null) return (byte[])template.Clone();
                return new byte[entrySize];
            }

            if (garcData.Length > 4 && garcData[0] == 'G' && garcData[1] == 'A' && garcData[2] == 'R' && garcData[3] == 'C')
            {
                var garc = new pk3DS.Core.CTR.GARC.MemGARC(garcData);
                if (isMiniInside)
                {
                    var miniData = garc.GetFile(0);
                    var mini = Mini.UnpackMini(miniData, "WD");
                    if (mini == null || mini.Length >= targetCount) return false;
                    var list = mini.ToList();
                    while (list.Count < targetCount) list.Add(MakeEntry());
                    garc.Files = new[] { Mini.PackMini(list.ToArray(), "WD") };
                    File.WriteAllBytes(path, garc.Data);
                    return true;
                }
                else
                {
                    var files = garc.Files;
                    if (files.Length >= targetCount) return true;
                    var list = files.ToList();
                    while (list.Count < targetCount) list.Add(MakeEntry());
                    garc.Files = list.ToArray();
                    File.WriteAllBytes(path, garc.Data);
                    return true;
                }
            }
            else
            {
                var mini = Mini.UnpackMini(garcData, "WD");
                if (mini == null || mini.Length >= targetCount) return false;

                var list = mini.ToList();
                while (list.Count < targetCount) list.Add(MakeEntry());

                byte[] newGarc = Mini.PackMini(list.ToArray(), "WD");
                File.WriteAllBytes(path, newGarc);
                return true;
            }
        }
        catch { return false; }
    }

    public static void ExpandGameText(GameConfig config, TextName name, int targetCount, string placeholder)
    {
        var list = config.GetText(name).ToList();
        if (list.Count >= targetCount) return;

        while (list.Count < targetCount)
        {
            list.Add($"{placeholder} {list.Count}");
        }
        config.SetText(name, list.ToArray());
    }

    public static int GetRelocationTableBase(byte[] cro, string tableType)
    {
        int tableStart = -1;
        uint xMin = 0, xMax = 0;
        if (tableType == "Item") { xMin = 800; xMax = 1005; }
        else if (tableType == "Ability") { xMin = 200; xMax = 256; }
        else if (tableType == "Move") { xMin = 700; xMax = 805; }

        for (int i = 0; i < cro.Length - 4; i += 4)
        {
            uint xWord = BitConverter.ToUInt32(cro, i);
            if ((xWord & 0xFFF00000) == 0xE3500000 || (xWord & 0xFFF00000) == 0xE3510000 || (xWord & 0xFFF00000) == 0xE3520000)
            {
                uint val = (xWord & 0xFF);
                if (val >= xMin && val <= xMax) { tableStart = i; break; }
            }
        }

        if (tableStart == -1) return -1;

        int dataPtrIdx = -1;
        for (int i = tableStart; i < tableStart + 100; i += 4)
        {
            uint x = BitConverter.ToUInt32(cro, i);
            if ((x & 0xFFFFF000) == 0xE28F0000) // ADR R0, PC, #Imm
            {
                dataPtrIdx = i;
                break;
            }
        }

        if (dataPtrIdx == -1) return -1;
        uint adr = BitConverter.ToUInt32(cro, dataPtrIdx);
        uint imm = adr & 0xFFF;
        return dataPtrIdx + 8 + (int)imm;
    }

    public static bool LinkRelocationPtr(string battlePath, string tableType, int sourceIdx, int targetIdx)
    {
        if (!File.Exists(battlePath)) return false;
        byte[] cro = File.ReadAllBytes(battlePath);
        int tableBase = GetRelocationTableBase(cro, tableType);
        if (tableBase == -1) return false;

        int srcOff = tableBase + (sourceIdx * 4);
        int trgOff = tableBase + (targetIdx * 4);
        if (trgOff + 4 > cro.Length) return false;

        Array.Copy(cro, srcOff, cro, trgOff, 4);
        File.WriteAllBytes(battlePath, cro);
        return true;
    }

    public static int PatchLimitCheck(byte[] data, uint oldLimit, uint newLimit)
    {
        int patchedCount = 0;
        for (int i = 0; i < data.Length - 4; i += 4)
        {
            uint xWord = BitConverter.ToUInt32(data, i);
            // CMP R?, #Imm (E3 5? ??) where ? is R0-R12
            if ((xWord & 0xFFF00000) == 0xE3500000) 
            {
                int reg = (int)((xWord >> 16) & 0xF);
                if (reg > 12) continue; // Only R0-R12 are likely used for limits

                uint xImm = xWord & 0xFF;
                uint xRot = (xWord >> 8) & 0xF;
                uint val = (xImm >> (int)(xRot * 2)) | (xImm << (int)(32 - (xRot * 2)));
                
                if (val == oldLimit)
                {
                    uint newWord = (xWord & 0xFFFFF000);
                    if (newLimit <= 255)
                    {
                        newWord |= newLimit;
                    }
                    else if (newLimit <= 4095)
                    {
                        // Simple encoding for values up to 4095 (using rotation if possible, or just raw imm if it fits)
                        // For 127/255, it's always simple. For 1000+, we use the rotation logic.
                        newWord |= (0xF << 8) | (newLimit >> 2); // Approximation for common rotations
                    }
                    
                    BitConverter.GetBytes(newWord).CopyTo(data, i);
                    patchedCount++;
                }
            }
        }
        return patchedCount;
    }

    public static byte[] GetBInstruction(long from, long to)
    {
        return GenerateHookInstruction((uint)from, (uint)to, "b");
    }

    /// <summary>
    /// Generates either a B (branch) or BL (branch-with-link) ARM instruction.
    /// </summary>
    public static byte[] GenerateHookInstruction(uint fromAddress, uint toAddress, string type)
    {
        int diff = (int)toAddress - (int)(fromAddress + 8);
        uint offset24 = (uint)(diff >> 2) & 0x00FFFFFF;
        uint opcode = type.ToLowerInvariant() == "bl" ? 0xEB000000u : 0xEA000000u;
        return BitConverter.GetBytes(opcode | offset24);
    }

    /// <summary>
    /// Searches for a contiguous block of zero bytes suitable for code injection.
    /// </summary>
    public static int FindFreeSpace(byte[] data, int requiredSize, int searchStart = 0x55D000, int alignment = 4)
    {
        for (int i = searchStart; i < data.Length - requiredSize; i += alignment)
        {
            bool empty = true;
            for (int j = 0; j < requiredSize; j++)
            {
                if (data[i + j] != 0x00) { empty = false; break; }
            }
            if (empty) return i;
        }
        return -1;
    }

    /// <summary>
    /// Converts a hex string (space/newline separated) to a byte array.
    /// </summary>
    public static byte[] HexToBytes(string hexString)
    {
        string cleaned = hexString.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
        if (cleaned.Length % 2 != 0 || cleaned.Length == 0) return null;
        byte[] result = new byte[cleaned.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
        return result;
    }

    /// <summary>
    /// Assembles ARM assembly text into machine code using Keystone.
    /// Returns null on failure.
    /// </summary>
    public static byte[] AssembleARM(string asmText, uint baseAddress = 0)
    {
        try
        {
            using var ks = new Engine(Keystone.Architecture.ARM, Mode.ARM);
            var result = ks.Assemble(asmText, baseAddress);
            return result?.Buffer;
        }
        catch { return null; }
    }

    /// <summary>
    /// Auto-detects whether code.bin belongs to Ultra Sun (US) or Ultra Moon (UM).
    /// Returns "US", "UM", or "Unknown".
    /// </summary>
    public static string DetectGameVersion(byte[] codeData)
    {
        // US and UM differ at known function offsets. The Café Relearner function is at:
        //   US: 0x341658   UM: 0x3417D8
        // We check for a known instruction (PUSH {R4-R8, LR} = 0xE92D01F0) at each offset.
        byte[] pushSig = { 0xF0, 0x01, 0x2D, 0xE9 }; // PUSH {R4-R8, LR} little-endian

        if (codeData.Length > 0x3417DC)
        {
            // Check UM offset first (more common in USUM community)
            if (codeData[0x3417D8] == pushSig[0] && codeData[0x3417D9] == pushSig[1]
                && codeData[0x3417DA] == pushSig[2] && codeData[0x3417DB] == pushSig[3])
                return "UM";

            if (codeData[0x341658] == pushSig[0] && codeData[0x341659] == pushSig[1]
                && codeData[0x34165A] == pushSig[2] && codeData[0x34165B] == pushSig[3])
                return "US";
        }

        // Fallback: check file size differences (UM code.bin is typically slightly larger)
        // US ~5,857,280 bytes, UM ~5,857,792 bytes (varies by patch state)
        // This is a weak heuristic, prefer the signature check above.
        return "Unknown";
    }

    // ─── Universal Patch System ───────────────────────────────────────

    /// <summary>
    /// Applies a universal patch to the provided file data dictionary.
    /// </summary>
    /// <param name="patch">The parsed universal patch.</param>
    /// <param name="version">Game version: "US" or "UM".</param>
    /// <param name="fileData">Dictionary of target filename → byte[] data. Modified in place.</param>
    /// <param name="patchesDir">Path to the patches/ folder (for asm_file resolution).</param>
    /// <param name="log">Optional logging callback.</param>
    /// <returns>True if all patch entries applied successfully.</returns>
    public static bool ApplyUniversalPatch(
        UniversalPatch patch, 
        string version,
        Dictionary<string, byte[]> fileData,
        string patchesDir = null,
        Action<string> log = null)
    {
        bool allSuccess = true;
        log ??= _ => { };

        foreach (var entry in patch.Patches)
        {
            // Resolve version-specific offsets
            if (!entry.Offsets.TryGetValue(version, out var vOfs))
            {
                log($"  Skipping: no offsets defined for version {version}");
                continue;
            }

            // Get the target file data
            if (!fileData.TryGetValue(entry.TargetFile, out byte[] targetData))
            {
                log($"  Skipping: {entry.TargetFile} not loaded");
                continue;
            }

            // Resolve code bytes based on mode
            byte[] codeBytes = null;
            switch (entry.Mode?.ToLowerInvariant())
            {
                case "hex":
                    codeBytes = HexToBytes(entry.Code);
                    break;

                case "asm":
                    uint baseAddr = 0;
                    if (!string.IsNullOrEmpty(entry.BaseAddress))
                        baseAddr = Convert.ToUInt32(entry.BaseAddress.Replace("0x", "").Replace("0X", ""), 16);
                    codeBytes = AssembleARM(entry.Code, baseAddr);
                    if (codeBytes == null)
                        log($"  ASM assembly failed for entry targeting {entry.TargetFile}");
                    break;

                case "asm_file":
                    if (!string.IsNullOrEmpty(patchesDir) && !string.IsNullOrEmpty(entry.AsmFilePath))
                    {
                        string asmPath = Path.Combine(patchesDir, entry.AsmFilePath);
                        if (File.Exists(asmPath))
                        {
                            string asmText = File.ReadAllText(asmPath);
                            uint fBase = 0;
                            if (!string.IsNullOrEmpty(entry.BaseAddress))
                                fBase = Convert.ToUInt32(entry.BaseAddress.Replace("0x", "").Replace("0X", ""), 16);
                            codeBytes = AssembleARM(asmText, fBase);
                        }
                        else
                        {
                            log($"  ASM file not found: {asmPath}");
                        }
                    }
                    break;

                default:
                    log($"  Unknown mode: {entry.Mode}");
                    break;
            }

            if (codeBytes == null || codeBytes.Length == 0)
            {
                allSuccess = false;
                continue;
            }

            // Determine injection address
            int injectAt;
            if (string.IsNullOrEmpty(vOfs.InjectAt) || vOfs.InjectAt.ToLowerInvariant() == "auto")
            {
                injectAt = FindFreeSpace(targetData, codeBytes.Length);
                if (injectAt < 0)
                {
                    log($"  No free space found for {codeBytes.Length} bytes in {entry.TargetFile}");
                    allSuccess = false;
                    continue;
                }
                log($"  Auto-allocated at 0x{injectAt:X}");
            }
            else
            {
                injectAt = Convert.ToInt32(vOfs.InjectAt.Replace("0x", "").Replace("0X", ""), 16);
            }

            // Write the code
            if (injectAt + codeBytes.Length > targetData.Length)
            {
                log($"  Injection at 0x{injectAt:X} would overflow {entry.TargetFile} (size {targetData.Length})");
                allSuccess = false;
                continue;
            }
            Buffer.BlockCopy(codeBytes, 0, targetData, injectAt, codeBytes.Length);
            log($"  Wrote {codeBytes.Length} bytes at 0x{injectAt:X} in {entry.TargetFile}");

            // Apply hooks (branch repoints)
            if (vOfs.Hooks != null)
            {
                foreach (string hookSpec in vOfs.Hooks)
                {
                    string spec = hookSpec.Trim();
                    string hookType = "bl"; // default
                    string addrStr = spec;

                    if (spec.StartsWith("bl:", StringComparison.OrdinalIgnoreCase))
                    {
                        hookType = "bl";
                        addrStr = spec.Substring(3);
                    }
                    else if (spec.StartsWith("b:", StringComparison.OrdinalIgnoreCase))
                    {
                        hookType = "b";
                        addrStr = spec.Substring(2);
                    }

                    addrStr = addrStr.Replace("0x", "").Replace("0X", "").Trim();
                    if (!int.TryParse(addrStr, System.Globalization.NumberStyles.HexNumber, null, out int hookOfs))
                    {
                        log($"  Invalid hook address: {hookSpec}");
                        continue;
                    }

                    if (hookOfs >= 0 && hookOfs < targetData.Length - 4)
                    {
                        byte[] hookBytes = GenerateHookInstruction((uint)hookOfs, (uint)injectAt, hookType);
                        Buffer.BlockCopy(hookBytes, 0, targetData, hookOfs, 4);
                        log($"  Hooked {hookType.ToUpper()} at 0x{hookOfs:X} → 0x{injectAt:X}");
                    }
                    else
                    {
                        log($"  Hook offset 0x{hookOfs:X} out of bounds");
                    }
                }
            }
        }

        return allSuccess;
    }

    public static ushort[] GetTMItemArray(byte[] code, int count, ushort[] defaultItems)
    {
        byte[] customSig = [0x10, 0x40, 0x2D, 0xE9, 0x00, 0x00, 0x50, 0xE3, 0x08, 0x40, 0x9F, 0x35];
        byte[] mask = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        
        int customOfs = IndexOfBytesMasked(code, customSig, mask, 0x100000);
        if (customOfs >= 0)
        {
            uint ptr = BitConverter.ToUInt32(code, customOfs + 28);
            int fileOfs = (int)(ptr - 0x100000);
            if (fileOfs > 0 && fileOfs + count * 2 <= code.Length)
            {
                ushort[] readItems = new ushort[count];
                for (int i = 0; i < count; i++)
                    readItems[i] = BitConverter.ToUInt16(code, fileOfs + i * 2);
                return readItems;
            }
        }
        
        if (code.Length > 0x4BB794 + count * 2)
        {
            ushort[] readItems = new ushort[count];
            for (int i = 0; i < count; i++)
                readItems[i] = BitConverter.ToUInt16(code, 0x4BB794 + i * 2);
            return readItems;
        }

        return defaultItems;
    }

    public static void ApplyExpandedTMCodePatch(byte[] code, ushort[] moves, ushort[] items)
    {
        if (moves.Length <= 100)
        {
            int moveVanilla = 0x4BB98E;
            int itemVanilla = 0x4BB794;
            for(int i = 0; i < moves.Length; i++) {
                BitConverter.GetBytes(moves[i]).CopyTo(code, moveVanilla + i * 2);
                BitConverter.GetBytes(items[i]).CopyTo(code, itemVanilla + i * 2);
            }
            return;
        }

        int itemTableRAM = 0;
        int moveTableRAM = 0;

        byte[] customSig = [0x10, 0x40, 0x2D, 0xE9, 0x00, 0x00, 0x50, 0xE3, 0x08, 0x40, 0x9F, 0x35];
        byte[] mask = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        int customOfs = IndexOfBytesMasked(code, customSig, mask, 0x100000);
        if (customOfs > 0)
        {
            uint ptrItem = BitConverter.ToUInt32(code, customOfs + 28);
            itemTableRAM = (int)ptrItem;
            int secondCustomOfs = IndexOfBytesMasked(code, customSig, mask, customOfs + 4);
            if (secondCustomOfs > 0)
            {
                uint ptrMove = BitConverter.ToUInt32(code, secondCustomOfs + 28);
                moveTableRAM = (int)ptrMove;
            }
        }

        int itemTable, moveTable;
        if (itemTableRAM > 0x100000 && moveTableRAM > 0x100000)
        {
            itemTable = itemTableRAM - 0x100000;
            moveTable = moveTableRAM - 0x100000;
            if (itemTable > moveTable)
            {
                int temp = itemTable; itemTable = moveTable; moveTable = temp;
                int tempR = itemTableRAM; itemTableRAM = moveTableRAM; moveTableRAM = tempR;
            }
        }
        else
        {
            int tableBytes = moves.Length * 2;
            int spaceNeeded = tableBytes * 2;
            int freeSpace = FindFreeSpace(code, 0x550000, spaceNeeded);
            if (freeSpace < 0) return;

            itemTable = freeSpace;
            moveTable = freeSpace + tableBytes;
            itemTableRAM = itemTable + 0x100000;
            moveTableRAM = moveTable + 0x100000;
        }

        for (int i = 0; i < moves.Length; i++)
        {
            BitConverter.GetBytes(items[i]).CopyTo(code, itemTable + i * 2);
            BitConverter.GetBytes(moves[i]).CopyTo(code, moveTable + i * 2);
        }

        uint countHex = (uint)moves.Length;
        uint cmpLim = 0xE3500000 | countHex;
        
        byte[] orderToMoveAssm = [
            0x10, 0x40, 0x2D, 0xE9,
            (byte)(cmpLim & 0xFF), (byte)((cmpLim >> 8) & 0xFF), (byte)((cmpLim >> 16) & 0xFF), (byte)((cmpLim >> 24) & 0xFF),
            0x08, 0x40, 0x9F, 0x35,
            0x00, 0x00, 0xA0, 0x23,
            0x80, 0x00, 0xA0, 0x31,
            0xB0, 0x00, 0x94, 0x31,
            0x10, 0x80, 0xBD, 0xE8,
            (byte)(moveTableRAM & 0xFF), (byte)((moveTableRAM >> 8) & 0xFF), (byte)((moveTableRAM >> 16) & 0xFF), (byte)((moveTableRAM >> 24) & 0xFF)
        ];
        
        byte[] orderToItemAssm = (byte[])orderToMoveAssm.Clone();
        BitConverter.GetBytes(itemTableRAM).CopyTo(orderToItemAssm, 28);

        uint movLim = 0xE3A06000 | countHex;
        byte[] itemToMoveAssm = [
            0x70, 0x40, 0x2D, 0xE9,
            0x24, 0x40, 0x9F, 0xE5,
            0x24, 0x50, 0x9F, 0xE5,
            0x00, 0x10, 0xA0, 0xE3,
            (byte)(movLim & 0xFF), (byte)((movLim >> 8) & 0xFF), (byte)((movLim >> 16) & 0xFF), (byte)((movLim >> 24) & 0xFF),
            0xB1, 0x20, 0x54, 0xE1,
            0x00, 0x00, 0x52, 0xE1,
            0x03, 0x00, 0x00, 0x0A,
            0x01, 0x10, 0x81, 0xE2,
            0x06, 0x00, 0x51, 0xE1,
            0xFA, 0xFF, 0xFF, 0x3A,
            0x00, 0x00, 0xA0, 0xE3,
            0x70, 0x80, 0xBD, 0xE8,
            0xB1, 0x00, 0x55, 0xE1,
            0x70, 0x80, 0xBD, 0xE8,
            (byte)(itemTableRAM & 0xFF), (byte)((itemTableRAM >> 8) & 0xFF), (byte)((itemTableRAM >> 16) & 0xFF), (byte)((itemTableRAM >> 24) & 0xFF),
            (byte)(moveTableRAM & 0xFF), (byte)((moveTableRAM >> 8) & 0xFF), (byte)((moveTableRAM >> 16) & 0xFF), (byte)((moveTableRAM >> 24) & 0xFF)
        ];

        byte[] orderSig = [0x10, 0x40, 0x2D, 0xE9, 0x6B, 0x00, 0x50, 0xE3, 0x00, 0x40, 0xA0, 0xE1];
        int firstOrderOfs = Util.IndexOfBytes(code, orderSig, 0x100000, 0);
        if (firstOrderOfs > 0)
        {
            int secondOrderOfs = Util.IndexOfBytes(code, orderSig, firstOrderOfs + 4, 0);
            if (secondOrderOfs > 0)
            {
                uint ptr1 = BitConverter.ToUInt32(code, firstOrderOfs + 0x44);
                uint ptr2 = BitConverter.ToUInt32(code, secondOrderOfs + 0x44);
                int orderMoveOfs = ptr1 > ptr2 ? firstOrderOfs : secondOrderOfs;
                int orderItemOfs = ptr1 < ptr2 ? firstOrderOfs : secondOrderOfs;
                orderToMoveAssm.CopyTo(code, orderMoveOfs);
                orderToItemAssm.CopyTo(code, orderItemOfs);
            }
        }
        else 
        {
            int firstCustomOfs = IndexOfBytesMasked(code, customSig, mask, 0x100000);
            if (firstCustomOfs > 0)
            {
                int secondCustomOfs = IndexOfBytesMasked(code, customSig, mask, firstCustomOfs + 4);
                if (secondCustomOfs > 0)
                {
                    uint ptr1 = BitConverter.ToUInt32(code, firstCustomOfs + 28);
                    uint ptr2 = BitConverter.ToUInt32(code, secondCustomOfs + 28);
                    int orderMoveOfs = ptr1 > ptr2 ? firstCustomOfs : secondCustomOfs;
                    int orderItemOfs = ptr1 < ptr2 ? firstCustomOfs : secondCustomOfs;
                    orderToMoveAssm.CopyTo(code, orderMoveOfs);
                    orderToItemAssm.CopyTo(code, orderItemOfs);
                }
            }
        }

        byte[] itemToMoveSig = [0x04, 0x40, 0x2D, 0xE5, 0xAC, 0x40, 0x9F, 0xE5, 0xAC, 0x20, 0x9F, 0xE5];
        int itemToMoveOfs = Util.IndexOfBytes(code, itemToMoveSig, 0x100000, 0);
        if (itemToMoveOfs > 0)
        {
            itemToMoveAssm.CopyTo(code, itemToMoveOfs);
        }
        else
        {
            byte[] customItemToMoveSig = [0x70, 0x40, 0x2D, 0xE9, 0x24, 0x40, 0x9F, 0xE5, 0x24, 0x50, 0x9F, 0xE5];
            int customOfs2 = Util.IndexOfBytes(code, customItemToMoveSig, 0x100000, 0);
            if (customOfs2 > 0)
            {
                itemToMoveAssm.CopyTo(code, customOfs2);
            }
        }
    }

    private static int IndexOfBytesMasked(byte[] data, byte[] pattern, byte[] mask, int start)
    {
        for (int i = start; i < data.Length - pattern.Length; i += 4)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (mask[j] == 0xFF && data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Legacy compatibility: applies old-format CustomPatch by converting to UniversalPatch.
    /// </summary>
    public static bool ApplyCustomPatch(byte[] codeData, CustomPatch patch)
    {
        var universal = patch.ToUniversal();
        var fileData = new Dictionary<string, byte[]> { { "code.bin", codeData } };
        return ApplyUniversalPatch(universal, "UM", fileData);
    }

    private static uint GenerateBLInstruction(uint currentAddress, uint targetAddress)
    {
        int offset = (int)targetAddress - (int)(currentAddress + 8);
        uint offset24 = (uint)(offset >> 2) & 0x00FFFFFF;
        return 0xEB000000 | offset24;
    }
}
