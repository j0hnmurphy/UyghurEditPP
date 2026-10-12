using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	// The recovery copy of a document is deleted when IsModified turns false (MainForm.TextOzgerdi),
	// so IsModified must stay true until the file is really written.
	[TestClass]
	public class TextEditorSaveTests
	{
		string gFolder;
		Action<string, string, string> gOriginalReplace;
		Action<Exception> gOriginalLog;

		[TestInitialize]
		public void SetUp()
		{
			gFolder = Path.Combine(Path.GetTempPath(), "UyghurEditPP.Tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(gFolder);
			gOriginalReplace = SafeFile.ReplaceFile;
			gOriginalLog = SafeFile.Log;
			SafeFile.Log = ee => {};
		}

		[TestCleanup]
		public void TearDown()
		{
			SafeFile.ReplaceFile = gOriginalReplace;
			SafeFile.Log = gOriginalLog;
			Directory.Delete(gFolder, true);
		}

		static void RunOnSta(Action action)
		{
			Exception error = null;
			Thread t = new Thread(() => {
				try{
					action();
				}
				catch(Exception ee){
					error = ee;
				}
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
			t.Join();
			if(error != null){
				throw new AssertFailedException(error.ToString());
			}
		}

		static TextEditor Edited(string text)
		{
			TextEditor ed = new TextEditor();
			ed.Text = text;
			ed.IsModified = true;
			return ed;
		}

		// Review finding 2: IsModified turned false before the file was replaced, so the copy
		// was gone while the save could still hang or fail.
		[TestMethod]
		public void Save_IsModifiedStaysTrue_UntilTheFileIsReplaced()
		{
			string file = Path.Combine(gFolder, "a.txt");
			File.WriteAllText(file, "old");
			RunOnSta(() => {
				TextEditor ed = Edited("new");
				bool? modifiedDuringReplace = null;
				SafeFile.ReplaceFile = (source, destination, backup) => {
					modifiedDuringReplace = ed.IsModified;
					gOriginalReplace(source, destination, backup);
				};
				ed.Save(file);
				Assert.AreEqual(true, modifiedDuringReplace);
				Assert.IsFalse(ed.IsModified);
			});
			Assert.AreEqual("new", File.ReadAllText(file));
		}

		[TestMethod]
		public void FailedSave_NeverTurnsIsModifiedOff()
		{
			string file = Path.Combine(gFolder, "a.txt");
			File.WriteAllText(file, "old");
			RunOnSta(() => {
				TextEditor ed = Edited("new");
				int changes = 0;
				DependencyPropertyDescriptor.FromProperty(TextEditor.IsModifiedProperty, typeof(TextEditor)).AddValueChanged(ed, (s, a) => changes++);
				SafeFile.ReplaceFile = (source, destination, backup) => { throw new UnauthorizedAccessException("read-only"); };
				Assert.ThrowsException<UnauthorizedAccessException>(() => ed.Save(file));
				Assert.AreEqual(0, changes);
				Assert.IsTrue(ed.IsModified);
			});
			Assert.AreEqual("old", File.ReadAllText(file));
		}

		// For review finding 6: the file's time and length as loaded and as saved.
		[TestMethod]
		public void LoadAndSave_RememberTheFilesTimeAndLength()
		{
			string file = Path.Combine(gFolder, "a.txt");
			File.WriteAllText(file, "loaded");
			RunOnSta(() => {
				TextEditor ed = new TextEditor();
				ed.Load(file);
				FileInfo fi = new FileInfo(file);
				Assert.AreEqual(fi.LastWriteTimeUtc.Ticks, ed.FileTime);
				Assert.AreEqual(fi.Length, ed.FileLength);

				ed.Text = "saved, and longer";
				ed.Save(file);
				fi.Refresh();
				Assert.AreEqual(fi.LastWriteTimeUtc.Ticks, ed.FileTime);
				Assert.AreEqual(fi.Length, ed.FileLength);
			});
		}
	}
}
