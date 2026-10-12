using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	// The recovery copy as MainForm keeps it while a file is saved. The data folder (settings,
	// recovery copies, logs) is a temporary one, so the user's own folder is not touched.
	[TestClass]
	public class MainFormRecoveryTests
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
			AppPaths.DataFolderOverride = gFolder;
			Assert.IsTrue(AppPaths.DataFolder.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase));
		}

		[TestCleanup]
		public void TearDown()
		{
			AppPaths.DataFolderOverride = null;
			SafeFile.ReplaceFile = gOriginalReplace;
			SafeFile.Log = gOriginalLog;
			Directory.Delete(gFolder, true);
		}

		static object Field(MainForm form, string name)
		{
			return typeof(MainForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
		}

		static void Call(MainForm form, string name, params object[] args)
		{
			try{
				typeof(MainForm).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, args);
			}
			catch(TargetInvocationException ee){
				throw ee.InnerException;
			}
		}

		string CopyText()
		{
			return Recovery.FindLeftovers(Path.Combine(gFolder, "recovery"), (pid, started) => false).Single().Items.Single().ReadText();
		}

		// Review finding 2: when saving fails, the copy is brought up to date at once (the
		// edits since the last copy would otherwise wait for the timer).
		[TestMethod]
		public void FailedSave_WritesTheCopyAgainAtOnce_AndASavedFileDeletesIt()
		{
			string file = Path.Combine(gFolder, "a.txt");
			File.WriteAllText(file, "old");
			RunOnSta(() => {
				using(MainForm form = new MainForm()){
					form.ShowInTaskbar = false;
					form.Show();   // loads the settings and starts the recovery, in the temporary folder
					Assert.IsNotNull(Field(form, "gRecovery"));
					form.OpenaFile(file);
					TextEditor ed = form.gEditor;
					ed.Document.Insert(0, "first ");   // the first change makes the copy at once
					Assert.AreEqual("first old", CopyText());
					ed.Document.Insert(0, "second ");  // later changes wait for the timer
					Assert.AreEqual("first old", CopyText());

					// Only the document's file cannot be written (the copies still can).
					SafeFile.ReplaceFile = (source, destination, backup) => {
						if(string.Equals(Path.GetFullPath(destination), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase)){
							throw new UnauthorizedAccessException("read-only");
						}
						gOriginalReplace(source, destination, backup);
					};
					Assert.ThrowsException<UnauthorizedAccessException>(() => Call(form, "SaveEditor", ed, file));
					Assert.AreEqual("second first old", CopyText());

					SafeFile.ReplaceFile = gOriginalReplace;
					Call(form, "SaveEditor", ed, file);
					Assert.AreEqual(0, Recovery.FindLeftovers(Path.Combine(gFolder, "recovery"), (pid, started) => false).Count);
				}
			});
			Assert.AreEqual("second first old", File.ReadAllText(file));
		}

		static void RunOnSta(Action action)
		{
			Exception error = null;
			Thread t = new Thread(() => {
				try{
					action();
				}
				catch(Exception e){
					error = e;
				}
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
			t.Join();
			if(error != null){
				throw new AssertFailedException(error.ToString());
			}
		}
	}
}
