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

		// The settings file as version 1.0.1 reads and writes it (no openFiles / activeFile).
		[DataContract]
		class Data101
		{
			[DataMember(Name = "language", EmitDefaultValue = false, Order = 1)] public string Language;
			[DataMember(Name = "recentFiles", EmitDefaultValue = false, Order = 10)] public string[] RecentFiles;
			[DataMember(Name = "theme", EmitDefaultValue = false, Order = 12)] public string Theme;
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
			Assert.AreEqual(2, old.RecentFiles.Length);
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
