/*
 * Copies of the unsaved documents, kept in %AppData%\UyghurEditPP\recovery while the
 * program runs, so that the text can be offered again after the program (or Windows)
 * ended abnormally. The program removes its copies when a document is saved or closed,
 * and when it exits normally.
 *
 * Each running program has its own index file, recovery-<process id>-<start time>.json,
 * next to the copies (<id>.txt, UTF-8). A program that finds the index of a process that
 * is no longer running knows that process did not exit normally. Two programs running at
 * the same time never touch each other's files. All writes go through SafeFile.
 *
 * At the start, each leftover is first taken over by renaming its index to
 * claimed-<process id>-<start time>-<old name>, so that two windows started together do not
 * both offer it; one taken by a program that is no longer running can be taken again. Old
 * files are deleted only after the question was answered, never a leftover not yet offered.
 *
 * Not thread safe: use it from the UI thread.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using UyghurEditPP.Document;

namespace UyghurEditPP
{
	public sealed class Recovery
	{
		public const string FolderName = "recovery";

		// Longer texts (characters) are not copied: writing them would stop the editor for too long.
		public const int MaxChars = 20 * 1000 * 1000;

		public const int DefaultMinutes = 2;
		public const int MaxMinutes = 24 * 60;
		public const int KeepDays = 30;

		// Seam for the tests.
		internal static Action<Exception> Log = ee => ErrorLog.Write(ee);

		static readonly Regex gIdPattern = new Regex("^[0-9a-f]{32}$");
		static readonly UTF8Encoding gUtf8 = new UTF8Encoding(false);

		[DataContract]
		internal class Copy
		{
			[DataMember(Name = "id", Order = 1)] public string Id;
			// The file the text belongs to; none for a new document.
			[DataMember(Name = "path", EmitDefaultValue = false, Order = 2)] public string Path;
			[DataMember(Name = "title", EmitDefaultValue = false, Order = 3)] public string Title;
			[DataMember(Name = "codePage", Order = 4)] public int CodePage;
			// Whether the file is saved with a byte order mark (UTF-8, UTF-16).
			[DataMember(Name = "bom", EmitDefaultValue = false, Order = 6)] public bool Bom;
			// When the copy was written (UTC ticks).
			[DataMember(Name = "saved", Order = 5)] public long Saved;
			// The file as it was when the document was loaded or saved last (UTC ticks, bytes);
			// none (0) when not known.
			[DataMember(Name = "fileTime", EmitDefaultValue = false, Order = 7)] public long FileTime;
			[DataMember(Name = "fileLength", EmitDefaultValue = false, Order = 8)] public long FileLength;
		}

		[DataContract]
		internal class Index
		{
			[DataMember(Name = "pid", Order = 1)] public int Pid;
			// When the process started (UTC ticks), to tell it from a later process with the same id.
			[DataMember(Name = "started", Order = 2)] public long Started;
			[DataMember(Name = "documents", Order = 3)] public List<Copy> Documents = new List<Copy>();
		}

		/// <summary>
		/// What an earlier program that did not exit normally left behind.
		/// </summary>
		public sealed class Leftover
		{
			internal string IndexFile;
			internal string Folder;
			internal List<Copy> Documents = new List<Copy>();

			public IEnumerable<LeftoverCopy> Items{
				get{
					foreach(Copy d in Documents){
						yield return new LeftoverCopy(Folder, d);
					}
				}
			}
		}

		public sealed class LeftoverCopy
		{
			readonly string gFolder;
			readonly Copy gDocument;

			internal LeftoverCopy(string folder, Copy document)
			{
				gFolder = folder;
				gDocument = document;
			}

			/// <summary>The file the text was from; null for a new document.</summary>
			public string Path{ get{ return gDocument.Path; } }
			/// <summary>The name of the tab.</summary>
			public string Title{ get{ return gDocument.Title; } }
			public int CodePage{ get{ return gDocument.CodePage; } }
			public bool Bom{ get{ return gDocument.Bom; } }
			public DateTime SavedUtc{ get{ return new DateTime(gDocument.Saved, DateTimeKind.Utc); } }
			public string Id{ get{ return gDocument.Id; } }

			/// <summary>
			/// Whether the file is still exactly as it was when the document was loaded or saved
			/// last (same time and length), so the copy can go back into its own tab. False when
			/// that is not known or the file is gone.
			/// </summary>
			public bool FileUnchanged()
			{
				if(string.IsNullOrEmpty(Path) || gDocument.FileTime == 0){
					return false;
				}
				FileInfo fi = new FileInfo(Path);
				return fi.Exists && fi.LastWriteTimeUtc.Ticks == gDocument.FileTime && fi.Length == gDocument.FileLength;
			}

			/// <summary>The copied text. Throws when it cannot be read.</summary>
			public string ReadText()
			{
				return File.ReadAllText(TextFile(gFolder, gDocument.Id), gUtf8);
			}
		}

		readonly string gFolder;
		readonly Index gIndex;

		public Recovery(string folder, int pid, DateTime startedUtc)
		{
			gFolder = folder;
			gIndex = new Index{Pid = pid, Started = startedUtc.Ticks};
		}

		/// <summary>The recovery folder of this program (not created yet).</summary>
		public static string DefaultFolder{
			get{
				return AppPaths.DataFile(FolderName);
			}
		}

		string IndexFile{
			get{
				return IndexFileName(gFolder, gIndex.Pid, gIndex.Started);
			}
		}

		static string IndexFileName(string folder, int pid, long started)
		{
			return System.IO.Path.Combine(folder, "recovery-" + pid + "-" + started + ".json");
		}

		static string TextFile(string folder, string id)
		{
			if(id == null || !gIdPattern.IsMatch(id)){
				throw new InvalidDataException("Not a recovery copy name: " + id);
			}
			return System.IO.Path.Combine(folder, id + ".txt");
		}

		/// <summary>
		/// The autosave interval in minutes for a value from the settings file: 0 turns the
		/// autosave off, a missing or out of range value gives the default.
		/// </summary>
		public static int Minutes(object setting)
		{
			if(setting is int){
				int m = (int)setting;
				if(m >= 0 && m <= MaxMinutes){
					return m;
				}
			}
			return DefaultMinutes;
		}

		/// <summary>The id for a new copy.</summary>
		public static string NewId()
		{
			return Guid.NewGuid().ToString("N");
		}

		/// <summary>
		/// Notes in the error log that a document is not copied because it is too long.
		/// Only logged, by the owner's decision: the window does not warn.
		/// </summary>
		public static void ReportTooLong(string title, int length)
		{
			Log(new InvalidOperationException("No recovery copy is kept of \"" + title + "\": " + length + " characters, more than " + MaxChars + "."));
		}

		/// <summary>
		/// What was last written for a document. The copy is written again when any of it
		/// changes: the text, the encoding, or the file it belongs to.
		/// </summary>
		public sealed class Written
		{
			public string Id;
			public ITextSourceVersion Version;
			public int CodePage;
			public bool Bom;
			public long FileTime;
			public long FileLength;

			public bool Same(ITextSourceVersion version, int codePage, bool bom, long fileTime, long fileLength)
			{
				return Version != null && version.BelongsToSameDocumentAs(Version) && version.CompareAge(Version) == 0
					&& codePage == CodePage && bom == Bom && fileTime == FileTime && fileLength == FileLength;
			}
		}

		/// <summary>
		/// Writes (or replaces) the copy <paramref name="id"/>. fileTime and fileLength are those
		/// of the file as loaded or saved last (0 when not known). The caller keeps the text within
		/// MaxChars. Throws when a file cannot be written.
		/// </summary>
		public void Save(string id, string path, string title, int codePage, bool bom, string text, long fileTime = 0, long fileLength = 0)
		{
			Directory.CreateDirectory(gFolder);
			// The copy first, then the index that names it: an index never names a missing copy.
			SafeFile.Write(TextFile(gFolder, id), stream => {
				byte[] bytes = gUtf8.GetBytes(text);
				stream.Write(bytes, 0, bytes.Length);
			});
			Copy doc = gIndex.Documents.Find(d => d.Id == id);
			if(doc == null){
				doc = new Copy{Id = id};
				gIndex.Documents.Add(doc);
			}
			doc.Path = path;
			doc.Title = title;
			doc.CodePage = codePage;
			doc.Bom = bom;
			doc.FileTime = fileTime;
			doc.FileLength = fileLength;
			doc.Saved = DateTime.UtcNow.Ticks;
			WriteIndex();
		}

		/// <summary>Forgets the copy <paramref name="id"/> and deletes it.</summary>
		public void Remove(string id)
		{
			if(gIndex.Documents.RemoveAll(d => d.Id == id) == 0){
				return;
			}
			// The index first: an index never names a missing copy.
			if(gIndex.Documents.Count == 0){
				File.Delete(IndexFile);
			}
			else{
				WriteIndex();
			}
			File.Delete(TextFile(gFolder, id));
		}

		/// <summary>
		/// Deletes everything of this program: used when it exits normally. Never throws.
		/// </summary>
		public void Clear()
		{
			try{
				string[] ids = gIndex.Documents.ConvertAll(d => d.Id).ToArray();
				gIndex.Documents.Clear();
				if(File.Exists(IndexFile)){
					File.Delete(IndexFile);
				}
				foreach(string id in ids){
					DeleteQuietly(TextFile(gFolder, id));
				}
			}
			catch(Exception ee){
				Log(ee);
			}
		}

		void WriteIndex()
		{
			SafeFile.Write(IndexFile, stream => {
				using(var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  ")){
					new DataContractJsonSerializer(typeof(Index)).WriteObject(writer, gIndex);
					writer.Flush();
				}
			});
		}

		static Index ReadIndex(string file)
		{
			using(FileStream fs = File.OpenRead(file)){
				return (Index)new DataContractJsonSerializer(typeof(Index)).ReadObject(fs);
			}
		}

		static void DeleteQuietly(string file)
		{
			try{
				File.Delete(file);
			}
			catch(Exception ee){
				Log(ee);
			}
		}

		/// <summary>
		/// Whether the program that wrote an index is still running. When that cannot be told at
		/// all it counts as running, so its files are left alone.
		/// </summary>
		public static bool IsRunning(int pid, long startedUtcTicks)
		{
			try{
				using(Process p = Process.GetProcessById(pid)){
					try{
						if(p.HasExited){
							return false;
						}
						return Math.Abs(p.StartTime.ToUniversalTime().Ticks - startedUtcTicks) < 2 * TimeSpan.TicksPerSecond;
					}
					catch(Exception){
						// The start time cannot be read (no access): go by the program name.
						return string.Equals(p.ProcessName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);
					}
				}
			}
			catch(ArgumentException){
				return false; // no process with that id
			}
			catch(Exception){
				return true;
			}
		}

		static readonly Regex gClaimed = new Regex(@"^claimed-(\d+)-(\d+)-(recovery-.+\.json)$", RegexOptions.IgnoreCase);

		// The index files in the folder: those of the programs, and those taken over at a start.
		static IEnumerable<string> IndexFiles(string folder)
		{
			foreach(string file in Directory.GetFiles(folder, "recovery-*.json")){
				yield return file;
			}
			foreach(string file in Directory.GetFiles(folder, "claimed-*.json")){
				yield return file;
			}
		}

		// Whether the program the index file belongs to is running: the one that took it over,
		// or else the one that wrote it.
		static bool OwnerRunning(string file, Index index, Func<int, long, bool> isRunning)
		{
			Match m = gClaimed.Match(System.IO.Path.GetFileName(file));
			if(m.Success){
				int pid;
				long started;
				if(int.TryParse(m.Groups[1].Value, out pid) && long.TryParse(m.Groups[2].Value, out started)){
					return isRunning(pid, started);
				}
			}
			return isRunning(index.Pid, index.Started);
		}

		/// <summary>
		/// The copies of programs that are no longer running, those with at least one readable
		/// copy. An index that cannot be read is skipped (and kept).
		/// </summary>
		public static List<Leftover> FindLeftovers(string folder, Func<int, long, bool> isRunning)
		{
			List<Leftover> found = new List<Leftover>();
			if(!Directory.Exists(folder)){
				return found;
			}
			foreach(string file in IndexFiles(folder)){
				try{
					Index index = ReadIndex(file);
					if(index == null || index.Documents == null || OwnerRunning(file, index, isRunning)){
						continue;
					}
					Leftover left = new Leftover{IndexFile = file, Folder = folder};
					foreach(Copy d in index.Documents){
						if(d != null && d.Id != null && gIdPattern.IsMatch(d.Id) && File.Exists(TextFile(folder, d.Id))){
							left.Documents.Add(d);
						}
					}
					if(left.Documents.Count > 0){
						found.Add(left);
					}
				}
				catch(Exception ee){
					Log(ee);
				}
			}
			return found;
		}

		/// <summary>
		/// Takes a leftover over for the program pid (started at startedUtcTicks), by renaming its
		/// index. Only one program can do so: false when another one took it first.
		/// </summary>
		public static bool Claim(Leftover left, int pid, long startedUtcTicks)
		{
			string name = System.IO.Path.GetFileName(left.IndexFile);
			Match m = gClaimed.Match(name);
			string claimed = System.IO.Path.Combine(left.Folder, "claimed-" + pid + "-" + startedUtcTicks + "-" + (m.Success ? m.Groups[3].Value : name));
			try{
				File.Move(left.IndexFile, claimed);
			}
			catch(Exception ee){
				if(!(ee is FileNotFoundException)){
					Log(ee);   // not merely taken by another program
				}
				return false;
			}
			left.IndexFile = claimed;
			return true;
		}

		/// <summary>Deletes a leftover: its index and all its copies. Never throws.</summary>
		public static void Discard(Leftover left)
		{
			try{
				Index index = ReadIndex(left.IndexFile);
				if(index != null && index.Documents != null){
					foreach(Copy d in index.Documents){
						if(d != null && d.Id != null && gIdPattern.IsMatch(d.Id)){
							DeleteQuietly(TextFile(left.Folder, d.Id));
						}
					}
				}
				DeleteQuietly(left.IndexFile);
			}
			catch(Exception ee){
				Log(ee);
			}
		}

		/// <summary>
		/// What happens at the start: the leftovers of programs that did not exit normally are
		/// taken over (see Claim) and, if there are any, ask() is called once. On "No" (false)
		/// they are deleted; on "Yes" (true) each one restore() returns true for is deleted.
		/// Only then the files older than olderThan are deleted (see DeleteOld), except those
		/// just offered. pid and startedUtcTicks are this program's.
		/// </summary>
		public static void Offer(string folder, int pid, long startedUtcTicks, Func<int, long, bool> isRunning, DateTime olderThan, Func<bool?> ask, Func<Leftover, bool> restore)
		{
			List<Leftover> found = FindLeftovers(folder, isRunning);
			// Kept from the cleanup: also a leftover that could not be taken over (held open, for
			// example), which is then offered at a later start.
			HashSet<string> offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(Leftover left in found){
				offered.Add(left.IndexFile);
			}
			List<Leftover> mine = found.FindAll(left => Claim(left, pid, startedUtcTicks));
			if(mine.Count > 0){
				foreach(Leftover left in mine){
					offered.Add(left.IndexFile);
				}
				bool? answer = ask();
				foreach(Leftover left in mine){
					// A leftover is deleted only after an explicit "No", or once all its documents
					// are back in tabs (and have copies of their own); otherwise it stays for the next start.
					if(answer == false || (answer == true && restore(left))){
						Discard(left);
					}
				}
			}
			try{
				DeleteOld(folder, olderThan, isRunning, offered);
			}
			catch(Exception ee){
				Log(ee);
			}
		}

		/// <summary>
		/// Deletes the files in the folder last written before olderThan. The files of a program
		/// (its index and its copies) are judged together, by the newest of them, and kept while
		/// that program (or the one that took them over) runs, or when keepIndexFiles names the
		/// index. Returns how many files were deleted.
		/// </summary>
		public static int DeleteOld(string folder, DateTime olderThan, Func<int, long, bool> isRunning, ICollection<string> keepIndexFiles = null)
		{
			if(!Directory.Exists(folder)){
				return 0;
			}
			DateTime limit = olderThan.ToUniversalTime();
			HashSet<string> keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(string file in IndexFiles(folder)){
				try{
					Index index = ReadIndex(file);
					if(index == null || index.Documents == null){
						continue;
					}
					List<string> session = new List<string>{ System.IO.Path.GetFileName(file) };
					foreach(Copy d in index.Documents){
						if(d != null && d.Id != null && gIdPattern.IsMatch(d.Id)){
							session.Add(d.Id + ".txt");
						}
					}
					bool recent = session.Exists(name => {
						string f = System.IO.Path.Combine(folder, name);
						return File.Exists(f) && File.GetLastWriteTimeUtc(f) >= limit;
					});
					if(recent || (keepIndexFiles != null && keepIndexFiles.Contains(file)) || OwnerRunning(file, index, isRunning)){
						keep.UnionWith(session);
					}
				}
				catch(Exception ee){
					Log(ee);   // an index that cannot be read is judged by its own age, below
				}
			}
			int count = 0;
			foreach(string file in Directory.GetFiles(folder)){
				if(keep.Contains(System.IO.Path.GetFileName(file)) || File.GetLastWriteTimeUtc(file) >= limit){
					continue;
				}
				try{
					File.Delete(file);
					count++;
				}
				catch(Exception ee){
					Log(ee);
				}
			}
			return count;
		}
	}
}
