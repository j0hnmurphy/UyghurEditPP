/*
 * Where UyghurEdit++ keeps its own files (settings, user dictionaries, logs).
 *
 * The program folder may be read-only (for example under Program Files), so
 * these files live in %AppData%\UyghurEditPP. Files left in the program folder
 * by older versions are copied there once.
 *
 * The correction list imla_xatatoghra.txt comes with the program and stays in the
 * program folder; only the user's own corrections are kept in %AppData%.
 */
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UyghurEditPP
{
	public static class AppPaths
	{
		public const string ConfigFileName      = "uyghuredit.cfg";
		public const string IshletkuchiFileName = "imla_ishletkuchi.txt";
		public const string XataToghraFileName  = "imla_xatatoghra.txt";

		// Not imla_xatatoghra.txt: the copy in the program folder is the shipped list, and it
		// is still read from there (with anything older versions appended to it).
		static readonly string[] gKonaHojjetler = { ConfigFileName, IshletkuchiFileName };

		/// <summary>
		/// %AppData%\UyghurEditPP
		/// </summary>
		public static string DataFolder{
			get{
				return ChooseDataFolder(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProgramFolder, IsPackaged, Path.GetTempPath());
			}
		}

		/// <summary>
		/// Picks the data folder: %AppData%\UyghurEditPP when there is a profile folder.
		/// Without one the program folder is used as before, except in a packaged (MSIX)
		/// install, where the program folder is read-only: then a folder under tempFolder.
		/// </summary>
		public static string ChooseDataFolder(string appData, string programFolder, bool packaged, string tempFolder)
		{
			if(!string.IsNullOrEmpty(appData)){
				return Path.Combine(appData, "UyghurEditPP");
			}
			// No profile folder (rare service/locked-down accounts).
			if(packaged){
				return Path.Combine(tempFolder, "UyghurEditPP");
			}
			return programFolder;
		}

		static bool gPackagedChecked;
		static bool gPackaged;

		/// <summary>
		/// True when running with package identity (installed from the MSIX package).
		/// </summary>
		public static bool IsPackaged{
			get{
				if(!gPackagedChecked){
					gPackaged = DetectPackaged();
					gPackagedChecked = true;
				}
				return gPackaged;
			}
		}

		const int ErrorInsufficientBuffer = 122;

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

		static bool DetectPackaged()
		{
			try{
				// With a null buffer the call fails with ERROR_INSUFFICIENT_BUFFER when the process
				// has a package, and with APPMODEL_ERROR_NO_PACKAGE when it has not. See
				// https://learn.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname
				int length = 0;
				return GetCurrentPackageFullName(ref length, null) == ErrorInsufficientBuffer;
			}
			catch(EntryPointNotFoundException){
				// Windows 7: no package support at all.
				return false;
			}
		}

		public static string LogFolder{
			get{
				return Path.Combine(DataFolder, "logs");
			}
		}

		public static string ProgramFolder{
			get{
				return AppDomain.CurrentDomain.BaseDirectory;
			}
		}

		public static string DataFile(string fileName)
		{
			return Path.Combine(DataFolder, fileName);
		}

		public static string ProgramFile(string fileName)
		{
			return Path.Combine(ProgramFolder, fileName);
		}

		/// <summary>
		/// Creates the data folder and copies settings and user dictionaries that
		/// older versions saved next to the program. Never throws; failures are logged.
		/// </summary>
		public static void Prepare()
		{
			try{
				Directory.CreateDirectory(DataFolder);
			}
			catch(Exception ee){
				ErrorLog.Write(ee);
				return;
			}
			foreach(string name in gKonaHojjetler){
				try{
					MigrateFile(name, ProgramFolder, DataFolder);
				}
				catch(Exception ee){
					ErrorLog.Write(ee);
				}
			}
			try{
				ErrorLog.DeleteOldLogs(LogFolder, DateTime.Now.AddDays(-30));
			}
			catch(Exception ee){
				ErrorLog.Write(ee);
			}
		}

		/// <summary>
		/// Copies oldFolder\fileName to newFolder\fileName when the new file does not
		/// exist yet and the old one does. The old file is left in place.
		/// Returns true when a file was copied.
		/// </summary>
		public static bool MigrateFile(string fileName, string oldFolder, string newFolder)
		{
			string oldPath = Path.Combine(oldFolder, fileName);
			string newPath = Path.Combine(newFolder, fileName);
			if(File.Exists(newPath) || !File.Exists(oldPath)){
				return false;
			}
			if(string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase)){
				return false;
			}
			Directory.CreateDirectory(newFolder);
			File.Copy(oldPath, newPath, false);
			return true;
		}
	}

	/// <summary>
	/// Appends errors to %AppData%\UyghurEditPP\logs\error-yyyyMMdd.log.
	/// </summary>
	public static class ErrorLog
	{
		static readonly object gLock = new object();

		public static string FileName{
			get{
				return Path.Combine(AppPaths.LogFolder, "error-" + DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".log");
			}
		}

		/// <summary>
		/// Deletes error-*.log files in folder last written before olderThan.
		/// Returns how many were deleted.
		/// </summary>
		public static int DeleteOldLogs(string folder, DateTime olderThan)
		{
			if(!Directory.Exists(folder)){
				return 0;
			}
			int count = 0;
			foreach(string f in Directory.GetFiles(folder, "error-*.log")){
				if(File.GetLastWriteTime(f) < olderThan){
					File.Delete(f);
					count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Writes the exception to the log file. Never throws.
		/// Returns the log file name, or null when it could not be written.
		/// </summary>
		public static string Write(Exception ex)
		{
			System.Diagnostics.Debug.WriteLine(ex);
			try{
				lock(gLock){
					string fileName = FileName;
					Directory.CreateDirectory(Path.GetDirectoryName(fileName));
					string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine;
					File.AppendAllText(fileName, text, Encoding.UTF8);
					return fileName;
				}
			}
			catch(Exception ee){
				System.Diagnostics.Debug.WriteLine(ee);
				return null;
			}
		}
	}
}
