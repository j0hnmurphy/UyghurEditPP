using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class InstanceLockTests
	{
		// A mutex can be taken again by the thread that holds it, so each "program" is a thread.
		static T OnThread<T>(Func<T> f)
		{
			T result = default(T);
			Thread t = new Thread(() => result = f());
			t.Start();
			t.Join();
			return result;
		}

		static string NewName()
		{
			return @"Local\UyghurEditPP.Tests." + Guid.NewGuid().ToString("N");
		}

		[TestMethod]
		public void OnlyTheFirstGetsTheLock_UntilItLetsGo()
		{
			string name = NewName();
			InstanceLock first = InstanceLock.TryTake(name);
			Assert.IsNotNull(first);
			Assert.IsNull(OnThread(() => InstanceLock.TryTake(name)), "second window");
			first.Dispose();
			InstanceLock third = OnThread(() => InstanceLock.TryTake(name));
			Assert.IsNotNull(third, "a window started after the first closed");
		}

		[TestMethod]
		public void ASecondWindowThatChecked_DoesNotBlockALaterOne()
		{
			string name = NewName();
			InstanceLock first = InstanceLock.TryTake(name);
			Assert.IsNull(OnThread(() => InstanceLock.TryTake(name)));
			first.Dispose();
			// The second window is still open, but it does not hold the mutex.
			Assert.IsNotNull(OnThread(() => InstanceLock.TryTake(name)));
		}

		[TestMethod]
		public void AProgramThatEndedWithoutLettingGo_LeavesTheLockFree()
		{
			string name = NewName();
			// The thread ends while it owns the mutex, as a crashed program would.
			OnThread(() => InstanceLock.TryTake(name));
			Assert.IsNotNull(InstanceLock.TryTake(name));
		}
	}
}
