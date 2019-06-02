using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BoringConcurrency.Common
{
    public static class InterlockedEnum
    {
        private static void AssertInt<T>() where T : Enum
        {
            Type typeOfEnum = Enum.GetUnderlyingType(typeof(T));
            bool isIntOrLong = typeOfEnum == typeof(int);
            Debug.Assert(isIntOrLong, "Only supports int");
        }

        public static T CompareExchange<T>(ref T location1, T value, T comparand) where T : Enum
        {
#if DEBUG
            AssertInt<T>();
#endif

            int returnValue = Interlocked.CompareExchange(
                location1: ref Unsafe.As<T, int>(ref location1)
                , value: Unsafe.As<T, int>(ref value)
                , comparand: Unsafe.As<T, int>(ref comparand)
            );
            return Unsafe.As<int, T>(ref returnValue);
        }

        public static T Exchange<T>(ref T location1, T value) where T : Enum
        {
#if DEBUG
            AssertInt<T>();
#endif

            int returnValue = Interlocked.Exchange(
                location1: ref Unsafe.As<T, int>(ref location1)
                , value: Unsafe.As<T, int>(ref value)
            );
            return Unsafe.As<int, T>(ref returnValue);
        }
    }
}
