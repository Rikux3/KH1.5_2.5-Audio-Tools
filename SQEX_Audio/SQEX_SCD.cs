//Reference: https://github.com/kode54/vgmstream/blob/master/src/meta/sqex_scd.c
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SQEX_Audio
{
    public class SQEX_SCD
    {
        public int StreamSize { get; set; }
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public int Codec { get; set; }
        public int LoopStart { get; set; }
        public int LoopEnd { get; set; }
        public int SubheaderSize { get; set; }
        public int AuxChunkCount { get; set; }
        public bool IsLooped
        {
            get
            {
                if (LoopEnd > 0)
                    return true;
                else return false;
            }
        }

        public byte[] Data { get; set; }
        public string StreamName { get; set; }

        //public int WAVSize { get; set; }
        //WAVSize is the SampleNumber (duh)
        public int SampleNumber { get; set; }
        public int LoopStartSample { get; set; }
        public int LoopEndSample { get; set; }
        public int BitrateKbps { get; set; }
    }

    public class SCDFile
    {
        public List<SQEX_SCD> Entries { get; set; }

        public SCDFile(string path, bool bigEndian = false)
        {
            Entries = new List<SQEX_SCD>();
            try
            {
                using (var reader = (bigEndian) ? new BinaryReaderBigEndian(File.Open(path, FileMode.Open, FileAccess.ReadWrite)) : new BinaryReader(File.Open(path, FileMode.Open, FileAccess.ReadWrite)))
                {
                    //Assuming we have the files from the 1.5+2.5 dump
                    //This file has an extra 16 byte header before the actual content
                    //(PS3 also has this header)
                    reader.BaseStream.Position = 16L;
                    var magic = Encoding.UTF8.GetString(reader.ReadBytes(0x8)).Replace("\0", string.Empty);
                    //var p1 = reader.ReadInt32();
                    //var p2 = reader.ReadInt32();
                    //if (p1 != 0x42444553 && p2 != 0x46435353)
                    if (magic != "SEDBSSCF")
                        throw new ArgumentException("No valid PS3/PS4 SCD!");

                    reader.BaseStream.Position = 0x1e;
                    var table_offsets = reader.ReadInt16();

                    reader.BaseStream.Position = table_offsets + 0x10;
                    var info_entries = reader.ReadInt16();
                    reader.BaseStream.Seek(0x2, SeekOrigin.Current);
                    var header_entries = reader.ReadInt16();
                    reader.BaseStream.Position = table_offsets + 0x1c;
                    var header_offset_sounds = reader.ReadInt32() + 0x10;

                    List<string> info_ent = new List<string>();

                    //Table0 (i guess?) has the offsets of the name entries
                    //There are more name entries than song entries. I don't know why.
                    //We discard the 00 entries
                    for (int i = 0; i < info_entries; i++)
                    {
                        reader.BaseStream.Position = 0x60 + i * 0x4;
                        var name_offset = reader.ReadInt32() + 0x10;
                        reader.BaseStream.Position = name_offset + 0x30;
                        var name = Encoding.UTF8.GetString(reader.ReadBytes(0x10)).Replace("\0", string.Empty);
                        if (!string.IsNullOrEmpty(name))
                            info_ent.Add(name);
                    }

                    //Table2 has the offsets to the sound entries
                    for (int i = 0; i < header_entries; i++)
                    {
                        reader.BaseStream.Position = header_offset_sounds + i * 0x04;
                        var entry_offset = reader.ReadInt32() + 0x10;

                        reader.BaseStream.Position = entry_offset + 0x0c;
                        if (reader.ReadInt32() == -1)
                            continue;

                        reader.BaseStream.Position = entry_offset;

                        SQEX_SCD scdfile = new SQEX_SCD();

                        scdfile.StreamSize = reader.ReadInt32();
                        scdfile.Channels = reader.ReadInt32();
                        scdfile.SampleRate = reader.ReadInt32();
                        scdfile.Codec = reader.ReadInt32();

                        scdfile.LoopStart = reader.ReadInt32();
                        scdfile.LoopEnd = reader.ReadInt32();
                        scdfile.SubheaderSize = reader.ReadInt32();
                        scdfile.AuxChunkCount = reader.ReadInt32();

                        reader.BaseStream.Seek(0x10, SeekOrigin.Current);

                        var post_meta_offset = (entry_offset + 0x20);

                        switch (scdfile.Codec)
                        {
                            case 0x16: //ATRAC9
                                reader.BaseStream.Position = post_meta_offset + 0x10;
                                scdfile.SampleNumber = reader.ReadInt32();

                                reader.BaseStream.Position = post_meta_offset + 0x20;
                                scdfile.LoopStartSample = reader.ReadInt32();
                                scdfile.LoopEndSample = reader.ReadInt32();

                                if (scdfile.SampleNumber == 1 && scdfile.SubheaderSize == 0x50)
                                {
                                    reader.BaseStream.Position = post_meta_offset + 0x30;
                                    scdfile.SampleNumber = reader.ReadInt32();
                                }
                                scdfile.BitrateKbps = (int)((Int64)scdfile.StreamSize * 8 * scdfile.SampleRate / scdfile.SampleNumber) / 1000;
                                break;
                            case 0x7: //MPEG

                                break;
                        }

                        var start_data_offset = post_meta_offset + scdfile.SubheaderSize;

                        reader.BaseStream.Position = start_data_offset;
                        scdfile.Data = reader.ReadBytes(scdfile.StreamSize);

                        if (info_ent.Count == header_entries)
                            scdfile.StreamName = info_ent[i];

                        Entries.Add(scdfile);
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception($"Error with file {path}!");
            }
        }
    }
}
