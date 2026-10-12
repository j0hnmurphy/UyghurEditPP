/*
 * Reads and writes the settings (%AppData%\UyghurEditPP\uyghuredit.json).
 *
 * Older versions kept the settings in uyghuredit.cfg, written with BinaryFormatter,
 * which Microsoft recommends no longer using. That file is only read, once, when no
 * uyghuredit.json exists yet, and is left in place as a backup.
 *
 * MainForm keeps working with the same Hashtable keys as before; this class converts
 * between that Hashtable and the JSON file.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;
using System.Text;

namespace UyghurEditPP
{
	public static class AppSettings
	{
		public const string FileName = "uyghuredit.json";

		// Seam for the tests.
		internal static Action<Exception> Log = ee => ErrorLog.Write(ee);

		[DataContract]
		internal class Data
		{
			[DataMember(Name = "language", EmitDefaultValue = false, Order = 1)] public string Language;
			[DataMember(Name = "keyboard", EmitDefaultValue = false, Order = 2)] public string Keyboard;
			[DataMember(Name = "autoDetectScript", EmitDefaultValue = false, Order = 3)] public bool? AutoDetectScript;
			[DataMember(Name = "spellingScript", EmitDefaultValue = false, Order = 4)] public string SpellingScript;
			[DataMember(Name = "fontName", EmitDefaultValue = false, Order = 5)] public string FontName;
			[DataMember(Name = "fontSize", EmitDefaultValue = false, Order = 6)] public float? FontSize;
			[DataMember(Name = "fontStyle", EmitDefaultValue = false, Order = 7)] public int? FontStyle;
			[DataMember(Name = "fontWeight", EmitDefaultValue = false, Order = 8)] public int? FontWeight;
			[DataMember(Name = "window", EmitDefaultValue = false, Order = 9)] public Bounds Window;
			[DataMember(Name = "recentFiles", EmitDefaultValue = false, Order = 10)] public string[] RecentFiles;
			[DataMember(Name = "caretOffsets", EmitDefaultValue = false, Order = 11)] public List<FileOffset> CaretOffsets;
			// "light", "dark" or "system" (follow the Windows app mode); none = light.
			[DataMember(Name = "theme", EmitDefaultValue = false, Order = 12)] public string Theme;
			// The files open when the program was last closed, in tab order, and the selected one.
			// Older versions ignore these members when they read the file (and drop them on saving).
			[DataMember(Name = "openFiles", EmitDefaultValue = false, Order = 13)] public string[] OpenFiles;
			[DataMember(Name = "activeFile", EmitDefaultValue = false, Order = 14)] public string ActiveFile;
			// Minutes between the copies of unsaved documents; 0 turns them off. Only set in the file by hand.
			[DataMember(Name = "autosaveMinutes", EmitDefaultValue = false, Order = 15)] public int? AutosaveMinutes;
		}

		[DataContract]
		internal class Bounds
		{
			[DataMember(Name = "x", Order = 1)] public int X;
			[DataMember(Name = "y", Order = 2)] public int Y;
			[DataMember(Name = "width", Order = 3)] public int Width;
			[DataMember(Name = "height", Order = 4)] public int Height;
		}

		[DataContract]
		internal class FileOffset
		{
			[DataMember(Name = "file", Order = 1)] public string File;
			[DataMember(Name = "offset", Order = 2)] public int Offset;
		}

		/// <summary>
		/// Reads the settings from jsonFile; only if it does not exist, from the old legacyFile.
		/// A jsonFile that cannot be read is renamed to jsonFile.bad-yyyyMMddHHmmss (so it is
		/// not overwritten on exit) and the defaults are used. Returns an empty table for the
		/// defaults. Never throws.
		/// </summary>
		public static Hashtable Load(string jsonFile, string legacyFile)
		{
			if(File.Exists(jsonFile)){
				try{
					using(FileStream fs = File.OpenRead(jsonFile)){
						Data data = (Data)new DataContractJsonSerializer(typeof(Data)).ReadObject(fs);
						return ToHashtable(data ?? new Data());
					}
				}
				catch(Exception ee){
					Log(ee);
					try{
						File.Move(jsonFile, jsonFile + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
					}
					catch(Exception moveError){
						Log(moveError);
					}
					return new Hashtable();
				}
			}
			if(legacyFile != null && File.Exists(legacyFile)){
				try{
					return ToHashtable(FromHashtable(ReadLegacy(legacyFile)));
				}
				catch(Exception ee){
					Log(ee);
				}
			}
			return new Hashtable();
		}

		/// <summary>
		/// Puts the open files list saved in jsonFile now (by the first window) into config, in
		/// place of the one read at the start: a later window must not save its stale copy over
		/// it. Returns false, leaving config as it is, when the file cannot be read (also after
		/// trying again for a moment while another window is writing it): the settings must
		/// then not be saved, so that the list on disk stays. Never throws.
		/// </summary>
		public static bool TakeOpenFilesFromDisk(string jsonFile, Hashtable config)
		{
			Hashtable onDisk = ReadForMerge(jsonFile);
			if(onDisk == null){
				return false;
			}
			TakeOpenFiles(onDisk, config);
			return true;
		}

		static void TakeOpenFiles(Hashtable onDisk, Hashtable config)
		{
			foreach(string key in new[] { "ACHIQ", "ACHIQ_TALLANGHAN" }){
				if(onDisk.ContainsKey(key)){
					config[key] = onDisk[key];
				}
				else{
					config.Remove(key);
				}
			}
		}

		/// <summary>
		/// For a window that is not the first one, before it saves its settings: as
		/// TakeOpenFilesFromDisk, and the recent files and caret positions are merged with
		/// those saved in jsonFile now. The files this window used (usedFiles) come first, in
		/// its order, with its caret positions; the others, and their positions, are taken from
		/// the file. At most maxRecent recent files. Returns false as TakeOpenFilesFromDisk does.
		/// (The other settings stay this window's: the last window closed decides them.)
		/// </summary>
		public static bool TakeListsFromDisk(string jsonFile, Hashtable config, ICollection<string> usedFiles, int maxRecent)
		{
			Hashtable onDisk = ReadForMerge(jsonFile);
			if(onDisk == null){
				return false;
			}
			TakeOpenFiles(onDisk, config);
			HashSet<string> used = new HashSet<string>(usedFiles, StringComparer.OrdinalIgnoreCase);
			List<string> recent = new List<string>();
			foreach(string f in config["IZLAR"] as string[] ?? new string[0]){
				if(used.Contains(f)){
					recent.Add(f);
				}
			}
			foreach(string f in onDisk["IZLAR"] as string[] ?? new string[0]){
				if(!used.Contains(f)){
					recent.Add(f);
				}
			}
			config["IZLAR"] = recent.GetRange(0, Math.Min(maxRecent, recent.Count)).ToArray();

			Dictionary<string, int> mine = config["ORUNLAR"] as Dictionary<string, int> ?? new Dictionary<string, int>();
			Dictionary<string, int> offsets = new Dictionary<string, int>(onDisk["ORUNLAR"] as Dictionary<string, int> ?? new Dictionary<string, int>());
			foreach(KeyValuePair<string, int> kv in mine){
				if(used.Contains(kv.Key)){
					offsets[kv.Key] = kv.Value;
				}
			}
			config["ORUNLAR"] = offsets;
			return true;
		}

		// How long a file being replaced by another window is waited for.
		const int ReadTries = 10;
		const int ReadWaitMs = 100;

		// The settings in jsonFile now; empty when there is no file yet; null when it cannot be read.
		static Hashtable ReadForMerge(string jsonFile)
		{
			for(int i = 1; ; i++){
				try{
					using(FileStream fs = File.OpenRead(jsonFile)){
						return ToHashtable((Data)new DataContractJsonSerializer(typeof(Data)).ReadObject(fs) ?? new Data());
					}
				}
				catch(Exception ee){
					if(ee is FileNotFoundException || ee is DirectoryNotFoundException){
						return new Hashtable();   // nothing saved yet
					}
					// In use (being replaced): try again for a moment. Anything else fails at once.
					if((ee is IOException || ee is UnauthorizedAccessException) && i < ReadTries){
						System.Threading.Thread.Sleep(ReadWaitMs);
						continue;
					}
					Log(ee);
					return null;
				}
			}
		}

		/// <summary>
		/// Writes the settings to jsonFile (through a temporary file).
		/// </summary>
		public static void Save(string jsonFile, Hashtable config)
		{
			Data data = FromHashtable(config);
			SafeFile.Write(jsonFile, stream => {
				using(var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  ")){
					new DataContractJsonSerializer(typeof(Data)).WriteObject(writer, data);
					writer.Flush();
				}
			});
		}

		internal static Hashtable ToHashtable(Data d)
		{
			Hashtable h = new Hashtable();
			if(d.Language != null) h["LANG"] = d.Language;
			KUNUPKA kun;
			if(d.Keyboard != null && Enum.TryParse(d.Keyboard, out kun)) h["KUNUPKA"] = kun;
			if(d.AutoDetectScript.HasValue) h["YEZIQAUTO"] = d.AutoDetectScript.Value;
			if(d.SpellingScript != null) h["IMLAYEZIQ"] = d.SpellingScript;
			if(d.FontName != null) h["FONTNAME"] = d.FontName;
			if(d.FontSize.HasValue) h["FONTSIZE"] = d.FontSize.Value;
			if(d.FontStyle.HasValue) h["FONTSTYLE"] = d.FontStyle.Value;
			if(d.FontWeight.HasValue) h["FONTWEIGHT"] = d.FontWeight.Value;
			if(d.Window != null) h["CHONGLUQI"] = new Rectangle(d.Window.X, d.Window.Y, d.Window.Width, d.Window.Height);
			if(d.RecentFiles != null) h["IZLAR"] = Array.FindAll(d.RecentFiles, f => !string.IsNullOrEmpty(f));
			if(d.Theme != null) h["TEMA"] = d.Theme;
			if(d.OpenFiles != null) h["ACHIQ"] = Array.FindAll(d.OpenFiles, f => !string.IsNullOrEmpty(f));
			if(!string.IsNullOrEmpty(d.ActiveFile)) h["ACHIQ_TALLANGHAN"] = d.ActiveFile;
			if(d.AutosaveMinutes.HasValue) h["AVTOSAQLASH"] = d.AutosaveMinutes.Value;
			if(d.CaretOffsets != null){
				Dictionary<string,int> orunlar = new Dictionary<string, int>();
				foreach(FileOffset fo in d.CaretOffsets){
					if(fo != null && fo.File != null) orunlar[fo.File] = fo.Offset;
				}
				h["ORUNLAR"] = orunlar;
			}
			return h;
		}

		internal static Data FromHashtable(Hashtable h)
		{
			Data d = new Data();
			d.Language = h["LANG"] as string;
			if(h["KUNUPKA"] is KUNUPKA) d.Keyboard = h["KUNUPKA"].ToString();
			if(h["YEZIQAUTO"] is bool) d.AutoDetectScript = (bool)h["YEZIQAUTO"];
			d.SpellingScript = h["IMLAYEZIQ"] as string;
			d.FontName = h["FONTNAME"] as string;
			if(h["FONTSIZE"] != null) d.FontSize = Convert.ToSingle(h["FONTSIZE"], System.Globalization.CultureInfo.InvariantCulture);
			if(h["FONTSTYLE"] is int) d.FontStyle = (int)h["FONTSTYLE"];
			if(h["FONTWEIGHT"] is int) d.FontWeight = (int)h["FONTWEIGHT"];
			if(h["CHONGLUQI"] is Rectangle){
				Rectangle rc = (Rectangle)h["CHONGLUQI"];
				d.Window = new Bounds{X = rc.X, Y = rc.Y, Width = rc.Width, Height = rc.Height};
			}
			d.RecentFiles = h["IZLAR"] as string[];
			d.Theme = h["TEMA"] as string;
			d.OpenFiles = h["ACHIQ"] as string[];
			d.ActiveFile = h["ACHIQ_TALLANGHAN"] as string;
			if(h["AVTOSAQLASH"] is int) d.AutosaveMinutes = (int)h["AVTOSAQLASH"];
			Dictionary<string,int> orunlar = h["ORUNLAR"] as Dictionary<string,int>;
			if(orunlar != null){
				d.CaretOffsets = new List<FileOffset>();
				foreach(KeyValuePair<string,int> kv in orunlar){
					d.CaretOffsets.Add(new FileOffset{File = kv.Key, Offset = kv.Value});
				}
			}
			return d;
		}

		static Hashtable ReadLegacy(string legacyFile)
		{
			using(FileStream fs = File.OpenRead(legacyFile)){
				BinaryFormatter bf = new BinaryFormatter();
				bf.Binder = new LegacyBinder();
				return (Hashtable)bf.Deserialize(fs);
			}
		}

		// Only the types the old settings file can contain may be created when reading it.
		// Each allowed name is bound to the type of this program's own framework, whatever
		// assembly the file names.
		internal sealed class LegacyBinder : SerializationBinder
		{
			static readonly Dictionary<string, Type> gRuxset = Ruxset(
				typeof(Hashtable),
				typeof(string),
				typeof(string[]),
				typeof(int),
				typeof(float),
				typeof(double),
				typeof(bool),
				typeof(Rectangle),
				typeof(KUNUPKA),
				// Dictionary<string,int> (the caret offsets) and the types it is stored with.
				typeof(Dictionary<string, int>),
				typeof(KeyValuePair<string, int>),
				typeof(KeyValuePair<string, int>[]),
				EqualityComparer<string>.Default.GetType());

			static Dictionary<string, Type> Ruxset(params Type[] types)
			{
				Dictionary<string, Type> ret = new Dictionary<string, Type>();
				foreach(Type t in types){
					ret[t.FullName] = t;
				}
				return ret;
			}

			public override Type BindToType(string assemblyName, string typeName)
			{
				Type type;
				if(gRuxset.TryGetValue(typeName, out type)){
					return type;
				}
				throw new SerializationException("Unexpected type in the old settings file: " + typeName);
			}
		}
	}
}
