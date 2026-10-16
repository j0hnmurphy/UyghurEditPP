using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class AppSettingsTests
	{
		string gFolder;
		string gJson;
		string gCfg;
		Action<Exception> gOriginalLog;
		List<Exception> gLogged;

		[TestInitialize]
		public void SetUp()
		{
			gFolder = Path.Combine(Path.GetTempPath(), "UyghurEditPP.Tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(gFolder);
			gJson = Path.Combine(gFolder, "uyghuredit.json");
			gCfg = Path.Combine(gFolder, "uyghuredit.cfg");
			gOriginalLog = AppSettings.Log;
			gLogged = new List<Exception>();
			AppSettings.Log = ee => gLogged.Add(ee);
		}

		[TestCleanup]
		public void TearDown()
		{
			AppSettings.Log = gOriginalLog;
			Directory.Delete(gFolder, true);
		}

		// A table like the one MainForm builds, with every key it uses.
		static Hashtable FullConfig()
		{
			Hashtable h = new Hashtable();
			h["LANG"] = "uey";
			h["KUNUPKA"] = KUNUPKA.UyghurLY;
			h["YEZIQAUTO"] = false;
			h["IMLAYEZIQ"] = "ULY";
			h["FONTNAME"] = "UKIJ Tuz";
			h["FONTSIZE"] = 21.5f;
			h["FONTSTYLE"] = 1;
			h["FONTWEIGHT"] = 0;
			h["CHONGLUQI"] = new Rectangle(10, 20, 1024, 768);
			h["IZLAR"] = new[] { @"C:\Hojjetler\a.txt", @"D:\ئۇيغۇرچە\b.txt" };
			h["ORUNLAR"] = new Dictionary<string, int> { { @"C:\Hojjetler\a.txt", 42 }, { @"D:\ئۇيغۇرچە\b.txt", 7 } };
			h["ACHIQ"] = new[] { @"D:\ئۇيغۇرچە\b.txt", @"C:\Hojjetler\a.txt" };
			h["ACHIQ_TALLANGHAN"] = @"C:\Hojjetler\a.txt";
			h["AVTOSAQLASH"] = 5;
			h["TAHMIN"] = false;   // the default is on, so off is the value worth keeping
			return h;
		}

		static void AssertSame(Hashtable expected, Hashtable actual)
		{
			Assert.AreEqual(expected.Count, actual.Count);
			foreach(DictionaryEntry e in expected){
				object a = actual[e.Key];
				if(e.Value is string[]){
					CollectionAssert.AreEqual((string[])e.Value, (string[])a, e.Key.ToString());
				}
				else if(e.Value is Dictionary<string, int>){
					CollectionAssert.AreEquivalent((Dictionary<string, int>)e.Value, (Dictionary<string, int>)a, e.Key.ToString());
				}
				else{
					Assert.AreEqual(e.Value, a, e.Key.ToString());
					Assert.AreEqual(e.Value.GetType(), a.GetType(), e.Key.ToString());
				}
			}
		}

		void WriteLegacy(Hashtable h)
		{
			using(FileStream fs = File.Create(gCfg)){
				new BinaryFormatter().Serialize(fs, h);
			}
		}

		[TestMethod]
		public void SaveThenLoad_KeepsEveryValueAndType()
		{
			AppSettings.Save(gJson, FullConfig());

			AssertSame(FullConfig(), AppSettings.Load(gJson, gCfg));
			Assert.AreEqual(0, gLogged.Count);
		}

		[TestMethod]
		public void Load_MissingKeys_StayMissing()
		{
			Hashtable h = new Hashtable();
			h["LANG"] = "uly";
			AppSettings.Save(gJson, h);

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			Assert.AreEqual(1, loaded.Count);
			Assert.AreEqual("uly", loaded["LANG"]);
		}

		[TestMethod]
		public void Load_OnlyLegacyFile_ReadsItAndLeavesItInPlace()
		{
			WriteLegacy(FullConfig());
			byte[] before = File.ReadAllBytes(gCfg);

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			AssertSame(FullConfig(), loaded);
			CollectionAssert.AreEqual(before, File.ReadAllBytes(gCfg));
			Assert.AreEqual(0, gLogged.Count);
		}

		[TestMethod]
		public void Load_JsonExists_WinsOverLegacyFile()
		{
			Hashtable old = FullConfig();
			old["LANG"] = "old";
			WriteLegacy(old);
			AppSettings.Save(gJson, FullConfig());

			Assert.AreEqual("uey", AppSettings.Load(gJson, gCfg)["LANG"]);
		}

		[TestMethod]
		public void Load_BrokenJson_UsesDefaultsAndKeepsTheBrokenFile()
		{
			WriteLegacy(FullConfig());
			File.WriteAllText(gJson, "{ not json");

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			// The old file is not brought back once a JSON file has existed.
			Assert.AreEqual(0, loaded.Count);
			Assert.AreEqual(1, gLogged.Count);
			Assert.IsFalse(File.Exists(gJson));
			string[] bad = Directory.GetFiles(gFolder, "uyghuredit.json.bad-*");
			Assert.AreEqual(1, bad.Length);
			Assert.AreEqual("{ not json", File.ReadAllText(bad[0]));
			Assert.IsTrue(File.Exists(gCfg));
		}

		[TestMethod]
		public void Load_EmptyRecentFiles_AreDropped()
		{
			File.WriteAllText(gJson, "{\"recentFiles\":[\"a.txt\",null,\"\",\"b.txt\"],\"caretOffsets\":[{\"file\":null,\"offset\":3},{\"file\":\"a.txt\",\"offset\":5}]}");

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			CollectionAssert.AreEqual(new[] { "a.txt", "b.txt" }, (string[])loaded["IZLAR"]);
			Dictionary<string, int> orunlar = (Dictionary<string, int>)loaded["ORUNLAR"];
			Assert.AreEqual(1, orunlar.Count);
			Assert.AreEqual(5, orunlar["a.txt"]);
		}

		[TestMethod]
		public void LegacyBinder_BindsAllowedNamesToExactTypes()
		{
			var binder = new AppSettings.LegacyBinder();

			Assert.AreSame(typeof(Dictionary<string, int>), binder.BindToType("mscorlib", typeof(Dictionary<string, int>).FullName));
			Assert.AreSame(typeof(Rectangle), binder.BindToType("System.Drawing, Version=9.9.9.9", "System.Drawing.Rectangle"));
			Assert.AreSame(typeof(KUNUPKA), binder.BindToType("SomethingElse", "UyghurEditPP.KUNUPKA"));
			Assert.ThrowsException<System.Runtime.Serialization.SerializationException>(() =>
				binder.BindToType("mscorlib", typeof(Dictionary<string, Version>).FullName));
			Assert.ThrowsException<System.Runtime.Serialization.SerializationException>(() =>
				binder.BindToType("mscorlib", "System.Version"));
		}

		[TestMethod]
		public void Load_LegacyFileWithUnexpectedType_IsRejected()
		{
			Hashtable h = new Hashtable();
			h["LANG"] = "uey";
			h["X"] = new Version(1, 2);
			WriteLegacy(h);

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			Assert.AreEqual(0, loaded.Count);
			Assert.AreEqual(1, gLogged.Count);
		}

		[TestMethod]
		public void Load_LegacyDictionaryOfOtherType_IsRejected()
		{
			Hashtable h = new Hashtable();
			h["ORUNLAR"] = new Dictionary<string, Version> { { "a", new Version(1, 0) } };
			WriteLegacy(h);

			Assert.AreEqual(0, AppSettings.Load(gJson, gCfg).Count);
			Assert.AreEqual(1, gLogged.Count);
		}

		[TestMethod]
		public void Load_NothingThere_ReturnsEmptyTable()
		{
			Assert.AreEqual(0, AppSettings.Load(gJson, gCfg).Count);
			Assert.AreEqual(0, gLogged.Count);
		}

		// The settings file as version 1.0.1 reads and writes it: AppSettings.Data, Bounds and
		// FileOffset copied from commit 43f4c7e (no openFiles / activeFile / autosaveMinutes).
		[DataContract]
		class Data101
		{
			[DataMember(Name = "language", EmitDefaultValue = false, Order = 1)] public string Language;
			[DataMember(Name = "keyboard", EmitDefaultValue = false, Order = 2)] public string Keyboard;
			[DataMember(Name = "autoDetectScript", EmitDefaultValue = false, Order = 3)] public bool? AutoDetectScript;
			[DataMember(Name = "spellingScript", EmitDefaultValue = false, Order = 4)] public string SpellingScript;
			[DataMember(Name = "fontName", EmitDefaultValue = false, Order = 5)] public string FontName;
			[DataMember(Name = "fontSize", EmitDefaultValue = false, Order = 6)] public float? FontSize;
			[DataMember(Name = "fontStyle", EmitDefaultValue = false, Order = 7)] public int? FontStyle;
			[DataMember(Name = "fontWeight", EmitDefaultValue = false, Order = 8)] public int? FontWeight;
			[DataMember(Name = "window", EmitDefaultValue = false, Order = 9)] public Bounds101 Window;
			[DataMember(Name = "recentFiles", EmitDefaultValue = false, Order = 10)] public string[] RecentFiles;
			[DataMember(Name = "caretOffsets", EmitDefaultValue = false, Order = 11)] public List<FileOffset101> CaretOffsets;
			[DataMember(Name = "theme", EmitDefaultValue = false, Order = 12)] public string Theme;
		}

		[DataContract]
		class Bounds101
		{
			[DataMember(Name = "x", Order = 1)] public int X;
			[DataMember(Name = "y", Order = 2)] public int Y;
			[DataMember(Name = "width", Order = 3)] public int Width;
			[DataMember(Name = "height", Order = 4)] public int Height;
		}

		[DataContract]
		class FileOffset101
		{
			[DataMember(Name = "file", Order = 1)] public string File;
			[DataMember(Name = "offset", Order = 2)] public int Offset;
		}

		[TestMethod]
		public void Load_FileFromOlderVersion_HasNoOpenFiles()
		{
			using(FileStream fs = File.Create(gJson)){
				new DataContractJsonSerializer(typeof(Data101)).WriteObject(fs, new Data101 { Language = "uey", Theme = "dark" });
			}

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			Assert.AreEqual("uey", loaded["LANG"]);
			Assert.IsFalse(loaded.ContainsKey("ACHIQ"));
			Assert.IsFalse(loaded.ContainsKey("ACHIQ_TALLANGHAN"));
			Assert.AreEqual(0, gLogged.Count);
		}

		[TestMethod]
		public void OlderVersion_ReadsFileWithNewMembers_AndIgnoresThem()
		{
			AppSettings.Save(gJson, FullConfig());
			StringAssert.Contains(File.ReadAllText(gJson), "\"openFiles\"");

			Data101 old;
			using(FileStream fs = File.OpenRead(gJson)){
				old = (Data101)new DataContractJsonSerializer(typeof(Data101)).ReadObject(fs);
			}

			Assert.AreEqual("uey", old.Language);
			Assert.AreEqual("UyghurLY", old.Keyboard);
			Assert.AreEqual(false, old.AutoDetectScript);
			Assert.AreEqual("ULY", old.SpellingScript);
			Assert.AreEqual("UKIJ Tuz", old.FontName);
			Assert.AreEqual(21.5f, old.FontSize);
			Assert.AreEqual(1, old.FontStyle);
			Assert.AreEqual(0, old.FontWeight);
			Assert.AreEqual(1024, old.Window.Width);
			Assert.AreEqual(2, old.RecentFiles.Length);
			Assert.AreEqual(2, old.CaretOffsets.Count);
			Assert.AreEqual(42, old.CaretOffsets[0].Offset);
		}

		[TestMethod]
		public void Load_EmptyOpenFiles_StaysEmpty_AndEmptyNamesAreDropped()
		{
			File.WriteAllText(gJson, "{\"openFiles\":[],\"activeFile\":\"\"}");
			Hashtable loaded = AppSettings.Load(gJson, gCfg);
			Assert.AreEqual(0, ((string[])loaded["ACHIQ"]).Length);
			Assert.IsFalse(loaded.ContainsKey("ACHIQ_TALLANGHAN"));

			File.WriteAllText(gJson, "{\"openFiles\":[\"a.txt\",null,\"\"],\"activeFile\":\"a.txt\"}");
			loaded = AppSettings.Load(gJson, gCfg);
			CollectionAssert.AreEqual(new[] { "a.txt" }, (string[])loaded["ACHIQ"]);
			Assert.AreEqual("a.txt", loaded["ACHIQ_TALLANGHAN"]);
		}

		// A second window closes after the first one saved its open files: it must save the
		// first window's list, not the stale one it read at its start.
		[TestMethod]
		public void SecondWindowClosing_KeepsTheOpenFilesTheFirstWindowSaved()
		{
			Hashtable first = FullConfig();
			first["ACHIQ"] = new[] { "new1.txt", "new2.txt" };
			first["ACHIQ_TALLANGHAN"] = "new2.txt";
			AppSettings.Save(gJson, first);

			Hashtable second = FullConfig();
			second["ACHIQ"] = new[] { "stale.txt" };
			second["ACHIQ_TALLANGHAN"] = "stale.txt";
			second["FONTSIZE"] = 30f;
			AppSettings.TakeListsFromDisk(gJson, second, new string[0], 15);
			AppSettings.Save(gJson, second);

			Hashtable saved = AppSettings.Load(gJson, gCfg);
			CollectionAssert.AreEqual(new[] { "new1.txt", "new2.txt" }, (string[])saved["ACHIQ"]);
			Assert.AreEqual("new2.txt", saved["ACHIQ_TALLANGHAN"]);
			Assert.AreEqual(30f, saved["FONTSIZE"]);   // its other settings are still saved
		}

		// Re-review: a second window closing wrote its recent files (read at its start) over
		// those the first window saved, and erased the first window's caret positions.
		[TestMethod]
		public void SecondWindowClosing_MergesTheRecentFilesAndCaretPositions()
		{
			Hashtable first = FullConfig();
			first["IZLAR"] = new[] { @"C:\first-new.txt", @"C:\shared.txt", @"C:\old.txt" };
			first["ORUNLAR"] = new Dictionary<string, int>{ { @"C:\first-new.txt", 10 }, { @"C:\shared.txt", 20 }, { @"C:\old.txt", 30 } };
			AppSettings.Save(gJson, first);

			// Read at its start: only old.txt and shared.txt; it then used second.txt and shared.txt.
			Hashtable second = FullConfig();
			second["IZLAR"] = new[] { @"C:\second.txt", @"C:\shared.txt", @"C:\old.txt" };
			second["ORUNLAR"] = new Dictionary<string, int>{ { @"C:\second.txt", 1 }, { @"C:\shared.txt", 2 }, { @"C:\old.txt", 99 } };
			Assert.IsTrue(AppSettings.TakeListsFromDisk(gJson, second, new[] { @"C:\second.txt", @"c:\SHARED.txt" }, 15));

			CollectionAssert.AreEqual(new[] { @"C:\second.txt", @"C:\shared.txt", @"C:\first-new.txt", @"C:\old.txt" }, (string[])second["IZLAR"]);
			Dictionary<string, int> offsets = (Dictionary<string, int>)second["ORUNLAR"];
			Assert.AreEqual(1, offsets[@"C:\second.txt"]);
			Assert.AreEqual(2, offsets[@"C:\shared.txt"]);     // this window's, it used the file
			Assert.AreEqual(10, offsets[@"C:\first-new.txt"]); // the first window's, kept
			Assert.AreEqual(30, offsets[@"C:\old.txt"]);       // not used here: the stale 99 is not saved
		}

		// Review of #24 (L2): caret positions of files no longer in the recent list were kept forever.
		[TestMethod]
		public void MergedCaretPositions_OnlyOfTheRecentFiles()
		{
			Hashtable first = FullConfig();
			first["IZLAR"] = new[] { "a" };
			first["ORUNLAR"] = new Dictionary<string, int>{ { "a", 1 }, { "gone", 2 } };
			AppSettings.Save(gJson, first);
			Hashtable second = FullConfig();
			second["IZLAR"] = new[] { "x" };
			second["ORUNLAR"] = new Dictionary<string, int>{ { "x", 3 }, { "old", 4 } };
			Assert.IsTrue(AppSettings.TakeListsFromDisk(gJson, second, new[] { "x", "old" }, 15));
			CollectionAssert.AreEquivalent(new[] { "x", "a" }, new List<string>(((Dictionary<string, int>)second["ORUNLAR"]).Keys));
		}

		// Review of #24 (L4): two windows closing at once could each read, merge and write the
		// settings in turn, the later write losing the other window's lists.
		[TestMethod, Timeout(30000)]
		public void SaveLock_SecondWindowWaits_UntilTheFirstHasWritten()
		{
			string name = @"Local\UyghurEditPP.Tests." + Guid.NewGuid().ToString("N");
			List<string> order = new List<string>();
			using(System.Threading.ManualResetEventSlim inside = new System.Threading.ManualResetEventSlim(),
			      release = new System.Threading.ManualResetEventSlim()){
				System.Threading.Thread first = new System.Threading.Thread(() => AppSettings.WithSaveLock(() => {
					inside.Set();
					release.Wait();
					lock(order) order.Add("first");
				}, name, 10000));
				first.Start();
				inside.Wait();
				System.Threading.Thread second = new System.Threading.Thread(() => AppSettings.WithSaveLock(() => {
					lock(order) order.Add("second");
				}, name, 10000));
				second.Start();
				Assert.IsFalse(second.Join(300), "the second window must wait");
				release.Set();
				first.Join();
				second.Join();
			}
			CollectionAssert.AreEqual(new[] { "first", "second" }, order);
		}

		// A window that does not let go (hung) must not stop the other from closing.
		[TestMethod, Timeout(30000)]
		public void SaveLock_NotFreed_GoesOnAfterTheTimeout()
		{
			string name = @"Local\UyghurEditPP.Tests." + Guid.NewGuid().ToString("N");
			using(System.Threading.ManualResetEventSlim inside = new System.Threading.ManualResetEventSlim(),
			      release = new System.Threading.ManualResetEventSlim()){
				System.Threading.Thread hung = new System.Threading.Thread(() => AppSettings.WithSaveLock(() => {
					inside.Set();
					release.Wait();
				}, name, 10000));
				hung.Start();
				inside.Wait();
				bool ran = false;
				AppSettings.WithSaveLock(() => ran = true, name, 100);
				Assert.IsTrue(ran);
				release.Set();
				hung.Join();
			}
		}

		[TestMethod]
		public void MergedRecentFiles_AreCut_AtTheLimit()
		{
			Hashtable first = FullConfig();
			first["IZLAR"] = new[] { "a", "b", "c" };
			AppSettings.Save(gJson, first);
			Hashtable second = FullConfig();
			second["IZLAR"] = new[] { "x", "a" };
			Assert.IsTrue(AppSettings.TakeListsFromDisk(gJson, second, new[] { "x" }, 3));
			CollectionAssert.AreEqual(new[] { "x", "a", "b" }, (string[])second["IZLAR"]);
		}

		[TestMethod]
		public void TakeListsFromDisk_NoListOnDisk_RemovesTheStaleOne()
		{
			Hashtable first = FullConfig();
			first.Remove("ACHIQ");
			first.Remove("ACHIQ_TALLANGHAN");
			AppSettings.Save(gJson, first);
			Hashtable second = FullConfig();
			second["ACHIQ"] = new[] { "stale.txt" };
			second["ACHIQ_TALLANGHAN"] = "stale.txt";
			AppSettings.TakeListsFromDisk(gJson, second, new string[0], 15);
			Assert.IsFalse(second.ContainsKey("ACHIQ_TALLANGHAN"));
			Assert.IsTrue(second["ACHIQ"] == null || ((string[])second["ACHIQ"]).Length == 0);
		}

		[TestMethod]
		public void TakeListsFromDisk_UnreadableFile_KeepsTheTableAndTheFile()
		{
			File.WriteAllText(gJson, "{ not json");
			Hashtable second = FullConfig();
			second["ACHIQ"] = new[] { "a.txt" };
			// False: the window must not save its settings, so the list on disk stays.
			Assert.IsFalse(AppSettings.TakeListsFromDisk(gJson, second, new string[0], 15));
			CollectionAssert.AreEqual(new[] { "a.txt" }, (string[])second["ACHIQ"]);
			Assert.IsTrue(File.Exists(gJson));   // not renamed to .bad-*: that is Load's job
			Assert.AreEqual(1, gLogged.Count);
		}

		// Re-review: while the first window replaces the file, it cannot be read for a moment;
		// the stale list was then kept and saved over the first window's.
		[TestMethod]
		public void TakeListsFromDisk_FileBriefly_Locked_TriesAgain()
		{
			Hashtable first = FullConfig();
			first["ACHIQ"] = new[] { "new.txt" };
			AppSettings.Save(gJson, first);
			Hashtable second = FullConfig();
			second["ACHIQ"] = new[] { "stale.txt" };
			FileStream locked = new FileStream(gJson, FileMode.Open, FileAccess.Read, FileShare.None);
			System.Threading.Timer release = new System.Threading.Timer(s => locked.Dispose(), null, 300, System.Threading.Timeout.Infinite);
			try{
				Assert.IsTrue(AppSettings.TakeListsFromDisk(gJson, second, new string[0], 15));
			}
			finally{
				release.Dispose();
				locked.Dispose();
			}
			CollectionAssert.AreEqual(new[] { "new.txt" }, (string[])second["ACHIQ"]);
		}

		[TestMethod]
		public void TakeListsFromDisk_FileStaysLocked_SaysSo_AndKeepsTheTable()
		{
			AppSettings.Save(gJson, FullConfig());
			Hashtable second = FullConfig();
			second["ACHIQ"] = new[] { "stale.txt" };
			using(new FileStream(gJson, FileMode.Open, FileAccess.Read, FileShare.None)){
				Assert.IsFalse(AppSettings.TakeListsFromDisk(gJson, second, new string[0], 15));
			}
			CollectionAssert.AreEqual(new[] { "stale.txt" }, (string[])second["ACHIQ"]);
		}

		[TestMethod]
		public void Load_AutosaveMinutes_WholeNumbersWork()
		{
			File.WriteAllText(gJson, "{\"autosaveMinutes\": 0}");
			Assert.AreEqual(0, AppSettings.Load(gJson, gCfg)["AVTOSAQLASH"]);

			File.WriteAllText(gJson, "{\"autosaveMinutes\": 10, \"language\": \"uey\"}");
			Hashtable loaded = AppSettings.Load(gJson, gCfg);
			Assert.AreEqual(10, loaded["AVTOSAQLASH"]);
			Assert.AreEqual("uey", loaded["LANG"]);
		}

		[TestMethod]
		public void Load_AutosaveMinutes_NotAWholeNumber_KeepsTheFileAsBad()
		{
			// Written by hand with the wrong type: like any unreadable settings file it is set
			// aside (not overwritten) and the defaults are used.
			File.WriteAllText(gJson, "{\"language\": \"uey\", \"autosaveMinutes\": \"abc\"}");

			Hashtable loaded = AppSettings.Load(gJson, gCfg);

			Assert.AreEqual(0, loaded.Count);
			Assert.AreEqual(1, Directory.GetFiles(gFolder, "uyghuredit.json.bad-*").Length);
		}

		[TestMethod]
		public void Load_AutosaveMinutes_QuotedNumberWorks_FractionIsSetAside()
		{
			File.WriteAllText(gJson, "{\"autosaveMinutes\": \"5\"}");
			Assert.AreEqual(5, AppSettings.Load(gJson, gCfg)["AVTOSAQLASH"]);

			File.WriteAllText(gJson, "{\"autosaveMinutes\": 2.5}");
			Hashtable loaded = AppSettings.Load(gJson, gCfg);
			Assert.AreEqual(0, loaded.Count);
			Assert.AreEqual(1, Directory.GetFiles(gFolder, "uyghuredit.json.bad-*").Length);
		}

		[TestMethod]
		public void Save_WritesReadableJson()
		{
			AppSettings.Save(gJson, FullConfig());

			string json = File.ReadAllText(gJson);
			StringAssert.Contains(json, "\"keyboard\": \"UyghurLY\"");
			StringAssert.Contains(json, "\"fontName\": \"UKIJ Tuz\"");
		}
	}
}
