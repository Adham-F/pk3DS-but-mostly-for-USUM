using System;
using System.Linq;

namespace pk3DS.Core.Structures.PersonalInfo;

public class PersonalInfoSM : PersonalInfoXY
{
    public new const int SIZE = 0x54;

    public PersonalInfoSM(byte[] data)
    {
        Data = new byte[SIZE];
        TMHM = new bool[128];
        TutorFlags = new bool[160];
        if (data == null)
            return;
        
        Array.Copy(data, 0, Data, 0, Math.Min(data.Length, SIZE));

        // TM/HM: 0x28 to 0x37 (16 bytes / 128 bits)
        TMHM = GetBits(Data.Skip(0x28).Take(0x10).ToArray());
        
        // Tutor Flags: 0x38 to 0x4B (20 bytes / 160 bits)
        // This covers Word 28, 29, 2A, 2B, 2C.
        byte[] tutorData = new byte[20];
        Array.Copy(Data, 0x38, tutorData, 0, Math.Min(20, Data.Length - 0x38));
        TutorFlags = GetBits(tutorData);
    }

    public bool[] TutorFlags;

    public override byte[] Write()
    {
        SetBits(TMHM).CopyTo(Data, 0x28);
        SetBits(TutorFlags).CopyTo(Data, 0x38);
        return Data;
    }

    public int SpecialZ_Item { get => BitConverter.ToUInt16(Data, 0x4C); set => BitConverter.GetBytes((ushort)value).CopyTo(Data, 0x4C); }
    public int SpecialZ_BaseMove { get => BitConverter.ToUInt16(Data, 0x4E); set => BitConverter.GetBytes((ushort)value).CopyTo(Data, 0x4E); }
    public int SpecialZ_ZMove { get => BitConverter.ToUInt16(Data, 0x50); set => BitConverter.GetBytes((ushort)value).CopyTo(Data, 0x50); }
    public bool LocalVariant { get => (Data[0x52] & 1) != 0; set => Data[0x52] = (byte)(value ? 1 : 0); }
}