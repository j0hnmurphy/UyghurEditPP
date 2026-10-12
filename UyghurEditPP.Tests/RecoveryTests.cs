using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class RecoveryTests
	{
		string gFolder;
		Action<Exception> gOriginalLog;
		List<Exception> gLogged;

		static readonly Func<int, long, bool> Dead = (pid, started) => false;
		static readonly Func<int, long, bool> Alive = (pid, started) => true;

		[TestInitialize]
		public void SetUp()
		{
			gFolder = Path.Combine(Path.GetTempPath(), "UyghurEditPP.Tests", Guid.NewGuid().ToString("N"), "recovery");
			gOriginalLog = Recovery.Log;
			gLogged = new List<Exception>();
			Recovery.Log = ee => gLogged.Add(ee);
		}

		[TestCleanup]
		public void TearDown()
		{
			Recovery.Log = gOriginalLog;
			string root = Directory.GetParent(gFolder).FullName;
			if(Directory.Exists(root)){
				Directory.Delete(root, true);
			}
		}

		Recovery New(int pid = 100, long started = 5)
		{
			return new Recovery(gFolder, pid, new DateTime(started, DateTimeKind.Utc));
		}

		string[] Files(string pattern = "*")
		{
			return Directory.Exists(gFolder) ? Directory.GetFiles(gFolder, pattern) : new string[0];
		}

		[TestMethod]
		public void SaveThenFind_ReturnsTheTextAndItsDetails()
		{
			string text = "ئۇيغۇرچە\r\nJapanese 日本語\nlone line\r\n\r\n";
			string id = Recovery.NewId();
			Assert.IsTrue(New().Save(id, @"D:\ئۇيغۇرچە\a.txt", "a.txt", 932, true, text));

			List<Recovery.Leftover> found = Recovery.FindLeftovers(gFolder, Dead);

			Assert.AreEqual(1, found.Count);
			Recovery.LeftoverCopy item = found[0].Items.Single();
			Assert.AreEqual(id, item.Id);
			Assert.AreEqual(@"D:\ئۇيغۇرچە\a.txt", item.Path);
			Assert.AreEqual("a.txt", item.Title);
			Assert.AreEqual(932, item.CodePage);
			Assert.IsTrue(item.Bom);
			Assert.AreEqual(text, item.ReadText());
			Assert.IsTrue((DateTime.UtcNow - item.SavedUtc).TotalMinutes < 1);
			Assert.AreEqual(0, gLogged.Count);
		}

		[TestMethod]
		public void NewDocument_HasNoPath()
		{
			New().Save(Recovery.NewId(), null, "Namsiz_1.txt", 0, false, "x");

			Recovery.LeftoverCopy item = Recovery.FindLeftovers(gFolder, Dead)[0].Items.Single();

			Assert.IsNull(item.Path);
			Assert.AreEqual("Namsiz_1.txt", item.Title);
		}

		[TestMethod]
		public void SaveAgain_ReplacesTheCopy()
		{
			Recovery r = New();
			string id = Recovery.NewId();
			r.Save(id, null, "t", 0, false, "first");
			r.Save(id, null, "t", 0, false, "second");

			Recovery.LeftoverCopy item = Recovery.FindLeftovers(gFolder, Dead)[0].Items.Single();

			Assert.AreEqual("second", item.ReadText());
			Assert.AreEqual(1, Files("*.txt").Length);
		}

		[TestMethod]
		public void Remove_LastCopy_DeletesTheIndexToo()
		{
			Recovery r = New();
			string id = Recovery.NewId();
			r.Save(id, null, "t", 0, false, "x");

			r.Remove(id);

			Assert.AreEqual(0, Files().Length);
			Assert.AreEqual(0, Recovery.FindLeftovers(gFolder, Dead).Count);
		}

		[TestMethod]
		public void Remove_OneOfTwo_KeepsTheOther()
		{
			Recovery r = New();
			string a = Recovery.NewId();
			string b = Recovery.NewId();
			r.Save(a, null, "a", 0, false, "A");
			r.Save(b, null, "b", 0, false, "B");

			r.Remove(a);

			Recovery.LeftoverCopy item = Recovery.FindLeftovers(gFolder, Dead)[0].Items.Single();
			Assert.AreEqual(b, item.Id);
			Assert.AreEqual("B", item.ReadText());
			Assert.IsFalse(File.Exists(Path.Combine(gFolder, a + ".txt")));
			r.Remove(a);   // again: nothing happens
		}

		[TestMethod]
		public void RunningProgram_IsNotALeftover()
		{
			New().Save(Recovery.NewId(), null, "t", 0, false, "x");

			Assert.AreEqual(0, Recovery.FindLeftovers(gFolder, Alive).Count);
			Assert.AreEqual(2, Files().Length);
		}

		[TestMethod]
		public void FindLeftovers_AsksWhetherThatProgramRuns_WithItsIdAndStartTime()
		{
			New(777, 12345).Save(Recovery.NewId(), null, "t", 0, false, "x");
			int pid = 0;
			long started = 0;

			Recovery.FindLeftovers(gFolder, (p, s) => { pid = p; started = s; return false; });

			Assert.AreEqual(777, pid);
			Assert.AreEqual(12345, started);
		}

		[TestMethod]
		public void TwoPrograms_DoNotTouchEachOther()
		{
			Recovery a = New(1, 10);
			Recovery b = New(2, 20);
			a.Save(Recovery.NewId(), null, "a", 0, false, "A");
			b.Save(Recovery.NewId(), null, "b", 0, false, "B");

			b.Clear();

			List<Recovery.Leftover> found = Recovery.FindLeftovers(gFolder, Dead);
			Assert.AreEqual(1, found.Count);
			Assert.AreEqual("A", found[0].Items.Single().ReadText());
		}

		[TestMethod]
		public void SameProcessIdWithAnotherStartTime_GetsAnotherIndex()
		{
			New(1, 10).Save(Recovery.NewId(), null, "old", 0, false, "old");
			New(1, 20).Save(Recovery.NewId(), null, "new", 0, false, "new");

			Assert.AreEqual(2, Recovery.FindLeftovers(gFolder, Dead).Count);
		}

		[TestMethod]
		public void Clear_RemovesAllOfThisProgram()
		{
			Recovery r = New();
			r.Save(Recovery.NewId(), null, "a", 0, false, "A");
			r.Save(Recovery.NewId(), null, "b", 0, false, "B");

			r.Clear();

			Assert.AreEqual(0, Files().Length);
			Assert.AreEqual(0, gLogged.Count);
		}

		[TestMethod]
		public void Clear_WithNothingWritten_Works()
		{
			New().Clear();
			Assert.AreEqual(0, gLogged.Count);
		}

		[TestMethod]
		public void Discard_RemovesTheIndexAndTheCopies()
		{
			Recovery r = New();
			r.Save(Recovery.NewId(), null, "a", 0, false, "A");
			r.Save(Recovery.NewId(), null, "b", 0, false, "B");

			Recovery.Discard(Recovery.FindLeftovers(gFolder, Dead)[0]);

			Assert.AreEqual(0, Files().Length);
		}

		[TestMethod]
		public void CorruptIndex_IsSkippedLoggedAndKept()
		{
			Directory.CreateDirectory(gFolder);
			string bad = Path.Combine(gFolder, "recovery-1-2.json");
			File.WriteAllText(bad, "{ not json");
			New(5, 6).Save(Recovery.NewId(), null, "ok", 0, false, "fine");

			List<Recovery.Leftover> found = Recovery.FindLeftovers(gFolder, Dead);

			Assert.AreEqual(1, found.Count);
			Assert.AreEqual("fine", found[0].Items.Single().ReadText());
			Assert.AreEqual(1, gLogged.Count);
			Assert.AreEqual("{ not json", File.ReadAllText(bad));
		}

		[TestMethod]
		public void IndexNamingAMissingCopy_IsSkipped()
		{
			Recovery r = New();
			string id = Recovery.NewId();
			r.Save(id, null, "t", 0, false, "x");
			File.Delete(Path.Combine(gFolder, id + ".txt"));

			Assert.AreEqual(0, Recovery.FindLeftovers(gFolder, Dead).Count);
		}

		[TestMethod]
		public void IdThatIsNotAHexName_IsRejected()
		{
			Recovery r = New();
			Assert.ThrowsException<InvalidDataException>(() => r.Save(@"..\x", null, "t", 0, false, "x"));
			Assert.ThrowsException<InvalidDataException>(() => r.Save("abc", null, "t", 0, false, "x"));
			Assert.AreEqual(0, Files().Length);

			// An index that someone edited by hand to point outside the folder.
			Directory.CreateDirectory(gFolder);
			File.WriteAllText(Path.Combine(gFolder, "recovery-9-9.json"),
				"{\"pid\":9,\"started\":9,\"documents\":[{\"id\":\"..\\\\..\\\\x\",\"codePage\":0,\"saved\":1}]}");
			Assert.AreEqual(0, Recovery.FindLeftovers(gFolder, Dead).Count);
		}

		[TestMethod]
		public void TooLongText_IsNotWritten()
		{
			bool saved = New().Save(Recovery.NewId(), null, "t", 0, false, new string('a', Recovery.MaxChars + 1));

			Assert.IsFalse(saved);
			Assert.AreEqual(0, Files().Length);
		}

		[TestMethod]
		public void DeleteOld_RemovesOldFiles_ButNotThoseOfARunningProgram()
		{
			Recovery running = New(1, 10);
			running.Save(Recovery.NewId(), null, "run", 0, false, "R");
			Recovery dead = New(2, 20);
			dead.Save(Recovery.NewId(), null, "dead", 0, false, "D");
			File.WriteAllText(Path.Combine(gFolder, ".stray.tmp"), "x");
			DateTime old = DateTime.Now.AddDays(-31);
			foreach(string f in Files()){
				File.SetLastWriteTime(f, old);
			}
			Recovery fresh = New(3, 30);
			fresh.Save(Recovery.NewId(), null, "fresh", 0, false, "F");

			int deleted = Recovery.DeleteOld(gFolder, DateTime.Now.AddDays(-30), (pid, started) => pid == 1);

			Assert.AreEqual(3, deleted);   // the dead one's index and copy, and the stray file
			List<Recovery.Leftover> rest = Recovery.FindLeftovers(gFolder, Dead);
			Assert.AreEqual(2, rest.Count);
			CollectionAssert.AreEquivalent(new[] { "R", "F" }, rest.Select(l => l.Items.Single().ReadText()).ToArray());
		}

		static void Age(string file, int days)
		{
			File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-days));
		}

		string CopyFile(string id)
		{
			return Path.Combine(gFolder, id + ".txt");
		}

		// Review finding 1: a copy that was not rewritten for 30 days belongs to a session that
		// wrote another copy recently; it was deleted alone and never offered.
		[TestMethod]
		public void DeleteOld_KeepsAnOldCopyOfASessionThatWroteRecently()
		{
			Recovery dead = New(2, 20);
			string untouched = Recovery.NewId(), recent = Recovery.NewId();
			dead.Save(untouched, null, "untouched", 0, false, "U");
			dead.Save(recent, null, "recent", 0, false, "N");
			Age(CopyFile(untouched), 40);

			Recovery.DeleteOld(gFolder, DateTime.UtcNow.AddDays(-30), Dead);

			Assert.IsTrue(File.Exists(CopyFile(untouched)));
			Assert.AreEqual(2, Recovery.FindLeftovers(gFolder, Dead)[0].Items.Count());
		}

		[TestMethod]
		public void DeleteOld_RemovesASessionWhoseFilesAreAllOld_AsAWhole()
		{
			Recovery dead = New(2, 20);
			dead.Save(Recovery.NewId(), null, "a", 0, false, "A");
			foreach(string f in Files()){
				Age(f, 40);
			}
			Assert.AreEqual(2, Recovery.DeleteOld(gFolder, DateTime.UtcNow.AddDays(-30), Dead));
			Assert.AreEqual(0, Files().Length);
		}

		// Review finding 1: the cleanup ran before the question, so an old leftover was deleted
		// without being offered.
		[TestMethod]
		public void Offer_AsksBeforeAnOldLeftoverIsDeleted()
		{
			New(2, 20).Save(Recovery.NewId(), null, "old", 0, false, "O");
			foreach(string f in Files()){
				Age(f, 40);
			}
			int asked = 0;
			// "Yes", but the document could not be restored: everything stays for the next start.
			Recovery.Offer(gFolder, 300, 30, Dead, DateTime.UtcNow.AddDays(-30), () => { asked++; return true; }, left => false);

			Assert.AreEqual(1, asked);
			Assert.AreEqual(2, Files().Length);

			Recovery.Offer(gFolder, 400, 40, Dead, DateTime.UtcNow.AddDays(-30), () => false, left => true);
			Assert.AreEqual(0, Files().Length, "after No");
		}

		// Code review: a leftover that could not be taken over (its index held open, for example by
		// a virus scanner) was not offered, and the cleanup then deleted its old copy.
		[TestMethod]
		public void Offer_LeftoverThatCannotBeTakenOver_IsNotDeletedUnoffered()
		{
			string id = Recovery.NewId();
			New(2, 20).Save(id, null, "old", 0, false, "O");
			foreach(string f in Files()){
				Age(f, 40);
			}
			string index = Files("recovery-*.json").Single();
			using(new FileStream(index, FileMode.Open, FileAccess.Read, FileShare.Read)){
				Recovery.Offer(gFolder, 300, 30, Dead, DateTime.UtcNow.AddDays(-30), () => { Assert.Fail("asked"); return null; }, left => true);
			}
			Assert.IsTrue(File.Exists(CopyFile(id)));
			Assert.AreEqual(1, Recovery.FindLeftovers(gFolder, Dead).Count, "offered at the next start");
		}

		[TestMethod]
		public void Offer_CleanupNeverTouchesTheRunningProgramsCopies()
		{
			Recovery me = New(300, 30);
			me.Save(Recovery.NewId(), null, "mine", 0, false, "M");
			foreach(string f in Files()){
				Age(f, 40);
			}
			Recovery.Offer(gFolder, 300, 30, (pid, started) => pid == 300, DateTime.UtcNow.AddDays(-30), () => { Assert.Fail("asked"); return null; }, left => true);
			Assert.AreEqual(2, Files().Length);
		}

		// Review finding 3: two windows started together both offered the same leftover; one
		// "No" deleted what the other was restoring.
		[TestMethod]
		public void Offer_TwoWindows_OnlyOneAsks()
		{
			New(100, 10).Save(Recovery.NewId(), null, "crashed", 0, false, "C");
			Func<int, long, bool> windowsRunning = (pid, started) => pid == 200 || pid == 300;
			int askedA = 0, askedB = 0;
			Recovery.Offer(gFolder, 200, 20, windowsRunning, DateTime.UtcNow.AddDays(-30), () => {
				askedA++;
				// While A's question is open, window B starts.
				Recovery.Offer(gFolder, 300, 30, windowsRunning, DateTime.UtcNow.AddDays(-30), () => { askedB++; return false; }, left => true);
				Assert.AreEqual(1, Files("*.txt").Length, "B must not delete what A offers");
				return true;
			}, left => left.Items.Single().ReadText() == "C");

			Assert.AreEqual(1, askedA);
			Assert.AreEqual(0, askedB);
			Assert.AreEqual(0, Files().Length, "restored by A, then deleted");
		}

		[TestMethod]
		public void Offer_LeftoverTakenByAWindowThatEnded_IsOfferedAgain()
		{
			New(100, 10).Save(Recovery.NewId(), null, "crashed", 0, false, "C");
			bool aRunning = true;
			Func<int, long, bool> running = (pid, started) => pid == 200 ? aRunning : false;
			// A takes it, but restoring fails: it keeps it while it runs.
			Recovery.Offer(gFolder, 200, 20, running, DateTime.UtcNow.AddDays(-30), () => true, left => false);
			Assert.AreEqual(0, Recovery.FindLeftovers(gFolder, running).Count, "A still runs");

			aRunning = false;
			int asked = 0;
			string text = null;
			Recovery.Offer(gFolder, 300, 30, running, DateTime.UtcNow.AddDays(-30), () => { asked++; return true; }, left => { text = left.Items.Single().ReadText(); return true; });
			Assert.AreEqual(1, asked);
			Assert.AreEqual("C", text);
			Assert.AreEqual(0, Files().Length);
		}

		// Review finding 6: whether the file is unchanged was judged by the time of the copy,
		// not by the file as it was loaded; a change between loading and the first copy was missed.
		[TestMethod]
		public void FileChangedAfterLoading_BeforeTheCopy_IsNotUnchanged()
		{
			Directory.CreateDirectory(gFolder);
			string file = Path.Combine(gFolder, "doc.txt");
			File.WriteAllText(file, "as loaded");
			FileInfo loaded = new FileInfo(file);
			long loadedTime = DateTime.UtcNow.AddMinutes(-10).Ticks;
			File.SetLastWriteTimeUtc(file, new DateTime(loadedTime, DateTimeKind.Utc));
			// Another program writes the file, then our copy is made.
			File.WriteAllText(file, "written by another program");
			File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-5));
			New().Save(Recovery.NewId(), file, "doc.txt", 65001, false, "edited", loadedTime, loaded.Length);

			Assert.IsFalse(Recovery.FindLeftovers(gFolder, Dead)[0].Items.Single().FileUnchanged());
		}

		[TestMethod]
		public void FileAsLoaded_IsUnchanged_OtherwiseNot()
		{
			Directory.CreateDirectory(gFolder);
			string file = Path.Combine(gFolder, "doc.txt");
			File.WriteAllText(file, "as loaded");
			FileInfo fi = new FileInfo(file);
			New(1).Save(Recovery.NewId(), file, "with", 65001, false, "edited", fi.LastWriteTimeUtc.Ticks, fi.Length);
			// A copy without the file's details (it cannot be told): not unchanged.
			New(2).Save(Recovery.NewId(), file, "without", 65001, false, "edited");
			List<Recovery.LeftoverCopy> found = Recovery.FindLeftovers(gFolder, Dead).Select(l => l.Items.Single()).ToList();
			Recovery.LeftoverCopy withDetails = found.Single(c => c.Title == "with");
			Recovery.LeftoverCopy without = found.Single(c => c.Title == "without");

			Assert.IsTrue(withDetails.FileUnchanged());
			Assert.IsFalse(without.FileUnchanged());
			File.AppendAllText(file, "!");
			File.SetLastWriteTimeUtc(file, fi.LastWriteTimeUtc);   // same time, other length
			Assert.IsFalse(withDetails.FileUnchanged());
			File.Delete(file);
			Assert.IsFalse(withDetails.FileUnchanged());
		}

		// Review finding 7: a copy was not written again when only the encoding changed.
		[TestMethod]
		public void Written_IsOutOfDate_WhenOnlyTheEncodingOrBomChanged()
		{
			UyghurEditPP.Document.TextDocument doc = new UyghurEditPP.Document.TextDocument("text");
			Recovery.Written w = new Recovery.Written{ Id = Recovery.NewId(), Version = doc.Version, CodePage = 65001, Bom = false };

			Assert.IsTrue(w.Same(doc.Version, 65001, false, 0, 0));
			Assert.IsFalse(w.Same(doc.Version, 932, false, 0, 0));
			Assert.IsFalse(w.Same(doc.Version, 65001, true, 0, 0));
			doc.Insert(0, "x");
			Assert.IsFalse(w.Same(doc.Version, 65001, false, 0, 0));
		}

		// Review finding 8: a document too long to copy had no protection, silently.
		[TestMethod]
		public void TooLongText_IsLogged()
		{
			New().Save(Recovery.NewId(), null, "t", 0, false, new string('a', Recovery.MaxChars + 1));
			Assert.AreEqual(1, gLogged.Count);
			StringAssert.Contains(gLogged[0].Message, "t");
		}

		[TestMethod]
		public void DeleteOld_NoFolder_ReturnsZero()
		{
			Assert.AreEqual(0, Recovery.DeleteOld(gFolder, DateTime.Now, Dead));
		}

		[TestMethod]
		public void Minutes_UsesTheDefaultForAnythingOutOfRange()
		{
			Assert.AreEqual(2, Recovery.Minutes(null));
			Assert.AreEqual(2, Recovery.Minutes(-1));
			Assert.AreEqual(2, Recovery.Minutes(Recovery.MaxMinutes + 1));
			Assert.AreEqual(2, Recovery.Minutes("5"));
			Assert.AreEqual(0, Recovery.Minutes(0));
			Assert.AreEqual(5, Recovery.Minutes(5));
			Assert.AreEqual(Recovery.MaxMinutes, Recovery.Minutes(Recovery.MaxMinutes));
		}

		[TestMethod]
		public void IsRunning_ComparesTheStartTime()
		{
			Process me = Process.GetCurrentProcess();

			Assert.IsTrue(Recovery.IsRunning(me.Id, me.StartTime.ToUniversalTime().Ticks));
			Assert.IsFalse(Recovery.IsRunning(me.Id, me.StartTime.ToUniversalTime().AddHours(-1).Ticks));
		}

		[TestMethod]
		public void IsRunning_NoSuchProcess_IsFalse()
		{
			int pid = 100000;
			while(true){
				try{
					Process.GetProcessById(pid).Dispose();
					pid++;
				}
				catch(ArgumentException){
					break;
				}
			}

			Assert.IsFalse(Recovery.IsRunning(pid, 1));
		}

		[TestMethod]
		public void IsRunning_ProcessWeCannotQuery_IsNotTakenForOurProgram()
		{
			// Process 4 is the Windows "System" process: its start time cannot be read.
			Assert.IsFalse(Recovery.IsRunning(4, 1));
		}
	}
}
