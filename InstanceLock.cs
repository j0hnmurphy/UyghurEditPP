/*
 * Tells the first window of the user's Windows session from the windows started while it
 * runs (for example by opening a file in Explorer). Only the first one reopens and saves the
 * list of open files, so that two windows do not open the same files and save over each other.
 *
 * A named mutex in the Local\ namespace is seen only in the session that created it, so
 * programs of other users (or other sessions) on the same PC do not count:
 * https://learn.microsoft.com/en-us/windows/win32/termserv/kernel-object-namespaces
 */
using System;
using System.Threading;

namespace UyghurEditPP
{
	public sealed class InstanceLock : IDisposable
	{
		public const string DefaultName = @"Local\UyghurEditPP.FirstWindow";

		Mutex gMutex;

		InstanceLock(Mutex mutex)
		{
			gMutex = mutex;
		}

		/// <summary>
		/// The lock, when no other program of this session holds it; otherwise null. Keep it
		/// while the window is open, and dispose of it on the same thread.
		/// </summary>
		public static InstanceLock TryTake(string name)
		{
			Mutex mutex;
			try{
				mutex = new Mutex(false, name);
			}
			catch(UnauthorizedAccessException ee){
				// Created by a program running as administrator, which this one may not open:
				// that program is the first window.
				ErrorLog.Write(ee);
				return null;
			}
			bool owned;
			try{
				owned = mutex.WaitOne(0);
			}
			catch(AbandonedMutexException){
				owned = true;   // the first program ended without letting go (it crashed): ours now
			}
			if(!owned){
				mutex.Dispose();
				return null;
			}
			return new InstanceLock(mutex);
		}

		public void Dispose()
		{
			if(gMutex != null){
				gMutex.ReleaseMutex();
				gMutex.Dispose();
				gMutex = null;
			}
		}
	}
}
