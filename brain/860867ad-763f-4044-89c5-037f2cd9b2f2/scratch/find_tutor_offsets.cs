
using System;
using System.IO;

class Program {
    static void Main() {
        string path = @"c:\Users\fulto\Downloads\3DS\pk3DS-master\RomFS\Shop.cro";
        if (!File.Exists(path)) { Console.WriteLine("No Shop.cro"); return; }
        byte[] data = File.ReadAllBytes(path);
        
        // Search for Tutor Move ID Table (USUM Vanilla)
        byte[] sig = { 0xAD, 0x00, 0xD7, 0x00, 0x0F, 0x02, 0xB0, 0x01 };
        for (int i = 0; i < data.Length - 8; i++) {
            bool match = true;
            for (int j = 0; j < 8; j++) {
                if (data[i+j] != sig[j]) { match = false; break; }
            }
            if (match) Console.WriteLine($"Table found at: 0x{i:X}");
        }

        // Search for Limit Check (CMP R?, #0x43)
        // ARM: E3 5? 00 43
        for (int i = 0; i < data.Length - 4; i++) {
            if (data[i] == 0x43 && data[i+1] == 0x00 && (data[i+2] & 0xF0) == 0x50 && data[i+3] == 0xE3) {
                Console.WriteLine($"Limit Check found at: 0x{i:X} (Instruction: {data[i+3]:X2}{data[i+2]:X2}{data[i+1]:X2}{data[i+0]:X2})");
            }
        }
    }
}
