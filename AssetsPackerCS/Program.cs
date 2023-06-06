using System;
using System.Collections.Generic;
using System.IO;

public class AssetItem {

    public static String FormatFileSize(int bytes) {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = (double)bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) {
            order++;
            len = len / 1024;
        }
        return String.Format("{0:0.##} {1}", len, sizes[order]);
    }

    public uint originalSize = 0;
    public uint packedSize = 0;
    public string Path { get; set; }
    public string OriginalSize { get => FormatFileSize((int)originalSize); }
    public string PackedSize { get => FormatFileSize((int)packedSize); }
    public string Ratio {
        get {
            uint rx = (100 * packedSize) / originalSize;
            return rx + "%";
        }
    }
}

public class AssetTransformation {
    public Stream InputBitmap;
    public Stream InputMapping;
    public Stream Output;
    public readonly string Name;

    public AssetTransformation(string name) {
        this.Name = name;
    }
}

public class Program {

    public static int Main(string[] args) {
        if (args.Length != 2) {
            Console.Error.WriteLine("Expected 2 arguments. Got " + args.Length);
            return 1;
        }
        Debugger.LogFun = Console.WriteLine;
        string fileIn = args[0];
        string fileOut = args[1];

        List<AssetTransformation> inputFiles = new List<AssetTransformation>();
        List<AssetItem> items = new List<AssetItem>();

        string[] eras = new string[] {
            "badlands",
            "platform",
            "install",
            "ashworld",
            "jungle",
            "desert",
            "ice",
            "twilight"
        };

        bool error = true;
        try {
            foreach (string era in eras) {
                string fBitmap = fileIn + "/" + era + ".png";
                string fMapping = fileIn + "/" + era + ".map";
                string fOutput = fileOut + "/" + era + ".bin";
                if (!File.Exists(fBitmap)) {
                    Console.Error.WriteLine("File " + fBitmap + "\ndoes not exist.");
                    return 1;
                } else if (!File.Exists(fMapping)) {
                    Console.Error.WriteLine("File " + fMapping + "\ndoes not exist.");
                    return 1;
                } else {
                    AssetTransformation at = new AssetTransformation(era);
                    inputFiles.Add(at);
                    at.InputBitmap = File.OpenRead(fBitmap);
                    at.InputMapping = File.OpenRead(fMapping);
                    at.Output = File.OpenWrite(fOutput);
                }
            }
            error = false;
        } catch (Exception e) {
            Debugger.Log(e);
            return 1;
        } finally {
            if (error) {
                foreach (AssetTransformation at in inputFiles) {
                    if (at.InputBitmap != null) {
                        at.InputBitmap.Close();
                    }
                    if (at.InputMapping != null) {
                        at.InputMapping.Close();
                    }
                    if (at.Output != null) {
                        at.Output.Close();
                    }
                }
            }
        }
        if (!error) {
            bool ok = true;
            try {
                foreach (AssetTransformation fs in inputFiles) {
                    if (ok) {
                        uint resultSize = 0;
                        Debugger.LogFun("Processing " + fs.Name);
                        ok &= ImageEncoder.AsyncWriteProcessFile(fs.InputBitmap, fs.InputMapping, fs.Output, out resultSize);
                        if (ok) {
                            AssetItem ai = new AssetItem();
                            ai.Path = fs.Name;
                            ai.originalSize = (uint)(fs.InputBitmap.Length + fs.InputMapping.Length);
                            ai.packedSize = (uint)fs.Output.Length;
                            Debugger.LogFun("Result for " + ai.Path + ": original size: " + ai.OriginalSize + ", packed size: " + ai.PackedSize + ", ratio: " + ai.Ratio);
                        } else {
                            break;
                        }
                    }
                }
                return ok ? 0 : 1;
            } finally {
                foreach (AssetTransformation at in inputFiles) {
                    if (at.InputBitmap != null) {
                        at.InputBitmap.Close();
                    }
                    if (at.InputMapping != null) {
                        at.InputMapping.Close();
                    }
                    if (at.Output != null) {
                        at.Output.Close();
                    }
                }
            }
        }
        return 0;
    }
    
}
